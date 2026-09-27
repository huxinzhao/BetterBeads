using BetterBeads.Data;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Shops;

namespace BetterBeads.Runtime;

internal sealed class WorkbenchContent
{
    public const string Id = "xinzh.BetterBeads.Workbench";
    public const string UnlockMailId = "xinzh.BetterBeads.Workbench.RobinLetter";
    private const string TextureName = "Mods/xinzh.BetterBeads/Workbench";
    private readonly WorkbenchSettings settings;
    private readonly ITranslationHelper text;
    private static Action<StardewValley.Object>? open;

    public WorkbenchContent(WorkbenchSettings settings, ITranslationHelper text, Action<StardewValley.Object> openMenu)
    {
        this.settings = settings;
        this.text = text;
        open = openMenu;
    }

    public void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        new Harmony("xinzh.BetterBeads.Workbench").Patch(AccessTools.Method(typeof(StardewValley.Object), "checkForAction"),
            prefix: new HarmonyMethod(typeof(WorkbenchContent), nameof(Interact)));
    }

    public void DayStarted()
    {
        if(!Context.IsWorldReady)return;
        var player=Game1.player;
        if(!WorkbenchUnlock.ShouldSendMail(settings.IsConfigured,
            GameStateQuery.CheckConditions(WorkbenchUnlock.LevelCondition),
            player.craftingRecipes.ContainsKey(Id),player.mailReceived.Contains(UnlockMailId),
            player.mailbox.Contains(UnlockMailId),player.mailForTomorrow.Contains(UnlockMailId)))return;
        player.mailbox.Add(UnlockMailId);
    }

    private static bool Interact(StardewValley.Object __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
    {
        if (__instance.QualifiedItemId != "(BC)" + Id) return true;
        __result = true;
        if (!justCheckingForActivity && who.IsLocalPlayer && Game1.activeClickableMenu is null) open?.Invoke(__instance);
        return false;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
            e.Edit(asset => asset.AsDictionary<string, BigCraftableData>().Data[Id] = new()
            {
                Name = Id, DisplayName = text.Get("bench.name").ToString(), Description = text.Get("bench.description").ToString(),
                Price = 0, Fragility = 0, CanBePlacedIndoors = true, CanBePlacedOutdoors = true,
                Texture = TextureName, SpriteIndex = 0
            });
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes") && settings.IsConfigured)
            e.Edit(asset => asset.AsDictionary<string, string>().Data[Id] = settings.Recipe(Id));
        else if(e.NameWithoutLocale.IsEquivalentTo("Data/Mail") && settings.IsConfigured)
            e.Edit(asset=>asset.AsDictionary<string,string>().Data[UnlockMailId]
                =text.Get("bench.unlock-mail").ToString()+"[#]"+text.Get("bench.unlock-mail-title"));
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops") && settings.IsConfigured)
            e.Edit(asset =>
            {
                if (!asset.AsDictionary<string, ShopData>().Data.TryGetValue("Carpenter", out var shop)) return;
                if (shop.Items.Any(item => item.Id == Id + ".Recipe")) return;
                shop.Items.Add(new ShopItemData {
                    Id = Id + ".Recipe", ItemId = "(BC)" + Id, IsRecipe = true,
                    Price = settings.RecipePrice!.Value, AvailableStock = 1,
                    Condition = WorkbenchUnlock.ShopCondition(Id),
                    ApplyProfitMargins = false, IgnoreShopPriceModifiers = true
                });
            });
    }

}
