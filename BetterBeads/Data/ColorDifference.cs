namespace BetterBeads.Data;

public readonly record struct LabColor(double L,double A,double B);

public static class ColorDifference
{
    public static LabColor FromRgba(uint rgba)
    {
        static double Linear(double value)=>value<=0.04045?value/12.92:Math.Pow((value+0.055)/1.055,2.4);
        static double Axis(double value)=>value>216d/24389d?Math.Cbrt(value):(24389d/27d*value+16)/116;
        double r=Linear((byte)(rgba>>24)/255d),g=Linear((byte)(rgba>>16)/255d),b=Linear((byte)(rgba>>8)/255d);
        double x=Axis((0.4124564*r+0.3575761*g+0.1804375*b)/0.95047);
        double y=Axis(0.2126729*r+0.7151522*g+0.072175*b);
        double z=Axis((0.0193339*r+0.119192*g+0.9503041*b)/1.08883);
        return new(116*y-16,500*(x-y),200*(y-z));
    }

    public static double Ciede2000(LabColor first,LabColor second)
    {
        const double pow25=6103515625d;
        static double Rad(double degrees)=>degrees*Math.PI/180;
        double c1=Math.Sqrt(first.A*first.A+first.B*first.B),c2=Math.Sqrt(second.A*second.A+second.B*second.B);
        double averageC=(c1+c2)/2;
        double g=(1-Math.Sqrt(Math.Pow(averageC,7)/(Math.Pow(averageC,7)+pow25)))/2;
        double a1=(1+g)*first.A,a2=(1+g)*second.A;
        double cp1=Math.Sqrt(a1*a1+first.B*first.B),cp2=Math.Sqrt(a2*a2+second.B*second.B);
        double h1=(Math.Atan2(first.B,a1)*180/Math.PI+360)%360,h2=(Math.Atan2(second.B,a2)*180/Math.PI+360)%360;
        double deltaHue=h2-h1;
        if(cp1*cp2==0)deltaHue=0;
        else if(deltaHue>180)deltaHue-=360;
        else if(deltaHue< -180)deltaHue+=360;
        double deltaH=2*Math.Sqrt(cp1*cp2)*Math.Sin(Rad(deltaHue/2));
        double averageL=(first.L+second.L)/2,averageCp=(cp1+cp2)/2;
        double averageHue;
        if(cp1*cp2==0)averageHue=h1+h2;
        else if(Math.Abs(h1-h2)<=180)averageHue=(h1+h2)/2;
        else if(h1+h2<360)averageHue=(h1+h2+360)/2;
        else averageHue=(h1+h2-360)/2;
        double t=1-0.17*Math.Cos(Rad(averageHue-30))+0.24*Math.Cos(Rad(2*averageHue))
            +0.32*Math.Cos(Rad(3*averageHue+6))-0.20*Math.Cos(Rad(4*averageHue-63));
        double deltaTheta=30*Math.Exp(-Math.Pow((averageHue-275)/25,2));
        double rc=2*Math.Sqrt(Math.Pow(averageCp,7)/(Math.Pow(averageCp,7)+pow25));
        double sl=1+0.015*Math.Pow(averageL-50,2)/Math.Sqrt(20+Math.Pow(averageL-50,2));
        double sc=1+0.045*averageCp,sh=1+0.015*averageCp*t,rt=-Math.Sin(Rad(2*deltaTheta))*rc;
        double dl=(second.L-first.L)/sl,dc=(cp2-cp1)/sc,dh=deltaH/sh;
        return Math.Sqrt(dl*dl+dc*dc+dh*dh+rt*dc*dh);
    }
}
