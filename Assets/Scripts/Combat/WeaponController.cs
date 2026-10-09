using CraftingSystem;
using LastTruck;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 인벤토리에서 무기를 선택한 상태로 좌클릭하면 무기 종류에 맞는 방식으로 공격한다.
    /// - Hitscan(AK-47 등): 클릭 시 마우스 방향으로 즉시 한 발.
    ///   백금 저격소총은 두 손으로 총을 든 모습을 보여 주고, 클릭 시 총구에서 실제 총알이 날아간다.
    /// - MeleeArc(마체테): 클릭 시 주변(전방 부채꼴)에서 가장 가까운 적 최대 1명에게 근접 피해.
    /// - ContinuousCone(화염방사기): 누르고 있는 동안 전방 부채꼴 범위에 주기적으로 피해 + 불꽃 이펙트.
    /// - ChargeAndRelease(사냥용 활): 누르고 있으면 차징, 일정 시간 이상 차징한 뒤 떼면 화살 발사.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class WeaponController : MonoBehaviour
    {
        private enum WeaponBehavior
        {
            Hitscan,
            MeleeArc,
            ContinuousCone,
            ChargeAndRelease,
            Throw,
        }

        private struct WeaponStats
        {
            public WeaponBehavior behavior;
            public int damage;
            public float range;
            public float cooldown;
            public float coneAngle;
            public float chargeTime;
            public float minChargeFraction;
            public Color effectColor;
            public float blastRadius;
            public float gasDuration;
            public float gasTickInterval;
            public int gasTickDamage;
            public float throwSpeed;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("활")]
        [Tooltip("손에 드는 활 모델. 피벗=손잡이, 로컬 +Y=활 위쪽, 로컬 +Z=화살이 나가는 방향")]
        [SerializeField] private GameObject bowPrefab;
        [Tooltip("발사할 화살. 피벗=꼬리(오늬), 로컬 +Z=화살촉")]
        [SerializeField] private ArrowProjectile arrowPrefab;
        [SerializeField] private float minArrowSpeed = 15f;
        [SerializeField] private float maxArrowSpeed = 35f;
        [Tooltip("최대 차징 시 화살을 뒤로 당기는 거리(m)")]
        [SerializeField] private float maxDrawDistance = 0.25f;
        [Tooltip("평소 들고 다닐 때 활을 앞으로 눕히는 각도. 0=세로, 90=앞으로 수평")]
        [SerializeField] private float carryTiltAngle = 50f;
        [Tooltip("조준 시 왼팔을 뻗는 정도(팔 길이 대비). 1이면 팔을 끝까지 편다")]
        [Range(0.5f, 1f)]
        [SerializeField] private float aimArmExtension = 0.8f;
        [Tooltip("조준 시 상체를 돌리는 각도. 왼쪽 어깨가 과녁을 향하는 궁수 자세(머리는 정면 유지)")]
        [SerializeField] private float aimTorsoTwist = 60f;
        [Tooltip("평소 자세 ↔ 조준 자세 전환 시간(초)")]
        [SerializeField] private float aimBlendTime = 0.15f;

        [Header("저격총")]
        [Tooltip("두 손으로 드는 저격총 모델. 피벗=오른손 손잡이, 로컬 +Z=총구 방향. 자식 LeftGrip(왼손 위치), Muzzle(총구)이 필요")]
        [SerializeField] private GameObject rifleHeldPrefab;
        [Tooltip("발사할 총알(중력 0, stickOnHit 끔)")]
        [SerializeField] private ArrowProjectile bulletPrefab;
        [SerializeField] private float bulletSpeed = 90f;
        [Tooltip("총을 들 때 상체를 돌리는 각도. 짧은 왼팔이 총열 아래 손잡이에 닿게 한다(머리는 정면 유지)")]
        [SerializeField] private float rifleTorsoTwist = 50f;
        [Tooltip("오른손(손잡이) 위치: 돌린 오른쪽 어깨 기준 오프셋(캐릭터 로컬, x=오른쪽 y=위 z=앞)")]
        [SerializeField] private Vector3 rifleGripOffset = new Vector3(-0.08f, 0f, 0.14f);

        private PlayerInventory _inventory;
        private Animator _animator;
        private GameObject _heldBow;
        private GameObject _nockedArrow;
        private Transform _bowHand;
        private Transform _rightHand;
        private Vector3 _desiredNockWorld;
        private Vector3 _leftIkCorrection;
        private Vector3 _rightIkCorrection;
        private Transform _chest;
        private Transform _head;
        private Vector3 _chestLocal;
        private Vector3 _leftShoulderLocal;
        private float _leftArmLength;
        private float _aimWeight;
        private WeaponStats _bowStats;

        private Transform _leftHandBone;
        private Vector3 _rightShoulderLocal;

        private GameObject _heldRifle;
        private Transform _rifleLeftGrip;
        private Transform _rifleMuzzle;
        private float _rifleWeight;
        private float _rifleRecoil;
        private Vector3 _rifleLeftIkCorrection;
        private Vector3 _rifleRightIkCorrection;

        // 활 로컬 기준 화살 꼬리를 거는 위치: 손잡이 뒤쪽 시위 위.
        private static readonly Vector3 NockRestLocalPosition = new Vector3(0f, 0.015f, -0.24f);

        // 조준 시 활 손잡이를 몸 가운데 쪽으로 당기는 거리(m). 짧은 오른팔이 시위에 닿게 한다.
        private const float AimGripInward = 0.05f;
        private float _nextFireTime;

        private bool _pressVetoed;
        private bool _isCharging;
        private float _chargeStartTime;
        private bool _isFlameActive;
        private Coroutine _flameEffectRoutine;
        private string _activeWeaponId;

        [Header("무기 이펙트")]
        [Tooltip("화염방사기를 쏘는 동안 총구 앞에 붙는 불꽃 이펙트(vfx_Flamethrower_01). 비워두면 기존 구체 파티클을 쓴다")]
        [SerializeField] private GameObject flamethrowerVfxPrefab;
        [Tooltip("불꽃이 닿는 거리 대비 이펙트 배율. 프리팹 원본은 약 4.1m까지 뻗는다")]
        [SerializeField] private float flamethrowerVfxLength = 4.1f;
        [Tooltip("근접 무기(마체테/창 등)가 적중했을 때 한 번 터지는 단순 타격 이펙트(vfx_Impact_01). 비워두면 기존 흰 구체를 쓴다")]
        [SerializeField] private GameObject meleeHitVfxPrefab;
        [Tooltip("타격 이펙트 크기 배율")]
        [SerializeField] private float meleeHitVfxScale = 0.35f;

        private GameObject _flameVfx;

        [Header("공격 애니메이션 (상체 레이어 'Attack')")]
        [Tooltip("근접 공격/사격/던지기 때 캐릭터 애니메이션을 재생한다. 컨트롤러에 Attack 레이어가 없으면 자동으로 무시된다 (Tools/Last Truck/Setup Attack Animation)")]
        [SerializeField] private bool useAttackAnimations = true;
        [Tooltip("휘두르기 시작 후 실제 타격이 들어가는 시점(초). 손이 가장 빨리 휘둘러지는 순간에 맞춘다")]
        [SerializeField] private float slashImpactDelay = 0.15f;
        [SerializeField] private float stabImpactDelay = 0.24f;
        [SerializeField] private float punchImpactDelay = 0.25f;

        [System.Serializable]
        public class HeldModel
        {
            public string itemId;
            public GameObject prefab;
            public Vector3 localPosition;
            public Vector3 localEuler;
            public float scale = 1f;
            [Tooltip("총: 총구가 항상 캐릭터가 바라보는 방향을 향하도록 월드 회전을 고정하고, 반동/총구/탄피 위치를 쓴다. 모델은 총구가 로컬 -Z인 Kenney 블래스터 기준")]
            public bool isGun;
            [Tooltip("탄피 종류: 0 없음, 1 권총탄, 2 소총탄")]
            public int casing;
        }

        [Header("손에 드는 무기 모델 (근접/총)")]
        [Tooltip("해당 아이템을 고르면 오른손(handslot.r)에 붙는 모델")]
        [SerializeField] private HeldModel[] heldMelee;

        [Header("총 이펙트")]
        [Tooltip("발사할 때 총구에서 터지는 불꽃(vfx_MuzzleFlash_01)")]
        [SerializeField] private GameObject muzzleFlashVfxPrefab;
        [SerializeField] private float muzzleFlashScale = 0.3f;
        [Tooltip("탄피 프리팹(권총탄/소총탄). 바닥에서 몇 초 뒤 사라진다")]
        [SerializeField] private GameObject casingPistolPrefab;
        [SerializeField] private GameObject casingRiflePrefab;
        [Tooltip("총을 쏠 때 총이 뒤로 밀렸다 돌아오는 거리(m)")]
        [SerializeField] private float gunKickBack = 0.14f;
        [Tooltip("총을 쏠 때 총구가 위로 들리는 각도(도)")]
        [SerializeField] private float gunKickPitch = 16f;
        [Tooltip("총알이 맞은 곳에 터지는 이펙트(vfx_Impact_01)")]
        [SerializeField] private GameObject bulletHitVfxPrefab;
        [SerializeField] private float bulletHitVfxScale = 0.45f;
        [Tooltip("대포/드릴 런처/레일 캐논 적중 이펙트(vfx_Explosion_01)")]
        [SerializeField] private GameObject explosionVfxPrefab;
        [SerializeField] private float explosionVfxScale = 0.5f;

        private GameObject _heldMeleeObj;
        private string _heldMeleeId;
        private bool _heldIsGun;
        private int _heldCasing;
        private Transform _heldGunModel;
        private Vector3 _heldGunBasePos;
        private Vector3 _heldGunEuler;
        private Vector3 _gunMuzzleLocal;
        private Vector3 _gunEjectLocal;
        private float _gunKick;
        private bool _heldTwoHand;
        private Vector3 _gunLeftGripLocal;
        private float _twoHandWeight;
        private int _attackLayer = -2;
        private float _attackLayerHoldUntil;
        private float _attackLayerWeight;
        private readonly Dictionary<string, bool> _animParamCache = new Dictionary<string, bool>();

        private bool HasAnimParam(string name)
        {
            if (_animator == null || !useAttackAnimations) return false;
            if (_animParamCache.TryGetValue(name, out bool has)) return has;
            has = false;
            foreach (AnimatorControllerParameter p in _animator.parameters)
                if (p.name == name) { has = true; break; }
            _animParamCache[name] = has;
            return has;
        }

        private bool PlayAttackAnim(string trigger)
        {
            if (!HasAnimParam(trigger)) return false;
            if (!_remoteView) AttackAnimPlayed?.Invoke(trigger);
            _attackLayerHoldUntil = Time.time + 0.25f;     // 전이가 시작될 때까지 레이어를 켜 둔다
            _animator.SetTrigger(trigger);
            return true;
        }

        private void SetShootingPose(bool on)
        {
            if (HasAnimParam("AtkShooting"))
            {
                if (on) _attackLayerHoldUntil = Time.time + 0.25f;
                _animator.SetBool("AtkShooting", on);
            }
        }

        private Transform FindRightHandSlot()
        {
            if (_animator == null || !_animator.isHuman) return null;
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return null;
            Transform slot = hand.Find("handslot.r");
            return slot != null ? slot : hand;
        }

        private void UpdateHeldMelee(string itemId)
        {
            HeldModel model = null;
            if (heldMelee != null && !string.IsNullOrEmpty(itemId))
                foreach (HeldModel m in heldMelee)
                    if (m != null && m.prefab != null && m.itemId == itemId) { model = m; break; }

            if (model == null)
            {
                if (_heldMeleeObj != null) { Destroy(_heldMeleeObj); _heldMeleeObj = null; _heldMeleeId = null; }
                return;
            }

            if (_heldMeleeObj != null && _heldMeleeId == itemId)
                return;
            if (_heldMeleeObj != null)
                Destroy(_heldMeleeObj);

            Transform slot = FindRightHandSlot();
            if (slot == null)
                return;

            _heldIsGun = model.isGun;
            _heldCasing = model.casing;
            _heldGunModel = null;

            if (model.isGun)
            {
                // 총: 손 위치에 붙은 빈 부모 + 그 아래 모델(총구 -Z → +Z로 뒤집음). 부모의 회전은 매 프레임 캐릭터 방향으로 고정한다.
                _heldMeleeObj = new GameObject("HeldGun_" + itemId);
                _heldMeleeObj.transform.SetParent(slot, false);
                _heldMeleeObj.transform.localPosition = model.localPosition;
                GameObject visual = Instantiate(model.prefab, _heldMeleeObj.transform);
                visual.name = "Model";
                _heldGunModel = visual.transform;
                _heldGunModel.localRotation = Quaternion.Euler(0f, 180f, 0f);
                _heldGunModel.localScale = Vector3.one * Mathf.Max(0.01f, model.scale);
                _heldGunBasePos = Vector3.zero;
                _heldGunModel.localPosition = _heldGunBasePos;
                _heldGunEuler = model.localEuler;
                foreach (Collider c in visual.GetComponentsInChildren<Collider>())
                    Destroy(c);

                // 총구/탄피 위치는 모델 크기에서 자동 계산(부모 로컬 기준, +Z가 총구 방향)
                Bounds b = new Bounds(Vector3.zero, Vector3.zero);
                bool any = false;
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
                {
                    Vector3 c = _heldMeleeObj.transform.InverseTransformPoint(r.bounds.center);
                    Vector3 e = r.bounds.extents;
                    Bounds rb = new Bounds(c, e * 2f);
                    if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
                }
                if (!any) b = new Bounds(Vector3.zero, new Vector3(0.1f, 0.1f, 0.4f));
                _gunMuzzleLocal = new Vector3(0f, b.center.y + b.extents.y * 0.3f, b.max.z);
                _gunEjectLocal = new Vector3(b.extents.x * 0.6f, b.center.y + b.extents.y * 0.6f, b.center.z);
                // 권총 외의 총은 양손으로: 왼손이 총열 아래쪽(앞부분)을 잡는다
                _heldTwoHand = itemId != ItemIds.Pistol;
                _gunLeftGripLocal = new Vector3(0f, b.center.y - b.extents.y * 0.3f, b.center.z + b.extents.z * 0.5f);
                _gunKick = 0f;
            }
            else
            {
                _heldMeleeObj = Instantiate(model.prefab, slot);
                _heldMeleeObj.name = "HeldMelee_" + itemId;
                _heldMeleeObj.transform.localPosition = model.localPosition;
                _heldMeleeObj.transform.localRotation = Quaternion.Euler(model.localEuler);
                _heldMeleeObj.transform.localScale = Vector3.one * Mathf.Max(0.01f, model.scale);
                foreach (Collider c in _heldMeleeObj.GetComponentsInChildren<Collider>())
                    Destroy(c);
            }
            _heldMeleeId = itemId;
        }

        // ---------- 총 반동 / 총구 불꽃 / 탄피 / 카메라 흔들림 ----------
        private void UpdateHeldGunPose()
        {
            float twoHandTarget = _heldIsGun && _heldTwoHand && _heldMeleeObj != null ? 1f : 0f;
            _twoHandWeight = Mathf.MoveTowards(_twoHandWeight, twoHandTarget, Time.deltaTime * 8f);

            if (!_heldIsGun || _heldMeleeObj == null)
                return;

            _gunKick = Mathf.MoveTowards(_gunKick, 0f, Time.deltaTime * 7f);
            // 총구는 항상 캐릭터가 바라보는 방향 + 발사 반동만큼 위로 들림
            _heldMeleeObj.transform.rotation = transform.rotation * Quaternion.Euler(-_gunKick * gunKickPitch, 0f, 0f) * Quaternion.Euler(_heldGunEuler);
            if (_heldGunModel != null)
                _heldGunModel.localPosition = _heldGunBasePos + new Vector3(0f, 0f, -_gunKick * gunKickBack);
        }

        private static float ShakeFor(string itemId)
        {
            switch (itemId)
            {
                case ItemIds.Pistol: return 0.4f;
                case ItemIds.Ak47: return 0.3f;
                case ItemIds.PlatinumSniperRifle: return 0.8f;
                case ItemIds.ShredderDrillLauncher: return 0.6f;
                case ItemIds.IronFieldCannon: return 0.9f;
                case ItemIds.PlatinumRailCannon: return 1f;
                default: return 0.3f;
            }
        }

        // 총알이 맞은 곳: 섬광 + 불똥. 대포류는 폭발 + 큰 흔들림
        private void SpawnBulletHit(Vector3 point, Vector3 normal)
        {
            if (!_remoteView) HitEffectSpawned?.Invoke(HitEffectBullet, _activeWeaponId, point, normal);

            bool heavy = _activeWeaponId == ItemIds.IronFieldCannon || _activeWeaponId == ItemIds.ShredderDrillLauncher
                         || _activeWeaponId == ItemIds.PlatinumRailCannon;
            GameObject prefab = heavy && explosionVfxPrefab != null ? explosionVfxPrefab : bulletHitVfxPrefab;
            if (prefab == null)
            {
                SpawnImpactVisual(point, new Color(1f, 0.9f, 0.5f));
                return;
            }

            Quaternion rot = normal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
            GameObject fx = Instantiate(prefab, point + normal * 0.05f, rot);
            fx.name = heavy ? "ExplosionVFX" : "BulletHitVFX";
            fx.transform.localScale = Vector3.one * (heavy ? explosionVfxScale : bulletHitVfxScale);
            Transform darkBack = fx.transform.Find("Darkback");
            if (darkBack != null)
                darkBack.gameObject.SetActive(false);
            fx.AddComponent<DelayedStopEmit>().Init(heavy ? 0.25f : 0.08f);
            Destroy(fx, 2f);
            if (heavy && !_remoteView)
                Combat.CameraShake.Shake(0.35f);
        }

        /// <summary>총을 쐈을 때: 반동 킥 + 총구 불꽃 + 탄피 + 카메라 흔들림.</summary>
        private void OnGunFired(string itemId, bool useHeldGun, Vector3 muzzleWorld, Vector3 rightDir, int casingKind)
        {
            if (!_remoteView) GunFired?.Invoke(itemId, useHeldGun, muzzleWorld, rightDir, casingKind);

            if (useHeldGun && _heldIsGun && _heldMeleeObj != null)
            {
                _gunKick = 1f;
                muzzleWorld = _heldMeleeObj.transform.TransformPoint(_gunMuzzleLocal);
                rightDir = _heldMeleeObj.transform.right;
                casingKind = _heldCasing;
            }

            if (muzzleFlashVfxPrefab != null)
            {
                GameObject flash = Instantiate(muzzleFlashVfxPrefab, muzzleWorld, Quaternion.LookRotation(transform.forward));
                flash.name = "MuzzleFlashVFX";
                flash.transform.localScale = Vector3.one * muzzleFlashScale;
                StartCoroutine(StopEmitAndCleanup(flash, 0.06f, 0.8f));
            }

            if (casingKind > 0)
                SpawnCasing(casingKind, useHeldGun && _heldMeleeObj != null ? _heldMeleeObj.transform.TransformPoint(_gunEjectLocal) : muzzleWorld - transform.forward * 0.2f, rightDir);

            if (!_remoteView) // 다른 사람이 쏜 총에는 내 화면을 흔들지 않는다
                Combat.CameraShake.Shake(ShakeFor(itemId));
        }

        private void SpawnCasing(int kind, Vector3 pos, Vector3 rightDir)
        {
            GameObject prefab = kind == 1 ? casingPistolPrefab : casingRiflePrefab;
            if (prefab == null)
                return;

            GameObject go = Instantiate(prefab, pos, Random.rotation);
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = rightDir * Random.Range(1.6f, 2.6f) + Vector3.up * Random.Range(1.8f, 3.0f) - transform.forward * Random.Range(0f, 0.6f);
                rb.angularVelocity = Random.insideUnitSphere * 20f;
            }
            Collider cc = go.GetComponent<Collider>();
            Collider pc = GetComponent<Collider>();
            if (cc != null && pc != null)
                Physics.IgnoreCollision(cc, pc);
        }

        // 공격 레이어는 공격 중에만 켠다(걷기/달리기 등 평소 모션은 기존 애니메이션 그대로 보이게)
        private void UpdateAttackLayerWeight()
        {
            if (_animator == null)
                return;
            if (_attackLayer == -2)
                _attackLayer = _animator.GetLayerIndex(AttackLayerName);
            if (_attackLayer < 0)
                return;

            bool busy = Time.time < _attackLayerHoldUntil
                        || _animator.IsInTransition(_attackLayer)
                        || !_animator.GetCurrentAnimatorStateInfo(_attackLayer).IsName("Empty");
            float target = busy ? 1f : 0f;
            _attackLayerWeight = Mathf.MoveTowards(_attackLayerWeight, target, Time.deltaTime * 10f);
            _animator.SetLayerWeight(_attackLayer, _attackLayerWeight);
        }

        private const string AttackLayerName = "Attack";

        private IEnumerator DelayedMeleeArc(WeaponStats stats, float delay)
        {
            yield return new WaitForSeconds(delay);
            FireMeleeArc(stats);
        }

        [Header("차징/연속 사용 중 이동")]
        [Tooltip("활을 당기는 중, 화염방사기·분무기를 뿜는 중의 이동 속도 배율. 1이면 느려지지 않는다")]
        [Range(0.1f, 1f)]
        [SerializeField] private float chargingMoveSpeedMultiplier = 0.5f;

        [Header("근접 무기 자동 조준")]
        [Tooltip("근접 무기(마체테 등)를 휘두르기 직전, 가장 가까운 대상을 바라보도록 자동으로 돌아서는 탐지 반경(m)")]
        [SerializeField] private float meleeAutoFaceRange = 6f;

        [Header("공격 후딜(이동 불가)")]
        [Tooltip("모든 무기 공격 후 기본으로 멈칫하는 시간(초)")]
        [SerializeField] private float attackRecoveryBase = 0.15f;
        [Tooltip("공격 딜레이(쿨다운) 1초당 추가로 멈칫하는 시간(초). 딜레이가 큰 무기일수록 반동으로 더 오래 멈춘다")]
        [SerializeField] private float attackRecoveryPerDelay = 0.2f;
        [Tooltip("후딜 최대 시간(초)")]
        [SerializeField] private float attackRecoveryMax = 0.7f;

        private Coroutine _attackRecoveryRoutine;

        [Header("맨손 공격")]
        [Tooltip("맨손일 때 공격 대상을 찾는 탐지 반경(m). 이 범위 안의 가장 가까운 대상을 바라보며 공격한다.")]
        [SerializeField] private float fistDetectRange = 6f;
        [Tooltip("실제로 피해가 닿는 근접 반경(m)")]
        [SerializeField] private float fistStrikeRadius = 1.4f;
        [SerializeField] private int fistDamage = 8;
        [SerializeField] private float fistCooldown = 0.5f;
        [Tooltip("공격 시작부터 피해가 적용되기까지(초). 이 동안 이동이 막힌다.")]
        [SerializeField] private float fistImpactDelay = 0.15f;
        [Tooltip("공격 전체 지속 시간(초, 이동 차단 총 시간)")]
        [SerializeField] private float fistWindup = 0.35f;

        private LastTruck.PlayerMove _playerMove;
        private float _nextFistTime;
        private bool _fistAttacking;

        private static readonly Dictionary<string, WeaponStats> Weapons = new Dictionary<string, WeaponStats>
        {
            {
                ItemIds.Ak47, new WeaponStats
                {
                    behavior = WeaponBehavior.Hitscan, damage = 15, range = 40f, cooldown = 0.15f,
                    effectColor = new Color(1f, 0.9f, 0.3f),
                }
            },
            {
                ItemIds.PlatinumSniperRifle, new WeaponStats
                {
                    behavior = WeaponBehavior.Hitscan, damage = 40, range = 80f, cooldown = 2.0f,
                    effectColor = new Color(0.4f, 0.85f, 1f),
                }
            },
            {
                ItemIds.Flamethrower, new WeaponStats
                {
                    behavior = WeaponBehavior.ContinuousCone, damage = 6, range = 6f, cooldown = 0.15f,
                    coneAngle = 50f, effectColor = new Color(1f, 0.5f, 0.15f),
                }
            },
            {
                ItemIds.HuntingBow, new WeaponStats
                {
                    behavior = WeaponBehavior.ChargeAndRelease, damage = 20, range = 30f,
                    chargeTime = 1.0f, minChargeFraction = 0.2f, effectColor = new Color(0.55f, 0.4f, 0.2f),
                }
            },
            {
                ItemIds.Machete, new WeaponStats
                {
                    behavior = WeaponBehavior.MeleeArc, damage = 25, range = 1.6f, cooldown = 0.4f,
                    coneAngle = 120f, effectColor = Color.white,
                }
            },
            {
                ItemIds.WoodSpear, new WeaponStats
                {
                    behavior = WeaponBehavior.MeleeArc, damage = 15, range = 2.0f, cooldown = 0.5f,
                    coneAngle = 100f, effectColor = new Color(0.6f, 0.5f, 0.3f),
                }
            },
            {
                ItemIds.FlameMachete, new WeaponStats
                {
                    behavior = WeaponBehavior.MeleeArc, damage = 32, range = 1.6f, cooldown = 0.35f,
                    coneAngle = 120f, effectColor = new Color(1f, 0.45f, 0.1f),
                }
            },
            {
                ItemIds.VibrationBlade, new WeaponStats
                {
                    behavior = WeaponBehavior.MeleeArc, damage = 38, range = 1.7f, cooldown = 0.3f,
                    coneAngle = 120f, effectColor = new Color(0.6f, 0.85f, 1f),
                }
            },
            {
                ItemIds.Pistol, new WeaponStats
                {
                    behavior = WeaponBehavior.Hitscan, damage = 10, range = 22f, cooldown = 0.22f,
                    effectColor = new Color(1f, 0.95f, 0.6f),
                }
            },
            {
                ItemIds.ChemicalSprayer, new WeaponStats
                {
                    behavior = WeaponBehavior.ContinuousCone, damage = 4, range = 5f, cooldown = 0.15f,
                    coneAngle = 45f, effectColor = new Color(0.45f, 0.85f, 0.3f),
                }
            },
            {
                ItemIds.ShredderDrillLauncher, new WeaponStats
                {
                    behavior = WeaponBehavior.Hitscan, damage = 35, range = 35f, cooldown = 0.6f,
                    effectColor = new Color(0.6f, 0.5f, 0.3f),
                }
            },
            {
                ItemIds.IronFieldCannon, new WeaponStats
                {
                    behavior = WeaponBehavior.Hitscan, damage = 60, range = 45f, cooldown = 1.8f,
                    effectColor = new Color(0.4f, 0.4f, 0.42f),
                }
            },
            {
                ItemIds.PlatinumRailCannon, new WeaponStats
                {
                    behavior = WeaponBehavior.Hitscan, damage = 90, range = 90f, cooldown = 2.5f,
                    effectColor = new Color(0.55f, 0.85f, 1f),
                }
            },
            {
                ItemIds.ChemicalGasGrenade, new WeaponStats
                {
                    behavior = WeaponBehavior.Throw, damage = 25, range = 20f, cooldown = 1.5f,
                    blastRadius = 3f, gasDuration = 4f, gasTickInterval = 1f, gasTickDamage = 5,
                    throwSpeed = 14f, effectColor = new Color(0.45f, 0.85f, 0.3f),
                }
            },
            {
                ItemIds.FragGrenade, new WeaponStats
                {
                    behavior = WeaponBehavior.Throw, damage = 35, range = 20f, cooldown = 1.2f,
                    blastRadius = 3f, gasDuration = 0f, gasTickInterval = 0f, gasTickDamage = 0,
                    throwSpeed = 14f, effectColor = new Color(0.55f, 0.5f, 0.45f),
                }
            },
        };

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _animator = GetComponentInChildren<Animator>();
            _playerMove = GetComponent<LastTruck.PlayerMove>();
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void OnDisable()
        {
            // 꺼질 때(사망/트럭 탑승 등) 진행 중이던 차징/화염도 정리해서 다른 사람 화면에 남지 않게 한다.
            CancelOngoingActions();
            StopFlameVfx();
            SetShootingPose(false);
            if (_heldMeleeObj != null) { Destroy(_heldMeleeObj); _heldMeleeObj = null; _heldMeleeId = null; }
            UnequipBow();
            UnequipRifle();
            LockMovement(false);
            if (_playerMove != null)
                _playerMove.SetSpeedMultiplier(1f);
        }

        private void Update()
        {
            // 멀티플레이: 다른 사람 캐릭터(복제본)는 입력을 읽지 않고 네트워크로 받은 상태만 보여준다.
            if (_remoteView)
            {
                UpdateRemoteView();
                return;
            }

            if (targetCamera == null)
                targetCamera = Camera.main;

            bool blocked =
                (UnityEngine.EventSystems.EventSystem.current != null &&
                 UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) ||
                (GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen) ||
                targetCamera == null;

            ItemData selected = _inventory.SelectedItem;
            UpdateHeldMelee(selected != null ? selected.itemID : null);
            WeaponStats stats = default;
            bool hasWeapon = !blocked && selected != null && Weapons.TryGetValue(selected.itemID, out stats);

            // 팝업/UI 위에 마우스가 있어 공격만 막힌 경우에도 활은 손에 계속 들고 있는다.
            bool holdingBow = selected != null && Weapons.TryGetValue(selected.itemID, out WeaponStats heldStats) &&
                              heldStats.behavior == WeaponBehavior.ChargeAndRelease;
            if (holdingBow)
                EquipBow();
            else
                UnequipBow();

            bool holdingRifle = selected != null && selected.itemID == ItemIds.PlatinumSniperRifle && rifleHeldPrefab != null;
            if (holdingRifle)
                EquipRifle();
            else
                UnequipRifle();

            ReportHeldWeapon(selected != null && Weapons.ContainsKey(selected.itemID) ? selected.itemID : null);

            if (!hasWeapon || selected.itemID != _activeWeaponId)
            {
                // 무기를 바꾸거나 내려놓으면 진행 중이던 차징/화염 상태를 정리한다.
                CancelOngoingActions();
                _activeWeaponId = hasWeapon ? selected.itemID : null;
                if (!hasWeapon)
                {
                    bool handEmpty = !blocked && selected == null;
                    UpdateFistAttack(handEmpty);
                    return;
                }
            }

            switch (stats.behavior)
            {
                case WeaponBehavior.Hitscan:
                    if (Input.GetMouseButtonDown(0))
                    {
                        _pressVetoed = RaycastHitsWorldInteractable(stats.range);
                        if (!_pressVetoed && Time.time >= _nextFireTime)
                        {
                            _nextFireTime = Time.time + stats.cooldown;
                            if (holdingRifle && bulletPrefab != null)
                                FireBullet(stats);
                            else
                            {
                                FireHitscan(stats, stats.damage);
                                // 사격 반동: 권총은 한 손, 그 외 총기는 두 손 자세(저격총은 IK로 따로 잡으므로 제외)
                                if (!holdingRifle)
                                    PlayAttackAnim(selected.itemID == ItemIds.Pistol ? "AtkShoot1H" : "AtkShoot2H");
                                OnGunFired(selected.itemID, true, transform.position + Vector3.up * 1.2f, transform.right, 0);
                            }
                            StartAttackRecovery(stats.cooldown);
                        }
                    }
                    break;

                case WeaponBehavior.MeleeArc:
                    if (Input.GetMouseButtonDown(0))
                    {
                        _pressVetoed = RaycastHitsWorldInteractable(stats.range);
                        if (!_pressVetoed && Time.time >= _nextFireTime)
                        {
                            _nextFireTime = Time.time + stats.cooldown;
                            AutoFaceNearestTarget(meleeAutoFaceRange);
                            bool stab = selected.itemID == ItemIds.WoodSpear;
                            if (PlayAttackAnim(stab ? "AtkStab" : "AtkSlash"))
                                StartCoroutine(DelayedMeleeArc(stats, stab ? stabImpactDelay : slashImpactDelay));
                            else
                                FireMeleeArc(stats);
                            StartAttackRecovery(stats.cooldown);
                        }
                    }
                    break;

                case WeaponBehavior.ContinuousCone:
                    if (Input.GetMouseButtonDown(0))
                        _pressVetoed = RaycastHitsWorldInteractable(stats.range);

                    if (!_pressVetoed && Input.GetMouseButton(0))
                    {
                        if (!AutoFaceNearestTarget(stats.range))
                            AimTowardMouse(stats.range);

                        if (!_isFlameActive)
                        {
                            _isFlameActive = true;
                            SetShootingPose(true);
                            if (selected.itemID == ItemIds.Flamethrower && flamethrowerVfxPrefab != null)
                                StartFlameVfx(stats);
                            else
                                _flameEffectRoutine = StartCoroutine(FlameEffectRoutine(stats));
                            if (!_remoteView) FlameChanged?.Invoke(true);
                        }

                        if (Time.time >= _nextFireTime)
                        {
                            _nextFireTime = Time.time + stats.cooldown;
                            FireCone(stats);
                            Combat.CameraShake.Shake(0.05f);     // 화염 뿜는 동안 은은한 진동
                        }
                    }
                    else if (_isFlameActive && (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)))
                    {
                        StopFlameEffect();
                        StartAttackRecovery(stats.cooldown);
                    }
                    break;

                case WeaponBehavior.ChargeAndRelease:
                    if (Input.GetMouseButtonDown(0))
                    {
                        _pressVetoed = RaycastHitsWorldInteractable(stats.range);
                        if (!_pressVetoed)
                        {
                            _chargeStartTime = Time.time;
                            _bowStats = stats;
                            SetCharging(true);
                        }
                    }
                    else if (_isCharging && Input.GetMouseButton(0))
                    {
                        if (!AutoFaceNearestTarget(stats.range))
                            AimTowardMouse(stats.range);
                    }
                    else if (Input.GetMouseButtonUp(0) && _isCharging)
                    {
                        SetCharging(false);
                        float fraction = GetChargeFraction(stats);
                        if (fraction >= stats.minChargeFraction)
                        {
                            int scaledDamage = Mathf.Max(1, Mathf.RoundToInt(stats.damage * Mathf.Lerp(0.4f, 1f, fraction)));
                            if (arrowPrefab != null)
                                FireArrow(stats, scaledDamage, fraction);
                            else
                                FireHitscan(stats, scaledDamage);
                            // 활은 쿨다운 대신 당긴 시간만큼(세게 당길수록 오래) 멈칫한다.
                            StartAttackRecovery(stats.chargeTime * fraction);
                        }
                    }
                    break;

                case WeaponBehavior.Throw:
                    if (Input.GetMouseButtonDown(0))
                    {
                        _pressVetoed = RaycastHitsWorldInteractable(stats.range);
                        if (!_pressVetoed && Time.time >= _nextFireTime)
                        {
                            _nextFireTime = Time.time + stats.cooldown;
                            FireGrenade(stats);
                            PlayAttackAnim("AtkThrow");
                            StartAttackRecovery(stats.cooldown);
                            // 1회용 소모품: 던지는 즉시 인벤토리에서 1개 소모되어 사라진다.
                            _inventory.RemoveItem(selected, 1);
                        }
                    }
                    break;
            }
        }

        #region 멀티플레이: 다른 사람 화면에 무기 보여주기 (LastTruck.Networking.NetworkWeaponVisuals)

        // 내 캐릭터: 아래 이벤트로 "무엇을 들었는지 / 쐈는지"를 알린다 → 네트워크로 다른 사람에게 전달.
        // 다른 사람 캐릭터(복제본): SetRemoteView(true) 상태에서 SetRemote*/PlayRemote*로 받은 대로 보여주기만 한다
        //   (피해 계산은 쏜 사람 컴퓨터 → 호스트가 한다. 여기서는 연출만, 내 화면 흔들림도 없음).

        public const int HitEffectImpact = 0;   // 기본 타격 구체
        public const int HitEffectBullet = 1;   // 총알 적중(섬광/폭발)
        public const int HitEffectMelee = 2;    // 근접 무기 적중

        /// <summary>손에 든 무기 ID가 바뀜 (무기가 아니면 null).</summary>
        public event System.Action<string> HeldWeaponChanged;
        /// <summary>활 차징 시작(true)/끝(false).</summary>
        public event System.Action<bool> ChargingChanged;
        /// <summary>화염방사기/분무기 분사 시작(true)/끝(false).</summary>
        public event System.Action<bool> FlameChanged;
        /// <summary>총알 궤적 (무기 ID, 시작, 끝).</summary>
        public event System.Action<string, Vector3, Vector3> ShotFired;
        /// <summary>맞은 곳 이펙트 (종류, 무기 ID, 위치, 표면 방향).</summary>
        public event System.Action<int, string, Vector3, Vector3> HitEffectSpawned;
        /// <summary>화살 발사 (위치, 속도).</summary>
        public event System.Action<Vector3, Vector3> ArrowFired;
        /// <summary>저격총 총알 발사 (위치, 속도).</summary>
        public event System.Action<Vector3, Vector3> BulletFired;
        /// <summary>수류탄 투척 (무기 ID, 위치, 속도).</summary>
        public event System.Action<string, Vector3, Vector3> GrenadeThrown;
        /// <summary>공격 애니메이션 트리거 (AtkSlash, AtkShoot2H 등).</summary>
        public event System.Action<string> AttackAnimPlayed;
        /// <summary>총 발사 연출 (무기 ID, 손에 든 총 기준 여부, 총구, 오른쪽 방향, 탄피 종류).</summary>
        public event System.Action<string, bool, Vector3, Vector3, int> GunFired;

        private bool _remoteView;
        private string _remoteWeaponId;
        private bool _remoteCharging;
        private float _remoteChargeStart;
        private bool _remoteFlame;
        private string _reportedHeldWeapon;
        private bool _hasReportedHeld;

        public bool IsRemoteView => _remoteView;

        /// <summary>다음 프레임에 들고 있는 무기를 다시 알린다 (네트워크 연결 직후 한 번).</summary>
        public void ResendHeldWeapon() => _hasReportedHeld = false;

        public void SetRemoteView(bool remote)
        {
            _remoteView = remote;
            if (!remote)
                return;
            CancelOngoingActions();
            UnequipBow();
            UnequipRifle();
        }

        public void SetRemoteHeldWeapon(string weaponId) => _remoteWeaponId = weaponId;

        public void SetRemoteCharging(bool charging)
        {
            if (charging && !_remoteCharging)
                _remoteChargeStart = Time.time;
            _remoteCharging = charging;
        }

        public void SetRemoteFlame(bool flaming) => _remoteFlame = flaming;

        public void PlayRemoteShot(string weaponId, Vector3 from, Vector3 to) => SpawnTracer(from, to, GetEffectColor(weaponId));

        public void PlayRemoteHitEffect(int kind, string weaponId, Vector3 position, Vector3 normal)
        {
            string previous = _activeWeaponId;
            _activeWeaponId = weaponId; // 총알 적중 이펙트 종류(대포류 폭발 등)를 무기로 고른다
            if (kind == HitEffectBullet) SpawnBulletHit(position, normal);
            else if (kind == HitEffectMelee) SpawnMeleeHit(position, GetEffectColor(weaponId));
            else SpawnImpactVisual(position, GetEffectColor(weaponId));
            _activeWeaponId = previous;
        }

        /// <summary>보여주기용 화살 (피해 없음 - 실제 피해는 쏜 사람 쪽 화살이 준다).</summary>
        public void PlayRemoteArrow(Vector3 position, Vector3 velocity)
        {
            if (arrowPrefab == null || velocity.sqrMagnitude < 0.0001f)
                return;
            WeaponStats stats = Weapons[ItemIds.HuntingBow];
            ArrowProjectile arrow = Instantiate(arrowPrefab, position, Quaternion.LookRotation(velocity));
            arrow.LaunchVisual(velocity, stats.range * 1.5f, hitMask, transform);
        }

        /// <summary>보여주기용 저격총 총알 (피해 없음).</summary>
        public void PlayRemoteBullet(Vector3 position, Vector3 velocity)
        {
            if (bulletPrefab == null || velocity.sqrMagnitude < 0.0001f)
                return;
            WeaponStats stats = Weapons[ItemIds.PlatinumSniperRifle];
            ArrowProjectile bullet = Instantiate(bulletPrefab, position, Quaternion.LookRotation(velocity));
            bullet.LaunchVisual(velocity, stats.range, hitMask, transform);
            SpawnImpactVisual(position, new Color(1f, 0.85f, 0.4f));
            _rifleRecoil = 1f;
        }

        /// <summary>보여주기용 수류탄 (피해 없음, 터지면 가스 구름 모양만).</summary>
        public void PlayRemoteGrenade(string weaponId, Vector3 position, Vector3 velocity)
        {
            if (weaponId == null || !Weapons.TryGetValue(weaponId, out WeaponStats stats))
                return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GrenadeProjectile(Remote)";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.18f;
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", stats.effectColor);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            GrenadeProjectile grenade = go.AddComponent<GrenadeProjectile>();
            grenade.LaunchVisual(velocity, stats.blastRadius, stats.gasDuration, hitMask, transform);
        }

        public void PlayRemoteAttackAnim(string trigger) => PlayAttackAnim(trigger);

        public void PlayRemoteGunFired(string itemId, bool useHeldGun, Vector3 muzzle, Vector3 right, int casingKind)
            => OnGunFired(itemId, useHeldGun, muzzle, right, casingKind);

        public static Color GetEffectColor(string weaponId)
        {
            return weaponId != null && Weapons.TryGetValue(weaponId, out WeaponStats stats) ? stats.effectColor : Color.white;
        }

        private void UpdateRemoteView()
        {
            WeaponStats stats = default;
            bool hasWeapon = _remoteWeaponId != null && Weapons.TryGetValue(_remoteWeaponId, out stats);
            _activeWeaponId = _remoteWeaponId;

            // 손에 드는 모델: 근접 무기/총(오른손), 활(왼손), 저격총(두 손)
            UpdateHeldMelee(_remoteWeaponId);

            bool holdingBow = hasWeapon && stats.behavior == WeaponBehavior.ChargeAndRelease;
            if (holdingBow)
            {
                EquipBow();
                _bowStats = stats;
                _chargeStartTime = _remoteChargeStart;
            }
            else
            {
                UnequipBow();
            }
            _isCharging = holdingBow && _remoteCharging;

            bool holdingRifle = _remoteWeaponId == ItemIds.PlatinumSniperRifle && rifleHeldPrefab != null;
            if (holdingRifle)
                EquipRifle();
            else
                UnequipRifle();

            bool flaming = hasWeapon && stats.behavior == WeaponBehavior.ContinuousCone && _remoteFlame;
            if (flaming && !_isFlameActive)
            {
                _isFlameActive = true;
                SetShootingPose(true);
                if (_remoteWeaponId == ItemIds.Flamethrower && flamethrowerVfxPrefab != null)
                    StartFlameVfx(stats);
                else
                    _flameEffectRoutine = StartCoroutine(FlameEffectRoutine(stats));
            }
            else if (!flaming && _isFlameActive)
            {
                StopFlameEffect();
            }
        }

        private void ReportHeldWeapon(string weaponId)
        {
            if (_hasReportedHeld && _reportedHeldWeapon == weaponId)
                return;
            _hasReportedHeld = true;
            _reportedHeldWeapon = weaponId;
            HeldWeaponChanged?.Invoke(weaponId);
        }

        private void SetCharging(bool charging)
        {
            if (_isCharging == charging)
                return;
            _isCharging = charging;
            if (!_remoteView)
                ChargingChanged?.Invoke(charging);
        }

        #endregion

        private void LateUpdate()
        {
            if (!_remoteView)
                UpdateChargingSlowdown();
            UpdateRifle();
            UpdateAttackLayerWeight();
            UpdateHeldGunPose();

            // 애니메이션이 손 뼈대를 움직인 뒤에 활/화살 자세를 덮어쓴다.
            if (_heldBow == null)
                return;

            float targetWeight = _isCharging ? 1f : 0f;
            float blendSpeed = aimBlendTime > 0f ? Time.deltaTime / aimBlendTime : 1f;
            _aimWeight = Mathf.MoveTowards(_aimWeight, targetWeight, blendSpeed);

            ApplyArcherStance();
            UpdateIkCorrection();
            UpdateBowPose();

            // 실제 궁수처럼 평소엔 화살을 걸지 않고, 차징할 때만 시위에 걸어 당긴다.
            if (_nockedArrow != null)
            {
                _nockedArrow.SetActive(_isCharging);
                float fraction = _isCharging ? GetChargeFraction(_bowStats) : 0f;
                float draw = maxDrawDistance * fraction;

                // 오른손 IK 목표는 원하는 만큼 당긴 위치. 팔이 짧아 거기까지 못 가면
                // 화살 꼬리를 실제 오른손 위치까지만 당겨서 손이 화살을 잡고 있는 모습을 유지한다.
                _desiredNockWorld = _heldBow.transform.TransformPoint(NockRestLocalPosition + Vector3.back * draw);
                if (_rightHand != null && _aimWeight > 0f)
                {
                    float handDraw = NockRestLocalPosition.z - _heldBow.transform.InverseTransformPoint(_rightHand.position).z;
                    draw = Mathf.Clamp(handDraw, 0f, draw);
                }
                _nockedArrow.transform.localPosition = NockRestLocalPosition + Vector3.back * draw;
            }
        }

        /// <summary>
        /// 조준 시 궁수 자세: 상체를 돌려 왼쪽 어깨가 과녁을 향하게 하고, 머리는 반대로 돌려 정면을 본다.
        /// 애니메이션이 매 프레임 뼈대를 새로 쓰므로 회전이 누적되지 않는다.
        /// </summary>
        private void ApplyArcherStance()
        {
            if (_chest == null || _aimWeight <= 0f)
                return;

            Quaternion twist = Quaternion.AngleAxis(aimTorsoTwist * _aimWeight, transform.up);
            _chest.rotation = twist * _chest.rotation;
            if (_head != null)
                _head.rotation = Quaternion.Inverse(twist) * _head.rotation;
        }

        /// <summary>
        /// 이 리그는 손 뼈대 스케일(3배) 때문에 휴머노이드 IK 결과가 목표에서 일정하게 어긋난다(손이 위로 뜸).
        /// 실제 손 위치와 목표의 차이를 매 프레임 조금씩 IK 목표에 더해서 손이 목표에 맞도록 보정한다.
        /// </summary>
        private void UpdateIkCorrection()
        {
            if (_aimWeight < 0.99f || _chest == null)
                return;

            const float gain = 0.5f;
            const float maxCorrection = 0.3f;

            Vector3 leftError = GetAimGripLocal() - transform.InverseTransformPoint(_bowHand.position);
            _leftIkCorrection = Vector3.ClampMagnitude(_leftIkCorrection + leftError * gain, maxCorrection);

            if (_rightHand != null && _nockedArrow != null)
            {
                Vector3 rightError = transform.InverseTransformPoint(_desiredNockWorld) -
                                     transform.InverseTransformPoint(_rightHand.position);
                _rightIkCorrection = Vector3.ClampMagnitude(_rightIkCorrection + rightError * gain, maxCorrection);
            }
        }

        /// <summary>
        /// 활 손잡이는 항상 실제 왼손 위치에 둔다(손이 활을 쥔 모습 유지).
        /// 방향만 평소(앞으로 비스듬히 눕힘) ↔ 조준(세워서 정면)을 _aimWeight로 섞는다.
        /// </summary>
        private void UpdateBowPose()
        {
            if (_bowHand == null)
                return;

            Quaternion facing = Quaternion.LookRotation(transform.forward, Vector3.up);
            Quaternion carryRot = facing * Quaternion.Euler(carryTiltAngle, 0f, 0f);
            _heldBow.transform.SetPositionAndRotation(
                _bowHand.position, Quaternion.Slerp(carryRot, facing, _aimWeight));
        }

        /// <summary>
        /// 조준 시 왼손(활 손잡이) 목표: 상체를 돌린 뒤의 왼쪽 어깨에서 정면으로 팔을 뻗은 지점. 캐릭터 로컬 좌표.
        /// </summary>
        private Vector3 GetAimGripLocal()
        {
            Quaternion twist = Quaternion.AngleAxis(aimTorsoTwist * _aimWeight, Vector3.up);
            Vector3 shoulder = _chestLocal + twist * (_leftShoulderLocal - _chestLocal);
            return shoulder + Vector3.forward * (_leftArmLength * aimArmExtension) + Vector3.right * AimGripInward;
        }

        /// <summary>
        /// IK는 상체를 돌리기 전 자세에서 풀리므로, 돌린 뒤 원하는 위치를 돌리기 전 기준으로 되돌려 목표로 준다.
        /// </summary>
        private Vector3 ToPreTwistWorld(Vector3 local, float twistDegrees)
        {
            Quaternion untwist = Quaternion.AngleAxis(-twistDegrees, Vector3.up);
            return transform.TransformPoint(_chestLocal + untwist * (local - _chestLocal));
        }

        /// <summary>
        /// 조준 중 팔 IK: 왼손은 활 손잡이, 오른손은 시위(당겨진 화살 꼬리)를 잡는다.
        /// 애니메이터 레이어의 IK Pass가 켜져 있어야 호출된다.
        /// </summary>
        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null)
                return;

            // 저격총: 오른손은 손잡이, 왼손은 총열 아래 LeftGrip.
            if (_heldRifle != null && _chest != null && _rifleLeftGrip != null)
            {
                float rw = _rifleWeight;
                float twistDegrees = rifleTorsoTwist * rw;
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, rw);
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rw);
                _animator.SetIKPosition(AvatarIKGoal.RightHand,
                    ToPreTwistWorld(GetRifleGripLocal() + _rifleRightIkCorrection, twistDegrees));
                Vector3 leftGripLocal = transform.InverseTransformPoint(_rifleLeftGrip.position);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand,
                    ToPreTwistWorld(leftGripLocal + _rifleLeftIkCorrection, twistDegrees));
                return;
            }

            // 권총 외의 총: 왼손도 총열 아래를 잡는다(양손)
            if (_heldIsGun && _heldTwoHand && _heldMeleeObj != null && _heldBow == null)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _twoHandWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, _heldMeleeObj.transform.TransformPoint(_gunLeftGripLocal));
                return;
            }

            float w = _heldBow != null && _chest != null ? _aimWeight : 0f;
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            if (w <= 0f)
                return;

            float bowTwist = aimTorsoTwist * _aimWeight;
            _animator.SetIKPosition(AvatarIKGoal.LeftHand, ToPreTwistWorld(GetAimGripLocal() + _leftIkCorrection, bowTwist));
            if (_nockedArrow != null)
            {
                Vector3 nockLocal = transform.InverseTransformPoint(_desiredNockWorld);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, ToPreTwistWorld(nockLocal + _rightIkCorrection, bowTwist));
            }
        }

        private float GetChargeFraction(WeaponStats stats)
        {
            if (stats.chargeTime <= 0f)
                return 1f;
            return Mathf.Clamp01((Time.time - _chargeStartTime) / stats.chargeTime);
        }

        private void CancelOngoingActions()
        {
            SetCharging(false);
            StopFlameEffect();
            StopAttackRecovery();
        }

        /// <summary>
        /// 공격 직후 잠깐 이동을 막는다(후딜). 공격 딜레이가 큰 무기일수록 반동을 생각해 더 오래 막는다.
        /// </summary>
        private void StartAttackRecovery(float attackDelay)
        {
            float seconds = Mathf.Min(attackRecoveryBase + attackDelay * attackRecoveryPerDelay, attackRecoveryMax);
            StopAttackRecovery();
            if (seconds > 0f)
                _attackRecoveryRoutine = StartCoroutine(AttackRecoveryRoutine(seconds));
        }

        private void StopAttackRecovery()
        {
            // 후딜이 진행 중일 때만 잠금을 푼다. 빈손일 때는 CancelOngoingActions가 매 프레임 불리므로,
            // 무조건 풀면 맨손 공격(FistAttackRoutine)이 건 이동 잠금까지 바로 풀려 버린다.
            if (_attackRecoveryRoutine == null)
                return;

            StopCoroutine(_attackRecoveryRoutine);
            _attackRecoveryRoutine = null;
            LockMovement(false);
        }

        private IEnumerator AttackRecoveryRoutine(float seconds)
        {
            LockMovement(true);
            yield return new WaitForSeconds(seconds);
            LockMovement(false);
            _attackRecoveryRoutine = null;
        }

        /// <summary>
        /// 활을 캐릭터 왼손에 쥐어 준다. 시위에 걸 화살도 미리 만들어 두고 차징할 때만 보인다.
        /// </summary>
        private void EquipBow()
        {
            if (_heldBow != null || bowPrefab == null)
                return;

            CacheArmBones();
            Transform hand = _leftHandBone != null ? _leftHandBone : transform;

            _aimWeight = 0f;
            _bowHand = hand;
            _heldBow = Instantiate(bowPrefab, hand);
            _heldBow.name = "HeldBow";
            // 손 뼈대에 스케일이 걸려 있어도(리그에 따라 3배 등) 프리팹 원래 크기로 보이게 한다.
            Vector3 parentScale = hand.lossyScale;
            Vector3 prefabScale = bowPrefab.transform.localScale;
            _heldBow.transform.localScale = new Vector3(
                prefabScale.x / parentScale.x, prefabScale.y / parentScale.y, prefabScale.z / parentScale.z);
            UpdateBowPose();
            DisableColliders(_heldBow);

            if (arrowPrefab != null)
            {
                _nockedArrow = Instantiate(arrowPrefab.gameObject, _heldBow.transform);
                _nockedArrow.name = "NockedArrow";
                Destroy(_nockedArrow.GetComponent<ArrowProjectile>());
                _nockedArrow.transform.localPosition = NockRestLocalPosition;
                _nockedArrow.transform.localRotation = Quaternion.identity;
                DisableColliders(_nockedArrow);
                _nockedArrow.SetActive(false);
            }
        }

        private void UnequipBow()
        {
            if (_heldBow == null)
                return;

            Destroy(_heldBow);
            _heldBow = null;
            _nockedArrow = null;
            _bowHand = null;
            _aimWeight = 0f;
        }

        /// <summary>
        /// 활·총 자세 계산에 쓰는 뼈대를 찾아 둔다. 위치는 장착 순간(상체를 돌리기 전) 캐릭터 로컬 좌표로 기록한다.
        /// 휴머노이드가 아니거나 필요한 뼈대가 없으면 _chest가 null이 되고, 이때는 IK 없이 모델만 붙인다.
        /// </summary>
        private void CacheArmBones()
        {
            _leftHandBone = null;
            _rightHand = null;
            _chest = null;
            _head = null;
            if (_animator == null || !_animator.isHuman)
                return;

            _leftHandBone = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftShoulder = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightShoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform elbow = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            if (_chest == null)
                _chest = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);

            if (_leftHandBone == null || _rightHand == null || leftShoulder == null || rightShoulder == null ||
                elbow == null || _chest == null)
            {
                _chest = null;
                return;
            }

            _chestLocal = transform.InverseTransformPoint(_chest.position);
            _leftShoulderLocal = transform.InverseTransformPoint(leftShoulder.position);
            _rightShoulderLocal = transform.InverseTransformPoint(rightShoulder.position);
            _leftArmLength = Vector3.Distance(leftShoulder.position, elbow.position) +
                             Vector3.Distance(elbow.position, _leftHandBone.position);
        }

        /// <summary>
        /// 저격총을 두 손으로 든다. 오른손은 손잡이, 왼손은 총열 아래 LeftGrip을 IK로 잡는다.
        /// </summary>
        private void EquipRifle()
        {
            if (_heldRifle != null || rifleHeldPrefab == null)
                return;

            CacheArmBones();
            _heldRifle = Instantiate(rifleHeldPrefab, transform);
            _heldRifle.name = "HeldRifle";
            _rifleLeftGrip = _heldRifle.transform.Find("LeftGrip");
            _rifleMuzzle = _heldRifle.transform.Find("Muzzle");
            DisableColliders(_heldRifle);
            _rifleWeight = 0f;
            _rifleRecoil = 0f;
            UpdateRifle();
        }

        private void UnequipRifle()
        {
            if (_heldRifle == null)
                return;

            Destroy(_heldRifle);
            _heldRifle = null;
            _rifleLeftGrip = null;
            _rifleMuzzle = null;
            _rifleWeight = 0f;
        }

        /// <summary>
        /// 오른손 목표: 상체를 돌린 뒤의 오른쪽 어깨에서 rifleGripOffset만큼 떨어진 지점(캐릭터 로컬).
        /// </summary>
        private Vector3 GetRifleGripLocal()
        {
            Quaternion twist = Quaternion.AngleAxis(rifleTorsoTwist * _rifleWeight, Vector3.up);
            return _chestLocal + twist * (_rightShoulderLocal - _chestLocal) + rifleGripOffset;
        }

        /// <summary>
        /// 총 자세: 상체를 돌리고(머리는 정면), 손 IK 보정을 갱신한 뒤 총을 실제 오른손 위치에 맞춰 놓는다.
        /// 반동(_rifleRecoil)이 있으면 총을 살짝 뒤로 밀고 총구를 들어 올린다.
        /// </summary>
        private void UpdateRifle()
        {
            if (_heldRifle == null)
                return;

            float blendSpeed = aimBlendTime > 0f ? Time.deltaTime / aimBlendTime : 1f;
            _rifleWeight = Mathf.MoveTowards(_rifleWeight, 1f, blendSpeed);
            _rifleRecoil = Mathf.MoveTowards(_rifleRecoil, 0f, Time.deltaTime / 0.15f);

            if (_chest != null)
            {
                Quaternion twist = Quaternion.AngleAxis(rifleTorsoTwist * _rifleWeight, transform.up);
                _chest.rotation = twist * _chest.rotation;
                if (_head != null)
                    _head.rotation = Quaternion.Inverse(twist) * _head.rotation;

                if (_rifleWeight >= 0.99f && _rifleLeftGrip != null)
                {
                    // 이 리그는 IK 결과가 목표에서 일정하게 어긋나므로(손 뼈대 3배 스케일) 차이만큼 목표를 보정한다.
                    const float gain = 0.5f;
                    const float maxCorrection = 0.3f;
                    Vector3 rightError = GetRifleGripLocal() - transform.InverseTransformPoint(_rightHand.position);
                    _rifleRightIkCorrection = Vector3.ClampMagnitude(_rifleRightIkCorrection + rightError * gain, maxCorrection);
                    Vector3 leftError = transform.InverseTransformPoint(_rifleLeftGrip.position) -
                                        transform.InverseTransformPoint(_leftHandBone.position);
                    _rifleLeftIkCorrection = Vector3.ClampMagnitude(_rifleLeftIkCorrection + leftError * gain, maxCorrection);
                }
            }

            Transform rifle = _heldRifle.transform;
            rifle.rotation = Quaternion.LookRotation(transform.forward, Vector3.up) *
                             Quaternion.Euler(-8f * _rifleRecoil, 0f, 0f);
            Vector3 gripWorld = _chest != null ? _rightHand.position : transform.TransformPoint(new Vector3(0.05f, 0.25f, 0.2f));
            rifle.position = gripWorld - transform.forward * (0.05f * _rifleRecoil);
        }

        /// <summary>
        /// 총구에서 조준점으로 총알을 쏜다. 근처에 적이 있으면 자동 조준, 없으면 마우스 방향.
        /// </summary>
        private void FireBullet(WeaponStats stats)
        {
            // 캐릭터를 돌리기 전에 총구 위치를 캐릭터 기준으로 기억해 두고, 돌린 뒤 다시 월드 좌표로 바꾼다.
            Vector3 muzzleLocal = _rifleMuzzle != null
                ? transform.InverseTransformPoint(_rifleMuzzle.position)
                : new Vector3(0f, 1.2f, 0.5f);

            Vector3 aimPoint = GetAutoAimPoint(stats.range, transform.TransformPoint(muzzleLocal).y);
            FaceDirection(aimPoint - transform.position);

            Vector3 muzzle = transform.TransformPoint(muzzleLocal);
            Vector3 dir = aimPoint - muzzle;
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;

            ArrowProjectile bullet = Instantiate(bulletPrefab, muzzle, Quaternion.LookRotation(dir));
            bullet.Launch(dir * bulletSpeed, stats.damage, stats.range, hitMask, transform);
            if (!_remoteView) BulletFired?.Invoke(muzzle, dir * bulletSpeed);

            SpawnImpact(muzzle, new Color(1f, 0.85f, 0.4f));
            _rifleRecoil = 1f;
            // 저격총은 자체 IK 시스템을 쓰므로 총구/탄피는 그 총구 기준으로, 소총탄 배출 + 큰 카메라 흔들림
            OnGunFired(ItemIds.PlatinumSniperRifle, false, muzzle, transform.right, 2);
        }

        private static void DisableColliders(GameObject go)
        {
            foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        /// <summary>
        /// 시위에 걸린 화살 위치에서 마우스가 가리키는 지점으로 화살을 쏜다.
        /// 차징이 길수록 빠르게 날아가고, 중력만큼 위로 보정해 조준점에 떨어지도록 한다.
        /// </summary>
        private void FireArrow(WeaponStats stats, int damage, float fraction)
        {
            Vector3 spawnPos;
            if (_nockedArrow != null)
                spawnPos = _nockedArrow.transform.position;
            else if (_heldBow != null)
                spawnPos = _heldBow.transform.position;
            else
                spawnPos = transform.position + Vector3.up * 1.2f + transform.forward * 0.3f;

            Vector3 target = GetAutoAimPoint(stats.range, spawnPos.y);
            Vector3 toTarget = target - spawnPos;
            if (toTarget.sqrMagnitude < 0.01f)
                toTarget = transform.forward;

            float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, fraction);
            float flightTime = toTarget.magnitude / speed;
            ArrowProjectile arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(toTarget));
            Vector3 velocity = toTarget.normalized * speed + Vector3.up * (0.5f * arrow.Gravity * flightTime);
            arrow.Launch(velocity, damage, stats.range * 1.5f, hitMask, transform);
            ArrowFired?.Invoke(spawnPos, velocity);
        }

        /// <summary>
        /// 화학 독가스 수류탄: 마우스가 가리키는 지점으로 포물선을 그리며 던진다.
        /// 맞으면 즉시 폭발 피해를 주고, 자리에 독가스 구름을 남긴다.
        /// </summary>
        private void FireGrenade(WeaponStats stats)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.3f + transform.forward * 0.4f;
            Vector3 target = GetMouseAimPoint(stats.range, spawnPos.y);
            Vector3 toTarget = target - spawnPos;
            if (toTarget.sqrMagnitude < 0.01f)
                toTarget = transform.forward * (stats.range * 0.5f);

            float speed = stats.throwSpeed;
            float flightTime = toTarget.magnitude / speed;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GrenadeProjectile";
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one * 0.18f;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", stats.effectColor);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            GrenadeProjectile grenade = go.AddComponent<GrenadeProjectile>();
            Vector3 velocity = toTarget.normalized * speed + Vector3.up * (0.5f * grenade.Gravity * flightTime);
            grenade.Launch(velocity, stats.damage, stats.blastRadius, stats.gasDuration, stats.gasTickInterval,
                stats.gasTickDamage, hitMask, transform);
            if (!_remoteView) GrenadeThrown?.Invoke(_activeWeaponId, spawnPos, velocity);
        }

        private Vector3 GetMouseAimPoint(float range, float fallbackHeight)
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

            // 플레이어 자신을 제외하고 마우스 아래 가장 가까운 지점을 조준점으로 쓴다.
            RaycastHit[] hits = Physics.RaycastAll(ray, range * 3f, hitMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = Vector3.zero;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform))
                    continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    point = hit.point;
                }
            }
            if (best < float.MaxValue)
                return point;

            Plane plane = new Plane(Vector3.up, new Vector3(0f, fallbackHeight, 0f));
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : ray.origin + ray.direction * range;
        }

        /// <summary>
        /// 맨손(빈손) 상태일 때의 공격. 원신처럼 근처 가장 가까운 대상을 자동으로 바라보며 때리고,
        /// 공격이 진행되는 동안(락온+타격)에는 WASD 이동을 잠근다.
        /// </summary>
        private void UpdateFistAttack(bool handEmpty)
        {
            if (!handEmpty || _fistAttacking)
                return;

            if (Input.GetMouseButtonDown(0) && Time.time >= _nextFistTime)
            {
                bool vetoed = RaycastHitsWorldInteractable(fistStrikeRadius);
                if (!vetoed)
                {
                    _nextFistTime = Time.time + fistCooldown;
                    StartCoroutine(FistAttackRoutine());
                }
            }
        }

        private IEnumerator FistAttackRoutine()
        {
            _fistAttacking = true;
            LockMovement(true);

            AutoFaceNearestTarget(fistDetectRange);

            // 주먹 애니메이션이 있으면 팔이 뻗는 순간에 맞춰 타격한다
            float impact = PlayAttackAnim("AtkPunch") ? Mathf.Max(fistImpactDelay, punchImpactDelay) : fistImpactDelay;
            yield return new WaitForSeconds(impact);
            FireFistStrike();

            float remaining = fistWindup - impact;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            LockMovement(false);
            _fistAttacking = false;
        }

        private void FireFistStrike()
        {
            Vector3 origin = transform.position + Vector3.up * 1f;
            Collider[] hits = Physics.OverlapSphere(origin, fistStrikeRadius, hitMask);

            Damageable nearest = null;
            Vector3 nearestPoint = origin;
            float nearestDist = float.MaxValue;

            foreach (Collider col in hits)
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null)
                    continue;

                float dist = (col.transform.position - transform.position).sqrMagnitude;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = d;
                    nearestPoint = col.ClosestPoint(origin);
                }
            }

            if (nearest != null)
            {
                nearest.TakeDamage(fistDamage, nearestPoint);
                SpawnImpact(nearestPoint, new Color(0.95f, 0.8f, 0.65f));
            }
        }

        private Damageable FindNearestDamageable(float range)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, range, hitMask);
            Damageable nearest = null;
            float nearestDist = float.MaxValue;

            foreach (Collider col in hits)
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null)
                    continue;

                float dist = (col.transform.position - transform.position).sqrMagnitude;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = d;
                }
            }

            return nearest;
        }

        private void LockMovement(bool locked)
        {
            if (_playerMove != null)
                _playerMove.SetMovementLocked(locked);
        }

        /// <summary>
        /// 활을 당기는 중이거나 화염방사기·분무기를 뿜는 중에는 이동 속도를 낮춘다.
        /// 무기를 바꾸거나 멈추면 CancelOngoingActions/StopFlameEffect로 상태가 풀려 다음 프레임에 원래 속도로 돌아온다.
        /// </summary>
        private void UpdateChargingSlowdown()
        {
            if (_playerMove == null)
                return;

            bool slowed = _isCharging || _isFlameActive;
            _playerMove.SetSpeedMultiplier(slowed ? chargingMoveSpeedMultiplier : 1f);
        }

        /// <summary>
        /// 원신처럼, 지정한 반경 안의 가장 가까운 대상 쪽으로 캐릭터를 자동으로 돌린다(공격 직전 락온용).
        /// </summary>
        /// <summary>탐지 범위 안의 가장 가까운 대상을 바라보도록 돌린다. 대상을 찾아 돌렸으면 true.</summary>
        private bool AutoFaceNearestTarget(float detectRange)
        {
            Damageable target = FindNearestDamageable(detectRange);
            if (target == null)
                return false;

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.01f)
                return false;

            FaceDirection(toTarget);
            return true;
        }

        /// <summary>캐릭터를 즉시 해당 수평 방향으로 돌린다.</summary>
        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Quaternion faceRotation = Quaternion.LookRotation(direction.normalized);

            // transform.rotation을 즉시 반영해야 같은 프레임에서 바로 이어지는 판정(부채꼴/레이캐스트 등)이
            // 새 방향을 기준으로 계산된다(그렇지 않으면 물리 스텝 전까지 이전 방향으로 판정됨).
            transform.rotation = faceRotation;

            // PlayerMove가 Rigidbody(보간 On)로 회전을 관리하므로, transform만 바꾸면 다음 물리
            // 스텝에서 Rigidbody의 이전 회전값으로 되돌아간다. Rigidbody도 같은 값으로 맞춰준다.
            // 멀티플레이 캐릭터는 Rigidbody가 없고 방향을 네트워크 입력으로 보내야 하므로 SetFacing을 쓴다.
            if (_playerMove != null && _playerMove.IsNetworkControlled)
                LastTruck.PlayerMove.SetFacing(transform, faceRotation);
            else if (_playerMove != null && _playerMove.rigidbody != null)
                _playerMove.rigidbody.MoveRotation(faceRotation);
        }

        /// <summary>
        /// 원거리 무기 자동 조준용 조준점. 범위 안에 대상이 있으면 그 대상을, 없으면 기존처럼 마우스 조준점을 반환한다.
        /// (수류탄은 이 함수를 쓰지 않고 항상 마우스 커서를 직접 조준한다.)
        /// </summary>
        private Vector3 GetAutoAimPoint(float range, float fallbackHeight)
        {
            Damageable target = FindNearestDamageable(range);
            if (target != null)
                return target.transform.position + Vector3.up * 1f;

            return GetMouseAimPoint(range, fallbackHeight);
        }

        private void StopFlameEffect()
        {
            if (_isFlameActive && !_remoteView)
                FlameChanged?.Invoke(false);
            _isFlameActive = false;
            if (_flameEffectRoutine != null)
            {
                StopCoroutine(_flameEffectRoutine);
                _flameEffectRoutine = null;
            }
            SetShootingPose(false);
            StopFlameVfx();
        }

        // 화염방사기: 플레이어 앞쪽에 불꽃 프리팹을 붙여 두고, 쏘는 동안 플레이어 방향을 따라간다.
        private void StartFlameVfx(WeaponStats stats)
        {
            if (_flameVfx != null)
                return;

            _flameVfx = Instantiate(flamethrowerVfxPrefab, transform);
            _flameVfx.name = "FlamethrowerVFX";
            _flameVfx.transform.localPosition = new Vector3(0f, 1.2f, 0.5f);
            _flameVfx.transform.localRotation = Quaternion.identity;
            float scale = flamethrowerVfxLength > 0.1f ? Mathf.Max(0.3f, stats.range / flamethrowerVfxLength) : 1f;
            _flameVfx.transform.localScale = Vector3.one * scale;
        }

        private void StopFlameVfx()
        {
            if (_flameVfx == null)
                return;

            // 방출만 멈추고 남은 불꽃은 자연스럽게 사라지게 한 뒤 정리한다.
            foreach (ParticleSystem ps in _flameVfx.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _flameVfx.transform.SetParent(null, true);
            Destroy(_flameVfx, 1.5f);
            _flameVfx = null;
        }

        private bool RaycastHitsWorldInteractable(float range)
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out RaycastHit hit, range, hitMask) && IsWorldInteractable(hit.collider);
        }

/// <summary>
        /// 화염방사기를 쓸 때, 현재 바라보는 방향이 아니라 마우스로 클릭(드래그)한 방향을 바라보도록 회전한다.
        /// </summary>
        private void AimTowardMouse(float range)
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 targetPoint;

            if (Physics.Raycast(ray, out RaycastHit hit, range * 2f, hitMask))
            {
                targetPoint = hit.point;
            }
            else
            {
                Plane groundPlane = new Plane(Vector3.up, transform.position);
                targetPoint = groundPlane.Raycast(ray, out float enter)
                    ? ray.GetPoint(enter)
                    : ray.origin + ray.direction * range;
            }

            Vector3 dir = targetPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                LastTruck.PlayerMove.SetFacing(transform, Quaternion.LookRotation(dir.normalized)); // 멀티플레이에서도 방향이 동기화되도록
        }


        private void FireHitscan(WeaponStats stats, int damage)
        {
            Vector3 muzzle = transform.position + Vector3.up * 1.2f;

            // 근처에 대상이 있으면 그쪽을 자동으로 바라보며 쏘고(원신 락온 스타일),
            // 없으면 기존처럼 마우스 커서 방향을 조준한다.
            Vector3 aimPoint = GetAutoAimPoint(stats.range, muzzle.y);
            Vector3 dir = aimPoint - muzzle;
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
            FaceDirection(dir);

            Vector3 endPoint;
            if (Physics.Raycast(muzzle, dir, out RaycastHit hit, stats.range, hitMask))
            {
                endPoint = hit.point;
                Damageable target = hit.collider.GetComponentInParent<Damageable>();
                if (target != null)
                    target.TakeDamage(damage, hit.point);

                SpawnBulletHit(hit.point, hit.normal);
            }
            else
            {
                endPoint = muzzle + dir * stats.range;
            }

            SpawnTracer(muzzle, endPoint, stats.effectColor);
        }

        /// <summary>
        /// 마체테: 전방 부채꼴 안에서 가장 가까운 적 최대 1명에게만 피해를 준다.
        /// </summary>
        private void FireMeleeArc(WeaponStats stats)
        {
            Vector3 origin = transform.position + Vector3.up * 1f;
            Collider[] hits = Physics.OverlapSphere(origin, stats.range, hitMask);

            Damageable nearest = null;
            Vector3 nearestPoint = origin;
            float nearestDist = float.MaxValue;

            foreach (Collider col in hits)
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null)
                    continue;

                Vector3 toTarget = col.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f &&
                    Vector3.Angle(transform.forward, toTarget.normalized) > stats.coneAngle * 0.5f)
                    continue;

                float dist = (col.transform.position - transform.position).sqrMagnitude;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = d;
                    nearestPoint = col.ClosestPoint(origin);
                }
            }

            if (nearest != null)
            {
                nearest.TakeDamage(stats.damage, nearestPoint);
                SpawnMeleeHit(nearestPoint, stats.effectColor);
                Combat.CameraShake.Shake(0.1f);
            }
        }

        /// <summary>
        /// 화염방사기: 전방 부채꼴 범위 안의 모든 적에게 동시에 피해를 준다.
        /// </summary>
        private void FireCone(WeaponStats stats)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Collider[] hits = Physics.OverlapSphere(origin, stats.range, hitMask);
            var damaged = new HashSet<Damageable>();

            foreach (Collider col in hits)
            {
                Damageable d = col.GetComponentInParent<Damageable>();
                if (d == null || damaged.Contains(d))
                    continue;

                Vector3 toTarget = col.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f)
                    continue;
                if (Vector3.Angle(transform.forward, toTarget.normalized) > stats.coneAngle * 0.5f)
                    continue;

                d.TakeDamage(stats.damage, col.ClosestPoint(origin));
                damaged.Add(d);
            }
        }

        private static bool IsWorldInteractable(Collider collider)
        {
            return collider.GetComponentInParent<ProcessingFacility>() != null ||
                   collider.GetComponentInParent<TruckStation>() != null ||
                   collider.GetComponentInParent<ResourceNode>() != null ||
                   collider.GetComponentInParent<SpecialResourceNode>() != null;
        }

        private IEnumerator FlameEffectRoutine(WeaponStats stats)
        {
            var wait = new WaitForSeconds(0.03f);
            while (true)
            {
                Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
                float angleOffset = Random.Range(-stats.coneAngle * 0.4f, stats.coneAngle * 0.4f);
                Vector3 dir = Quaternion.Euler(0f, angleOffset, 0f) * transform.forward;
                float dist = Random.Range(stats.range * 0.3f, stats.range);
                Vector3 pos = origin + dir * dist + Vector3.up * Random.Range(-0.2f, 0.3f);
                SpawnFlameParticle(pos, stats.effectColor);
                yield return wait;
            }
        }

        private void SpawnFlameParticle(Vector3 pos, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FlameParticle";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            float size = Random.Range(0.08f, 0.18f);
            go.transform.localScale = Vector3.one * size;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            Destroy(go, 0.25f);
        }

        private void SpawnTracer(Vector3 from, Vector3 to, Color color)
        {
            if (!_remoteView)
                ShotFired?.Invoke(_activeWeaponId, from, to);

            var go = new GameObject("WeaponTracer");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.05f;
            lr.endWidth = 0.02f;
            lr.numCapVertices = 4;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0.2f);
            Destroy(go, 0.08f);
        }

        // 근접 무기 적중: 베기/문양 없이 맞은 지점에서 한 번 터지는 단순 타격 이펙트
        private void SpawnMeleeHit(Vector3 pos, Color color)
        {
            if (!_remoteView) HitEffectSpawned?.Invoke(HitEffectMelee, _activeWeaponId, pos, Vector3.zero);

            if (meleeHitVfxPrefab == null)
            {
                SpawnImpactVisual(pos, color);
                return;
            }

            GameObject go = Instantiate(meleeHitVfxPrefab, pos, Quaternion.identity);
            go.name = "MeleeHitVFX";
            go.transform.localScale = Vector3.one * meleeHitVfxScale;
            // 뒤에 깔리는 검은 얼룩(Darkback)은 빼고 밝은 섬광 + 튀는 불똥만 남겨 '단순 타격'으로 쓴다
            Transform darkBack = go.transform.Find("Darkback");
            if (darkBack != null)
                darkBack.gameObject.SetActive(false);
            StartCoroutine(StopEmitAndCleanup(go, 0.1f, 1.2f));
        }

        private static IEnumerator StopEmitAndCleanup(GameObject go, float emitSeconds, float destroyAfter)
        {
            yield return new WaitForSeconds(emitSeconds);
            if (go == null)
                yield break;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(go, destroyAfter);
        }

        private void SpawnImpact(Vector3 pos, Color color)
        {
            if (!_remoteView)
                HitEffectSpawned?.Invoke(HitEffectImpact, _activeWeaponId, pos, Vector3.zero);
            SpawnImpactVisual(pos, color);
        }

        private void SpawnImpactVisual(Vector3 pos, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "WeaponImpact";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.12f;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            StartCoroutine(AnimateImpact(go));
        }

        private static IEnumerator AnimateImpact(GameObject go)
        {
            const float duration = 0.18f;
            float t = 0f;
            Vector3 baseScale = go.transform.localScale;

            while (t < duration)
            {
                t += Time.deltaTime;
                go.transform.localScale = baseScale * (1f + (t / duration) * 3f);
                yield return null;
            }

            Destroy(go);
        }
    }
}
