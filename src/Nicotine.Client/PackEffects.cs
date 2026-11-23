using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using JsonType;

namespace Nicotine.Client
{
    [HarmonyPatch(typeof(FoodDrink), MethodType.Constructor, typeof(string), typeof(FoodDrinkTemplate))]
    internal static class PackEffects
    {
        [HarmonyPostfix]
        private static void Postfix(FoodDrink __instance)
        {
            if (!SmokePatch.IsPack(__instance) || __instance.HealthEffectsComponent == null)
            {
                return;
            }

            PerCigarette(__instance);
            AddRush(__instance);
        }

        private static void PerCigarette(FoodDrink pack)
        {
            var n = pack.FoodDrinkComponent?.MaxResource ?? 0f;
            var health = pack.HealthEffectsComponent;
            if (n <= 1f)
            {
                return;
            }

            foreach (var attribute in pack.Attributes)
            {
                var name = attribute.Name;
                switch (attribute.Id)
                {
                    case EItemAttributeId id when id == EItemAttributeId.UseTime:
                        var time = (health.UseTime / n).ToString("0.#");
                        attribute.StringValue = () => time + "sec".Localized();
                        attribute.FullStringValue = () => "USE TIME".Localized() + " " + time + "sec".Localized();
                        break;
                    case EHealthFactorType factor when health.HealthEffects != null && health.HealthEffects.TryGetValue(factor, out var spec):
                        var one = new HealthEffectSpecification { Delay = spec.Delay, Duration = spec.Duration / n, Value = spec.Value / n };
                        attribute.Base = () => one.Value;
                        attribute.StringValue = () => one.GetStringValue();
                        attribute.FullStringValue = () => one.GetFullStringValue(name);
                        break;
                    case EDamageEffectType type when health.DamageEffects != null && health.DamageEffects.TryGetValue(type, out var damage):
                        var cut = new DamageEffectSpecification
                        {
                            Delay = damage.Delay,
                            Duration = damage.Duration / n,
                            FadeOut = damage.FadeOut,
                            Cost = damage.Cost,
                            HealthPenaltyMin = damage.HealthPenaltyMin,
                            HealthPenaltyMax = damage.HealthPenaltyMax,
                        };
                        attribute.StringValue = () => cut.GetStringValue();
                        attribute.FullStringValue = () => cut.GetFullStringValue(name);
                        break;
                }
            }
        }

        private static void AddRush(FoodDrink pack)
        {
            if (!string.IsNullOrEmpty(pack.HealthEffectsComponent.StimulatorBuffs) || !Singleton<GlobalConfiguration>.Instantiated)
            {
                return;
            }

            var set = Backend.Config?.Relief?.CalmBuffSet;
            var buffs = Singleton<GlobalConfiguration>.Instance.Health?.Effects?.Stimulator?.Buffs;
            if (string.IsNullOrEmpty(set) || buffs == null || !buffs.ContainsKey(set))
            {
                return;
            }

            new StimulatorBuffsComponent(pack, new BuffSetName(set)).SetupStimulatorBuffsAttributes(pack);
        }

        private sealed class BuffSetName : IStimulatorBuffsComponentTemplate
        {
            public BuffSetName(string name) => StimulatorBuffs = name;

            public string StimulatorBuffs { get; }
        }
    }
}
