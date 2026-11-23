using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace Nicotine.Client
{
    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.DoMedEffect))]
    internal static class SmokePatch
    {
        private const float Margin = 0.001f;

        public static bool IsPack(Item item) => item is FoodDrink && Backend.PackIds.Contains(item.TemplateId.ToString());

        [HarmonyPrefix]
        private static void Prefix(ActiveHealthController __instance, Item item, ref float? amount)
        {
            if (!(item is FoodDrink food) || !IsPack(item) || __instance.Player == null || !__instance.Player.IsYourPlayer)
            {
                return;
            }

            var component = food.FoodDrinkComponent;
            if (component.MaxResource <= 0f || component.HpPercent <= 0f)
            {
                return;
            }

            var cigarette = Mathf.Min(1f, component.HpPercent);
            amount = (cigarette > Margin * 2f ? cigarette - Margin : cigarette) / component.MaxResource;
        }
    }
}
