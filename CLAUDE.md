# Last Truck — Claude 작업 규칙

Unity 6000.3 (URP) + Photon Fusion(호스트 모드) 생존 게임. 낮에는 채집·제작·트럭 강화, 밤에는 생존 조건(좀비)을 버틴다.
메인 씬: `Assets/Scenes/MiningMapTest.unity`

## 항상 지킬 것
- 사용자에게는 **한국어**로 답한다.
- 씬을 바꾸면 **씬을 저장**한다.
- **커밋/푸시는 사용자가 직접** 한다. 요청받지 않으면 git commit 하지 않는다.
- 코드 수정 → 컴파일 전에 **플레이 모드가 꺼져 있는지** 확인한다 (플레이 중 컴파일하면 Fusion 에러가 쏟아진다).
- Unity가 꺼지면 Claude가 `Unity.exe`를 직접 실행하지 말고 **사용자에게 Unity Hub로 열어 달라고** 한다 (직접 실행하면 라이선스 확인에 실패해 꺼진다).
- 큰 씬 작업(맵 빌드 등) 전에는 씬 파일을 백업해 두고, 오래 걸리는 작업 전에는 씬부터 저장한다.

## 작업별 스킬 (`.claude/skills/`)
- `unity-playtest` — Unity MCP로 컴파일·플레이·스크린샷·코드로 기능 테스트하는 절차
- `add-item` — 아이템/무기/도구/음식 추가 파이프라인 (ItemIds → 셋업 스크립트 → 아이콘 → 도감 → 손 모델)
- `map-builder` — 맵 구역·좌표, 확장 땅 빌더, 채집물/트럭 통과 설정, 내비메시 주의사항
- `ui-toolkit-hud` — HUD/트럭 창(UI Toolkit) 스타일과 새 화면·탭 추가 방법

## 코드 지도 (자주 보는 곳)
- 게임 진행: `LastTruck/Scripts/GameManager.cs`, `SurvivalDirector.cs`(밤 생존 조건), `StoryDirector.cs`(탈출), `ProgressMissions.cs`(도전과제)
- 전투: `Scripts/Combat/WeaponController.cs`(모든 무기 수치/동작), `Damageable.cs`, `GrenadeProjectile.cs`
- 아이템/제작: `Scripts/Crafting/ItemIds.cs`, `ItemCatalog`(Resources/Crafting/ItemCatalog.asset), `GameUIController*.cs`(트럭 창)
- 트럭/포탑: `Scripts/Crafting/TruckTurret.cs`, `TruckAutoTurret.cs`, `TruckUpgrades.cs`
- 몬스터: `LastTruck/Monster/MonsterSpawner.cs`, `MonsterAI.cs`, 프리팹 `LastTruck/Monster/Prefab/Zombie.prefab` (멀티용 `Multiplayer/Prefabs/Monsters/Monster_Network.prefab`)
- 멀티플레이: `LastTruck/Scripts/Networking/` — 호스트가 판정, 클라이언트는 표시만 하는 구조가 기본
- 에디터 셋업/맵 빌더: `Assets/Editor/` (메뉴 `Tools/Last Truck/...`)

## 보고 방식
- 확인한 것과 확인하지 못한 것(멀티플레이, 실제 키 입력 등)을 구분해서 말한다.
- 바꾼 파일은 마크다운 링크로 알려 준다.
