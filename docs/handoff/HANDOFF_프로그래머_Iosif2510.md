# 프로그래머 인수인계서

기준 커밋 `25edc5d`의 코드, 직렬화 에셋, 프로젝트 설정 및 Git 작성 이력을 기준으로 정리했다. 공동 작업물의 세부 담당 경계는 확인 필요.

## 1. 맡은 범위와 결과물 목록

| 담당 범위 | 주요 결과물 |
| --- | --- |
| 주문 라운드, 채점, 씬 전환 | `Assets/Scripts/Core/RoundDirector.cs`, `Assets/Scripts/Core/RoundJudge.cs`, `Assets/Scripts/Core/SceneDimmer.cs`, `Assets/Scenes/01_Order.unity`, `Assets/Scenes/02_Workshop.unity` |
| 카운터 UI와 대사·결과 연출 | `Assets/Scripts/Core/OrderSceneView.cs`, `Assets/Scripts/Core/OrderEntrance.cs`, `Assets/Scripts/Core/OrderDialogueView.cs`, `Assets/Scripts/Core/RoundResultView.cs`, `Assets/Prefabs/UI/OrderCanvas.prefab` |
| 작업실 조작과 제조 연출 | `Assets/Scripts/Workshop/`, `Assets/Scripts/Player/FirstPersonController.cs`, `Assets/Prefabs/Workshop/`, `Assets/Prefabs/UI/WorkshopCanvas.prefab`, `Assets/Arts/Shaders/Liquid.shadergraph`, `Assets/Arts/Shaders/Liquid_Bubble.shadergraph` |
| 주문 데이터 이관과 검증 | `Assets/GameData/`, `Assets/Scripts/Data/`, `Assets/Editor/CatalogValidationMenu.cs`, `Docs/MVP_DATA_MIGRATION_PLAN.md` |
| 초상화와 아트의 런타임 연결 및 검수 도구 | `Assets/Scripts/Portrait/`, `Assets/Prefabs/Portrait/PortraitView.prefab`, `Assets/Scenes/Tests/PortraitTest.unity`, `Assets/Editor/ArtPipelineMenu.cs`, `Assets/Editor/PortraitTestDriverEditor.cs`, `Docs/ARTIST_GUIDE.md` |
| 선택형 온라인 대사와 로컬 대사 연결 | `Assets/Scripts/Core/OpenAiDialogueProvider.cs`, `Tools/OrderDialogueRelay/relay.mjs`, `Tools/OrderDialogueRelay/order-dialogue.ko.txt`, `Assets/GameData/DialogueCatalog.asset` |

## 2. 소스 코드 구조

| `Assets/Scripts/` 하위 폴더 | 주요 스크립트와 역할 |
| --- | --- |
| `Core/` | `RoundDirector`가 씬 전환 중에도 주문, 재료, 점수와 제한 시간을 유지한다. `SceneDimmer`가 페이드와 로딩을 처리한다. `OrderSceneView`가 카운터의 단계별 입력과 화면을 연결한다. `OrderDialogueView`, `DialogueTimeline`, `DialogueLineLayout`이 대사 표시와 줄바꿈을 처리하고 `RoundResultView`가 결과와 도장을 표시한다. |
| `Data/` | `CatalogLoader`가 카탈로그를 검증하고 ID 조회용 구조를 만든다. `OrderGenerator`가 손님 외형, 질병, 레시피와 병·잉크 정답을 정한다. `AppearanceBuckets`는 일반 생성에서 사용할 외형 후보를 구성한다. |
| `Workshop/`, `Player/` | `FirstPersonController`가 1인칭 이동과 시점을 처리한다. `WorkshopInteraction`이 대상 감지, 집기·보관·회전·제조 제출을 제어한다. `WorkshopShelf`와 `WorkshopCauldron`이 선반과 솥의 참조를 관리한다. `WorkshopIngredientMotion`, `LiquidMotion`, `LiquidBubbleController`가 재료와 액체를 연출하고 `WorkshopHud`와 `PocketModelDisplay`가 작업실 UI·주머니 미리보기를 갱신한다. |
| `Portrait/`, `Art/` | `PortraitView`가 상점과 테스트 씬의 공용 초상화 진입점이다. `PortraitLayers`, `PortraitColorModule`, `PortraitSpineRig`가 레이어·색·Spine 애니메이션을 담당한다. `CounterCurtain`, `UITheme`, `UiTypography`가 커튼과 UI 스타일을 담당한다. |

빌드에서 활성화된 첫 씬은 `01_Order`다. 손님 받기를 누르면 `Waiting`에서 시작해 온라인 대사 사용 시 `Preparing`을 거쳐 `Consult`가 된다. 작업실 버튼으로 `02_Workshop`의 `Storage`에 들어가 세 재료를 제출하고 제조를 마치면 `01_Order`의 `Prescribe`로 돌아온다. 병과 잉크를 결정하면 `Ending`에서 대사·결과 연출을 실행한 뒤 `Result`가 되며, 결과를 닫으면 다음 주문을 받을 수 있다. `Consult`부터 `Prescribe`까지 같은 제한 시간을 사용하며, 시간이 끝나면 `Ending`으로 가고 작업실에서도 카운터로 돌아온다. 타이틀 씬의 `SceneFlow`는 `01_Order`를 가리킨다.

## 3. 데이터 구조

`01_Order`의 `RoundDirector.catalog`가 `Assets/GameData/PotionCatalog.asset`을 참조한다. `RoundDirector.BeginOrder()`에서 최초 주문 시 `CatalogLoader.Load()`가 하위 ScriptableObject를 읽으며, `OrderGenerator`가 조회 결과로 주문을 생성한다.

| `Assets/GameData/` 하위 파일 | 주요 필드와 읽는 곳 |
| --- | --- |
| `PotionCatalog.asset` | 손님, 질병·레시피, 재료, 처방 규칙, 수치, 팔레트, 캐릭터 말투, 로컬 대사의 참조를 묶는다. `RoundDirector`, `CatalogLoader`가 읽는다. |
| `Customers.asset`, `PortraitColors.asset` | 손님 ID·성별·종족, 고정 외형 여부, 파츠 Sprite, 색 후보, 홍채 마스크와 Spine JSON, 팔레트 ID·색 값. 외형 생성과 `PortraitView`에서 사용한다. |
| `DiseasesAndRecipes.asset` | 질병의 `recipeId`, 증상 의미·대사 예시, 금지어, 도감 설명·일러스트·강조 효과, 레시피의 `ingredientVariantIds`. `OrderGenerator`가 주문 구성에, `RoundJudge`가 재료 매칭에, `OpenAiDialogueProvider`가 증상 요청·응답 검증에 사용한다. |
| `Ingredients.asset` | 종류 아래에 재료 변형 ID, 식별 특징, 프리팹과 도감 썸네일이 있다. `CatalogLoader`가 종류·변형 ID를 검증하고, `WorkshopInteraction`이 재료 변형 ID로 제출한다. |
| `PrescriptionRules.asset` | `bottles`는 성별과 병 모양 대응, `inks`는 우선순위·번호·이름·외형 조건 및 예외다. `OrderGenerator.ResolveBottle`과 `ResolveInk`가 정답을 고르고, `RoundJudge`가 제출값과 비교한다. |
| `RoundTuning.asset` | 제한 180초, 집기 거리 2.5, 제조 4초, 일치 재료 한 개·병·잉크 각각 20점, 도장 기준 60·100점, 대사 속도·줄 길이·반응 간격. `RoundDirector`, 작업실, 대사 UI와 `RoundJudge`가 사용한다. 시간 보너스는 `floor(남은 초 × 0.1)`이다. |
| `DialogueCatalog.asset`, `CharacterVoices.asset` | 전자는 질병 ID에 연결된 증상 대사와 `rcv_*`, `aft_*`, `timeout` 반응 키를 담는다. 후자는 손님 코드, 말투, 성격, 잡담 소재를 담는다. `CatalogLoader`가 키와 참조를 검증하고, `OrderGenerator`와 `OpenAiDialogueProvider`가 해당 주문의 대사를 구성한다. |
| `UITheme.asset` | 공용 UI 스타일 에셋이다. 작업실 UI에서 주머니 프레임과 상태별 색을 사용한다. |

## 4. 아트 파이프라인

- 초상화는 `Docs/Portraits/manifest.json`과 `Docs/Portraits/README.md`에서 파츠를 확인하고, `Assets/Arts/Portraits/`의 파츠와 Spine JSON을 `Customers.asset`에 연결한다. 기본 적층 순서는 아래부터 뒷머리 → 홍채 색 마스크 → 베이스 → 감은 눈 → 앞머리다. 베이크 파츠는 1024×1536 RGBA PNG 캔버스와 투명 여백을 유지한다. 가이드의 임포트 설정은 `Sprite (2D and UI)`, `Single`, 중앙 피벗이다. ELF_F1 베이스의 `.meta`에는 100 PPU와 Read/Write 꺼짐이 기록돼 있다. `PortraitTest`에서 레이어, 색, 눈 감김, `idle`, 엔딩 틴트를 확인한다. ELF_F1 개별 설정과 별도 Spine 아틀라스 위치는 `Docs/ELF_F1_ASSET_SETUP.md`에 있다.
- 작업실에서 재료 FBX는 `Assets/Arts/Models/Ingredients/Models/`, 프리팹과 머티리얼은 `Assets/Prefabs/Workshop/IngredientS/`에 있다. 카탈로그의 변형 ID에 프리팹과 도감 Sprite를 연결하고 선반과 주머니에서 확인한다. `ING_A1.fbx.meta`에서 임포트 스케일 1을 확인할 수 있다. 솥 액체는 Unity Shader Graph에서 편집하며, 액체와 기포 연동 코드는 `Assets/Scripts/Workshop/`에 있다. 원본 모델 제작 도구·버전 및 FBX 내보내기 작업 순서는 확인 필요.
- 처방장 소스는 `Docs/Art/PotionWorkshopCabinet/`에 있다. 선반, 병 3개, 잉크 6개가 분리되어 있고 `color/`, `mask/`, `height/`, `normal/`과 `layout.json`으로 관리된다. Unity 연결본은 `Assets/Arts/UI/scene3_ui/Potions/`와 `01_Order` 씬의 처방 UI에서 확인한다. 생성 프롬프트 원문은 처방장의 `Docs/Art/PotionWorkshopCabinet/source/generation-prompts.txt`와 도감의 `Docs/PotionMemos/prompts/`에 있다. 그 외 초상화·3D 제작에 사용한 프롬프트와 원본 작업 파일은 확인 필요.

## 5. 에디터 설정

- `ProjectSettings/ProjectVersion.txt`의 Unity 버전은 **6000.3.28f1**이다. `ProjectSettings/GraphicsSettings.asset`은 `Assets/Settings/PC_RPAsset.asset`을 참조한다. `01_Order` 카메라는 `Order_Renderer2D.asset`에 해당하는 인덱스 1, `02_Workshop`은 기본 `PC_Renderer.asset`을 사용한다.
- `Packages/manifest.json` 및 `Packages/packages-lock.json`의 주요 패키지는 URP·VFX Graph 17.3.0, Input System 1.20.1, uGUI 2.0.0, 2D Animation 13.0.6, Unity Toon Shader 0.16.1-preview, Unity Pipeline 1.0.0, Newtonsoft.Json 3.2.1과 Git 의존성 LitMotion, UniTask, spine-csharp다.
- `ProjectSettings/EditorBuildSettings.asset`에서 `01_Order`, `02_Workshop`이 활성화돼 있고 `00_Title`, `SampleScene`은 비활성화돼 있다. `PortraitTest`는 목록에 없다. 배포용 플랫폼과 최종 배포 빌드 옵션은 확인 필요.
- `Potion Shop/Data/Validate Catalog`로 카탈로그를 검증한다. `Potion Shop/Art` 메뉴로 재료·손님·팔레트·테마와 카운터·작업실·초상화 테스트 씬을 연다. `PortraitTestDriverEditor`는 파츠·색·리그 표시를 점검하고, `ArtTestSettings`는 에디터에서 주문의 외형을 고정할 수 있다. `WorkshopRayGizmo`는 작업실 상호작용 레이를 표시한다. `ArtPipelineSetup.Install()`은 아트 연결을 초기화하는 수동 마이그레이션 함수이며 플레이 중이거나 저장하지 않은 씬이 있으면 중단한다.

## 6. 외부 의존성

- `01_Order`의 `RoundDirector`에서 `Use Dialogue Api`를 켜면 Unity가 `OpenAiDialogueProvider`를 통해 같은 PC의 `127.0.0.1:8765`에 주문 대사를 요청한다. 중계는 저장소 루트에서 `node Tools/OrderDialogueRelay/relay.mjs`로 시작한다. 프로젝트 문서 기준 실행 환경은 Node.js 22다. 중계 프로세스 환경 또는 터미널의 가려진 입력란에 키 필요. 중계는 `order-dialogue.ko.txt`와 주문 데이터로 OpenAI Responses API의 구조화된 출력을 요청한다. 프롬프트 변경 후에는 중계를 재시작한다.
- 기준 커밋의 `Use Dialogue Api`는 꺼져 있다. 로컬 대사, 외형 생성, 레시피, 작업실 상호작용, 채점 및 결과 연출은 프로젝트에 포함된 코드와 에셋으로 동작한다. 온라인 옵션을 켜도 준비 상한 10초를 넘기거나 공급자가 요청·응답 검증 오류를 반환하면 로컬 대사로 진행한다. 시연 환경에서의 중계 연결과 실제 API 응답은 확인 필요.

## 7. 알려진 문제와 임시 처리, 재작업 시 주의점

- 타이틀에서 시작하는 빌드가 필요하면 `00_Title`을 활성화하고 시작 버튼에서 `01_Order`로 넘어가는지 확인한다. 기준 설정에서는 카운터가 첫 씬이다.
- 재료를 교체할 때 프리팹의 재료 ID와 카탈로그를 함께 맞춘다. `CatalogLoader.CheckRecipe`는 레시피별로 정확히 세 재료와 서로 다른 재료 종류를 검증한다. 수정 후 `Validate Catalog`로 참조 누락을 확인한다.
- 작업실은 `Interactable` 레이어를 사용하며, 레이 원점 안쪽에 겹친 콜라이더도 검사한다. 재료·솥 프리팹 교체 후 해당 레이어, 콜라이더 및 레이 도달을 함께 확인한다.
- 기준 `ArtTestSettings.asset`의 `useFixedOrder`는 꺼져 있다. 고정 주문은 일반 주문의 잉크 균등 추첨을 우회하므로 검수에 사용한 뒤 끈다. `EditorSettings.asset`은 Play Mode 진입 시 Domain Reload를 건너뛰도록 설정되어 있다. 정적 인스턴스나 에디터 주문 팩터리를 변경할 경우 `RoundDirector`의 `SubsystemRegistration` 초기화를 함께 확인한다.
- 시간 초과는 내부적으로 실패 도장 키를 공유하며, `RoundResultView`가 `TimedOut`을 보고 전용 도장 이미지를 선택한다. 도장 매핑을 수정하면 실패·시간 초과를 각각 확인한다.
- 최종 Player 빌드와 실제 시연 환경의 전 구간 동작, 아트 검수 완료 상태는 확인 필요.
