using System;
using System.Collections;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.HealthSystem;
using UnityEngine;

namespace Nicotine.Client
{
    internal sealed class SmokeEvents : MonoBehaviour
    {
        public Transform Lighter;
        public AudioClip Sound;
        public ParticleSystem Exhale;
        public Transform Flame;
        public Vector3 FlameScale = Vector3.one;
        public Transform Cigarette;
        public Transform Head;
        public ProceduralWeaponAnimation View;

        private static readonly Quaternion CameraRest = Quaternion.Euler(-180f, 0f, 0f);
        private Quaternion _looked;
        private static bool _lookLogged;

        private const float ReleaseSeconds = 0.2f;
        private bool _lipsPending;
        private bool _inLips;
        private Vector3 _lipsPosition;
        private Quaternion _lipsRotation;
        private float _releaseAt = -1f;
        private static bool _lipsLogged;

        public Transform RightHand;
        public Transform LeftHand;
        public Transform RightMarker;
        public Transform LeftMarker;

        public Player Owner;
        public Player.MedsController Controller;
        public WeaponPrefab Prefab;
        private bool _wasInHands;

        private const int HandsLayer = 1;
        private const float DragLoopFrom = 0.86f;
        private const float DragExitAt = 0.9f;
        private const float DragSpeed = 1.2f;
        private const float DragBlend = 0.25f;
        private Animator _animator;

        private static bool _warned;
        private readonly List<(Transform Bone, Vector3 Position, Quaternion Rotation, Vector3 Scale)> _pack =
            new List<(Transform, Vector3, Quaternion, Vector3)>();

        public void NicotineLighterClick() => PlayLighter(Sound, Lighter != null ? Lighter.position : transform.position);

        public static void PlayLighter(AudioClip sound, Vector3 position)
        {
            if (sound == null)
            {
                return;
            }

            try
            {
                if (Singleton<BetterAudio>.Instantiated)
                {
                    Singleton<BetterAudio>.Instance.PlayAtPoint(position, sound, BetterAudio.AudioSourceGroupType.Character, 15, 0.8f);
                    return;
                }
            }
            catch (Exception ex)
            {
                if (!_warned)
                {
                    _warned = true;
                    NicotinePlugin.Log.LogWarning($"Nicotine: game audio refused the lighter sound, playing it directly: {ex.Message}");
                }
            }

            AudioSource.PlayClipAtPoint(sound, position, 0.8f);
        }

        public void NicotineExhale()
        {
            if (Exhale != null)
            {
                Exhale.Emit(UnityEngine.Random.Range(18, 28));
            }
        }

        public void NicotineToLips() => _lipsPending = Cigarette != null && Head != null;

        public void NicotineFromLips()
        {
            _lipsPending = false;
            if (!_inLips || Cigarette == null)
            {
                return;
            }

            _inLips = false;
            _releaseAt = Time.time;
        }

        public void BeginSmoke()
        {
            _lipsPending = false;
            _inLips = false;
            _releaseAt = -1f;
            _wasInHands = false;
            _animator = GetComponent<Animator>();
            enabled = true;
        }

        private void KeepSmoking()
        {
            if (_animator == null || Cigarette == null)
            {
                return;
            }

            var current = _animator.GetCurrentAnimatorClipInfo(HandsLayer);
            if (current.Length == 0 || current[0].clip == null || !current[0].clip.name.StartsWith("nicotine_smoke"))
            {
                return;
            }

            var state = _animator.GetCurrentAnimatorStateInfo(HandsLayer);
            if (_animator.IsInTransition(HandsLayer))
            {
                var next = _animator.GetNextAnimatorClipInfo(HandsLayer);
                if (next.Length == 0 || next[0].clip == null || next[0].clip.name != "nicotine_flick")
                {
                    return;
                }
            }
            else if (state.normalizedTime < DragLoopFrom)
            {
                return;
            }

            var speed = Mathf.Abs(state.speed * state.speedMultiplier * _animator.speed);
            var drag = current[0].clip.length / (speed > 0.01f ? speed : DragSpeed);
            if (SecondsLeft() < drag * DragExitAt + DragBlend)
            {
                return;
            }

            _animator.CrossFadeInFixedTime(state.fullPathHash, DragBlend, HandsLayer, 0f);
        }

        private float SecondsLeft()
        {
            var health = Owner != null ? Owner.ActiveHealthController : null;
            var item = Controller != null ? Controller.Item : null;
            if (health == null || item == null)
            {
                return 0f;
            }

            foreach (var effect in health.Effects)
            {
                if (effect is ActiveHealthController.MedEffect med && ReferenceEquals(med.MedItem, item))
                {
                    switch (med.State)
                    {
                        case EEffectState.Added:
                            return med.CurStateTimeLeft + med.WorkStateTime;
                        case EEffectState.Started:
                            return med.CurStateTimeLeft;
                    }
                }
            }

            return 0f;
        }

        private void OnEnable() => Application.onBeforeRender += FollowHead;

        private void OnDisable() => Application.onBeforeRender -= FollowHead;

        private bool Smoking()
        {
            if (Prefab != null && Prefab.IsInPool)
            {
                return false;
            }

            if (Owner == null || Controller == null)
            {
                return !_wasInHands;
            }

            if (ReferenceEquals(Owner.HandsController, Controller))
            {
                _wasInHands = true;
                return true;
            }

            return !_wasInHands;
        }

        private void EndSmoke()
        {
            foreach (var part in new Component[] { Cigarette, Lighter, Exhale })
            {
                if (part != null)
                {
                    part.gameObject.SetActive(false);
                }
            }

            _lipsPending = false;
            _inLips = false;
            _releaseAt = -1f;
            _pendingMeasure = null;
            StopAllCoroutines();
            enabled = false;
        }

        private void FollowHead()
        {
            if (!Smoking())
            {
                EndSmoke();
                return;
            }

            if (Cigarette != null)
            {
                LookWithClip();
            }

            if (Lighter != null && InRealHand(LeftHand, LeftMarker, Lighter.parent, out var lighterPosition, out var lighterRotation))
            {
                Lighter.SetPositionAndRotation(lighterPosition, lighterRotation);
            }

            if (Cigarette == null)
            {
                return;
            }

            var inHand = InRealHand(RightHand, RightMarker, Cigarette.parent, out var position, out var rotation);
            if (!inHand)
            {
                position = Cigarette.parent.position;
                rotation = Cigarette.parent.rotation;
            }

            if (_lipsPending && Head != null)
            {
                _lipsPending = false;
                _inLips = true;
                _lipsPosition = Head.InverseTransformPoint(position);
                _lipsRotation = Quaternion.Inverse(Head.rotation) * rotation;
                if (!_lipsLogged)
                {
                    _lipsLogged = true;
                    NicotinePlugin.Log.LogInfo($"Nicotine: cigarette in the lips at {_lipsPosition * 100f:F1}cm from the camera");
                }
            }

            if (Head != null && (_inLips || _releaseAt >= 0f))
            {
                var lipsPosition = Head.TransformPoint(_lipsPosition);
                var lipsRotation = Head.rotation * _lipsRotation;
                if (_inLips)
                {
                    position = lipsPosition;
                    rotation = lipsRotation;
                }
                else
                {
                    var u = Mathf.Clamp01((Time.time - _releaseAt) / ReleaseSeconds);
                    var s = u * u * (3f - 2f * u);
                    position = Vector3.Lerp(lipsPosition, position, s);
                    rotation = Quaternion.Slerp(lipsRotation, rotation, s);
                    if (u >= 1f)
                    {
                        _releaseAt = -1f;
                    }
                }
            }

            Cigarette.SetPositionAndRotation(position, rotation);
        }

        private void LookWithClip()
        {
            var container = View != null ? View.HandsContainer : null;
            if (!NicotinePlugin.LookDown.Value || Head == null || container == null || container.CameraAnimatedFP == null ||
                container.CameraAnimatedTP == null)
            {
                return;
            }

            var current = Head.localRotation;
            if (current == _looked)
            {
                return;
            }

            var share = Mathf.Clamp01(View.FarAimPlane);
            var clip = container.CameraAnimatedFP.localRotation * container.CameraAnimatedTP.localRotation;
            var applied = Quaternion.Lerp(CameraRest, clip, share);
            _looked = clip * Quaternion.Inverse(applied) * current;
            Head.localRotation = _looked;
            if (!_lookLogged && Quaternion.Angle(clip, applied) > 5f)
            {
                _lookLogged = true;
                NicotinePlugin.Log.LogInfo($"Nicotine: added {Quaternion.Angle(clip, applied):0}deg of the clip's camera turn that the game " +
                                           $"left out (Head Bobbing {share:0.00})");
            }
        }

        private static bool InRealHand(Transform hand, Transform marker, Transform carrier, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (hand == null || marker == null || carrier == null || marker.parent == null)
            {
                return false;
            }

            var (markerPosition, markerRotation) = InFrameOf(marker, marker.parent);
            var (carrierPosition, carrierRotation) = InFrameOf(carrier, marker.parent);
            var inverse = Quaternion.Inverse(markerRotation);
            position = hand.TransformPoint(inverse * (carrierPosition - markerPosition));
            rotation = hand.rotation * (inverse * carrierRotation);
            return true;
        }

        private static (Vector3 position, Quaternion rotation) InFrameOf(Transform t, Transform ancestor)
        {
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            for (var x = t; x != null && x != ancestor; x = x.parent)
            {
                position = x.localRotation * position + x.localPosition;
                rotation = x.localRotation * rotation;
            }

            return (position, rotation);
        }

        private void LateUpdate()
        {
            if (!Smoking())
            {
                EndSmoke();
                return;
            }

            if (_pendingMeasure != null)
            {
                StopAllCoroutines();
                StartCoroutine(_pendingMeasure);
                _pendingMeasure = null;
            }

            KeepSmoking();

            if (Flame == null || !Flame.gameObject.activeInHierarchy)
            {
                return;
            }

            var t = Time.time;
            Flame.localScale = Vector3.Scale(FlameScale, new Vector3(1f + 0.06f * Mathf.Sin(t * 29f + 2f),
                1f + 0.12f * Mathf.Sin(t * 31f) + 0.07f * Mathf.Sin(t * 47f + 1f), 1f + 0.06f * Mathf.Sin(t * 23f)));
        }

        public void RememberPack(IEnumerable<Transform> bones)
        {
            if (_pack.Count > 0)
            {
                return;
            }

            foreach (var bone in bones)
            {
                if (bone != null)
                {
                    _pack.Add((bone, bone.localPosition, bone.localRotation, bone.localScale));
                }
            }
        }

        public void RestorePack()
        {
            foreach (var (bone, position, rotation, scale) in _pack)
            {
                if (bone != null)
                {
                    bone.localPosition = position;
                    bone.localRotation = rotation;
                    bone.localScale = scale;
                }
            }
        }

        public void Measure(Player player, Transform cigarette, Transform lighter, Transform rightMarker, Transform leftMarker)
        {
            _pendingMeasure = player != null && player.PlayerBones != null ? MeasureLater(player, cigarette, lighter, rightMarker, leftMarker) : null;
        }

        private IEnumerator _pendingMeasure;

        private IEnumerator MeasureLater(Player player, Transform cigarette, Transform lighter, Transform rightMarker, Transform leftMarker)
        {
            var started = Time.time;
            foreach (var at in new[] { 2.2f, 6.5f })
            {
                while (Time.time - started < at)
                {
                    yield return null;
                }

                yield return new WaitForEndOfFrame();
                var right = player.PlayerBones.RightPalm;
                var left = player.PlayerBones.LeftPalm;
                string Cm(Transform a, Transform b) => a != null && b != null ? $"{Vector3.Distance(a.position, b.position) * 100f:0.0}cm" : "n/a";
                NicotinePlugin.Log.LogInfo($"Nicotine: {at:0.0}s into the smoke: cigarette {(cigarette != null && cigarette.gameObject.activeInHierarchy ? "shown" : "hidden")} " +
                                           $"{Cm(cigarette, right)} from the right hand, lighter {(lighter != null && lighter.gameObject.activeInHierarchy ? "shown" : "hidden")} " +
                                           $"{Cm(lighter, left)} from the left hand; IK markers off the hands: right {Cm(rightMarker, right)}, left {Cm(leftMarker, left)}");

                var view = Camera.main;
                if (view != null)
                {
                    string OnScreen(Transform t)
                    {
                        if (t == null)
                        {
                            return "n/a";
                        }

                        var p = view.WorldToViewportPoint(t.position);
                        return $"({p.x:0.00}, {p.y:0.00}, {p.z:0.00})";
                    }

                    NicotinePlugin.Log.LogInfo($"Nicotine:   on screen: cigarette {OnScreen(cigarette)}, lighter {OnScreen(lighter)}, right hand {OnScreen(right)}, " +
                                               $"left hand {OnScreen(left)}; FOV {view.fieldOfView:0}, camera {(Head != null ? Head.InverseTransformPoint(view.transform.position) * 100f : Vector3.zero):F1}cm from Cam");
                }

                var animator = GetComponent<Animator>();
                if (animator != null && lighter != null && leftMarker != null && cigarette != null && rightMarker != null)
                {
                    var layers = new List<string>();
                    for (var i = 0; i < animator.layerCount; i++)
                    {
                        layers.Add($"{animator.GetLayerName(i)} {animator.GetLayerWeight(i):0.00}");
                    }

                    NicotinePlugin.Log.LogInfo($"Nicotine:   real-hand placement moved the cigarette {Cm(cigarette, cigarette.parent)} and the lighter " +
                                               $"{Cm(lighter, lighter.parent)}; hands turned off their markers: right " +
                                               $"{(right != null ? Quaternion.Angle(right.rotation, rightMarker.rotation) : 0f):0.0}deg, left " +
                                               $"{(left != null ? Quaternion.Angle(left.rotation, leftMarker.rotation) : 0f):0.0}deg; view squash " +
                                               $"{(animator.transform.lossyScale.z / Mathf.Max(0.0001f, animator.transform.lossyScale.x)):0.00}");
                    var clips = animator.GetCurrentAnimatorClipInfo(1);
                    NicotinePlugin.Log.LogInfo($"Nicotine:   layers {string.Join(", ", layers)}; hands layer playing " +
                                               $"{(clips.Length > 0 ? clips[0].clip.name : "nothing")} at {animator.GetCurrentAnimatorStateInfo(1).normalizedTime:0.00}" +
                                               $"{(animator.IsInTransition(1) ? " (blending)" : "")}; lighter in the left marker's frame " +
                                               $"{leftMarker.InverseTransformPoint(lighter.position) * 100f:F1}cm (clip: (-11.4, -2.9, 0.3)), cigarette in the right " +
                                               $"marker's frame {rightMarker.InverseTransformPoint(cigarette.position) * 100f:F1}cm");
                }
            }
        }
    }
}
