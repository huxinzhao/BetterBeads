namespace BetterBeads.Data;

/// <summary>Frame-rate independent, bounded transition for a button's hover and press feedback.</summary>
public readonly record struct ButtonMotion(float Hover,float Press,float Spring=0,float Velocity=0)
{
    public ButtonMotion Step(bool hovered,bool pressed,bool enabled,double seconds)
    {
        if(!enabled)return default;
        double dt=Math.Clamp(seconds,0,0.1);
        if(dt==0)return this;
        float blend=(float)(1-Math.Exp(-dt*18));
        // Analytic damped spring: same response at different frame rates, with a small overshoot.
        double target=(hovered?0.35:0)+(pressed?0.65:0),omega=28,damping=0.55;
        double decay=omega*damping,frequency=omega*Math.Sqrt(1-damping*damping);
        double delta=Spring-target,a=delta,b=(Velocity+decay*delta)/frequency;
        double cos=Math.Cos(frequency*dt),sin=Math.Sin(frequency*dt),e=Math.Exp(-decay*dt);
        float spring=(float)(target+e*(a*cos+b*sin));
        float velocity=(float)(e*((-decay*a+frequency*b)*cos+(-decay*b-frequency*a)*sin));
        return new(Hover+((hovered?1:0)-Hover)*blend,Press+((pressed?1:0)-Press)*blend,
            Math.Clamp(spring,-0.2f,1.25f),Math.Clamp(velocity,-40,40));
    }
    public UiRect Face(UiRect bounds,bool selected=false,int minimumHeight=0,bool reducedMotion=false)
    {
        int spread=Math.Clamp((int)Math.Round(3*Spring),-1,3),squash=Math.Clamp((int)Math.Round(4*Spring),-1,4);
        if(reducedMotion){spread=0;squash=0;}
        squash=Math.Min(squash,Math.Max(0,bounds.Height-4-minimumHeight));
        int depth=selected?3:Math.Clamp((int)Math.Round(3*Press),0,3);
        return new(bounds.X-spread,bounds.Y+depth+squash/2,Math.Max(1,bounds.Width+2*spread),Math.Max(1,bounds.Height-4-squash));
    }
    public int Offset => (int)Math.Round(1-Hover+2*Press);
}
