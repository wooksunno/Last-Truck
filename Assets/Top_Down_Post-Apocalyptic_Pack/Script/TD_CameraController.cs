using UnityEngine;

public class TDCameraController : MonoBehaviour
{
    [Header("Движение")]
    [Tooltip("Скорость перемещения камеры по XZ-плоскости")]
    public float moveSpeed = 20f;

    [Tooltip("Толщина зоны у краёв экрана для скролла мышью")]
    public float edgeSize = 20f;

    [Tooltip("Двигать камеру, когда мышь у краёв экрана")]
    public bool useEdgeScroll = true;

    [Header("Edge Scroll (фикс старта)")]
    [Tooltip("Сколько кадров после старта игнорировать edge-scroll, чтобы камера не улетала в minX/minZ.")]
    public int edgeScrollIgnoreFramesOnStart = 2;

    [Tooltip("Игнорировать edge-scroll, если позиция мыши выглядит как (0,0) (часто на первом кадре/при фокусе окна).")]
    public bool ignoreEdgeScrollWhenMouseAtZero = true;

    [Header("Зум (перемещение камеры вперёд/назад)")]
    [Tooltip("Скорость приближения/отдаления камеры колёсиком")]
    public float zoomSpeed = 50f;

    [Tooltip("Минимальная высота камеры над землёй")]
    public float minHeight = 10f;

    [Tooltip("Максимальная высота камеры над землёй")]
    public float maxHeight = 60f;

    [Header("Ограничения карты (по позиции камеры)")]
    [Tooltip("Включить/выключить ограничения движения камеры")]
    public bool useBounds = true;

    [Tooltip("Минимальный и максимальный X, где может находиться камера")]
    public Vector2 minXmaxX = new Vector2(-50f, 50f);

    [Tooltip("Минимальный и максимальный Z, где может находиться камера")]
    public Vector2 minZmaxZ = new Vector2(-50f, 50f);

    private Camera cam;

    // стартовый фикс
    private int edgeIgnoreFramesLeft;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("TDCameraController: на объекте нет компонента Camera!");
            enabled = false;
            return;
        }

        // Для 3D TD — перспективная камера
        cam.orthographic = false;

        // На старте игнорируем edge-scroll несколько кадров, чтобы не прижало к minX/minZ
        edgeIgnoreFramesLeft = Mathf.Max(0, edgeScrollIgnoreFramesOnStart);
    }

    private void Update()
    {
        HandleMove();
        HandleZoom();

        if (useBounds)
            ClampPosition();

        // уменьшаем счётчик после всех вычислений кадра
        if (edgeIgnoreFramesLeft > 0)
            edgeIgnoreFramesLeft--;
    }

    // ---------------- ДВИЖЕНИЕ ----------------
    private void HandleMove()
    {
        Vector3 dir = Vector3.zero;

        // WASD / стрелки (старый Input)
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            dir.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            dir.x += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            dir.z += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            dir.z -= 1f;

        // Движение мышью у краёв экрана
        bool edgeAllowedThisFrame = useEdgeScroll && edgeIgnoreFramesLeft <= 0;

        if (edgeAllowedThisFrame)
        {
            Vector3 mp3 = Input.mousePosition;
            Vector2 mp = new Vector2(mp3.x, mp3.y);

            if (ignoreEdgeScrollWhenMouseAtZero && mp.x <= 0.5f && mp.y <= 0.5f)
            {
                // ничего
            }
            else
            {
                if (mp.x <= edgeSize)
                    dir.x -= 1f;
                else if (mp.x >= Screen.width - edgeSize)
                    dir.x += 1f;

                if (mp.y <= edgeSize)
                    dir.z -= 1f;
                else if (mp.y >= Screen.height - edgeSize)
                    dir.z += 1f;
            }
        }

        // Чтобы по диагонали не было быстрее
        if (dir.sqrMagnitude > 1f)
            dir.Normalize();

        // Двигаем камеру по плоскости XZ относительно её поворота по Y
        Vector3 moveDir = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * dir;
        moveDir.y = 0f;

        // unscaledDeltaTime — камера НЕ ускоряется от Time.timeScale
        transform.position += moveDir * moveSpeed * Time.unscaledDeltaTime;
    }

    // ---------------- ЗУМ ----------------
    private void HandleZoom()
    {
        // Колёсико мыши в старом Input
        float rawScroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(rawScroll) < 0.0001f)
            return;

        // Приводим к примерно сопоставимому масштабу с InputSystem (там были "крупные" значения)
        // Не меняет смысл: просто делает зум ощутимым.
        float scaledScroll = rawScroll * 120f;

        // unscaledDeltaTime — зум НЕ ускоряется от Time.timeScale
        float zoomDelta = scaledScroll * zoomSpeed * Time.unscaledDeltaTime;

        Vector3 newPos = transform.position + transform.forward * zoomDelta;
        newPos.y = Mathf.Clamp(newPos.y, minHeight, maxHeight);
        transform.position = newPos;
    }

    // ---------------- ОГРАНИЧЕНИЯ ПО КАРТЕ ----------------
    private void ClampPosition()
    {
        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, minXmaxX.x, minXmaxX.y);
        pos.z = Mathf.Clamp(pos.z, minZmaxZ.x, minZmaxZ.y);

        transform.position = pos;
    }
}
