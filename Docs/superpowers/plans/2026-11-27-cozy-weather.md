# COZY 날씨·낮밤 시험 적용 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** KYI_TEST 씬에서 COZY 3가 하늘·해·달·빛·안개·비·번개·날씨 소리를 그리되, 시간과 날씨는 우리 GameManager/WeatherDirector를 따르게 하고, F10으로 기존 방식과 바꿔 가며 비교한다.

**Architecture:** 씬에는 `CozyWeatherBridge` 오브젝트 하나만 둔다. 플레이가 시작되면 브리지가 COZY 프리팹을 런타임에 만들어 DontDestroyOnLoad로 옮기고(우리 WorldChunkCuller 등록·에디터 시간 실행을 피함), COZY 시계를 멈춘 채 매 프레임 `CozyDayClock`이 계산한 시각과 WeatherDirector 날씨를 넣는다. COZY를 켠 동안 우리 해·달·낮밤·밤 Volume·PastelLook·비 파티클은 끄고, 끄면 전부 되돌린다.

**Tech Stack:** Unity 6000.3 URP, C# (Assembly-CSharp), COZY 3.6.23 (`DistantLands.Cozy`, `DistantLands.Cozy.Data`, 자동 참조 asmdef), Unity MCP(execute_code)로 검증.

**Spec:** `docs/superpowers/specs/2026-11-27-cozy-weather-design.md`

## Global Constraints
- 바꾸는 씬은 `Assets/Scenes/KYI_TEST.unity` 하나. MiningMapTest/Menu/빌드 씬 목록은 건드리지 않는다.
- 게임 규칙(GameManager, SurvivalDirector, 날씨 결정 규칙·패널티, 멀티플레이 코드)은 바꾸지 않는다. 공용 코드 수정은 `WeatherDirector.cs`의 정적 값 하나뿐.
- 기존 날씨/낮밤 코드는 지우지 않는다(끄기만).
- COZY의 `Tools/Cozy: Stylized Weather 3/Setup Scene` 메뉴는 쓰지 않는다(기존 방향광 삭제 대화상자).
- COZY 에셋(ScriptableObject 프로필)을 런타임에 직접 수정하지 않는다. 시계 정지는 `perennialProfile`의 런타임 복제본에 한다.
- 날씨 매핑(모두 `Packages/com.distantlands.cozy.core/Content/Resources/Profiles/Weather Profiles/` 아래):
  맑음 → `Mostly Clear.asset`, 짙은 안개 → `Dense Fog.asset`, 비 → `Legacy/Light Rain.asset`, 폭풍우 → `Legacy/Thunder Storm.asset`.
  (Legacy를 쓰는 이유: 새 Precipitation 프로필은 COZY 기온이 낮으면 눈으로 바뀐다. Legacy는 항상 비.)
- 날씨 전환 6초, 동틀녘 10초, 밤 시간 흐름 30초당 COZY 1시간, 전환 키 F10(`Debug.isDebugBuild`일 때만).
- 시간 매핑: 낮 08:00→16:30, 노을 →20:00, 밤 20:00→02:00(멈춤), 동틀녘 05:00→08:00. 끊기지 않고 앞으로만(02:00→05:00 건너뜀만 예외).
- 검증은 Unity MCP `execute_code`(codedom, C# 6: `out var`·`$""` 금지, `UnityEngine.Object.` 전체 이름). 플레이 중 컴파일 금지(먼저 stop).
- 커밋/푸시는 사용자가 직접 한다. 계획에 commit 단계 없음. 커밋 메시지에 Claude 표시 금지.
- 스크린샷·백업은 스크래치패드에만 둔다(프로젝트 폴더 금지).

## Review Focus
1. COZY 켠 채로 씬을 떠나거나 플레이를 멈춤 → `WeatherDirector.VisualsSuppressed=false`, `PastelLook.Enabled` 원래값, RenderSettings 원래값, COZY 인스턴스 파괴. (Task 4 Step 6)
2. F10을 여러 번 눌러도 COZY 인스턴스는 하나, 끌 때마다 우리 해·달·Volume이 원래 상태로. (Task 4 Step 5)
3. 날씨 키트로 COZY 켜기 전에 날씨를 골라 둔 경우 → 켜는 즉시 그 날씨가 적용(전환 0초). (Task 4 Step 4)
4. 밤이 3분보다 길어짐 → 02:00에서 멈추고 넘지 않음. (Task 1 Step 2)
5. GameManager 없음/네트워크 시작 대기 중 → 08:00 유지, 에러 없음. (Task 1 Step 2)

---

### Task 1: 시간 계산기 `CozyDayClock`

**Files:**
- Create: `Assets/LastTruck/Scripts/Cozy/CozyDayClock.cs`

**Interfaces:**
- Produces (namespace `LastTruck`, 일반 C# 클래스, MonoBehaviour 아님):
  - `public CozyDayClock(float dawnSeconds = 10f, float secondsPerNightHour = 30f)`
  - `public float NightFactor { get; }` — 0 낮 ~ 1 밤 (AtmosphereController와 같은 규칙)
  - `public float Hours { get; }` — 마지막으로 계산한 시각(0~24)
  - `public float Step(bool hasGame, bool isNight, float duskProgress, float dayElapsed01, float dt)` — COZY용 하루 비율(0~1, `Hours/24`)을 돌려준다.

규칙(Step 안):
- `hasGame=false` → NightFactor 0, Hours 8.
- target = isNight ? 1 : duskProgress. target ≥ NightFactor면 바로 따라가고, 작으면 `dt/dawnSeconds` 속도로 내려간다(동틀녘). 밤 경과 시간은 isNight 동안 누적, 낮이 되면 0으로.
- `h(d) = Lerp(8, 16.5, dayElapsed01)`.
  - 밤: `Hours = 20 + Min(nightElapsed / secondsPerNightHour, 6)` (최대 26 = 02:00), 반환은 `(Hours % 24)/24`.
  - 낮·동틀녘(NightFactor가 내려가는 중): `Hours = Lerp(h(d), 5, NightFactor)`.
  - 낮·해질녘(그 외): `Hours = Lerp(h(d), 20, NightFactor)`.

- [ ] **Step 1: `CozyDayClock.cs` 작성** — 위 시그니처와 규칙대로. 한국어 요약 주석(파일 머리)을 기존 스크립트 스타일로.
- [ ] **Step 2: 컴파일 후 execute_code로 검증** (플레이 모드 아님)

```csharp
var c = new LastTruck.CozyDayClock(10f, 30f);
var sb = new System.Text.StringBuilder();
c.Step(false, false, 0f, 0.5f, 0.1f); sb.Append("noGame=" + c.Hours + " ");                 // 기대 8
c.Step(true, false, 0f, 0f, 0.1f);    sb.Append("dayStart=" + c.Hours.ToString("F2") + " "); // 기대 8.00
c.Step(true, false, 0f, 1f, 0.1f);    sb.Append("dayEnd=" + c.Hours.ToString("F2") + " ");   // 기대 16.50
c.Step(true, false, 0.5f, 0.9f, 0.1f);sb.Append("dusk50 nf=" + c.NightFactor.ToString("F2") + " h=" + c.Hours.ToString("F2") + " "); // nf 0.50, h = Lerp(15.65,20,0.5)=17.83
c.Step(true, true, 1f, 1f, 0.1f);     sb.Append("night0=" + c.Hours.ToString("F2") + " ");   // 기대 20.00
for (int i = 0; i < 400; i++) c.Step(true, true, 1f, 1f, 1f);                                 // 400초 밤
float t = c.Step(true, true, 1f, 1f, 1f); sb.Append("longNight=" + c.Hours.ToString("F2") + " t=" + t.ToString("F4") + " "); // 기대 26.00, t=0.0833
c.Step(true, false, 0f, 0f, 0.1f);    sb.Append("dawnStart nf=" + c.NightFactor.ToString("F2") + " h=" + c.Hours.ToString("F2") + " "); // nf 0.99, h≈5.03
for (int i = 0; i < 120; i++) c.Step(true, false, 0f, 0f, 0.1f);                              // 12초
sb.Append("dawnEnd nf=" + c.NightFactor.ToString("F2") + " h=" + c.Hours.ToString("F2"));     // nf 0.00, h 8.00
return sb.ToString();
```
Expected: `noGame=8 dayStart=8.00 dayEnd=16.50 dusk50 nf=0.50 h=17.83 night0=20.00 longNight=26.00 t=0.0833 dawnStart nf=0.99 h=5.03 dawnEnd nf=0.00 h=8.00` (마지막 자리 ±0.01 허용), `EditorUtility.scriptCompilationFailed == false`.

### Task 2: WeatherDirector 화면 연출 끄기 값

**Files:**
- Modify: `Assets/LastTruck/Scripts/WeatherDirector.cs` (`ResetStatics` 32행, `Update`의 `_atmosphere` 블록 219–223행, `UpdateRain` 275행)

**Interfaces:**
- Produces: `public static bool VisualsSuppressed { get; set; }` — true면 비 파티클을 멈추고(`StopEmittingAndClear`), `_atmosphere.WeatherFog`/`LanternClear` 쓰기를 건너뛴다. `Kind`, 패널티 프로퍼티, 알림은 그대로.
- `ResetStatics`에서 false로 초기화.

- [ ] **Step 1: 위 정적 값 추가와 두 곳 분기 작성.**
- [ ] **Step 2: 컴파일 후 플레이(KYI_TEST)에서 검증** — `WeatherDirector.SetOverride(WeatherKind.Rain)` → 2초 뒤 `GameObject.Find("RainFx")`의 ParticleSystem `isPlaying == true` → `VisualsSuppressed = true` → 다음 호출에서 `isPlaying == false && particleCount == 0`, `WeatherDirector.Kind == Rain`, `PlayerSpeedScale < 1`. 끝나면 `VisualsSuppressed=false`, `SetOverride(null)`, 정지.
Expected: 위 조건 모두 true.

### Task 3: 연결 컴포넌트 `CozyWeatherBridge`

**Files:**
- Create: `Assets/LastTruck/Scripts/Cozy/CozyWeatherBridge.cs`

**Interfaces:**
- Consumes: `CozyDayClock`(Task 1), `WeatherDirector.VisualsSuppressed`·`WeatherDirector.Kind`(Task 2), `PastelLook.Enabled`/`PastelLook.Toggle()`, `GameManager.Instance`(`CurrentPhase`, `DuskProgress`, `DayTimeRemaining`, `DayDurationSeconds`, `IsWaitingForNetworkStart`), COZY: `CozyWeather`(`timeModule`, `weatherModule`), `CozyTimeModule.currentTime`(float 암시 변환)·`perennialProfile`, `CozyWeatherModule.ecosystem`(`weatherSelectionMode = CozyEcosystem.EcosystemStyle.manual`, `SetWeather(WeatherProfile, float)`), `CozyWeather.sunLight`, `CozyWeather.cozyCamera`(null이면 `Camera.main`을 넣는다).
- Produces (namespace `LastTruck`, `[DefaultExecutionOrder(-50)]` — COZY Update보다 먼저 시각을 넣는다):
  - Serialized: `bool useCozy = true`, `Volume nightVolume`, `WeatherProfile clearProfile, fogProfile, rainProfile, stormProfile`, `float weatherTransitionSeconds = 6f`, `float dawnSeconds = 10f`, `float secondsPerNightHour = 30f`.
  - `public bool CozyOn { get; }`, `public CozyWeather Cozy { get; }`(런타임 인스턴스), `public void SetCozy(bool on)`.

동작:
- `Start`: `Resources.Load<GameObject>("Cozy Prefabs/Cozy Weather Sphere")`를 Instantiate → `DontDestroyOnLoad` → 비활성 상태로 보관. `perennialProfile`을 `Instantiate`한 복제본에 `pauseTime = true`를 넣어 교체. AtmosphereController·DayNightController·씬의 방향광(COZY 인스턴스 아래 제외) 목록을 찾아 둔다. 그 뒤 `SetCozy(useCozy)`.
- `SetCozy(true)`: RenderSettings(ambientMode, ambientSky/Equator/GroundColor, ambientLight, ambientIntensity, fog, fogColor, fogDensity, fogMode, sun, defaultReflectionMode, skybox) 저장 → 우리 것 끔(AtmosphereController·DayNightController `enabled=false`, 방향광 각각 이전 `enabled` 저장 후 false, nightVolume `enabled` 저장 후 false, PastelLook이 켜져 있으면 `Toggle()` 하고 기억) → `WeatherDirector.VisualsSuppressed = true`, `Shader.SetGlobalFloat("_GameWeatherFog", 0)` → COZY 인스턴스 활성, ecosystem manual, 현재 `WeatherDirector.Kind` 프로필을 `SetWeather(p, 0f)`.
- `SetCozy(false)`: 역순으로 전부 복원(COZY 비활성, 저장한 enabled 값, PastelLook 다시 Toggle, `VisualsSuppressed=false`, RenderSettings 복원, AtmosphereController·DayNightController `enabled=true`).
- `Update`: `Debug.isDebugBuild && Input.GetKeyDown(KeyCode.F10)` → `SetCozy(!CozyOn)` + `HudToast.Show("COZY 켬 (F10)" / "COZY 끔 · 원래 날씨 (F10)")`. COZY 켬일 때: `Cozy.timeModule.currentTime = _clock.Step(...)`, `Shader.SetGlobalFloat("_GameNightFactor", _clock.NightFactor)`, `WeatherDirector.Kind`가 바뀌었으면 `SetWeather(p, weatherTransitionSeconds)`.
- `OnDestroy`: 켜져 있으면 `SetCozy(false)`, COZY 인스턴스와 프로필 복제본 Destroy.

- [ ] **Step 1: `CozyWeatherBridge.cs` 작성** (위 시그니처·동작). 한국어 요약 주석.
- [ ] **Step 2: 컴파일 확인** — 플레이 정지 상태에서 `refresh_unity`(force, compile request) → `scriptCompilationFailed == false`, `System.Type.GetType("LastTruck.CozyWeatherBridge, Assembly-CSharp") != null`.
Expected: 둘 다 true, 콘솔 error 0.

### Task 4: KYI_TEST 배치와 플레이 검증

**Files:**
- Modify: `Assets/Scenes/KYI_TEST.unity` (루트에 `CozyWeatherBridge` 오브젝트 1개 추가)

**Interfaces:**
- Consumes: Task 3 컴포넌트, KYI_TEST의 AtmosphereController가 가진 night Volume(직렬화 필드 `nightVolume`을 에디터 코드에서 `SerializedObject`로 읽어 연결).

- [ ] **Step 1: 백업** — KYI_TEST.unity를 스크래치패드에 `KYI_TEST.unity.bak2`로 복사.
- [ ] **Step 2: 배치·저장** — KYI_TEST를 열고(현재 씬이 dirty면 멈추고 사용자에게 묻기), 루트 `CozyWeatherBridge` 오브젝트 생성, 4개 프로필(Global Constraints 경로)과 nightVolume 연결, 저장. `git diff --stat`으로 씬 변경이 이 오브젝트뿐인지 확인.
- [ ] **Step 3: 시간 흐름 확인** — 플레이 → 5초 뒤: `Cozy != null`, `CozyOn`, 우리 방향광 모두 `enabled=false`, COZY `sunLight.enabled`, `RenderSettings.sun == Cozy.sunLight`. GameManager `DayTimeRemaining`을 리플렉션으로 줄여 해질녘/밤을 만들고, 낮·노을·밤·동틀녘마다 `Cozy.timeModule.currentTime`(float) 값과 스크린샷(`ScreenCapture`, 스크래치패드) 기록.
Expected: 시각이 매핑대로(낮 0.33~0.69, 노을→0.83, 밤 0.83→0.083, 동틀녘 0.21→0.33), 화면이 낮→노을→밤→새벽.
- [ ] **Step 4: 날씨 확인** — COZY 끈 상태에서 `SetOverride(Fog)` → F10 대신 `SetCozy(true)` → 즉시 `ecosystem.currentWeather.name == "Dense Fog"`. 이어서 Rain/Storm/Clear를 하나씩 `SetOverride` → 7초 뒤 currentWeather 이름이 `Light Rain`/`Thunder Storm`/`Mostly Clear`, 각 스크린샷. `GameObject.Find("RainFx")` 파티클 수 0.
Expected: 이름 일치, 우리 비 파티클 0.
- [ ] **Step 5: 켜고 끄기 확인** — 같은 위치에서 `SetCozy(false)` → 우리 방향광 enabled 상태가 켜기 전 저장값과 같음, AtmosphereController.enabled, `VisualsSuppressed=false`, `PastelLook.Enabled` 원래값, `RenderSettings.ambientMode`·`fog`·`sun` 원래값, COZY 인스턴스 `activeSelf=false`. `SetCozy(true)`/`false`를 3번 반복 후 `FindObjectsByType<CozyWeather>(FindObjectsInactive.Include)` 개수 1. 전후 스크린샷.
Expected: 모두 일치, 개수 1.
- [ ] **Step 6: 씬 떠나기 확인** — COZY 켠 채 `SceneManager.LoadScene("Menu")` → `CozyWeather` 개수 0, `VisualsSuppressed=false`, `PastelLook.Enabled` 원래값. 정지 후 Menu 플레이 → 콘솔 error 0.
- [ ] **Step 7: 성능** — KYI_TEST 플레이에서 COZY 켬/끔 각각 10초: 평균 FPS(execute_code 프레임 카운트), Windows 카운터 Unity GPU 3D %, nvidia-smi 전력·온도.
Expected: 둘 다 평균 55fps 이상. 수치를 표로 보고.
- [ ] **Step 8: 정리** — `Time.timeScale=1`, `SetOverride(null)`, 플레이 정지, 원래 열려 있던 씬(Menu) 다시 열기. 바꾼 파일과 확인/미확인 목록 보고.
