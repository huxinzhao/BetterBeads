using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BetterBeads.Data;

public static class DesignStorage
{
    // An engineering limit, not a finalized gameplay template size.
    public const int MaxDimension = 256;
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    public static bool TryReadBlueprint(string raw, out Blueprint? design)
    {
        design = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<Blueprint>(raw);
            if (parsed is null || !IsStructurallyValid(parsed)) return false;
            design = parsed;
            return true;
        }
        catch (JsonException) { return false; }
    }

    public static bool IsStructurallyValid(Blueprint design)
    {
        if (string.IsNullOrWhiteSpace(design.Id) || design.Name is null || design.TemplateId is null
            || !Enum.IsDefined(design.SwordOrientation)
            || design.Revision < 0 || design.Views is null || design.WoolMaterials is null || design.SupplementaryMaterials is null
            || design.SelectedEffects is null || design.SelectedEffects.Any(string.IsNullOrWhiteSpace)) return false;
        foreach (var pair in design.Views)
        {
            var grid = pair.Value;
            if (string.IsNullOrWhiteSpace(pair.Key) || grid is null || !ValidSize(grid.Width, grid.Height)
                || grid.Cells is null || grid.Cells.Count != grid.Width * grid.Height) return false;
            if (grid.Cells.Any(c => c is not null && string.IsNullOrWhiteSpace(c.ColorId))) return false;
        }
        if (design.WoolMaterials.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0)) return false;
        if (design.SupplementaryMaterials.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0)) return false;
        var pixels = design.Reference;
        return pixels is null || (ValidSize(pixels.Width, pixels.Height)
            && pixels.Pixels is not null && pixels.Pixels.Length == pixels.Width * pixels.Height);
    }

    private static bool ValidSize(int width, int height) => width > 0 && height > 0
        && width <= MaxDimension && height <= MaxDimension;

    public static bool TryReadSnapshot(string raw, out ProductSnapshot? snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<ProductSnapshot>(raw);
            if (parsed is null || parsed.SchemaVersion != 1 || string.IsNullOrWhiteSpace(parsed.InstanceId)
                || string.IsNullOrWhiteSpace(parsed.RulesVersion) || parsed.Design is null
                || (parsed.WeaponRulesVersion is not null && string.IsNullOrWhiteSpace(parsed.WeaponRulesVersion))
                || (parsed.FurnitureVariantId is not null && string.IsNullOrWhiteSpace(parsed.FurnitureVariantId))
                || !IsStructurallyValid(parsed.Design) || parsed.ActualMaterials is null
                || parsed.FinalStats is null || parsed.Effects is null || parsed.EffectParameters is null
                || (parsed.EffectRulesVersion is not null && string.IsNullOrWhiteSpace(parsed.EffectRulesVersion))
                || parsed.EffectParameters.Any(p=>string.IsNullOrWhiteSpace(p.Key) || !double.IsFinite(p.Value))
                || parsed.ActualMaterials.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value <= 0)
                || parsed.FinalStats.Any(p => !double.IsFinite(p.Value))
#if BEADS_LITE
                || !ArtworkMarket.Valid(parsed.Valuation)
#endif
                ) return false;
            snapshot = parsed;
            return true;
        }
        catch (JsonException) { return false; }
    }

    // Include actual pixels, not just blueprint ID/revision: unsaved edits must invalidate previews.
    public static string VisualKey(ProductSnapshot snapshot)
    {
        var text = new StringBuilder();
        text.Append(snapshot.Design.TemplateId).Append('|').Append(snapshot.FurnitureVariantId).Append('|').Append((int)snapshot.Design.Use)
            .Append('|').Append((int)snapshot.Design.SwordOrientation);
        foreach (var view in snapshot.Design.Views.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            text.Append('|').Append(view.Key.Length).Append(':').Append(view.Key)
                .Append(':').Append(view.Value.Width).Append('x').Append(view.Value.Height);
            foreach (var cell in view.Value.Cells)
                text.Append('/').Append(cell is null ? "empty" : cell.Rgba.ToString("X8"));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
