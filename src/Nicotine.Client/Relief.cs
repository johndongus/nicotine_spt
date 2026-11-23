using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EFT.HealthSystem;

namespace Nicotine.Client
{
    internal static class Relief
    {
        private const float MinRemaining = 0.5f;

        public static void Apply(ActiveHealthController health, ReliefConfig config, float strength, ICollection<string> withdrawalSets)
        {
            var effects = health.Effects.ToArray();
            var drivenByBuffs = new HashSet<object>();

            foreach (var stimulator in effects.OfType<ActiveHealthController.Stimulator>())
            {
                var name = stimulator.Name ?? "";
                var withdrawal = withdrawalSets.Contains(name);
                var ours = withdrawal || name == config.CalmBuffSet;
                foreach (var buff in stimulator._buffs)
                {
                    CollectEffects(buff.ValueObj, drivenByBuffs);
                    if (buff.Settings == null)
                    {
                        continue;
                    }

                    if (buff.Settings.IsBuff)
                    {
                        if (!ours)
                        {
                            Extend(health, stimulator, buff, config, config.PositiveBuffBoost * strength);
                        }

                        continue;
                    }

                    if (withdrawal)
                    {
                        Cut(health, stimulator, buff, 1f);
                    }
                    else if (!ours)
                    {
                        Cut(health, stimulator, buff, BuffCut(buff.Settings.BuffType.ToString(), config) * strength);
                    }
                }
            }

            foreach (var effect in effects)
            {
                if (drivenByBuffs.Contains(effect))
                {
                    continue;
                }

                switch (effect)
                {
                    case ActiveHealthController.Tremor _:
                        Cut(effect, (config.RemoveTremor ? 1f : config.NegativeBuffCut) * strength);
                        break;
                    case ActiveHealthController.TunnelVision _:
                        Cut(effect, config.TunnelVisionCut * strength);
                        break;
                    case ActiveHealthController.Contusion _:
                        Cut(effect, config.ContusionCut * strength);
                        break;
                    case ActiveHealthController.Stun _:
                        Cut(effect, config.StunCut * strength);
                        break;
                }
            }
        }

        private static float BuffCut(string buffType, ReliefConfig config)
        {
            switch (buffType)
            {
                case "HandsTremor":
                    return config.RemoveTremor ? 1f : config.NegativeBuffCut;
                case "QuantumTunnelling":
                    return config.TunnelVisionCut;
                case "Contusion":
                case "ContusionBlur":
                case "ContusionWiggle":
                    return config.ContusionCut;
                case "Pain":
                case "HealthRate":
                case "EnergyRate":
                case "HydrationRate":
                case "SkillRate":
                case "MaxStamina":
                case "StaminaRate":
                case "WeightLimit":
                    return config.NegativeBuffCut;
                default:
                    return 0f;
            }
        }

        private static void Cut(ActiveHealthController health, ActiveHealthController.Stimulator stimulator, ActiveHealthController.Stimulator.Buff buff, float fraction)
        {
            if (fraction <= 0f)
            {
                return;
            }

            fraction = Math.Min(fraction, 1f);
            var settings = buff.Settings;
            if (buff.Active)
            {
                var remaining = settings.Delay + settings.Duration - stimulator._curTime;
                if (remaining <= 0f)
                {
                    return;
                }

                var keep = remaining * (1f - fraction);
                settings.Duration = Math.Max(0f, stimulator._curTime + (keep < MinRemaining ? 0f : keep) - settings.Delay);
                health.StimulatorBuffEventCall(buff);
            }
            else if (!buff.Processed)
            {
                if (fraction >= 0.999f)
                {
                    buff.Processed = true;
                }
                else
                {
                    settings.Duration *= 1f - fraction;
                }
            }
        }

        private static readonly ConditionalWeakTable<ActiveHealthController.Stimulator.Buff, StrongBox<float>> Extended =
            new ConditionalWeakTable<ActiveHealthController.Stimulator.Buff, StrongBox<float>>();

        private static void Extend(ActiveHealthController health, ActiveHealthController.Stimulator stimulator, ActiveHealthController.Stimulator.Buff buff,
            ReliefConfig config, float fraction)
        {
            var settings = buff.Settings;
            if (fraction <= 0f || settings.Duration <= 0f)
            {
                return;
            }

            float remaining;
            if (buff.Active)
            {
                remaining = settings.Delay + settings.Duration - stimulator._curTime;
            }
            else if (!buff.Processed)
            {
                remaining = settings.Duration;
            }
            else
            {
                return;
            }

            var original = buff.OriginalSettings != null && buff.OriginalSettings.Duration > 0f ? buff.OriginalSettings.Duration : settings.Duration;
            var used = Extended.GetOrCreateValue(buff);
            var add = Math.Min(remaining * fraction, Math.Min(config.PositiveBuffBoostMaxSeconds, original * config.PositiveBuffBoostCap - used.Value));
            if (remaining <= 0f || add < 0.1f)
            {
                return;
            }

            settings.Duration += add;
            used.Value += add;
            if (buff.Active)
            {
                health.StimulatorBuffEventCall(buff);
            }
        }

        private static void Cut(ActiveHealthController.Effect effect, float fraction)
        {
            if (fraction <= 0f || !effect.Active)
            {
                return;
            }

            fraction = Math.Min(fraction, 1f);
            var remaining = effect.CurStateTimeLeft;
            if (float.IsInfinity(remaining) || float.IsNaN(remaining))
            {
                if (fraction >= 0.999f)
                {
                    effect.ForceResidue();
                }

                return;
            }

            var keep = remaining * (1f - fraction);
            if (keep < MinRemaining)
            {
                effect.ForceResidue();
            }
            else
            {
                effect.AddWorkTime(keep, true);
            }
        }

        private static void CollectEffects(object value, HashSet<object> into)
        {
            if (value is ActiveHealthController.Effect effect)
            {
                into.Add(effect);
            }
            else if (value is IEnumerable list && !(value is string))
            {
                foreach (var item in list)
                {
                    if (item is ActiveHealthController.Effect inner)
                    {
                        into.Add(inner);
                    }
                }
            }
        }
    }
}
