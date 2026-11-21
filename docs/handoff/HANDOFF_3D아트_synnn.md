# 3D 아트 인수인계

확인 기준은 UnityJamTest의 `art/3d` 커밋 `43e75a7`이다.

## 1. 맡은 범위와 결과물

환경 소품, 1인칭 팔 모델, 오디오 파일을 팀 저장소에 반영했다. 아래 환경 경로의 기준은 `Assets/Arts/Models/gg/`다.

- 가구와 건축: `Shelf/Shelf.fbx`, `Table/Table.fbx`, `Wall/Wall.fbx`, `Corner/Corner.fbx`, `Floor/`, `WoodenSign/WoodenSign_1to2_RefinedFrame.fbx`.
- 지붕: `GableRoof/GableRoof.fbx`, `GableRoof/GableRoof_18m_RefinedFraming.fbx`, `GableRoof_18m/GableRoof_18m.fbx`.
- 가마솥과 소품: `Cauldron/Cauldron.fbx`, `Cauldron/Cauldron_boolean.fbx`, `Cauldron/Cauldron_Interior_Volume.fbx`, `PotionBottle/PotionBottle/PotionBottle.fbx`, `Lantern/Lantern/Lantern.fbx`, `Candle/Candle.fbx`, `Campfire/Campfire.fbx`.
- 식생: `Mushrooms/GreenMushroom/GreenMushroom.fbx`, `Mushrooms/PinkMushroom/PinkMushroom.fbx`, `Mushrooms/SmallMushroom/SmallMushroom.fbx`, `MossPatch/MossPatch.fbx`, `Ivy/HangingIvy/HangingIvy.fbx`, `Ivy/IvyVine/IvyVine.fbx`.
- 오디오: `Soundeffect/`에 `bgm.wav`, `boilingsound.wav`, `paper.mp3`, `splash.wav`, `zipper.wav`.
- 1인칭 팔: `Assets/Arts/Models/first-person-arms-rigged.fbx`. 별도 애니메이션의 프로젝트 반영 위치는 확인 필요.

## 2. 소스 코드 구조

- 담당 범위의 런타임 C# 구현: 해당 없음. 아트 에셋과 함께 포션병의 `PotionGlass.shader`, 벽등의 `LanternGlass.shader`가 각 모델 폴더에 있다.
- `Assets/Scripts/Core/SceneFlow.cs`는 직렬화된 `nextSceneName`으로 씬을 단일 모드에서 비동기 로드한다. 빌드에 활성화된 순서는 `Assets/Scenes/00_Title.unity`, `01_Order.unity`, `02_Workshop.unity`다.
- `Assets/Scripts/Workshop/WorkshopCauldron.cs`는 양조 상태와 남은 시간을 관리하고 `potionSurface`를 참조한다. `WorkshopShelf.cs`는 `stockRoot`를 직렬화한다. 담당 FBX와 각 씬 오브젝트의 연결은 확인 필요.

## 3. 데이터 구조

- 담당 범위에서 추가한 별도 런타임 데이터 파일: 해당 없음.
- 연관 데이터는 `Assets/GameData/Ingredients.asset`과 `Assets/Scripts/Data/IngredientCatalog.cs`를 참고한다. `kinds`는 재료 종류 목록, `variants`는 종류에 속한 변형 목록이다. 변형의 `id`는 식별자, `displayName`은 표시명, `prefab`과 `icon`은 각각 모델 프리팹과 표시 이미지 참조다. `Assets/Scripts/Data/CatalogLoader.cs`에 종류와 변형의 조회 구조가 있다. 담당 모델이 할당된 항목은 확인 필요.

## 4. 아트 파이프라인

- 저장소에서 확인되는 산출물 구성은 모델 FBX, PNG 텍스처, 일부 Unity 머티리얼과 셰이더다. 원본 제작 도구, 편집 원본 위치, 작업 순서, 실제 사용 프롬프트는 프로젝트 파일 기준 확인 필요.
- 가마솥 몸체와 내부 체적이 별도 FBX로 나뉜다. 포션병과 벽등은 각 모델 폴더의 전용 유리 머티리얼과 셰이더를 함께 확인한다. 색상·노멀·금속도 및 매끄러움 텍스처는 파일명 `BaseColor`, `Normal`, `MetallicSmoothness`로 구분한다. 원본 레이어와 머티리얼 슬롯 매핑은 확인 필요.
- FBX 내보내기 축·단위·리그 및 Unity 임포트 프리셋은 확인 필요. 교체 전에 기존 텍스처와 머티리얼 연결 및 팔의 리그 설정을 Inspector에서 확인한다.

## 5. 에디터 설정

- Unity `6000.3.28f1` (`ProjectSettings/ProjectVersion.txt`). `Packages/manifest.json`에 URP `17.3.0`, Input System `1.20.1`, Unity Pipeline `1.0.0`이 있다.
- `ProjectSettings/EditorBuildSettings.asset`의 활성 씬은 2절의 세 씬이다. `Assets/Scenes/SampleScene.unity`는 비활성이다. 최종 빌드 대상과 직접 만든 Unity 에디터 도구의 등록 여부는 확인 필요.

## 6. 외부 의존성

- 담당 결과물은 프로젝트 내 FBX·PNG·머티리얼·셰이더·오디오 파일이다. Git LFS를 통해 추적되는 파일은 원본을 받은 뒤 작업한다.
- 이 에셋의 게임 실행 중 온라인 서비스 또는 API 사용 여부는 확인 필요. 제작 단계의 서비스 설정과 오프라인 범위는 확인 필요.

## 7. 알려진 문제와 재작업 시 주의점

- 저장소에 기록된 담당 에셋의 결함, 임시 처리 및 실제 빌드 검증 결과는 확인 필요.
- 박공 지붕에는 기본형과 18m 변형이 함께 있으므로, 교체 전에 씬에서 참조 중인 모델을 확인한다. 가마솥을 수정할 때는 몸체와 내부 체적 파일을 각각 확인한다.
- 기존 파일을 교체할 때는 Unity 참조를 유지하도록 경로와 `.meta`를 보존하고, 변경된 LFS 파일이 정상적으로 내려받아지는지 확인한다.
