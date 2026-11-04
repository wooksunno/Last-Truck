using System.Collections;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 채집이 끝난 프롭을 바로 지우지 않고 짧은 연출 뒤에 없앤다 (보기만 하는 연출, 네트워크 동기화 없음).
    ///  - 키 큰 나무: 밑동을 축으로 캔 사람 반대쪽으로 점점 빨라지며 쓰러지고, 땅에 닿아 살짝 튄 뒤 가라앉으며 사라진다.
    ///  - 그 밖(돌, 통나무, 약초): 살짝 부풀었다가 작아지며 사라진다.
    /// 연출이 시작되면 콜라이더를 모두 꺼서 길을 막거나 다시 채집되지 않게 한다.
    /// </summary>
    public class PropRemoveEffect : MonoBehaviour
    {
        [SerializeField] private float fallSeconds = 1.1f;
        [SerializeField] private float lieSeconds = 0.7f;
        [SerializeField] private float vanishSeconds = 0.5f;
        [SerializeField] private float popSeconds = 0.35f;

        private Vector3 _pivot;
        private Vector3 _axis;
        private float _height;
        private bool _fall;

        /// <summary>프롭 연출을 시작한다. player가 없으면(다른 사람이 캔 경우) 가장 가까운 플레이어를 기준으로 방향을 정한다.</summary>
        public static void Play(GameObject root, string itemId, GameObject player)
        {
            if (root == null)
                return;
            if (root.GetComponent<PropRemoveEffect>() != null)
                return;

            foreach (Collider c in root.GetComponentsInChildren<Collider>())
                c.enabled = false;
            // 채집 중 흔들림(TreeShake)이 남아 있으면 원래 메쉬로 돌려 놓고 멈춘다
            TreeShake shake = root.GetComponent<TreeShake>();
            if (shake != null)
                shake.enabled = false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Destroy(root);
                return;
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            var fx = root.AddComponent<PropRemoveEffect>();
            fx._height = b.size.y;
            fx._pivot = new Vector3(b.center.x, b.min.y, b.center.z);
            // 나무(Wood)이면서 옆으로 넓기보다 키가 큰 것만 쓰러뜨린다 (통나무는 누워 있으므로 제외)
            fx._fall = itemId == ItemIds.Wood && b.size.y > Mathf.Max(b.size.x, b.size.z) * 1.3f && b.size.y > 2f;

            if (fx._fall)
            {
                Vector3 from = player != null ? player.transform.position : NearestPlayer(fx._pivot);
                Vector3 dir = fx._pivot - from;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f)
                    dir = Random.insideUnitSphere;
                dir.y = 0f;
                dir.Normalize();
                // 카메라 안쪽(화면 위쪽)으로 넘어가면 금방 화면 밖으로 사라지므로, 그 성분을 줄여 옆으로 쓰러지게 한다
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camFwd = cam.transform.forward;
                    camFwd.y = 0f;
                    if (camFwd.sqrMagnitude > 0.01f)
                    {
                        camFwd.Normalize();
                        float into = Vector3.Dot(dir, camFwd);
                        if (into > 0f)
                            dir = (dir - camFwd * into * 0.8f).normalized;
                    }
                }
                if (dir.sqrMagnitude < 0.01f)
                    dir = Vector3.forward;
                fx._axis = Vector3.Cross(Vector3.up, dir);
            }
        }

        private static Vector3 NearestPlayer(Vector3 pos)
        {
            GameObject best = null;
            float bd = float.MaxValue;
            foreach (GameObject p in GameObject.FindGameObjectsWithTag("Player"))
            {
                float d = (p.transform.position - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            if (best != null)
                return best.transform.position;
            Camera cam = Camera.main;
            return cam != null ? cam.transform.position : pos - Vector3.forward;
        }

        private IEnumerator Start()
        {
            if (_fall)
                yield return Fall();
            else
                yield return Pop();
            Destroy(gameObject);
        }

        private IEnumerator Fall()
        {
            // 처음엔 살짝 버티다가 점점 빨라지며 넘어간다
            float angle = 0f;
            float t = 0f;
            while (t < fallSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fallSeconds);
                float target = 88f * k * k * k;
                Rotate(target - angle);
                angle = target;
                yield return null;
            }

            // 땅에 닿아 한 번 튀었다가 눕는다
            const float bounceSeconds = 0.28f;
            t = 0f;
            while (t < bounceSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / bounceSeconds);
                float target = 88f - Mathf.Sin(k * Mathf.PI) * 7f;
                Rotate(target - angle);
                angle = target;
                yield return null;
            }
            Rotate(88f - angle);

            yield return new WaitForSeconds(lieSeconds);

            // 쓰러진 줄기 가운데를 중심으로 작아지며 땅으로 가라앉는다
            Vector3 startScale = transform.localScale;
            Vector3 startPos = transform.position;
            Vector3 center = CurrentCenter();
            float sink = Mathf.Clamp(_height * 0.08f, 0.2f, 1f);
            t = 0f;
            while (t < vanishSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / vanishSeconds);
                float e = k * k;
                transform.localScale = startScale * (1f - e);
                transform.position = center + (startPos - center) * (1f - e) + Vector3.down * sink * e;
                yield return null;
            }
        }

        private Vector3 CurrentCenter()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return transform.position;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b.center;
        }

        private void Rotate(float degrees)
        {
            if (Mathf.Abs(degrees) > 0f)
                transform.RotateAround(_pivot, _axis, degrees);
        }

        private IEnumerator Pop()
        {
            Vector3 startScale = transform.localScale;
            float t = 0f;
            while (t < popSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / popSeconds);
                // 0 ~ 0.25: 1.12배로 부풀고, 그 뒤 0으로 줄어든다
                float s = k < 0.25f ? Mathf.Lerp(1f, 1.12f, k / 0.25f) : Mathf.Lerp(1.12f, 0f, (k - 0.25f) / 0.75f);
                transform.localScale = startScale * s;
                yield return null;
            }
        }
    }
}
