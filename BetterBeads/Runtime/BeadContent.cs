using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;

namespace BetterBeads.Runtime;

internal sealed class BeadContent
{
    private const string TextureName = "Mods/xinzh.BetterBeads/Beads";
    private readonly ProcessingCatalog catalog;
    private readonly ITranslationHelper text;
    private static HashSet<string> beadIds = new();

    public BeadContent(ProcessingCatalog catalog, ITranslationHelper text) { this.catalog = catalog; this.text = text; }
    public void Register(IModHelper helper)
    {
        beadIds = catalog.Materials.Select(m => BeadItems.BeadId(m.Id)).ToHashSet();
        helper.Events.Content.AssetRequested += OnAssetRequested;
        var harmony = new Harmony("xinzh.BetterBeads.Beads");
        foreach (var name in new[] { "sellToStorePrice", "salePrice" })
            harmony.Patch(AccessTools.Method(typeof(StardewValley.Object), name), postfix: new HarmonyMethod(typeof(BeadContent), nameof(ZeroPrice)));
    }
    private static void ZeroPrice(Item __instance, ref int __result)
    { if (beadIds.Contains(__instance.QualifiedItemId)) __result = 0; }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, ObjectData>().Data;
                for (int i = 0; i < catalog.Materials.Count; i++)
                {
                    var material = catalog.Materials[i];
                    string name = text.Get("material." + material.Id).ToString();
                    data[BeadItems.BeadId(material.Id)[3..]] = new() {
                        Name = "BetterBeads " + material.Id, DisplayName = MaterialRules.Canonical(material)=="decoration"?text.Get("bead.ordinary").ToString():text.Get("bead.name", new { material = name }).ToString(),
                        Description = text.Get("bead.description", new { uses = string.Join("、", Enum.GetValues<ProductUse>().Where(use=>MaterialRules.CanUse(material,use)).Select(use => text.Get("use." + use).ToString())) })
                            +(material.Series==MaterialSeries.Other?"":"\n"+text.Get("bead.profile",new{series=text.Get("series."+material.Series),hardness=material.Hardness,weight=material.Weight})
                                +"\n"+text.Get(material.RefinedFrom is null?"bead.refine-recipe":"bead.refined-description")),
                        Type = "Basic", Category = -8, Price = 0, Edibility = -300,
                        Texture = TextureName, SpriteIndex = i, CanBeGivenAsGift = false, CanBeTrashed = true,
                        ExcludeFromFishingCollection = true, ExcludeFromShippingCollection = true, ExcludeFromRandomSale = true
                    };
                }
            });
        else if (e.NameWithoutLocale.IsEquivalentTo(TextureName)) e.LoadFrom(()=>ArtResources.Beads(catalog), AssetLoadPriority.Low);
    }

}
