using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using UnityEngine;

namespace Nicotine.Client
{
    internal sealed class Addiction : MonoBehaviour
    {
        private const float SaveEverySeconds = 60f;
        private static readonly int[] Milestones = { 25, 50, 75 };

        private readonly List<float> _recentSmokes = new List<float>();
        private readonly List<float> _chain = new List<float>();
        private float _coughUntil;
        private float _nextCoughAt;
        private readonly System.Random _random = new System.Random();

        private GameWorld _world;
        private GameWorld _explained;
        private Player _player;
        private ActiveHealthController _health;
        private SavedState _state;
        private bool _loaded;
        private float _nextSaveAt;
        private float _nextEpisodeAt;
        private bool _craving;

        private NicotineConfig Config => Backend.Config;

        private bool _failed;
        private GameWorld _noPlayer;

        private void Start() => NicotinePlugin.Log.LogInfo("Nicotine: raid tracker running");

        private void Update()
        {
            try
            {
                Watch();
            }
            catch (Exception ex)
            {
                if (!_failed)
                {
                    _failed = true;
                    NicotinePlugin.Log.LogError($"Nicotine: raid tracker failed: {ex}");
                }
            }
        }

        private void Watch()
        {
            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            var player = world != null ? world.MainPlayer : null;
            if (world != null && player == null)
            {
                player = world.AllAlivePlayersList?.FirstOrDefault(p => p != null && p.IsYourPlayer);
                if (player == null && !ReferenceEquals(world, _noPlayer))
                {
                    _noPlayer = world;
                    NicotinePlugin.Log.LogInfo($"Nicotine: world '{world.LocationId}' has no player of yours yet");
                }
            }
            var inRaid = NicotinePlugin.Enabled.Value && player != null && player.IsYourPlayer && player.ActiveHealthController != null &&
                         !string.IsNullOrEmpty(world.LocationId) && !world.LocationId.Equals("hideout", StringComparison.OrdinalIgnoreCase);

            if (_world != null && (!inRaid || !ReferenceEquals(world, _world)))
            {
                EndRaid();
            }

            if (!inRaid)
            {
                if (world != null && player != null && !ReferenceEquals(world, _explained))
                {
                    _explained = world;
                    NicotinePlugin.Log.LogInfo($"Nicotine: not tracking here: enabled {NicotinePlugin.Enabled.Value}, own player {player.IsYourPlayer}, " +
                                               $"health {player.ActiveHealthController != null}, location '{world.LocationId}'");
                }

                return;
            }

            if (_world == null)
            {
                BeginRaid(world, player);
            }

            if (_health == null || !_player.HealthController.IsAlive)
            {
                return;
            }

            Cough();
            if (_loaded)
            {
                Tick(Time.deltaTime);
            }
        }

        private void Cough()
        {
            var now = Time.time;
            if (now >= _coughUntil || now < _nextCoughAt)
            {
                return;
            }

            var overdo = Config.Overdo;
            _nextCoughAt = now + Mathf.Lerp(overdo.CoughIntervalMin, overdo.CoughIntervalMax, (float)_random.NextDouble());
            if (Enum.TryParse(overdo.CoughPhrase, true, out EPhraseTrigger phrase))
            {
                _player.Say(phrase, true);
            }
        }

        private void OnDestroy() => EndRaid();

        private async void BeginRaid(GameWorld world, Player player)
        {
            _world = world;
            _player = player;
            _health = player.ActiveHealthController;
            _recentSmokes.Clear();
            _chain.Clear();
            _coughUntil = 0f;
            _craving = false;
            _loaded = false;
            NicotinePlugin.Log.LogInfo($"Nicotine: raid on '{world.LocationId}' started, loading addiction state");
            CigaretteVisuals.PreloadLighter();
            if (_health != null)
            {
                _health.EffectResidualEvent += OnEffectResidual;
            }

            try
            {
                var state = await Backend.LoadAsync();
                if (ReferenceEquals(_world, world))
                {
                    _state = state;
                    _loaded = true;
                    _nextSaveAt = Time.time + SaveEverySeconds;
                    _nextEpisodeAt = Time.time + 20f;
                    NicotinePlugin.Log.LogInfo($"Nicotine: addiction {_state.Addiction:0.#}, {Minutes(_state.SinceLastSmoke)} since the last cigarette, {_state.Smoked} smoked");
                }
            }
            catch (Exception ex)
            {
                NicotinePlugin.Log.LogWarning($"Nicotine could not load addiction state, not tracking it this raid: {ex.Message}");
            }
        }

        private void EndRaid()
        {
            if (_health != null)
            {
                _health.EffectResidualEvent -= OnEffectResidual;
            }

            if (_loaded)
            {
                _ = Backend.SaveAsync(_state);
            }

            _world = null;
            _player = null;
            _health = null;
            _loaded = false;
        }

        private void OnEffectResidual(IHealthEffect effect)
        {
            if (_health == null || !(effect is ActiveHealthController.MedEffect med) || med._interrupted || !SmokePatch.IsPack(med.MedItem))
            {
                return;
            }

            try
            {
                OnSmoked(med.MedItem);
            }
            catch (Exception ex)
            {
                NicotinePlugin.Log.LogError($"Nicotine: handling a smoke failed: {ex}");
            }
        }

        private void OnSmoked(Item pack)
        {
            var now = Time.time;
            var relief = Config.Relief;
            _recentSmokes.RemoveAll(t => now - t > relief.RepeatWindowSeconds);
            var strength = Mathf.Pow(Mathf.Clamp01(relief.RepeatFactor), _recentSmokes.Count);
            _recentSmokes.Add(now);

            var wasCraving = Severity() >= Config.Withdrawal.CravingNoticeAt;
            Relief.Apply(_health, relief, strength, WithdrawalSets());
            if (!string.IsNullOrEmpty(relief.CalmBuffSet))
            {
                _health.TryDoExternalBuff(relief.CalmBuffSet);
            }

            var overdo = Config.Overdo;
            _chain.RemoveAll(t => now - t > overdo.ChainWindowSeconds);
            _chain.Add(now);
            if (overdo.ChainLimit > 0 && _chain.Count > overdo.ChainLimit)
            {
                if (!string.IsNullOrEmpty(overdo.BuffSet))
                {
                    _health.TryDoExternalBuff(overdo.BuffSet);
                }

                if (now >= _coughUntil && !string.IsNullOrEmpty(overdo.Message))
                {
                    Notify(overdo.Message, true);
                }

                _coughUntil = now + overdo.CoughSeconds;
                _nextCoughAt = now + 1.5f;
            }

            if (!_loaded)
            {
                return;
            }

            var before = _state.Addiction;
            var a = Config.Addiction;
            _state.Addiction = Mathf.Clamp(before + a.PerCigarette * Mathf.Max(0f, 1f - before / Mathf.Max(1f, a.Ceiling)), 0f, 100f);
            _state.SinceLastSmoke = 0f;
            _state.Smoked++;
            _craving = false;

            var left = pack is FoodDrink food ? Mathf.FloorToInt(food.FoodDrinkComponent.HpPercent) : 0;
            var milestone = Milestones.LastOrDefault(m => before < m && _state.Addiction >= m);
            if (milestone > 0)
            {
                Notify(milestone >= 75 ? "You can't go long without a smoke any more." : milestone >= 50 ? "You're hooked." : "Smoking is becoming a habit.", true);
            }
            else if (wasCraving)
            {
                Notify("That hits the spot.", false);
            }

            if (NicotinePlugin.ShowSmokeInfo.Value)
            {
                Notify($"{left} left in the pack · addiction {_state.Addiction:0}%", false);
            }

            _ = Backend.SaveAsync(_state);
            _nextSaveAt = now + SaveEverySeconds;
        }

        private void Tick(float dt)
        {
            if (_state.SinceLastSmoke >= 0f)
            {
                _state.SinceLastSmoke += dt;
                if (_state.SinceLastSmoke > CravingWindow())
                {
                    _state.Addiction = Mathf.Max(0f, _state.Addiction - Config.Addiction.RecoveryPerMinute * dt / 60f);
                }
            }

            var now = Time.time;
            var severity = Severity();
            var withdrawal = Config.Withdrawal;
            if (severity >= withdrawal.CravingNoticeAt)
            {
                if (!_craving)
                {
                    _craving = true;
                    Notify("You're craving a cigarette.", true);
                    _nextEpisodeAt = Mathf.Max(_nextEpisodeAt, now + 30f);
                }

                if (now >= _nextEpisodeAt)
                {
                    Episode(severity);
                    var interval = Mathf.Lerp(withdrawal.IntervalSecondsMild, withdrawal.IntervalSecondsSevere, severity);
                    _nextEpisodeAt = now + interval * (1f + withdrawal.Jitter * (float)(_random.NextDouble() * 2 - 1));
                }
            }
            else
            {
                _craving = false;
            }

            if (now >= _nextSaveAt)
            {
                _nextSaveAt = now + SaveEverySeconds;
                _ = Backend.SaveAsync(_state);
            }
        }

        private float CravingWindow()
        {
            var a = Config.Addiction;
            var heaviness = Mathf.InverseLerp(a.Threshold, 100f, _state.Addiction);
            return Mathf.Lerp(a.WindowMinutesLight, a.WindowMinutesHeavy, heaviness) * 60f;
        }

        public float Severity()
        {
            if (!_loaded || _state.SinceLastSmoke < 0f || _state.Addiction < Config.Addiction.Threshold)
            {
                return 0f;
            }

            var ramp = Mathf.Clamp01((_state.SinceLastSmoke - CravingWindow()) / Mathf.Max(1f, Config.Addiction.RampMinutes * 60f));
            return ramp * Mathf.Clamp01(_state.Addiction / 80f);
        }

        private void Episode(float severity)
        {
            var active = new HashSet<string>(_health.FindActiveEffects<ActiveHealthController.Stimulator>().Select(s => s.Name));
            var pool = Config.Withdrawal.Effects
                .Where(e => !string.IsNullOrEmpty(e.BuffSet) && severity >= e.MinSeverity && severity <= e.MaxSeverity && e.Weight > 0f && !active.Contains(e.BuffSet))
                .ToList();
            if (pool.Count == 0)
            {
                return;
            }

            var roll = (float)_random.NextDouble() * pool.Sum(e => e.Weight);
            var pick = pool[pool.Count - 1];
            foreach (var effect in pool)
            {
                roll -= effect.Weight;
                if (roll <= 0f)
                {
                    pick = effect;
                    break;
                }
            }

            _health.DoExternalBuff(pick.BuffSet);
            if (!string.IsNullOrEmpty(pick.Message))
            {
                Notify(pick.Message, true);
            }
        }

        private HashSet<string> WithdrawalSets() => new HashSet<string>(Config.Withdrawal.Effects.Select(e => e.BuffSet));

        private static void Notify(string message, bool important)
        {
            if (NicotinePlugin.ShowNotifications.Value)
            {
                NotificationManager.DisplayMessageNotification(message, important ? ENotificationDurationType.Long : ENotificationDurationType.Default,
                    important ? ENotificationIconType.Alert : ENotificationIconType.Default);
            }
        }

        private static string Minutes(float seconds) => seconds < 0f ? "never smoked" : $"{seconds / 60f:0.#} min";
    }
}
