using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static LastTruck.Networking.EditorTools.LobbyEditorUI;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 메뉴 "LastTruck > Multiplayer > 3. 캐릭터 선택 UI 설치 (Menu 씬)".
    ///
    /// 이미 있는 Menu 씬(손으로 고친 내용 포함)은 그대로 두고, 아래만 "추가"한다. 다시 실행하면 이 도구가 만든 것만 새로 만든다.
    ///  - 대기실(RoomPanel) 왼쪽: 내 캐릭터 초상화 + 이름 + [캐릭터 변경] 버튼 (오브젝트 이름: MyCharacter)
    ///  - MenuCanvas 맨 위: 캐릭터 선택 창 (CharacterSelect / CharacterSelectPanel)
    ///  - 프리팹: CharacterPortraitSlot.prefab (선택 창의 초상화 버튼 하나)
    ///  - LobbyPlayerRow.prefab: 참가자 줄에 초상화 + 캐릭터 이름 추가
    /// </summary>
    public static class CharacterSelectInstaller
    {
        #region 메뉴 / 설치 흐름

        private const string DialogTitle = "Last Truck - 캐릭터 선택 UI";
        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
        private const string SlotPrefabPath = UIPrefabDir + "/CharacterPortraitSlot.prefab";

        private const string RoomWidgetName = "MyCharacter";
        private const string SelectRootName = "CharacterSelect";

        [MenuItem("LastTruck/Multiplayer/3. 캐릭터 선택 UI 설치 (Menu 씬)", priority = 103)]
        private static void InstallMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "플레이 모드에서는 실행할 수 없습니다.", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string result = Install();
            EditorUtility.DisplayDialog(DialogTitle, result, "확인");
        }

        public static string Install()
        {
            if (!System.IO.File.Exists(MenuScenePath))
            {
                return $"{MenuScenePath} 가 없습니다.";
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MenuScenePath)
            {
                scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            LobbyUIController lobby = Object.FindFirstObjectByType<LobbyUIController>(FindObjectsInactive.Include);
            if (lobby == null) return "Menu 씬에서 LobbyUIController를 찾지 못했습니다.";

            var lobbySo = new SerializedObject(lobby);
            var roomPanel = lobbySo.FindProperty("roomPanel").objectReferenceValue as GameObject;
            var rowPrefab = lobbySo.FindProperty("playerRowPrefab").objectReferenceValue as LobbyPlayerRow;
            if (roomPanel == null) return "LobbyUIController의 roomPanel이 비어 있습니다.";

            Transform canvas = lobby.transform;
            Load();
            EnsureFolder(UIPrefabDir);

            // 다시 실행할 때: 예전에 이 도구가 만든 것만 지운다.
            DestroyChild(roomPanel.transform, RoomWidgetName);
            DestroyChild(canvas, SelectRootName);

            var log = new List<string>();

            CharacterPortraitSlot slotPrefab = CreateSlotPrefab();
            log.Add($"- 프리팹: {SlotPrefabPath}");

            if (rowPrefab != null && PatchPlayerRowPrefab(rowPrefab)) log.Add("- 참가자 줄 프리팹에 초상화/캐릭터 이름 추가");

            RoomWidget widget = BuildRoomWidget(roomPanel.transform);
            log.Add("- 대기실: 내 캐릭터 + [캐릭터 변경] 버튼");

            BuildSelectPanel(canvas, slotPrefab, widget);
            log.Add("- 캐릭터 선택 창 (오버워치/발로란트 스타일)");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            return "설치했습니다.\n\n" + string.Join("\n", log) +
                   "\n\n초상화가 비어 보이면 '2. 캐릭터 초상화 촬영'을 실행하세요.";
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName) Object.DestroyImmediate(child.gameObject);
            }
        }

        #endregion

        #region 초상화 슬롯 프리팹

        private static CharacterPortraitSlot CreateSlotPrefab()
        {
            GameObject root = UIObject("CharacterPortraitSlot", null);
            RT(root).sizeDelta = new Vector2(140f, 170f);

            // 선택(미리보기) 테두리: 배경보다 조금 크게, 배경 뒤에
            GameObject frame = UIObject("PreviewFrame", root.transform);
            Stretch(RT(frame), -5f, -5f, -5f, -5f);
            AddImage(frame, AccentColor, RoundSprite).raycastTarget = false;

            GameObject background = UIObject("Background", root.transform);
            Stretch(RT(background));
            Image backgroundImage = AddImage(background, RowColor, RoundSprite);

            GameObject portrait = UIObject("Portrait", root.transform);
            Stretch(RT(portrait), 8f, 8f, 8f, 40f);
            Image portraitImage = AddImage(portrait, Color.white);
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            TextMeshProUGUI nameText = AddLabel(root.transform, "Name", "캐릭터", 20, TextColor, TextAlignmentOptions.Center,
                new Vector2(0f, 20f), new Vector2(130f, 34f), anchor: new Vector2(0.5f, 0f));

            // 지금 쓰고 있는 캐릭터 표시 (오른쪽 위 체크)
            GameObject equipped = UIObject("EquippedMark", root.transform);
            Place(RT(equipped), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(30f, 30f));
            AddImage(equipped, ReadyColor, RoundSprite).raycastTarget = false;
            GameObject check = UIObject("Check", equipped.transform);
            Stretch(RT(check), 5f, 5f, 5f, 5f);
            Image checkImage = AddImage(check, Color.white);
            checkImage.sprite = LoadIcon("icon_check.png");
            checkImage.preserveAspect = true;
            checkImage.raycastTarget = false;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = backgroundImage;
            ApplyButtonColors(button);

            CharacterPortraitSlot slot = root.AddComponent<CharacterPortraitSlot>();
            Wire(slot,
                ("button", button),
                ("portraitImage", portraitImage),
                ("nameText", nameText),
                ("previewFrame", frame),
                ("equippedMark", equipped));

            frame.SetActive(false);
            equipped.SetActive(false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CharacterPortraitSlot>();
        }

        #endregion

        #region 참가자 줄 프리팹 패치

        private static bool PatchPlayerRowPrefab(LobbyPlayerRow rowPrefab)
        {
            string path = AssetDatabase.GetAssetPath(rowPrefab);
            if (string.IsNullOrEmpty(path)) return false;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                LobbyPlayerRow row = contents.GetComponent<LobbyPlayerRow>();
                if (row == null) return false;

                DestroyChild(contents.transform, "Portrait");
                DestroyChild(contents.transform, "CharacterName");

                GameObject portrait = UIObject("Portrait", contents.transform);
                Place(RT(portrait), new Vector2(0f, 0.5f), new Vector2(118f, 0f), new Vector2(66f, 66f));
                Image portraitImage = AddImage(portrait, Color.white);
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;

                TextMeshProUGUI characterName = AddLabel(contents.transform, "CharacterName", "캐릭터", 22, SubTextColor,
                    TextAlignmentOptions.MidlineRight, new Vector2(-305f, 0f), new Vector2(170f, 50f),
                    anchor: new Vector2(1f, 0.5f));

                // 닉네임 칸을 초상화/캐릭터 이름 자리만큼 줄인다.
                Transform nickname = contents.transform.Find("Nickname");
                if (nickname is RectTransform nickRt && nickRt.anchorMin == Vector2.zero && nickRt.anchorMax == Vector2.one)
                {
                    nickRt.offsetMin = new Vector2(162f, nickRt.offsetMin.y);
                    nickRt.offsetMax = new Vector2(-400f, nickRt.offsetMax.y);
                }

                Wire(row, ("portraitImage", portraitImage), ("characterNameText", characterName));
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        #endregion

        #region 대기실: 내 캐릭터

        private struct RoomWidget
        {
            public Image Portrait;
            public TextMeshProUGUI Name;
            public Button OpenButton;
        }

        private static RoomWidget BuildRoomWidget(Transform roomPanel)
        {
            GameObject root = UIObject(RoomWidgetName, roomPanel);
            Place(RT(root), new Vector2(-690f, 50f), new Vector2(280f, 450f));
            AddImage(root, PanelColor, RoundSprite).raycastTarget = false;

            AddLabel(root.transform, "Header", "내 캐릭터", 26, SubTextColor, TextAlignmentOptions.Center,
                new Vector2(0f, 190f), new Vector2(240f, 40f));

            GameObject portraitBg = UIObject("PortraitBackground", root.transform);
            Place(RT(portraitBg), new Vector2(0f, 40f), new Vector2(230f, 230f));
            AddImage(portraitBg, RowColor, RoundSprite).raycastTarget = false;

            GameObject portrait = UIObject("Portrait", portraitBg.transform);
            Stretch(RT(portrait), 6f, 6f, 6f, 6f);
            Image portraitImage = AddImage(portrait, Color.white);
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            TextMeshProUGUI name = AddLabel(root.transform, "CharacterName", "캐릭터", 30, TextColor,
                TextAlignmentOptions.Center, new Vector2(0f, -105f), new Vector2(260f, 50f), bold: true);

            Button open = MakeButton(root.transform, "ChangeCharacterButton", "캐릭터 변경",
                new Vector2(0f, -175f), new Vector2(240f, 64f), ButtonColor, out _, 26f);

            return new RoomWidget { Portrait = portraitImage, Name = name, OpenButton = open };
        }

        #endregion

        #region 캐릭터 선택 창

        private static void BuildSelectPanel(Transform canvas, CharacterPortraitSlot slotPrefab, RoomWidget widget)
        {
            // 항상 켜져 있는 부모(스크립트가 여기 붙음) + 열고 닫는 전체 화면 패널
            GameObject root = UIObject(SelectRootName, canvas);
            Stretch(RT(root));
            root.transform.SetAsLastSibling();

            GameObject panel = UIObject("CharacterSelectPanel", root.transform);
            Stretch(RT(panel));
            AddImage(panel, new Color(BgColor.r, BgColor.g, BgColor.b, 0.97f)); // 뒤쪽 클릭 차단
            Transform p = panel.transform;

            AddLabel(p, "Title", "캐릭터 선택", 42, TextColor, TextAlignmentOptions.Center,
                new Vector2(0f, 495f), new Vector2(800f, 64f), bold: true);
            Button close = MakeButton(p, "CloseButton", "닫기 (ESC)", new Vector2(830f, 495f), new Vector2(200f, 60f),
                ButtonColor, out _, 24f);

            // 가운데: 큰 초상화
            GameObject previewBg = UIObject("PreviewBackground", p);
            Place(RT(previewBg), new Vector2(-330f, 230f), new Vector2(400f, 400f));
            AddImage(previewBg, PanelColor, RoundSprite).raycastTarget = false;
            GameObject preview = UIObject("PreviewPortrait", previewBg.transform);
            Stretch(RT(preview), 10f, 10f, 10f, 10f);
            Image previewImage = AddImage(preview, Color.white);
            previewImage.preserveAspect = true;
            previewImage.raycastTarget = false;

            // 가운데 오른쪽: 이름 / 설명 / 능력치
            TextMeshProUGUI previewName = AddLabel(p, "PreviewName", "캐릭터 이름", 54, AccentColor,
                TextAlignmentOptions.MidlineLeft, new Vector2(250f, 390f), new Vector2(720f, 72f), bold: true);
            TextMeshProUGUI previewDescription = AddLabel(p, "PreviewDescription", "캐릭터 설명", 26, TextColor,
                TextAlignmentOptions.TopLeft, new Vector2(250f, 285f), new Vector2(720f, 140f));
            previewDescription.overflowMode = TextOverflowModes.Ellipsis;

            string[] statLabels = CharacterSelectUI.StatLabels;
            var fills = new Object[statLabels.Length];
            var values = new Object[statLabels.Length];
            for (int i = 0; i < statLabels.Length; i++)
            {
                float y = 170f - i * 38f;
                AddLabel(p, "StatLabel" + i, statLabels[i], 24, SubTextColor, TextAlignmentOptions.MidlineLeft,
                    new Vector2(-15f, y), new Vector2(190f, 34f));

                GameObject barBg = UIObject("StatBar" + i, p);
                Place(RT(barBg), new Vector2(255f, y), new Vector2(330f, 16f));
                AddImage(barBg, FieldColor, RoundSprite).raycastTarget = false;

                GameObject fill = UIObject("Fill", barBg.transform);
                RectTransform fillRt = RT(fill);
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = new Vector2(0.5f, 1f);
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
                AddImage(fill, AccentColor, RoundSprite).raycastTarget = false;
                fills[i] = fillRt;

                values[i] = AddLabel(p, "StatValue" + i, "0", 24, TextColor, TextAlignmentOptions.MidlineRight,
                    new Vector2(485f, y), new Vector2(110f, 34f), bold: true);
            }

            // 가운데 아래: 변경 버튼
            Button confirm = MakeButton(p, "ConfirmButton", "변경", new Vector2(0f, -95f), new Vector2(360f, 80f),
                AccentColor, out TextMeshProUGUI confirmLabel, 32f);

            // 아래: 초상화 목록 (8칸씩 줄바꿈, 많아지면 세로 스크롤)
            GameObject scrollRoot = UIObject("PortraitList", p);
            Place(RT(scrollRoot), new Vector2(0f, -340f), new Vector2(1300f, 390f));
            AddImage(scrollRoot, PanelColor, RoundSprite);
            ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            GameObject viewport = UIObject("Viewport", scrollRoot.transform);
            Stretch(RT(viewport), 6f, 6f, 6f, 6f);
            AddImage(viewport, Clear);
            viewport.AddComponent<RectMask2D>();

            GameObject content = UIObject("Content", viewport.transform);
            RectTransform contentRt = RT(content);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(140f, 170f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(14, 14, 14, 14);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 8;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = RT(viewport);
            scroll.content = contentRt;

            TextMeshProUGUI empty = AddLabel(scrollRoot.transform, "EmptyMessage",
                "캐릭터 목록이 비어 있습니다.\n'1. 네트워크 프리팹 생성'을 실행하세요.", 26, SubTextColor,
                TextAlignmentOptions.Center, Vector2.zero, new Vector2(900f, 120f));
            empty.gameObject.SetActive(false);

            panel.SetActive(false);

            CharacterSelectUI ui = root.AddComponent<CharacterSelectUI>();
            Wire(ui,
                ("selectPanel", panel),
                ("slotContainer", content.transform),
                ("slotPrefab", slotPrefab),
                ("previewPortrait", previewImage),
                ("previewNameText", previewName),
                ("previewDescriptionText", previewDescription),
                ("confirmButton", confirm),
                ("confirmLabel", confirmLabel),
                ("closeButton", close),
                ("emptyMessage", empty),
                ("roomPortrait", widget.Portrait),
                ("roomCharacterNameText", widget.Name),
                ("openButton", widget.OpenButton));
            WireArray(ui, "statFills", fills);
            WireArray(ui, "statValueTexts", values);
        }

        #endregion
    }
}
