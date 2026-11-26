using CraftingSystem;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 가공 화면 가운데에 보이는 "3D 미니 장면" 하나 (설비 하나). 에디터의 ProcessDioramaBuilder가 프리팹으로 만들어 둔다.
    /// 장면을 찍는 카메라 위치, 재료가 놓일 자리, 가공 중/평소/완성 때 켤 효과, 돌아가는 부품을 들고 있다.
    /// 실제 움직임은 ProcessDiorama가 매 프레임 Animate로 부른다.
    /// </summary>
    public class ProcessDioramaStage : MonoBehaviour
    {
        public enum Motion { Roast, Grind, Press, Cut, Bubble, Heat, Dry }

        [Header("카메라")]
        public Vector3 camPosition = new Vector3(0f, 2.2f, -5.4f);
        public Vector3 camTarget = new Vector3(0f, 1.0f, 0f);
        public float camFov = 34f;
        public Color clearColor = new Color(0.1f, 0.08f, 0.1f);

        [Header("조명 (이 장면을 찍을 때만 켜진다)")]
        public Light keyLight;
        public Color ambient = new Color(0.42f, 0.42f, 0.48f);

        [Header("재료")]
        public Motion motion;
        public Transform itemAnchor;
        public float itemSize = 0.6f;
        public Color workTint = Color.white;     // 가공이 끝나갈수록 재료가 이 색으로 물든다 (굽기=갈색, 가열=주황)

        [Header("효과")]
        public GameObject idleFx;    // 항상
        public GameObject busyFx;    // 가공 중에만
        public GameObject doneFx;    // 완성됐을 때만
        public Light busyLight;      // 가공 중에 세지는 빛 (불 등)
        public float busyLightIntensity = 3f;

        [Header("움직이는 부품")]
        public Transform[] spinners;
        public Vector3[] spinAxes;
        public float[] spinSpeeds;   // 초당 각도 (가공 중), 평소에는 거의 멈춤
        public Transform shaker;     // 가공 중 덜덜 떨린다

        [Header("배경")]
        public GameObject env3D;     // 코드로 꾸민 3D 배경 (생성 배경 그림이 있으면 끈다)
        public Renderer backdrop;    // 생성 배경 그림을 붙이는 판

        private Vector3 _shakerBase;
        private bool _inited;
        private float _spinBlend;

        private void Init()
        {
            if (_inited) return;
            _inited = true;
            if (shaker != null) _shakerBase = shaker.localPosition;
        }

        /// <summary>state: 0 빈 칸, 1 가공 중, 2 완성. item = 지금 보여 줄 재료(가공 중) 또는 결과물(완성).</summary>
        public virtual void Animate(int state, float progress, float age, SpriteRenderer item, SpriteRenderer output, float dt)
        {
            Init();
            float t = Time.time;
            bool busy = state == 1;

            if (idleFx != null && !idleFx.activeSelf) idleFx.SetActive(true);
            SetActive(busyFx, busy);
            SetActive(doneFx, state == 2);
            if (busyLight != null)
            {
                float target = busy ? busyLightIntensity * (0.85f + 0.15f * Mathf.PerlinNoise(t * 6f, 0.3f)) : busyLightIntensity * 0.35f;
                busyLight.intensity = Mathf.Lerp(busyLight.intensity, target, 1f - Mathf.Exp(-dt * 6f));
            }

            // 돌아가는 부품: 가공 중이면 빠르게, 아니면 서서히 멈춘다
            _spinBlend = Mathf.Lerp(_spinBlend, busy ? 1f : 0.04f, 1f - Mathf.Exp(-dt * 3f));
            if (spinners != null)
                for (int i = 0; i < spinners.Length; i++)
                {
                    if (spinners[i] == null) continue;
                    Vector3 axis = spinAxes != null && i < spinAxes.Length ? spinAxes[i] : Vector3.right;
                    float speed = spinSpeeds != null && i < spinSpeeds.Length ? spinSpeeds[i] : 360f;
                    spinners[i].Rotate(axis, speed * _spinBlend * dt, Space.Self);
                }
            if (shaker != null)
                shaker.localPosition = _shakerBase + (busy ? new Vector3(Mathf.PerlinNoise(t * 40f, 1f) - 0.5f, Mathf.PerlinNoise(t * 40f, 7f) - 0.5f, 0f) * 0.02f : Vector3.zero);

            if (item != null) item.gameObject.SetActive(busy);
            if (output != null) output.gameObject.SetActive(state == 2);
            if (itemAnchor == null) return;

            // 방금 넣은 재료는 위에서 톡 떨어진다
            float drop = age < 0.5f ? Mathf.Pow(1f - age / 0.5f, 2f) * 0.9f : 0f;

            if (busy && item != null)
            {
                Vector3 p = itemAnchor.position + Vector3.up * drop;
                Quaternion r = Quaternion.identity;
                float s = 1f;
                Vector3 scale = Vector3.one;
                float k = Mathf.Clamp01(progress);
                Color tint = Color.Lerp(Color.white, workTint, k);
                switch (motion)
                {
                    case Motion.Roast:   // 꼬치에서 천천히 흔들리며 익는다
                        p += Vector3.up * Mathf.Sin(t * 2f) * 0.03f;
                        r = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.4f) * 12f);
                        break;
                    case Motion.Grind:   // 숫돌에 대고 앞뒤로 갈아낸다
                        p += itemAnchor.right * Mathf.Sin(t * 9f) * 0.06f + Vector3.up * Mathf.Abs(Mathf.Sin(t * 9f)) * 0.015f;
                        r = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 6f);
                        break;
                    case Motion.Press:   // 롤러 사이로 들어가 납작하게 눌렸다 나온다 (2.4초 주기)
                    {
                        float c = (t % 2.4f) / 2.4f;
                        p += itemAnchor.right * Mathf.Lerp(-0.55f, 0.55f, c);
                        float squash = Mathf.Clamp01(1f - Mathf.Abs(c - 0.5f) * 5f);
                        scale = new Vector3(1f + squash * 0.35f, 1f - squash * 0.55f, 1f);
                        break;
                    }
                    case Motion.Cut:     // 회전 톱날 아래로 천천히 밀려 들어간다
                    {
                        float c = Mathf.PingPong(t * 0.35f, 1f);
                        p += itemAnchor.right * Mathf.Lerp(-0.35f, 0.25f, c) + Vector3.up * (Mathf.PerlinNoise(t * 30f, 0f) - 0.5f) * 0.01f;
                        break;
                    }
                    case Motion.Bubble:  // 액체 속에서 둥둥 떠다닌다
                        p += new Vector3(Mathf.Sin(t * 1.3f) * 0.05f, Mathf.Sin(t * 2.1f) * 0.06f, 0f);
                        r = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * 20f);
                        tint = Color.Lerp(tint, new Color(0.7f, 1f, 0.75f), 0.35f);
                        break;
                    case Motion.Heat:    // 불길 속에서 달아오른다 (빛나듯 깜빡인다)
                    {
                        p += Vector3.up * Mathf.Sin(t * 1.5f) * 0.02f;
                        float glow = 0.5f + 0.5f * Mathf.Sin(t * 5f);
                        tint = Color.Lerp(tint, new Color(1f, 0.85f, 0.6f), glow * k * 0.5f);
                        break;
                    }
                    case Motion.Dry:     // 건조대에 매달려 바람에 살랑인다
                        r = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.1f) * 7f + Mathf.Sin(t * 2.7f) * 2f);
                        break;
                }
                item.transform.position = p;
                item.transform.rotation = Quaternion.LookRotation(item.transform.position - CamWorld()) * r;
                item.transform.localScale = Vector3.Scale(scale, Vector3.one * itemSize * s);
                item.color = tint;
            }

            if (state == 2 && output != null)
            {
                // 완성: 결과물이 살짝 떠올라 빙글 돌며 반짝인다
                float pop = age < 0.4f ? Mathf.Sin(age / 0.4f * Mathf.PI) * 0.25f : 0f;
                Vector3 p = itemAnchor.position + Vector3.up * (0.35f + Mathf.Sin(t * 2.2f) * 0.06f);
                output.transform.position = p;
                output.transform.rotation = Quaternion.LookRotation(p - CamWorld()) * Quaternion.Euler(0f, Mathf.Sin(t * 1.6f) * 25f, 0f);
                output.transform.localScale = Vector3.one * itemSize * (1.15f + pop);
                output.color = Color.white;
            }
        }

        private Vector3 CamWorld() => transform.TransformPoint(camPosition);

        private static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
