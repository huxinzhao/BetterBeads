using BetterBeads.Data;

internal static class CatalogChecks
{
    public static void Run(Action<bool, string> check)
    {
        var catalog = DefaultProcessing.Create();
        check(catalog.Validate().Count == 0, "默认原料、材质、色卡引用与来源覆盖有效");
        var restored = System.Text.Json.JsonSerializer.Deserialize<ProcessingCatalog>(DesignStorage.Serialize(catalog))!;
        check(restored.Validate().Count == 0 && restored.Sources.Count == catalog.Sources.Count, "加工配置序列化往返后引用与产量可用");
        var translationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../BetterBeads/i18n/default.json"));
        var strings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(translationPath))!;
        var keys = catalog.Materials.Select(m => "material." + m.Id).Concat(catalog.Colors.Select(c => c.NameKey))
            .Concat(Enum.GetNames<ProductUse>().Select(n => "use." + n));
        check(keys.All(strings.ContainsKey), "材质、色卡和用途中文键完整");
        check(catalog.Match("(O)378", 0)!.Yield == 2 && catalog.Match("(O)334", 0)!.Yield == 12,
            "矿石和金属锭使用不同产量");
        check(catalog.Match("(O)428", 0)?.Yield==36 && catalog.Match("(O)unknown", 0) is null,
            "布料和未知原料不会隐式加工");
        check(MaterialRules.Allows(catalog, ProductUse.Sword, new Dictionary<string, int> { ["ruby"] = 8 })
            && !MaterialRules.Allows(catalog, ProductUse.Sword, new Dictionary<string, int> { ["ruby"] = 8, ["wood"] = 1 }),
            "允许纯宝石武器，混入一颗非法底料则整次拒绝");
        check(!MaterialRules.Allows(catalog, ProductUse.Hat, new Dictionary<string, int> { ["stone"] = 24 })
            && MaterialRules.Allows(catalog, ProductUse.Statue, new Dictionary<string, int> { ["stone"] = 24 }),
            "材质用途不由数量或硬度替代");
    }
}
