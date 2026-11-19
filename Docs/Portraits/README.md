# 엔진용 홍채 베이크

인물마다 게임용 파츠 5장과 개발자 확인용 합성 `PREVIEW` 1장이 있습니다. `manifest.json`의 `base`에 인물별 동공 형태가 고정되어 있습니다. 게임에서는 베이스 뒤의 `IRIS_SOLID_MASK`에 원하는 눈색을 적용합니다. `PREVIEW`는 기본 색과 뜬 눈을 합성한 참고 이미지입니다.

| 아래부터 | 파일 | 적용 |
| --- | --- | --- |
| 1 | `HAIR_BACK` | 뒷머리. 이끼 골렘의 파일은 공통 5슬롯을 맞추기 위한 투명 PNG |
| 2 | `IRIS_SOLID_MASK` | 마스크의 흰색에 원하는 눈색을 곱함 |
| 3 | `BASE` | 동공, 홍채 결, 눈꺼풀 그림자와 마스크 안 하이라이트를 병합한 베이스 |
| 4 | `EYE_CLOSED` | 눈을 감을 때만 켜서 홍채와 하이라이트를 덮음 |
| 5 | `HAIR_FRONT` | 앞머리 또는 이끼 |

은발 엘프·금발 엘프·빨간 머리 인간·이끼 골렘은 원형, 여우 수인은 세로 동공입니다. 이끼 골렘에는 무광 표현을 적용했습니다.

레이어는 전부 1024×1536 RGBA이며, 중앙 피벗과 동일한 위치로 쌓습니다. 머리가 움직이면 홍채 단색, 베이스, 감은 눈이 함께 움직이게 합니다. 눈색이 바뀌어도 베이스에 구운 동공과 음영이 유지됩니다.

게임 리소스는 `Assets/Arts/Portraits/`에 있고, 합성 참고 이미지는 `Docs/Portraits/Previews/`에 있습니다. `manifest.json`의 경로는 저장소 루트 기준입니다. Unity Catalog에 베이스와 파츠를 연결하고 기존 단색 홍채 오브젝트에 눈색을 적용합니다.

## 표정 파츠

손님마다 기쁨과 분노 스프라이트를 한 장씩 제공합니다. 기본 표정은 다섯 층을 사용합니다. 기쁨 또는 분노를 표시할 때는 해당 스프라이트 한 장을 같은 중앙 피벗으로 얹습니다.

`manifest.json`의 `expressions`에 스프라이트 경로와 `drawAfter`가 있습니다. 일반 표정은 베이스 위와 감은 눈 아래에 놓습니다. 여우 수인의 분노는 감은 눈 위와 앞머리 아래에 놓습니다. 토비의 분노는 앞머리 위에 놓아 왼쪽 앞머리 마스크를 적용합니다. 이 순서는 토비 프로필에 연결되어 있습니다.

Unity에서 사용할 때는 표정 전환 코드 또는 데이터의 Sprite 필드에 연결합니다.

## 추가 손님 17명

Unity의 `Assets/GameData/Customers.asset`에 베이스, 홍채 단색 마스크, 감은 눈, 머리 파츠, 기쁨과 분노 참조를 연결했습니다. Unity에서 `Assets/Scenes/Tests/PortraitTest.unity`를 열고 캐릭터 ID를 선택해 기본 초상화와 색을 확인할 수 있습니다. 게임에 등장시킬 손님은 `Customers`의 `Spawn Pool`에 해당 ID를 추가합니다.

| 종족 | 손님 및 ID |
| --- | --- |
| 인간 | 클라라 `HUM_F1`, 몰리 `HUM_F2`, 토비 `HUM_M1`, 한스 `HUM_M2`, 브루노 `HUM_M3` |
| 엘프 | 로제타 `ELF_F2`, 하일 `ELF_F3`, 탈리오 `ELF_M2`, 베른 `ELF_M3` |
| 수인 | 포피 `BST_F1`, 마들렌 `BST_F2`, 루파 `BST_F3`, 펠릭스 `BST_M1`, 우르스 `BST_M3` |
| 골렘 | 모스 `GOL_G2`, 쿼츠 `GOL_G3`, 볼더 `GOL_G4` |

앞머리나 뒷머리가 없는 캐릭터의 투명 PNG는 공통 슬롯 규격에 맞춘 자리 표시자입니다. `manifest.json`의 `hairFrontIsPlaceholder` 또는 `hairBackIsPlaceholder`로 확인할 수 있고 Unity 프로필에서는 해당 슬롯을 비워 둡니다. 각 `PREVIEW`는 기본 색상과 뜬 눈을 합성한 확인용 이미지입니다.

## 공용 엔딩 슬라임

합성본은 `manifest.json`의 `endingSlime`에 있습니다. 깜빡임을 적용할 때는 `endingSlimeLayers.base` 위에 `eyeOpen` 또는 `eyeClosed` 한 장을 표시합니다. 세 파츠는 1024×1536 투명 PNG이며 중앙 피벗을 공유합니다.
## Unity 연결

ELF_M1, HUM_F3, BST_M2, GOL_G1의 `Customers` 항목에 베이스, 감은 눈, 머리 레이어, **Baked Iris**, **Spine Rig**가 연결되어 있습니다. `PortraitView`는 홍채 스프라이트에 선택된 눈색을 적용하고, 인물 폴더의 Spine JSON에서 `idle`을 반복 재생합니다. 머리카락은 뿌리에서 고개를 따라가고 끝으로 갈수록 움직입니다. GOL_G1의 이끼는 고정 장식 색으로 처리합니다.

| PortraitTest 인물 | 헤어 | 눈 | 대기 루프 |
| --- | --- | --- | ---: |
| ELF_M1 | Silver | Purple | 4.3초 |
| HUM_F3 | Red | Green | 4.0초 |
| BST_M2 | Red | Yellow | 3.7초 |
| GOL_G1 | None | Orange | 5.2초 |

