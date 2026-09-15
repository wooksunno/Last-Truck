using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// 플레이어 인벤토리(우하단) + 트럭/시설 팝업 UI.
    /// </summary>
    public class GameUIController : MonoBehaviour
    {
        public static GameUIController Instance { get; private set; }

        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Font uiFont;

        private Canvas _canvas;
        private RectTransform _playerSlotRoot;
        private GameObject _popupRoot;
        private Text _popupTitle;
        private RectTransform _popupBody;
        private ScrollRect _popupScrollRect;
        private Button _closeButton;

        private ProcessingFacility _activeFacility;
        private TruckStation _activeTruck;
        private PlayerInventory _activePlayer;
        private RecipeData _selectedRecipe;
        private bool _truckCodexOpen;
        private ItemData _codexSelectedItem;
        private bool _craftingPanelOpen;
        private CraftingCategory _craftingCategory = CraftingCategory.Tools;
        private ItemData _withdrawItem;
        private int _withdrawMax;

        private enum CraftingCategory
        {
            Tools,
            Weapons,
            Materials
        }

        private Text _facilityCountdownText;

        private class FacilityJob
        {
            public RecipeData recipe;
            public float remaining;
        }

        private readonly Dictionary<ProcessingFacility, FacilityJob> _facilityJobs = new Dictionary<ProcessingFacility, FacilityJob>();

        private class ChainStep
        {
            public ItemData item;
            public RecipeData viaRecipe;
        }

        public bool IsPopupOpen => _popupRoot != null && _popupRoot.activeSelf;

        public void Initialize(PlayerInventory inventory)
        {
            playerInventory = inventory;
            EnsureFont();
            EnsureEventSystem();

            if (_canvas == null)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = transform.GetChild(i);
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }

            BuildCanvas();
            BindPlayerInventory();
            RefreshPlayerInventoryUI();
            ClosePopup();
        }

        private void Awake()
        {
            Instance = this;
            EnsureFont();
        }

        private void EnsureFont()
        {
            if (uiFont == null)
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null)
                uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (playerInventory != null)
                playerInventory.Changed -= RefreshPlayerInventoryUI;
        }

        public void OpenFacilityPanel(ProcessingFacility facility, PlayerInventory player)
        {
            _activeFacility = facility;
            _activeTruck = null;
            _activePlayer = player;
            _selectedRecipe = null;
            _craftingPanelOpen = false;
            ShowPopup($"{facility.InteractLabel} 가공", BuildFacilityContent);
        }

        public void OpenTruckPanel(TruckStation truck, PlayerInventory player)
        {
            _activeTruck = truck;
            _activeFacility = null;
            _activePlayer = player;
            _selectedRecipe = null;
            _truckCodexOpen = false;
            _codexSelectedItem = null;
            _craftingPanelOpen = false;
            _withdrawItem = null;
            ShowPopup("트럭 거점", BuildTruckContent);
        }

        /// <summary>
        /// 인벤토리 옆 '제작' 버튼으로 트럭 방문 없이 바로 조립 목록을 연다.
        /// </summary>
        public void OpenCraftingPanel()
        {
            if (playerInventory == null)
                return;

            if (_activeTruck == null)
                _activeTruck = FindFirstObjectByType<TruckStation>();

            _activePlayer = playerInventory;
            _activeFacility = null;
            _truckCodexOpen = false;
            _codexSelectedItem = null;
            _craftingPanelOpen = true;
            _selectedRecipe = null;
            ShowPopup("제작", BuildTruckContent);
        }

        /// <summary>
        /// 헤더의 '도감' 버튼으로 어디서든(시설/제작 패널 포함) 제작 링크 도감을 연다.
        /// </summary>
        public void OpenJournal()
        {
            if (_activeTruck == null)
                _activeTruck = FindFirstObjectByType<TruckStation>();
            if (_activeTruck == null)
                return;

            _activePlayer = playerInventory;
            _activeFacility = null;
            _craftingPanelOpen = false;
            _truckCodexOpen = true;
            _codexSelectedItem = null;
            _selectedRecipe = null;
            ShowPopup("트럭 거점", BuildTruckContent);
        }

        public void ClosePopup()
        {
            if (_popupRoot != null)
                _popupRoot.SetActive(false);

            _activeFacility = null;
            _activeTruck = null;
            _selectedRecipe = null;
            _codexSelectedItem = null;
            _truckCodexOpen = false;
            _craftingPanelOpen = false;
            _withdrawItem = null;
        }

        private void ShowPopup(string title, System.Action builder)
        {
            _popupRoot.SetActive(true);
            _popupTitle.text = title;
            ClearChildren(_popupBody);
            builder?.Invoke();
        }

        private void BindPlayerInventory()
        {
            if (playerInventory == null)
                return;

            playerInventory.Changed -= RefreshPlayerInventoryUI;
            playerInventory.Changed += RefreshPlayerInventoryUI;
        }

        private void RefreshPlayerInventoryUI()
        {
            if (_playerSlotRoot == null || playerInventory == null)
                return;

            ClearChildren(_playerSlotRoot);

            foreach (InventorySlot slot in playerInventory.Slots)
            {
                if (slot == null || slot.IsEmpty)
                    continue;

                InventorySlot captured = slot;
                CreateInventorySlotView(_playerSlotRoot, slot.item, slot.count, 72f, () =>
                {
                    if (_activeTruck == null)
                        return;
                    TransferItem(playerInventory.Inventory, _activeTruck.TruckInventory.Inventory, captured.item, captured.count);
                    ShowPopup("트럭 거점", BuildTruckContent);
                });
            }
        }

        private static void TransferItem(ItemStackInventory source, ItemStackInventory destination, ItemData item, int count)
        {
            if (source == null || destination == null || item == null || count <= 0)
                return;

            if (destination.AddItem(item, count))
                source.RemoveItem(item, count);
            else
                Debug.LogWarning("공간이 부족하여 아이템을 이동할 수 없습니다.");
        }

        private void BuildFacilityContent()
        {
            if (_activeFacility == null)
                return;

            if (_popupScrollRect != null)
                _popupScrollRect.vertical = true;

            if (_activeFacility.Recipes.Count == 0)
                _activeFacility.LoadRecipesFromCatalog();

            _facilityJobs.TryGetValue(_activeFacility, out FacilityJob activeJob);
            bool isProcessing = activeJob != null;

            if (!isProcessing && _selectedRecipe != null &&
                !new List<RecipeData>(_activeFacility.Recipes).Contains(_selectedRecipe))
                _selectedRecipe = null;

            CreateFurnacePanel(_popupBody, activeJob);

            CreateLabel(_popupBody, "가공할 재료를 선택하세요", 16, FontStyle.Bold);
            RectTransform grid = CreateScrollableGrid(_popupBody, "재료 선택", 4, 200f);
            foreach (RecipeData recipe in _activeFacility.Recipes)
            {
                if (recipe == null || recipe.inputs == null || recipe.inputs.Count == 0 ||
                    recipe.inputs[0] == null || recipe.inputs[0].item == null)
                    continue;

                bool canCraft = _activePlayer != null && _activePlayer.HasIngredients(recipe);
                bool isSelected = !isProcessing && _selectedRecipe == recipe;
                RecipeData captured = recipe;
                CreateRecipeSelectIcon(grid, recipe, canCraft, isSelected, () =>
                {
                    if (isProcessing)
                        return;
                    _selectedRecipe = captured;
                    ShowPopup($"{_activeFacility.InteractLabel} 가공", BuildFacilityContent);
                });
            }
        }

        private void CreateFurnacePanel(RectTransform parent, FacilityJob activeJob)
        {
            var container = new GameObject("FurnacePanel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            container.transform.SetParent(parent, false);
            container.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 150);
            container.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
            LayoutElement cle = container.GetComponent<LayoutElement>();
            cle.preferredHeight = 150;
            cle.minHeight = 150;

            RectTransform containerRt = container.GetComponent<RectTransform>();

            bool isProcessing = activeJob != null;
            RecipeData recipe = isProcessing ? activeJob.recipe : _selectedRecipe;
            ItemData inputItem = null;
            int needCount = 0;
            if (recipe != null && recipe.inputs != null && recipe.inputs.Count > 0 && recipe.inputs[0] != null)
            {
                inputItem = recipe.inputs[0].item;
                needCount = recipe.inputs[0].count;
            }

            ItemData outputItem = recipe != null && recipe.output != null ? recipe.output.item : null;
            int outputCount = recipe != null && recipe.output != null ? recipe.output.count : 0;
            int haveCount = (_activePlayer != null && inputItem != null) ? _activePlayer.GetItemCount(inputItem) : 0;
            bool canProcess = recipe != null && !isProcessing &&
                              _activePlayer != null && _activePlayer.HasIngredients(recipe);

            CreateFurnaceSlot(containerRt, new Vector2(0f, 0.5f), new Vector2(90f, 0f), inputItem,
                inputItem != null ? $"{haveCount}/{needCount}" : "재료 선택");

            CreateFurnaceMiddle(containerRt, recipe, canProcess, isProcessing, activeJob);

            CreateFurnaceSlot(containerRt, new Vector2(1f, 0.5f), new Vector2(-90f, 0f), outputItem,
                outputItem != null ? $"x{outputCount}" : "-");
        }

        private void CreateFurnaceSlot(RectTransform parent, Vector2 anchor, Vector2 anchoredPos, ItemData item, string subText)
        {
            var go = new GameObject("FurnaceSlot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(96f, 118f);
            go.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.95f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f);
            iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0, -10);
            iconRt.sizeDelta = new Vector2(64f, 64f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = item != null ? item.icon : null;
            iconImg.color = item != null
                ? (item.icon != null ? Color.white : new Color(0.7f, 0.7f, 0.75f))
                : new Color(0.4f, 0.4f, 0.45f, 0.5f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0);
            textRt.anchorMax = new Vector2(1, 0);
            textRt.pivot = new Vector2(0.5f, 0f);
            textRt.anchoredPosition = new Vector2(0, 6);
            textRt.sizeDelta = new Vector2(-6, 28);
            Text text = textGo.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = subText ?? "";
        }

        private void CreateFurnaceMiddle(RectTransform parent, RecipeData recipe, bool canProcess, bool isProcessing, FacilityJob activeJob)
        {
            var go = new GameObject("FurnaceMiddle", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(150f, 118f);

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
            arrowGo.transform.SetParent(go.transform, false);
            RectTransform arrowRt = arrowGo.GetComponent<RectTransform>();
            arrowRt.anchorMin = new Vector2(0, 1);
            arrowRt.anchorMax = new Vector2(1, 1);
            arrowRt.pivot = new Vector2(0.5f, 1f);
            arrowRt.anchoredPosition = Vector2.zero;
            arrowRt.sizeDelta = new Vector2(0, 26);
            Text arrowText = arrowGo.GetComponent<Text>();
            arrowText.font = uiFont;
            arrowText.fontSize = 20;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = new Color(0.6f, 0.6f, 0.65f);
            arrowText.text = "→";

            var buttonGo = new GameObject("ProcessButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(go.transform, false);
            RectTransform buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRt.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRt.pivot = new Vector2(0.5f, 0.5f);
            buttonRt.anchoredPosition = new Vector2(0, -8);
            buttonRt.sizeDelta = new Vector2(126f, 44f);
            Image btnImg = buttonGo.GetComponent<Image>();
            btnImg.color = canProcess ? new Color(0.3f, 0.55f, 0.35f) : new Color(0.25f, 0.25f, 0.28f);
            Button btn = buttonGo.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.interactable = canProcess;
            RecipeData captured = recipe;
            btn.onClick.AddListener(() => StartFacilityProcessing(captured));

            CreateText(buttonGo.transform, "Label", isProcessing ? "가공 중" : "가공하기", 14, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Text label = buttonGo.GetComponentInChildren<Text>();
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = canProcess ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            label.raycastTarget = false;

            var countdownGo = new GameObject("Countdown", typeof(RectTransform), typeof(Text));
            countdownGo.transform.SetParent(go.transform, false);
            RectTransform countdownRt = countdownGo.GetComponent<RectTransform>();
            countdownRt.anchorMin = new Vector2(0, 0);
            countdownRt.anchorMax = new Vector2(1, 0);
            countdownRt.pivot = new Vector2(0.5f, 0f);
            countdownRt.anchoredPosition = new Vector2(0, 4);
            countdownRt.sizeDelta = new Vector2(0, 20);
            Text countdownText = countdownGo.GetComponent<Text>();
            countdownText.font = uiFont;
            countdownText.fontSize = 12;
            countdownText.alignment = TextAnchor.MiddleCenter;
            countdownText.color = new Color(1f, 0.85f, 0.4f);
            countdownText.text = isProcessing && activeJob != null ? $"{Mathf.Max(0f, activeJob.remaining):0.0}초 남음" : "";
            _facilityCountdownText = isProcessing ? countdownText : null;
        }

        private void CreateRecipeSelectIcon(RectTransform parent, RecipeData recipe, bool canCraft, bool isSelected, UnityEngine.Events.UnityAction onClick)
        {
            ItemData input = recipe.inputs != null && recipe.inputs.Count > 0 ? recipe.inputs[0].item : null;
            if (input == null)
                return;

            var go = new GameObject($"Select_{recipe.recipeID}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 86f);

            Image bg = go.GetComponent<Image>();
            bg.color = canCraft ? new Color(0.95f, 0.85f, 0.35f, 0.85f) : new Color(0.15f, 0.16f, 0.2f, 0.85f);

            Button button = go.GetComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);

            if (isSelected)
            {
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.4f, 0.85f, 1f, 1f);
                outline.effectDistance = new Vector2(3f, 3f);
            }
            else if (canCraft)
            {
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.95f, 0.4f, 1f);
                outline.effectDistance = new Vector2(2f, 2f);
            }

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f);
            iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0, -4);
            iconRt.sizeDelta = new Vector2(48f, 48f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = input.icon;
            iconImg.color = input.icon != null ? Color.white : new Color(0.7f, 0.7f, 0.75f);
            iconImg.raycastTarget = false;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(go.transform, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 0);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0, 2);
            nameRt.sizeDelta = new Vector2(-2, 22);
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = uiFont;
            nameText.fontSize = 10;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = canCraft ? new Color(0.15f, 0.13f, 0.02f) : Color.white;
            nameText.text = input.itemName;
            nameText.raycastTarget = false;
        }

        private void StartFacilityProcessing(RecipeData recipe)
        {
            ProcessingFacility facility = _activeFacility;
            if (recipe == null || facility == null || _activePlayer == null || _facilityJobs.ContainsKey(facility))
                return;

            if (!_activePlayer.HasIngredients(recipe))
            {
                Debug.LogWarning("재료가 부족합니다.");
                return;
            }

            PlayerInventory player = _activePlayer;

            if (recipe.processingSeconds <= 0f)
            {
                bool ok = facility.TryProcess(recipe, player);
                if (!ok)
                    Debug.LogWarning("재료가 부족합니다.");
                RefreshPlayerInventoryUI();
                ShowPopup($"{facility.InteractLabel} 가공", BuildFacilityContent);
                return;
            }

            var job = new FacilityJob { recipe = recipe, remaining = recipe.processingSeconds };
            _facilityJobs[facility] = job;
            ShowPopup($"{facility.InteractLabel} 가공", BuildFacilityContent);
            StartCoroutine(FacilityProcessRoutine(facility, player, recipe, job));
        }

        private IEnumerator FacilityProcessRoutine(ProcessingFacility facility, PlayerInventory player, RecipeData recipe, FacilityJob job)
        {
            while (job.remaining > 0f)
            {
                yield return null;
                job.remaining -= Time.deltaTime;
                if (_activeFacility == facility && IsPopupOpen && _facilityCountdownText != null)
                    _facilityCountdownText.text = $"{Mathf.Max(0f, job.remaining):0.0}초 남음";
            }

            bool ok = facility.TryProcess(recipe, player);
            _facilityJobs.Remove(facility);

            if (!ok)
                Debug.LogWarning("재료가 부족합니다.");

            RefreshPlayerInventoryUI();

            if (_activeFacility == facility && IsPopupOpen)
                ShowPopup($"{facility.InteractLabel} 가공", BuildFacilityContent);
        }

        private void BuildTruckContent()
        {
            if (_activeTruck == null)
                return;

            if (_truckCodexOpen)
            {
                BuildTruckCodex();
                return;
            }

            if (_craftingPanelOpen)
            {
                BuildCraftingPanel();
                return;
            }

            if (_popupScrollRect != null)
                _popupScrollRect.vertical = true;

            TruckInventory truckInv = _activeTruck.TruckInventory;
            TruckCraftingManager craft = _activeTruck.CraftingManager;
            if (craft != null && craft.Recipes.Count == 0)
                craft.LoadAssemblyRecipesFromCatalog();

            if (_selectedRecipe != null)
                CreateRecipePreview(_popupBody, _selectedRecipe, truckInv, craft);

            if (_withdrawItem != null)
                CreateWithdrawPrompt(_popupBody, truckInv);

            RectTransform actionRow = CreateRow(_popupBody);
            CreateSmallActionButton(actionRow, "원재료 +10", new Color(0.3f, 0.5f, 0.35f), () =>
            {
                GrantRawMaterialsToTruck(truckInv, 10);
                ShowPopup("트럭 거점", BuildTruckContent);
            });
            CreateFlexibleSpacer(actionRow);
            CreateSmallActionButton(actionRow, "제작", new Color(0.3f, 0.42f, 0.55f), OpenCraftingPanel);

            RectTransform truckGrid = CreateScrollableGrid(_popupBody, "트럭 보관함", 6, 380f);
            if (truckInv != null)
            {
                foreach (InventorySlot slot in truckInv.Slots)
                {
                    if (slot == null || slot.IsEmpty)
                        continue;

                    InventorySlot captured = slot;
                    CreateInventorySlotView(truckGrid, slot.item, slot.count, 64f, () =>
                    {
                        _withdrawItem = captured.item;
                        _withdrawMax = captured.count;
                        ShowPopup("트럭 거점", BuildTruckContent);
                    });
                }
            }
        }

        private void CreateWithdrawPrompt(RectTransform parent, TruckInventory truckInv)
        {
            ItemData item = _withdrawItem;
            int max = truckInv != null ? truckInv.GetItemCount(item) : _withdrawMax;
            if (item == null || max <= 0)
            {
                _withdrawItem = null;
                return;
            }

            var container = new GameObject("WithdrawPrompt", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            container.transform.SetParent(parent, false);
            container.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 80);
            container.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

            HorizontalLayoutGroup h = container.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.padding = new RectOffset(12, 12, 10, 10);
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = false;

            LayoutElement cle = container.GetComponent<LayoutElement>();
            cle.preferredHeight = 80;
            cle.minHeight = 80;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(container.transform, false);
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 56f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = item.icon;
            iconImg.color = item.icon != null ? Color.white : new Color(0.7f, 0.7f, 0.75f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(container.transform, false);
            labelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 56f);
            Text labelText = labelGo.GetComponent<Text>();
            labelText.font = uiFont;
            labelText.fontSize = 13;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.text = $"{item.itemName}\n보유 {max}개";

            InputField field = CreateNumberInputField(container.GetComponent<RectTransform>(), Mathf.Clamp(_withdrawMax, 1, max), max);

            CreateSmallActionButton(container.GetComponent<RectTransform>(), "확인", new Color(0.3f, 0.55f, 0.35f), () =>
            {
                if (!int.TryParse(field.text, out int amount))
                    amount = 1;
                amount = Mathf.Clamp(amount, 1, max);
                TransferItem(truckInv.Inventory, _activePlayer.Inventory, item, amount);
                _withdrawItem = null;
                ShowPopup("트럭 거점", BuildTruckContent);
            });

            CreateSmallActionButton(container.GetComponent<RectTransform>(), "취소", new Color(0.35f, 0.3f, 0.3f), () =>
            {
                _withdrawItem = null;
                ShowPopup("트럭 거점", BuildTruckContent);
            });
        }

        private InputField CreateNumberInputField(RectTransform parent, int defaultValue, int maxValue)
        {
            var go = new GameObject("QuantityInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 44f);
            go.GetComponent<Image>().color = new Color(0.18f, 0.19f, 0.23f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 4);
            textRt.offsetMax = new Vector2(-8, -4);
            Text text = textGo.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;

            InputField field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.characterLimit = Mathf.Max(1, maxValue.ToString().Length);
            field.text = defaultValue.ToString();

            return field;
        }

        private static void CreateFlexibleSpacer(RectTransform parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1;
        }

        private void BuildCraftingPanel()
        {
            if (_popupScrollRect != null)
                _popupScrollRect.vertical = true;

            TruckInventory truckInv = _activeTruck != null ? _activeTruck.TruckInventory : null;
            TruckCraftingManager craft = _activeTruck != null ? _activeTruck.CraftingManager : null;
            if (craft != null && craft.Recipes.Count == 0)
                craft.LoadAssemblyRecipesFromCatalog();

            RectTransform tabRow = CreateRow(_popupBody);
            CreateSmallActionButton(tabRow, "← 뒤로", new Color(0.3f, 0.3f, 0.35f), () =>
            {
                _craftingPanelOpen = false;
                _selectedRecipe = null;
                ShowPopup("트럭 거점", BuildTruckContent);
            });
            CreateCategoryTab(tabRow, "도구", CraftingCategory.Tools);
            CreateCategoryTab(tabRow, "무기", CraftingCategory.Weapons);
            CreateCategoryTab(tabRow, "재료", CraftingCategory.Materials);

            if (_selectedRecipe != null)
                CreateRecipePreview(_popupBody, _selectedRecipe, truckInv, craft);

            RectTransform grid = CreateScrollableGrid(_popupBody, "아이템", 5, 300f);
            if (craft != null)
            {
                foreach (RecipeData recipe in craft.Recipes)
                {
                    if (recipe == null || recipe.output == null || recipe.output.item == null)
                        continue;
                    if (!MatchesCraftingCategory(recipe.output.item, _craftingCategory))
                        continue;

                    bool canCraft = truckInv != null && truckInv.HasIngredients(recipe);
                    RecipeData captured = recipe;
                    CreateCraftIconView(grid, recipe, canCraft, () =>
                    {
                        _selectedRecipe = captured;
                        ShowPopup("제작", BuildTruckContent);
                    });
                }
            }
        }

        private void CreateCategoryTab(RectTransform parent, string label, CraftingCategory category)
        {
            bool selected = _craftingCategory == category;
            CreateSmallActionButton(parent, label, selected ? new Color(0.35f, 0.42f, 0.62f) : new Color(0.22f, 0.22f, 0.26f), () =>
            {
                if (_craftingCategory == category)
                    return;
                _craftingCategory = category;
                _selectedRecipe = null;
                ShowPopup("제작", BuildTruckContent);
            });
        }

        private static bool MatchesCraftingCategory(ItemData item, CraftingCategory category)
        {
            switch (category)
            {
                case CraftingCategory.Tools:
                    return item.itemType == ItemType.Tool;
                case CraftingCategory.Weapons:
                    return item.itemType == ItemType.Finished;
                case CraftingCategory.Materials:
                    return item.itemType == ItemType.Raw || item.itemType == ItemType.Intermediate;
                default:
                    return true;
            }
        }

        private void CreateRecipePreview(RectTransform parent, RecipeData recipe, TruckInventory truckInv, TruckCraftingManager craft)
        {
            var container = new GameObject("RecipePreview", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            container.transform.SetParent(parent, false);
            container.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 190);
            container.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

            VerticalLayoutGroup vlg = container.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            LayoutElement cle = container.GetComponent<LayoutElement>();
            cle.preferredHeight = 190;
            cle.minHeight = 190;

            CreateLabel(container.GetComponent<RectTransform>(), recipe.GetDisplayName(), 16, FontStyle.Bold);

            bool canCraft = truckInv != null && truckInv.HasIngredients(recipe);

            RectTransform ingredientRow = CreateRow(container.GetComponent<RectTransform>());
            foreach (RecipeIngredient ingredient in recipe.inputs)
            {
                if (ingredient == null || ingredient.item == null || ingredient.count <= 0)
                    continue;

                int have = truckInv != null ? truckInv.GetItemCount(ingredient.item) : 0;
                CreateIngredientPreviewSlot(ingredientRow, ingredient.item, have, ingredient.count);
            }

            CreateCraftConfirmButton(container.GetComponent<RectTransform>(), canCraft, () =>
            {
                if (craft != null)
                    craft.TryCraft(recipe);
                ShowPopup(_craftingPanelOpen ? "제작" : "트럭 거점", BuildTruckContent);
            });
        }

        private void CreateIngredientPreviewSlot(RectTransform parent, ItemData item, int have, int need)
        {
            bool enough = have >= need;

            var go = new GameObject($"Ingredient_{item.itemID}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 78f);
            go.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.2f, 0.9f);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 60;
            le.preferredHeight = 78;
            le.minWidth = 60;
            le.minHeight = 78;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f);
            iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0, -4);
            iconRt.sizeDelta = new Vector2(44f, 44f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = item.icon;
            iconImg.color = item.icon != null ? Color.white : new Color(0.7f, 0.7f, 0.75f);

            var textGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0);
            textRt.anchorMax = new Vector2(1, 0);
            textRt.pivot = new Vector2(0.5f, 0f);
            textRt.anchoredPosition = new Vector2(0, 2);
            textRt.sizeDelta = new Vector2(-4, 26);
            Text text = textGo.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = enough ? new Color(0.55f, 0.95f, 0.55f) : new Color(0.95f, 0.4f, 0.4f);
            text.text = $"{have}/{need}";
        }

        private void CreateCraftConfirmButton(RectTransform parent, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("CraftButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44f);

            Image img = go.GetComponent<Image>();
            img.color = interactable ? new Color(0.3f, 0.55f, 0.35f) : new Color(0.25f, 0.25f, 0.28f);

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = interactable;
            btn.onClick.AddListener(onClick);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 44;
            le.minHeight = 40;

            CreateText(go.transform, "Label", "제작", 15, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Text t = go.GetComponentInChildren<Text>();
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            t.color = interactable ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            t.raycastTarget = false;
        }

        private void BuildTruckCodex()
        {
            if (_popupScrollRect != null)
                _popupScrollRect.vertical = true;

            if (_codexSelectedItem != null)
                BuildCodexItemDetail(_codexSelectedItem);
            else
                BuildCodexItemList();
        }

        private void BuildCodexItemList()
        {
            RectTransform backRow = CreateRow(_popupBody);
            CreateSmallActionButton(backRow, "← 뒤로", new Color(0.3f, 0.3f, 0.35f), () =>
            {
                _truckCodexOpen = false;
                ShowPopup("트럭 거점", BuildTruckContent);
            });

            CreateLabel(_popupBody, "제작 도감 - 아이템을 선택하세요", 18, FontStyle.Bold);

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            foreach (ItemData item in catalog.Items)
            {
                if (item == null || FindRecipesProducing(item).Count == 0)
                    continue;

                ItemData captured = item;
                CreateCodexListEntry(_popupBody, item, () =>
                {
                    _codexSelectedItem = captured;
                    ShowPopup("트럭 거점", BuildTruckContent);
                });
            }
        }

        private void BuildCodexItemDetail(ItemData item)
        {
            RectTransform backRow = CreateRow(_popupBody);
            CreateSmallActionButton(backRow, "← 목록", new Color(0.3f, 0.3f, 0.35f), () =>
            {
                _codexSelectedItem = null;
                ShowPopup("트럭 거점", BuildTruckContent);
            });

            CreateLabel(_popupBody, $"{item.itemName} - 제작 링크", 18, FontStyle.Bold);

            List<RecipeData> producingRecipes = FindRecipesProducing(item);
            if (producingRecipes.Count == 0)
                CreateLabel(_popupBody, "원재료입니다 (별도의 제작 과정이 없습니다).", 13, FontStyle.Italic);

            for (int i = 0; i < producingRecipes.Count; i++)
            {
                RecipeData recipe = producingRecipes[i];
                if (recipe == null || recipe.inputs == null)
                    continue;

                string header = recipe.requiredFacility == FacilityType.None
                    ? "트럭 조립"
                    : $"{GetFacilityName(recipe.requiredFacility)} 가공";
                if (producingRecipes.Count > 1)
                    header += $" (방법 {i + 1}/{producingRecipes.Count})";
                CreateLabel(_popupBody, header, 14, FontStyle.Bold);

                foreach (RecipeIngredient ingredient in recipe.inputs)
                {
                    if (ingredient == null || ingredient.item == null)
                        continue;

                    List<ChainStep> chain = BuildBackwardChain(ingredient.item);
                    CreateChainLaneRow(_popupBody, chain, recipe, item);
                }
            }

            CreateUsedInSection(_popupBody, item);
        }

        private void CreateUsedInSection(RectTransform parent, ItemData item)
        {
            List<RecipeData> usingRecipes = FindRecipesUsing(item);
            if (usingRecipes.Count == 0)
            {
                CreateLabel(parent, "이 아이템으로 더 만들 수 있는 것이 없습니다 (최종 완제품).", 13, FontStyle.Italic);
                return;
            }

            CreateLabel(parent, "이 아이템의 활용처", 16, FontStyle.Bold);
            foreach (RecipeData r in usingRecipes)
            {
                if (r == null || r.output == null || r.output.item == null)
                    continue;

                RectTransform row = CreateHorizontalScrollRow(parent, 90f);
                CreateChainItemNode(row, item, false, () => NavigateToCodexItem(item));

                int altCount = FindRecipesProducing(r.output.item).Count;
                CreateArrowConnector(row);
                CreateChainFacilityNode(row, r.requiredFacility, altCount);
                CreateArrowConnector(row);

                ItemData resultItem = r.output.item;
                CreateChainItemNode(row, resultItem, false, () => NavigateToCodexItem(resultItem));
            }
        }

        private void CreateChainLaneRow(RectTransform parent, List<ChainStep> chain, RecipeData consumingRecipe, ItemData targetItem)
        {
            RectTransform row = CreateHorizontalScrollRow(parent, 90f);

            for (int i = 0; i < chain.Count; i++)
            {
                if (i > 0)
                {
                    int altCount = FindRecipesProducing(chain[i].item).Count;
                    CreateArrowConnector(row);
                    CreateChainFacilityNode(row, chain[i].viaRecipe.requiredFacility, altCount);
                    CreateArrowConnector(row);
                }

                ItemData nodeItem = chain[i].item;
                CreateChainItemNode(row, nodeItem, false, () => NavigateToCodexItem(nodeItem));
            }

            int altForTarget = FindRecipesProducing(targetItem).Count;
            CreateArrowConnector(row);
            CreateChainFacilityNode(row, consumingRecipe.requiredFacility, altForTarget);
            CreateArrowConnector(row);
            CreateChainItemNode(row, targetItem, true, () => NavigateToCodexItem(targetItem));
        }

        private void NavigateToCodexItem(ItemData item)
        {
            if (item == null)
                return;

            _codexSelectedItem = item;
            ShowPopup("트럭 거점", BuildTruckContent);
        }

        private static List<ChainStep> BuildBackwardChain(ItemData targetItem)
        {
            var chain = new List<ChainStep>();
            ItemData current = targetItem;
            for (int depth = 0; depth < 6 && current != null; depth++)
            {
                List<RecipeData> recipes = FindRecipesProducing(current);
                if (recipes.Count == 0)
                {
                    chain.Add(new ChainStep { item = current, viaRecipe = null });
                    break;
                }

                RecipeData primary = recipes[0];
                chain.Add(new ChainStep { item = current, viaRecipe = primary });

                if (primary.inputs == null || primary.inputs.Count != 1 ||
                    primary.inputs[0] == null || primary.inputs[0].item == null)
                    break;

                current = primary.inputs[0].item;
            }

            chain.Reverse();
            return chain;
        }

        private static List<RecipeData> FindRecipesProducing(ItemData item)
        {
            var result = new List<RecipeData>();
            if (item == null)
                return result;

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            foreach (RecipeData r in catalog.Recipes)
            {
                if (r != null && r.output != null && r.output.item == item)
                    result.Add(r);
            }

            return result;
        }

        private static List<RecipeData> FindRecipesUsing(ItemData item)
        {
            var result = new List<RecipeData>();
            if (item == null)
                return result;

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            foreach (RecipeData r in catalog.Recipes)
            {
                if (r == null || r.inputs == null)
                    continue;

                foreach (RecipeIngredient ing in r.inputs)
                {
                    if (ing != null && ing.item == item)
                    {
                        result.Add(r);
                        break;
                    }
                }
            }

            return result;
        }

        private static Color GetFacilityColor(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Campfire: return new Color(0.85f, 0.4f, 0.15f);
                case FacilityType.PrecisionCutter: return new Color(0.25f, 0.55f, 0.8f);
                case FacilityType.RollerPress: return new Color(0.5f, 0.7f, 0.3f);
                case FacilityType.None: return new Color(0.5f, 0.4f, 0.25f);
                default: return new Color(0.4f, 0.4f, 0.45f);
            }
        }

        private static string GetFacilityName(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.Campfire: return "모닥불";
                case FacilityType.PrecisionCutter: return "절삭기";
                case FacilityType.RollerPress: return "롤러 프레스기";
                case FacilityType.None: return "트럭 조립";
                default: return type.ToString();
            }
        }

        private RectTransform CreateHorizontalScrollRow(RectTransform parent, float height)
        {
            var wrapperGo = new GameObject("ChainRow", typeof(RectTransform), typeof(LayoutElement));
            wrapperGo.transform.SetParent(parent, false);
            wrapperGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, height);
            LayoutElement wle = wrapperGo.GetComponent<LayoutElement>();
            wle.preferredHeight = height;
            wle.minHeight = height;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(wrapperGo.transform, false);
            RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            RectTransform contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 0);
            contentRt.anchorMax = new Vector2(0, 1);
            contentRt.pivot = new Vector2(0, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup h = contentGo.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 0;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.padding = new RectOffset(4, 4, 4, 4);

            ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
            sr.content = contentRt;
            sr.viewport = viewportRt;
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 24f;

            WheelToHorizontalScroll wheel = scrollGo.AddComponent<WheelToHorizontalScroll>();
            wheel.scrollRect = sr;

            return contentRt;
        }

        private void CreateChainItemNode(RectTransform parent, ItemData item, bool highlighted, UnityEngine.Events.UnityAction onClick)
        {
            if (item == null)
                return;

            var go = new GameObject($"Node_{item.itemID}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(62f, 82f);

            Image bg = go.GetComponent<Image>();
            bg.color = highlighted ? new Color(0.95f, 0.85f, 0.35f, 0.35f) : new Color(0.15f, 0.16f, 0.2f, 0.9f);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 62;
            le.preferredHeight = 82;
            le.minWidth = 62;
            le.minHeight = 82;

            if (onClick != null)
            {
                Button btn = go.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(onClick);
            }

            if (highlighted)
            {
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.9f, 0.3f, 1f);
                outline.effectDistance = new Vector2(2f, 2f);
            }

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f);
            iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0, -4);
            iconRt.sizeDelta = new Vector2(46f, 46f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = item.icon;
            iconImg.color = item.icon != null ? Color.white : new Color(0.7f, 0.7f, 0.75f);
            iconImg.raycastTarget = false;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(go.transform, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 0);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0, 2);
            nameRt.sizeDelta = new Vector2(-2, 22);
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = uiFont;
            nameText.fontSize = 10;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = item.itemName;
            nameText.raycastTarget = false;
        }

        private void CreateChainFacilityNode(RectTransform parent, FacilityType type, int recipeAlternativeCount)
        {
            var go = new GameObject($"Facility_{type}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(76f, 82f);
            go.GetComponent<Image>().color = GetFacilityColor(type);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 76;
            le.preferredHeight = 82;
            le.minWidth = 76;
            le.minHeight = 82;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(go.transform, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0.4f);
            nameRt.anchorMax = new Vector2(1, 1f);
            nameRt.pivot = new Vector2(0.5f, 0.5f);
            nameRt.anchoredPosition = Vector2.zero;
            nameRt.sizeDelta = new Vector2(-4, 0);
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = uiFont;
            nameText.fontSize = 11;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = GetFacilityName(type);

            if (recipeAlternativeCount > 1)
            {
                var badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(Image));
                badgeGo.transform.SetParent(go.transform, false);
                RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
                badgeRt.anchorMin = new Vector2(0, 0);
                badgeRt.anchorMax = new Vector2(1, 0.4f);
                badgeRt.pivot = new Vector2(0.5f, 0f);
                badgeRt.anchoredPosition = new Vector2(0, 2);
                badgeRt.sizeDelta = new Vector2(-6, -2);
                badgeGo.GetComponent<Image>().color = new Color(0.95f, 0.85f, 0.25f, 0.9f);

                var badgeTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                badgeTextGo.transform.SetParent(badgeGo.transform, false);
                RectTransform btRt = badgeTextGo.GetComponent<RectTransform>();
                btRt.anchorMin = Vector2.zero;
                btRt.anchorMax = Vector2.one;
                btRt.offsetMin = Vector2.zero;
                btRt.offsetMax = Vector2.zero;
                Text bt = badgeTextGo.GetComponent<Text>();
                bt.font = uiFont;
                bt.fontSize = 9;
                bt.alignment = TextAnchor.MiddleCenter;
                bt.color = new Color(0.2f, 0.15f, 0.02f);
                bt.text = $"기타 {recipeAlternativeCount - 1}";
            }
        }

        private void CreateArrowConnector(RectTransform parent)
        {
            var go = new GameObject("Arrow", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 24;
            le.preferredHeight = 82;
            le.minWidth = 20;
            Text t = go.GetComponent<Text>();
            t.font = uiFont;
            t.fontSize = 18;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.6f, 0.6f, 0.65f);
            t.text = "→";
        }

        private void CreateCodexListEntry(RectTransform parent, ItemData item, UnityEngine.Events.UnityAction onClick)
        {
            var row = new GameObject($"CodexEntry_{item.itemID}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 60);

            Image bg = row.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.05f);
            Button btn = row.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);

            HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.padding = new RectOffset(10, 10, 6, 6);
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            LayoutElement rowLe = row.GetComponent<LayoutElement>();
            rowLe.preferredHeight = 60;
            rowLe.minHeight = 60;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(row.transform, false);
            LayoutElement iconLe = iconGo.GetComponent<LayoutElement>();
            iconLe.preferredWidth = 44;
            iconLe.preferredHeight = 44;
            iconLe.minWidth = 44;
            iconLe.minHeight = 44;
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = item.icon;
            iconImg.color = item.icon != null ? Color.white : new Color(0.7f, 0.7f, 0.75f);
            iconImg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textGo.transform.SetParent(row.transform, false);
            LayoutElement textLe = textGo.GetComponent<LayoutElement>();
            textLe.flexibleWidth = 1;
            textLe.preferredHeight = 44;
            Text text = textGo.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = 15;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = item.itemName;
            text.raycastTarget = false;
        }

        private static void GrantRawMaterialsToTruck(TruckInventory truckInv, int amount)
        {
            if (truckInv == null)
                return;

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            truckInv.AddItem(catalog.GetItem(ItemIds.Wood), amount);
            truckInv.AddItem(catalog.GetItem(ItemIds.Stone), amount);
            truckInv.AddItem(catalog.GetItem(ItemIds.IronOre), amount);
            truckInv.AddItem(catalog.GetItem(ItemIds.CopperOre), amount);
        }

        private RectTransform CreateScrollableGrid(RectTransform parent, string title, int columns, float height = 380f)
        {
            var containerGo = new GameObject(title + "Container", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            containerGo.transform.SetParent(parent, false);
            containerGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, height);
            VerticalLayoutGroup vlg = containerGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            LayoutElement containerLe = containerGo.GetComponent<LayoutElement>();
            containerLe.preferredHeight = height;
            containerLe.minHeight = height;

            CreateLabel(containerGo.GetComponent<RectTransform>(), title, 14, FontStyle.Bold);

            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
            scrollGo.transform.SetParent(containerGo.transform, false);
            scrollGo.GetComponent<LayoutElement>().flexibleHeight = 1;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            RectTransform contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            GridLayoutGroup grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(64f, 86f);
            grid.spacing = new Vector2(6f, 6f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
            sr.content = contentRt;
            sr.viewport = viewportRt;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 24f;

            return contentRt;
        }

        private void CreateCraftIconView(RectTransform parent, RecipeData recipe, bool canCraft, UnityEngine.Events.UnityAction onClick)
        {
            ItemData output = recipe.output != null ? recipe.output.item : null;
            if (output == null)
                return;

            var go = new GameObject($"Craft_{recipe.recipeID}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 86f);

            Image bg = go.GetComponent<Image>();
            bg.color = canCraft ? new Color(0.95f, 0.85f, 0.35f, 0.85f) : new Color(0.15f, 0.16f, 0.2f, 0.85f);

            Button button = go.GetComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);

            if (canCraft)
            {
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.95f, 0.4f, 1f);
                outline.effectDistance = new Vector2(3f, 3f);
            }

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f);
            iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0, -4);
            iconRt.sizeDelta = new Vector2(48f, 48f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.sprite = output.icon;
            iconImg.color = output.icon != null ? Color.white : new Color(0.7f, 0.7f, 0.75f);
            iconImg.raycastTarget = false;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(go.transform, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 0);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0, 2);
            nameRt.sizeDelta = new Vector2(-4, 22);
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = uiFont;
            nameText.fontSize = 10;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = canCraft ? new Color(0.15f, 0.13f, 0.02f) : Color.white;
            nameText.text = output.itemName;
            nameText.raycastTarget = false;
        }

        private void CreateSmallActionButton(RectTransform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("SmallAction", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(118f, 60f);

            Image img = go.GetComponent<Image>();
            img.color = color;
            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 118;
            le.preferredHeight = 60;
            le.minWidth = 100;
            le.minHeight = 50;

            CreateText(go.transform, "Label", label, 13, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Text t = go.GetComponentInChildren<Text>();
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            t.raycastTarget = false;
        }

        private void BuildCanvas()
        {
            if (_canvas != null)
                return;

            var canvasGo = new GameObject("CraftingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 우하단 플레이어 인벤토리 (3칸만 보이고 나머지는 드래그/휠 스크롤)
            GameObject playerPanel = CreatePanel(canvasGo.transform, "PlayerInventoryPanel",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-20f, 20f), new Vector2(280f, 150f), new Color(0f, 0f, 0f, 0.65f));

            CreateText(playerPanel.transform, "PlayerInventoryTitle", "플레이어 인벤토리", 16, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
                new Vector2(12, -6), new Vector2(-24, 22));

            GameObject playerScrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            playerScrollGo.transform.SetParent(playerPanel.transform, false);
            RectTransform playerScrollRt = playerScrollGo.GetComponent<RectTransform>();
            playerScrollRt.anchorMin = new Vector2(0, 0);
            playerScrollRt.anchorMax = new Vector2(1, 1);
            playerScrollRt.offsetMin = new Vector2(10, 10);
            playerScrollRt.offsetMax = new Vector2(-10, -28);

            GameObject playerViewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            playerViewportGo.transform.SetParent(playerScrollGo.transform, false);
            RectTransform playerViewportRt = playerViewportGo.GetComponent<RectTransform>();
            playerViewportRt.anchorMin = Vector2.zero;
            playerViewportRt.anchorMax = Vector2.one;
            playerViewportRt.offsetMin = Vector2.zero;
            playerViewportRt.offsetMax = Vector2.zero;
            playerViewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

            GameObject slotRootGo = new GameObject("Slots", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            slotRootGo.transform.SetParent(playerViewportGo.transform, false);
            _playerSlotRoot = slotRootGo.GetComponent<RectTransform>();
            _playerSlotRoot.anchorMin = new Vector2(0, 0);
            _playerSlotRoot.anchorMax = new Vector2(0, 1);
            _playerSlotRoot.pivot = new Vector2(0, 0.5f);
            _playerSlotRoot.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup h = slotRootGo.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.padding = new RectOffset(4, 4, 4, 4);

            ContentSizeFitter fit = slotRootGo.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect playerScroll = playerScrollGo.GetComponent<ScrollRect>();
            playerScroll.content = _playerSlotRoot;
            playerScroll.viewport = playerViewportRt;
            playerScroll.horizontal = true;
            playerScroll.vertical = false;
            playerScroll.movementType = ScrollRect.MovementType.Clamped;
            playerScroll.scrollSensitivity = 24f;

            WheelToHorizontalScroll wheelAdapter = playerScrollGo.AddComponent<WheelToHorizontalScroll>();
            wheelAdapter.scrollRect = playerScroll;

            // 인벤토리 옆 상시 노출 '제작' 버튼 (트럭까지 가지 않아도 조립 목록을 바로 연다)
            CreateButton(canvasGo.transform, "CraftPanelButton", "제작",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-312f, 20f), new Vector2(90f, 50f), new Color(0.3f, 0.42f, 0.55f), OpenCraftingPanel);

            // 중앙 팝업
            _popupRoot = CreatePanel(canvasGo.transform, "InteractionPopup",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560f, 640f), new Color(0.08f, 0.09f, 0.12f, 0.94f));

            _popupTitle = CreateText(_popupRoot.transform, "Title", "Popup", 24, TextAnchor.UpperCenter,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
                new Vector2(0, -12), new Vector2(-80, 36));

            _closeButton = CreateButton(_popupRoot.transform, "CloseButton", "X",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-12, -12), new Vector2(36, 36), new Color(0.55f, 0.2f, 0.2f), ClosePopup);

            CreateButton(_popupRoot.transform, "JournalButton", "도감",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-56, -12), new Vector2(70, 36), new Color(0.55f, 0.48f, 0.2f), OpenJournal);

            GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
            bodyGo.transform.SetParent(_popupRoot.transform, false);
            RectTransform bodyRect = bodyGo.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0, 0);
            bodyRect.anchorMax = new Vector2(1, 1);
            bodyRect.offsetMin = new Vector2(16, 16);
            bodyRect.offsetMax = new Vector2(-16, -56);
            bodyGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.02f);

            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(bodyGo.transform, false);
            _popupBody = contentGo.GetComponent<RectTransform>();
            _popupBody.anchorMin = new Vector2(0, 1);
            _popupBody.anchorMax = new Vector2(1, 1);
            _popupBody.pivot = new Vector2(0.5f, 1f);
            _popupBody.anchoredPosition = Vector2.zero;
            _popupBody.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup v = contentGo.GetComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = false;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.padding = new RectOffset(4, 4, 4, 4);

            ContentSizeFitter bodyFit = contentGo.GetComponent<ContentSizeFitter>();
            bodyFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bodyFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect scroll = bodyGo.GetComponent<ScrollRect>();
            scroll.content = _popupBody;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            _popupScrollRect = scroll;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text CreateText(
            Transform parent,
            string name,
            string message,
            int fontSize,
            TextAnchor anchor,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Text text = go.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = message;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void CreateLabel(RectTransform parent, string message, int fontSize, FontStyle style)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.text = message;
            text.alignment = TextAnchor.MiddleLeft;
            go.GetComponent<LayoutElement>().preferredHeight = style == FontStyle.Italic ? 28 : 32;
            go.GetComponent<LayoutElement>().minHeight = 24;
        }

        private RectTransform CreateRow(RectTransform parent)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            HorizontalLayoutGroup h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 6;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minHeight = 70;
            le.preferredHeight = 78;
            ContentSizeFitter fit = go.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.GetComponent<RectTransform>();
        }

        private void CreateInventorySlotView(Transform parent, ItemData item, int count, float size, UnityEngine.Events.UnityAction onClick = null)
        {
            var go = new GameObject($"Slot_{item.itemID}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size + 28);
            Image slotBg = go.GetComponent<Image>();
            slotBg.color = new Color(0.15f, 0.16f, 0.2f, 0.95f);
            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size + 28;
            le.minWidth = size;
            le.minHeight = size + 20;

            if (onClick != null)
            {
                Button slotButton = go.AddComponent<Button>();
                slotButton.targetGraphic = slotBg;
                slotButton.onClick.AddListener(onClick);
            }

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f);
            iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0, -4);
            iconRt.sizeDelta = new Vector2(size - 10, size - 10);
            Image iconImage = iconGo.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.sprite = item.icon;
            iconImage.color = item.icon != null ? Color.white : new Color(0.7f, 0.7f, 0.75f);

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(go.transform, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 0);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0, 2);
            nameRt.sizeDelta = new Vector2(-4, 26);
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = uiFont;
            nameText.fontSize = 11;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = $"{item.itemName}\nx{count}";
        }

        private void CreateActionButton(RectTransform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            Button button = CreateButton(parent, "Action", label,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0, 64), color, onClick);

            LayoutElement le = button.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 64;
            le.minHeight = 56;

            RectTransform rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, 64);

            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 14;
                text.alignment = TextAnchor.MiddleLeft;
                RectTransform tr = text.rectTransform;
                tr.offsetMin = new Vector2(12, 4);
                tr.offsetMax = new Vector2(-12, -4);
            }
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = color;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            CreateText(go.transform, "Label", label, 16, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            Text text = go.GetComponentInChildren<Text>();
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
