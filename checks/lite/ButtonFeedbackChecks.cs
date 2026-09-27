using BetterBeads.Data;
using System.Text.Json;

internal static class ButtonFeedbackChecks
{
    public static void Run(Action<bool,string> check)
    {
        ButtonMotion slow=default,fast=default;
        for(int i=0;i<30;i++)slow=slow.Step(true,true,true,1d/30);
        for(int i=0;i<120;i++)fast=fast.Step(true,true,true,1d/120);
        check(Math.Abs(slow.Spring-fast.Spring)<0.0001&&Math.Abs(slow.Hover-fast.Hover)<0.0001,"spring agrees at 30 and 120 fps");
        var rect=new UiRect(100,100,160,44);float lowest=1;
        for(int i=0;i<120;i++){fast=fast.Step(true,false,true,1d/120);lowest=Math.Min(lowest,fast.Spring);}
        check(lowest<0.35f&&Math.Abs(fast.Spring-0.35f)<0.001,"release rebounds past hover rest then settles");
        for(int i=0;i<120;i++)fast=fast.Step(false,false,true,1d/120);
        check(Math.Abs(fast.Spring)<0.001&&fast.Hover<0.001&&fast.Press<0.001,"leave returns to rest");
        check(slow.Step(true,true,false,1)==default&&slow.Step(true,true,true,-1)==slow,"disabled and nonpositive time do not animate");
        var controls=new FrameControls();controls.Add((rect,()=>{}));
        bool bounded=true;
        for(int i=0;i<60;i++)
        {
            var motion=slow.Step(i%2==0,i%3==0,true,0.1);var face=motion.Face(rect);
            bounded&=face.Width<=rect.Width+6&&face.Height>=rect.Height-8&&controls.HitTest(rect.X-1,rect.Y) is null
                &&controls.HitTest(rect.Right-1,rect.Bottom-1)==rect;
            slow=motion;
        }
        check(bounded,"repeated entry and presses remain bounded with unchanged hit targets");
        var feedback=new UiFeedbackState();
        check(feedback.Observe(null,0,0,0)==HoverFeedback.None&&feedback.Observe(rect,110,110,1)==HoverFeedback.Enter,"enter fires on pointer transition");
        check(Enumerable.Range(0,600).All(i=>feedback.Observe(rect,110,110,1+i/60d)==HoverFeedback.None),"stationary hover emits no repeated sounds");
        check(feedback.Observe(null,0,0,12)==HoverFeedback.Leave,"leaving control fires once");
        check(feedback.Observe(rect,110,110,12.01)==HoverFeedback.None,"rapid crossings are rate limited");
        check(feedback.Observe(null,110,110,13)==HoverFeedback.None,"modal replacement under stationary cursor is silent");
        controls.Clear();var modal=new UiRect(200,200,40,40);controls.Add((modal,()=>{}));
        check(controls.HitTest(110,110) is null&&controls.HitTest(210,210)==modal,"only active modal controls can receive feedback");
        // Export actual motion samples for a labelled offline animation preview.
        var frames=new List<object>();ButtonMotion state=default;
        for(int i=0;i<180;i++)
        {
            double t=i/60d;bool hover=t>=0.5&&t<2.2,down=t>=1.2&&t<1.5;
            if(i==72)state=state with{Press=1,Spring=1,Velocity=0};
            state=state.Step(hover,down,true,1d/60);
            frames.Add(new{Time=t,Hover=state.Hover,Press=state.Press,Spring=state.Spring,Face=state.Face(rect),Stage=!hover?"移开":down?"按下":t<1.2?"进入":"松开回弹"});
        }
        Directory.CreateDirectory("art/button-feedback-0.16.19");
        File.WriteAllText("art/button-feedback-0.16.19/motion.json",JsonSerializer.Serialize(frames));
    }
}
