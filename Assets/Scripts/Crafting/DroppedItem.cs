using System.Collections.Generic;
using LastTruck;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 플레이어가 버린 아이템 (마인크래프트 방식).
    ///  - 던지면 포물선으로 날아가 바닥에서 튕기고, 작게 빙글빙글 돌며 둥둥 떠 있다. 발밑에는 부드러운 그림자만 깔린다.
    ///  - 같은 아이템이 가까이 떨어지면 하나로 합쳐지고(개수 표시), 개수가 많을수록 겹쳐 쌓인 모양으로 보인다.
    ///  - 서로 다른 아이템이 같은 자리에 겹치면 살짝 밀어내서 한곳에 뭉치지 않게 한다.
    ///  - E키로 줍는다. (멀티플레이: 버린 사람 화면에만 보이는 연출이라 서로 복제되지 않는다.)
    /// </summary>
    public class DroppedItem : MonoBehaviour, IInteractable, IInteractLabel, IInteractPriority
    {
        private const int InteractLayer = 4;       // PlayerInteract.interactLayer (Water)와 같은 레이어
        private const float RestHeight = 0.32f;    // 바닥에서 아이템 중심까지의 높이
        private const float MergeRadius = 1.2f;    // 이 안에 같은 아이템이 있으면 합친다
        private const float SpreadRadius = 0.55f;  // 다른 아이템과 이만큼 가까우면 밀어낸다
        private const float Gravity = 22f;
        private const float ItemScale = 0.55f;
        private const float SpinSpeed = 70f;       // 초당 회전 각도

        private ItemData _item;
        private int _count;
        private int _durability;                   // 도구를 버린 경우 그 도구의 남은 내구도 (0 = 새 것)
        private float _seed;
        private Transform _visual;                 // 돌고 떠다니는 부분
        private Transform _shadow;
        private float _spawnTime;
        private bool _picked;
        private bool _built;
        private int _copies = -1;

        private Vector3 _vel;
        private bool _flying;
        private float _flightTime;
        private float _groundY;
        private float _pop;                        // 합쳐질 때 살짝 커졌다 돌아오는 연출
        private float _settleTime;
        private float _spin;
        private Vector3 _origin;

        private static Sprite s_shadowSprite;
        private static readonly RaycastHit[] Hits = new RaycastHit[24];
        private static readonly Collider[] Overlaps = new Collider[24];

        public string InteractLabel => _item != null ? _item.itemName + (_count > 1 ? " x" + _count : "") : "";
        public string InteractSubLabel => "E 줍기";
        public Color InteractLabelColor => new Color(1f, 0.88f, 0.55f);
        public int InteractPriority => 3;

        // ───────────────────────── 만들기 ─────────────────────────

        /// <summary>
        /// 근처에 같은 아이템이 이미 떨어져 있으면 거기에 개수를 합치고, 없으면 새로 떨어뜨린다.
        /// (가방이 꽉 찬 채로 계속 채집해도 바닥이 아이템으로 어지럽지 않게)
        /// </summary>
        public static DroppedItem SpawnOrMerge(Transform player, ItemData item, int count)
        {
            if (player == null || item == null || count <= 0)
                return null;

            DroppedItem existing = FindMergeTarget(player.position, item, null, 3f);
            if (existing != null)
            {
                existing.Absorb(count);
                return existing;
            }
            return Spawn(player, item, count);
        }

        /// <summary>player 앞쪽으로 아이템을 던진다. durability는 도구가 닳은 정도를 그대로 들고 있게 한다.</summary>
        public static DroppedItem Spawn(Transform player, ItemData item, int count, int durability = 0)
        {
            if (player == null || item == null || count <= 0)
                return null;

            Vector3 fwd = player.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = player.position + Vector3.up * 1.15f + fwd * 0.45f;
            Vector3 vel = fwd * Random.Range(2.6f, 3.4f) + side * Random.Range(-0.5f, 0.5f) + Vector3.up * Random.Range(2.2f, 2.9f);
            return Create(origin, vel, item, count, durability);
        }

        /// <summary>월드 위치에서 사방으로 튀어 나오게 아이템을 떨어뜨린다 (몬스터 전리품 등).</summary>
        public static DroppedItem SpawnAt(Vector3 position, ItemData item, int count, int durability = 0)
        {
            if (item == null || count <= 0)
                return null;

            float ang = Random.value * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            Vector3 vel = dir * Random.Range(1.0f, 2.2f) + Vector3.up * Random.Range(2.6f, 3.6f);
            return Create(position + Vector3.up * 0.9f, vel, item, count, durability);
        }

        private static DroppedItem Create(Vector3 origin, Vector3 vel, ItemData item, int count, int durability)
        {
            var go = new GameObject("DroppedItem_" + item.itemName);
            go.layer = InteractLayer;
            go.transform.position = origin;

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.75f;

            var di = go.AddComponent<DroppedItem>();
            di._durability = durability;
            di._item = item;
            di._count = count;
            di._seed = Random.value * 10f;
            di._spawnTime = Time.time;
            di._vel = vel;
            di._flying = true;
            di._groundY = origin.y - 1f;
            di._origin = origin;
            di.Build();
            return di;
        }

        // ───────────────────────── 합치기 ─────────────────────────

        private static DroppedItem FindMergeTarget(Vector3 around, ItemData item, DroppedItem except, float radius)
        {
            if (item == null || item.HasDurability)   // 내구도가 있는 도구는 한 개마다 닳은 정도가 달라서 합치지 않는다
                return null;

            int n = Physics.OverlapSphereNonAlloc(around, radius, Overlaps, 1 << InteractLayer, QueryTriggerInteraction.Collide);
            DroppedItem best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (!Overlaps[i].TryGetComponent<DroppedItem>(out var e) || e == except) continue;
                if (e._picked || e._flying || e._item == null || e._durability != 0) continue;
                if (!ItemMatching.IsSameItem(e._item, item)) continue;
                float d = (e.transform.position - around).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }

        private void Absorb(int count)
        {
            _count += count;
            _pop = 1f;
            Build();
        }

        // ───────────────────────── 모양 ─────────────────────────

        private static int CopiesFor(int count) => count <= 1 ? 1 : count <= 8 ? 2 : count <= 24 ? 3 : 4;

        private static Sprite ShadowSprite()
        {
            if (s_shadowSprite != null) return s_shadowSprite;
            const int N = 48;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);   // 부드러운 가장자리
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_shadowSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            s_shadowSprite.name = "DroppedItemShadow";
            return s_shadowSprite;
        }

        private static readonly Dictionary<Sprite, Sprite> s_cleanIcons = new Dictionary<Sprite, Sprite>();

        /// <summary>
        /// 아이콘에 깔린 반투명 사각 배경(슬롯 칸용)을 걷어 낸 스프라이트. 이게 없으면 월드에서 겹쳐 쌓일수록 유리판처럼 보인다.
        /// 아이템마다 한 번만 만들어 둔다.
        /// </summary>
        private static Sprite CleanIcon(Sprite src)
        {
            if (src == null) return null;
            if (s_cleanIcons.TryGetValue(src, out Sprite cached) && cached != null) return cached;

            Sprite result = src;
            try
            {
                Texture2D tex = src.texture;
                Rect r = src.textureRect;
                int w = Mathf.Max(1, (int)r.width), h = Mathf.Max(1, (int)r.height);
                RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var copy = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                copy.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                Color32[] px = copy.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    float a = px[i].a / 255f;
                    px[i].a = (byte)(Mathf.Clamp01((a - 0.4f) / 0.35f) * 255f);   // 옅은 배경은 지우고, 그림 가장자리는 부드럽게
                }
                copy.SetPixels32(px);
                copy.Apply(false, true);
                result = Sprite.Create(copy, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), src.pixelsPerUnit);
                result.name = src.name + "_drop";
            }
            catch (System.Exception)
            {
                result = src;   // 실패하면 원래 아이콘 그대로
            }
            s_cleanIcons[src] = result;
            return result;
        }

        private void Build()
        {
            if (_item == null) return;

            if (!_built)
            {
                _built = true;

                var vis = new GameObject("Visual");
                vis.layer = InteractLayer;
                vis.transform.SetParent(transform, false);
                _visual = vis.transform;

                var sh = new GameObject("Shadow");
                sh.layer = InteractLayer;
                sh.transform.SetParent(transform, false);
                _shadow = sh.transform;
                _shadow.rotation = Quaternion.Euler(90f, 0f, 0f);
                var ssr = sh.AddComponent<SpriteRenderer>();
                ssr.sprite = ShadowSprite();
                ssr.color = new Color(0f, 0f, 0f, 0.42f);
                ssr.sortingOrder = 0;
            }

            int copies = CopiesFor(_count);
            if (copies == _copies) return;
            _copies = copies;

            for (int i = _visual.childCount - 1; i >= 0; i--)
                Destroy(_visual.GetChild(i).gameObject);

            var rng = new System.Random(_seed.GetHashCode());
            for (int c = 0; c < copies; c++)
            {
                var copy = new GameObject("Copy" + c);
                copy.layer = InteractLayer;
                copy.transform.SetParent(_visual, false);
                // 겹쳐 쌓인 느낌: 살짝씩 어긋나고 위로 조금씩 올라간다
                float ox = c == 0 ? 0f : ((float)rng.NextDouble() - 0.5f) * 0.22f;
                float oz = c == 0 ? 0f : ((float)rng.NextDouble() - 0.5f) * 0.22f;
                copy.transform.localPosition = new Vector3(ox, c * 0.05f, oz);
                BuildCopy(copy.transform, c);
            }
        }

        private void BuildCopy(Transform parent, int index)
        {
            Sprite icon = CleanIcon(_item.icon);
            if (icon == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cube.GetComponent<Collider>());
                cube.layer = InteractLayer;
                cube.transform.SetParent(parent, false);
                cube.transform.localScale = Vector3.one * 0.3f;
                cube.transform.localRotation = Quaternion.Euler(20f, 30f, 0f);
                return;
            }

            // 아이콘 해상도와 상관없이 월드에서 같은 크기(ItemScale 미터)로 보이게 맞춘다
            float fit = ItemScale / Mathf.Max(0.01f, Mathf.Max(icon.bounds.size.x, icon.bounds.size.y));

            // 얇은 두께를 흉내내는 겹 3장 (가운데만 밝게, 앞뒤는 살짝 어둡게) -> 옆에서 봐도 납작한 종이처럼 사라지지 않는다
            for (int s = 0; s < 3; s++)
            {
                var layer = new GameObject("Slice" + s);
                layer.layer = InteractLayer;
                layer.transform.SetParent(parent, false);
                layer.transform.localPosition = new Vector3(0f, 0f, (s - 1) * 0.022f);
                layer.transform.localScale = Vector3.one * fit;
                var sr = layer.AddComponent<SpriteRenderer>();
                sr.sprite = icon;
                sr.sortingOrder = 5 + index * 3 + s;
                float tint = s == 1 ? 1f : 0.62f;
                sr.color = new Color(tint, tint, tint, 1f);
            }
        }

        // ───────────────────────── 움직임 ─────────────────────────

        private static bool RayHit(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, dist, ~0, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                Collider col = Hits[i].collider;
                if (col == null || Hits[i].distance >= bd) continue;
                if (col.GetComponentInParent<CharacterController>() != null) continue;
                if (col.GetComponentInParent<PlayerInventory>() != null) continue;
                if (col.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                bd = Hits[i].distance;
                best = Hits[i];
                found = true;
            }
            return found;
        }

        private void Update()
        {
            if (_flying)
                StepFlight(Time.deltaTime);
        }

        private void StepFlight(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            _flightTime += dt;
            _vel.y -= Gravity * dt;

            Vector3 pos = transform.position;
            Vector3 next = pos + _vel * dt;

            // 벽에 부딪히면 튕겨 나온다
            Vector3 move = next - pos;
            float len = move.magnitude;
            if (len > 1e-4f && RayHit(pos, move / len, len + 0.18f, out RaycastHit wall) && Mathf.Abs(wall.normal.y) < 0.55f)
            {
                Vector3 n = wall.normal; n.y = 0f; n.Normalize();
                Vector3 h = Vector3.Reflect(new Vector3(_vel.x, 0f, _vel.z), n) * 0.35f;
                _vel = new Vector3(h.x, _vel.y, h.z);
                next = new Vector3(pos.x, next.y, pos.z);
            }

            // 바닥: 아래로 떨어질 때만 확인
            float top = Mathf.Max(pos.y, next.y) + 0.35f;
            if (RayHit(new Vector3(next.x, top, next.z), Vector3.down, 40f, out RaycastHit ground))
            {
                _groundY = ground.point.y;
                if (_vel.y <= 0f && next.y <= _groundY + RestHeight)
                {
                    next.y = _groundY + RestHeight;
                    if (_vel.y < -3.2f && _flightTime < 3f)
                    {
                        _vel.y = -_vel.y * 0.38f;           // 통통 튕긴다
                        _vel.x *= 0.62f; _vel.z *= 0.62f;
                    }
                    else
                    {
                        transform.position = next;
                        Land();
                        return;
                    }
                }
            }
            else if (next.y < pos.y - 0.01f && _flightTime > 4f)
            {
                // 땅이 없는 곳으로 떨어졌다: 아이템이 사라지지 않게 던진 자리로 되돌려 놓는다
                transform.position = _origin;
                _groundY = _origin.y - RestHeight;
                Land();
                return;
            }

            transform.position = next;
            if (_flightTime > 5f)
            {
                Land();
            }
        }

        private void Land()
        {
            _flying = false;
            _vel = Vector3.zero;
            _settleTime = Time.time;

            // 같은 아이템이 가까이 있으면 그쪽에 합쳐진다
            DroppedItem target = FindMergeTarget(transform.position, _item, this, MergeRadius);
            if (target != null && _durability == 0)
            {
                target.Absorb(_count);
                _picked = true;
                Destroy(gameObject);
                return;
            }

            // 다른 아이템 위에 겹쳐 떨어졌으면 비어 있는 쪽으로 살짝 옮긴다
            SpreadFromNeighbors();
        }

        private void SpreadFromNeighbors()
        {
            Vector3 pos = transform.position;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int n = Physics.OverlapSphereNonAlloc(pos, SpreadRadius, Overlaps, 1 << InteractLayer, QueryTriggerInteraction.Collide);
                Vector3 push = Vector3.zero;
                int crowd = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!Overlaps[i].TryGetComponent<DroppedItem>(out var o) || o == this || o._picked || o._flying) continue;
                    Vector3 d = pos - o.transform.position; d.y = 0f;
                    if (d.sqrMagnitude < 0.0004f) d = Random.insideUnitSphere;
                    d.y = 0f;
                    push += d.normalized * (SpreadRadius - Mathf.Min(SpreadRadius, d.magnitude) + 0.12f);
                    crowd++;
                }
                if (crowd == 0) break;

                Vector3 np = pos + push;
                if (RayHit(new Vector3(np.x, pos.y + 0.6f, np.z), Vector3.down, 3f, out RaycastHit g))
                    np.y = g.point.y + RestHeight;
                else
                    break;
                // 밀려나는 길에 벽이 있으면 포기
                Vector3 step = np - pos; step.y = 0f;
                if (step.sqrMagnitude > 1e-6f && RayHit(pos, step.normalized, step.magnitude + 0.1f, out RaycastHit w) && Mathf.Abs(w.normal.y) < 0.55f)
                    break;
                pos = np;
            }
            transform.position = pos;
            _groundY = pos.y - RestHeight;
        }

        private void LateUpdate()
        {
            if (_visual == null) return;

            float t = Time.time + _seed;
            float spinRate = _flying ? SpinSpeed * 5f : SpinSpeed;
            _spin += spinRate * Time.deltaTime;
            float bob = _flying ? 0f : Mathf.Sin(t * 2.4f) * 0.045f + 0.04f;

            if (_pop > 0f) _pop = Mathf.Max(0f, _pop - Time.deltaTime * 3.5f);
            float pop = 1f + Mathf.Sin(_pop * Mathf.PI) * 0.35f;
            _visual.localScale = Vector3.one * pop;
            _visual.localPosition = new Vector3(0f, bob, 0f);
            // 카메라를 정면으로 보는 방향을 기준으로 빙글 돈다. 정면/뒷면에서는 천천히, 옆면은 빠르게 지나가서 늘 그림이 잘 보인다.
            float baseYaw = 0f;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 d = transform.position - cam.transform.position;
                if (d.x * d.x + d.z * d.z > 1e-4f) baseYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            float th = (_spin + _seed * 36f) * Mathf.Deg2Rad;
            float phi = (th - 0.55f * Mathf.Sin(2f * th)) * Mathf.Rad2Deg;
            _visual.rotation = Quaternion.Euler(0f, baseYaw + phi, 0f);

            if (_shadow != null)
            {
                // 그림자는 항상 발밑 바닥에 붙고, 높이 떠 있을수록 작고 옅어진다
                float h = Mathf.Max(0f, transform.position.y - _groundY - RestHeight + bob);
                float k = Mathf.Clamp01(1f - h * 0.35f);
                _shadow.position = new Vector3(transform.position.x, _groundY + 0.025f, transform.position.z);
                _shadow.localScale = Vector3.one * (0.62f * (0.6f + 0.4f * k)) * (1f + (_copies - 1) * 0.12f);
            }
        }

        // ───────────────────────── 줍기 ─────────────────────────

        public void Interact(GameObject player)
        {
            if (_picked || _item == null) return;
            if (Time.time - _spawnTime < 0.6f) return;   // 버리자마자 눌린 E로 바로 주워지지 않게

            PlayerInventory inv = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inv == null) return;

            // 들어가는 만큼만 줍고 나머지는 남겨 둔다
            int take = Mathf.Min(inv.SpaceFor(_item), _count);
            if (take <= 0)
            {
                HudToast.Show("인벤토리가 가득 찼습니다.");
                return;
            }

            if (!inv.Inventory.AddItem(_item, take, _durability))
                return;

            _count -= take;
            if (_count <= 0)
            {
                _picked = true;
                Destroy(gameObject);
            }
            else
            {
                Build();   // 남은 개수에 맞춰 겹쳐진 모양을 줄인다
            }
        }
    }
}
