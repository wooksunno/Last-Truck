---
name: unity-playtest
description: Last Truck을 Unity MCP로 컴파일하고 플레이 모드에서 기능을 확인할 때 사용. 컴파일 확인, 플레이/정지, 코드로 테스트 상황 만들기(좀비 소환, 밤 만들기, 아이템 지급, private 메서드 호출), 스크린샷 찍기, Unity가 응답 없거나 꺼졌을 때 대처.
---

# Unity 플레이 테스트 절차 (Last Truck)

## 0. 시작 전
- Unity MCP 도구가 없으면 `ToolSearch`로 `select:mcp__unityMCP__execute_code,mcp__unityMCP__manage_editor,mcp__unityMCP__refresh_unity,mcp__unityMCP__read_console`를 한 번에 불러온다.
- `execute_code`는 반드시 `action: "execute"`를 넣는다. 컴파일러는 codedom(C# 6 수준)이다:
  - `out var` 대신 변수를 먼저 선언하고 `out hit`로 넘긴다.
  - `Object`는 `System.Object`와 헷갈리므로 항상 `UnityEngine.Object.FindObjectsByType...`처럼 전체 이름을 쓴다.
  - 문자열 보간(`$"..."`)은 피하고 `+`로 잇는다.
- 상태 확인 한 줄: `return "playing=" + Application.isPlaying + " failed=" + UnityEditor.EditorUtility.scriptCompilationFailed;`

## 1. Unity 창 앞으로 띄우기
Unity는 백그라운드에 있으면 갱신이 멈춰서 플레이 진입·컴파일이 끝나지 않거나 타임아웃이 난다. 플레이/컴파일 전에 실행:
```bash
powershell -ExecutionPolicy Bypass -File ".claude/skills/unity-playtest/scripts/focus_unity.ps1"
```
(프로젝트 루트 기준 경로. MCP 스크린샷이 흰 화면으로 나올 때도 이걸 먼저 한다.)

## 2. 컴파일
1. **플레이 중이면 먼저 `manage_editor` action `stop`.** 플레이 중 컴파일은 Fusion AssertException/NRE 폭주를 일으킨다.
2. `refresh_unity` (mode `force`, compile `request`, wait_for_ready true).
3. `read_console`(types error)만 믿지 말고 **`EditorUtility.scriptCompilationFailed`를 꼭 확인**한다. 콘솔이 비어 있어도 컴파일이 실패해서 예전 어셈블리가 돌고 있던 적이 있다.
4. 새 타입이 실제로 들어왔는지: `System.Type.GetType("네임스페이스.타입, Assembly-CSharp")` (에디터 스크립트는 `Assembly-CSharp-Editor`).
- Python으로 C# 파일을 고칠 때 bash heredoc 안에 따옴표가 섞이면 깨진다 → 패치 스크립트를 스크래치패드에 파일로 쓰고 `python 파일경로`로 실행한다. 문자열 안 `\n`은 C# 리터럴이 줄바꿈되지 않게 주의.

## 3. 플레이 진입
- `manage_editor` action `play`. 직후 첫 `execute_code`는 타임아웃이 잘 난다 → 한 번 더 호출하면 된다.
- 로컬 플레이어: `CraftingSystem.GatherRules.Player` (GameObject). 인벤토리 `PlayerInventory`, 무기 `Combat.WeaponController`.
- 카탈로그: `CraftingSystem.ItemCatalog.GetOrCreate().GetItem("item_id")`, 지급: `inv.AddItem(item, n)`, 손에 들기: `inv.SelectSlot(i)`.

## 4. 테스트 상황 만들기 (코드)
- **좀비 소환**: `UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LastTruck/Monster/Prefab/Zombie.prefab")`를 Instantiate. 위치는 `UnityEngine.AI.NavMesh.SamplePosition`으로 보정. 가만히 세워 두려면 `MonsterAI`(전역 네임스페이스)와 `NavMeshAgent`를 끈다.
- **등급 붙여 소환(체력/크기 배율)**: `FindFirstObjectByType<MonsterSpawner>().SpawnTiered(hpScale, dmgScale, size, boss, go => {...})`.
- **밤 만들기**: `GameManager.Instance`의 `DayTimeRemaining` setter가 private → 리플렉션으로 0.5로 설정하면 곧 밤.
- **private 메서드/필드**: `BindingFlags.NonPublic | Static | Instance`로 `GetMethod(...).Invoke`. 예: WeaponController의 static `Weapons` 사전에서 수치를 꺼내 `FireHitscan`/`FireMeleeArc`/`FireGrenade`를 직접 부르면 마우스 없이 무기를 시험할 수 있다. 내구도 테스트는 그 전에 `BeginAttackWear(id)`를 부른다.
- **물리 위치를 옮긴 직후**에는 `Physics.SyncTransforms()`를 불러야 레이캐스트/OverlapSphere에 반영된다.
- 카메라 각도: `FindFirstObjectByType<Unity.Cinemachine.CinemachineOrbitalFollow>()`의 `VerticalAxis`/`HorizontalAxis`/`RadialAxis`를 구조체째 꺼내 값 바꾸고 다시 대입.

## 5. 스크린샷
- **플레이 중 게임 화면(HUD 포함)**: `ScreenCapture.CaptureScreenshot(경로)` → 다음 호출 뒤에 파일이 생긴다 → `Read`로 본다.
- **원하는 시점(에디터/플레이 모두)**: 임시 Camera + RenderTexture로 `cam.Render()` 후 ReadPixels → PNG. HUD는 안 찍힌다. 끝나면 카메라/RT를 DestroyImmediate.
- **위에서 본 지도**: 같은 방법에 `orthographic = true`, y 800에서 아래로.
- 빠른 이펙트(폭발/총구 섬광)는 `Time.timeScale = 0.05~0.3`로 늦추고 찍은 뒤 반드시 1로 되돌린다.
- 스크린샷은 스크래치패드에 저장한다 (프로젝트 안에 두지 않는다).

## 6. 끝낼 때
- `Time.timeScale = 1`로 되돌렸는지, 테스트용 좀비/아이템이 씬에 저장되지 않게 **플레이를 정지**했는지 확인.
- 씬을 바꾼 작업이면 플레이 정지 후 저장.

## 7. 문제가 생겼을 때
- `No Unity Editor instances found` / `Could not connect`: 창 띄우기 스크립트 실행 후 재시도. 계속되면 `tasklist | grep -i Unity.exe`로 살아 있는지 확인.
- **Unity가 꺼졌으면**: `%LOCALAPPDATA%\Unity\Editor\Editor.log` 끝부분의 스택트레이스로 원인을 확인하고, 사용자에게 **Unity Hub로 다시 열어 달라고** 한다 (Claude가 Unity.exe를 직접 실행하면 라이선스 오류로 바로 꺼진다). 저장 안 한 씬 변경은 사라진다.
- 오래 걸리는 작업(빌드, 굽기) 결과는 정적 필드/로그/파일에도 남겨서, 호출이 타임아웃 나도 나중에 읽을 수 있게 한다.
