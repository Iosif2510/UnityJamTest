# 아티스트 작업 가이드

**에셋 연결 → 저장 → Play로 확인 → Play 종료 후 수정** 순서로 작업합니다.

Unity 상단의 `Potion Shop > Art` 메뉴를 사용하세요. 모델·그림은 Inspector에 드래그해서 연결하고, 배치는 Prefab Mode에서 편집합니다.

> **수정은 Play를 종료한 뒤 하세요.** 씬에서 바꾼 값은 사라질 수 있지만, SO·공유 재질의 변경은 원본에 남을 수 있습니다.

## 1. 작업별 바로가기

| 하려는 작업 | 열 메뉴 | 확인할 위치 |
| --- | --- | --- |
| 재료 모델 교체 | `Ingredients` | 게임용 프리팹의 `VisualRoot` |
| 초상화 PNG 연결 | `Customers` | 해당 인물의 Sprite 필드 |
| 헤어·눈의 공용 색 보정 | `Portrait Colors` | 색의 `Color` 값 |
| 초상화 모듈 테스트 | `Open Portrait Test` | `PortraitTestDriver` Inspector |
| 특정 손님으로 게임 확인 | `Test Settings` → `Open Counter` | 실제 카운터 화면 |
| 재료 집기·주머니 확인 | `Open Workshop` | 실제 창고 화면 |
| 주머니 프레임·색 변경 | `Shared UI Theme` | Pocket 관련 필드 |

위 메뉴는 모두 `Potion Shop > Art` 아래에 있습니다. 데이터 검사는 `Potion Shop > Data > Validate Catalog`에서 실행합니다.

**용어:** SO는 목록·설정을 담은 에셋, 프리팹은 게임 오브젝트의 재사용 원본입니다. Prefab Mode는 그 원본을 여는 편집 화면이며, Inspector는 선택한 항목의 설정 화면입니다.

## 2. 재료 모델 바꾸기

### 연결·저장

1. **Ingredients**를 열고 `Kinds → Variants`에서 원하는 재료 ID를 찾습니다.
2. `Prefab`에 연결된 프리팹을 열어 **Prefab Mode**로 들어갑니다. 현재 재료 원본은 `Assets/Prefabs/Graybox/ingredient_*.prefab`에 있습니다.
3. **`VisualRoot` 아래 모델**을 교체하고 재질·위치·방향·크기를 맞춥니다.
4. 프리팹을 저장하고 **Validate Catalog**를 실행합니다.

```text
ingredient_*               고정 ID·상호작용 유지
├─ VisualRoot              모델·재질·시각 변환 편집
└─ InteractionColliders    선택 판정 영역 유지
```

| 변경한 것 | 추가로 할 일 |
| --- | --- |
| 같은 프리팹 내부의 모델·재질 | 프리팹 저장 |
| 게임용 프리팹 자체 | SO의 `Prefab` 참조 교체 + `WarehouseIngredient > Variant Id` 일치 확인 |
| 모델 크기·형상이 크게 달라짐 | 프로그래머와 Collider 범위 확인 |

> 루트의 scale은 **1**로 유지하세요. 새 모델의 Collider·Rigidbody가 중복되지 않게 하고, 기존 ID와 `InteractionColliders`를 삭제하지 마세요.

### 플레이 확인

**Open Workshop → Play → 게임 화면 클릭** 순서로 시작합니다.

| 입력 | 동작 |
| --- | --- |
| 좌클릭 | 재료 집기 / 준비된 재료를 솥에 투입 |
| 우클릭 | 들고 있는 재료 반납 |
| F | 주머니에 넣기 |
| Esc | 커서 잠금 해제 |

- 선반·손·주머니에서 **같은 모델의 크기와 방향**을 확인합니다.
- 반납했을 때 원래 위치로 돌아오는지 확인합니다.
- 주머니 3칸을 채운 뒤 솥 투입을 확인합니다.

**알아두기:** SO에서 프리팹을 교체한 결과는 **다음 Play부터** 진열에 반영됩니다. 편집 화면에는 이전 배치 모델이 보일 수 있습니다. 주머니는 아이콘이 아닌 실제 모델을 렌더링하며, 현재 `SkinnedMeshRenderer`는 지원하지 않습니다.

## 3. 초상화 PNG 연결하기

### 파일 준비

| 항목 | 규격 |
| --- | --- |
| 폴더 | `Assets/Arts/Portraits/<인물 ID>/` |
| 파일명 예시 | `POR_HUM_M1_BASE.png` |
| 크기·형식 | **1024×1536**, RGBA PNG |
| 임포트 | Sprite (2D and UI), Single, 중앙 피벗 |
| 정렬 | 모든 레이어의 캔버스 유지. 투명 여백을 자르지 않음 |

### Sprite 연결

1. **Customers**를 열고 `Profiles`에서 `HUM_M1` 같은 **대문자 호출코드**를 찾습니다.
2. 아래 표에 따라 Sprite를 드래그해 연결합니다.
3. `Iris Regions`에 실제 그림에 맞는 눈 사각형 **1~2개**를 지정합니다.
4. SO를 저장하고 **Open Portrait Test**로 확인합니다.

| Inspector 필드 | 연결할 그림 | 필수 여부 |
| --- | --- | --- |
| `Base Portrait` | 몸·옷·얼굴·귀·입·뜬 눈 | 필수 |
| `Eye Closed` | 감은 눈과 눈 주변 피부 | 필수 |
| `Hair Front` | 앞머리 | 골렘 외 필수 |
| `Hair Back` | 뒷머리 | 긴 머리만 |
| `Slime Portrait` | 공통 엔딩 슬라임 | 슬라임 효과 확인 시 필요 |

`Slime Portrait`는 개별 인물 안이 아닌 Customers의 공통 필드입니다. 이전 시험용 `customer.*` 항목은 Retired로 보존되어 있으므로 새 그림을 연결하지 않습니다.

### 그림에서 확인할 것

- **BASE:** 홍채 자리만 투명하게 비웁니다. 동공·음영은 검정 반투명, 하이라이트는 흰색으로 남깁니다.
- **감은 눈:** 홍채 구멍까지 피부색으로 완전히 덮습니다.
- **헤어:** 명도 **70~100% 회색조**로 제작합니다. 골렘 장식은 BASE에 최종 색으로 넣습니다.
- **공용 색:** Portrait Colors의 `Color`만 보정합니다. `Value`는 판정용 식별자입니다.

눈 좌표 계산과 본 리그 연결은 [보충 자료](ARTIST_GUIDE_REFERENCE.md)에 있습니다. 성별·귀·송곳니·ID는 그림 연결을 위해 변경하지 마세요.

## 4. 초상화 모듈 테스트하기

1. **Open Portrait Test**를 엽니다.
2. Hierarchy에서 **PortraitTestDriver**를 선택합니다.
3. `Customers`, `Palette`, `View` 참조와 인물 ID·Hair·Eyes를 확인합니다. **골렘의 Hair는 None**입니다.
4. **Play**를 누르고 Inspector 아래 버튼을 사용합니다.

| 확인할 것 | 사용할 버튼 |
| --- | --- |
| 선택한 인물·색 적용, 처음 상태로 복귀 | **SO 입력 적용 / 초기화** |
| 레이어 영역·겹침 | **뒷머리/홍채/베이스/감은 눈/앞머리만 표시** |
| 단독 표시를 전체 모습으로 복귀 | **전체 합성 복원** |
| 감은 눈이 홍채 구멍을 덮는지 | **한 번 깜빡이기 / 감은 눈 유지** |
| 반복 깜빡임 | **자동 깜빡임 / 깜빡임 정지** |
| 전체 레이어 색 변화 | **엔딩 틴트** |
| 공통 슬라임으로 교체 | **슬라임 교체** |
| 연결된 본 애니메이션 | **연결한 리그 재생 / 리그 정지** |

기본 깜빡임은 **3~5초 간격, 0.1초 감김**입니다. 잘못된 색·미연결 Sprite·리그는 Inspector 상태 메시지에서 확인합니다. 리그가 없어도 정적 합성·색·깜빡임을 먼저 확인할 수 있습니다.

**권장 순서:** 전체 합성 → 레이어 단독 → 감은 눈 → 자동 깜빡임 → 본 움직임 → 엔딩 → 초기화.

테스트 씬은 주문·질병·시간 제한 없이 선택 인물을 보여주며 Draft도 확인할 수 있습니다. 버튼은 원본 SO를 저장하지 않습니다. 수정할 때는 **Play 종료 → SO/프리팹 수정·저장 → 재실행**하세요.

## 5. 실제 카운터에서 확인하기

1. **Test Settings**에서 `Use Fixed Order`를 켭니다.
2. `Customer Id`, `Hair Color`, `Eye Color`, `Seed`를 지정합니다. 색을 `Unspecified`로 두면 허용 목록에서 추첨합니다.
3. **Open Counter → Play**로 실제 화면을 확인합니다.
4. 확인을 마치면 **Use Fixed Order를 끕니다.**

초상화가 화면 높이의 60%로 표시될 때 귀·눈 색·헤어 색·송곳니가 보이고, 대사창·병 선택 UI와 겹치지 않는지 확인하세요. 모듈 테스트에서 엔딩 효과를 강제 재생한 것과 실제 판정에 따른 엔딩 확인은 구분합니다.

이 고정 설정은 Editor 전용입니다. 테스트를 위해 원본의 시간 제한·활성 상태·정답 데이터를 바꾸지 않습니다.

## 6. UI 그림·배치 바꾸기

| 대상 | 편집할 에셋 |
| --- | --- |
| 카운터 UI | `Assets/Prefabs/UI/OrderCanvas.prefab` |
| 창고 UI | `Assets/Prefabs/UI/WorkshopCanvas.prefab` |
| 코드로 생성되는 주머니 프레임·색 | **Shared UI Theme** |

1. 해당 프리팹을 **Prefab Mode**로 엽니다.
2. Image의 Sprite·Color, Text의 폰트·크기·정렬, RectTransform 배치를 수정하고 저장합니다.
3. 실제 씬을 Play해 버튼의 선택·비활성 상태와 긴 한국어 대사를 확인합니다.
4. Game View에서 **1920×1080 / 1280×720 / 다른 화면 비율**로 잘림을 확인합니다.

9-slice는 Sprite의 **Border**와 Image의 **Type = Sliced**를 함께 맞춥니다. 주머니 Theme의 `Pocket Frame`, `Pocket Empty/Filled/Background`는 다음 Play부터 반영됩니다. 색은 기존 패널·버튼·텍스트 테마에 맞춥니다.

> Button·Label·이름·컨트롤러 참조는 유지하세요. 새 오브젝트로 삭제·재생성하면 연결이 끊어질 수 있습니다.

## 7. 문제가 생겼을 때

| 증상 | 먼저 확인할 것 |
| --- | --- |
| 모델을 바꿨는데 진열이 그대로임 | 프리팹 저장 후 Play를 다시 시작했는지, SO의 Prefab 참조가 맞는지 |
| 주머니 모습이 바뀌지 않음 | Icon이 아닌 실제 모델을 바꿨는지, MeshRenderer 모델인지 |
| 초상화가 보이지 않음 | 대문자 인물 ID의 Base Portrait가 연결됐는지 |
| 눈 색이 없거나 밖으로 샘 | Iris Regions의 좌표·범위와 BASE 투명 구멍 |
| 깜빡일 때 뜬 눈이 보임 | Eye Closed가 홍채 구멍까지 완전히 덮는지 |
| 본 재생이 미연결로 표시됨 | Rig Prefab·Animator Controller·SpriteSkin 연결을 리깅 담당자와 확인 |
| UI 클릭이 막힘 | 장식 Image의 Raycast Target, EventSystem, GraphicRaycaster |
| 일반 플레이에도 같은 손님이 나옴 | Test Settings의 Use Fixed Order가 켜져 있는지 |

검사는 **Validate Catalog**로 시작합니다. 오류의 에셋·ID를 확인하고, 아이콘 누락 등 경고와 구분하세요. 해결이 필요하면 **씬 이름, 인물/재료 ID, 색·시드, 재현 동작**을 함께 전달합니다.

## 8. 작업 완료 체크

- [ ] Play를 종료한 뒤 원본 SO·프리팹에 저장했다.
- [ ] ID·판정 속성·Collider·기능 참조를 유지했다.
- [ ] 재료의 선반·손·주머니 모습과 반납 위치를 확인했다.
- [ ] 초상화의 판정 요소·깜빡임·본 움직임·초기화를 확인했다.
- [ ] 공용 재질·색의 다른 사용처와 UI 해상도를 확인했다.
- [ ] 새 파일의 `.meta`를 함께 전달하고 고정 테스트 설정을 껐다.

등록된 인물의 **Active 상태는 아트 검수 완료 표시가 아닙니다.** 실제 초상화 PNG·눈 위치·인물별 본 리그와 시연 빌드는 별도 검수가 필요합니다. 상세 프리팹 구조, 리깅 조건, 구현 시점 검증 기록은 [보충 자료](ARTIST_GUIDE_REFERENCE.md)를 참고하세요.
