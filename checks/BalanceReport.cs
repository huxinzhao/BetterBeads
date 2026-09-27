using BetterBeads.Data;
using System.Globalization;

internal static class BalanceReport
{
    public static void Write()
    {
        var catalog=DefaultProcessing.Create();
        var examples=new (string Name,Dictionary<string,int> Bill,string[] Effects)[]{
            ("copper",new(){{"copper",100}},Array.Empty<string>()),
            ("iron",new(){{"iron",100}},Array.Empty<string>()),
            ("gold",new(){{"gold",100}},Array.Empty<string>()),
            ("iridium",new(){{"iridium",100}},Array.Empty<string>()),
            ("diamond",new(){{"diamond",100}},Array.Empty<string>()),
            ("iron80_ruby20",new(){{"iron",80},{"ruby",20}},new[]{"ruby"}),
            ("refined_iron60_jade20_amethyst20",new(){{"refined-iron",60},{"jade",20},{"amethyst",20}},new[]{"jade","amethyst"})
        };
        Console.WriteLine("weapon,composition,min_damage,max_damage,speed,knockback,crit_chance,crit_multiplier,life_steal_rate");
        foreach(var recipe in DefaultWeapons.Recipes())foreach(var sample in examples)
        {
            var effects=SpecialEffects.Evaluate(recipe.Template.Use,catalog,sample.Bill,sample.Effects);
            var v=WeaponStats.Calculate(recipe.Template.Use,recipe.WeaponRules!,catalog,sample.Bill,effects).Values!;
            Console.WriteLine(string.Join(",",new[]{recipe.Template.Use.ToString(),sample.Name,
                v.MinDamage.ToString(),v.MaxDamage.ToString(),v.Speed.ToString(),
                v.Knockback.ToString("0.###",CultureInfo.InvariantCulture),v.CritChance.ToString("0.####",CultureInfo.InvariantCulture),
                v.CritMultiplier.ToString("0.###",CultureInfo.InvariantCulture),effects.LifeStealRate.ToString("0.###",CultureInfo.InvariantCulture)}));
        }
    }
}
