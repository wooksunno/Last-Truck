using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LastTruck.EditorTools
{
    /// <summary>
    /// 좀비가 "물어뜯는" 공격 애니메이션(Zombie_Bite.anim)을 만든다.
    ///
    /// Synty 좀비에는 애니메이션이 없고 KayKit 휴머노이드 클립에도 물어뜯기가 없어서,
    /// 대기 모션(Idle_A, 전신)을 바탕으로 상체 근육 값만 덧그려서 만든다:
    ///   ① 움츠리며 뒤로 젖힘 → ② 앞으로 덮치며 머리를 숙이고 팔로 붙잡음 → ③ 머리를 좌우로 흔들며 물어뜯음 → ④ 원래 자세
    /// 수치(부호/크기/타이밍)는 아래 Keys 테이블에서 고치고, 메뉴로 다시 만들면 된다.
    /// 메뉴: LastTruck/Monsters/좀비 물어뜯기 애니메이션 다시 만들기
    /// </summary>
    public static class ZombieBiteClipBuilder
    {
        public const string BasePath = "Assets/KayKit/Characters/Animations/Animations/Rig_Medium/General/Idle_A.anim";
        public const string OutPath = "Assets/LastTruck/Monster/Animation/Zombie_Bite.anim";
        public const string ControllerPath = "Assets/LastTruck/Monster/Animation/Zombie Animator.controller";
        public const string PrefabPath = "Assets/LastTruck/Monster/Prefab/Zombie.prefab";

        public const float Length = 1.3f;
        /// <summary>피해가 들어가는 순간(덮치는 순간)의 클립 비율.</summary>
        public const float HitTiming = 0.42f;

        // 근육 이름 → (시간, 대기 모션에 더할 값) 키 목록. 값은 -1~1 근육 범위.
        // Spine/Chest Front-Back: 음수 = 앞으로 숙임 / Neck·Head Nod Down-Up: 음수 = 아래 / Arm Down-Up: 양수 = 위
        private static Dictionary<string, float[]> Keys()
        {
            // 시간: 0, 0.25(젖힘), 0.50(덮침), 0.62, 0.74, 0.86, 0.98(물어뜯는 중), 1.12, 1.3(복귀)
            return new Dictionary<string, float[]>
            {
                // 시간 축은 아래 Times와 같은 순서
                { "Spine Front-Back",          new[] { 0f,  0.30f, -0.75f, -0.70f, -0.75f, -0.70f, -0.75f, -0.30f, 0f } },
                { "Chest Front-Back",          new[] { 0f,  0.25f, -0.60f, -0.55f, -0.60f, -0.55f, -0.60f, -0.25f, 0f } },
                { "UpperChest Front-Back",     new[] { 0f,  0.20f, -0.40f, -0.35f, -0.40f, -0.35f, -0.40f, -0.15f, 0f } },
                { "Neck Nod Down-Up",          new[] { 0f,  0.45f, -0.85f, -0.55f, -0.90f, -0.55f, -0.90f, -0.30f, 0f } },
                { "Head Nod Down-Up",          new[] { 0f,  0.35f, -0.70f, -0.40f, -0.75f, -0.40f, -0.75f, -0.25f, 0f } },
                { "Head Turn Left-Right",      new[] { 0f,  0f,     0f,     0.85f, -0.85f,  0.85f, -0.85f,  0f,    0f } },
                { "Neck Turn Left-Right",      new[] { 0f,  0f,     0f,     0.50f, -0.50f,  0.50f, -0.50f,  0f,    0f } },
                { "Left Arm Down-Up",          new[] { 0f,  0.35f,  0.55f,  0.50f,  0.55f,  0.50f,  0.55f,  0.2f,  0f } },
                { "Right Arm Down-Up",         new[] { 0f,  0.35f,  0.55f,  0.50f,  0.55f,  0.50f,  0.55f,  0.2f,  0f } },
                { "Left Arm Front-Back",       new[] { 0f,  0.20f, -0.50f, -0.45f, -0.50f, -0.45f, -0.50f, -0.2f,  0f } },
                { "Right Arm Front-Back",      new[] { 0f,  0.20f, -0.50f, -0.45f, -0.50f, -0.45f, -0.50f, -0.2f,  0f } },
            };
        }

        private static readonly float[] Times = { 0f, 0.25f, 0.50f, 0.62f, 0.74f, 0.86f, 0.98f, 1.12f, 1.3f };

        [MenuItem("LastTruck/Monsters/좀비 물어뜯기 애니메이션 다시 만들기")]
        public static void BuildMenu()
        {
            Build();
            EditorUtility.DisplayDialog("좀비", "Zombie_Bite.anim을 다시 만들고 Zombie Animator / Zombie 프리팹에 연결했습니다.", "확인");
        }

        public static AnimationClip Build()
        {
            AnimationClip baseClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BasePath);
            if (baseClip == null)
            {
                Debug.LogError("[ZombieBite] 바탕 클립을 찾을 수 없습니다: " + BasePath);
                return null;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutPath);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip { name = "Zombie_Bite" };
            else clip.ClearCurves();

            Dictionary<string, float[]> deltas = Keys();
            const float step = 1f / 30f;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(baseClip))
            {
                AnimationCurve src = AnimationUtility.GetEditorCurve(baseClip, binding);
                if (src == null) continue;

                AnimationCurve dst = new AnimationCurve();
                float[] delta;
                bool hasDelta = binding.type == typeof(Animator) && deltas.TryGetValue(binding.propertyName, out delta) && delta != null;
                delta = hasDelta ? deltas[binding.propertyName] : null;

                for (float t = 0f; t <= Length + 0.0001f; t += step)
                {
                    float v = src.Evaluate(Mathf.Repeat(t, Mathf.Max(0.01f, baseClip.length)));
                    if (hasDelta) v = Mathf.Clamp(v + SampleDelta(delta, t), -1f, 1f);
                    dst.AddKey(new Keyframe(t, v));
                }
                for (int i = 0; i < dst.length; i++) dst.SmoothTangents(i, 0f);
                AnimationUtility.SetEditorCurve(clip, binding, dst);
            }

            // 대기 모션에 없는 근육(예: Head Turn)이 있으면 새로 만든다
            foreach (KeyValuePair<string, float[]> kv in deltas)
            {
                bool exists = false;
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
                    if (b.type == typeof(Animator) && b.propertyName == kv.Key) { exists = true; break; }
                if (exists) continue;

                AnimationCurve dst = new AnimationCurve();
                for (float t = 0f; t <= Length + 0.0001f; t += step) dst.AddKey(new Keyframe(t, Mathf.Clamp(SampleDelta(kv.Value, t), -1f, 1f)));
                for (int i = 0; i < dst.length; i++) dst.SmoothTangents(i, 0f);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key), dst);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.stopTime = Length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.frameRate = 30f;

            if (isNew) AssetDatabase.CreateAsset(clip, OutPath);
            EditorUtility.SetDirty(clip);

            Wire(clip);
            AssetDatabase.SaveAssets();
            return clip;
        }

        private static float SampleDelta(float[] values, float t)
        {
            AnimationCurve c = new AnimationCurve();
            for (int i = 0; i < Times.Length && i < values.Length; i++) c.AddKey(new Keyframe(Times[i], values[i]));
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0.5f);
            return c.Evaluate(t);
        }

        /// <summary>Zombie Animator의 Attack 상태와 Zombie 프리팹의 MonsterAnimationSet에 연결한다.</summary>
        private static void Wire(AnimationClip clip)
        {
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl != null)
            {
                foreach (ChildAnimatorState cs in ctrl.layers[0].stateMachine.states)
                {
                    if (cs.state.name == "Attack") cs.state.motion = clip;
                }
                EditorUtility.SetDirty(ctrl);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                MonsterAnimationSet set = root.GetComponent<MonsterAnimationSet>();
                if (set != null)
                {
                    set.attackClip = clip;
                    set.attackHitTiming = HitTiming;
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
