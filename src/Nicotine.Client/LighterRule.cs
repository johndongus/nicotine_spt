using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using HarmonyLib;

namespace Nicotine.Client
{
    internal static class LighterRule
    {
        public static void Patch(Harmony harmony)
        {
            var prefix = new HarmonyMethod(typeof(LighterRule), nameof(Prefix));
            var args = new[] { typeof(FoodDrink), typeof(float), typeof(Callback<IMedsController>), typeof(int), typeof(bool) };
            var patched = new List<string>();
            foreach (var type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(Types).Where(t => typeof(Player).IsAssignableFrom(t)))
            {
                var method = type.GetMethod("Proceed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, args, null);
                if (method != null && !method.IsAbstract)
                {
                    harmony.Patch(method, prefix: prefix);
                    patched.Add(type.Name);
                }
            }

            NicotinePlugin.Log.LogInfo($"Nicotine: lighter rule hooked into {string.Join(", ", patched)}");
        }

        private static IEnumerable<Type> Types(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static bool Prefix(Player __instance, FoodDrink foodDrink, Callback<IMedsController> callback)
        {
            if (!Backend.Config.Packs.RequireLighter || !SmokePatch.IsPack(foodDrink) || __instance == null || !__instance.IsYourPlayer ||
                HasLighter(__instance))
            {
                return true;
            }

            NotificationManager.DisplayMessageNotification("You need a lighter or matches to smoke.", ENotificationDurationType.Default,
                ENotificationIconType.Alert);
            callback?.Invoke(new Result<IMedsController>(null, "No lighter"));
            return false;
        }

        public static bool HasLighter(Player player)
        {
            var ids = Backend.Config.Packs.LighterIds;
            return player.Inventory.GetPlayerItems(EPlayerItems.Equipment).Any(i => ids.Contains(i.TemplateId.ToString()));
        }
    }
}
