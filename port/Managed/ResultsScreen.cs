using System;
using System.Globalization;

namespace OsuLegacy
{
    // Player.RESULTS_DISPLAY_DELAY: judge everything, then leave one second
    // for the final object/feedback before allowing the results transition.
    public sealed class GameplayCompletion
    {
        private readonly Session session;
        private readonly double lastEnd;
        private double completedAt=-1;
        public GameplayCompletion(Session session){this.session=session;foreach(Circle obj in session.Map.Objects)lastEnd=Math.Max(lastEnd,obj.EndTime);}
        public bool Ready(double time,bool paused,bool failed){
            if(failed || !session.Finished || session.Map.Objects.Count==0){completedAt=-1;return false;}
            if(paused)return false;
            if(completedAt<0)completedAt=time;
            return time>=Math.Max(lastEnd,completedAt)+1000;
        }
    }

    public sealed class ScoreResult
    {
        public readonly string Title,Artist,Creator,Difficulty,Background,Mods,Rank;
        public readonly int Score,MaxCombo,PossibleCombo,Great,Ok,Meh,Miss,SliderHit,SliderMiss,Bonus,Misclicks;
        public readonly int PreviousBest,TimingCount;
        public readonly double Accuracy,AverageError,UnstableRate,HitWindow;
        public readonly bool NewBest,FullCombo,Silver;
        public readonly bool Taiko;
        public readonly bool Catch;
        public readonly bool Mania;
        public readonly int Perfect,Good,HoldBreaks;
        public readonly int TinyMiss,BananaHit,BananaMiss,FruitMiss,DropletMiss;
        public readonly DateTime PlayedAt;
        public readonly int[] Histogram=new int[32];
        public readonly double[] AccuracyGraph=new double[64];
        public ScoreResult(Session session,string mods,LocalScore previous){
            if(!session.Finished)throw new InvalidOperationException("Results require a completed play.");
            Beatmap map=session.Map;Title=map.Title;Artist=map.Artist;Creator=map.Creator;Difficulty=map.Difficulty;Background=map.BackgroundPath;
            Taiko=map.Mode==1;
            Catch=session.Catch!=null;
            Mania=session.Mania!=null;
            if(Mania){Perfect=session.Mania.Counts[6];Good=session.Mania.Counts[4];HoldBreaks=session.Mania.HoldBreaks;}
            if(Catch){TinyMiss=session.Catch.TinyMiss;BananaHit=session.Catch.BananaHit;BananaMiss=session.Catch.BananaMiss;FruitMiss=session.Catch.FruitMiss;DropletMiss=session.Catch.DropletMiss;}
            Mods=String.IsNullOrEmpty(mods)?"None":mods;
            Score=session.TotalScore;Accuracy=session.Accuracy;Rank=Mania?session.Mania.Rank:Catch?LocalScoreStore.CatchRank(Accuracy):LocalScoreStore.Rank(Accuracy,session.Miss);MaxCombo=session.MaxCombo;
            Silver=(Rank=="SS" || Rank=="S") && ((","+Mods.Replace(" ","")+",").Contains(",HD,") || (","+Mods.Replace(" ","")+",").Contains(",FL,"));
            Great=session.Great;Ok=session.Ok;Meh=session.Meh;Miss=session.Miss;SliderHit=session.SliderTicksHit;SliderMiss=session.SliderTicksMissed;Bonus=session.BonusScore;Misclicks=session.Misclicks;
            if(Mania){Great=session.Mania.Counts[5];Ok=session.Mania.Counts[3];}
            if(Mania)PossibleCombo=session.Mania.PossibleCombo;
            else if(Catch)PossibleCombo=session.Catch.PossibleCombo;
            else foreach(Circle obj in map.Objects)PossibleCombo+=Taiko?(obj.Kind==ObjectKind.Circle?1:0):obj.Kind==ObjectKind.Slider?1+obj.Events.Count:1;
            FullCombo=PossibleCombo>0 && Miss==0 && SliderMiss==0 && MaxCombo==PossibleCombo;
            PreviousBest=previous==null?-1:previous.Score;NewBest=LocalScoreStore.IsBetter(Score,Accuracy,MaxCombo,previous);PlayedAt=DateTime.Now;
            HitWindow=Mania?session.Mania.Windows[1]*1.5:Taiko?session.TaikoOkWindow:session.Windows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Meh);
            double mean=0,squares=0;int count=0;
            foreach(Session.HitTiming hit in session.AllHitTimings){count++;double nextMean=mean+(hit.Offset-mean)/count;squares+=(hit.Offset-mean)*(hit.Offset-nextMean);mean=nextMean;
                int bin=(int)((hit.Offset+HitWindow)/(2*HitWindow)*Histogram.Length);Histogram[Math.Max(0,Math.Min(Histogram.Length-1,bin))]++;}
            TimingCount=count;AverageError=count==0?0:mean;UnstableRate=count==0?0:10*Math.Sqrt(Math.Max(0,squares/count));
            // Resample whole-play primary judgements into a bounded graph.
            int processed=0,points=0;
            for(int i=0;i<AccuracyGraph.Length;i++){
                int until=(int)Math.Ceiling((i+1.0)/AccuracyGraph.Length*session.ScoreHistory.Count);
                while(processed<until){string result=session.ScoreHistory[processed++].Result;points+=result=="305"?305:result=="300"?300:result=="200"?200:result=="150"?150:result=="100"?100:result=="50"?50:0;}
                AccuracyGraph[i]=processed==0?1:points/((Mania?305.0:300.0)*processed);
            }
        }
    }

    public enum ResultsAction { None, Back, Retry, Statistics }
    // Compact adaptation of the expanded lazer score panel, not a gameplay HUD
    // overlay. Existing GPU primitives render the entire scene and its graphs.
    public sealed class ResultsScreen
    {
        public readonly ScoreResult Score;
        private readonly double enteredAt;
        private readonly bool animate;
        private bool rankSoundPlayed,applausePlayed;
        private IUiSurface fittedSurface;
        private string fittedTitle,fittedArtist,fittedDifficulty;
        public bool Statistics {get;private set;}
        public ResultsScreen(ScoreResult score,double now,bool animate){Score=score;enteredAt=now;this.animate=animate;}
        public bool IsAnimating(double now){return animate && now-enteredAt<3.65;}
        public string RankImpactSample {get {return "results-rank-impact-"+(Score.Rank=="SS"?"pass-ss":Score.Rank=="S"||Score.Rank=="A"?"pass":Score.Rank=="D"?"fail-d":"fail")+".wav";}}
        public string ApplauseSample {get {return "results-applause-"+(Score.Rank=="SS"?"s":Score.Rank.ToLowerInvariant())+".wav";}}
        public int TakeSoundCues(double now){int cues=0;double elapsed=now-enteredAt;
            if(!rankSoundPlayed && elapsed>=(animate?1.95:0)){rankSoundPlayed=true;cues|=1;}
            if(!applausePlayed && elapsed>=(animate?2.495:.545)){applausePlayed=true;cues|=2;}
            return cues;
        }
        public ResultsAction Tap(double x,double y){
            if(y<281 || y>=315)return ResultsAction.None;
            if(x>=12 && x<119)return ResultsAction.Back;
            if(x>=127 && x<293){Statistics=!Statistics;return ResultsAction.Statistics;}
            if(x>=351 && x<468)return ResultsAction.Retry;
            return ResultsAction.None;
        }
        private static string Clip(string value,int length){value=value??"";return value.Length>length?value.Substring(0,length-3)+"...":value;}
        private static string Fit(IUiSurface draw,string value,double width,double size){
            value=value??"";ITextWidthSurface measure=draw as ITextWidthSurface;
            if(measure==null)return Clip(value,Math.Max(4,(int)(width/(size*.65))));
            if(measure.TextWidth(value,size)<=width)return value;
            int low=0,high=value.Length;while(low<high){int middle=(low+high+1)/2;if(measure.TextWidth(value.Substring(0,middle)+"...",size)<=width)low=middle;else high=middle-1;}
            if(low>0 && low<value.Length && Char.IsHighSurrogate(value[low-1]) && Char.IsLowSurrogate(value[low]))low--;
            return value.Substring(0,low)+"...";
        }
        private static void Text(IUiSurface draw,double x,double y,string value,double size){draw.Text(x,y,value,size,.96,.95,1);}
        private static void Label(IUiSurface draw,double x,double y,string value){draw.Text(x,y,value,8,.70,.67,.77);}
        // OsuColour.ForRank from the attached client, not legacy gold/blue ranks.
        private static void RankColour(string rank,out double r,out double g,out double b){int colour=rank=="SS"?0xde31ae:rank=="S"?0x02b5c3:rank=="A"?0x88da20:rank=="B"?0xe3b130:rank=="C"?0xff8e5d:0xff5a5a;r=((colour>>16)&255)/255.0;g=((colour>>8)&255)/255.0;b=(colour&255)/255.0;}
        private static void GradedArc(IUiSurface draw,double start,double end,string rank,double reveal){
            start+=1.0/360;end=Math.Min(end-1.0/360,reveal);if(end<=start)return;
            int count=Math.Max(2,(int)Math.Ceiling((end-start)*64));double[] points=new double[(count+1)*2];
            for(int i=0;i<=count;i++){double angle=-Math.PI/2+(start+(end-start)*i/count)*Math.PI*2;points[i*2]=Math.Cos(angle)*44;points[i*2+1]=Math.Sin(angle)*44;}
            double r,g,b;RankColour(rank,out r,out g,out b);draw.Polyline(points,count+1,87,148,1,4,r,g,b,1);
        }
        public void Draw(IUiSurface draw,double now){
            if(fittedSurface!=draw){fittedSurface=draw;fittedTitle=Fit(draw,Score.Title,343,15);fittedArtist=Fit(draw,Score.Artist,354,10);fittedDifficulty=Fit(draw,Score.Difficulty+"  /  mapped by "+Score.Creator,355,8);}
            draw.Background(Score.Background,false,.65);
            draw.Rect(0,0,480,65,.07,.055,.10,.96,0);
            if(Score.Background!=null)draw.Artwork(Score.Background,12,10,37,43,1);
            Text(draw,59,7,fittedTitle,15);Text(draw,59,28,fittedArtist,10);
            Label(draw,59,46,fittedDifficulty);Text(draw,426,45,"Guest",9);
            draw.Text(416,8,"results",13,1,.65,.82);
            draw.Rect(12,74,150,199,.07,.055,.11,.96,6);draw.Rect(172,74,296,199,.08,.06,.12,.96,6);
            double progress=animate?Math.Max(0,Math.Min(1,(now-enteredAt-.45)/3)):1;
            double eased=1-Math.Pow(1-progress,10);
            draw.Circle(87,148,53,.14,.11,.20,1,true);draw.Arc(87,148,56,1,7,.25,.20,.31,1);
            draw.Arc(87,148,56,Score.Accuracy/100*eased,7,.61,.98,.83,1);
            double rankProgress=animate?1-Math.Pow(1-Math.Max(0,Math.Min(1,(now-enteredAt-.15)/.8)),10):1;
            GradedArc(draw,0,.70,"D",rankProgress);GradedArc(draw,.70,.80,"C",rankProgress);GradedArc(draw,.80,.90,"B",rankProgress);GradedArc(draw,.90,.95,"A",rankProgress);GradedArc(draw,.95,.99,"S",rankProgress);GradedArc(draw,.99,1,"SS",rankProgress);
            if(!animate || now-enteredAt>=1.95)Text(draw,Score.Rank=="SS"?64:76,126,Score.Rank,34);
            Text(draw,50,213,(Score.Accuracy*eased).ToString("F2",CultureInfo.InvariantCulture)+"%",18);Label(draw,67,237,"accuracy");
            Label(draw,43,258,Score.FullCombo?"FULL COMBO":"BEATMAP CLEARED");
            if(Statistics)DrawStatistics(draw);
            else {
                Label(draw,184,85,"TOTAL SCORE");if(Score.NewBest)draw.Text(326,85,"NEW PERSONAL BEST",8,1,.62,.80);
                Text(draw,184,102,((int)Math.Round(Score.Score*eased)).ToString("N0",CultureInfo.InvariantCulture),34);
                Label(draw,184,143,"MAX COMBO");Text(draw,184,156,Score.MaxCombo+"x",18);Label(draw,255,166,"/ "+Score.PossibleCombo+"x");
                Label(draw,331,143,"MODS");Text(draw,331,160,Clip(Score.Mods,18),10);
                if(Score.Mania){Count(draw,184,191,"MAX",Score.Perfect,.7,1,1);Count(draw,231,191,"300",Score.Great,1,.85,.3);Count(draw,278,191,"200",Score.Good,.4,1,.62);Count(draw,325,191,"100",Score.Ok,.4,.7,1);Count(draw,372,191,"50",Score.Meh,1,.6,.3);Count(draw,419,191,"MISS",Score.Miss,1,.33,.52);}
                else {Count(draw,184,191,Score.Catch?"FRUIT":Score.Taiko?"GREAT":"300",Score.Great,.35,.8,1);Count(draw,255,191,Score.Catch?"DROPS":Score.Taiko?"GOOD":"100",Score.Ok,.40,1,.62);
                if(!Score.Taiko)Count(draw,326,191,Score.Catch?"TINY":"50",Score.Meh,1,.75,.3);Count(draw,397,191,"MISS",Score.Miss,1,.33,.52);}
                string previous=Score.PreviousBest<0?"First local score":("Best: "+Score.PreviousBest.ToString("N0",CultureInfo.InvariantCulture)+"   "+(Score.Score>=Score.PreviousBest?"+":"")+(Score.Score-Score.PreviousBest).ToString("N0",CultureInfo.InvariantCulture));
                Label(draw,184,242,previous);Label(draw,184,258,Score.PlayedAt.ToString("yyyy-MM-dd HH:mm")+"   Local play");
            }
            Button(draw,12,282,107,"< back",.28,.23,.37);Button(draw,127,282,166,Statistics?"score summary":"statistics",.34,.24,.54);Button(draw,351,282,117,"retry",.82,.62,.14);
        }
        private static void Count(IUiSurface draw,double x,double y,string label,int value,double r,double g,double b){draw.Text(x,y,label,9,r,g,b);Text(draw,x,y+14,value.ToString(),18);}
        private static void Button(IUiSurface draw,double x,double y,double width,string label,double r,double g,double b){draw.Rect(x,y,width,32,r,g,b,1,5);Text(draw,x+12,y+8,label,12);}
        private void DrawStatistics(IUiSurface draw){
            if(Score.Catch){
                Label(draw,184,84,"CATCH STATISTICS");
                Text(draw,184,107,"Fruit: "+Score.Great+" caught / "+Score.FruitMiss+" missed",13);
                Text(draw,184,134,"Droplets: "+Score.Ok+" caught / "+Score.DropletMiss+" missed",13);
                Text(draw,184,161,"Tiny: "+Score.Meh+" caught / "+Score.TinyMiss+" missed",13);
                Text(draw,184,188,"Bananas: "+Score.BananaHit+" caught / "+Score.BananaMiss+" missed",13);
                Label(draw,184,221,"Tiny misses affect accuracy, not combo.");
                Label(draw,184,243,"Bananas award bonus score only.");return;
            }
            Label(draw,184,84,"TAP TIMING DISTRIBUTION");
            int peak=1;foreach(int count in Score.Histogram)peak=Math.Max(peak,count);
            draw.Rect(184,99,272,54,.04,.03,.07,1,0);
            for(int i=0;i<Score.Histogram.Length;i++){double height=43.0*Score.Histogram[i]/peak;draw.Rect(185+i*8.4,147-height,7,height,i<16?.42:1,i<16?.7:.65,i<16?1:.4,.9,0);}
            draw.Rect(319,100,1,48,1,1,1,.5,0);Label(draw,185,154,"early");Label(draw,435,154,"late");
            Label(draw,184,171,"AVERAGE ERROR");Text(draw,184,184,Score.TimingCount==0?"--":Score.AverageError.ToString("+0.0;-0.0;0.0",CultureInfo.InvariantCulture)+" ms",14);
            Label(draw,331,171,"UNSTABLE RATE");Text(draw,331,184,Score.TimingCount==0?"--":Score.UnstableRate.ToString("F1",CultureInfo.InvariantCulture),14);
            Label(draw,184,208,"ACCURACY THROUGH PLAY");double[] points=new double[Score.AccuracyGraph.Length*2];
            for(int i=0;i<Score.AccuracyGraph.Length;i++){points[i*2]=i*270.0/(Score.AccuracyGraph.Length-1);points[i*2+1]=(1-Score.AccuracyGraph[i])*21;}
            draw.Rect(184,221,272,26,.04,.03,.07,1,0);draw.Polyline(points,Score.AccuracyGraph.Length,185,223,1,1.25,.65,.42,1,1);
            Label(draw,184,257,Score.Mania?"Hold breaks: "+Score.HoldBreaks+"   Press and release timing included":Score.Taiko?"Roll / swell / large-note bonus: "+Score.Bonus:"Slider parts "+Score.SliderHit+" / "+(Score.SliderHit+Score.SliderMiss)+"   Bonus "+Score.Bonus+"   Extra taps "+Score.Misclicks);
        }
    }
}
