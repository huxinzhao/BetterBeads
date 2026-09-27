using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BetterBeads.Runtime;

/// <summary>Fonts are replaced when the language changes; resource invalidation also clears both bounded tables.</summary>
internal static class UiTextCache
{
    private readonly record struct MetricKey(SpriteFont Font,float Spacing,int LineSpacing,string Text);
    private readonly record struct FitKey(MetricKey Metric,int Width,float Scale);
    private static readonly BoundedMemo<MetricKey,Vector2> metrics=new(1024);
    private static readonly BoundedMemo<FitKey,string> fitted=new(512);
    private static readonly Func<MetricKey,Vector2> measure=k=>k.Font.MeasureString(k.Text);
    private static readonly Func<FitKey,string> fit=Fit;
    internal static Vector2 Measure(SpriteFont font,string text)=>metrics.Get(new(font,font.Spacing,font.LineSpacing,text),measure);
    internal static string Ellipsize(SpriteFont font,string text,int width,float scale)
        =>fitted.Get(new(new(font,font.Spacing,font.LineSpacing,text),width,scale),fit);
    private static string Fit(FitKey key)
    {
        var font=key.Metric.Font;string label=key.Metric.Text;
        // Preserve the existing font/kerning-dependent truncation result, but compute it only once per key.
        if(Measure(font,label).X*key.Scale<=key.Width)return label;
        while(label.Length>0&&font.MeasureString(label+"…").X*key.Scale>key.Width)label=label[..^1];
        return label+"…";
    }
    internal static void Clear(){metrics.Clear();fitted.Clear();}
}
