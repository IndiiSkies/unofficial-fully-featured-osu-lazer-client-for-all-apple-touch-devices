using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OsuLegacy
{
    // SoloGameplayLeaderboardProvider orders the tracked play below saved
    // scores on ties. Snapshot the target so saving results cannot move it.
    public sealed class SelfCompetition
    {
        public readonly bool HasBest;
        public readonly int BestScore,BestCombo;
        public readonly double BestAccuracy;
        public bool Passed {get;private set;}
        public double PassedAt {get;private set;}
        public SelfCompetition(LocalScore best) {
            HasBest=best!=null;PassedAt=-10000;
            if(best!=null){BestScore=best.Score;BestCombo=best.MaxCombo;BestAccuracy=best.Accuracy;}
        }
        public int Position {get {return !HasBest || Passed?1:2;}}
        public long Gap(int score){return (long)score-BestScore;}
        public void Update(int score,double time) {
            if(HasBest && !Passed && score>BestScore){Passed=true;PassedAt=time;}
        }
    }
    public sealed class LocalScore
    {
        public string Path, Rank;
        public int Score, MaxCombo, Great, Ok, Meh, Miss, Plays,Perfect,Good;
        public double Accuracy;
    }

    public sealed class LocalScoreStore
    {
        public readonly Dictionary<string,LocalScore> Scores=new Dictionary<string,LocalScore>(StringComparer.Ordinal);
        private readonly string path;
        public LocalScoreStore(string path) { this.path=path;Load(); }
        private static string B64(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value??"")); }
        private static string Text(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        private void Load()
        {
            if(!File.Exists(path))return;
            try { foreach(string line in File.ReadAllLines(path)) {
                string[] v=line.Split('\t');if(v.Length!=11 && v.Length!=13)continue;
                LocalScore s=new LocalScore {Path=Text(v[0]),Rank=v[1]};
                int.TryParse(v[2],out s.Score);double.TryParse(v[3],NumberStyles.Float,CultureInfo.InvariantCulture,out s.Accuracy);
                int.TryParse(v[4],out s.MaxCombo);int.TryParse(v[5],out s.Great);int.TryParse(v[6],out s.Ok);
                int.TryParse(v[7],out s.Meh);int.TryParse(v[8],out s.Miss);int.TryParse(v[9],out s.Plays);
                if(v.Length==13){int.TryParse(v[11],out s.Perfect);int.TryParse(v[12],out s.Good);}
                if(s.Rank=="S" && s.Accuracy>=100 && s.Miss==0)s.Rank="SS";
                Scores[s.Path]=s;
            }} catch { Scores.Clear(); }
        }
        public void Record(Session session)
        {
            if(!session.Finished)return;
            string key=session.ScoreKey;LocalScore old;Scores.TryGetValue(key,out old);
            int plays=old==null?1:old.Plays+1;
            int total=session.TotalScore;
            if(IsBetter(total,session.Accuracy,session.MaxCombo,old)) Scores[key]=new LocalScore {Path=key,Rank=session.Mania!=null?session.Mania.Rank:session.Catch!=null?CatchRank(session.Accuracy):Rank(session.Accuracy,session.Miss),Score=total,Accuracy=session.Accuracy,
                MaxCombo=session.MaxCombo,Great=session.Mania==null?session.Great:session.Mania.Counts[5],Ok=session.Mania==null?session.Ok:session.Mania.Counts[3],Meh=session.Meh,Miss=session.Miss,Plays=plays,Perfect=session.Mania==null?0:session.Mania.Counts[6],Good=session.Mania==null?0:session.Mania.Counts[4]};
            else old.Plays=plays;
            Save();
        }
        public static bool IsBetter(int score,double accuracy,int combo,LocalScore previous){return previous==null || score>previous.Score || (score==previous.Score && (accuracy>previous.Accuracy || (accuracy==previous.Accuracy && combo>previous.MaxCombo)));}
        public static string CatchRank(double accuracy){return accuracy>=100?"SS":accuracy>=98?"S":accuracy>=94?"A":accuracy>=90?"B":accuracy>=85?"C":"D";}
        // ScoreProcessor.RankFromScore + OsuScoreProcessor's miss restriction.
        public static string Rank(double accuracy,int misses) {if(accuracy>=100 && misses==0)return "SS";if(accuracy>=95 && misses==0)return "S";if(accuracy>=90)return "A";if(accuracy>=80)return "B";if(accuracy>=70)return "C";return "D";}
        private void Save()
        {
            List<string> lines=new List<string>();foreach(LocalScore s in Scores.Values)lines.Add(B64(s.Path)+"\t"+s.Rank+"\t"+s.Score+"\t"+
                s.Accuracy.ToString(CultureInfo.InvariantCulture)+"\t"+s.MaxCombo+"\t"+s.Great+"\t"+s.Ok+"\t"+s.Meh+"\t"+s.Miss+"\t"+s.Plays+"\t0\t"+s.Perfect+"\t"+s.Good);
            File.WriteAllLines(path,lines.ToArray());
        }
    }
}
