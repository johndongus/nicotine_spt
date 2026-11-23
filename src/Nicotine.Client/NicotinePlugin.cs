using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Nicotine.Client
{
    [BepInPlugin(Guid, "Nicotine", Version)]
    public sealed class NicotinePlugin : BaseUnityPlugin
    {
        public const string Guid = "com.nicotine.client";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ShowNotifications;
        internal static ConfigEntry<bool> ShowSmokeInfo;
        internal static ConfigEntry<bool> LookDown;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Track addiction and withdrawal. One cigarette per use always applies.");
            ShowNotifications = Config.Bind("General", "Notifications", true, "Craving, withdrawal and milestone messages.");
            ShowSmokeInfo = Config.Bind("General", "Smoke info", true, "After each cigarette, show what's left in the pack and your addiction.");
            LookDown = Config.Bind("General", "Look down while smoking", true,
                "The view dips to the lighter's flame while lighting up, and a little with each drag, whatever your Head Bobbing " +
                "setting. The game scales these camera moves by Head Bobbing; at its lowest (0.2) the lighting happens at the bottom " +
                "edge of the screen.");

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(NicotinePlugin).Assembly);
            CigaretteVisuals.Patch(harmony);
            LighterRule.Patch(harmony);

            gameObject.AddComponent<Addiction>();
            Log.LogInfo($"Nicotine {Version} loaded");
        }
    }
}
