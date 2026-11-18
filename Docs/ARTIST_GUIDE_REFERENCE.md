# 아티스트 가이드 보충 자료

일반 작업 순서는 [아티스트 가이드](ARTIST_GUIDE.md)를 따른다. 이 문서는 구조 확인·좌표 입력·리깅 담당자를 위한 상세 자료다.

## 프리팹 구조와 유지할 참조

이번 작업 시작 시 카운터의 Canvas는 씬 오브젝트였고, 창고는 선반 6개와 솥이 각각 공용 프리팹의 인스턴스였다. 재료는 선반의 Stock 아래 Slot_01~03에 개별 프리팹으로 배치되어 있었다. 이 구성을 유지하면서 다음 편집 경계를 마련했다.

```text
02_Workshop / Stations
├─ Cauldron                           공용 Cauldron.prefab
│  ├─ GrayboxCauldron                  솥 외형 (기존 충돌도 포함)
│  └─ PotionSurface                   액체 표시, WorkshopCauldron이 제어
└─ IngredientShelves
   └─ IngredientShelf_01~06            공용 IngredientShelf.prefab
      ├─ GrayboxRack                  선반 외형·기존 충돌
      └─ Stock
         └─ Slot_01~03                IngredientSlot: 카탈로그 + 바리에이션 ID
            └─ ingredient_*           편집 상태의 배치 미리보기

재료 게임용 프리팹
ingredient_*                          WarehouseIngredient: 고정 ID
├─ VisualRoot                         모델·재질·시각 변환 편집
└─ InteractionColliders              선택 판정, VisualRoot와 별도 관리
```

창고 선반 위치·회전과 각 슬롯의 현재 재료 ID는 씬에 보존한다. Play를 시작하면 IngredientSlot이 편집용 배치 모델을 숨기고, Ingredients.asset의 같은 ID에 연결된 프리팹을 생성한다. **SO의 Prefab 교체는 다음 Play부터 반영된다.** 편집 상태의 진열 그림은 새 프리팹으로 즉시 갱신되는 기능이 아니다.

선반/솥 공용 프리팹의 아트는 Prefab Mode에서 수정한다. 선반 씬 인스턴스의 Stock·슬롯 ID 오버라이드는 선반별 내용이므로 `Apply All`로 공용 선반에 적용하지 않는다. `WorkshopShelf.stockRoot`, `WorkshopCauldron.tuning/potionSurface`, 상호작용 Collider와 Interactable 레이어는 기능 참조다. 외형 변경으로 삭제하지 않는다.

## 홍채 영역 좌표 계산

Iris Regions는 좌하단 원점의 0~1 사각형이다. 원본 픽셀 좌표가 `(x, y, width, height)`라면 각각 `x/1024`, `y/1536`, `width/1024`, `height/1536`으로 입력한다. 위에서 아래로 잰 y 좌표라면 `(1536-y-height)/1536`으로 변환한다. 초기 눈 위치는 자동 추측하지 않았으므로 실제 그림에 맞게 지정한다.

## 본 리그 연결

`com.unity.2d.animation` 13.0.6을 추가했다. Rig Prefab의 루트에 PortraitRigDefinition을 두고 Animator, BASE/감은 눈/앞·뒷머리 SpriteSkin, Head Bone을 연결한다. 사용하지 않는 헤어 Skin은 비운다. 각 SpriteSkin은 대응 SO의 Sprite와 **동일한 Sprite 에셋**을 사용하고 원본 캔버스·중앙 피벗·PPU를 공유한다. 각 SpriteRenderer의 기준 로컬 위치 0·회전 0·scale 1을 유지하며 움직임은 본으로 만든다. 감은 눈 영역은 BASE 눈 주변과 같은 본 가중치를 사용한다.

PortraitSkinnedImage는 SpriteSkin의 CPU 변형 정점을 기본 uGUI 메시로 전달한다. 별도 색 교체 셰이더 없이 Image.color와 UI 마스킹 경로를 유지하며, 원본 SpriteRenderer는 화면에 중복 렌더링하지 않는다. 홍채 사각형은 Head Bone의 변환을 따라간다. 눈 주변은 Head에 동일하게 귀속시키고 국소적으로 다른 가중치를 쓰면 홍채·감은 눈이 어긋날 수 있다. Atlas 패킹과 임의의 원본 리그 Transform 보정은 최초 검수 범위가 아니므로 개별 원본 Sprite부터 확인한다.

리그가 없으면 정적·색·깜빡임 테스트는 가능하지만 본 재생은 미연결로 표시된다. 현재 인물의 실제 아트·리그는 아직 납품되지 않았으므로 리깅 제작 및 인물별 시각 검수는 별도로 진행해야 한다.

## 구현 시점의 확인 결과

다음은 2026-11-18 구현 시점의 검증 기록이며, 이후 바뀐 아트의 검수를 대신하지 않는다.

- Unity Editor 6000.3.28f1의 실제 컴파일을 통과했다.
- 카탈로그에서 고정 인물 22명, 152개 외형 조합의 시드 재현성과 잉크 6종 도달을 확인했다.
- 재료 프리팹 18개의 ID·VisualRoot·독립 Collider, 진열 슬롯 18개, 기존 선반 6개·솥 1개 보존, 테스트 씬의 빌드 목록 제외를 확인했다.
- 창고 Play에서 실제 슬롯 생성, Hold/ReturnToShelf 호출 시 충돌 해제·원위치 복원, 주머니 모델 RenderTexture 생성을 확인했다. 마우스/키보드 전체 수동 조작과 시연 빌드는 별도 확인 대상이다.
- PortraitTest Play의 임시 진단 SpriteSkin에서 프레임 진행, CPU 변형 데이터, 본 이동에 따른 UI 메시·홍채의 약 18px 동일 이동, 감은 눈 표시, LitMotion 틴트, 슬라임 교체와 정상 합성 초기화를 확인했다. 진단 데이터는 런타임 복제본만 사용했으며 저작 인물 SO에 저장하지 않았다.
- 실제 초상화 PNG·인물별 눈 위치·애니메이션 Controller와 본 리그는 별도 아트 작업이 필요하다. 실제 인물의 경계·가독성·본 가중치·배경 위 합격, 모든 UI 해상도·입력 상태의 시각 검수와 배포 빌드는 이번 자동 검증으로 대신하지 않았다.
