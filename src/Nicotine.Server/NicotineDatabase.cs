using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Common.Models.Logging;

namespace Nicotine.Server;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload + 80100)]
public sealed class NicotineDatabase(
    NicotineContext context,
    TemplateTable templates,
    GlobalTable globals,
    LocaleTable locales,
    ISptLogger<NicotineDatabase> logger) : IOnLoad
{
    private const string FoodParent = "5448e8d04bdc2ddf718b4569";

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        if (context.ConfigError != null)
        {
            logger.Warning($"[Nicotine] {context.ConfigError}");
        }
        else if (context.ConfigCreated)
        {
            logger.Info($"[Nicotine] Wrote default config to {System.IO.Path.Combine(context.ModDirectory, "config.json")}");
        }
        else if (context.ConfigUpgraded)
        {
            logger.Info("[Nicotine] Added new options to config.json (your values are kept).");
        }

        var config = context.Config;
        if (!config.Enabled)
        {
            logger.Info("[Nicotine] Disabled in config.json.");
            return Task.CompletedTask;
        }

        RegisterBuffSets(config);

        var converted = 0;
        foreach (var id in config.Packs.Ids)
        {
            if (ConvertPack(id, config.Packs))
            {
                converted++;
            }
        }

        if (config.Packs.Describe)
        {
            RegisterDescriptions(config);
        }

        logger.Success($"[Nicotine] {converted} cigarette pack type(s) are now smokable ({config.Packs.Cigarettes} per pack).");
        return Task.CompletedTask;
    }

    private void RegisterDescriptions(NicotineConfig config)
    {
        var details = Details(config);
        foreach (var language in locales.Global.Values)
        {
            language.AddTransformer(strings =>
            {
                if (strings == null)
                {
                    return strings;
                }

                foreach (var id in config.Packs.Ids)
                {
                    var key = $"{id} Description";
                    var flavour = strings.TryGetValue(key, out var original) ? original?.Trim() : null;
                    strings[key] = string.IsNullOrEmpty(flavour) ? details : $"{flavour}\n\n{details}";
                }

                return strings;
            });
        }
    }

    private static string Details(NicotineConfig config)
    {
        var packs = config.Packs;
        var lines = new List<string>
        {
            $"Smokable: {packs.Cigarettes} cigarettes per pack, one per use ({Number(packs.SmokeSeconds)} s). The stats listed are for one cigarette.",
            $"Per cigarette: {Signed(packs.EnergyPerCigarette)} energy, {Signed(packs.HydrationPerCigarette)} hydration" +
            (packs.PainReliefSeconds > 0 ? $", {Number(packs.PainReliefSeconds)} s of pain relief." : "."),
            "Takes the edge off: ends hand tremor and tunnel vision, and shortens concussion, stuns and the side effects of " +
            "stimulants and alcohol. Positive effects you already have last longer. Chain smoking works less.",
        };

        if (packs.RequireLighter)
        {
            lines.Insert(1, "Needs a lighter or matches in your gear.");
        }

        if (config.BuffSets.TryGetValue(config.Relief.CalmBuffSet, out var rush) && rush.Count > 0)
        {
            var now = rush.Where(b => b.Delay <= 1).ToList();
            var comedown = rush.Where(b => b.Delay > 1).ToList();
            if (now.Count > 0)
            {
                lines.Add($"Nicotine rush for {Time(now.Max(b => b.Duration))}: {string.Join(", ", now.Select(Describe).Where(p => p != null))}.");
            }

            if (comedown.Count > 0)
            {
                lines.Add($"Then a {Time(comedown.Max(b => b.Duration))} comedown: {string.Join(", ", comedown.Select(Describe).Where(p => p != null))}.");
            }

            lines.Add($"One rush at a time: smoking again within {Time(rush.Max(b => b.Delay + b.Duration) - 1)} only takes the edge off.");
        }

        lines.Add("Habit-forming: the more you smoke, the sooner you crave the next one. Go without and the restlessness, " +
                  "shakes and headaches set in, and they get worse. Smoking ends them; quitting takes a while.");
        return string.Join("\n", lines);
    }

    private static string? Describe(BuffConfig buff) => buff.BuffType switch
    {
        "HealthRate" when buff.Value > 0 => $"heals {Number(buff.Value * buff.Duration)} HP over {Time(buff.Duration)}",
        "HealthRate" => $"{Signed(buff.Value * buff.Duration)} HP over {Time(buff.Duration)}",
        "EnergyRate" => $"{Signed(buff.Value * buff.Duration)} energy",
        "HydrationRate" => $"{Signed(buff.Value * buff.Duration)} hydration",
        "SkillRate" => $"{Signed(buff.Value)} {SplitWords(buff.SkillName)}",
        "MaxStamina" => $"{Signed(buff.Value)} max stamina",
        "StaminaRate" => $"{Signed(buff.Value)} stamina recovery",
        _ => null,
    };

    private static string Time(double seconds) => seconds >= 60 && seconds % 60 == 0 ? $"{Number(seconds / 60)} min" : $"{Number(seconds)} s";

    private static string Signed(double value) => (value > 0 ? "+" : "") + Number(value);

    private static string Number(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string SplitWords(string name) => System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");

    private bool ConvertPack(string id, PackConfig packs)
    {
        if (!templates.Items.TryGetValue(new MongoId(id), out var item) || item.Properties == null)
        {
            logger.Warning($"[Nicotine] Item {id} not found, skipped.");
            return false;
        }

        var p = item.Properties;
        var n = packs.Cigarettes;
        item.Parent = new MongoId(FoodParent);
        p.MaxResource = n;
        p.StackMaxSize = 1;
        p.FoodEffectType = "afterUse";
        p.UsePrefab = new Prefab { Path = packs.UsePrefab, Rcid = "" };
        p.ItemSound = packs.ItemSound;
        p.StimulatorBuffs = "";

        p.FoodUseTime = packs.SmokeSeconds * n;
        p.EffectsHealth = new Dictionary<HealthFactor, EffectsHealthProperties>
        {
            [HealthFactor.Energy] = new() { Value = packs.EnergyPerCigarette * n },
            [HealthFactor.Hydration] = new() { Value = packs.HydrationPerCigarette * n },
        };
        p.EffectsDamage = packs.PainReliefSeconds > 0
            ? new Dictionary<DamageEffectType, EffectsDamageProperties> { [DamageEffectType.Pain] = new() { Delay = 0, Duration = packs.PainReliefSeconds * n } }
            : new Dictionary<DamageEffectType, EffectsDamageProperties>();

        return true;
    }

    private void RegisterBuffSets(NicotineConfig config)
    {
        var buffs = globals.Configuration.Health.Effects.Stimulator.Buffs;
        foreach (var (name, entries) in config.BuffSets)
        {
            buffs[name] = entries.Select(b => new Buff
            {
                BuffType = b.BuffType,
                Chance = b.Chance,
                Delay = b.Delay,
                Duration = b.Duration,
                Value = b.Value,
                AbsoluteValue = b.AbsoluteValue,
                SkillName = b.SkillName ?? "",
                AppliesTo = b.AppliesTo ?? [],
            }).ToList();
        }
    }
}
