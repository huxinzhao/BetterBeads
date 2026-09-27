namespace BetterBeads.Data;

public sealed record TemplateSpec(string Id, ProductUse Use, int Width, int Height, string[] Views,
    string BillingView, int MinimumBeads = 1, int? WoolBudget = null, bool ManufacturingAvailable = false, int MinimumMaterials = 0, int StructureBudget = 0);
public sealed record DraftIssue(string Code, string? View = null, int? Cell = null, string? Id = null);
public sealed record MaterialAmount(string Id, int Needed, int Owned,bool IsFree=false)
{
    public int Missing => IsFree?0:Math.Max(0, Needed - Owned);
}
public sealed record DraftReport(IReadOnlyList<DraftIssue> Issues, IReadOnlyList<MaterialAmount> Materials)
{
    public bool CanMake => Issues.Count == 0;
}

public static class DraftAssessment
{
    public static DraftReport Evaluate(Blueprint design, TemplateSpec? template, ProcessingCatalog catalog,
        IReadOnlySet<string> unlocked, IReadOnlyDictionary<string, int> inventory,bool creative=false)
    {
        var issues = new List<DraftIssue>();
        var bill = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!DesignStorage.IsStructurallyValid(design)) return new(new[] { new DraftIssue("structure") }, Array.Empty<MaterialAmount>());
        bool clothing = design.Use is ProductUse.Hat or ProductUse.Shirt or ProductUse.Pants;
        bool weapon = WeaponMaterials.IsWeapon(design.Use);
        if (template is null || template.Id != design.TemplateId || template.Use != design.Use)
            issues.Add(new("template"));
        else
        {
            if (!template.ManufacturingAvailable) issues.Add(new("unavailable"));
            foreach (var view in template.Views)
            {
                if (!design.Views.TryGetValue(view, out var grid)) issues.Add(new("missing-view", view));
                else if (grid.Width != template.Width || grid.Height != template.Height) issues.Add(new("size", view));
                else if(clothing && grid.Cells.All(c=>c is null))issues.Add(new("empty-view",view));
            }
            if (design.Views.Keys.Any(v => !template.Views.Contains(v))) issues.Add(new("extra-view"));
        }
        var materials = catalog.Materials.ToDictionary(m => m.Id, StringComparer.Ordinal);
        foreach (var view in design.Views)
            for (int i = 0; i < view.Value.Cells.Count; i++)
            {
                var cell = view.Value.Cells[i];
                if (cell is null) continue;
                // Colors are free; the saved opaque RGB is authoritative, including legacy/custom colors.
                if((byte)cell.Rgba!=255)issues.Add(new("invalid-color",view.Key,i,cell.ColorId));
                if (clothing) continue;
                if(MaterialRules.IsDecoration(design.Use))
                {
                    if(view.Key==template?.BillingView)bill["decoration"]=bill.GetValueOrDefault("decoration")+1;
                    continue;
                }
                if (cell.MaterialId is null) issues.Add(new("unassigned-material",view.Key,i));
                else if (!materials.TryGetValue(cell.MaterialId, out var material)) issues.Add(new("unknown-material",view.Key,i,cell.MaterialId));
                else if (!MaterialRules.CanUse(material,design.Use)) issues.Add(new("incompatible-material",view.Key,i,material.Id));
                if (view.Key == template?.BillingView && cell.MaterialId is not null)
                    bill[cell.MaterialId] = bill.GetValueOrDefault(cell.MaterialId) + 1;
            }
        int effective = template is not null && design.Views.TryGetValue(template.BillingView,out var billing)
            ? billing.Cells.Count(c => c is not null) : 0;
        if (effective < Math.Max(1,template?.MinimumBeads ?? 1)) issues.Add(new("empty"));
        foreach(var pair in design.SupplementaryMaterials.Where(p=>p.Value>0))
        {
            if(!weapon)continue;
            if(!materials.TryGetValue(pair.Key,out var material)) issues.Add(new("unknown-material",Id:pair.Key));
            else if(!MaterialRules.CanUse(material,design.Use)) issues.Add(new("incompatible-material",Id:pair.Key));
            long combined=(long)bill.GetValueOrDefault(pair.Key)+pair.Value;
            if(combined>int.MaxValue) issues.Add(new("material-overflow",Id:pair.Key));
            bill[pair.Key]=(int)Math.Min(int.MaxValue,combined);
        }
        if (clothing)
        {
            if (template?.WoolBudget is not > 0) issues.Add(new("wool-budget"));
            if(template?.WoolBudget is >0)bill["wool"]=template.WoolBudget.Value;
        }
        if(MaterialRules.IsDecoration(design.Use) && effective>0 && template?.StructureBudget>0)
            bill["decoration"]=Math.Max(bill.GetValueOrDefault("decoration"),template.StructureBudget);
        if(bill.Values.Sum(n=>(long)n)<(template?.MinimumMaterials??0)) issues.Add(new("minimum-material"));
        var amounts = bill.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new MaterialAmount(p.Key,p.Value,Math.Max(0,inventory.GetValueOrDefault(p.Key)),creative)).ToArray();
        foreach (var material in amounts) if (material.Missing > 0) issues.Add(new("insufficient-material",Id:material.Id));
        return new(issues,amounts);
    }
}
