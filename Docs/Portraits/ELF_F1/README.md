# 금발 엘프 초상화 전달용 README

인물 ID는 `ELF_F1`입니다. 작업지시서의 인물 행에 맞춰 여성 엘프, 꽃집 직원, 길고 뾰족한 귀, 송곳니 없음으로 설정했습니다. 리소스는 최신 에디터에서 가져왔으며, 요청받은 홍채 윗경계 보정을 포함합니다.

## 파일 구조

- `Assets/Arts/Portraits/ELF_F1/`에 기본 PNG 4장과 `Iris/` 보조 PNG 13장이 있습니다.
- `Docs/Portraits/ELF_F1/Data/`에 `CustomerArchetype_ELF_F1.json`, `ColorPalette_ELF_F1.json`, `PortraitSettings_ELF_F1.json`이 있습니다.

## 데이터 테이블 연결

1. `CustomerArchetype_ELF_F1.json`의 단일 행을 팀의 `CustomerArchetype`에 옮깁니다. `hairBack`, `base`, `eyeClosed`, `hairFront`의 경로를 각각 임포트한 스프라이트 참조로 연결합니다. `gender`의 `여`와 `earType`의 `길고 뾰족한`은 프로젝트에서 열거형을 사용한다면 해당 값에 매핑합니다.
2. `ColorPalette_ELF_F1.json`의 6개 색을 키로 연결합니다. `hairColors`와 `eyeColors`는 이 키를 참조하며, 초기 헤어색은 `gold`, 초기 눈색은 `blue`입니다.
3. `PortraitSettings_ELF_F1.json`에서 기본 스타일, 깜빡임, 홍채 스타일 및 좌표를 읽습니다. JSON 경로는 모두 UnityJamTest 저장소 루트 기준이고 구분자는 `/`입니다. JSON은 데이터 테이블 이관용 예제입니다. 프로젝트의 로더 또는 임포터 형식에 맞춰 연결합니다.

## 레이어와 애니메이션

4개 기본 레이어와 `Iris/` 바로 아래의 보조 PNG 3장은 모두 투명 RGBA, 1024 × 1536px입니다. 전체 캔버스를 유지하고 피벗은 정중앙 `(0.5, 0.5)`으로 가져옵니다. 크기와 위치를 같게 설정한 뒤 아래에서 위 순서로 합성합니다.

| 순서 | 레이어 | 처리 |
| --- | --- | --- |
| 0 | `HAIR_BACK` | 회색조 RGB에 헤어색을 곱합니다. |
| 1 | `IRIS` | 선택한 눈색으로 만든 홍채를 배치합니다. |
| 2 | `BASE` | 몸, 의상, 얼굴, 귀, 입과 뜬 눈입니다. |
| 3 | `EYE_CLOSED` | 기본은 숨김입니다. 깜빡일 때만 베이스를 덮습니다. |
| 4 | `HAIR_FRONT` | 회색조 RGB에 헤어색을 곱합니다. |

자동 깜빡임은 3~5초 무작위 간격으로 `EYE_CLOSED`를 0.1초 켭니다. 고개를 움직이는 경우 홍채와 감은 눈 레이어도 베이스의 머리 움직임을 따라가게 합니다. 호흡과 머리카락 흔들림 세기는 프로젝트의 애니메이션 값으로 설정합니다.

## 홍채 설정

- **기본(`default`)**은 `POR_ELF_F1_BASE.png`와 `POR_ELF_F1_IRIS_MASK.png`를 사용합니다. 흰색 마스크 RGB에 선택한 눈색을 곱하고 알파를 보존한 뒤 베이스 아래에 둡니다. 베이스에 그려진 동공과 음영이 색 위에 합성됩니다.
- **스타일 5종**은 `POR_ELF_F1_BASE_IRIS_EMPTY.png`와 `POR_ELF_F1_IRIS_VARIANTS_MASK.png`를 한 쌍으로 사용합니다. 눈색을 입힌 마스크에 `backingShade`를 합성한 뒤 스타일별 좌우 텍스처를 올리고 그 결과를 같은 마스크의 알파로 제한합니다. 마지막으로 교체 베이스를 위에 덮으면 윗눈꺼풀과 아래 눈선에 맞춰 홍채가 잘립니다. 두 베이스는 스타일에 따라 교체합니다.
- 디테일 PNG는 각각 256 × 256px입니다. `sourceRectPx`로 가장자리 4px를 제외하고 `drawRectPx`로 크기와 위치를 맞춥니다. 두 사각형의 표기는 `[x, y, width, height]`이고 원점은 왼쪽 위입니다. `L`과 `R`은 화면에서 보는 방향입니다. `slit`의 두 디테일은 각각 사각형 중심에서 `rotationDegClockwise`만큼 회전합니다. 각도가 음수이면 화면에서 반시계 방향입니다.
- 기본 팔레트와 깜빡임은 작업지시서의 규칙이고, 보조 마스크와 스타일 좌표는 현재 에디터의 구현값입니다. 작업지시서에서 홍채 영역 구현 방식은 미확정이며, 이번 전달본은 해당 영역에 PNG 마스크를 사용합니다.

### 디테일 색상 재현

에디터의 홍채 스타일을 재현할 때는 아래 식을 텍스처 픽셀에 적용합니다. RGB와 선택한 눈색 `eye`는 sRGB 0~1, 알파는 원본 그대로 사용합니다. `x`, `y`는 256px 이미지의 픽셀 중심을 `[-0.5, 0.5]` 범위로 정규화한 좌표입니다. `saturate`는 0~1로 제한합니다.

```text
v = dot(rgb, [0.2126, 0.7152, 0.0722])
c = saturate((max(rgb) - min(rgb)) * 255 / 84)
shade = min(1, v / 0.51)^1.08
glint = 0.87 * saturate((v - 0.51) / 0.49)
warmth = max(0, rgb.r - rgb.b)
color = mix(v, eye * shade * (1 - glint) + glint + warmth * [0.13, 0, -0.15], c)
pupil = (0.115, 0.415) for slit, (0.195, 0.205) for other styles
outside = saturate((length([x, y] / pupil) - 0.96) / 0.46)
lift = upperShadeLift * outside * saturate((0.04 - y) / 0.26) * saturate((0.56 - v) / 0.28)
output.rgb = saturate(mix(color, eye * 0.75, lift))
```

합성할 때는 원본 알파와 같은 크기를 유지합니다. 기본 스타일은 위 계산 없이 단색 마스크를 사용합니다.

## 반영 전 확인

- 초기 조합인 금색 머리와 파란 눈으로 앞·뒤 헤어 정렬을 확인합니다.
- 기본과 홍채 스타일 5종을 전환하며 양쪽 눈의 경계와 `slit`의 기울기를 확인합니다.
- 감은 눈을 표시하고 홍채나 흰자가 비치지 않는지 확인합니다.

PNG는 Git LFS로 관리하며 Unity 임포트용 `.meta` 파일을 함께 제공합니다. 데이터 연결 시 프로젝트의 `Customers.asset`과 `PortraitColors.asset`을 사용합니다.

기준 문서는 《손님 초상화 작업지시서 (MVP)》 2~4쪽과 7~9쪽입니다.
