using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TimeUIManager : MonoBehaviour
{
    public static TimeUIManager Instance { get; private set; }

    [Header("UI Text")]
    [SerializeField] private TMP_Text week;
    [SerializeField] private TMP_Text calender;

    public int TotalWeek {  get; private set; }
    public int Month { get; private set; }
    public int Week {  get; private set; }

    public bool IsPlayerActive { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
}
