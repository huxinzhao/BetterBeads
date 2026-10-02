using BetterBeads.Data;
using System.Text.Json;

internal static class PlayerUiChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var layouts=new List<object>();
        foreach(var size in new[]{(480,420),(640,480),(854,480),(1024,768),(1280,720),(1280,900),(1920,1080)})
        {
            var scale=WorkbenchScale.Calculate(size.Item1,size.Item2,40);var editor=SimpleEditorLayout.Calculate(scale.Width,scale.Height);
            var move=PatternMoveLayout.Calculate(editor.Frame);var recover=RecoveryLayout.Calculate(editor.Frame);
            var scene=ScenePreviewLayout.Calculate(editor.Frame);var explain=WeaponExplanationLayout.Calculate(scene);
            var moveButtons=new[]{move.Up,move.Left,move.Down,move.Right,move.Mode,move.Copy,move.Apply,move.Cancel,move.Close};
            check(moveButtons.All(r=>move.Dialog.Contains(r)&&r.Width>=40&&r.Height>=40)&&Disjoint(moveButtons),"move controls and native close do not overlap "+size);
            check(move.Description.Bottom+8<=move.Up.Y&&move.Preview.Bottom+8<=move.Description.Y&&move.Close.Bottom+8<=move.Preview.Y,
                "move header, preview, wrapped note and actions share eight-pixel gaps "+size);
            check(move.Apply.Height==48&&move.Cancel.Height==48&&move.Mode.Height==44&&move.Copy.Height==44&&move.Apply.Width<=144,
                "move uses standard proportions without stretching short action labels "+size);
            check(new[]{recover.Close,recover.Preview,recover.Name,recover.Description,recover.Delete,recover.Restore}.All(recover.Dialog.Contains)
                &&Disjoint(new[]{recover.Preview,recover.Name,recover.Description,recover.Delete,recover.Restore}),"recovery content and actions remain aligned and bounded "+size);
            check(recover.Delete.Height==48&&recover.Restore.Height==48&&recover.Close.Bottom+8<=recover.Preview.Y,"recovery uses the same header and action heights "+size);
            check(explain.Body.Bottom+16<=scene.Toggle.Y&&explain.Body.Contains(explain.Picture)&&explain.Body.Contains(explain.Legend)
                &&new[]{explain.Text,explain.Previous,explain.Next,explain.Page}.All(explain.Body.Contains)
                &&Disjoint(new[]{explain.Picture,explain.Legend,explain.Text,explain.Previous,explain.Next,explain.Page}),"weapon picture and text pages do not overlap "+size);
            check(explain.Picture.Width==explain.Picture.Height&&explain.Picture.Width>=100&&explain.Text.Height>=144
                &&explain.Next.Height==44&&scene.Toggle.Height==44,"weapon illustration remains square and readable while long text is paged "+size);
            layouts.Add(new{Width=size.Item1,Height=size.Item2,Scale=scale,Move=move,Recovery=recover,Scene=scene,Explanation=explain});
        }
        double Width(string s)=>s.EnumerateRunes().Count()*10;
        var chinese=UiParagraph.Wrap("亮色=主体，十字=分析握持端。",60,Width);
        check(chinese.Length==3&&string.Concat(chinese)=="亮色=主体，十字=分析握持端。"&&chinese.All(s=>Width(s)<=60),"Chinese wraps without truncating content or reducing font size");
        var english=UiParagraph.Wrap("Separate beads add weight, not reach.",100,Width);
        check(english.All(s=>Width(s)<=100)&&string.Join(' ',english)=="Separate beads add weight, not reach.","English wraps at words while retaining meaning");
        check(UiParagraph.Wrap("abcdefghijkl",40,Width).SequenceEqual(new[]{"abcd","efgh","ijkl"}),"long single tokens wrap without clipping");
        check(UiParagraph.Wrap("A\n\nB",40,Width).SequenceEqual(new[]{"A","","B"}),"paragraph breaks survive wrapping");
        check(string.Concat(UiParagraph.Wrap("豆😀豆",10,Width))=="豆😀豆","wrapping preserves surrogate pairs");
        check(UiParagraph.Wrap("test",0,Width).Length==0&&UiParagraph.Wrap("😀",1,Width).Single()=="😀","empty areas and single oversized glyphs terminate safely");
        var pages=UiParagraph.Pages(new[]{new[]{"count","length","damage"},new[]{"weight-a","weight-b"},new[]{"range"}},4);
        check(pages.Length==2&&pages[0].SequenceEqual(new[]{"count","length","damage"})&&pages[1].SequenceEqual(new[]{"weight-a","weight-b","range"}),"paging keeps short explanation paragraphs together");
        check(UiParagraph.Pages(new[]{new[]{"a","b","c","d","e"}},2).SelectMany(p=>p).SequenceEqual(new[]{"a","b","c","d","e"}),"oversized paragraphs page without dropping lines");
        check(UiParagraph.Wrap("Count 70 / Separate 1",190,Width).Last()=="Separate 1","statistic labels stay with their numeric values");
        Directory.CreateDirectory("art/player-experience-1.1.0-RC4");
        File.WriteAllText("art/player-experience-1.1.0-RC4/layouts.json",JsonSerializer.Serialize(layouts));
    }
    private static bool Disjoint(UiRect[] areas)=>areas.SelectMany((a,i)=>areas.Skip(i+1).Select(b=>(a,b))).All(p=>!p.a.Overlaps(p.b));
}
