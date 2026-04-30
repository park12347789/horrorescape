# Fragmented Option / Feature Ownership Audit & Integration Design

> 분석 범위: `Assets/Scripts` 런타임/리빌드/UI/오브젝티브 계층 전수 검색 (`rg`) + 핵심 소유 후보 코드 직접 추적.  
> 제한 사항: **현재 환경에서는 Unity Editor 실기동 검증 불가**. 따라서 인스펙터 연결은 `SerializeField`, 초기화 코드, scene-local `RSceneReferenceLookup`, `GetComponent`, Composition Root 바인딩 코드 기준으로 추정/확인했다.

## Executive Summary

Current route-data owner: `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`.
`RRunSessionController` scene-local fields and `MainEscapeRuntimeSettings` are
compatibility/alignment fallback data, not route-data authorities.

현재 구조를 한 문장으로 표현하면: **"ScriptableObject 전역 설정 + 씬 로컬 직렬화 상태 + static singleton + Composition Root 수동 바인딩이 동시에 공존하는 다중 진실원 구조"**다.

- 위험성: 동일 의미(예: 손전등 상태, 디버그 가시화, 라우팅/기본값)가 여러 owner 후보에 중복되어 source-of-truth 붕괴 위험이 있다.
- 가장 심각한 파편화 패턴: **플레이어 상태(손전등/배터리/인벤토리/체력)가 `RRunSessionController` 저장 상태 + 각 컴포넌트 런타임 상태 + 디버그 강제 적용 경로로 분산**된 구조.
- 최우선 정리 대상: 1) Save/Load + Runtime State 경계, 2) Fog/Visibility + Debug bypass 경계, 3) UI option 해석 경계.
- 주요 리스크: 씬 전환 시 상태 덮어쓰기, legacy serialized fields와 snapshot mirror 불일치, 바인딩 누락 시 scene-local 복구 경로가 숨은 의존성처럼 보일 가능성, 이벤트/폴링 혼용으로 동기화 순서 불안정.

---

## Current Fragmentation Audit

## 1) Current Architecture Overview

### 전반 양상
- 전역 설정은 `MainEscapeRuntimeSettings.Load()` 정적 로드 방식으로 다수 스크립트가 직접 접근한다 (`Objectives`, `Rebuild`, `UI`, `Editor` 전역). Sources: [MainEscapeRuntimeSettings.cs](../../Assets/Scripts/Objectives/MainEscapeRuntimeSettings.cs), [IRAnalogNoiseUiTheme.cs](../../Assets/Scripts/Rebuild/UI/IRAnalogNoiseUiTheme.cs)
- 런타임 상태는 `RRunSessionController`가 보유하지만, 실제 적용은 `RRunController`, `Player*` 컴포넌트, `MainEscapeDebugModeController`가 각자 수행한다. Sources: [RRunSessionController.cs](../../Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs), [MainEscapeDebugModeController.cs](../../Assets/Scripts/Objectives/MainEscapeDebugModeController.cs)
- UI는 일부는 바인더 이벤트 기반(`Changed`), 일부는 매 프레임 재계산/갱신 흐름이 섞여 있다. Sources: [IRPlayerQuickSlotsHudBinder.cs](../../Assets/Scripts/Rebuild/UI/IRPlayerQuickSlotsHudBinder.cs), [DoorDiscoveryVisibilityController.cs](../../Assets/Scripts/Objectives/DoorDiscoveryVisibilityController.cs)
- Fog/Visibility는 공통 오버레이 소비자 인터페이스(`IFogOfWarOverlayConsumer`)와 scene-local `RSceneReferenceLookup` fallback을 사용한다. 다만 소비자 쪽 `LateUpdate` 폴링은 아직 남아 있어 owner/read timing 경계는 계속 주의가 필요하다. Sources: [RSceneCompositionRoot.RuntimeBinding.cs](../../Assets/Scripts/Rebuild/Runtime/RSceneCompositionRoot.RuntimeBinding.cs), [DoorDiscoveryVisibilityController.cs](../../Assets/Scripts/Objectives/DoorDiscoveryVisibilityController.cs)

### ownership 불분명 구간
- 손전등 ON/OFF: `PlayerFlashlightEquipment`가 owner처럼 보이나, `RRunSessionController.RestorePlayerState` 및 `ApplyCurrentFloorPlayerDefaults`가 다시 상태를 주입한다(2차 writer). Sources: [PlayerFlashlightEquipment.cs](../../Assets/Scripts/Player/PlayerFlashlightEquipment.cs), [RRunSessionController.cs](../../Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs)
- 디버그 관련 표현/무적/포그 bypass: `MainEscapeDebugModeController`가 다수 시스템을 직접 mutate한다(Noise, Health, Fog, Player presentation). Source: [MainEscapeDebugModeController.cs](../../Assets/Scripts/Objectives/MainEscapeDebugModeController.cs)
- 라우팅 설정: `RRunSessionController` 내부 직렬화 필드가 런타임 owner이며, `RRunRoutingSettings`/`MainEscapeRuntimeSettings`는 누락 시 fallback 또는 정렬 검증 데이터로만 남는다. Source: [RRunSessionController.cs](../../Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs)

### UI/runtime/saved settings/gameplay 연결 방식
- UI theme 토글은 `MainEscapeRuntimeSettings.UseTemporaryAnalogNoiseUi`를 UI 계층이 직접 읽어 분기한다. Source: [IRAnalogNoiseUiTheme.cs](../../Assets/Scripts/Rebuild/UI/IRAnalogNoiseUiTheme.cs)
- 세션 저장은 `RRunSessionController`가 legacy serialized fields와 `RRunPlayerStateSnapshot`/`IRunPlayerStateStore` 경로를 호환용으로 함께 유지한다. Source: [RRunSessionController.cs](../../Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs)
- 런타임 적용은 `RSceneCompositionRoot`에서 일괄 바인딩하고, 누락 시에는 가능한 범위에서 scene-local `RSceneReferenceLookup` 또는 명시 reference를 우선 사용한다.

### 옵션 중복 해석 여부
- 동일 옵션(예: flashlight enabled, fog bypass, quick slot count)이 여러 레이어에서 중복 해석된다.
  - flashlight enabled: `PlayerFlashlightEquipment`, `WasdPlayerController`, `RRunSessionController`.
  - fog bypass: `MainEscapeDebugModeController` + `FlashlightFogOfWarOverlay` + visibility consumers.
  - UI slot count: `MainEscapeRuntimeSettings.QuickSlotVisibleCount` + `PlayerQuickItemController.GetConfiguredSlotCount()` 병합 로직.

## 2) Inventory of Fragmented Options / Features

| 기능/옵션 | 현재 대표 관련 스크립트들 | 실제 소유자 추정 | Readers | Writers | 참조 방식 | 중복 여부 | 리스크 | 비고 |
|---|---|---|---|---|---|---|---|---|
| 손전등 활성/장착 상태 | `PlayerFlashlightEquipment`, `WasdPlayerController`, `RRunSessionController` | **복수 후보** (`PlayerFlashlightEquipment` + `RRunSessionController`) | HUD/적시야/포그/UI | 장비 토글, 세션 restore/default 주입 | SerializeField, 이벤트, direct method call | 높음 | Critical | source of truth 붕괴 핵심 |
| 손전등 배터리/가시성 스케일 | `PlayerFlashlightBattery`, `WasdPlayerController`, `RRunSessionController` | `PlayerFlashlightBattery`(런타임), `RRunSessionController`(저장) | HUD, Fog overlay | 배터리 drain/아이템 소모/restore | Update 폴링 + restore write | 높음 | High | 저장값과 런타임 소비 로직 경계 모호 |
| 포그 bypass + 가시성 | `FlashlightFogOfWarOverlay`, `MainEscapeDebugModeController`, `DoorDiscoveryVisibilityController`, `FogReactiveEnemyVisibility` | `FlashlightFogOfWarOverlay`(추정) | 문/적 가시화 컴포넌트 | debug controller + overlay API | IFogOfWarOverlayConsumer, scene-local lookup, LateUpdate polling | 높음 | Critical | side effect 다발 가능 |
| 런 라우팅(로비/층별 씬) | `RRunSessionController`, `RRunRoutingSettings`, `MainEscapeRuntimeSettings` | `RRunSessionController` scene-local fields | Lobby/UI/router | SessionController + fallback/alignment assets | serialized scene route + fallback ScriptableObject | 중간 | Medium | legacy flag는 런타임 owner를 전환하지 않음 |
| 플레이어 기본값(체력/시작 아이템) | `RRunPlayerDefaults`, `RRunSessionController`, `Player*` | `RRunPlayerDefaults` 또는 SessionController | HUD/player systems | Session restore/default apply | ScriptableObject + runtime mutate | 중간 | High | bootstrapping 중복 |
| 디버그 모드/무적/성능 overlay | `MainEscapeDebugModeController`, `MainEscapeRuntimeSettings`, `PlayerHealth`, `NoiseSystem` | `MainEscapeDebugModeController` | 다수 런타임 시스템 | Debug controller(Update 키입력) | Input polling, direct mutation | 높음 | High | UI requester/owner 분리 없음 |
| 오디오 믹스/앰비언스 | `PrototypeAudioManager`, 씬별 호출자들 | `PrototypeAudioManager` singleton | Player/Run/UI | static Try* APIs | static singleton | 중간 | Medium | global singleton, hidden dependency |
| 노이즈 이벤트 버스 | `NoiseSystem`, Enemy listeners, DebugModeController | `NoiseSystem` singleton | Enemy/visualization | NoiseEmitter/NoiseFloorPanel | static singleton + event list poll | 중간 | Medium | single owner는 있으나 global mutable |
| UI 테마/HUD 설정 | `MainEscapeRuntimeSettings`, `IRAnalogNoiseUiTheme`, `IRPlayerQuickSlotsHudBinder` | 부재(명시 owner 없음) | UI binders/views | UI theme static helpers | static load + direct read | 높음 | High | “실제 주 소유자 없음” |
| 씬 바인딩 캐시/인스펙터 참조 | `RSceneCompositionRoot` | `RSceneCompositionRoot` | 런타임 전역 | CompositionRoot + fallback find | SerializeField + scene search | 중간 | Medium | inspector와 자동탐색 혼재 |

> 의미상 동일하지만 이름 다른 상태값 묶음:
- `savedFlashlightEnabled` / `flashlightEnabled` / `startEnabled` / `flashlightEnabledState` (같은 의미권역: “현재 손전등 켜짐 상태”). Sources: [RRunSessionController.cs](../../Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs), [PlayerFlashlightEquipment.cs](../../Assets/Scripts/Player/PlayerFlashlightEquipment.cs), [WasdPlayerController.cs](../../Assets/Scripts/Player/WasdPlayerController.cs)

---

## Feature Cluster Deep Dive

## A. Fog / Visibility / Lighting

### Feature Cluster Summary
- 담당: 플레이어 시야, 문/적 가시화, 로컬 라이트 반응.
- 파편화 원인: fog overlay owner는 하나인데, 소비자 쪽에서 자체 visible 판정/폴링을 재구현한다. 탐색 fallback은 broad Find가 아니라 같은 씬 `RSceneReferenceLookup`으로 축소된 상태다.
- 실제 문제: debug bypass, overlay null, scene rebind 누락 시 표시 불일치 발생 가능.

### Scripts Involved
- Owner 후보: `FlashlightFogOfWarOverlay`(상태/텍스처/갱신 루프). Source: [FlashlightFogOfWarOverlay.cs](../../Assets/Scripts/Objectives/FlashlightFogOfWarOverlay.cs)
- Consumer(문): `DoorDiscoveryVisibilityController` (`LateUpdate`, 자체 샘플링). Source: [DoorDiscoveryVisibilityController.cs](../../Assets/Scripts/Objectives/DoorDiscoveryVisibilityController.cs)
- Consumer(적): `FogReactiveEnemyVisibility` + `MainEscapeEnemyVisibilityUtility` 동적 부착/초기화. Sources: [FogReactiveVisibility.cs](../../Assets/Scripts/Objectives/FogReactiveVisibility.cs), [MainEscapeEnemyVisibilityUtility.cs](../../Assets/Scripts/Objectives/MainEscapeEnemyVisibilityUtility.cs)
- Bootstrap/Binding: `RSceneCompositionRoot.RuntimeBinding` (`IFogOfWarOverlayConsumer` 일괄 바인딩). Source: [RSceneCompositionRoot.RuntimeBinding.cs](../../Assets/Scripts/Rebuild/Runtime/RSceneCompositionRoot.RuntimeBinding.cs)
- Writer(강제): `MainEscapeDebugModeController.SetBypassEnabled` 경로. Source: [MainEscapeDebugModeController.cs](../../Assets/Scripts/Objectives/MainEscapeDebugModeController.cs)

### Current Data Flow
`Player pose` → `FlashlightFogOfWarOverlay.Update` → `GetStateAtWorldPoint/visibility query` → `Door/Enemy consumer LateUpdate`.
- 이벤트 기반이 아니라 소비자 폴링이 많다.
- 씬 전환 후 `RSceneCompositionRoot` 재바인딩에 의존.

### Ownership Problems
- 동일 가시성 판단이 overlay + consumer에서 일부 중복.
- debug bypass는 owner 외부에서 직접 mutate.
- overlay 연결 실패 시 consumer가 scene-local lookup으로 복구할 수 있어도, 필수 바인딩 누락 자체는 validator로 드러내야 한다.

### Refactor Complexity
- **High**: 시각 피드백과 적 AI 노출 판정이 결합됨. 작은 변경도 체감 회귀 위험 큼.

## B. UI Option / UI State

### Summary
- HUD/로비/UI 테마 값이 runtime settings를 직접 읽거나 런타임 데이터를 직접 계산한다.
- UI가 requester가 아니라 사실상 owner처럼 옵션 해석을 수행.

### Scripts Involved
- `IRAnalogNoiseUiTheme.IsEnabled`가 settings 직접 로드. Source: [IRAnalogNoiseUiTheme.cs](../../Assets/Scripts/Rebuild/UI/IRAnalogNoiseUiTheme.cs)
- `IRPlayerQuickSlotsHudBinder`가 slotCount를 settings+runtime 둘 다 조합. Source: [IRPlayerQuickSlotsHudBinder.cs](../../Assets/Scripts/Rebuild/UI/IRPlayerQuickSlotsHudBinder.cs)
- `InventoryHudPresentationBuilder`도 settings 직접 사용(추가 코드 확인 필요).

### Data Flow
`UI Binder` → `MainEscapeRuntimeSettings.Load()` + `Player runtime` 직접 읽기 → View 렌더.

### Ownership Problems
- UI 옵션 owner 계층 부재.
- UI마다 settings 접근 방식이 분산.

### Complexity
- **Medium**: UI 설정 provider 도입 시 비교적 안전하게 분리 가능.

## C. Runtime Gameplay Toggle (Flashlight/Health/QuickSlot)

### Summary
- 플레이어 서브시스템이 각각 상태를 소유하지만, session restore/default/debug가 재주입.

### Scripts
- `PlayerFlashlightEquipment` (장착/토글 owner 후보). Source: [PlayerFlashlightEquipment.cs](../../Assets/Scripts/Player/PlayerFlashlightEquipment.cs)
- `PlayerFlashlightBattery` (충전량 owner 후보). Source: [PlayerFlashlightBattery.cs](../../Assets/Scripts/Player/PlayerFlashlightBattery.cs)
- `PlayerHealth` (체력/무적). Source: [PlayerHealth.cs](../../Assets/Scripts/Player/PlayerHealth.cs)
- `PlayerQuickItemController` (슬롯 입력/사용). Source: [PlayerQuickItemController.cs](../../Assets/Scripts/Player/PlayerQuickItemController.cs)
- `RRunSessionController` (restore/capture/default 주입). Source: [RRunSessionController.cs](../../Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs)

### Data Flow
입력(`WasdPlayerController`) → 각 Player 컴포넌트 Update → `Changed` 이벤트 → HUD 반영.
씬 경계에서는 SessionController가 캡처/복원 수행.

### Ownership Problems
- Session restore가 런타임 owner를 직접 mutate.
- 시작 기본값(`startEnabled`, defaults asset, saved state)의 우선순위가 분산.

### Complexity
- **Critical**: 저장 호환성과 실제 플레이 루프 영향이 큼.

## D. Save / Load Settings

### Summary
- `RRunSessionController`가 legacy serialized state + `RRunPlayerStateSnapshot` mirror + optional state store를 동시에 운영.

### Scripts
- `RRunSessionController` + `RRunPlayerStatePersistence` + `RRunPlayerStateStore`.
- `RRunPlayerDefaults`, `RRunRoutingSettings` 자산.

### Data Flow
Capture 시: runtime → persistence/store → snapshot → legacy serialized fields 호환 기록.
Restore 시: snapshot/serialized state 우선, 없으면 defaults.

### Ownership Problems
- 저장 owner는 SessionController지만, legacy serialized fields와 snapshot mirror가 동일 데이터를 함께 표현한다.
- `ApplyCurrentFloorPlayerDefaults`가 restore 이후 재적용되어 writer 충돌 여지.

### Complexity
- **Critical**: 호환성 및 씬 전환 안정성 핵심.

## E. Debug / Developer Flags

### Summary
- 디버그 플래그는 settings 값 + runtime controller 상태 + 키입력 실시간 토글이 혼재.

### Scripts
- `MainEscapeDebugModeController` (핵심 writer/applier), `MainEscapeRuntimeSettings` (기본 키/플래그 제공).

### Problems
- Debug controller가 presentation + invincibility + fog bypass + noise pulse를 한 클래스에서 모두 조작(역할 과밀).
- Release 동작 경계가 약함.

### Complexity
- **High**: 다중 시스템 side effect.

## F. Input / Control Mapping

### Summary
- 입력은 `WasdPlayerController`가 직접 action map 조회/소비하고, 여러 하위 시스템이 해당 메서드를 polling.

### Scripts
- `WasdPlayerController` (`ConsumeFlashlightTogglePressedThisFrame`, quick slot/throw input gateway). Source: [WasdPlayerController.cs](../../Assets/Scripts/Player/WasdPlayerController.cs)
- `PlayerFlashlightEquipment`, `PlayerQuickItemController`가 해당 토글을 소비.

### Problems
- 입력 owner가 플레이어 컨트롤러에 과집중, feature별 input handler 계층 없음.

### Complexity
- **Medium**.

## G. Scene-specific Runtime Configuration

### Summary
- `RSceneCompositionRoot`가 바인딩/초기화 중앙 허브이고, runtime 누락 시 각 스크립트는 scene-local reference lookup이나 명시 fallback으로만 복구해야 한다.

### Scripts
- `RSceneCompositionRoot`, `RSceneReferenceLookup`, `RRunController`, `RRunSessionController`.

### Problems
- inspector 연결 + 코드 탐색 혼재.
- bootstrap 순서 의존성이 숨겨짐.

### Complexity
- **High** (씬 체인 핵심).

---

## Ownership / Dependency Map

### [Flashlight Enabled]
- Current Owner Candidates: `PlayerFlashlightEquipment`, `RRunSessionController`.
- Readers: `WasdPlayerController`, HUD, fog 관련 표현.
- Writers: `PlayerFlashlightEquipment.Toggle`, `RRunSessionController.Restore/Defaults`, (간접) debug.
- Appliers: `PlayerFlashlightEquipment.ApplyFlashlightState` → `WasdPlayerController`.
- Serializer: `RRunSessionController`.
- Bootstrapper: `RSceneCompositionRoot` + `RRunSessionController.RestoreGameplayStateNextFrame`.
- Problems: 복수 writer, 초기값 충돌.
- Recommended Future Owner: `FlashlightStateOwner` (신규, 단일).

### [Flashlight Charge]
- Current Owner Candidates: `PlayerFlashlightBattery`.
- Readers: HUD, fog visibility scale 경로.
- Writers: `Update drain`, battery 사용, session restore.
- Appliers: `WasdPlayerController.SetFlashlightBatteryScale`.
- Serializer: `RRunSessionController`.
- Bootstrapper: `RRunSessionController`.
- Problems: runtime 값과 persistence 값 경계 약함.
- Recommended Future Owner: `FlashlightChargeOwner` + `IFlashlightChargeReadModel`.

### [Fog Bypass / Visibility]
- Current Owner Candidates: `FlashlightFogOfWarOverlay` + debug controller(override writer).
- Readers: Door/Enemy visibility consumers.
- Writers: `MainEscapeDebugModeController`, overlay API.
- Appliers: each consumer `LateUpdate`.
- Serializer: 없음.
- Bootstrapper: `RSceneCompositionRoot.BindGameplayRuntime`.
- Problems: owner 외부 강제 변경, consumer 폴링 중복.
- Recommended Future Owner: `FogVisibilityOwner` (overlay 내부) + `IFogVisibilityService`.

### [Run Routing]
- Current Owner Candidates: `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`. `RRunSessionController` scene-local fields and `MainEscapeRuntimeSettings` are compatibility/alignment fallback data, not route-data authorities.
- Readers: scene router/lobby/ui.
- Writers: inspector, canonical asset assignment.
- Appliers: `RSceneRouter`.
- Serializer: scene serialized.
- Bootstrapper: session controller awake.
- Problems: fallback assets must stay aligned with the scene-local route, but the legacy override flag no longer switches runtime ownership.
- Recommended Future Owner: `RunRoutingOwner` (항상 asset-backed).

### [Debug Flags]
- Current Owner Candidates: `MainEscapeDebugModeController` runtime bools + settings defaults.
- Readers: overlay/gui/player/fog/noise.
- Writers: keyboard polling.
- Appliers: debug controller 내부 direct mutation.
- Serializer: 없음(세션 저장 X).
- Bootstrapper: composition root install.
- Problems: 역할 과밀, side effect.
- Recommended Future Owner: `DebugModeOwner` + feature별 applier.

### [UI Theme Enable]
- Current Owner Candidates: 없음 (`MainEscapeRuntimeSettings` 직접 읽기 분산).
- Readers: `IRAnalogNoiseUiTheme`, UI builders.
- Writers: asset 인스펙터.
- Appliers: 각 UI static helper.
- Serializer: ScriptableObject asset.
- Bootstrapper: 각 UI script 호출 시점.
- Problems: UI 계층에서 설정 직접 소유처럼 동작.
- Recommended Future Owner: `UiSettingsOwner`.

---

## Anti-Patterns

| 안티패턴 | 발견 위치 | 왜 위험한가 | 통합 해소안 |
|---|---|---|---|
| 같은 옵션을 여러 스크립트 직접 참조 | 손전등 enabled/charge (`PlayerFlashlightEquipment`, `RRunSessionController`, `WasdPlayerController`) | 우선순위 충돌로 예측 어려움 | 단일 Owner + 변경 요청 커맨드화 |
| 같은 상태를 다른 이름으로 중복 보관 | `savedFlashlightEnabled`, `flashlightEnabled`, `startEnabled` | 디버깅 난이도 급증 | naming 통일 + read model 분리 |
| UI가 runtime 내부 상태 직접 변경/해석 | 테마/슬롯 수를 UI 계층에서 결정 | UI 교체 시 로직 복제 | `IUiSettingsProvider` 도입 |
| 적용 담당과 저장 담당 혼합 | `RRunSessionController` restore 후 defaults 재적용 | restore 이후 상태 덮어쓰기 | Serializer와 RuntimeOwner 분리 |
| 씬 초기화가 런타임값 덮어씀 | scene load 시 restore/default 적용 | 씬 전환 race | bootstrap 단계 명문화 |
| legacy fields + snapshot mirror 혼용 | `RRunSessionController` saved fields와 `RRunPlayerStateSnapshot` 동시 표현 | stale mirror/default-order 가능성 | durable store/read model로 수렴 |
| ScriptableObject가 설정+실행 경계에 걸침 | runtime settings를 다층 직접 로드 | 설정 파급 범위 과대 | owner가 settings를 읽어 캐시/배포 |
| event + polling 동시 존재 | 일부 `Changed` 이벤트, 일부 `LateUpdate` 폴링 | 중복 연산/타이밍 이슈 | event 우선, 폴링 최소화 |
| inspector 연결 + scene-local lookup 혼재 | CompositionRoot + `RSceneReferenceLookup` fallback | 숨은 의존성 | 필수 바인딩은 inspector 고정 + validator |
| 기능 변경이 타 기능 내부 로직에 암묵 의존 | Debug 변경 시 Fog/Noise/Player 동시 영향 | side effect | feature별 applier 분리 |

---

## Target Architecture Proposal

## Integration Design Principles

1. **Single Owner per Option**  
   - 필요성: 다중 writer 제거.
   - 적용 위치: 손전등, 디버그, 라우팅, UI 테마.
   - 적용 방식: `*Owner` 클래스 1개만 mutable state 보유.

2. **Single Source of Truth**  
   - 런타임 상태와 저장 상태를 분리(`RuntimeState`, `PersistedState`).

3. **Read via interface/service only**  
   - Reader는 `I...ReadModel`만 의존.
   - 사용자 요청 반영: 인터페이스는 반드시 `I` 접두어.

4. **Writers do not directly mutate unrelated systems**  
   - Writer는 Owner에 command 요청만, 적용은 applier가 수행.

5. **Appliers should not own settings**  
   - 예: Fog applier는 값 적용만, 설정 보유 금지.

6. **Persistence/runtime 분리**  
   - `IRunStateSerializer`는 저장만, Owner는 실행 상태만.

7. **UI is requester, not owner**  
   - UI는 `IUiSettingsReadModel` 조회 + 변경 요청 이벤트만.

8. **Scene bootstrap must not silently override**  
   - bootstrap 단계에서 “RestoreComplete 이전 기본값 주입 금지” 규칙.

9. **Temporary debug flags isolated**  
   - `DebugModeOwner` + `IDebugFeatureApplier` 묶음으로 분리.

10. **Naming reveals ownership/lifecycle**  
   - `XxxSettingsOwner`, `XxxRuntimeApplier`, `XxxPersistence`, `IXxxReadModel`.

## Proposed Target Architecture

### Example Target Pattern
- `XxxSettingsOwner`: 유일 소유자
- `XxxSettingsView`: UI 표시 전용
- `XxxSettingsInputHandler`: 변경 요청 전달
- `XxxRuntimeApplier`: 실제 반영
- `XxxPersistence`: 저장/로드

### 기능군 적용안

1) Flashlight
- `FlashlightStateOwner` (enabled, owned, charge read references)
- `IFlashlightStateReadModel`
- `FlashlightRuntimeApplier` (WasdPlayerController 반영)
- `FlashlightPersistence` (세션 직렬화)
- `FlashlightInputHandler` (토글 입력)

2) Fog
- `FogVisibilityOwner` (bypass, visibility query cache)
- `IFogVisibilityService`
- `FogVisibilityApplier` (Door/Enemy 소비자에 이벤트 push)

3) Run Session
- `RunRoutingOwner` (항상 asset 기반)
- `IRunPlayerStateSerializer`
- `RunSessionBootstrapper` (씬 로드시 초기화 책임만)

4) UI
- `UiSettingsOwner` (`MainEscapeRuntimeSettings` 캐시/노출)
- `IUiSettingsReadModel`
- 각 Binder는 Owner interface만 참조

### 패턴이 맞는 곳 / 안 맞는 곳
- 적합: 장기 유지 옵션(손전등, 라우팅, UI, 디버그).
- 덜 적합: 짧은 생명 임시 이펙트(일회성 연출 객체)는 과도한 계층화 불필요.

### 권장 미래 폴더 구조(초안)
- `Assets/Scripts/Runtime/Ownership/`
  - `Flashlight/`
  - `Fog/`
  - `RunSession/`
  - `Ui/`
  - `Debug/`
- `Assets/Scripts/Runtime/Interfaces/` (`I...` only)
- `Assets/Scripts/Runtime/Appliers/`
- `Assets/Scripts/Runtime/Persistence/`

---

## Migration Plan

| 단계 | 목표 | 실제 작업 | 파일 범위 | 위험도 | 사전조건 | 완료기준 | 롤백 포인트 |
|---|---|---|---|---|---|---|---|
| 0. 분석 태깅 | owner/reader/writer 표시 | 주요 필드/메서드 주석 태깅, 문서 고정 | Docs + 핵심 클래스 | Low | 없음 | 기능별 owner 후보 확정 | 태깅 커밋 되돌리기 |
| 1. Ownership 선언 | 동작 유지, owner 명세만 도입 | `I...ReadModel`/Owner 스켈레톤 추가 (연결X) | Runtime/Interfaces, Ownership | Medium | 팀 승인 | 컴파일 통과, 동작 동일 | 인터페이스 추가만 revert |
| 2. 읽기 경로 분리 | direct field read 축소 | UI/Binder가 Owner interface로 읽기 | UI/Binder | Medium | 단계1 | settings 직접 load 감소 | binder 단위 rollback |
| 3. 쓰기 경로 분리 | writer 단일화 | debug/session 입력을 owner command로 우회 | Debug/Run/Player | High | 단계2 | owner 외 직접 mutate 제거율 측정 | 기능군별 revert |
| 4. 중복 상태 제거 | duplicate bool/int 제거 | saved/snapshot/store 우선순위 정리 | Session/Player | Critical | 단계3 + 테스트 | 동일 의미 상태 1개화 | save-path 단위 rollback |
| 5. 저장/로드 정리 | serializer 분리 | persistence 계층 이동 | Session/Persistence | High | 단계4 | restore/capture 테스트 통과 | serializer 교체 이전 커밋 |
| 6. UI 연결 수정 | requester-only UI | UI input handler 도입 | UI | Medium | 단계2~5 | UI가 owner mutate 안함 | panel별 rollback |
| 7. direct reference 제거 | fallback 탐색 축소 | scene-local lookup/GetComponent hot fallback 정리 | Runtime/Objectives | High | 바인딩 검증 도구 | 씬별 validator green | scene cluster rollback |
| 8. 회귀 테스트 | 루프 안정성 확인 | EditMode/PlayMode + 수동 체크리스트 | Assets/Tests | High | 모든 단계 | canonical loop 유지 | 실패 단계부터 역순 롤백 |

> 핵심: **한 번에 합치지 않고, “먼저 ownership만 확정하고 동작 유지” 단계(1~2)를 반드시 거친다.**

---

## File-by-File Action Proposal

| 파일 | 현재 역할 | 문제 | 앞으로의 역할 | 분류 | 수정 필요 | 삭제/축소 가능성 | 의존 영향 |
|---|---|---|---|---|---|---|---|
| `Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs` | 세션/저장/복원/라우팅 허브 | 역할 과밀, static+instance 중복 | `RunSessionOrchestrator` + serializer 위임 | 분리 | 높음 | 일부 축소 | 매우 큼 |
| `Assets/Scripts/Player/PlayerFlashlightEquipment.cs` | 손전등 보유/토글 | session과 writer 충돌 | `FlashlightStateOwner` 또는 owner consumer | 분리 | 중간 | 축소 가능 | 큼 |
| `Assets/Scripts/Player/PlayerFlashlightBattery.cs` | 충전량 owner + 적용 | persistence 경계 혼합 | charge owner 유지, serializer 분리 | 유지+분리 | 중간 | 낮음 | 중간 |
| `Assets/Scripts/Objectives/FlashlightFogOfWarOverlay.cs` | 포그 owner + 렌더 | debug 외부 write 허용 | fog owner 유지, command API만 노출 | 유지 | 중간 | 낮음 | 큼 |
| `Assets/Scripts/Objectives/MainEscapeDebugModeController.cs` | 디버그 통합 컨트롤 | side effect 과다 | debug owner + applier들로 분해 | 분리 | 높음 | 축소 가능 | 큼 |
| `Assets/Scripts/Rebuild/Runtime/RSceneCompositionRoot.cs` | 씬 조립/바인딩 | 자동탐색 혼재 | bootstrapper 전용, 필수 바인딩 엄격화 | 유지+축소 | 중간 | 낮음 | 큼 |
| `Assets/Scripts/Rebuild/UI/IRAnalogNoiseUiTheme.cs` | UI 테마 static helper | 설정 직접 소유처럼 동작 | `IUiSettingsReadModel` 소비자로 전환 | 대체 | 중간 | 일부 static 축소 | 중간 |
| `Assets/Scripts/Rebuild/UI/IRPlayerQuickSlotsHudBinder.cs` | 퀵슬롯 HUD 바인딩 | settings 직접 read | owner read-model 통해 조회 | 유지 | 낮음 | 낮음 | 중간 |
| `Assets/Scripts/Audio/PrototypeAudioManager.cs` | 전역 오디오 singleton | hidden global dependency | AudioOwner interface façade | 축소 | 중간 | 낮음 | 중간 |
| `Assets/Scripts/Noise/NoiseSystem.cs` | 노이즈 버스 | singleton global mutable | owner 유지, readonly feed 강화 | 유지 | 낮음 | 낮음 | 중간 |
| `Assets/Scripts/Objectives/DoorDiscoveryVisibilityController.cs` | 문 발견 가시성 | 폴링+탐색 fallback | fog service subscriber | 대체 | 중간 | 가능 | 중간 |
| `Assets/Scripts/Objectives/FogReactiveVisibility.cs` | 적 가시화 반응 | 로컬 판정 중복 | fog applier consumer 단순화 | 축소 | 중간 | 중간 | 중간 |

---

## Risk Assessment

| 리스크 | 발생 가능성 | 영향도 | 감지 방법 | 완화 방법 |
|---|---:|---:|---|---|
| 씬 전환 시 값 초기화 꼬임 | 높음 | 치명적 | floor 전환 회귀 테스트, snapshot diff | bootstrap 단계 고정 + restore 시점 통제 |
| 저장값 호환성 깨짐 | 중간 | 높음 | 이전 세이브 상태 재주입 테스트 | serializer adapter 유지 |
| inspector reference 유실 | 중간 | 높음 | `MainEscapeRuntimeValidator` + null binding audit | 필수 reference validator 강화 |
| 이벤트 중복 등록 | 중간 | 중간 | 로그 카운터/구독자 수 assert | bind/unbind 표준화 |
| 런타임 동기화 누락 | 중간 | 높음 | owner 값 변경 후 applier 반영 확인 | owner->applier 단방향 이벤트 |
| 기능군 부분 이전 중 임시 불일치 | 높음 | 중간 | 단계별 feature flags 테스트 | 마이그레이션 토글/롤백 커밋 |
| debug/release 동작 충돌 | 중간 | 높음 | debug off 상태 비교 시나리오 | debug owner 격리, 빌드 분기 |

---

## Validation Plan

| 테스트 이름 | 목적 | 절차 | 기대 결과 | 실패 시 의심 지점 |
|---|---|---|---|---|
| Flashlight Toggle Ownership Test | 단일 writer 보장 | 토글 입력→owner 변경→applier 반영 추적 | enabled 상태 1회만 변경 | session restore/debug override 경합 |
| Flashlight Save/Restore Test | 저장/복원 일관성 | 층 이동 전후 charge/enabled 비교 | 값 유지 | serializer/restore 순서 |
| Fog Bypass Consistency Test | bypass 전파 확인 | debug bypass on/off 후 door/enemy visibility 비교 | 모두 일치 | consumer polling fallback |
| Scene Transition Persistence Test | 씬 전환 유지성 | 5F→1F 루프 중 상태 스냅샷 수집 | owner 상태 연속 | bootstrap 시 초기화 덮어쓰기 |
| UI Runtime Match Test | UI 표시-실제값 일치 | HUD 값과 runtime owner 값 비교 | 1:1 일치 | binder direct read 잔존 |
| Duplicate Writer Regression Test | 중복 writer 제거 검증 | writer별 호출 카운트 기록 | owner 외 writer 0 | legacy direct method call |
| Inspector Binding Integrity Test | 인스펙터 연결 안정성 | scene별 composition validation | 필수 binding 누락 없음 | prefab/scene reference 깨짐 |

---

## Final Recommendation

### 지금 당장 가장 먼저 정리할 기능군 Top 3
1. **Save/Load + Runtime Player State (Flashlight/Health/Inventory)**
2. **Fog/Visibility + Debug Bypass 경계**
3. **UI Settings 접근 경로 (Theme/QuickSlot/HUD sizing)**

### 절대 먼저 건드리면 안 되는 민감 구간
- `RMainEscape_Lobby -> RMainScene_5F -> ... -> 1F` 씬 라우팅 실동작 로직 자체(초기 단계에서 구조만 감싸고 동작 변경 금지).
- `RRunSessionController`의 씬 전환 훅(`HandleSceneLoaded`) 즉시 대수술 금지.

### 가장 안전한 시작 지점
- UI 읽기 경로부터 owner interface화 (`IUiSettingsReadModel`) → 동작 변경 없이 의존만 축소.

### 가장 위험한 source-of-truth 붕괴 사례
- flashlight enabled 상태: session restore/default 적용 + runtime 토글 + debug 표현 적용이 교차.

### 가장 먼저 owner 선언해야 하는 옵션
- `FlashlightEnabled` (이유: 플레이 감각/UI/포그/저장 전부에 영향).

### 런타임 상태와 저장 설정을 반드시 분리해야 하는 후보
- `flashlight enabled/charge`, `player health`, `inventory`, `run routing override`.

### Naming Convention 제안
- 인터페이스: `I` 접두어 필수 (`IFlashlightStateReadModel`, `IRunStateSerializer`).
- Owner: `XxxOwner`
- Controller(입력/오케스트레이션): `XxxController`
- Provider(Read-only): `XxxProvider` 또는 `IXxxReadModel`
- Applier: `XxxApplier`
- Persistence: `XxxPersistence` 또는 `IXxxSerializer`

### 구현 전 사용자 승인 필요 결정 포인트
1. `RRunSessionController` legacy saved fields를 언제 snapshot/store 단일 경로로 수렴할지 여부(저장 호환 전략 포함).
2. 라우팅 owner는 scene-local `RRunSessionController`로 유지한다. 장기적으로 fallback/alignment asset을 얼마나 남길지 결정 필요.
3. Debug 기능을 배포 빌드에서도 유지할지(범위/권한).
4. Fog consumer를 polling에서 event 구독형으로 전환할 범위.

---

## 배치 작업 가이드 (문서 따로가 아니라 실제 작업 배치 방식)

초보자 기준으로, 실제 리팩터링 작업은 아래 순서대로 **작게** 배치하면 안전하다.

1. **읽기 경로 먼저 배치**
   - UI/Binder에서 settings 직접 접근을 `I...ReadModel`로 바꾸기.
   - 이 단계는 동작 변화 없이 컴파일/표시만 확인.

2. **쓰기 경로 배치**
   - flashlight 한 기능만 골라 writer를 owner 하나로 제한.
   - 기존 writer는 owner 호출로 포워딩만 하게 유지.

3. **적용자 분리 배치**
   - owner는 값만 보관, applier가 `WasdPlayerController` 반영.

4. **저장 분리 배치**
   - serializer를 분리하고 session은 orchestration만.

5. **기능군 단위 회귀**
   - flashlight 통과 후 fog, 이후 debug 순서.

> 작동이 안 되면 보강 코드 추가보다 **원인분석(누가 writer인지, 어느 lifecycle에서 덮였는지)**을 먼저 수행한다.

