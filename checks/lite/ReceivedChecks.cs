using BetterBeads.Data;
using System.Text.Json;

internal static class ReceivedChecks
{
    public static void Run(Action<bool,string> check)
    {
        var state=new ReceivedPresentation();
        state.Update(1,false);check(!state.CanContinue,"craft input held cannot dismiss result");
        state.Update(0,true);check(state.CanContinue,"release enables native continuation after minimum display");
        state=new();state.Update(.1,true);check(!state.CanContinue,"early release still leaves time to see the item");
        state.Update(.36,true);check(state.CanContinue&&state.Reveal==1,"reveal settles and native input opens after minimum display");
        state.Update(-5,false);check(state.CanContinue&&state.Reveal==1,"negative frame duration cannot reverse result state");
        var picture=new ProductSnapshot{Design=SimpleCrafting.Blank(SimpleCrafting.Picture32)};
        picture.Design.Name="花园#装饰^画";string before=DesignStorage.Serialize(picture);
        string text=ReceivedPresentation.Message(picture);
        check(text.Contains("花园＃装饰＾画")&&!text.Contains('#')&&!text.Contains('^')&&text.Contains("图纸已保存"),"name remains literal native dialogue text and confirms saved state");
        check(DesignStorage.Serialize(picture)==before,"presentation never changes product snapshot");
        foreach(string template in new[]{SimpleCrafting.Sword,SimpleCrafting.Dagger,SimpleCrafting.Hammer})
        {
            var weapon=new ProductSnapshot{Design=SimpleCrafting.Blank(template),FinalStats=new(){{"minDamage",20},{"maxDamage",28}}};
            weapon.Design.Name="测试武器";check(ReceivedPresentation.Message(weapon).Contains("20～28"),"weapon damage is read from existing snapshot "+template);
        }
        var dumps=new List<object>();
        foreach(var size in new[]{(480,420),(640,480),(854,480),(1280,720),(1920,1080)})
        {
            int top=size.Item2-180;
            foreach(var point in new[]{(-200,-200),(size.Item1/2,size.Item2/2),(size.Item1+200,size.Item2+200)})
            {
                var r=ReceivedPresentation.Preview(size.Item1,size.Item2,top,point.Item1,point.Item2);
                check(r.X>=16&&r.Right<=size.Item1-16&&r.Y>=16&&r.Bottom<=top-16&&r.Width==r.Height&&r.Width<=128,"showcase stays clear of native dialogue and screen edges "+size+point);
            }
            dumps.Add(new{Width=size.Item1,Height=size.Item2,DialogueTop=top,Preview=ReceivedPresentation.Preview(size.Item1,size.Item2,top,size.Item1/2,size.Item2/2-60)});
        }
        Directory.CreateDirectory("art/received-0.16.22");
        File.WriteAllText("art/received-0.16.22/layouts.json",JsonSerializer.Serialize(dumps));
    }
}
