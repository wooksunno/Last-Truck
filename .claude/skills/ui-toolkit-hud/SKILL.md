---
name: ui-toolkit-hud
description: Last Truck의 화면 UI(HUD, 알림, 임무 카드, 트럭 창 탭, 원형 메뉴)를 새로 만들거나 고칠 때 사용. 게임과 어울리는 스킨(갈색 9-slice 패널, 금색/크림 글자, SDF 글꼴), 화면 배치 지도, 트럭 창에 새 탭 추가하는 방법, OnGUI 대신 UI Toolkit을 쓰는 이유.
---

# UI Toolkit HUD (Last Truck)

새 UI는 **UI Toolkit**으로 만든다. `OnGUI`는 HUD가 준비되지 않았을 때(`LastTruckHud.IsReady == false`)의 대체 표시로만 쓴다.
사용자는 "게임이랑 안 맞는 이질감"에 민감하다 → 아래 스킨/색/글꼴을 그대로 따르고, 만든 뒤 반드시 스크린샷으로 확인한다.

## 기본 재료
- HUD 본체: `Assets/LastTruck/Scripts/UI/LastTruckHud.cs` (게임 시작 때 자동 생성, 문서 두 개: HUD(팝업 뒤) / Toast(팝업 앞)).
  - 새 HUD 부품은 `MissionHud`, `AchievementHud`처럼 **클래스로 만들고 `LastTruckHud.Build()`에서 `Build(_hudRoot)`, `Update()`에서 `Tick()`** 을 부른다.
- 스킨: `LastTruckUiSkin.Get()` → `panelBrownDark`, `panelBrown`, `panelGreyBolts`, `buttonRed`, `buttonBrown` 스프라이트, `font`.
  - 9-slice 입히기: `LastTruckHud.Skin(element, sprite, 0.5f, tint)` 또는 `UiToolkitSkin.Slice(...)`. HUD 카드 표준 틴트 `new Color(0.7f, 0.6f, 0.55f, 0.97f)`.
- 글꼴: `LastTruckHud.SharedFont` (SDF, 한글 동적). Label마다 `style.unityFontDefinition`을 꼭 넣는다. 굵게 + 외곽선 `unityTextOutlineWidth 0.8~1.6`, 색 `(0.15,0.09,0.03,0.95)`.
- 색: 금 `(1, 0.8, 0.34)` 제목/강조, 크림 `(0.97, 0.93, 0.82)` 본문, 초록 `(0.4~0.45, 1, 0.62)` 완료, 빨강 `(1, 0.4, 0.36)` 실패, 흐린 글 `(0.78, 0.72, 0.62)`.
- 그림: `UiIcons.Get(name)` (`Resources/UI/Icons/`: heart bolt truck sun moon crate hammer gear book upgrade shield speed bulb sword crosshair turret rate burst engine bumper plate lock plus check close grid sortname sortcount star). 없으면 글자로 대체.
- 해상도 기준: PanelSettings 1920×1080, 화면 크기에 맞춰 늘어난다 → 픽셀 값은 1920×1080 기준으로 쓴다.
- USS: `Assets/Resources/UI/LastTruckHud.uss`(`lt-*`), `LastTruckWindow.uss`(트럭 창 `tw-*`). 코드에서 `style.*`로 직접 줘도 된다.

## 화면 배치 지도 (겹치지 않게)
| 자리 | 쓰는 것 |
|---|---|
| 위 가운데 | 낮/밤 시계 (`DayNightBadge`), 그 아래 top≈150에 **도전과제 달성 배너** |
| 오른쪽 위 | 시계 다이얼, 미니맵, 스태미나 |
| 오른쪽 가운데 | 키 안내 (`MakeKeyHint`: 탐색 E / 공격 클릭 / 임무 확인 J / 생존 임무 K) |
| 오른쪽 아래 | 인벤토리 핫바 |
| 왼쪽 아래 | 생존 임무 카드 (`MissionHud`, K로 열고 닫음, 0.84배) |
| 아래 가운데 | 무전 대화창 (bottom 40, 폭 720, 갈색 판), 토스트 알림 `HudToast.Show(text, 초)` (bottom 190) |
| 왼쪽 위 | 트럭 내구도 막대 |
- 큰 목록/관리 화면은 HUD에 띄우지 말고 **트럭 창의 탭**으로 만든다 (사용자가 도전과제 목록을 그렇게 옮겨 달라고 했다).
- 팝업(트럭 창)이 열려 있으면 HUD 카드는 숨긴다: `GameUIController.Instance.IsPopupOpen`.

## 트럭 창(TruckStorageView)에 새 탭 추가
트럭 창 = `Assets/LastTruck/Scripts/UI/TruckStorageView*.cs` (partial, 탭마다 파일 하나) + 열고 닫는 상태는 `Assets/Scripts/Crafting/GameUIController*.cs`.
1. `GameUIController.cs`
   - `TruckTabLabels`, `TabIconNames`, `TruckTabColors`에 항목 추가 (번호 = 배열 순서. 원형 메뉴와 번호가 같아야 하므로 **중간에 끼워 넣지 말고 끝에 추가**).
   - 책갈피 위아래 순서는 `TruckTabOrder`로 바꾼다 (예: 도전과제(5번)를 도감(3번) 바로 아래에).
   - `_truckXxxOpen` 플래그 추가 → 다른 탭을 열 때 모두 false로 (`_truckUpgradeOpen = false;`가 있는 곳마다 같이), `CurrentTruckTab()`, `OnTruckTab()` switch, `BuildTruckContent()` 분기, `PopupSizeForState()`.
   - `_truckTabRoots`/`_truckTabBgs` 배열 크기도 늘린다.
2. `GameUIController.CodexUpgradeToolkit.cs`(또는 새 partial)에 `OpenTruckXxxPanel()` + `BuildTruckXxxPanel()`: `EnsureToolkitWindow()` → `_storageView.ShowXxx(model)` → `_storageToolkitActive = true`. 탭 목록은 `BuildToolkitTabs(inv, 번호)`.
3. `TruckStorageView.Xxx.cs`: `BeginWindow("모드이름", "제목", onClose)` → 내용 → `_hoverLabel = MakeLabel(설명, "tw-hint")` → `EndWindow(m.tabs)`. 스크롤 목록은 `_gridScroll`에 넣으면 다시 그려도 스크롤 위치가 유지된다.
4. 원형 메뉴(트럭 옆 Tab): `LastTruck/Scripts/UI/TruckRadialMenu.cs`의 `Count`, `Names`도 함께 늘린다.

## 움직임
- 등장: `translate` + `scale` + `opacity`를 `Tick`에서 직접 보간 (`EaseOutBack` 0.4초 정도). 사라짐 0.3~0.5초.
- 진행 막대는 목표값으로 `MoveTowards`/지수 보간해서 차오르게.
- 달성/완료 알림은 큐에 넣고 하나씩 보여 준다 (`AchievementHud` 참고).

## 확인
- 플레이 중 `ScreenCapture.CaptureScreenshot`으로 HUD 포함 화면을 찍어 겹침·가독성 확인 (`unity-playtest` 스킬).
- 낮/밤 둘 다, 팝업이 열린 상태도 확인. 글자 크기는 1920×1080에서 본문 16px 이상.
