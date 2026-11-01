using System;using System.Collections.Generic;
namespace OsuLegacy {
    // Upstream BreakPeriod / BreakTracker / BreakOverlay constants and timing.
    public sealed class BreakTimeline {
        public const double MinimumDuration=650,FadeDuration=325;
        private readonly List<double[]> periods=new List<double[]>();
        public BreakTimeline(IEnumerable<double[]> source){foreach(var period in source)if(period!=null && period.Length>=2 && !Double.IsNaN(period[0]) && !Double.IsNaN(period[1]) && !Double.IsInfinity(period[0]) && !Double.IsInfinity(period[1]) && period[1]-period[0]>=MinimumDuration)periods.Add(new double[]{period[0],period[1]});periods.Sort(delegate(double[] a,double[] b){return a[0].CompareTo(b[0]);});}
        public bool TryGet(double time,out double start,out double end){foreach(var period in periods){if(period[0]>time)break;if(time>=period[0] && time<period[1]){start=period[0];end=period[1];return true;}}start=end=0;return false;}
        public bool Active(double time){double start,end;return TryGet(time,out start,out end) && time<=end-FadeDuration;}
        public double DrainDuration(double from,double to){
            if(to<=from)return 0;double duration=to-from,cursor=from;
            foreach(var period in periods){double left=Math.Max(cursor,period[0]),right=Math.Min(to,period[1]-FadeDuration);if(right>left){duration-=right-left;cursor=right;}if(period[0]>=to)break;}
            return Math.Max(0,duration);
        }
        public static int Seconds(double end,double time){return Math.Max(0,(int)Math.Ceiling((end-time)/1000));}
        public static double Alpha(double start,double end,double time){return Math.Max(0,Math.Min(1,Math.Min((time-start)/FadeDuration,(end-time)/FadeDuration)));}
        public static double RemainingBar(double start,double end,double time){return Math.Max(0,Math.Min(1,(end-time-2*FadeDuration)/(end-start-FadeDuration)));}
    }
}
