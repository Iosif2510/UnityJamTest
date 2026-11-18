# ELF_F1 초상화 연결 기록

## 실제 사용 리소스

| 필드 | 연결 |
| --- | --- |
| Base Portrait | `Iris/POR_ELF_F1_BASE_IRIS_EMPTY.png` |
| Eye Closed | `POR_ELF_F1_EYE_CLOSED.png` |
| Hair Front / Back | 해당 `HAIR_FRONT` / `HAIR_BACK` PNG |
| Iris Mask | `Iris/POR_ELF_F1_IRIS_MASK.png` |
| Iris Variants Mask | `Iris/POR_ELF_F1_IRIS_VARIANTS_MASK.png` |
| Iris Detail Style | Clear (기본) |
| Iris Details | CLEAR / GLOW / OBLIQUE / SLIT / SOFT 각각 Left·Right 연결 |

원본 `POR_ELF_F1_BASE.png`는 이미 홍채가 그려진 참고용 이미지다. 색을 바꿔 표시할 때는 홍채를 비운 베이스를 사용한다. 성별·직업·귀·송곳니와 허용 헤어/눈 색은 기존 ELF_F1 값을 유지한다.

## 눈 영역과 레이어

마스크 알파에서 측정한 원본 눈 영역(이미지 좌상단 기준)은 화면 왼쪽 `(434, 467, 40, 32)`, 오른쪽 `(552, 424, 45, 35)`이다. 좌/우는 인물의 해부학적 방향 대신 **화면 기준**으로 연결했다.

디테일 원형이 눈 구멍 전체를 덮도록 표시 사각형은 중심을 유지하면서 1.5배 확장하고, 좌하단 기준 0~1 좌표로 저장했다. 실제 보이는 경계는 마스크가 결정한다. 따라서 Iris Regions는 이 인물에서 최종 눈 모양이 아닌 **마스크 뒤 디테일의 배치 범위**다.

```text
HairBack
IrisMask                 전체 캔버스, Mask / Show Mask Graphic 꺼짐
├─ IrisLeft              위치 지정된 디테일
└─ IrisRight             위치 지정된 디테일
Base                     홍채 구멍이 있는 베이스
EyeClosed
HairFront
Slime
```

홍채 디테일에는 원본 Sprite를 연결한다. `PortraitIrisDetails`는 팔레트 눈 색의 HSV 색상각과 각 Iris Detail의 **Source Hue Degrees** 차이를 `UI/Hue Shift` 셰이더에 전달한다. CLEAR·OBLIQUE·SOFT의 기준은 175°, GLOW·SLIT의 기준은 170°다. 색상각만 회전하므로 원본의 채도·명도·투명도가 유지된다. 엔딩 틴트는 회전 이후에 적용된다. 디테일 텍스처의 Read/Write는 꺼져 있다.

마스크·디테일이 없는 기존 인물은 원래 단색 사각형 방식을 유지한다.

## Spine 대기 루프

`Customers`의 ELF_F1 **Spine Rig**에 `Assets/Arts/Portraits/ELF_F1/ELF_F1.spine.json`을 연결했다. 공유 `PortraitView`가 ELF_F1을 표시하면 설치된 `spine-csharp`로 4초 `idle`을 반복 재생한다. Spine의 가중치 메시로 베이스를 변형하고, 본의 움직임을 머리카락·감은 눈·홍채 마스크에 적용한다. 앞머리와 뒷머리는 고개 본을 기준으로 각각 약 3.2°, 4.4°까지 추가로 회전한다. 기존 초상화 스프라이트와 팔레트가 렌더링에 사용된다. 단독 렌더링용 아틀라스와 텍스처는 `Docs/Portraits/ELF_F1/Spine/`에 있다.

## 확인 방법

1. `Potion Shop > Art > Open Portrait Test`를 연다. 씬 기본 입력을 ELF_F1 / Gold / Blue로 지정했다.
2. Play 후 합성·깜빡임·엔딩 틴트를 확인한다.
3. 스타일을 바꾸려면 Play를 종료하고 Customers의 ELF_F1에서 Iris Detail Style을 변경·저장한다.
4. **Stop Rig**와 **Play Rig**로 대기 루프를 일시 정지하거나 재개한다. **Auto Blink**는 Spine 타임라인의 깜빡임으로 돌아간다.
