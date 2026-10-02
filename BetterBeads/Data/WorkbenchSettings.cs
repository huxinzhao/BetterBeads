namespace BetterBeads.Data;

public sealed class WorkbenchSettings
{
    public string InputMode { get; set; } = "auto";
    public bool AllowValuableOrdinarySources { get; set; }
    public bool CreativeMode { get; set; }
    public bool ReducedMotion { get; set; }
    public bool HoverSounds { get; set; } = true;
    public bool ClickSounds { get; set; } = true;
    public bool BeadSounds { get; set; } = true;
    public bool SuccessSounds { get; set; } = true;
    public int NearbyChestRadius { get; set; } = 5;
    public int? RecipePrice { get; set; } = 5000;
    public Dictionary<string, int> Ingredients { get; set; } = new()
    {
        ["388"] = 250,
        ["390"] = 100,
        ["334"] = 5
    };
    public bool IsConfigured => RecipePrice is >= 0 && Ingredients is { Count: > 0 }
        && Ingredients.All(p => !string.IsNullOrWhiteSpace(p.Key) && !p.Key.Any(char.IsWhiteSpace)
            && !p.Key.Contains('/') && p.Value > 0);
    public string Recipe(string itemId) => IsConfigured
        ? $"{string.Join(" ", Ingredients.Select(p => $"{p.Key} {p.Value}"))}/Home/{itemId}/true/none"
        : throw new InvalidOperationException("Workbench balance has not been confirmed.");
}
