// Curve approximation adapted from osu.Framework.Utils.PathApproximator.
// Original algorithms: ppy Pty Ltd, MIT. See ThirdParty/gameplay-provenance.md.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OsuLegacy
{
    public struct PathPoint
    {
        public double X, Y;
        public PathPoint(double x, double y) { X = x; Y = y; }
        public static PathPoint operator +(PathPoint a, PathPoint b) { return new PathPoint(a.X+b.X,a.Y+b.Y); }
        public static PathPoint operator -(PathPoint a, PathPoint b) { return new PathPoint(a.X-b.X,a.Y-b.Y); }
        public static PathPoint operator *(PathPoint a, double n) { return new PathPoint(a.X*n,a.Y*n); }
        public double Length { get { return Math.Sqrt(X*X+Y*Y); } }
    }

    public sealed class SliderPath
    {
        public readonly List<PathPoint> Points = new List<PathPoint>();
        private readonly List<double> distances = new List<double>();
        public double Length { get; private set; }
        public double[] Coordinates, ReverseAtStart, ReverseAtEnd;

        public static SliderPath Parse(string definition, double x, double y, double expectedLength)
        {
            var path = new SliderPath();
            string[] tokens = definition.Split('|');
            string kind = tokens[0]; bool lastRepeated = false;
            List<PathPoint> control = new List<PathPoint>(); control.Add(new PathPoint(x,y));
            for (int i=1;i<tokens.Length;i++)
            {
                if (tokens[i].IndexOf(':') < 0)
                {
                    path.Append(kind,control); kind=tokens[i];
                    PathPoint last=control[control.Count-1]; control.Clear(); control.Add(last); continue;
                }
                string[] xy=tokens[i].Split(':');
                if (xy.Length!=2) throw new FormatException("Invalid slider control point");
                PathPoint point=new PathPoint(Beatmap.Number(xy[0]),Beatmap.Number(xy[1]));
                lastRepeated = control.Count > 0 && (point-control[control.Count-1]).Length == 0;
                // A repeated Bezier control point separates two curve segments.
                if (kind=="B" && control.Count>1 && (point-control[control.Count-1]).Length==0)
                { path.Append(kind,control); control.Clear(); }
                control.Add(point);
                if (control.Count>4096) throw new FormatException("Too many slider control points");
            }
            path.Append(kind,control);
            if (path.Points.Count==0) path.Points.Add(new PathPoint(x,y));
            path.FitLength(expectedLength, !lastRepeated);
            path.ReverseAtStart = path.Arrow(0,0.05);
            path.ReverseAtEnd = path.Arrow(1,0.95);
            return path;
        }

        private void Add(PathPoint p)
        {
            if (Points.Count==0 || (p-Points[Points.Count-1]).Length>0.000001) Points.Add(p);
            if (Points.Count>65536) throw new FormatException("Slider path is too complex");
        }
        private void Append(string kind, List<PathPoint> control)
        {
            if (control.Count==0) return;
            if (kind=="L") { foreach(PathPoint p in control) Add(p); return; }
            if (kind=="P" && control.Count==3 && Arc(control)) return;
            if (kind=="C")
            {
                for(int i=0;i<control.Count-1;i++)
                {
                    PathPoint b=control[i], c=control[i+1];
                    PathPoint a=i>0?control[i-1]:b, d=i+2<control.Count?control[i+2]:c*2-b;
                    for(int j=0;j<=50;j++)
                    {
                        double t=j/50.0,t2=t*t,t3=t*t2;
                        Add((b*2+(c-a)*t+(a*2-b*5+c*4-d)*t2+(a*(-1)+b*3-c*3+d)*t3)*0.5);
                    }
                }
                if (control.Count==1) Add(control[0]);
                return;
            }
            if (kind.Length>1 && kind[0]=='B')
            {
                int degree;
                if (!int.TryParse(kind.Substring(1),out degree) || degree<1) throw new FormatException("Invalid B-spline degree");
                BSpline(control,degree); return;
            }
            if (kind!="B" && kind!="P") throw new NotSupportedException("Unknown slider curve: "+kind);
            Bezier(control.ToArray());
        }
        private void Bezier(PathPoint[] points)
        {
            List<PathPoint[]> pending=new List<PathPoint[]>(); pending.Add(points);
            while(pending.Count>0)
            {
                PathPoint[] p=pending[pending.Count-1];pending.RemoveAt(pending.Count-1);int count=p.Length;
                bool flat=true;
                for(int i=1;i<count-1;i++) if ((p[i-1]-p[i]*2+p[i+1]).Length>0.5) { flat=false; break; }
                PathPoint[] left=new PathPoint[count],right=new PathPoint[count],mid=(PathPoint[])p.Clone();
                for(int i=0;i<count;i++)
                {
                    left[i]=mid[0];right[count-i-1]=mid[count-i-1];
                    for(int j=0;j<count-i-1;j++) mid[j]=(mid[j]+mid[j+1])*0.5;
                }
                if (flat)
                {
                    var joined=new List<PathPoint>(left);
                    for(int i=1;i<count;i++) joined.Add(right[i]);
                    Add(p[0]);
                    for(int i=1;i<count-1;i++) Add((joined[2*i-1]+joined[2*i]*2+joined[2*i+1])*0.25);
                    Add(p[count-1]);
                }
                else { pending.Add(right);pending.Add(left); }
            }
        }
        private bool Arc(List<PathPoint> p)
        {
            PathPoint a=p[0],b=p[1],c=p[2];
            double determinant=2*(a.X*(b.Y-c.Y)+b.X*(c.Y-a.Y)+c.X*(a.Y-b.Y));
            if (Math.Abs(determinant)<0.000001) return false;
            double aa=a.X*a.X+a.Y*a.Y,bb=b.X*b.X+b.Y*b.Y,cc=c.X*c.X+c.Y*c.Y;
            PathPoint center=new PathPoint((aa*(b.Y-c.Y)+bb*(c.Y-a.Y)+cc*(a.Y-b.Y))/determinant,
                (aa*(c.X-b.X)+bb*(a.X-c.X)+cc*(b.X-a.X))/determinant);
            double radius=(a-center).Length;
            double start=Math.Atan2(a.Y-center.Y,a.X-center.X),end=Math.Atan2(c.Y-center.Y,c.X-center.X);
            double direction=((b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X))>0?1:-1;
            double range=direction*(end-start);while(range<0)range+=Math.PI*2;
            int count=radius<=0.05?2:Math.Max(2,(int)Math.Ceiling(range/(2*Math.Acos(1-0.1/radius))));
            if(count>65536)throw new FormatException("Slider arc is too complex");
            for(int i=0;i<count;i++){double angle=start+direction*range*i/(count-1);Add(center+new PathPoint(Math.Cos(angle),Math.Sin(angle))*radius);}
            return true;
        }
        private void BSpline(List<PathPoint> control,int degree)
        {
            int n=control.Count-1;
            if(n<=0){Add(control[0]);return;}
            degree=Math.Min(degree,n);
            double[] knots=new double[n+degree+2];
            for(int i=0;i<knots.Length;i++) knots[i]=i<=degree?0:i>=n+1?1:(double)(i-degree)/(n-degree+1);
            int steps=Math.Min(32768,Math.Max(100,n*100));
            for(int step=0;step<=steps;step++)
            {
                double t=(double)step/steps; int k=degree;
                while(k<n && t>=knots[k+1])k++;
                PathPoint[] d=new PathPoint[degree+1];for(int j=0;j<=degree;j++)d[j]=control[k-degree+j];
                for(int r=1;r<=degree;r++)for(int j=degree;j>=r;j--)
                {
                    double denominator=knots[j+1+k-r]-knots[j+k-degree];
                    double alpha=denominator==0?0:(t-knots[j+k-degree])/denominator;
                    d[j]=d[j-1]*(1-alpha)+d[j]*alpha;
                }
                Add(d[degree]);
            }
        }
        private void FitLength(double expected, bool extend)
        {
            double length=0;
            for(int i=1;i<Points.Count;i++)
            {
                double segment=(Points[i]-Points[i-1]).Length;
                if(length+segment>expected)
                {
                    Points[i]=Points[i-1]+(Points[i]-Points[i-1])*((expected-length)/segment);
                    Points.RemoveRange(i+1,Points.Count-i-1);break;
                }
                length+=segment;
            }
            distances.Add(0);
            for(int i=1;i<Points.Count;i++) distances.Add(distances[i-1]+(Points[i]-Points[i-1]).Length);
            Length=distances[distances.Count-1];
            if(extend && Length<expected && Points.Count>1)
            {
                double extension=expected-Length;
                if(extension<=0.000001)
                {
                    // A sub-tolerance correction adjusts the existing endpoint's
                    // distance. Add() would discard its near-duplicate point;
                    // appending only a distance would corrupt the lookup table.
                    distances[distances.Count-1]=expected;
                }
                else
                {
                    if(Points.Count>=65536)throw new FormatException("Slider path is too complex");
                    PathPoint direction=Points[Points.Count-1]-Points[Points.Count-2];
                    Points.Add(Points[Points.Count-1]+direction*(extension/direction.Length));
                    distances.Add(expected);
                }
                Length=expected;
            }
            Coordinates=new double[Points.Count*2];
            for(int i=0;i<Points.Count;i++){Coordinates[i*2]=Points[i].X;Coordinates[i*2+1]=Points[i].Y;}
        }
        private double[] Arrow(double end,double inward)
        {
            PathPoint p=Position(end),direction=Position(inward)-p;
            double length=direction.Length;
            if(length==0)return new double[0];
            direction=direction*(1/length);
            PathPoint normal=new PathPoint(-direction.Y,direction.X);
            PathPoint a=p-direction*8+normal*9,b=p+direction*8,c=p-direction*8-normal*9;
            return new double[]{a.X,a.Y,b.X,b.Y,c.X,c.Y};
        }
        public PathPoint Position(double progress)
        {
            double distance=Math.Max(0,Math.Min(1,progress))*Length;
            int low=0,high=distances.Count-1;
            while(low<high){int middle=(low+high)/2;if(distances[middle]<distance)low=middle+1;else high=middle;}
            if(low==0)return Points[0];
            double length=distances[low]-distances[low-1];
            return length==0?Points[low]:Points[low-1]+(Points[low]-Points[low-1])*((distance-distances[low-1])/length);
        }
    }
}
