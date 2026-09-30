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
- **네트워크 캐릭터 스폰 켜기**: `[GameLauncher]`의 `spawnNetworkCharacters` 체크 + `characterPrefabs`에 NetworkObject(+NetworkTransform)가 붙은 캐릭터 프리팹 등록.
  **주의: PlayerMove/PlayerAttack/PlayerStats가 NetworkBehaviour로 바뀌기 전에는 켜지 말 것** (지금 스크립트는 로컬 키보드 입력을 그대로 읽기 때문에 모든 PC에서 모든 캐릭터가 같이 움직인다).

---

## 6. 현재 한계 / 다음 작업

- **인게임 동기화 미적용**: 이 브랜치의 게임플레이 스크립트(PlayerMove 등)는 아직 싱글플레이용이다. 지금은 로비 → 같은 타이밍에 Demo_01 진입까지만 네트워크로 묶여 있고, 게임 씬 안에서는 각자 로컬로 돌아간다.
  다음 단계: 캐릭터 이동/애니메이션/전투를 NetworkBehaviour(호스트 권한) 구조로 전환 → `spawnNetworkCharacters` 켜기. 입력은 `GameLauncher.OnInput`이 이미 `NetworkInputData`로 보내고 있다(InGame 상태일 때만).
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
