using System.Collections.Generic;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 카메라와 내 캐릭터 사이를 가리는 몬스터(또는 카메라 바로 앞에 붙은 몬스터)를 점 무늬로 흐리게 한다.
    ///  - 캐릭터 셰이더(LastTruck/StylizedCharacter)의 _DitherFade를 바꾼다 (반투명이 아니라 정렬 문제가 없다).
    ///  - 그림자는 그대로 남아서 "거기 있다"는 건 알 수 있다.
    ///  - 보이기만 하는 연출이라 각자 화면에서 따로 계산한다.
    /// </summary>
    public class CameraOccluderFade : MonoBehaviour
    {
        private const float MaxFade = 0.72f;            // 다 지우지 않고 윤곽은 남긴다
        private const float NearCameraDistance = 4.5f;  // 카메라에서 이만큼 안이면 무조건 흐리게
        private const float LineRadius = 1.1f;          // 카메라→캐릭터 선에서 이만큼 안이면 가린다
        private const float FadeSpeed = 5f;             // 초당 변화량
        private const float RescanInterval = 0.4f;

        private static readonly int DitherFadeId = Shader.PropertyToID("_DitherFade");

        private class Entry
        {
            public Transform root;
            public Renderer[] renderers;
            public Material[] materials;
            public float fade;
        }

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<int> _remove = new List<int>();
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("CameraOccluderFade");
            DontDestroyOnLoad(go);
            go.AddComponent<CameraOccluderFade>();
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            Networking.NetworkPlayer me = Networking.NetworkPlayer.Local;
            if (cam == null || me == null)
            {
                if (_entries.Count > 0) ClearAll();
                return;
            }

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + RescanInterval;
                Rescan();
            }

            Vector3 camPos = cam.transform.position;
            Vector3 target = me.transform.position + Vector3.up * 0.9f;
            Vector3 line = target - camPos;
            float lineLen = line.magnitude;
            Vector3 lineDir = lineLen > 0.001f ? line / lineLen : cam.transform.forward;
            float dt = Time.unscaledDeltaTime;

            _remove.Clear();
            foreach (KeyValuePair<int, Entry> kv in _entries)
            {
                Entry e = kv.Value;
                if (e.root == null) { _remove.Add(kv.Key); continue; }

                Vector3 center = e.root.position + Vector3.up * 1.2f;
                bool occluding = (center - camPos).sqrMagnitude < NearCameraDistance * NearCameraDistance;
                if (!occluding)
                {
                    // 카메라와 캐릭터 사이(캐릭터보다 카메라에 가까운 쪽)에서 선에 가까우면
                    float along = Vector3.Dot(center - camPos, lineDir);
                    if (along > 0f && along < lineLen - 0.6f)
                    {
                        Vector3 closest = camPos + lineDir * along;
                        Vector3 off = center - closest;
                        off.y *= 0.5f;   // 키가 커서 위아래로 넓게 가리므로 높이 차이는 덜 본다
                        occluding = off.sqrMagnitude < LineRadius * LineRadius;
                    }
                }

                float goal = occluding ? MaxFade : 0f;
                if (Mathf.Approximately(e.fade, goal)) continue;
                e.fade = Mathf.MoveTowards(e.fade, goal, FadeSpeed * dt);
                Apply(e);
            }
            foreach (int id in _remove) _entries.Remove(id);
        }

        /// <summary>지금 있는 몬스터 목록을 다시 모은다 (새로 생긴 것 추가, 사라진 것은 LateUpdate에서 뺀다).</summary>
        private void Rescan()
        {
            foreach (Networking.NetworkMonster m in FindObjectsByType<Networking.NetworkMonster>(FindObjectsSortMode.None))
            {
                if (m == null) continue;
                int id = m.GetInstanceID();
                if (_entries.ContainsKey(id)) continue;

                var renderers = new List<Renderer>();
                var materials = new List<Material>();
                foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    foreach (Material mat in r.materials)   // 몸 색/피격 색 때문에 이미 몬스터별 재질이다
                    {
                        if (mat != null && mat.HasProperty(DitherFadeId)) { materials.Add(mat); }
                    }
                    renderers.Add(r);
                }
                if (materials.Count == 0) continue;
                _entries[id] = new Entry { root = m.transform, renderers = renderers.ToArray(), materials = materials.ToArray(), fade = 0f };
            }
        }

        private static void Apply(Entry e)
        {
            foreach (Material mat in e.materials)
                if (mat != null) mat.SetFloat(DitherFadeId, e.fade);
        }

        private void ClearAll()
        {
            foreach (Entry e in _entries.Values)
            {
                if (e.root == null) continue;
                e.fade = 0f;
                Apply(e);
            }
            _entries.Clear();
        }
    }
}
