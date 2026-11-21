# 3D 아트 인수인계

프로젝트 파일 확인 기준은 UnityJamTest의 `art/3d` 커밋 `43e75a7`이다. 제작 과정은 작업 채팅, 제작 스크립트, 담당자 설명을 참고했다.

## 1. 맡은 범위와 결과물

환경 소품, 1인칭 팔 모델, 오디오 파일을 팀 저장소에 반영했다. 아래 환경 경로의 기준은 `Assets/Arts/Models/gg/`다.

- 가구와 건축 — `Shelf/Shelf.fbx`, `Table/Table.fbx`, `Wall/Wall.fbx`, `Corner/Corner.fbx`, `Floor/`, `WoodenSign/WoodenSign_1to2_RefinedFrame.fbx`.
- 지붕 — `GableRoof/GableRoof.fbx`, `GableRoof/GableRoof_18m_RefinedFraming.fbx`, `GableRoof_18m/GableRoof_18m.fbx`.
- 가마솥과 소품 — `Cauldron/Cauldron.fbx`, `Cauldron/Cauldron_boolean.fbx`, `Cauldron/Cauldron_Interior_Volume.fbx`, `PotionBottle/PotionBottle/PotionBottle.fbx`, `Lantern/Lantern/Lantern.fbx`, `Candle/Candle.fbx`, `Campfire/Campfire.fbx`.
- 식생 — `Mushrooms/GreenMushroom/GreenMushroom.fbx`, `Mushrooms/PinkMushroom/PinkMushroom.fbx`, `Mushrooms/SmallMushroom/SmallMushroom.fbx`, `MossPatch/MossPatch.fbx`, `Ivy/HangingIvy/HangingIvy.fbx`, `Ivy/IvyVine/IvyVine.fbx`.
- 오디오 — `Soundeffect/`에 `bgm.wav`, `boilingsound.wav`, `paper.mp3`, `splash.wav`, `zipper.wav`.
- 1인칭 팔 — `Assets/Arts/Models/first-person-arms-rigged.fbx`. 약초 잡기·놓기는 별도 제작물이다. `first_person_arms_herb_motion.glb`에 2.4초 `Herb_Grab_Release` 클립이 있고, `herb_grab_release.blend`에 1인칭 카메라와 예시 약초가 있다. 작업 기록의 부착·분리 시점은 각각 0.77초와 1.67초이며 오른손 소켓은 `HerbSocket.R`이다. 이 클립의 Unity 연결 상태는 확인 필요.

## 2. 소스 코드 구조

- 담당 범위의 런타임 C# 구현은 해당 없음. 아트 에셋과 함께 포션병의 `PotionGlass.shader`, 벽등의 `LanternGlass.shader`가 각 모델 폴더에 있다.
- `Assets/Scripts/Core/SceneFlow.cs`는 직렬화된 `nextSceneName`을 사용해 씬을 단일 모드에서 비동기로 로드한다. `Assets/Scenes/`의 직렬화 설정은 `00_Title` → `01_Order` → `02_Workshop` → `01_Order` 순으로 이어진다.
- `Assets/Scripts/Workshop/WorkshopCauldron.cs`는 양조 상태와 남은 시간을 관리하고 `potionSurface`를 참조한다. 같은 폴더의 `WorkshopShelf.cs`는 재고 배치에 사용하는 `stockRoot`를 참조한다. 담당 FBX별 실제 씬·프리팹 연결은 확인 필요.

## 3. 데이터 구조

- 담당 범위에서 추가한 별도 런타임 데이터 파일은 해당 없음.
- 연관 데이터는 `Assets/GameData/Ingredients.asset`과 `Assets/Scripts/Data/IngredientCatalog.cs`에 있다. `kinds`는 재료 종류 목록, `variants`는 종류에 속한 변형 목록이다. 변형의 `id`는 식별자, `displayName`은 표시명, `prefab`과 `icon`은 모델 프리팹과 표시 이미지 참조다. `PotionCatalog.cs`가 재료 카탈로그를 참조하고, `Assets/Scripts/Core/RoundDirector.cs`에서 `Assets/Scripts/Data/CatalogLoader.cs`의 `Load`를 호출한다. 담당 모델이 할당된 구체적 카탈로그 항목은 확인 필요.

## 4. 아트 파이프라인

1. OpenAI 이미지 생성으로 약초창고 쿼터뷰와 개별 소품 시안을 만들고 Meshy.ai에서 사용할 참고 이미지로 준비했다. Meshy에서 생성한 GLB를 Blender로 가져온 뒤 메시와 재질을 수정했다. 선반은 기둥과 돌출부, 지붕은 트러스와 서까래, 장작은 그을음과 나뭇결을 다듬었다.
2. 가마솥은 몸체에 `Boolean Difference`의 `Exact` 연산을 적용하고 손잡이를 몸체의 하위 오브젝트로 분리했다. `Interior Volume - Boolean Cutter`는 내부 체적을 확인하는 별도 메시이며, 체적 FBX는 몸체와 같은 원점으로 맞췄다. 편집 파일명은 `Cauldron_Procedural_Boolean.blend`다. 바닥 재질은 반복 석재 타일 위에 바닥 전체용 얼룩 오버레이를 한 번 얹는 구조다. 편집 파일명은 `Natural_Plum_Slate.blend`다.
3. 담당자 설명에 따르면 Blender에서 GLB를 임포트해 수정한 후 기본 FBX 내보내기 설정으로 익스포트했다. 별도로 보존된 가마솥과 팔의 자동 내보내기 스크립트에는 전방 `-Z`, 상방 `Y`, 단위 스케일 적용, `add_leaf_bones=False`, `bake_anim=False`가 기록돼 있다. 이 값은 해당 스크립트 작업의 설정이다.
4. Unity 쪽에서는 FBX와 `BaseColor`, `Normal`, `MetallicSmoothness` PNG를 모델 폴더에서 함께 확인한다. 포션병과 벽등의 유리 파츠는 각 폴더의 전용 `.mat`과 `.shader`를 사용하며 두 머티리얼의 렌더 큐는 `3000`이다. 바닥 제작 기록에는 텍스처 `Wrap Mode = Repeat`, 노멀 `Texture Type = Normal Map`을 적용하도록 안내돼 있다. 저장소에서 개별 FBX의 임포트 `.meta`를 확인할 수 없는 경우 실제 리그·스케일·머티리얼 슬롯 설정은 확인 필요. 편집 원본의 팀 공유 위치도 확인 필요.

### 작업 채팅에서 확인한 프롬프트

- 약초창고 콘셉트 — “전체모습은 쿼터뷰로 뽑아줘. 스타일은 로우폴리 3D 스타일로” 요청했다. 배치도에서 연갈색은 선반, 주황색은 카운터, 검은 원은 가마솥으로 지정했다. 파스텔톤, 어두운 석재 바닥, 이끼·버섯, 덩굴과 촛불을 포함했다.
- 모닥불 이미지 — 이미지 생성 프롬프트 첫 문장은 “Make ONE isolated compact campfire prop for Meshy image-to-3D, in the warm hand-painted low-poly fantasy style of the supplied herb storeroom.”이다. 후속 요청에서는 불꽃을 제거하고 중앙의 검은 그을음과 껍질의 갈라짐을 남겼다.
- Meshy에서 에셋별로 입력한 프롬프트 원문은 확인 필요.

## 5. 에디터 설정

- Unity `6000.3.28f1` (`ProjectSettings/ProjectVersion.txt`). `Packages/manifest.json`에 URP `17.3.0`, Input System `1.20.1`, Unity Pipeline `1.0.0`이 있다. `ProjectSettings/GraphicsSettings.asset`의 기본 렌더 파이프라인 참조는 `Assets/Settings/PC_RPAsset.asset`이다.
- `ProjectSettings/EditorBuildSettings.asset`에서 `00_Title`, `01_Order`, `02_Workshop`이 활성이고 `SampleScene`은 비활성이다. 입력 액션 파일은 `Assets/InputSystem_Actions.inputactions`다. 최종 배포 빌드 대상은 확인 필요.
- `Assets/Editor/ArtPipelineMenu.cs`의 `Potion Shop/Art` 메뉴에서 재료·고객·테마 데이터와 주문·공방·포트레이트 테스트 씬을 열 수 있다. 담당자가 추가한 Unity 에디터 도구는 해당 없음. 별도 로컬 작업 도구로 GLB를 불러와 이동·회전·스케일을 조정하고 내보내는 1인칭 팔 웹 편집기와, 음높이 조정·미리듣기·WAV 저장을 지원하는 효과음 웹 편집기를 제작했다.

## 6. 외부 의존성

- 온라인 제작 서비스는 OpenAI 이미지 생성과 Meshy.ai다. Meshy.ai API를 사용하려면 키 필요. Blender에서 진행한 메시·재질 수정, GLB 및 FBX 내보내기와 로컬 편집기 작업은 오프라인으로 진행할 수 있다. Mixamo는 리타기팅 방법을 검토했다.
- 담당 결과물은 프로젝트 내 FBX·PNG·머티리얼·셰이더·오디오로 제공한다. 해당 에셋 자체를 불러오는 데 온라인 API는 필요하지 않다. 저장소를 처음 받거나 LFS 파일을 갱신할 때는 Git LFS 원본을 내려받는다.

## 7. 알려진 문제와 재작업 시 주의점

- 1인칭 팔과 가마솥의 Unity 배치 임포트는 작업 로그에서 라이선스 초기화 실패로 중단된 것을 확인했다. 당시 Blender 재임포트에서 팔 관절 변형과 가마솥 내부 형상을 확인했다. 팀 프로젝트에서의 최종 Unity 임포트 및 빌드 검증 결과는 확인 필요.
- 박공 지붕에는 기본형과 18m 변형이 함께 있다. 교체 전에 씬에서 참조 중인 모델을 확인하고, 가마솥 수정 시 몸체와 체적의 원점을 맞춘다.
- 에셋을 교체할 때 기존 Unity 참조를 유지하려면 경로와 기존 `.meta`를 보존한다. 변경한 LFS 파일은 원본을 정상적으로 내려받을 수 있는지 확인한다.
