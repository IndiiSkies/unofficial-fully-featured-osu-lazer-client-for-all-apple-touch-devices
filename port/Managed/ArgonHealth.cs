using System;using System.Collections.Generic;
namespace OsuLegacy {
    // ArgonBarPathUtils: 300px width, 30px bar height + 20px path padding.
    public sealed class ArgonHealthPath {
        public readonly double[] Points;private readonly double[] distances;private readonly double length;
        public ArgonHealthPath(){
            var points=new List<double>();points.Add(10);points.Add(10);points.Add(210);points.Add(10);
            double radius=10*(1+Math.Sqrt(2));
            for(int i=1;i<=8;i++){double a=(-90+45.0*i/8)*Math.PI/180;points.Add(210+radius*Math.Cos(a));points.Add(10+radius+radius*Math.Sin(a));}
            points.Add(250-10/Math.Sqrt(2));points.Add(40-10/Math.Sqrt(2));
            for(int i=1;i<=8;i++){double a=(135-45.0*i/8)*Math.PI/180;points.Add(260+radius*Math.Cos(a));points.Add(40-radius+radius*Math.Sin(a));}
            points.Add(290);points.Add(40);Points=points.ToArray();distances=new double[Points.Length/2];
            for(int i=1;i<distances.Length;i++){double dx=Points[2*i]-Points[2*i-2],dy=Points[2*i+1]-Points[2*i-1];distances[i]=distances[i-1]+Math.Sqrt(dx*dx+dy*dy);}length=distances[distances.Length-1];
        }
        public int CopyRange(double start,double end,double[] output){
            start=Math.Max(0,Math.Min(1,start))*length;end=Math.Max(0,Math.Min(1,end))*length;if(end<=start)return 0;int count=0;
            for(int i=1;i<distances.Length;i++){
                double lo=Math.Max(start,distances[i-1]),hi=Math.Min(end,distances[i]);if(hi<=lo)continue;
                if(count==0){double t=(lo-distances[i-1])/(distances[i]-distances[i-1]);output[count*2]=Points[2*i-2]+(Points[2*i]-Points[2*i-2])*t;output[count*2+1]=Points[2*i-1]+(Points[2*i+1]-Points[2*i-1])*t;count++;}
                double u=(hi-distances[i-1])/(distances[i]-distances[i-1]);output[count*2]=Points[2*i-2]+(Points[2*i]-Points[2*i-2])*u;output[count*2+1]=Points[2*i-1]+(Points[2*i+1]-Points[2*i-1])*u;count++;
            }return count;
        }
    }
    public sealed class ArgonHealthAnimation {
        public double Value {get;private set;}public double Trail {get;private set;}public bool Damage {get;private set;}
        private double previousTime=Double.NaN,damageAt,damageFrom;
        public void Update(double health,bool missed,double time){
            health=Math.Max(0,Math.Min(1,health));
            if(Double.IsNaN(previousTime) || time<previousTime){Value=Trail=health;Damage=false;previousTime=time;return;}
            if(missed && health<Value){damageAt=time;Damage=true;Trail=Math.Max(Trail,Value);damageFrom=Trail;}
            double dt=Math.Max(0,time-previousTime);previousTime=time;Value=health+(Value-health)*Math.Pow(.5,dt/50);
            if(Damage && health>=Trail)Damage=false;
            if(Damage && time-damageAt>=500){double t=Math.Min(1,(time-damageAt-500)/300);Trail=health+(damageFrom-health)*Math.Pow(1-t,5);if(t>=1){Trail=health;Damage=false;}}
            else if(!Damage)Trail=health+(Trail-health)*Math.Pow(.5,dt/50);
        }
    }
}
