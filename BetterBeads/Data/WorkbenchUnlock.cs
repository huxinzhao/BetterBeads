namespace BetterBeads.Data;

public static class WorkbenchUnlock
{
    // Use the unbuffed level so food cannot temporarily unlock the recipe.
    public const string LevelCondition="PLAYER_BASE_FORAGING_LEVEL Current 2";
    public static string ShopCondition(string recipeId)
        =>LevelCondition+", !PLAYER_HAS_CRAFTING_RECIPE Current "+recipeId;
    public static bool ShouldSendMail(bool configured,bool levelReached,bool knowsRecipe,bool received,bool inMailbox,bool forTomorrow)
        =>configured&&levelReached&&!knowsRecipe&&!received&&!inMailbox&&!forTomorrow;
}
