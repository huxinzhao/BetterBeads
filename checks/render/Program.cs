using BetterBeads.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// SpriteFont measurement requires no GraphicsDevice, game launch, or loaded texture.
SpriteFont Font(int advance=10)
{
    var chars=" ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789中文按钮保存图纸国…".Distinct().OrderBy(c=>c).ToList();
    return new SpriteFont(null!,chars.Select(_=>new Rectangle(0,0,advance,20)).ToList(),chars.Select(_=>new Rectangle(0,0,advance,20)).ToList(),chars,24,1,chars.Select(_=>new Vector3(0,advance,0)).ToList(),' ');
}
int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
try
{
    var font=Font();
    foreach(string text in new[]{"", "保存图纸", "中文按钮 ABCDEFGHIJKLMNOPQRSTUVWXYZ", "A\nB", "     "})
    foreach(int width in new[]{1,20,80,160,1000})
    foreach(float scale in new[]{0.5f,1f,1.6f})
    {
        string expected=text;
        if(font.MeasureString(expected).X*scale>width)
        {
            while(expected.Length>0&&font.MeasureString(expected+"…").X*scale>width)expected=expected[..^1];expected+="…";
        }
        Check(UiTextCache.Measure(font,text)==font.MeasureString(text)&&UiTextCache.Ellipsize(font,text,width,scale)==expected,"font measurement and exact previous truncation");
    }
    string longText="中文按钮 ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    var measure=UiTextCache.Measure(font,longText);var fitted=UiTextCache.Ellipsize(font,longText,90,1);
    bool same=true;
    for(int i=0;i<600;i++)same&=UiTextCache.Measure(font,longText)==measure&&ReferenceEquals(fitted,UiTextCache.Ellipsize(font,longText,90,1));
    Check(same,"stationary frames reuse exact fitted string object");
    font.Spacing=4;font.LineSpacing=31;
    Check(UiTextCache.Measure(font,longText)==font.MeasureString(longText)&&UiTextCache.Measure(font,longText)!=measure,"font metric changes invalidate measurement key");
    var otherFont=Font(15);
    Check(UiTextCache.Measure(otherFont,longText)==otherFont.MeasureString(longText),"replacement language font keeps separate measurements");
    UiTextCache.Clear();
    Check(UiTextCache.Measure(font,longText)==font.MeasureString(longText),"asset reload safely clears text caches");
    Console.WriteLine($"PASS {passed} font and truncation checks (no game or graphics device).");
}
catch(Exception e){Console.Error.WriteLine("FAIL "+e);Environment.ExitCode=1;}
