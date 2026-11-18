---
name: add-item
description: Last Truck에 새 아이템(재료, 무기, 도구, 음식/소모품, 트럭 파츠, 갑옷)을 추가하거나 기존 아이템의 레시피·수치·아이콘을 바꿀 때 사용. ItemIds → 에디터 셋업 스크립트(아이템 에셋·레시피·내구도·카탈로그 등록) → 아이콘 → 도감 분류 → 무기 수치 → 손에 드는 모델 순서와 빠뜨리기 쉬운 곳 목록.
---

# 아이템 추가 파이프라인 (Last Truck)

게임은 **에셋 카탈로그** `Assets/Resources/Crafting/ItemCatalog.asset`을 쓴다 (`ItemCatalog.GetOrCreate()`).
`CraftingContentFactory.cs`는 카탈로그 에셋이 없을 때만 쓰는 예전 대체 경로라 여기에 추가해도 게임에 안 나온다.

## 1. id 등록
`Assets/Scripts/Crafting/ItemIds.cs`에 `public const string Xxx = "xxx";` + 한 줄 주석(무엇인지). 분류 구역(원재료/도구/근접 무기/원거리…)에 맞춰 넣는다.

## 2. 에셋 만들기 = 에디터 셋업 스크립트에 추가
새 파일을 만들기보다 성격이 맞는 기존 셋업에 `Def`를 추가한다. 셋 다 **다시 실행해도 중복으로 만들지 않는다**.
| 스크립트 | 메뉴 | 담는 것 |
|---|---|---|
| `Assets/Editor/WeaponsToolsSetup.cs` | `Tools/Last Truck/Items: Add Weapons & Tools ...` | 무기·도구. **아이콘 자동 렌더링 + 손에 드는 모델을 캐릭터 프리팹 7개에 등록**까지 한다 |
| `Assets/Editor/NewItemsSetup.cs` | `Tools/Last Truck/Items: Add Turrets, Foods, Repair Kit ...` | 포탑, 트럭 상위 파츠, 음식, 소모품, 탈출 부품, 그리고 `ToolDurability` 표 |
| `Assets/Editor/WoodStoneItemsSetup.cs` | — | 나무/돌 소비처 (투척 돌, 덫, 횃불, 바리케이드) |

`Def`에 넣는 값: `id, name(한글), type(ItemType: Raw/Intermediate/Finished/Tool/Weapon), weapon(isWeapon), stack(maxStack), durability, inputs, outCount`.
- 레시피 id 규칙: 트럭 조립 `assemble_<id>` (`FacilityType.None`), 시설 가공은 `bonfire_`/`chem_`/`grind_`/`press_`/`cut_`/`tan_`/`furnace_` + id, 시설 가공은 `processingSeconds`(보통 40~60).
- 재료 id가 카탈로그에 없으면 레시피 재료가 빠진다 → 실행 결과의 `!! 재료 없음` 줄을 확인한다.
- 실행은 Unity MCP `execute_code`로: `System.Type.GetType("WeaponsToolsSetup, Assembly-CSharp-Editor").GetMethod("Run").Invoke(null, null)` (결과 문자열을 그대로 보고).

## 3. 아이콘
- 위치: `Assets/Resources/Crafting/Icons/<id>.png` (Sprite, Single, mipmap 끔, alphaIsTransparency). 같은 이름 파일이 있으면 셋업이 그 그림을 쓴다.
- 3D 모델이 있으면 `WeaponsToolsSetup`의 `BuildIconSubject`에 case를 추가하면 투명 배경 + 어두운 테두리로 자동 렌더링된다 (파일이 **없을 때만** 그림).
- 아이콘을 다시 그리려면 png를 지우고 셋업을 다시 실행한다. **이 사이에 사용자가 커밋하면 아이콘이 빠진 채로 들어갈 수 있으니** 바로 다시 만든다. 다시 만든 뒤 `ItemData.icon` 참조가 살아 있는지 확인.
- 렌더 결과는 반드시 `Read`로 열어서 눈으로 확인 (모델이 옆으로 누워 있거나 하는 일이 잦다).

## 4. 분류·도감
`Assets/Scripts/Crafting/TruckCodexView.cs`의 HashSet에 넣어야 맞는 탭에 나온다:
`WeaponIds`(무기), `FoodIds`(음식), `FacilityIds`(설비), `TruckIds`(트럭 강화/파츠), `AmmoIds`(탄환). 없으면 `ItemType`으로 정해진다 (Tool → 도구, Finished+isWeapon false → 소모품).

## 5. 종류별 추가 작업
- **무기**: `Assets/Scripts/Combat/WeaponController.cs`의 static `Weapons` 사전에 `WeaponStats` 추가.
  - behavior: `Hitscan`(총, `pellets`/`spreadAngle` 산탄, `pierce` 관통), `MeleeArc`(근접, `cleave` 다중, `swingAnim` "AtkSlash"/"AtkStab"/"AtkChop"), `ContinuousCone`(화염방사), `ChargeAndRelease`(활), `Throw`(투척, `area` = Gas/Fire/Flare).
  - 화면 흔들림 `ShakeFor`, 내구도는 **적을 맞혔을 때만** 닳는다 (`BeginAttackWear` → `WearOnHit`/`ProjectileWear`). 새 공격 경로를 만들면 이 둘을 꼭 부른다.
  - 손 모델: `WeaponsToolsSetup.HeldModels`에 추가 (KayKit 근접은 scale 1 기준, Kenney 도구는 3.6, 총은 `gun=true`, 총구가 +Z인 모델은 `plusZ=true`).
- **도구**: 채집 속도는 `Assets/Scripts/Crafting/GatherRules.cs` (`ToolSpeedFactor`=곡괭이 등급, `SpecialToolFactor`=도끼/호미 같은 전용 도구). 곡괭이 판정 `WeaponController.IsPickaxe`, 도끼 `IsAxe`. 손에 든 도구 동작은 `HeldLantern`/`HeldBinoculars`처럼 컴포넌트를 만들고 `PlayerInventory.Awake`에서 자동으로 붙인다.
- **먹는/쓰는 소모품**: `Assets/Scripts/Crafting/EatController.cs`의 `Edibles` 사전 (회복량, 스태미나, 도구 수리, 무기 강화 등). 효과가 없으면 쓰지 못하게 `HasUseFor`도 맞춘다.
- **갑옷**: `PlayerArmor.cs` (가방에 있으면 적용).
- **트럭 파츠/포탑**: `TruckUpgrades.cs`의 `TruckUpgradeRules` (슬롯, 효과 문구, 포탑 수치표; 자동 포탑 id는 `auto_` + 수동 id).
- **몬스터 전리품**: `MonsterLoot.cs`.
- **도전과제에서 쓰는 아이템**이면 `LastTruck/Scripts/ProgressMissions.cs`의 `TrackedIds()`에도 넣어야 진행이 센다.

## 6. 확인
1. 컴파일 (`unity-playtest` 스킬 참고) → 셋업 실행 → 결과 줄 확인.
2. 플레이 모드에서 `inv.AddItem(cat.GetItem(id), n)`로 지급 → 손에 들기 → 기능 확인 (무기는 좀비를 세워 놓고 리플렉션으로 발사).
3. 트럭 창 제작 탭에 레시피가 나오는지, 도감 분류가 맞는지 스크린샷으로 확인.
4. 사용자에게 아이템 표(이름/특징/재료)로 보고한다.
