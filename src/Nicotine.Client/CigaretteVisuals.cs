using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Comfort.Common;
using Diz.Jobs;
using Diz.Resources;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace Nicotine.Client
{
    internal static class CigaretteVisuals
    {
        public const string CigaretteName = "NicotineCigarette";
        public const string LighterName = "NicotineLighter";
        public const string ExhaleName = "NicotineExhale";
        private const string RightMarker = "weapon_R_IK_marker";
        private const string LeftMarker = "weapon_L_IK_marker";
        private const string CameraBone = "Camera_animated";
        private const string ItemBone = "weapon";

        private const string CigaretteCarrier = "pack_cover001";
        private const string LighterCarrier = "pack_cover00";
        private static readonly string[] PackBones = { "pack_CAT", "pack_root", CigaretteCarrier, LighterCarrier };

        private static readonly HashSet<BaseSoundPlayer> Muted = new HashSet<BaseSoundPlayer>();

        private const string CrickentBundle = "assets/content/items/barter/lighters/item_crickent.bundle";

        private static AssetBundle _bundle;
        private static bool _bundleFailed;
        private static GameObject _cigarettePrefab;
        private static GameObject _lighterPrefab;
        private static GameObject _exhalePrefab;
        private static Vector3 _flameScale = Vector3.one;
        private static AudioClip _lighterSound;
        private static Mesh _crickentMesh;
        private static Material[] _crickentMaterials;
        private static bool _crickentLoading;
        private static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        private static readonly Dictionary<(RuntimeAnimatorController, string), AnimatorOverrideController> Overrides =
            new Dictionary<(RuntimeAnimatorController, string), AnimatorOverrideController>();

        private static readonly string[] SmokeClips = { "nicotine_smoke_1", "nicotine_smoke_2" };
        private static readonly System.Random Random = new System.Random();

        public static void Patch(Harmony harmony)
        {
            var setup = AccessTools.Method(typeof(Player.MedsController), "Setup");
            if (setup == null)
            {
                NicotinePlugin.Log.LogWarning("Nicotine: MedsController.Setup not found, smoking keeps the placeholder animation.");
                return;
            }

            harmony.Patch(setup.MakeGenericMethod(typeof(Player.MedsController)),
                prefix: new HarmonyMethod(typeof(CigaretteVisuals), nameof(SetupPrefix)));

            foreach (var name in new[] { "SoundEventHandler", "SoundAtPointEventHandler", "EventReceiver" })
            {
                var method = AccessTools.Method(typeof(BaseSoundPlayer), name, new[] { typeof(string) });
                if (method != null)
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(CigaretteVisuals), nameof(MutePrefix)));
                }
                else
                {
                    NicotinePlugin.Log.LogWarning($"Nicotine: BaseSoundPlayer.{name} not found, the snack's sounds may play while smoking");
                }
            }
        }

        private static bool MutePrefix(BaseSoundPlayer __instance) => !Muted.Contains(__instance);

        private static bool _hookLogged;

        private static void SetupPrefix(Player.MedsController controller, Player player, Item item)
        {
            try
            {
                if (!_hookLogged)
                {
                    _hookLogged = true;
                    NicotinePlugin.Log.LogInfo($"Nicotine: meds/food setup hook active ({controller?.GetType().Name})");
                }

                var prefab = controller != null && controller._controllerObject != null ? controller._controllerObject.GetComponent<WeaponPrefab>() : null;
                var instance = prefab != null ? prefab._objectInstance : null;
                var animator = instance != null ? instance.GetComponent<Animator>() : null;
                if (animator == null)
                {
                    if (SmokePatch.IsPack(item))
                    {
                        NicotinePlugin.Log.LogWarning($"Nicotine: no hands animator for {item.TemplateId} (prefab {prefab != null}, instance {instance != null})");
                    }

                    return;
                }

                var smoke = SmokePatch.IsPack(item) && player != null && player.IsYourPlayer && LoadBundle();
                if (smoke)
                {
                    Apply(controller, prefab, instance, animator, player);
                }
                else
                {
                    Restore(prefab, instance, animator, player);
                }
            }
            catch (Exception ex)
            {
                NicotinePlugin.Log.LogError($"Nicotine: could not set up the smoking visuals: {ex}");
            }
        }

        private static void Apply(Player.MedsController controller, WeaponPrefab prefab, GameObject instance, Animator animator, Player player)
        {
            var cigaretteCarrier = Find(instance.transform, CigaretteCarrier);
            var lighterCarrier = Find(instance.transform, LighterCarrier);
            if (cigaretteCarrier == null || lighterCarrier == null)
            {
                NicotinePlugin.Log.LogWarning($"Nicotine: {CigaretteCarrier}/{LighterCarrier} not found in {instance.name}, keeping the snack's look");
                return;
            }

            var events = instance.GetComponent<SmokeEvents>() ?? instance.AddComponent<SmokeEvents>();
            events.RememberPack(PackBones.Select(name => Find(instance.transform, name)));

            var cigarette = Attach(instance, cigaretteCarrier, _cigarettePrefab, CigaretteName, true);
            var lighter = _lighterPrefab != null ? Attach(instance, lighterCarrier, _lighterPrefab, LighterName, true) : null;
            if (lighter != null)
            {
                if (_crickentMesh == null)
                {
                    PreloadLighter();
                }

                AddCrickent(lighter);
            }

            var head = Find(instance.transform, CameraBone);
            var exhale = head != null && _exhalePrefab != null ? Attach(instance, head, _exhalePrefab, ExhaleName, false) : null;
            SetItemMeshes(instance, false);

            events.Lighter = lighter;
            events.Sound = _lighterSound;
            events.Exhale = exhale != null ? exhale.GetComponent<ParticleSystem>() : null;
            events.Flame = lighter != null ? lighter.Find("Flame") : null;
            events.FlameScale = _flameScale;
            events.Cigarette = cigarette;
            events.RightHand = player != null && player.PlayerBones != null ? player.PlayerBones.RightPalm : null;
            events.LeftHand = player != null && player.PlayerBones != null ? player.PlayerBones.LeftPalm : null;
            events.RightMarker = Find(instance.transform, RightMarker);
            events.LeftMarker = Find(instance.transform, LeftMarker);
            events.Head = player != null && player.ProceduralWeaponAnimation != null && player.ProceduralWeaponAnimation.HandsContainer != null
                ? player.ProceduralWeaponAnimation.HandsContainer.CameraTransform
                : null;
            events.View = player != null ? player.ProceduralWeaponAnimation : null;
            events.Owner = player;
            events.Controller = controller;
            events.Prefab = prefab;
            events.BeginSmoke();
            if (cigarette == null)
            {
                return;
            }

            var sound = prefab.GetComponent<BaseSoundPlayer>();
            if (sound != null)
            {
                Muted.Add(sound);
            }

            events.Measure(player, cigarette, lighter, Find(instance.transform, RightMarker), Find(instance.transform, LeftMarker));

            var original = prefab.AnimatorController;
            if (original != null)
            {
                SwapController(prefab, animator, OverrideFor(original, SmokeClips[Random.Next(SmokeClips.Length)]), player);
            }
        }

        private static Transform Attach(GameObject instance, Transform parent, GameObject prefab, string name, bool onCarrier)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = UnityEngine.Object.Instantiate(prefab, parent, false).transform;
                child.name = name;
                MatchItemLook(instance, child.gameObject);
            }

            if (onCarrier)
            {
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
            }

            child.gameObject.SetActive(true);
            return child;
        }

        private static void Restore(WeaponPrefab prefab, GameObject instance, Animator animator, Player player)
        {
            var sound = prefab.GetComponent<BaseSoundPlayer>();
            if (sound != null)
            {
                Muted.Remove(sound);
            }

            var ours = instance.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == CigaretteName || t.name == LighterName || t.name == ExhaleName).ToList();
            if (ours.Count == 0)
            {
                return;
            }

            foreach (var t in ours)
            {
                UnityEngine.Object.Destroy(t.gameObject);
            }

            var events = instance.GetComponent<SmokeEvents>();
            if (events != null)
            {
                events.RestorePack();
                events.enabled = false;
            }
            SetItemMeshes(instance, true);
            if (prefab.AnimatorController != null)
            {
                SwapController(prefab, animator, prefab.AnimatorController, player);
            }
        }

        private static void SwapController(WeaponPrefab prefab, Animator animator, RuntimeAnimatorController controller, Player player)
        {
            if (animator.runtimeAnimatorController == controller)
            {
                return;
            }

            animator.runtimeAnimatorController = controller;
            if (player != null)
            {
                prefab.RebindAnimator(player);
            }
            else
            {
                animator.Rebind();
            }
        }

        private static void MatchItemLook(GameObject instance, GameObject model)
        {
            var itemBone = Find(instance.transform, ItemBone);
            var reference = itemBone != null ? itemBone.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => !IsOurs(r.transform)) : null;
            foreach (var transform in model.GetComponentsInChildren<Transform>(true))
            {
                if (reference != null)
                {
                    transform.gameObject.layer = reference.gameObject.layer;
                }
            }

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null || material.shader.isSupported)
                    {
                        continue;
                    }

                    var fallback = reference != null && reference.sharedMaterial != null ? reference.sharedMaterial.shader : null;
                    NicotinePlugin.Log.LogWarning($"Nicotine: shader {material.shader.name} unsupported on {renderer.name}, using {fallback?.name ?? "none"}");
                    if (fallback != null)
                    {
                        var texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                        var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                        material.shader = fallback;
                        if (texture != null && material.HasProperty("_MainTex"))
                        {
                            material.SetTexture("_MainTex", texture);
                        }

                        if (material.HasProperty("_Color"))
                        {
                            material.SetColor("_Color", color);
                        }
                    }
                }
            }

            NicotinePlugin.Log.LogInfo($"Nicotine: {model.name} on layer {model.layer}, shaders " +
                string.Join(", ", model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null)
                    .Select(m => m.shader.name).Distinct()));
        }

        private static void SetItemMeshes(GameObject instance, bool visible)
        {
            var item = Find(instance.transform, ItemBone);
            if (item == null)
            {
                return;
            }

            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsOurs(renderer.transform))
                {
                    renderer.enabled = visible;
                }
            }
        }

        private static bool IsOurs(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name == CigaretteName || t.name == LighterName || t.name == ExhaleName)
                {
                    return true;
                }
            }

            return false;
        }

        private static AnimatorOverrideController OverrideFor(RuntimeAnimatorController original, string smoke)
        {
            if (Overrides.TryGetValue((original, smoke), out var cached))
            {
                return cached;
            }

            var result = new AnimatorOverrideController(original) { name = $"{original.name}_{smoke}" };
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(result.overridesCount);
            result.GetOverrides(pairs);
            var replaced = new List<string>();
            for (var i = 0; i < pairs.Count; i++)
            {
                var name = pairs[i].Key != null ? pairs[i].Key.name : "";
                if (name.Contains("_3rd") || !name.StartsWith("item_"))
                {
                    continue;
                }

                var ours = name.EndsWith("_use_1") ? smoke
                    : name.EndsWith("_use_2") ? "nicotine_flick"
                    : name.EndsWith("_start") ? "nicotine_start"
                    : name.Contains("_abort") ? "nicotine_abort"
                    : null;
                if (ours != null && Clips.TryGetValue(ours, out var clip))
                {
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, clip);
                    replaced.Add($"{name} -> {ours}");
                }
            }

            result.ApplyOverrides(pairs);
            NicotinePlugin.Log.LogInfo($"Nicotine: smoking animation set up on {original.name}: {string.Join(", ", replaced)}");
            Overrides[(original, smoke)] = result;
            return result;
        }

        private static bool LoadBundle()
        {
            if (_bundle != null)
            {
                return true;
            }

            if (_bundleFailed)
            {
                return false;
            }

            var path = Path.Combine(Path.GetDirectoryName(typeof(NicotinePlugin).Assembly.Location) ?? ".", "nicotine.bundle");
            _bundle = File.Exists(path) ? AssetBundle.LoadFromFile(path) : null;
            if (_bundle == null)
            {
                _bundleFailed = true;
                NicotinePlugin.Log.LogWarning($"Nicotine: {path} missing or unreadable, smoking keeps the placeholder animation.");
                return false;
            }

            var prefabs = _bundle.LoadAllAssets<GameObject>();
            _cigarettePrefab = prefabs.FirstOrDefault(g => g.name == CigaretteName);
            _lighterPrefab = prefabs.FirstOrDefault(g => g.name == LighterName);
            _exhalePrefab = prefabs.FirstOrDefault(g => g.name == ExhaleName);
            var flame = _lighterPrefab != null ? _lighterPrefab.transform.Find("Flame") : null;
            _flameScale = flame != null ? flame.localScale : Vector3.one;
            _lighterSound = _bundle.LoadAllAssets<AudioClip>().FirstOrDefault();
            foreach (var clip in _bundle.LoadAllAssets<AnimationClip>())
            {
                Clips[clip.name] = clip;
            }

            NicotinePlugin.Log.LogInfo($"Nicotine: loaded {path} (cigarette {(_cigarettePrefab != null ? "ok" : "MISSING")}, " +
                                       $"lighter {(_lighterPrefab != null ? "ok" : "MISSING")}, sound {(_lighterSound != null ? "ok" : "MISSING")}, " +
                                       $"exhale {(_exhalePrefab != null ? "ok" : "MISSING")}, " +
                                       $"clips {string.Join(", ", Clips.Keys)})");
            return _cigarettePrefab != null;
        }

        public static async void PreloadLighter()
        {
            if (_crickentMesh != null || _crickentLoading)
            {
                return;
            }

            if (!Singleton<ObjectsFactory>.Instantiated)
            {
                NicotinePlugin.Log.LogWarning("Nicotine: the game's object factory isn't up, the light-up shows only the flame");
                return;
            }

            _crickentLoading = true;
            try
            {
                await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid, ObjectsFactory.AssemblyType.Local,
                    new[] { new ResourceKey { path = CrickentBundle } }, JobYieldPriority.General, null, ObjectsFactory.DefaultCancellationToken);
                var prefab = Singleton<IEasyAssets>.Instance.GetAsset<GameObject>(CrickentBundle);
                var renderer = prefab != null
                    ? prefab.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponent<MeshFilter>()?.sharedMesh != null)
                    : null;
                if (renderer != null)
                {
                    _crickentMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    _crickentMaterials = renderer.sharedMaterials;
                }

                NicotinePlugin.Log.LogInfo($"Nicotine: Crickent lighter model {(renderer != null ? "loaded" : "not found")}");
            }
            catch (Exception ex)
            {
                NicotinePlugin.Log.LogWarning($"Nicotine: could not load the Crickent lighter, the light-up shows only the flame: {ex.Message}");
            }
            finally
            {
                _crickentLoading = false;
            }
        }

        private static void AddCrickent(Transform lighter)
        {
            if (_crickentMesh == null)
            {
                return;
            }

            var model = lighter.Find("Model");
            if (model == null)
            {
                model = new GameObject("Model").transform;
                model.SetParent(lighter, false);
                model.gameObject.layer = lighter.gameObject.layer;
                model.gameObject.AddComponent<MeshFilter>();
                model.gameObject.AddComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var filter = model.GetComponent<MeshFilter>();
            if (filter.sharedMesh != _crickentMesh)
            {
                filter.sharedMesh = _crickentMesh;
                model.GetComponent<MeshRenderer>().sharedMaterials = _crickentMaterials;
            }
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                var found = Find(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
