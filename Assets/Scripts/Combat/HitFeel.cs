using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 전투 손맛 연출 (보이기만 하는 것 - 피해/위치 판정과는 상관없다).
    ///  - HitStop: 때린 사람과 맞은 대상의 애니메이션을 아주 잠깐(0.05~0.1초) 멈춰 "탁" 걸리는 느낌을 준다.
    ///    Time.timeScale은 건드리지 않는다 (멀티플레이 시뮬레이션/다른 사람 화면에 영향이 없게 애니메이터만 멈춘다).
    ///  - Punch: 맞은 몸이 순간 납작해졌다가 튕겨 돌아온다 (모든 화면, Damageable 피격 연출에서 부른다).
    ///  - KillBurst: 쓰러질 때 먼지/살점 같은 덩어리가 터진다 (모든 화면).
    /// </summary>
    public class HitFeel : MonoBehaviour
    {
        private static HitFeel _instance;

        private static HitFeel Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("HitFeel");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HitFeel>();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _frozen.Clear(); _punching.Clear(); }

        // ───────────────────────── 히트스톱 ─────────────────────────

        private static readonly Dictionary<Animator, float> _frozen = new Dictionary<Animator, float>();   // 애니메이터 → 풀릴 시각(실시간)

        /// <summary>a(때린 사람)와 b(맞은 대상)의 애니메이션을 seconds초 멈춘다. 이미 멈춰 있으면 더 긴 쪽으로.</summary>
        public static void HitStop(GameObject a, GameObject b, float seconds)
        {
            if (seconds <= 0f) return;
            Freeze(a, seconds);
            Freeze(b, seconds);
            Instance.enabled = true;
        }

        private static void Freeze(GameObject go, float seconds)
        {
            if (go == null) return;
            float until = Time.unscaledTime + seconds;
            foreach (Animator anim in go.GetComponentsInChildren<Animator>())
            {
                if (anim == null) continue;
                if (_frozen.TryGetValue(anim, out float cur)) { _frozen[anim] = Mathf.Max(cur, until); continue; }
                if (!anim.enabled) continue;   // 원래 꺼져 있던 애니메이터는 건드리지 않는다
                anim.enabled = false;          // 끄면 지금 자세 그대로 멈춘다
                _frozen[anim] = until;
            }
        }

        private static readonly List<Animator> _release = new List<Animator>();

        private void Update()
        {
            if (_frozen.Count == 0) return;
            float now = Time.unscaledTime;
            _release.Clear();
            foreach (var kv in _frozen)
                if (kv.Key == null || now >= kv.Value) _release.Add(kv.Key);
            foreach (Animator anim in _release)
            {
                _frozen.Remove(anim);
                if (anim != null) anim.enabled = true;
            }
        }

        private void OnDisable()
        {
            // 혹시 꺼지면 멈춘 애니메이터를 모두 풀어 준다 (영원히 굳어 있지 않게)
            foreach (var kv in _frozen) if (kv.Key != null) kv.Key.enabled = true;
            _frozen.Clear();
        }

        // ───────────────────────── 맞은 몸 튕김 ─────────────────────────

        private static readonly HashSet<Transform> _punching = new HashSet<Transform>();

        /// <summary>맞은 대상의 몸(애니메이터가 붙은 모델)을 순간 납작하게 했다가 튕겨 돌린다. strength 0~1.</summary>
        public static void Punch(GameObject target, float strength)
        {
            if (target == null) return;
            Animator anim = target.GetComponentInChildren<Animator>();
            Transform body = anim != null && anim.transform != target.transform ? anim.transform : null;
            if (body == null || _punching.Contains(body)) return;   // 루트를 건드리면 크기(중간보스)와 겹치므로 모델만
            Instance.StartCoroutine(PunchRoutine(body, Mathf.Clamp01(strength)));
        }

        private static IEnumerator PunchRoutine(Transform body, float strength)
        {
            _punching.Add(body);
            Vector3 baseScale = body.localScale;
            const float duration = 0.22f;
            float t = 0f;
            while (t < duration && body != null)
            {
                t += Time.deltaTime;
                float k = t / duration;
                // 처음엔 옆으로 퍼지며 납작 → 위로 살짝 늘어남 → 원래대로 (감쇠 진동)
                float wobble = Mathf.Sin(k * Mathf.PI * 2.2f) * (1f - k) * (0.16f + 0.12f * strength);
                body.localScale = new Vector3(baseScale.x * (1f + wobble), baseScale.y * (1f - wobble), baseScale.z * (1f + wobble));
                yield return null;
            }
            if (body != null) body.localScale = baseScale;
            _punching.Remove(body);
        }

        // ───────────────────────── 쓰러질 때 터지는 연출 ─────────────────────────

        private static Material _burstMaterial;

        /// <summary>몬스터가 쓰러질 때 덩어리와 먼지가 터진다. size = 몸집(중간보스면 크게).</summary>
        public static void KillBurst(Vector3 position, float size)
        {
            if (_burstMaterial == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh == null) return;
                _burstMaterial = new Material(sh);
            }

            size = Mathf.Clamp(size, 0.6f, 3f);
            var go = new GameObject("KillBurst");
            go.transform.position = position + Vector3.up * 0.9f * size;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f * size, 7.5f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * size, 0.32f * size);
            main.gravityModifier = 1.6f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.55f, 0.25f), new Color(0.25f, 0.18f, 0.22f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(26 * size), (short)(36 * size)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f * size;

            var sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _burstMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            Destroy(go, 2f);

            // 바닥에 퍼지는 먼지 고리
            var dust = Combat.CombatFx.Spawn("FX_Impact_Stone_01", position + Vector3.up * 0.1f, Quaternion.identity, 0.8f * size, 3f);
            if (dust != null) Combat.CombatFx.Tint(dust, new Color(0.55f, 0.5f, 0.42f));
        }
    }
}
