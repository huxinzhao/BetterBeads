namespace BetterBeads.Data;

public sealed record WeaponDraftPreview(DraftReport Report,WeaponStatResult? Stats,SpecialEffectReport? Effects=null)
{
    public static WeaponDraftPreview Evaluate(Blueprint design,ManufacturingRecipe? recipe,ProcessingCatalog catalog,
        IReadOnlySet<string> unlocked,IReadOnlyDictionary<string,int> stock,bool creative=false)
    {
        var report=DraftAssessment.Evaluate(design,recipe?.Template,catalog,unlocked,stock,creative);
        if(report.Issues.Any(i=>i.Code=="structure"))return new(report,null);
        if(!WeaponMaterials.IsWeapon(design.Use))return new(report,null);
        if(recipe?.WeaponRules is null || !ManufacturingCatalog.ValidRecipe(recipe))
            return new(WithInvalidRules(report),null);
        var bill=report.Materials.ToDictionary(m=>m.Id,m=>m.Needed);
        SpecialEffectReport? effects=recipe.SpecialEffectsEnabled?SpecialEffects.Evaluate(design.Use,catalog,bill,design.SelectedEffects):null;
        if(effects?.Errors.Count>0 || (!recipe.SpecialEffectsEnabled && design.SelectedEffects.Count>0))
            report=new(report.Issues.Append(new DraftIssue("effect-selection")).ToArray(),report.Materials);
        // Inventory/color shortages can block production without hiding a valid design's expected attributes.
        if(report.Issues.Any(i=>i.Code is not ("insufficient-material" or "locked-color")))return new(report,null,effects);
        var stats=WeaponStats.Calculate(design.Use,recipe.WeaponRules,catalog,bill,effects);
        return new(stats.Values is null?WithInvalidRules(report):report,stats,effects);
    }
    private static DraftReport WithInvalidRules(DraftReport report)=>new(report.Issues.Append(new DraftIssue("weapon-rules")).ToArray(),report.Materials);
}
