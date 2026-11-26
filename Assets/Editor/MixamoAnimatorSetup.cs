using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Mixamo 동작(Assets/LastTruck/Animations/Mixamo)으로 좀비/플레이어 애니메이터를 다시 짠다.
///
/// 좀비 (Zombie Animator.controller, 제자리에서 다시 만든다 → 프리팹 참조는 그대로)
///  - 대기 / 걷기 / 달리기 2종(RunVariant): Speed로 고르고, LocoMul로 재생 속도를 실제 이동 속도에 맞춘다(발 미끄러짐 방지).
///  - 공격: 좀비 공격(2.5초)을 AttackSpeed배로 돌린다.
///  - 피격 2종(HitVariant): 앞부분(반응)만 보여 주고 대기로 돌아간다.
///  - 넘어짐(Knockdown 트리거): 뒤로 넘어졌다가 일어난다.
///  - 사망 4종(DeathVariant).
///
/// 플레이어 (치비 캐릭터 컨트롤러 전부)
///  - doHit가 점프(Jumping Up)로 가던 것을 피격 반응(작게)으로, doHitBig은 크게 밀리는 반응으로.
/// </summary>
public static class MixamoAnimatorSetup
{
    private const string Root = "Assets/LastTruck/Animations/Mixamo/";
    private const string ZombieController = "Assets/LastTruck/Monster/Animation/Zombie Animator.controller";
    private const string PlayerControllerFolder = "Assets/LastTruck/PolyOne/Chibi Character/Animation/Controler";

    public const float AttackSpeed = 1.6f;      // 2.5초 → 1.56초, 타격 0.4 지점(1.0초 → 0.63초)
    public const float HitStunSeconds = 0.55f;  // 피격 경직 (클립 앞부분)
    private const float KnockFallSpeed = 1.35f;
    private const float GetUpSpeed = 1.25f;

    private static AnimationClip Clip(string file, string name = null)
    {
        string path = Root + file + ".fbx";
        name = name ?? System.IO.Path.GetFileNameWithoutExtension(file);
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
        if (clip == null) Debug.LogError("[MixamoAnimatorSetup] 클립 없음: " + path + " / " + name);
        return clip;
    }

    [MenuItem("Tools/Last Truck/Animation/Mixamo: 좀비 + 플레이어 애니메이터 다시 만들기")]
    private static void Menu()
    {
        var sb = new StringBuilder();
        BuildZombie(sb);
        SetupPlayers(sb);
        SetupPlayerLocomotion(sb);
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 플레이어 이동 모션: 달리기/걷기 상태에 RunAnimMul(재생 배율, NetworkPlayerMovement가 실제 속도로 맞춘다)을 걸고,
    /// 대기↔달리기 전환을 0.05초 → 0.15~0.2초로 늘려 툭툭 끊기지 않게 한다.
    /// </summary>
    public static void SetupPlayerLocomotion(StringBuilder sb)
    {
        string[] moveStates = { "Run", "Run Fast", "Walk" };
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { PlayerControllerFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null || ctrl.layers.Length == 0) continue;
            if (!ctrl.parameters.Any(p => p.name == "RunAnimMul"))
                ctrl.AddParameter(new AnimatorControllerParameter { name = "RunAnimMul", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            int changed = 0;
            foreach (ChildAnimatorState cs in ctrl.layers[0].stateMachine.states)
            {
                AnimatorState s = cs.state;
                bool move = moveStates.Contains(s.name);
                if (move)
                {
                    s.speedParameterActive = true;
                    s.speedParameter = "RunAnimMul";
                }
                foreach (AnimatorStateTransition t in s.transitions)
                {
                    if (t.destinationState == null) continue;
                    string to = t.destinationState.name;
                    bool toMove = moveStates.Contains(to);
                    if (s.name == "Idle" && toMove) { t.duration = 0.15f; changed++; }
                    else if (move && to == "Idle") { t.duration = 0.2f; changed++; }
                    else if (move && toMove) { t.duration = 0.2f; changed++; }
                }
            }
            EditorUtility.SetDirty(ctrl);
            sb.AppendLine("플레이어 이동 모션: " + System.IO.Path.GetFileName(path) + " (전환 " + changed + "개)");
        }
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ 좀비

    public static void BuildZombie(StringBuilder sb)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ZombieController);
        if (ctrl == null) { sb.AppendLine("좀비 컨트롤러 없음"); return; }

        // 걷기/조깅: 다친 사람 동작(절뚝이며 걷기 1.3m/s, 절뚝이며 뛰기 2.3m/s)이 느린 좀비에 잘 맞는다
        // (좀비 달리기 3.4m/s를 0.6배로 돌리면 슬로 모션처럼 보였다)
        AnimationClip idle = Clip("Zombie/Zombie_Idle"), walk = Clip("Zombie/Zombie_Limp_Walk"), jog = Clip("Zombie/Zombie_Limp_Run");
        AnimationClip runA = Clip("Zombie/Zombie_Run"), runB = Clip("Zombie/Zombie_Running");
        AnimationClip attack = Clip("Zombie/Zombie_Attack");
        AnimationClip hitA = Clip("Zombie/Zombie_Hit_A"), hitB = Clip("Zombie/Zombie_Hit_B");
        AnimationClip knock = Clip("Zombie/Falling_Back_Death", "Knockdown_Fall"), getUp = Clip("Zombie/Getting_Up_C");
        AnimationClip[] deaths =
        {
            Clip("Zombie/Zombie_Dying"), Clip("Zombie/Zombie_Death"), Clip("Zombie/Falling_Back_Death"), Clip("Zombie/Zombie_Dying_B"),
        };

        // 파라미터
        foreach (AnimatorControllerParameter p in ctrl.parameters.ToArray()) ctrl.RemoveParameter(p);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Knockdown", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "LocoMul", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        ctrl.AddParameter("RunVariant", AnimatorControllerParameterType.Int);
        ctrl.AddParameter("HitVariant", AnimatorControllerParameterType.Int);
        ctrl.AddParameter("DeathVariant", AnimatorControllerParameterType.Int);
        // 다 같이 똑같이 움직이지 않게 (NetworkMonster가 몬스터마다 다르게 정한다):
        //  Mirror = 좌우 반전(절뚝이는 다리/휘두르는 팔이 바뀐다), CycleOffset = 걸음 박자 어긋나게, IdleVariant = 대기 동작 4종
        ctrl.AddParameter("Mirror", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("CycleOffset", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IdleVariant", AnimatorControllerParameterType.Float);

        // 상태 머신 비우기
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states.ToArray()) sm.RemoveState(s.state);
        foreach (ChildAnimatorStateMachine m in sm.stateMachines.ToArray()) sm.RemoveStateMachine(m.stateMachine);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);

        AnimatorState Loco(string name, Motion m, Vector3 pos)
        {
            AnimatorState s = sm.AddState(name, pos);
            s.motion = m;
            s.speedParameterActive = true;
            s.speedParameter = "LocoMul";
            s.mirrorParameterActive = true;
            s.mirrorParameter = "Mirror";
            s.cycleOffsetParameterActive = true;
            s.cycleOffsetParameter = "CycleOffset";
            s.writeDefaultValues = true;
            return s;
        }
        AnimatorState Once(string name, Motion m, float speed, Vector3 pos)
        {
            AnimatorState s = sm.AddState(name, pos);
            s.motion = m;
            s.speed = speed;
            s.mirrorParameterActive = true;
            s.mirrorParameter = "Mirror";
            s.writeDefaultValues = true;
            return s;
        }

        // 대기 동작 4종: 좀비 대기 + 다친 사람 대기 3종(서 있기/비틀거리기/아파하기). IdleVariant(0~3 정수)로 하나만 고른다.
        foreach (BlendTree old in AssetDatabase.LoadAllAssetsAtPath(ZombieController).OfType<BlendTree>().ToArray())
            Object.DestroyImmediate(old, true);
        var idleTree = new BlendTree { name = "IdleVariants", blendParameter = "IdleVariant", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(idleTree, ctrl);
        AnimationClip[] idles = { idle, Clip("Zombie/Zombie_Idle_B"), Clip("Zombie/Zombie_Idle_C"), Clip("Zombie/Zombie_Idle_D") };
        for (int i = 0; i < idles.Length; i++) if (idles[i] != null) idleTree.AddChild(idles[i], i);
        AnimatorStateTransition To(AnimatorState from, AnimatorState to, float dur, float exitTime = -1f)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = exitTime >= 0f;
            if (exitTime >= 0f) t.exitTime = exitTime;
            t.duration = dur;
            t.hasFixedDuration = true;
            return t;
        }
        AnimatorStateTransition Any(AnimatorState to, float dur)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.duration = dur;
            t.hasFixedDuration = true;
            t.canTransitionToSelf = false;
            return t;
        }

        AnimatorState sIdle = Loco("Idle", idleTree, new Vector3(300, 0));
        AnimatorState sWalk = Loco("Walk", walk, new Vector3(300, 100));
        AnimatorState sJog = Loco("Jog", jog, new Vector3(300, 200));
        AnimatorState sRunA = Loco("RunA", runA, new Vector3(550, 50));
        AnimatorState sRunB = Loco("RunB", runB, new Vector3(550, 150));
        sm.defaultState = sIdle;

        // 이동 속도(m/s)로 고르기: 대기 < 0.15 < 절뚝 걷기 < 1.6 < 절뚝 뛰기 < 2.8 < 좀비 달리기 (경계에서 깜빡이지 않게 내려갈 때는 조금 낮춰서)
        // NetworkMonster가 Speed = m/s, LocoMul = 실제 속도 / 지금 상태 클립의 속도
        const float walkMin = 0.15f, jogMin = 1.6f, runMin = 2.8f;
        To(sIdle, sWalk, 0.2f).AddCondition(AnimatorConditionMode.Greater, walkMin, "Speed");
        To(sIdle, sJog, 0.2f).AddCondition(AnimatorConditionMode.Greater, jogMin, "Speed");
        To(sWalk, sIdle, 0.25f).AddCondition(AnimatorConditionMode.Less, walkMin * 0.6f, "Speed");
        To(sWalk, sJog, 0.25f).AddCondition(AnimatorConditionMode.Greater, jogMin, "Speed");
        To(sJog, sWalk, 0.25f).AddCondition(AnimatorConditionMode.Less, jogMin - 0.25f, "Speed");
        To(sJog, sIdle, 0.25f).AddCondition(AnimatorConditionMode.Less, walkMin * 0.6f, "Speed");
        foreach (var (state, variant) in new[] { (sRunA, 0), (sRunB, 1) })
        {
            foreach (AnimatorState from in new[] { sIdle, sWalk, sJog })
            {
                AnimatorStateTransition t = To(from, state, 0.2f);
                t.AddCondition(AnimatorConditionMode.Greater, runMin, "Speed");
                t.AddCondition(AnimatorConditionMode.Equals, variant, "RunVariant");
            }
            To(state, sJog, 0.25f).AddCondition(AnimatorConditionMode.Less, runMin - 0.35f, "Speed");
            To(state, sIdle, 0.25f).AddCondition(AnimatorConditionMode.Less, walkMin * 0.6f, "Speed");
        }

        // 공격
        AnimatorState sAttack = Once("Attack", attack, AttackSpeed, new Vector3(50, -120));
        To(sAttack, sIdle, 0.2f, 0.92f);
        AnimatorStateTransition ta = Any(sAttack, 0.1f);
        ta.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        ta.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");

        // 피격: 클립 앞부분(경직 시간)만 보이고 부드럽게 돌아간다
        foreach (var (clip, variant) in new[] { (hitA, 0), (hitB, 1) })
        {
            AnimatorState s = Once("Hit" + (variant == 0 ? "A" : "B"), clip, 1.15f, new Vector3(50, -40 + variant * 60));
            float exit = clip != null ? Mathf.Clamp01(HitStunSeconds * 1.15f / clip.length) : 0.3f;
            To(s, sIdle, 0.3f, exit);
            AnimatorStateTransition t = Any(s, 0.06f);
            t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            t.AddCondition(AnimatorConditionMode.Equals, variant, "HitVariant");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        }

        // 넘어졌다 일어나기
        AnimatorState sKnock = Once("Knockdown", knock, KnockFallSpeed, new Vector3(50, 120));
        AnimatorState sGetUp = Once("GetUp", getUp, GetUpSpeed, new Vector3(50, 200));
        To(sKnock, sGetUp, 0.4f, 0.97f);
        To(sGetUp, sIdle, 0.3f, 0.93f);
        AnimatorStateTransition tk = Any(sKnock, 0.08f);
        tk.AddCondition(AnimatorConditionMode.If, 0, "Knockdown");
        tk.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");

        // 사망
        for (int i = 0; i < deaths.Length; i++)
        {
            AnimatorState s = Once("Death" + i, deaths[i], 1f, new Vector3(800, -100 + i * 70));
            AnimatorStateTransition t = Any(s, 0.12f);
            t.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            t.AddCondition(AnimatorConditionMode.Equals, i, "DeathVariant");
        }

        EditorUtility.SetDirty(ctrl);

        // 이 컨트롤러를 쓰는 몬스터 프리팹의 클립/타이밍
        int prefabs = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LastTruck" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Animator an = root.GetComponentInChildren<Animator>(true);
                MonsterAnimationSet set = root.GetComponent<MonsterAnimationSet>();
                if (an == null || set == null || an.runtimeAnimatorController != ctrl) continue;
                set.attackClip = attack;
                set.getHitClip = hitA;
                set.deathClip = deaths.OrderByDescending(d => d != null ? d.length : 0f).First();
                set.attackHitTiming = 0.4f;
                set.attackPlaybackSpeed = AttackSpeed;
                set.hitStunSeconds = HitStunSeconds;
                an.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                prefabs++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        sb.AppendLine("좀비 애니메이터: 상태 " + sm.states.Length + "개, 프리팹 " + prefabs + "개 갱신");
    }

    // ------------------------------------------------------------------ 플레이어

    public static void SetupPlayers(StringBuilder sb)
    {
        AnimationClip small = Clip("Reactions/React_Small_Head");
        AnimationClip large = Clip("Reactions/React_Large_Front");
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { PlayerControllerFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null || ctrl.layers.Length == 0) continue;
            if (!ctrl.parameters.Any(p => p.name == "doHitBig")) ctrl.AddParameter("doHitBig", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            AnimatorState idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle") ?? sm.defaultState;
            AnimatorState run = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Run");
            bool hasIsRun = ctrl.parameters.Any(p => p.name == "isRun");

            // 예전 연결(doHit → 점프)과 이전에 만든 피격 상태를 지운다
            foreach (AnimatorStateTransition t in sm.anyStateTransitions.ToArray())
                if (t.conditions.Any(c => c.parameter == "doHit" || c.parameter == "doHitBig")) sm.RemoveAnyStateTransition(t);
            foreach (ChildAnimatorState s in sm.states.ToArray())
                if (s.state.name == "HitSmall" || s.state.name == "HitLarge") sm.RemoveState(s.state);

            AnimatorState Hit(string name, AnimationClip clip, string trigger, float speed, float exitSeconds, Vector3 pos)
            {
                AnimatorState s = sm.AddState(name, pos);
                s.motion = clip;
                s.speed = speed;
                s.writeDefaultValues = true;
                float exit = clip != null ? Mathf.Clamp01(exitSeconds * speed / clip.length) : 0.8f;
                // 피격이 끝날 때 이미 움직이고 있으면 대기를 거치지 않고 바로 달리기로 (다리가 잠깐 서 있지 않게)
                if (run != null && hasIsRun)
                {
                    AnimatorStateTransition toRun = s.AddTransition(run);
                    toRun.hasExitTime = true;
                    toRun.exitTime = exit;
                    toRun.duration = 0.15f;
                    toRun.hasFixedDuration = true;
                    toRun.AddCondition(AnimatorConditionMode.If, 0, "isRun");
                }
                AnimatorStateTransition back = s.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = exit;
                back.duration = 0.2f;
                back.hasFixedDuration = true;
                AnimatorStateTransition any = sm.AddAnyStateTransition(s);
                any.duration = 0.06f;
                any.hasFixedDuration = true;
                any.canTransitionToSelf = true;
                any.AddCondition(AnimatorConditionMode.If, 0, trigger);
                return s;
            }
            // 경직(HitReaction 0.6초)에 맞춰 끝낸다
            Hit("HitSmall", small, "doHit", 1.1f, 0.55f, new Vector3(-250, 250));
            Hit("HitLarge", large, "doHitBig", 1.15f, 0.7f, new Vector3(-250, 320));

            EditorUtility.SetDirty(ctrl);
            sb.AppendLine("플레이어: " + System.IO.Path.GetFileName(path));
        }
        AssetDatabase.SaveAssets();
    }
}
