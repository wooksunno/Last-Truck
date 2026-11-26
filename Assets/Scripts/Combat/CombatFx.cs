using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 전투 이펙트 도우미. Synty "POLYGON - Particle FX" 프리팹을 Resources/CombatFx 에서 불러 쓴다
    /// (수류탄 폭발/연기, 독가스, 불길, 화염방사 ...). 씬이나 프리팹에 따로 연결하지 않아도 어디서나 쓸 수 있다.
    /// </summary>
    public static class CombatFx
    {
        private const string Dir = "CombatFx/";
        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        public static GameObject Load(string name)
        {
            if (Cache.TryGetValue(name, out GameObject cached) && cached != null) return cached;
            GameObject prefab = Resources.Load<GameObject>(Dir + name);
            Cache[name] = prefab;
            return prefab;
        }

        /// <summary>이펙트를 만든다. life초 뒤에 지운다 (0이하면 안 지움). 프리팹이 없으면 null.</summary>
        public static GameObject Spawn(string name, Vector3 position, Quaternion rotation, float scale = 1f, float life = 6f, Transform parent = null)
        {
            GameObject prefab = Load(name);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, position, rotation, parent);
            go.name = name;
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = prefab.transform.localScale * scale;
            if (life > 0f) Object.Destroy(go, life);
            return go;
        }

        /// <summary>모든 파티클의 색을 바꾼다 (연기를 초록 독가스로 등). 알파는 유지한다.</summary>
        public static void Tint(GameObject go, Color color)
        {
            if (go == null) return;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule m = ps.main;
                m.startColor = new Color(color.r, color.g, color.b, color.a);
                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                if (col.enabled)
                {
                    // 수명 색 변화는 색은 버리고 투명도만 남긴다 (서서히 사라지는 느낌 유지)
                    var g = new Gradient();
                    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                              new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
                    col.color = g;
                }
            }
        }

        /// <summary>새로 뿜는 것만 멈춘다 (남은 연기/불꽃은 자연스럽게 사라진 뒤 지운다).</summary>
        public static void StopEmitting(GameObject go, float destroyAfter = 6f)
        {
            if (go == null) return;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(go, destroyAfter);
        }
    }
}
