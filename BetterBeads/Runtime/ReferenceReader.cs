using BetterBeads.Data;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;
using Microsoft.Xna.Framework.Graphics;

namespace BetterBeads.Runtime;

internal sealed class ReferenceReader
{
    private readonly ProductItems products=new();
    private readonly IMonitor monitor;
    private static readonly System.Reflection.FieldInfo Offset=HarmonyLib.AccessTools.Field(typeof(Furniture),"sourceIndexOffset");
    public void ReportPreviewError(Exception error)=>monitor.Log(ContentText.Get("copy.ReferenceReader.9ff126739c","穿戴预览失败：")+error,LogLevel.Warn);
    public Blueprint? CaptureDesign(Item item)=>products.Read(item)?.Design.Copy();
    public ReferenceReader(IMonitor monitor)=>this.monitor=monitor;

    // Capture now from the selected instance. Confirming an import never re-reads a slot or removes an item.
    public bool TryCapture(Item item,string preferredView,out ReferencePixels? source,out string failure)
    {
        source=null;failure="import.unsupported";
        try
        {
            if(ProductItems.IsProduct(item))
            {
                var snapshot=products.Read(item);
                if(snapshot is null){failure="import.invalid-product";return false;}
                var views=snapshot.Design.Views;
                var grid=views.TryGetValue(preferredView,out var selected)?selected:views["front"];
                source=new(){SourceItemId=item.QualifiedItemId,Width=grid.Width,Height=grid.Height,
                    Pixels=grid.Cells.Select(c=>c?.Rgba??0u).ToArray()};
                failure="";return true;
            }
            // Explicit adapters only; custom subclasses can own arbitrary drawing pipelines.
            var type=item.GetType();
            if(item.modData.Any() || !(type==typeof(StardewValley.Object) || type==typeof(ColoredObject)
                || type==typeof(Furniture) || type==typeof(Clothing) || type==typeof(Hat) || type==typeof(MeleeWeapon)))return false;
            if(item is StardewValley.Object obj && obj.IsRecipe)return false;
            if(item.ItemId=="SmokedFish")return false;
            var data=ItemRegistry.GetData(item.QualifiedItemId);
            if(data is null){failure="import.missing-data";return false;}
            var texture=data.GetTexture();var rect=data.GetSourceRect();
            bool flip=false;
            if(item is Furniture furniture)
            {
                rect=furniture.sourceRect.Value;rect.X+=rect.Width*(Offset.GetValue(furniture) is Netcode.NetInt offset?offset.Value:0);
                flip=furniture.flipped.Value;
            }
            if(item is Hat)
                rect.Y+=preferredView switch{"right"=>20,"left"=>40,"back"=>60,_=>0};
            if(item is Clothing shirt && shirt.clothesType.Value==Clothing.ClothesType.SHIRT)
            {
                rect.Y+=preferredView switch{"right"=>8,"left"=>16,"back"=>24,_=>0};
            }
            if(rect.Width<=0 || rect.Height<=0 || rect.Width>DesignStorage.MaxDimension || rect.Height>DesignStorage.MaxDimension
                || rect.X<0 || rect.Y<0 || (long)rect.X+rect.Width>texture.Width || (long)rect.Y+rect.Height>texture.Height)
            {failure="import.source-bounds";return false;}
            var pixels=new Color[rect.Width*rect.Height];
            texture.GetData(0,rect,pixels,0,pixels.Length);
            if(item is ColoredObject colored)
            {
                if(colored.ColorSameIndexAsParentSheetIndex)Tint(pixels,colored.color.Value);
                else
                {
                    var overlay=Read(texture,data.GetSourceRect(1,item.ParentSheetIndex));Tint(overlay,colored.color.Value);Over(pixels,overlay);
                }
            }
            if(item is Clothing clothing)
            {
                var tint=clothing.isPrismatic.Value?Utility.GetPrismaticColor():clothing.clothesColor.Value;
                if(clothing.clothesType.Value==Clothing.ClothesType.SHIRT)
                {var dye=rect;dye.X+=texture.Width/2;var overlay=Read(texture,dye);Tint(overlay,tint);Over(pixels,overlay);}
                else Tint(pixels,tint);
            }
            if(flip)for(int y=0;y<rect.Height;y++)Array.Reverse(pixels,y*rect.Width,rect.Width);
            source=new(){SourceItemId=item.QualifiedItemId,Width=rect.Width,Height=rect.Height,
                Pixels=pixels.Select(c=>PixelImport.FromPremultiplied(c.R,c.G,c.B,c.A)).ToArray()};
            failure="";return true;
        }
        catch(Exception ex)
        {
            failure="import.read-failed";
            monitor.Log(ContentText.Format("copy.ReferenceReader.ea35481ec2",$"读取参考图失败（{item.QualifiedItemId}）：{ex.Message}"),LogLevel.Warn);
            return false;
        }
    }
    private static Color[] Read(Texture2D texture,Rectangle rect)
    {
        if(rect.X<0 || rect.Y<0 || rect.Right>texture.Width || rect.Bottom>texture.Height || rect.Width<1 || rect.Height<1)
            throw new ArgumentException("Reference layer outside texture.");
        var pixels=new Color[rect.Width*rect.Height];texture.GetData(0,rect,pixels,0,pixels.Length);return pixels;
    }
    private static void Tint(Color[] pixels,Color tint)
    {
        for(int i=0;i<pixels.Length;i++){var p=pixels[i];pixels[i]=new Color(p.R*tint.R/255,p.G*tint.G/255,p.B*tint.B/255,p.A);}
    }
    private static void Over(Color[] back,Color[] front)
    {
        if(back.Length!=front.Length)throw new ArgumentException("Reference layers differ in size.");
        for(int i=0;i<back.Length;i++)
        {var a=front[i];var b=back[i];int inv=255-a.A;back[i]=new Color(Math.Min(255,a.R+b.R*inv/255),Math.Min(255,a.G+b.G*inv/255),Math.Min(255,a.B+b.B*inv/255),Math.Min(255,a.A+b.A*inv/255));}
    }
}
