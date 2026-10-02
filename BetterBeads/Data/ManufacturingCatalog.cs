namespace BetterBeads.Data;

public static class ManufacturingCatalog
{
    public static bool ValidRecipe(ManufacturingRecipe recipe)
    {
        var t=recipe.Template;
        if(t is null || string.IsNullOrWhiteSpace(t.Id) || !Enum.IsDefined(typeof(ProductUse),t.Use)
            || string.IsNullOrWhiteSpace(recipe.OutputItemId) || recipe.ExtraCost<0
            || t.Width<=0 || t.Height<=0 || t.Width>DesignStorage.MaxDimension || t.Height>DesignStorage.MaxDimension
            || t.Views is null || t.Views.Length==0 || t.Views.Any(string.IsNullOrWhiteSpace)
            || t.Views.Distinct(StringComparer.Ordinal).Count()!=t.Views.Length || !t.Views.Contains(t.BillingView)
            || t.MinimumBeads<1 || t.MinimumBeads>t.Width*t.Height || t.MinimumMaterials<0 || t.StructureBudget<0)return false;
        return WeaponMaterials.IsWeapon(t.Use)
            ?recipe.WeaponRules is {IsValid:true} rules && rules.Use==t.Use
            :recipe.WeaponRules is null && !recipe.SpecialEffectsEnabled;
    }

    // Startup-only cross-check against the registered item templates; pure planning can use isolated test recipes.
    public static IReadOnlyList<string> Validate(IReadOnlyList<ManufacturingRecipe> recipes,ProcessingCatalog materials)
    {
        var issues=new List<string>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var recipe in recipes)
        {
            if(recipe is null || !ValidRecipe(recipe)){issues.Add("recipe.invalid");continue;}
            var t=recipe.Template;
            if(!ids.Add(t.Id))issues.Add("recipe.duplicate:"+t.Id);
            bool registered;
            string prefix;
            if(WeaponMaterials.IsWeapon(t.Use))
            {
                prefix="(W)";
                registered=WeaponTemplates.TryGet(t.Id,out var weapon) && weapon.Use==t.Use
                    && weapon.PixelWidth==t.Width && weapon.PixelHeight==t.Height && t.Views.SequenceEqual(new[]{"front"}) && t.BillingView=="front";
                var allowed=materials.Materials.Where(m=>MaterialRules.CanUse(m,t.Use)).ToArray();
                if(allowed.Length==0 || allowed.Any(m=>WeaponMaterials.Analyze(t.Use,materials,new Dictionary<string,int>{{m.Id,1}}).Summary is null))
                    issues.Add("recipe.weapon-materials:"+t.Id);
            }
            else if(ClothingTemplates.IsClothing(t.Use))
            {
                prefix=t.Use==ProductUse.Hat?"(H)":t.Use==ProductUse.Shirt?"(S)":"(P)";
                var c=ClothingTemplates.Find(t.Id);
                registered=c is not null && c.Use==t.Use && t.Width==c.Width && t.Height==c.Height && t.Views.SequenceEqual(c.Views) && t.WoolBudget is >0;
            }
            else if(SimpleCrafting.IsDecoration(t.Use))
            {
                prefix=t.Use==ProductUse.Wallpaper?"(WP)":"(FL)";
                registered=ProductCategories.ForTemplate(t.Id) is {} category && category.Use==t.Use
                    &&t.Width==t.Height&&category.Sizes.Contains(t.Width)&&t.Views.SequenceEqual(new[]{"front"})&&t.BillingView=="front";
            }
            else
            {
                prefix="(F)";
                registered=FurnitureTemplates.TryGet(t.Id,out var furniture) && furniture.Use==t.Use
                    && furniture.PixelWidth==t.Width && furniture.PixelHeight==t.Height && t.Views.SequenceEqual(new[]{"front"}) && t.BillingView=="front";
            }
            if(!registered)issues.Add("recipe.template:"+t.Id);
            if(recipe.OutputItemId!=prefix+t.Id)issues.Add("recipe.output:"+t.Id);
        }
        return issues;
    }
}
