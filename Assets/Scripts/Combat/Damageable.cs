using LastTruck;
using System.Collections;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 피격 가능한 대상(허수아비, 몬스터 등). 데미지를 받으면 잠깐 빨갛게 변하고 데미지 숫자를 띄운다.
    ///
    /// 멀티플레이: 같은 오브젝트에 NetworkHealth가 있으면 체력은 호스트가 관리한다.
    ///  - TakeDamage는 호스트에게 피해를 "요청"만 하고,
    ///  - 빨간색 깜빡임/데미지 숫자는 호스트가 적용한 뒤 모든 컴퓨터에서 똑같이 나온다 (OnNetworkHit).
    ///  - 체력 0이면 호스트가 오브젝트를 없앤다 (Destroy 대신 네트워크 Despawn).
    /// </summary>
    public class Damageable : MonoBehaviour, LastTruck.Networking.INetworkHealthListener
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float hitFlashDuration = 0.15f;

        private int _currentHealth;
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private Coroutine _flashRoutine;
        private Rigidbody _rigidbody;
        private Coroutine _rootRoutine;
        private float _rootUntilTime;

        // ★ 경찰 표식 타이머
        private float _markUntilTime;
        public bool IsMarked => Time.time < _markUntilTime;

        private LastTruck.Networking.NetworkHealth _networkHealth;

        private bool _dead;

        /// <summary>싱글플레이: 피해를 받은 순간 (피해량). 몬스터 경직 등.</summary>
        public event System.Action<int> Damaged;

        /// <summary>싱글플레이: 체력이 0이 된 순간. 구독자가 있으면 Destroy 대신 구독자가 처리한다 (사망 애니메이션 후 제거).</summary>
        public event System.Action Died;

        /// <summary>싱글플레이: 기절(복서 능력 등)이 걸린 순간 (지속 시간). MonsterAI가 이동/공격을 멈춘다.</summary>
        public event System.Action<float> Stunned;

        /// <summary>싱글플레이에서 이미 죽었는가 (사망 애니메이션 중).</summary>
        public bool IsDeadOffline => _dead;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;

        /// <summary>덫 등으로 이동이 묶인 상태인지. 이동/AI 스크립트는 이 값을 보고 움직임을 멈춰야 한다.</summary>
        public bool IsRooted => Time.time < _rootUntilTime;

        /// <summary>멀티플레이에서 체력을 호스트가 관리하는 오브젝트인가.</summary>
        private bool IsNetworked => _networkHealth != null && _networkHealth.Object != null && _networkHealth.Object.IsValid;

        #region 생명주기

        private void Awake()
        {
            _currentHealth = maxHealth;
            RefreshRenderers();
            _rigidbody = GetComponent<Rigidbody>();
            _networkHealth = GetComponent<LastTruck.Networking.NetworkHealth>();
        }

        /// <summary>빨갛게 깜빡일 렌더러 목록을 다시 모은다 (외형을 바꾼 뒤에 부른다).</summary>
        public void RefreshRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalColors[i] = GetColor(_renderers[i]);
            }
        }

        #endregion

        #region 이동 묶기 / 경찰 표식

        /// <summary>최대 체력을 바꾸고 체력을 가득 채운다(허수아비처럼 쉽게 죽지 않는 대상을 만들 때).</summary>
        public void SetMaxHealth(int value)
        {
            maxHealth = Mathf.Max(1, value);
            _currentHealth = maxHealth;
        }

        /// <summary>지정한 시간(초) 동안 이동을 묶는다. 겹쳐 걸리면 더 긴 쪽으로 갱신된다.</summary>
        public void Root(float duration)
        {
            if (duration <= 0f)
                return;

            _rootUntilTime = Mathf.Max(_rootUntilTime, Time.time + duration);
            if (_rootRoutine == null)
                _rootRoutine = StartCoroutine(RootRoutine());
        }

        private IEnumerator RootRoutine()
        {
            while (Time.time < _rootUntilTime)
            {
                if (_rigidbody != null && !_rigidbody.isKinematic)
                    _rigidbody.linearVelocity = Vector3.zero;
                yield return null;
            }

            _rootRoutine = null;
        }

        //public void TakeDamage(int amount, Vector3 hitPoint)
        //{
        //    if (amount <= 0)
        //        return;

        //    _currentHealth -= amount;
        //    SpawnDamagePopup(hitPoint, amount);

        //    if (_flashRoutine != null)
        //        StopCoroutine(_flashRoutine);
        //    _flashRoutine = StartCoroutine(FlashRed());

        //    if (_currentHealth <= 0)
        //    {
        //        // Debug.Log($"[Damageable] {name} 체력 소진 - 초기화");
        //        // _currentHealth = maxHealth;
        //        Destroy(gameObject);
        //    }
        //}

        /// <summary>
        /// 지정한 시간(초) 동안 기절시킨다 (이동 + 공격 모두 멈춤). 복서 능력에서 쓴다.
        /// 멀티플레이: 몬스터 AI는 호스트에서 돌기 때문에 호스트에게 요청한다 (NetworkMonster.RequestStun).
        /// </summary>
        public void ApplyStun(float duration)
        {
            if (duration <= 0f)
                return;

            // 기절한 동안 몬스터 외곽선이 붉게 깜빡인다 (기절을 건 사람 화면)
            if (!_dead) StunRimVisual.Show(this, duration);

            if (IsNetworked)
            {
                var networkMonster = GetComponent<LastTruck.Networking.NetworkMonster>();
                if (networkMonster != null) networkMonster.RequestStun(duration);
                BroadcastAbilityFx(AbilityFx.Stun, duration, transform.position);   // 다른 플레이어 화면에도 기절 연출
                return;
            }

            if (_dead) return;
            Root(duration);   // MonsterAI가 없는 대상도 최소한 움직이지는 못하게
            Stunned?.Invoke(duration);
        }

        /// <summary>캐릭터 능력 연출 종류 (멀티플레이에서 다른 플레이어 화면에 보여 줄 때 쓴다).</summary>
        public enum AbilityFx : byte
        {
            PoliceMark = 1,       // 경찰 표식 림라이트
            PoliceMarkFlash = 2,  // 표식 + 추가 피해 번쩍
            Stun = 3,             // 복서 기절: 붉은 외곽선 + "기절!" 글자
            ArmyEnhanced = 4,     // 군인 강화 공격: "강화 공격!" 글자
        }

        /// <summary>멀티플레이: 내 화면에 띄운 능력 연출을 다른 플레이어들에게도 보낸다 (싱글이면 아무것도 안 함).</summary>
        private void BroadcastAbilityFx(AbilityFx fx, float duration, Vector3 point)
        {
            if (!IsNetworked) return;
            var networkMonster = GetComponent<LastTruck.Networking.NetworkMonster>();
            if (networkMonster != null) networkMonster.BroadcastAbilityFx((byte)fx, duration, point);
        }

        /// <summary>다른 플레이어가 보낸 능력 연출을 이 화면에 보여 준다 (연출만, 피해/표식 판정은 바꾸지 않는다).</summary>
        public void ShowRemoteAbilityFx(byte fx, float duration, Vector3 point)
        {
            if (_dead) return;
            duration = Mathf.Clamp(duration, 0f, 30f);
            switch ((AbilityFx)fx)
            {
                case AbilityFx.PoliceMark:
                    if (duration > 0f) PoliceMarkVisual.Show(this, Time.time + duration);
                    break;
                case AbilityFx.PoliceMarkFlash:
                    if (duration > 0f) PoliceMarkVisual.Show(this, Time.time + duration);
                    PoliceMarkVisual.Flash(this);
                    break;
                case AbilityFx.Stun:
                    if (duration > 0f) StunRimVisual.Show(this, duration);
                    LastTruck.AbilityFeedback.Popup(transform.position + Vector3.up * 2f, "기절!", LastTruck.AbilityFeedback.BoxerColor, 0.75f);
                    break;
                case AbilityFx.ArmyEnhanced:
                    LastTruck.AbilityFeedback.Popup(point, "강화 공격!", LastTruck.AbilityFeedback.ArmyColor);
                    break;
            }
        }

        /// <summary>
        /// 경찰 표식을 부여
        /// </summary>
        public void ApplyPoliceMark(float duration)
        {
            if (duration <= 0f) return;
            _markUntilTime = Mathf.Max(_markUntilTime, Time.time + duration);
        }

        #endregion

        #region 피해

        public void TakeDamage(int amount, Vector3 hitPoint)
        {
            // 캐릭터 고유 능력(군인 추가 피해, 경찰 표식)은 "때린 사람" 기준이다.
            // 멀티플레이에서는 내 캐릭터의 능력을 쓴다 (아무 플레이어나 찾으면 다른 사람 능력이 적용될 수 있음).
            // 장비 강화(무기 강화 키트)도 플레이어가 직접 준 피해에만 붙는다.
            TakeDamage(CraftingSystem.PlayerGear.ScaleDamage(amount), hitPoint, LastTruck.Networking.LocalPlayerLookup.AbilityController);
        }

        /// <summary>
        /// 때린 사람의 능력을 직접 넘기는 버전 (덫처럼 "설치한 사람"이 따로 있는 경우).
        /// attackerAbility가 null이면 능력 보너스 없이 기본 피해만 준다.
        /// </summary>
        public void TakeDamage(int amount, Vector3 hitPoint, LastTruck.CharacterAbilityController attackerAbility)
        {
            if (amount <= 0)
                return;

            int finalDamage = amount;
            var abilityController = attackerAbility;

            if (abilityController != null)
            {
                // 1. 기본 데미지(군인 등)
                finalDamage = abilityController.GetCalculatedDamage(amount, out bool isEnhanced);
                if (isEnhanced)
                {
                    LastTruck.AbilityFeedback.Popup(hitPoint, "강화 공격!", LastTruck.AbilityFeedback.ArmyColor);
                    BroadcastAbilityFx(AbilityFx.ArmyEnhanced, 0f, hitPoint);
                }

                bool wasMarked = IsMarked;

                // 2. 경찰 표식 추가 피해 계산 및 표식 갱신
                finalDamage = abilityController.GetCalculatedPoliceDamage(finalDamage, IsMarked, out float markDuration);

                // 타격 시 표식 부여 (경찰 캐릭터일 경우 markDuration > 0)
                if (markDuration > 0f)
                {
                    ApplyPoliceMark(markDuration);

                    // 발동 기록(콘솔/디버그 창)은 CharacterAbilityController가 남긴다.
                    // 화면에는 글자 대신 몬스터 몸에 노란 림라이트 + 발밑 링으로 표시 (PoliceMarkVisual).
                    PoliceMarkVisual.Show(this, _markUntilTime);
                    if (wasMarked)
                        PoliceMarkVisual.Flash(this);   // 표식 추가 피해가 들어간 순간 번쩍

                    // 다른 플레이어 화면에도 표식 림라이트 (추가 피해 순간이면 번쩍까지)
                    BroadcastAbilityFx(wasMarked ? AbilityFx.PoliceMarkFlash : AbilityFx.PoliceMark, _markUntilTime - Time.time, hitPoint);
                }
            }

            // 멀티플레이: 호스트에게 요청만 한다. 연출은 호스트가 적용한 뒤 OnNetworkHit으로 모두에게 나온다.
            if (IsNetworked)
            {
                _networkHealth.RequestDamage(finalDamage, hitPoint, true);
                return;
            }

            if (_dead) return;

            _currentHealth -= finalDamage;
            PlayHitEffect(hitPoint, finalDamage);
            Damaged?.Invoke(finalDamage);

            if (_currentHealth <= 0)
            {
                PoliceMarkVisual.Hide(this);
                StunRimVisual.Hide(this);
                CraftingSystem.MonsterLoot.Drop(gameObject, maxHealth);
                PlayKillFeel();
                // 사망 연출을 맡은 스크립트(MonsterAI 등)가 있으면 그쪽이 애니메이션 후 없앤다.
                if (Died != null)
                {
                    _dead = true;
                    Died.Invoke();
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        #endregion

        #region 피격 연출 (빨간색 깜빡임 + 데미지 숫자)

        private void PlayHitEffect(Vector3 hitPoint, int amount)
        {
            // 데미지 숫자는 띄우지 않는다 (빨간 깜빡임 + 피격 동작 + 밀려남으로 맞은 걸 보여 준다)
            // 몸이 납작해졌다 튕기는 연출은 애니메이션이 없는 것(자원/구조물)에만 (캐릭터는 피격 동작이 있어 어색해진다)
            if (GetComponentInChildren<Animator>() == null)
                HitFeel.Punch(gameObject, Mathf.Clamp01(amount / 40f));
            // 타격음: 플레이어가 맞으면 묵직하게, 몬스터는 세게 맞을수록 묵직한 소리
            bool isPlayer = GetComponent<LastTruck.Networking.NetworkPlayer>() != null;
            LastTruck.Audio.Sfx.PlayAt(isPlayer || amount >= 30 ? "hit_heavy" : "hit_flesh", hitPoint,
                                       Mathf.Clamp(0.7f + amount / 60f, 0.7f, 1f), isPlayer ? 0.9f : 1f, 0.03f);

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                RestoreColors();   // 이전 깜빡임이 빨간 채로 남지 않게
            }
            if (isActiveAndEnabled)
                _flashRoutine = StartCoroutine(FlashRed());
        }

        private void RestoreColors()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) SetColor(_renderers[i], _originalColors[i]);
        }

        private IEnumerator FlashRed()
        {
            // 맞는 순간 빨갛게 → 원래 색
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) SetColor(_renderers[i], Color.red);

            yield return new WaitForSeconds(hitFlashDuration);

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    SetColor(_renderers[i], _originalColors[i]);
            }

            _flashRoutine = null;
        }

        private static Color GetColor(Renderer renderer)
        {
            Material mat = renderer.material;
            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color"))
                return mat.GetColor("_Color");
            return Color.white;
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            Material mat = renderer.material;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
        }

        private void SpawnDamagePopup(Vector3 worldPos, int amount)
        {
            // 팝업의 애니메이션/파괴는 DamagePopup이 스스로 처리한다.
            // (여기서 코루틴을 돌리면 이 오브젝트가 파괴될 때 코루틴도 멈춰 숫자가 남는다)
            DamagePopup.Spawn(worldPos, amount);
        }

        #endregion

        #region 멀티플레이 (NetworkHealth가 호출)

        float LastTruck.Networking.INetworkHealthListener.NetworkMaxHealth => maxHealth;

        void LastTruck.Networking.INetworkHealthListener.OnNetworkHealthChanged(float current, float max)
        {
            _currentHealth = Mathf.CeilToInt(current);
        }

        void LastTruck.Networking.INetworkHealthListener.OnNetworkHit(float amount, Vector3 hitPoint)
        {
            PlayHitEffect(hitPoint, Mathf.RoundToInt(amount));
        }

        void LastTruck.Networking.INetworkHealthListener.OnNetworkDeath()
        {
            // 호스트가 곧 오브젝트를 없앤다 (NetworkHealth). 몬스터면 내 화면에 전리품(고기/가죽)을 떨어뜨린다.
            PoliceMarkVisual.Hide(this);
            StunRimVisual.Hide(this);
            CraftingSystem.MonsterLoot.Drop(gameObject, maxHealth);
            PlayKillFeel();
        }

        /// <summary>쓰러질 때: 덩어리가 터지고, 내 캐릭터가 가까우면 화면이 한 번 크게 흔들린다.</summary>
        private void PlayKillFeel()
        {
            if (GetComponent<LastTruck.Networking.NetworkPlayer>() != null) return;   // 플레이어 기절에는 쓰지 않는다
            float size = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            HitFeel.KillBurst(transform.position, size);
            LastTruck.Audio.Sfx.PlayAt("kill", transform.position, 1f, size > 1.5f ? 0.75f : 1f, 0.05f);   // 큰 놈은 더 낮고 무겁게
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude < 30f * 30f)
                CameraShake.Shake(size > 1.5f ? 0.45f : 0.18f);
        }

        #endregion
    }
}
