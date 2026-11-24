using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 동굴 잠복 좀비 (호스트만). 낮에도 동굴 안에 좀비가 미리 깔려 있어서, 처음 들어갔을 때 깜짝 놀라게 한다.
    ///  - 보물상자(동굴 안에 있다) 주변에 걷는 좀비 2마리씩, 동굴 통로 곳곳에 "뛰는 좀비"(플레이어 기본 이동보다 조금 빠름).
    ///  - 맵 곳곳의 작은 동굴(굴 4곳 + 숲 동굴 구석) 안에도 걷는 좀비를 깔고, 굴마다 하나는 뛰는 좀비.
    ///  - 제자리에 있다가 가까이(14m) 오면 쫓아오고, 동굴에서 너무 멀리 끌려 나오면 돌아간다 (NetworkMonster 잠복 모드).
    ///  - 트럭을 노리지 않고, 아침 정리(MonsterSpawner.ClearAllMonsters)에도 지워지지 않는다.
    ///  - 매일 아침, 죽은 자리만큼 다시 채운다 (동굴은 늘 위험하다).
    /// </summary>
    public class CaveLurkers : MonoBehaviour
    {
        private const float RunnerSpeed = 6.6f;   // 플레이어 기본 이동(6) 보다 조금 빠르고 Shift 달리기(9.6)보다는 느림 → 달리면 도망칠 수 있다
        private const int WalkersPerChest = 2;
        private const int MaxLurkers = 36;

        // 동굴 통로 (숲 광산) - 뛰는 좀비 자리
        private static readonly Vector3[] TunnelSpots =
        {
            new Vector3(-358f, 2f, 260f), new Vector3(-358f, 2f, 330f), new Vector3(-358f, 2f, 440f), new Vector3(-358f, 2f, 520f),
        };

        // 맵 곳곳의 작은 동굴(04_Cave/Cave의 PP_Cave_Tube_*, ForestCaveNook) 안쪽 - 걷는 좀비 자리.
        // 천장이 12m 안에 있고(=굴 안), 트럭까지 걸어서 이어지는 내비메시 칸만 골라 서로 8m 이상 떨어지게 뽑았다.
        // (Tube_03, A_DeepPit은 바깥과 길이 이어지지 않아 뺐다)
        private static readonly Vector3[] CaveSpots =
        {
            // 동쪽 굴 (Tube_02)
            new Vector3(219.0f, -14.9f, 47.2f), new Vector3(199.0f, -10.8f, 72.2f), new Vector3(229.0f, -11.5f, 27.2f), new Vector3(209.0f, -12.8f, 59.7f),
            // 동쪽 굴 (Tube_06)
            new Vector3(204.4f, -7.9f, -45.5f), new Vector3(176.5f, -9.4f, -35.3f), new Vector3(199.2f, -9.0f, -20.1f), new Vector3(226.7f, -7.7f, -35.1f),
            // 남쪽 굴 (Tube_04 / 01 / 11, 서로 이어져 있다)
            new Vector3(-30.1f, -7.8f, -178.9f), new Vector3(-7.2f, -7.3f, -193.7f), new Vector3(-35.5f, -6.9f, -155.5f),
            new Vector3(-29.7f, -7.0f, -138.2f), new Vector3(-31.7f, -3.1f, -110.9f), new Vector3(-24.3f, -7.0f, -127.9f),
            // 숲 동굴 구석 (ForestCaveNook)
            new Vector3(-436.7f, 0.5f, 63.9f), new Vector3(-416.7f, 1.4f, 53.9f), new Vector3(-451.7f, 1.5f, 79.0f),
        };
        // 굴마다 하나씩은 뛰는 좀비 (CaveSpots 번호)
        private static readonly int[] CaveRunnerIndices = { 0, 4, 8 };

        private struct Spot { public Vector3 pos; public bool runner; public NetworkMonster alive; }
        private readonly List<Spot> _spots = new List<Spot>();
        private GameManager _gm;
        private bool _spawned;
        private int _lastCycle = -1;
        private float _nextTry;

        private static CaveLurkers _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("CaveLurkers");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CaveLurkers>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        private void Update()
        {
            if (Time.time < _nextTry) return;
            _nextTry = Time.time + 1f;

            GameManager gm = GameManager.Instance;
            GameLauncher launcher = GameLauncher.Instance;
            bool host = launcher != null && launcher.Runner != null && launcher.Runner.IsRunning && launcher.Runner.IsServer;
            if (gm == null || !host || gm.IsWaitingForNetworkStart || gm.IsNetworkMirror || NetworkGameState.IsGameOver) return;

            // 새 게임이면 자리를 다시 잡는다
            if (gm != _gm) { _gm = gm; _spawned = false; _lastCycle = -1; _spots.Clear(); }

            if (!_spawned)
            {
                BuildSpots();
                FillEmpty();
                _spawned = true;
                _lastCycle = gm.CurrentCycle;
                return;
            }

            // 매일 아침 빈자리를 채운다
            if (gm.CurrentPhase == GameManager.GamePhase.Day && gm.CurrentCycle != _lastCycle)
            {
                _lastCycle = gm.CurrentCycle;
                FillEmpty();
            }
        }

        private void BuildSpots()
        {
            _spots.Clear();
            foreach (CraftingSystem.TreasureChest chest in FindObjectsByType<CraftingSystem.TreasureChest>(FindObjectsSortMode.None))
            {
                for (int i = 0; i < WalkersPerChest && _spots.Count < MaxLurkers; i++)
                {
                    float a = (i * 180f + chest.GetInstanceID() % 90) * Mathf.Deg2Rad;
                    Vector3 p = chest.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.5f;
                    AddSpot(p, false);
                }
            }
            foreach (Vector3 t in TunnelSpots)
                if (_spots.Count < MaxLurkers) AddSpot(t, true);
            for (int i = 0; i < CaveSpots.Length && _spots.Count < MaxLurkers; i++)
                AddSpot(CaveSpots[i], System.Array.IndexOf(CaveRunnerIndices, i) >= 0, 2f);
        }

        private void AddSpot(Vector3 p, bool runner, float snap = 6f)
        {
            // 굴 자리는 좁게 붙인다 (넓게 찾으면 굴 위 지면으로 붙을 수 있다)
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, snap, NavMesh.AllAreas)) return;
            _spots.Add(new Spot { pos = hit.position, runner = runner });
        }

        private void FillEmpty()
        {
            for (int i = 0; i < _spots.Count; i++)
            {
                Spot s = _spots[i];
                bool alive = s.alive != null && s.alive.Object != null && s.alive.Object.IsValid &&
                             s.alive.GetComponent<NetworkHealth>() is NetworkHealth h && !h.IsDead;
                if (alive) continue;

                // 플레이어 바로 앞에서 갑자기 생기지 않게 (25m 안에 누가 있으면 다음 기회에)
                if (AnyPlayerNear(s.pos, 25f)) continue;

                float yaw = Random.Range(0f, 360f);
                // 뛰는 좀비는 빠른 대신 약하다 (체력 80%, 피해 70%) - 낮에 혼자 만나도 감당할 수 있게
                GameObject go = NetworkMonsterSpawning.Spawn(s.pos, Quaternion.Euler(0f, yaw, 0f), MonsterKind.Walker,
                                                             s.runner ? 0.8f : 1f, s.runner ? 0.7f : 1f, s.runner ? 1.05f : 1.18f, false);
                NetworkMonster m = go != null ? go.GetComponent<NetworkMonster>() : null;
                if (m != null) m.HostMakeLurker(s.pos, s.runner ? RunnerSpeed : 0f);
                s.alive = m;
                _spots[i] = s;
            }
        }

        private static bool AnyPlayerNear(Vector3 p, float r)
        {
            foreach (NetworkPlayer pl in NetworkPlayer.All)
                if (pl != null && (pl.transform.position - p).sqrMagnitude < r * r) return true;
            return false;
        }
    }
}
