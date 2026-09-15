using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class StatsUIManager : MonoBehaviour
{
    public static StatsUIManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject statsWindow;

    [Header("UI Rect")]
    [SerializeField] private RectTransform charcterImgRect;
    [SerializeField] private RectTransform statsInfoRect;

    [Header("Info Components")]
    [SerializeField] private Image characterSprite;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text danceText;
    [SerializeField] private TMP_Text singText;
    [SerializeField] private TMP_Text visualText;
    [SerializeField] private TMP_Text humanityText;
    [SerializeField] private TMP_Text awarenessText;
    [SerializeField] private TMP_Text fanText;

    // UI animation
    [Header("Rect Ani")]
    [SerializeField] private float slideDistance = 300f;
    [SerializeField] private float duration = 0.5f;

    private Vector2 imgTargetPos;
    private Vector2 infoTargetPos;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }

        imgTargetPos = charcterImgRect.anchoredPosition;
        infoTargetPos = statsInfoRect.anchoredPosition;

        statsWindow.SetActive(false);
    }

    public void OpenUI(CharacterStatsData data)
    {
        characterSprite.sprite = data.characterSprite;
        nameText.text = data.characterName;
        danceText.text = $"{data.dance}";
        singText.text = $"{data.sing}";
        visualText.text = $"{data.visual}";
        humanityText.text = $"{data.humanity}";
        awarenessText.text = $"{data.awareness}";
        fanText.text = $"{data.fan}";

        statsWindow.SetActive(true);

        // UI animation
        charcterImgRect.DOKill();
        statsInfoRect.DOKill();

        charcterImgRect.anchoredPosition = imgTargetPos + new Vector2(slideDistance, 0f);
        statsInfoRect.anchoredPosition = infoTargetPos + new Vector2(-slideDistance, 0f);

        charcterImgRect.DOAnchorPos(imgTargetPos, duration).SetEase(Ease.OutCubic);
        statsInfoRect.DOAnchorPos(infoTargetPos, duration).SetEase(Ease.OutCubic);
    }

    public void CloseUI()
    {
        charcterImgRect.DOKill();
        statsInfoRect.DOKill();

        statsWindow.SetActive(false);
    }
}