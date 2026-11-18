---
name: map-builder
description: Last Truck 맵(MiningMapTest 씬)을 넓히거나 고치거나 채집물·광맥·장식을 배치할 때 사용. 구역별 좌표, 확장 땅 빌더(OpenWorldExpansionBuilder)와 광산 동굴, PurePoly 타일/팔레트 색 규칙, 채집물 설치기, 트럭 통과 레이어, 경계, 내비메시 크래시 주의사항.
---

# 맵 작업 (Last Truck)

씬 `Assets/Scenes/MiningMapTest.unity` (약 100MB). 모든 지형은 `Map` 아래 번호 묶음에 있다.
**큰 작업 전에 씬 파일을 스크래치패드에 복사해 백업**하고, 오래 걸리는 단계 전에는 씬부터 저장한다.

## 구역 지도 (월드 좌표, 대략)
| 묶음 | 내용 | 범위 |
|---|---|---|
| `01_Terrain/Terrain` | **시작 지역 (구매 에셋, 디자인 완성도 높음 — 함부로 고치지 말 것)** 트럭 시작 (0, 8, -9) | x -130~150, z -100~130, 회색 바위 고리로 둘러싸임 |
| `01_Terrain/ForestGroundTiles` 등 | 서쪽 숲 (직접 만든 곳) | x -508~-186, z -105~171 |
| `04_Cave`, `03_Ores&Crystals/HallOres*` | **지하 동굴** — 위가 뚫린 깊은 구덩이 (바닥 y -10 안팎) | 시작 지역 남·동쪽, x 170~360 / z -330~240 |
| `04_Cave/ForestMine` | 숲 북쪽 원형 초원 + 사막 가는 통로 | x -477~0, z 0~596 |
| `08_Desert` | 사막 (기름 밭) | x -617~2, z 571~1028 |
| `09_Quarry` | 채석장 + 큰 산맥(`Mountains`, 채석장 남쪽 z 350~600에 벽처럼) | x -293~565, z 240~1248 |
| `07_RuinedTown` | 폐허 도시 (섬). 주변은 큰 물 평면 `Models/Plane` (y -2, 콜라이더 있음) | x 454~1380 |
| `10_Expansion` | **확장 땅 3구역** (아래) | |

### 확장 땅 `10_Expansion`
- 빌더: `Assets/Editor/MapBuilders/OpenWorldExpansionBuilder.cs` — 메뉴 `Tools/Last Truck/Map: Build Open World Expansion` (seed 20261117, 약 20초, 다시 실행하면 지우고 같은 결과로 다시 만듦). `Remove`, `Bake Expansion NavMesh` 메뉴도 있다.
  - 푸른 초원 x -120~170, z 100~290 / 바위 고원 x 170~640, z 100~290 / 안개 늪 x 380~640, z -345~100.
  - 시작 지역 북쪽 끝(x -40~50, z 110)이 원래 열려 있어 그대로 이어진다. 트럭 길 `Road` 배열 = 시작 → 초원 → 고원 → 늪.
- 함께 쓰는 빌더: `ExpansionMineCave.cs`(고원 광산 입구 뒤 지하 동굴, 입구 최대 2개, 바닥 y -16 — Build가 호출), `OpenWorldExpansionGrove.cs`(같은 빌더의 partial — 안개 늪 동쪽 (565, -110) "독버섯 군락" `MysticGrove`, 빛나는 거대 버섯. Build가 `BuildGrove()`로 호출).
- 구역 이름 표시: 런타임 `LastTruck/Scripts/ZoneBanner.cs`(사각 범위 목록) + `ZoneMarker.cs`(동굴·군락처럼 위치가 생성 규칙으로 정해지는 곳에 다는 표식).
- **빌더 코드를 고친 뒤에는 Build를 다시 실행해야 씬에 반영된다.** 수작업으로 `10_Expansion`을 고치면 다음 Build에서 사라진다.

## 바닥 타일과 색
- PurePoly 타일(`PP_Ground_0x`, `PP_Meadow_0x`, `PP_Lake_0x`…)은 23m. 확장 빌더는 정점을 **월드 xz만으로 정해지는 높이 함수**로 들어 올려 이웃 타일 경계를 맞춘다. 메시는 `Assets/Scenes/MiningMapAssets/Expansion/Tiles/`에 에셋으로 저장 (수백 개 → `AssetDatabase.StartAssetEditing`으로 묶어 쓴다).
- 새 바닥은 시작 지역 바닥보다 약 0.2m 낮게 둔다 (겹친 곳은 기존 바닥이 위로 보여 z-파이팅이 없다).
- 색은 팔레트 `PurePoly/Mining_Pack/Textures/PP_Color_Palette.png`(8×8 칸, 칸 안 세로로 밝기 그라데이션)의 UV로 정한다. **한 타일 안에서는 팔레트의 같은 줄(가로로 이웃한 칸)만** 쓴다 — 줄이 다르면 정점 사이 보간이 중간 줄의 엉뚱한 색(노랑/주황)을 지나간다.
  - 칸 중심 u = (열*32+16)/256, v = 1 - (행*32 + 2 + 밝기*27)/256.
  - 쓰는 줄: 1행(연두→짙은 녹색, 초원/늪), 0행(흰색→검정, 고원 회색), 3행(사막 모래), 4행 7열(시작 지역 갈색 흙).
- 바닥 콜라이더는 태그 **`Ground`** 여야 몬스터가 스폰된다 (`MonsterSpawner`가 Ground 태그 레이캐스트).

## 배치 도우미 / 후처리 (빌더 끝에 꼭)
- **채집물**: `GatherablePropInstaller.Install(false)` — 이름으로 판정 (`PP_Tree_*`/`PP_Fir_Tree*`/`PP_Fantasy_Birch*`/`PP_Leafless_Tree*` 나무, `PP_Rock_*` 돌(12m 이하), `PP_Grass_0x` 일부 약초(4.5%) 나머지 들풀, `PP_Grass_Single` 들풀, `PP_Cactus`). `PP_Rock_Plateau/Veins/Gem`, `PP_Tree_Root`는 제외. 확실한 약초/독약초는 직접 `GatherTrigger`(layer 4, 트리거) + `GatherableProp.Configure`를 단다.
- **광맥**: `SpecialResourceNode`(layer 4 + 트리거 구) `Configure(이름, item, 양, 등급, minHits, maxHits, minRegen, maxRegen, "", 채집시간)` 후 `OreCrystalConverter.Convert(node, itemId, seed)`로 결정 모델. 등급: 구리 2 / 철 3 / 금(id `platinum_ore`) 4 / 다이아 5. 동굴 밖 광석 일괄 변환은 `CaveOreConverter.RunOutdoor()`.
- **트럭 통과**: `TruckPassSetup.Apply(scene)` — `Map/<번호묶음>/<그룹>`의 그룹 이름으로 판정: `Vegetation`, `Mushrooms`는 전부 통과, `Stones&Rocks`, `Crystals&Ores&Veins`, `Props` 등은 3.2m 미만만 통과. **새 묶음을 만들 때 이 그룹 이름을 그대로 쓴다.**
- 기름 웅덩이: `SpecialResourceNode` + 필요 도구 `ItemIds.CopperHandPump`, 재질 `Assets/Scenes/MiningMapAssets/Desert/PP_Oil_Material.mat`.
- 랜드마크(야영지·유적처럼 넓은 자리가 필요한 것)는 **나무·바위를 뿌리기 전에 먼저** 놓는다. 나중에 놓으면 빈자리를 못 찾아 0개가 된다.

## 경계
- 허공 쪽: 보이지 않는 BoxCollider 벽 + 산(`PP_Mountain_01/02`)을 바깥에. 짧은 틈에도 산을 놓아 허공이 안 보이게.
- **지하 동굴 쪽에는 산을 놓지 않는다** (동굴 위에 떠 버린다) → 안쪽 가장자리에 이끼 암반 + 벽.
- 호수 쪽: 물가 바위 몇 개 + 벽 (호수 건너 폐허 도시가 보이게 촘촘하지 않게).

## 내비메시 ⚠️
- **`Map`의 NavMeshSurface 전체를 다시 굽지 말 것.** 다시 구웠을 때 Unity가 `rasterizeTri`에서 크래시해 저장 안 한 작업을 날렸다 (폐허 도시 지형까지 함께 계산된 것이 원인으로 추정).
- 확장 땅은 `10_Expansion`에 따로 붙인 NavMeshSurface로 굽는다 (`Map: Bake Expansion NavMesh`, 결과 `Assets/Scenes/MiningMapAssets/Expansion/NavMesh-Expansion.asset`) + 통로에 폭 70m `NavMeshLink`.
- 새 구역을 또 만들면 같은 방식(그 구역만 따로 굽기 + 경계에 링크)으로 하고, **굽기 전에 씬 저장**.

## 확인
1. 위에서 본 지도(직교 카메라, y 800)로 구역·경계·빈틈 확인.
2. 땅 위 시점 스크린샷 여러 장 (이음매, 각 구역, 경계 쪽).
3. 플레이: 걸어서/트럭으로 이동, 낭떠러지·끼임, 밤 좀비 스폰과 추적(구역 경계 넘어서), 채집.
