using Combat;
using CraftingSystem;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 무기 연출 동기화 (플레이어 프리팹에 붙는다, 메뉴 1번이 추가).
    ///
    ///  - 들고 있는 무기 / 활 차징 / 화염방사기 분사 상태는 [Networked] 값으로 모두에게 (늦게 보이는 사람도 같은 상태).
    ///  - 총알 궤적 / 맞은 곳 이펙트 / 화살·총알·수류탄 / 공격 모션 / 총구 불꽃·탄피는 한 번씩 일어나는 일이라 RPC로 보낸다 (쏜 사람 본인에게는 다시 보내지 않음).
    ///  - 다른 사람 화면의 캐릭터(복제본)는 WeaponController를 "보여주기 전용"(SetRemoteView)으로 돌려
    ///    같은 무기 모델(활/저격총/근접·총)/조준 자세/공격 모션/총구 궤적/불꽃/화살을 그린다.
    ///    복제 투사체는 피해를 주지 않고, 다른 사람이 쏜 총에 내 화면이 흔들리지도 않는다.
    ///  - 피해 계산은 그대로: 쏜 사람 컴퓨터에서 맞았는지 판정 → 호스트가 체력 처리(NetworkHealth).
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkWeaponVisuals : NetworkBehaviour
    {
        #region 네트워크 상태

        /// <summary>손에 든 무기 (아이템 카탈로그 번호 + 1, 0 = 무기 없음).</summary>
        [Networked] private int HeldWeapon { get; set; }
        [Networked] private NetworkBool Charging { get; set; }
        [Networked] private NetworkBool Flaming { get; set; }

        #endregion

        #region 상태

        private WeaponController _weapon;
        private NetworkPlayer _player;

        #endregion

        #region 생명주기

        private void Awake()
        {
            _weapon = GetComponent<WeaponController>();
            _player = GetComponent<NetworkPlayer>();
        }

        public override void Spawned()
        {
            if (_weapon == null) _weapon = GetComponent<WeaponController>();
            if (_weapon == null) return;

            if (Object.HasInputAuthority)
            {
                _weapon.SetRemoteView(false);
                _weapon.HeldWeaponChanged += OnLocalHeldWeaponChanged;
                _weapon.ChargingChanged += OnLocalChargingChanged;
                _weapon.FlameChanged += OnLocalFlameChanged;
                _weapon.ShotFired += OnLocalShotFired;
                _weapon.HitEffectSpawned += OnLocalHitEffect;
                _weapon.ArrowFired += OnLocalArrowFired;
                _weapon.BulletFired += OnLocalBulletFired;
                _weapon.GrenadeThrown += OnLocalGrenadeThrown;
                _weapon.AttackAnimPlayed += OnLocalAttackAnim;
                _weapon.GunFired += OnLocalGunFired;
                _weapon.ResendHeldWeapon();
            }
            else
            {
                _weapon.SetRemoteView(true);
                _weapon.enabled = true;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_weapon == null) return;
            _weapon.HeldWeaponChanged -= OnLocalHeldWeaponChanged;
            _weapon.ChargingChanged -= OnLocalChargingChanged;
            _weapon.FlameChanged -= OnLocalFlameChanged;
            _weapon.ShotFired -= OnLocalShotFired;
            _weapon.HitEffectSpawned -= OnLocalHitEffect;
            _weapon.ArrowFired -= OnLocalArrowFired;
            _weapon.BulletFired -= OnLocalBulletFired;
            _weapon.GrenadeThrown -= OnLocalGrenadeThrown;
            _weapon.AttackAnimPlayed -= OnLocalAttackAnim;
            _weapon.GunFired -= OnLocalGunFired;
        }

        #endregion

        #region 내 캐릭터 → 호스트 / 다른 사람

        private bool CanSend => Object != null && Object.IsValid;

        private void OnLocalHeldWeaponChanged(string weaponId) { if (CanSend) RPC_SetHeld(ToIndex(weaponId)); }
        private void OnLocalChargingChanged(bool charging) { if (CanSend) RPC_SetCharging(charging); }
        private void OnLocalFlameChanged(bool flaming) { if (CanSend) RPC_SetFlame(flaming); }
        private void OnLocalShotFired(string weaponId, Vector3 from, Vector3 to) { if (CanSend) RPC_Shot(ToIndex(weaponId), from, to); }
        private void OnLocalHitEffect(int kind, string weaponId, Vector3 position, Vector3 normal) { if (CanSend) RPC_HitEffect(kind, ToIndex(weaponId), position, normal); }
        private void OnLocalArrowFired(Vector3 position, Vector3 velocity) { if (CanSend) RPC_Arrow(position, velocity); }
        private void OnLocalBulletFired(Vector3 position, Vector3 velocity) { if (CanSend) RPC_Bullet(position, velocity); }
        private void OnLocalGrenadeThrown(string weaponId, Vector3 position, Vector3 velocity) { if (CanSend) RPC_Grenade(ToIndex(weaponId), position, velocity); }
        private void OnLocalAttackAnim(string trigger) { if (CanSend) RPC_AttackAnim(trigger); }
        private void OnLocalGunFired(string weaponId, bool useHeldGun, Vector3 muzzle, Vector3 right, int casingKind)
        {
            if (CanSend) RPC_GunFired(ToIndex(weaponId), useHeldGun, muzzle, right, casingKind);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetHeld(int weaponIndex) => HeldWeapon = weaponIndex;

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetCharging(NetworkBool charging) => Charging = charging;

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetFlame(NetworkBool flaming) => Flaming = flaming;

        [Rpc(RpcSources.InputAuthority, RpcTargets.All, InvokeLocal = false)]
        private void RPC_Shot(int weaponIndex, Vector3 from, Vector3 to)
        {
            if (IsRemoteViewReady) _weapon.PlayRemoteShot(ToWeaponId(weaponIndex), from, to);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All, InvokeLocal = false)]
        private void RPC_HitEffect(int kind, int weaponIndex, Vector3 position, Vector3 normal)
        {
            if (IsRemoteViewReady) _weapon.PlayRemoteHitEffect(kind, ToWeaponId(weaponIndex), position, normal);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All, InvokeLocal = false)]
        private void RPC_Bullet(Vector3 position, Vector3 velocity)
        {
            if (IsRemoteViewReady) _weapon.PlayRemoteBullet(position, velocity);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All, InvokeLocal = false)]
        private void RPC_Grenade(int weaponIndex, Vector3 position, Vector3 velocity)
        {
            if (IsRemoteViewReady) _weapon.PlayRemoteGrenade(ToWeaponId(weaponIndex), position, velocity);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All, InvokeLocal = false)]
        private void RPC_AttackAnim(string trigger)
        {
            if (IsRemoteViewReady) _weapon.PlayRemoteAttackAnim(trigger);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All, InvokeLocal = false)]
        private void RPC_GunFired(int weaponIndex, NetworkBool useHeldGun, Vector3 muzzle, Vector3 right, int casingKind)
        {
            if (IsRemoteViewReady) _weapon.PlayRemoteGunFired(ToWeaponId(weaponIndex), useHeldGun, muzzle, right, casingKind);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All, InvokeLocal = false)]
        private void RPC_Arrow(Vector3 position, Vector3 velocity)
        {
            if (IsRemoteViewReady) _weapon.PlayRemoteArrow(position, velocity);
        }

        #endregion

        #region 다른 사람 캐릭터: 화면 반영

        private bool IsRemoteViewReady => _weapon != null && !Object.HasInputAuthority && _weapon.IsRemoteView;

        public override void Render()
        {
            if (!IsRemoteViewReady) return;

            // 죽었거나 트럭에 탄 사람은 무기를 보여주지 않는다.
            bool visible = _player == null || (_player.IsAlive && !_player.IsSeated);
            _weapon.SetRemoteHeldWeapon(visible ? ToWeaponId(HeldWeapon) : null);
            _weapon.SetRemoteCharging(visible && Charging);
            _weapon.SetRemoteFlame(visible && Flaming);
        }

        #endregion

        #region 무기 ID ↔ 번호

        private static int ToIndex(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return 0;
            ItemData item = ItemCatalog.GetOrCreate().GetItem(weaponId);
            int index = ItemIndexLookup.IndexOf(item);
            return index >= 0 ? index + 1 : 0;
        }

        private static string ToWeaponId(int weaponIndex)
        {
            if (weaponIndex <= 0) return null;
            ItemData item = ItemIndexLookup.ItemAt(weaponIndex - 1);
            return item != null ? item.itemID : null;
        }

        #endregion
    }
}
