#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace BetterBeads.Runtime;

/// <summary>Reads current game textures on demand; no copied Stardew artwork is packaged.</summary>
internal static class GameSpriteTemplates
{
    internal sealed record Entry(string Id,Blueprint Design);
    public static IReadOnlyList<Entry> Load(out string? missing)
    {
        var entries=new List<Entry>();
        var failures=new List<string>();
        Add("white-chicken","白色鸡",SimpleCrafting.Picture16,()=>Read(Game1.content.Load<Texture2D>("Animals\\White Chicken"),new(0,0,16,16)));
        Add("junimo","祝尼魔",SimpleCrafting.Picture16,()=>Read(Game1.content.Load<Texture2D>("Characters\\Junimo"),new(0,0,16,16),new Color(128,210,91)));
        Add("stardrop","星之果实",SimpleCrafting.Picture16,()=>ReadItem("(O)434"));
        Add("galaxy-sword","银河剑",SimpleCrafting.Sword,()=>ReadItem("(W)4"));
        Add("sebastian","赛巴斯 · 站立",SimpleCrafting.Sword32,
            ()=>Read(Game1.content.Load<Texture2D>("Characters\\Sebastian"),new(0,0,16,32)),
            16,32,"copper",SwordOrientation.Vertical);
        missing=failures.Count==0?null:string.Join(", ",failures);
        return entries;

        void Add(string id,string fallback,string kind,Func<uint[]> pixels,int width=16,int height=16,
            string metal="iridium",SwordOrientation orientation=SwordOrientation.Diagonal)
        {
            try
            {
                string name=ContentText.Get("simple.template."+id,fallback);
                entries.Add(new("builtin:"+id,LiteTemplatePixels.Create(name,kind,pixels(),width,height,metal,orientation)));
            }
            catch(Exception)
            {
                // A missing or replaced game texture must not prevent the workbench from opening.
                failures.Add(ContentText.Get("simple.template."+id,fallback));
            }
        }
    }
    private static uint[] ReadItem(string itemId)
    {
        var item=ItemRegistry.GetData(itemId);
        if(item is null||item.IsErrorItem)throw new InvalidOperationException("Missing game item sprite: "+itemId);
        return Read(item.GetTexture(),item.GetSourceRect());
    }
    private static uint[] Read(Texture2D texture,Rectangle source,Color? tint=null)
    {
        if(source.Width!=16||source.Height is not (16 or 32)||source.X<0||source.Y<0||source.Right>texture.Width||source.Bottom>texture.Height)
            throw new InvalidOperationException("Unexpected game sprite size");
        var colors=new Color[source.Width*source.Height];texture.GetData(0,source,colors,0,colors.Length);
        return colors.Select(c=>
        {
            if(tint is {} shade)c=new Color(c.R*shade.R/255,c.G*shade.G/255,c.B*shade.B/255,c.A);
            return ((uint)c.R<<24)|((uint)c.G<<16)|((uint)c.B<<8)|c.A;
        }).ToArray();
    }
}
#endif
