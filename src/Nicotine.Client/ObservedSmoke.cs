using EFT;
using UnityEngine;

namespace Nicotine.Client
{
    internal sealed class ObservedSmoke : MonoBehaviour
    {
        private const int HandsLayer = 1;
        private const float LightAt = 0.35f;
        private static readonly int Drag = Animator.StringToHash("Hands.USE");
        private static readonly int LastDrag = Animator.StringToHash("Hands.USE 1");
        private const float DragExhaleAt = 0.88f;
        private const float LastDragExhaleAt = 0.98f;

        public Transform Cigarette;
        public GameObject Ember;
        public ParticleSystem Exhale;
        public AudioClip Sound;
        public Player Owner;
        public Player.MedsController Controller;
        public WeaponPrefab Prefab;

        private Animator _animator;
        private float _startedAt;
        private bool _lit;
        private bool _wasInHands;
        private int _state;
        private bool _exhaled;

        public void BeginSmoke()
        {
            _animator = GetComponent<Animator>();
            _startedAt = -1f;
            _lit = false;
            _wasInHands = false;
            _state = 0;
            _exhaled = false;
            if (Ember != null)
            {
                Ember.SetActive(false);
            }

            enabled = true;
        }

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

        public void EndSmoke()
        {
            foreach (var part in new Component[] { Cigarette, Exhale })
            {
                if (part != null)
                {
                    part.gameObject.SetActive(false);
                }
            }

            enabled = false;
        }

        private void LateUpdate()
        {
            if (!Smoking())
            {
                EndSmoke();
                return;
            }

            if (!_wasInHands && Owner != null && Controller != null)
            {
                return;
            }

            if (_startedAt < 0f)
            {
                _startedAt = Time.time;
            }

            if (!_lit && Time.time - _startedAt >= LightAt)
            {
                _lit = true;
                if (Ember != null)
                {
                    Ember.SetActive(true);
                }

                SmokeEvents.PlayLighter(Sound, Cigarette != null ? Cigarette.position : transform.position);
            }

            FollowMouth();
            if (_animator == null)
            {
                return;
            }

            var info = _animator.GetCurrentAnimatorStateInfo(HandsLayer);
            if (info.fullPathHash != _state)
            {
                _state = info.fullPathHash;
                _exhaled = false;
            }

            var at = _state == Drag ? DragExhaleAt : _state == LastDrag ? LastDragExhaleAt : -1f;
            if (_lit && !_exhaled && at > 0f && info.normalizedTime >= at)
            {
                _exhaled = true;
                if (Exhale != null)
                {
                    Exhale.gameObject.SetActive(true);
                    Exhale.Emit(Random.Range(18, 28));
                }
            }
        }

        private void FollowMouth()
        {
            var head = Owner != null && Owner.PlayerBones != null && Owner.PlayerBones.Head != null ? Owner.PlayerBones.Head.Original : null;
            if (Exhale == null || head == null)
            {
                return;
            }

            var look = Owner.MovementContext != null ? Owner.LookDirection : head.forward;
            if (look.sqrMagnitude < 0.01f)
            {
                look = head.forward;
            }

            look.Normalize();
            Exhale.transform.SetPositionAndRotation(head.position + look * 0.12f - Vector3.up * 0.03f,
                Quaternion.LookRotation(look - Vector3.up * 0.25f, Vector3.up));
        }
    }
}
