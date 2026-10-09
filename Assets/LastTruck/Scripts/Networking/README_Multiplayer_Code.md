# Last Truck 멀티플레이 로비 - 개발자용 코드 설명

기능/규칙 정리는 `README_Multiplayer_Features.md`를 보세요. 이 문서는 코드를 고치거나 이어서 개발할 사람을 위한 설명입니다.

- 엔진: Unity 6000.3.23f1
- 네트워크: Photon Fusion 2 (Assets/Photon, Host 모드 = `GameMode.Host` / `GameMode.Client`)
- UI: uGUI + TextMeshPro, 한글 폰트는 Pretendard (SIL OFL 1.1, 라이선스 파일 동봉)

---

## 1. 처음 세팅하는 법

1. 브랜치를 받고 Unity로 프로젝트를 연다 (컴파일 에러가 없는지 확인).
2. Photon App ID 확인: `Tools > Fusion > Realtime Settings` 에 Fusion App ID가 들어 있어야 한다.
3. 메뉴 **`LastTruck > Multiplayer > Build Menu Scene (UI 자동 생성)`** 실행. 아래가 자동으로 만들어진다.
   - `Assets/Scenes/Menu.unity` (Build Settings 0번으로 등록, Demo_01이 없으면 뒤에 추가)
   - `Assets/LastTruck/Multiplayer/Prefabs/LobbyPlayerEntry.prefab` (네트워크 프리팹)
   - `Assets/LastTruck/Multiplayer/Prefabs/UI/SessionListRow.prefab`, `LobbyPlayerRow.prefab`
   - `Assets/LastTruck/Multiplayer/Fonts/Pretendard-Regular SDF.asset` (한글 TMP 폰트, Dynamic)
   - 아이콘 PNG를 Sprite로 임포트 설정, Photon 리전 `kr` 고정(비어 있을 때만), Fusion 프리팹 테이블 재빌드
4. `Menu` 씬을 열고 Play.

5. 캐릭터 멀티플레이 설치: 메뉴 **`LastTruck > Multiplayer > 0. 캐릭터 멀티플레이 전체 설치 (1~3 한 번에)`** (자세한 내용은 8장).

> 에디터 스크립트(`Networking/Editor/MenuSceneBuilder.cs`)는 UI 생성용 도구일 뿐이라, 생성 후 지워도 게임에는 영향이 없다.
> 다만 다시 실행하면 Menu 씬과 로비 UI 프리팹을 **덮어쓰므로**, 손으로 레이아웃을 다듬기 시작했다면 다시 실행하지 말 것.

### 여러 명 테스트

- 같은 PC: 빌드(.exe)를 하나 만들고 에디터 + 빌드로 실행하거나, ParrelSync로 에디터 복제본을 띄운다.
- 자동 생성 닉네임은 저장하지 않으므로 같은 PC에서 여러 개 띄워도 이름이 서로 다르게 나온다 (직접 바꾼 닉네임은 PlayerPrefs에 저장되어 공유됨).

---

## 2. 파일 구성 (`Assets/LastTruck/Scripts/Networking/`)

| 파일 | 역할 |
|---|---|
| `GameLauncher.cs` | 네트워크 관리자. NetworkRunner 생성/종료, 로비 접속, 방 만들기/참가/나가기, 준비·시작, 호스트 이탈 감지, 씬 전환. `INetworkRunnerCallbacks` 구현. DontDestroyOnLoad 싱글톤 |
| `LobbyUIController.cs` | Menu 씬 화면(방 목록 / 방 만들기 / 비밀번호 / 대기실). GameLauncher 이벤트를 구독해서 그리기만 한다 |
| `SystemUI.cs` | 로딩 패널 + 팝업 큐. DontDestroyOnLoad 싱글톤이라 게임 씬에서도 쓸 수 있다 |
| `LobbyPlayerEntry.cs` | 방 참가자 1명의 네트워크 데이터(닉네임/방장/준비). `NetworkBehaviour` |
| `SessionListRow.cs` | 방 목록 한 줄 UI |
| `LobbyPlayerRow.cs` | 대기실 참가자 한 줄 UI |
| `LobbyRules.cs` | 규칙 상수 모음(인원, 글자 수, 비밀번호 자릿수, 타임아웃, 세션 프로퍼티 키) |
| `PlayerProfile.cs` | 로컬 닉네임 생성/검증/저장 |
| `NetworkInputData.cs` | 인게임 입력 구조체(이동 방향, 버튼). 게임플레이 네트워크 전환 때 사용 |
| `Editor/MenuSceneBuilder.cs` | Menu 씬·UI·프리팹 자동 생성 에디터 도구 |

---

## 3. 전체 구조

```
[Menu 씬]
 ├─ [GameLauncher]  (DontDestroyOnLoad) ── NetworkRunner 오브젝트를 만들고 버림 (러너는 1회용)
 ├─ SystemCanvas    (DontDestroyOnLoad) ── SystemUI: 로딩 / 팝업 큐 (sortingOrder 100)
 ├─ MenuCanvas ─ LobbyUIController ── GameLauncher.Instance 이벤트 구독
 └─ EventSystem, Main Camera

GameLauncher --(StateChanged / SessionListUpdated 이벤트)--> LobbyUIController
LobbyPlayerEntry --(static Changed 이벤트)--> LobbyUIController, GameLauncher(방장 닉네임 캐시)
어디서든 --> SystemUI.ShowLoading / HideLoading / Alert / Confirm (정적 함수)
```

- Menu 씬을 다시 로드하면 씬 안의 `[GameLauncher]`, `SystemCanvas`가 새로 생기지만, `Awake`에서 기존 인스턴스가 있으면 **새로 생긴 쪽이 스스로 파괴**된다.
  그래서 다른 스크립트는 인스펙터로 GameLauncher를 연결하지 말고 **`GameLauncher.Instance`** 로 접근해야 한다.

### 상태 머신 (`LauncherState`)

```
Offline ─(다시 시도)─> ConnectingLobby ─> InLobby ─┬─ CreateSession ─> CreatingSession ─> InRoom
                                                   └─ JoinSession   ─> JoiningSession  ─> InRoom
InRoom ─(호스트 StartGameAsHost / OnSceneLoadStart)─> StartingGame ─(OnSceneLoadDone)─> InGame
InRoom/InGame ─(RequestLeaveSession 또는 연결 끊김)─> Leaving ─> (Menu 씬 로드) ─> ConnectingLobby ...
실패/취소/타임아웃 ─> 방 목록으로 복귀(ConnectingLobby) + 팝업
```

`LobbyUIController.OnLauncherStateChanged`가 상태에 따라 방 목록/대기실 패널을 켜고 끈다.

---

## 4. 핵심 동작 설명

### 4-1. NetworkRunner 수명

- Fusion의 `NetworkRunner`는 **한 번 끊기면 재사용 불가**. 그래서 로비로 돌아갈 때마다 `ShutdownRunnerAsync()` → `CreateRunner()`로 새로 만든다.
- 로비 접속(`JoinSessionLobby(SessionLobby.ClientServer)`)에 쓴 러너로 그대로 `StartGame`을 호출해 방을 만들거나 들어간다 (Fusion 공식 패턴).
- 우리가 의도해서 끄는 러너는 `_runner`를 **먼저 null로 비우고** 끈다. 그래서 `OnShutdown`/`OnDisconnectedFromServer`에서 `runner != _runner`이면 "의도한 종료"로 보고 무시하고, 같으면 "예상치 못한 끊김"으로 처리한다.

### 4-2. 로딩 + 타임아웃 + 취소 (`AwaitWithTimeout`, `_operationId`)

- 모든 요청(로비 접속/방 만들기/방 참가)은 `SystemUI.ShowLoading`으로 화면을 막은 뒤 `AwaitWithTimeout`으로 기다린다.
- `Task.WhenAny(요청, 15초)` 중 타임아웃이 먼저 끝나면 실패 처리 (`LobbyRules.OperationTimeoutSeconds`).
- 작업마다 `BeginOperation()`으로 번호를 올린다. await 이후 번호가 달라져 있으면(취소/다른 작업 시작) 그 결과는 **버린다**. 로딩의 [취소] 버튼은 `CancelCurrentOperation()` → 번호 증가 + 러너 종료 + 로비 복귀.
- 실패 사유(`ShutdownReason`) → 한국어 메시지 변환은 `DescribeFailure()`.

### 4-3. 방 정보(세션 프로퍼티)와 비밀번호

- `SessionName`은 방을 구분하는 **고유 키**라서 GUID를 쓴다. 화면에 보이는 제목은 세션 프로퍼티 `title`로 따로 보낸다.
- 세션 프로퍼티: `title`(string), `pw`(int, 0=공개 1=비공개). 인원은 `SessionInfo.PlayerCount / MaxPlayers` 기본값을 쓴다.
- **비밀번호 자체는 세션 프로퍼티에 넣지 않는다** (로비의 누구나 읽을 수 있음).
  - 참가자: `StartGameArgs.ConnectionToken`에 UTF-8 비밀번호를 실어 보낸다.
  - 호스트: `OnConnectRequest`에서 자기 `_hostPassword`와 비교해 `request.Accept()` / `request.Refuse()`.
  - 거절당한 쪽은 `ShutdownReason.ConnectionRefused`를 받고 "비밀번호가 올바르지 않습니다"로 표시한다 (현재 호스트가 거절하는 이유는 비밀번호뿐).
- 1인 방은 `IsVisible = false`로 만들어 목록에서 숨긴다. 게임 시작 시 `SessionInfo.IsOpen = false`로 닫는다.

### 4-4. 대기실 참가자 목록 (`LobbyPlayerEntry`)

- 호스트가 `OnPlayerJoined`(호스트 자신 포함)마다 `LobbyPlayerEntry` 프리팹을 스폰한다. Input Authority = 해당 플레이어.
- `[Networked]` 값 `Nickname(NetworkString<_32>)`, `IsHost`, `IsReady`는 **호스트만 쓴다**. 클라이언트는 RPC로 요청한다.
  - `RPC_SetNickname(string)`: 스폰 직후 본인이 호출
  - `RPC_SetReady(NetworkBool)`: 준비 버튼
- 값이 바뀌면 `[OnChangedRender]` → 정적 이벤트 `LobbyPlayerEntry.Changed` → UI 갱신. 혹시 이벤트를 놓쳐도 대기실은 0.5초마다 다시 그린다.
- `Spawned()`에서 `Runner.MakeDontDestroyOnLoad`를 호출해 **게임 씬으로 넘어가도 유지**된다 (인게임에서 방장 닉네임 등을 쓰기 위함).
- 시작 버튼 조건: `GameLauncher.AreAllPlayersReady()` = 엔트리 수 ≥ 접속 인원 && 방장 외 전원 `IsReady`. 호스트가 누르는 순간 한 번 더 검사한다.

### 4-5. 게임 시작 / 씬 전환

- 호스트: `IsOpen = false` → `Runner.LoadScene(SceneRef.FromIndex(Demo_01), LoadSceneMode.Single)`.
- 모든 참가자에서 `OnSceneLoadStart` → 로딩 표시, `OnSceneLoadDone` → 활성 씬이 Demo_01이면 `InGame` + 로딩 해제.
- 씬 이름은 `[GameLauncher]` 인스펙터의 `menuSceneName`, `gameSceneName`으로 바꿀 수 있다 (Build Settings에 있어야 함).

### 4-6. 호스트 이탈 / 연결 끊김

- 클라이언트는 호스트가 사라지면 `OnDisconnectedFromServer` 또는 `OnShutdown`을 받는다 → `HandleUnexpectedDisconnect`.
- 끊긴 뒤에는 네트워크 오브젝트가 전부 사라져 방장 닉네임을 읽을 수 없으므로, **방에 있는 동안 `LobbyPlayerEntry.Changed`에서 방장 닉네임을 미리 캐시**해 둔다 (`CachedHostNickname`).
- 메시지: 참가자였으면 `호스트 접속 끊김: {닉네임}`, 방장 자신이 끊겼거나 로비에서 끊기면 `서버와의 연결이 끊어졌습니다.`
- 게임 씬에 있었다면 `SceneManager.LoadSceneAsync(Menu)` 후 로비에 재접속한다. 팝업은 SystemUI가 씬 전환에도 살아 있어서 그대로 보인다.
- `OnApplicationQuit`에서 러너를 정상 종료해, 방장이 창을 닫으면 참가자들이 바로 끊김을 알 수 있게 했다.

### 4-7. 팝업 큐 (`SystemUI`)

- `SystemUI.Alert(title, message, onClosed, confirmLabel)` / `SystemUI.Confirm(title, message, onYes, onNo, yesLabel, noLabel)`
- 요청은 `Queue`에 쌓이고 하나 닫히면 다음 것을 보여준다. 팝업 패널은 로딩 패널보다 뒤(위)에 있어서 항상 위에 보인다.
- `SystemUI.ShowLoading(message, onCancel)` - onCancel을 넘기면 취소 버튼이 보인다. `SystemUI.HideLoading()`.
- SystemUI가 없는 씬에서 호출해도 에러 없이 로그만 남는다.

---

## 5. 자주 할 작업

- **규칙 값 바꾸기**: `LobbyRules.cs` (최대 인원, 글자 수, 비밀번호 자릿수, 타임아웃 등).
- **인게임 "나가기" 버튼 만들기**: 버튼 OnClick에서 `GameLauncher.Instance.RequestLeaveSession()` 호출. 방장이면 확인 팝업이 자동으로 뜬다.
- **인게임에서 팝업/로딩 쓰기**: `SystemUI.Alert(...)`, `SystemUI.ShowLoading(...)`.
- **UI 모양 바꾸기**: Menu 씬/프리팹을 직접 수정하면 된다. 단, 그 뒤에 에디터 메뉴를 다시 실행하면 덮어써지니 주의.
- **캐릭터 추가**: 8-4 참고 (데이터 에셋 만들고 목록에 넣고 메뉴 1, 2 실행).

---

## 6. 현재 한계 / 다음 작업

- **인게임 동기화 1단계만 적용**: 플레이어 스폰/이동/회전/이동 애니메이션/카메라, 맵 시드. 전투·몬스터·낮밤·인벤토리·트럭은 아직 각자 로컬 (8-6 참고).
- 재호스팅(방장 넘기기), Photon Host Migration은 미구현. 지금은 방장이 나가면 전원 방 목록으로.
- `ConnectionRefused` = 비밀번호 틀림으로 표시하는데, 나중에 다른 거절 사유(강퇴 등)를 추가하면 메시지를 구분하는 방법(예: 입장 후 RPC로 사유 전달)이 필요하다.

---

## 7. 문제 해결

| 증상 | 원인 / 해결 |
|---|---|
| 한글이 □로 보임 | 한글 폰트 에셋 생성 실패. `Assets/LastTruck/Multiplayer/Fonts/Pretendard-Regular.ttf`가 있는지 확인 후 메뉴 재실행 |
| 서로의 방이 목록에 안 보임 | 리전이 다름(`Tools > Fusion > Realtime Settings`의 Fixed Region이 모두 `kr`인지), 또는 App Version이 다른 빌드끼리 |
| 방 만들 때 "lobbyPlayerEntryPrefab이 비어 있습니다" | 메뉴 재실행, 또는 `[GameLauncher]` 인스펙터에 `LobbyPlayerEntry.prefab` 연결 |
| 스폰 시 프리팹을 못 찾는다는 Fusion 에러 | `Tools > Fusion > Rebuild Prefab Table` 실행 |
| 게임 시작 시 "Build Settings에 등록되어 있지 않습니다" | `File > Build Profiles`의 씬 목록에 Menu, Demo_01이 있는지 확인 |
| Demo_01에서 바로 Play하면 로비가 없음 | 정상. 멀티플레이는 Menu 씬에서 시작해야 한다 |
| 게임 씬에 들어갔는데 캐릭터가 안 나옴 | 메뉴 `1. 네트워크 프리팹 생성` 실행 여부, `Assets/LastTruck/Multiplayer/Resources/CharacterCatalog.asset`의 캐릭터마다 networkPrefab이 연결됐는지, `Tools > Fusion > Rebuild Prefab Table` |
| 캐릭터 선택 창의 초상화가 비어 있음 | 메뉴 `2. 캐릭터 초상화 촬영`에서 [전체 촬영] |
| 초상화 구도가 이상함 (너무 멀다/잘린다) | 촬영 창에서 화각/아래로 담는 범위/머리 위 여백/구도 상하 이동을 바꾸고 다시 촬영 |
| 캐릭터가 땅에 파묻히거나 떠 있음 | `NetworkPlayer_Base.prefab`의 CharacterController Center/Height 조정 (모든 캐릭터에 적용됨) |

---

## 8. 캐릭터 선택 + 인게임 멀티플레이 1단계

### 8-1. 에디터 메뉴 (`LastTruck > Multiplayer`)

| 메뉴 | 하는 일 | 다시 실행하면 |
|---|---|---|
| **0. 캐릭터 멀티플레이 전체 설치** | 아래 1 → 2(전체 촬영) → 3을 한 번에 | 1은 빠진 것만, 2·3은 새로 |
| **1. 네트워크 프리팹 생성 (캐릭터/몬스터/게임 상태)** | `Resources/CharacterCatalog.asset` 생성(비어 있으면 모든 CharacterStatsData 등록), 캐릭터마다 원본 프리팹(modelPrefab) 자동 연결, `Prefabs/Characters/NetworkPlayer_Base.prefab` + 캐릭터별 Variant 생성, networkPrefab 연결, Fusion 프리팹 테이블 재빌드 | "빠진 것만 만들기" / "모두 다시 만들기" 선택 |
| **2. 캐릭터 초상화 촬영** | 원본 프리팹을 임시 씬에서 Idle 자세로 세워 정면 얼굴~어깨를 찍고 `Multiplayer/Portraits/*.png`(투명 배경) 저장 → portrait 연결. 구도/조명 값은 창에서 조절(이 PC에 저장) | 덮어씀 |
| **3. 캐릭터 선택 UI 설치 (Menu 씬)** | 기존 Menu 씬은 그대로 두고 `RoomPanel/MyCharacter`, `MenuCanvas/CharacterSelect`만 추가, `CharacterPortraitSlot.prefab` 생성, `LobbyPlayerRow.prefab`에 초상화/캐릭터 이름 추가 | 이 도구가 만든 것만 지우고 새로 만듦 |

### 8-2. 새 파일

| 파일 | 역할 |
|---|---|
| `Characters/CharacterCatalog.cs` | 캐릭터 목록 (ScriptableObject, Resources). **목록 순서 = 캐릭터 번호**, 네트워크로는 번호만 보낸다 |
| `Characters/CharacterSelectUI.cs` | 대기실 내 캐릭터 위젯 + 전체 화면 선택 창 (미리보기 → [변경] 확정 → `LobbyPlayerEntry.RPC_SetCharacter`) |
| `Characters/CharacterPortraitSlot.cs` | 선택 창의 초상화 버튼 하나 |
| `Gameplay/NetworkPlayer.cs` | 캐릭터의 신분증(닉네임/캐릭터 번호, `Local`, `All`). 스폰 시 내 캐릭터면 `LocalPlayerBinder` 실행, 남의 캐릭터면 입력 읽는 스크립트(PlayerInteract/PlayerInventory/WeaponController/PlayerAttack 등) 끄기 |
| `Gameplay/NetworkPlayerMovement.cs` | Fusion `NetworkCharacterController`로 이동, 이동 애니메이션(isRun/isWalk) |
| `Gameplay/LocalPlayerBinder.cs` | 내 캐릭터에 카메라(Cinemachine/CameraFollow), HP바, 인벤토리·제작·미니맵 UI(`CraftingSceneBootstrap.SetupLocalPlayer`), (임시) 몬스터 스포너 연결 |
| `Gameplay/PlayerSpawnArea.cs` | 스폰 위치 (씬에 놓인 싱글플레이 캐릭터 자리 주변 원 위, 바닥 높이 레이캐스트) |
| `Gameplay/NetworkMapSeed.cs` | 호스트가 정한 맵 시드 읽기 |
| `Editor/LobbyEditorUI.cs`, `NetworkCharacterBuilder.cs`, `CharacterPortraitCapture.cs`, `CharacterSelectInstaller.cs`, `MultiplayerCharacterSetup.cs` | 위 에디터 메뉴들 |

### 8-3. 고친 기존 파일 (최소한만)

| 파일 | 변경 |
|---|---|
| `CharacterStatsData.cs` | 필드 추가: `portrait`, `description`, `modelPrefab`, `networkPrefab`, 프로퍼티 `DisplayName` (기존 값은 그대로) |
| `PlayerMove.cs` | 네트워크 캐릭터(NetworkPlayerMovement가 붙은 경우)면 입력/물리를 건너뛰고 창구 역할만. `SetFacing()`(조준 방향 동기화용) 추가. **싱글플레이 동작은 그대로** |
| `CameraFollow.cs` | target이 비어 있으면 건너뜀, `LateUpdate`로 변경 |
| `CraftingSceneBootstrap.cs` | 멀티플레이면: 씬의 싱글플레이 캐릭터 제거(그 자리를 스폰 위치로), 플레이어 관련 연결은 `SetupLocalPlayer()`로 미룸, 연습용 허수아비 생성 안 함 |
| `MapGenerator.cs` | 멀티플레이면 호스트 시드로 난수 생성 (모두 같은 맵) |
| `WeaponController.cs` | 조준 회전 한 줄을 `PlayerMove.SetFacing()`으로 (멀티에서도 방향 동기화) |
| `TruckInteractable.cs` | 멀티플레이에서는 탑승을 막고 안내 팝업 (트럭 단계에서 교체) |
| `GameLauncher.cs` | 캐릭터 스폰(고른 캐릭터/닉네임, 한 프레임 뒤), 입력에 조준 방향 추가, 게임 시작 시 맵 시드 설정, `IsOnlineSession` |
| `LobbyPlayerEntry.cs` | `CharacterIndex`, `MapSeed`, `RPC_SetCharacter`, `Find()` |
| `LobbyPlayerRow.cs`, `LobbyUIController.cs`, `PlayerProfile.cs`, `NetworkInputData.cs` | 초상화 표시, 캐릭터 번호 저장, `FaceDirection` 입력 |

### 8-4. 캐릭터 추가하는 법 (15명까지 계획)

1. 캐릭터 원본 프리팹을 만든다 (모델 + Animator + `Character` 컴포넌트, stats에 새 CharacterStatsData 연결). 기존 캐릭터와 같은 애니메이터 파라미터(isRun/isWalk/doAttack/doHit/doDie)를 쓸 것.
2. `Assets/LastTruck/Multiplayer/Resources/CharacterCatalog.asset`의 목록 **맨 뒤에** 새 데이터를 넣는다 (중간에 끼우면 기존 번호가 밀린다).
3. 데이터의 `playerName`(표시 이름), `description`(설명)을 채운다.
4. 메뉴 `1. 네트워크 프리팹 생성`(빠진 것만) → `2. 캐릭터 초상화 촬영`.
   선택 창과 스폰은 목록을 읽어서 자동으로 반영된다 (UI 수정 필요 없음).

### 8-5. 인게임 흐름

1. 호스트 [게임 시작] → `LobbyPlayerEntry.MapSeed` 설정 → 씬 로드.
2. 모든 PC에서 Demo_01 로드 → `CraftingSceneBootstrap.Awake`(순서 -100)가 멀티플레이 감지: 씬의 싱글플레이 캐릭터(Army) 비활성+삭제, 그 위치를 `PlayerSpawnArea`에 기록, 월드만 준비.
   `MapGenerator.Start`가 같은 시드로 같은 맵 생성.
3. 호스트 `OnSceneLoadDone` → 한 프레임 뒤 참가자마다 `CharacterCatalog`에서 고른 캐릭터 프리팹 스폰 (Input Authority = 그 참가자, onBeforeSpawned에서 닉네임/번호 설정).
4. 각 PC에서 `NetworkPlayer.Spawned`: 내 캐릭터면 `LocalPlayerBinder`, 남의 캐릭터면 입력 스크립트 끄기.
5. 매 틱 `GameLauncher.OnInput`이 카메라 기준 이동 방향 + 걷기 + 조준 방향을 보내고, `NetworkPlayerMovement.FixedUpdateNetwork`가 호스트(와 본인 예측)에서 이동, `Render`에서 애니메이션.

### 8-6. 코드 스타일

- 길고 기능이 여러 개인 스크립트는 `#region`으로 비슷한 기능끼리 묶었다 (IDE에서 접어서 볼 수 있음).
- 한글이 깨져 있던 `PlayerStats.cs`는 수정하면서 UTF-8로 바꿨다.

---

## 9. 인게임 2단계: 전투 / 몬스터 / 낮밤 / 게임 오버

### 9-1. 에디터 메뉴 1번이 추가로 만드는 것

| 에셋 | 내용 |
|---|---|
| `Multiplayer/Prefabs/NetworkGameState.prefab` | NetworkObject + `NetworkGameState` |
| `Multiplayer/Prefabs/Monsters/Monster_Network.prefab` | `Mr.No/Monster/Monster.prefab`의 **Variant** + NetworkObject + NetworkTransform + `NetworkHealth` + `NetworkMonster` (원본 몬스터를 고치면 따라온다) |
| `Multiplayer/Resources/NetworkPrefabRegistry.asset` | 위 두 프리팹 연결 (GameLauncher/MonsterSpawner가 읽음) |
| `NetworkPlayer_Base.prefab` | 이미 있으면 새로 만들지 않고 `NetworkHealth`(플레이어, 사망 3초 뒤 사라짐)만 추가 |

### 9-2. 새 파일

| 파일 | 역할 |
|---|---|
| `Combat/NetworkHealth.cs` | 동기화 체력. 클라이언트는 `RPC_RequestDamage`로 요청 → 호스트가 적용 → `RPC_PlayHit`으로 모두에게 피격 연출. 팀킬 금지, 사망 시 `IsDead` + 몇 초 뒤 Despawn. `DiedOnHost`(킬 집계), `Died`(연출) |
| `Combat/INetworkHealthListener.cs` | 체력 변화를 화면에 반영하는 쪽 (PlayerStats, Damageable)이 구현 |
| `Game/NetworkGameState.cs` | 로딩 대기(모두 준비 or 30초) → `GameManager.BeginNetworkGame`, 낮/밤 값 복사(클라이언트는 `ApplyNetworkState`), 인원수 → `GameManager.PlayerCount`, 트럭 내구도 동기화, 게임 오버 판정/팝업 |
| `Game/NetworkPrefabRegistry.cs` | 게임 씬 네트워크 프리팹 목록 (Resources) |
| `Monsters/NetworkMonster.cs` | 호스트 전용 AI: 트럭 기본 추적, 인식 범위 안 가장 가까운 플레이어로 전환(히스테리시스), 공격. NavMeshAgent는 경로만 계산하고 위치는 네트워크 틱에서 옮긴다(NetworkTransform과 충돌 방지). 클라이언트에서는 에이전트/기존 MonsterAI 끔 |
| `Monsters/NetworkMonsterSpawning.cs` | MonsterSpawner용 창구: 호스트만 스폰, 네트워크 몬스터 Spawn, 스폰 기준 = 트럭, 킬 집계 연결 |
| `Editor/NetworkGamePrefabBuilder.cs` | 9-1의 에셋 생성 |

### 9-3. 고친 기존 파일

| 파일 | 변경 |
|---|---|
| `Combat/Damageable.cs` | `INetworkHealthListener` 구현. 멀티에서는 `TakeDamage`가 호스트에 요청만 하고, 빨간색 깜빡임/데미지 숫자는 `OnNetworkHit`에서 모두에게 |
| `PlayerStats.cs` | 같은 방식 + 팀킬 무시, HP바는 동기화 값으로. UTF-8 변환 |
| `GameManager.cs` | 멀티: 준비될 때까지 대기(`BeginNetworkGame`), 클라이언트는 미러(`SetNetworkMirror`/`ApplyNetworkState`로 같은 이벤트 발생), 인원 배율 `quotaMultiplierByPlayerCount`, 게임 오버 후 정지 |
| `MonsterSpawner.cs` | 멀티: 호스트만 스폰, 트럭 기준, `maxAliveMonsters`(기본 30), 네트워크 몬스터 사용 |
| `TruckInformation.cs` | `CurrentDurability`/`MaxDurability`/`IsBroken`, 클라이언트용 `ApplyNetworkDurability` |
| `NetworkPlayer.cs` | 로딩 완료 RPC, 사망 시 조작 스크립트 끄기, `IsAlive`/`Health` |
| `NetworkPlayerMovement.cs` | 사망/시작 전/게임 오버면 이동 불가 |
| `GameLauncher.cs` | 대기 화면, 게임 상태 스폰, 게임 오버 처리(방장이 먼저 나가도 "호스트 끊김" 팝업 안 띄움) |
| `LocalPlayerBinder.cs` | 임시 몬스터 스포너 연결 제거 (이제 호스트가 트럭 기준으로 스폰) |

### 9-4. 조절할 값 (인스펙터)

- `Monster_Network.prefab` > NetworkMonster: 인식 범위 12 / 포기 거리 15 / 사거리 1.5 / 플레이어 피해 10 / 트럭 피해 10 / 쿨다운 1.2
- `Monster_Network.prefab` > NetworkHealth: 사라지는 시간 0.3 (체력은 원본 Damageable의 maxHealth)
- `NetworkPlayer_Base.prefab` > NetworkHealth: 사망 후 사라지는 시간 3 (체력은 캐릭터 스탯 maxHealth)
- Demo_01 `GameManager` > 인원별 쿼터 배율, `MonsterSpawner` > 동시 생존 상한
- `NetworkGameState.prefab` > 로딩 대기 최대 시간 30

### 9-5. 아직 로컬 (다음 단계)

- 무기 연출(들고 있는 무기 모델, 조준 자세, 총구/화염/화살 궤적)은 쏜 사람 화면에만 보인다. **피해와 피격 연출은 동기화됨.**
- 채집/개인 인벤토리/시설 가공, 트럭 운전/탑승(멀티에서 막아 둠)/수리.

## 10. 인게임 3단계: 트럭 인벤토리 동기화 / UI 폰트 / 몬스터 버그 수정

### 10-1. 트럭 인벤토리 동기화
- `Game/NetworkTruckInventory.cs` (NetworkGameState 프리팹에 같이 붙음, 메뉴 1번이 추가)
  - 호스트: `TruckInventory.Changed` → 슬롯 목록(카탈로그 아이템 번호 + 개수, 최대 64칸)을 `[Networked]` 배열에 쓰고 `Revision++`.
  - 클라이언트: `Revision`이 바뀌면 `ItemStackInventory.ReplaceAll`로 통째로 덮어씀 → `Changed` → 열린 트럭 창 다시 그림.
  - 요청 RPC(클라 → 호스트): 넣기 / 꺼내기 / 제작 / 원재료+10. 결과 아이템은 `[RpcTarget]` RPC로 요청한 사람에게만 준다.
- `TruckInventorySync` (같은 파일): UI가 부르는 창구. 싱글/호스트는 바로 처리, 클라이언트는 요청만.
  - `MoveItems` / `AddAndCount`: `AddItem`이 공간 부족 시 일부만 넣고 false를 돌려주는 문제로 생기던 복사 버그 방지.
- `ItemIndexLookup`: 아이템/레시피 ↔ `ItemCatalog` 순서 번호.
- 고친 팀 파일: `GameUIController` (트럭 관련 넣기/꺼내기/제작 → `TruckInventorySync`, 트럭 창 자동 갱신 `LateUpdate`),
  `TruckInventory` (클라이언트는 기본 지급 안 함), `ItemStackInventory` (`ReplaceAll` 추가).

### 10-2. UI 폰트
- `UI/InGameFonts.cs`: 씬 로드 시 유니티 기본 폰트(Arial/LegacyRuntime)를 쓰는 legacy `Text`만 Pretendard로 교체.
  `GameUIController`가 코드로 만드는 글자도 같은 폰트. 폰트는 `NetworkPrefabRegistry.uiFont` (메뉴 1번이 연결 + Hinted Smooth 설정).

### 10-3. 몬스터가 안 보이는데 트럭이 파괴되던 버그
- 원인: 트럭의 `WheelCollider`는 `Collider.ClosestPoint`를 지원하지 않아 입력 좌표를 그대로 돌려줌 → 거리 0 →
  몬스터가 스폰 위치(트럭에서 15~25m, 화면 밖)에서 움직이지 않고 트럭을 공격.
- 수정: `NetworkMonster.ClosestPointOnTruck`에서 바퀴/트리거 제외, 지원 안 되는 콜라이더는 `bounds`로 계산,
  몸체 콜라이더가 없으면 렌더러 범위 사용. 스폰 시 위치/내비메시 로그(`logSpawn`).

## 11. 인게임 4단계: 트럭 / 무기 연출 / 특산물 채집 / 나가기 메뉴 / 동료 표시

메뉴 `LastTruck > Multiplayer > 1. 네트워크 프리팹 생성`을 다시 실행해야 아래 컴포넌트가 프리팹에 붙는다.
- NetworkGameState 프리팹: `NetworkTruck`, `NetworkGathering` 추가 / 플레이어 Base 프리팹: `NetworkWeaponVisuals` 추가
- NetworkPrefabRegistry: `tmpFont`(Pretendard SDF) 연결

### 11-1. 트럭 (`Truck/NetworkTruck.cs`)
- 씬의 기존 트럭을 그대로 쓴다 (트럭에 NetworkObject를 붙이지 않음). 좌석 `[Networked] NetworkArray<PlayerRef>(4)`, 0 = 운전석.
- 탑승: `TruckInteractable.Interact` → `NetworkTruck.RequestBoard()` → 호스트가 거리 확인 후 좌석 배정. 내리기/라이트/운전석 이동도 요청 RPC.
- 운전: `GameLauncher.OnInput`이 운전석이면 `TruckThrottle/TruckSteer/TruckBrake`를 보냄 → 호스트가
  `Runner.TryGetInputForPlayer`로 읽어 `TruckMove.SetExternalInput` (TruckMove에 `useExternalInput` 추가, 키보드 직접 읽기 끔).
- 위치: 호스트가 매 틱 `TruckPosition/Rotation` 기록(호스트 Rigidbody 보간 켬) → 클라이언트는 Rigidbody kinematic + 스냅샷 보간 + 한 번 더 스무딩, 바퀴 모델은 움직인 거리만큼 굴림.
- 탄 캐릭터: `NetworkPlayerMovement`가 이동 대신 좌석으로 Teleport(호스트), `CharacterController.detectCollisions = false`.
  `NetworkPlayer.Render`가 모델/콜라이더를 숨기고 내 캐릭터면 입력 스크립트를 끔(내리면 끈 것만 복구). 몬스터는 탄 사람을 무시.
- 죽거나 나간 사람 좌석은 호스트가 비움(죽은 캐릭터는 트럭 밖으로 옮김). 운전자가 내리면 라이트 끔.

### 11-2. 무기 연출 (`Combat/NetworkWeaponVisuals.cs`)
- `WeaponController`에 이벤트(HeldWeaponChanged/ChargingChanged/FlameChanged/ShotFired/ImpactSpawned/ArrowFired)와
  "보여주기 전용" 모드(`SetRemoteView`) 추가. 복제본은 입력을 읽지 않고 받은 상태로 활 장착/조준 자세/불꽃을 그린다.
- 상태(든 무기/차징/화염)는 `[Networked]`, 순간 이펙트(궤적/맞은 곳/화살)는 `RpcTargets.All, InvokeLocal = false` RPC.
- 복제 화살은 `ArrowProjectile.LaunchVisual`(피해 없음). `NetworkPlayer`는 복제본의 WeaponController를 끄지 않는다.

### 11-3. 특산물 채집 (`Game/NetworkGathering.cs`)
- `SpecialResourceNode.Interact` → `NetworkGathering.TryHandleGather`: 게임 시작 후 + 가방 공간(보너스 아이템 포함) 확인 → 호스트 요청.
- 호스트 `TryTakeHit`(보너스 굴림/횟수 차감/고갈) → 결과를 요청자에게만 `[RpcTarget]` RPC. 고갈/재생성은 전원에게 RPC.
- 노드는 위치로 찾는다 (같은 시드 → 같은 위치). `SpecialResourceNode.AllNodes`, `DepletionChanged` 추가.

### 11-4. 화면 (`UI/`)
- `InGameHud.Ensure()` (NetworkGameState.Spawned): `InGameMenu`(ESC → `GameLauncher.RequestLeaveFromGame`),
  `TeammateNameplates`(화면 캔버스 이름표 + NetworkHealth HP바), `MinimapTeammateMarkers`(MinimapUI에 공개 속성 추가).

