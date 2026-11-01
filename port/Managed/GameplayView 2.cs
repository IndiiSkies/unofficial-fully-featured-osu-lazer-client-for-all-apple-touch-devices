using System;

namespace OsuLegacy
{
    public sealed class GameplayView
    {
        public double Scale=1, OriginX, OriginY;
        public double BackgroundDim=0.35;
        private IUiSurface draw;
        private void Text(double x,double y,string value,double size)
        { draw.Text(x,y,value,size,0.95,0.96,1); }
        private void Circle(PathPoint point,double radius,double r,double g,double b,double alpha,bool fill)
        { draw.Circle(OriginX+point.X*Scale,OriginY+point.Y*Scale,radius*Scale,r,g,b,alpha,fill); }
        public PathPoint ToPlayfield(double x,double y) { return new PathPoint((x-OriginX)/Scale,(y-OriginY)/Scale); }
        public void Draw(IUiSurface surface,Session session,double time,int width,int height)
        {
            draw=surface;
            bool background=surface.Background(session.Map.BackgroundPath,false,BackgroundDim);
            if(background)
            {
                surface.Rect(0,0,width,59,0.02,0.015,0.035,0.65,0);
            }
            session.Advance(time);
            Scale=Math.Min((width-24.0)/512,(height-92.0)/384);
            OriginX=(width-512*Scale)/2;OriginY=68+(height-92-384*Scale)/2;
            Text(12,8,"< Back    "+session.Map.Title,15);
            Text(12,34,session.Combo+"x   "+session.Accuracy.ToString("F2")+"%   "+session.LastResult,15);
            for(int i=session.VisibleEnd(time)-1;i>=session.FirstPending;i--)
            {
                Circle obj=session.Map.Objects[i];if(obj.Judged)continue;
                if(obj.Kind==ObjectKind.Spinner){DrawSpinner(session,obj,time);continue;}
                PathPoint head=new PathPoint(obj.X,obj.Y);
                if(obj.Kind==ObjectKind.Slider)
                {
                    surface.Polyline(obj.Path.Coordinates,obj.Path.Points.Count,OriginX,OriginY,Scale,session.Radius*2+3,0.85,0.88,0.96,0.9);
                    surface.Polyline(obj.Path.Coordinates,obj.Path.Points.Count,OriginX,OriginY,Scale,session.Radius*2-3,0.24,0.14,0.38,0.96);
                    int currentSpan=obj.SpanDuration<=0?0:Math.Max(0,Math.Min(obj.SpanCount-1,(int)((time-obj.Time)/obj.SpanDuration)));
                    foreach(SliderEvent e in obj.Events)if(e.Span==currentSpan && !e.Judged && !e.Tail && !e.Repeat)Circle(obj.Path.Position(e.Progress),4,1,1,1,0.8,true);
                    if(time>=obj.Time && time<=obj.EndTime)
                    {
                        PathPoint ball=obj.PositionAt(time);
                        Circle(ball,session.Radius*2.4,session.Tracking(obj,time)?0.5:0.95,session.Tracking(obj,time)?1:0.65,0.85,0.45,false);
                        Circle(ball,session.Radius*0.75,1,0.65,0.82,1,true);
                        Circle(ball,session.Radius*0.75,1,1,1,1,false);
                    }
                    if(obj.SpanDuration>0)
                    {
                        int span=Math.Max(0,Math.Min(obj.SpanCount-1,(int)((time-obj.Time)/obj.SpanDuration)));
                        if(span<obj.SpanCount-1)
                        {
                            double[] arrow=span%2==0?obj.Path.ReverseAtEnd:obj.Path.ReverseAtStart;
                            if(arrow.Length>0)surface.Polyline(arrow,3,OriginX,OriginY,Scale,4,1,1,1,1);
                        }
                    }
                }
                if(!obj.HeadJudged)
                {
                    Circle(head,session.Radius,0.95,0.25,0.52,0.9,true);
                    Circle(head,session.Radius,1,1,1,1,false);
                    double remaining=obj.Time-time;
                    if(remaining>0)Circle(head,session.Radius*(1+3*remaining/session.Preempt),1,1,1,0.7,false);
                    Text(OriginX+head.X*Scale-4,OriginY+head.Y*Scale-8,(i+1).ToString(),12);
                }
            }
            if(session.Finished)Text(14,height-23,"Complete / max combo "+session.MaxCombo+" / bonus "+session.BonusScore,13);
        }
        private void DrawSpinner(Session session,Circle spinner,double time)
        {
            double cx=OriginX+256*Scale,cy=OriginY+192*Scale,radius=135*Scale;
            draw.Circle(cx,cy,radius,0.19,0.12,0.30,0.95,true);
            draw.Circle(cx,cy,radius,0.95,0.35,0.65,1,false);
            double approach=1-Math.Max(0,Math.Min(1,(time-spinner.Time)/spinner.Duration));
            draw.Circle(cx,cy,radius*(0.35+0.65*approach),1,1,1,0.55,false);
            draw.Arc(cx,cy,radius+5,Math.Min(1,session.SpinnerProgress(spinner)),5,1,0.65,0.82,1);
            double angle=spinner.VisualRotation;
            draw.Circle(cx+Math.Cos(angle)*radius*0.7,cy+Math.Sin(angle)*radius*0.7,6,1,1,1,1,true);
            Text(cx-31,cy-21,time<spinner.Time?"Get ready":"SPIN!",18);
            if(session.BonusSpins(spinner)>0)Text(cx-40,cy+34,"bonus "+session.BonusSpins(spinner)*1000,11);
            Text(cx-28,cy+10,Math.Floor(spinner.Rotation/(2*Math.PI)+0.0000001)+" / "+session.SpinsRequired(spinner),14);
        }
    }
}
