using BetterBeads.Data;
using StardewValley;

namespace BetterBeads.Runtime;

internal static class DraftStatus
{
    public static DraftReport Evaluate(Blueprint design,ProcessingCatalog catalog,SaveProgress progress,ManufacturingService? manufacturing=null)
    {
        var stock=ReadStock(catalog,manufacturing);
        var recipe=manufacturing?.FindRecipe(design.TemplateId);
        if(WeaponMaterials.IsWeapon(design.Use))return WeaponDraftPreview.Evaluate(design,recipe,catalog,progress.UnlockedColors,stock,PlayMode.Creative).Report;
        TemplateSpec? template=recipe?.Template
            ?? (design.TemplateId==ProductTemplates.Picture?new(ProductTemplates.Picture,ProductUse.Picture,16,16,new[]{"front"},"front"):null);
        return DraftAssessment.Evaluate(design,template,catalog,progress.UnlockedColors,stock,PlayMode.Creative);
    }
    public static IReadOnlyDictionary<string,int> ReadStock(ProcessingCatalog catalog,ManufacturingService? manufacturing=null)=>manufacturing?.ReadStock()??catalog.Materials.ToDictionary(m=>m.Id,m=>(int)Math.Min(int.MaxValue,
        Game1.player.Items.Take(Game1.player.MaxItems).Where(item=>item is not null && item.QualifiedItemId==BeadItems.BeadId(m.Id)
            && item.Quality==0 && InventoryService.IsPlainObject(item)).Sum(item=>(long)item.Stack)));
}
