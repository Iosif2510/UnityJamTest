# 2D 아트와 기획 인수인계 (tuiella)

확인 기준은 `main` 커밋 `94550f6`과 `art/ingredients` 커밋 `fe41374`다. 저장소 파일에서 확인한 것만 사실로 적었다. 제작 과정 가운데 저장소에 없는 부분은 작업 스크립트와 작업 기록을 참고했고, 그 자리에 출처를 밝혔다.

담당 범위는 커밋 작성자로 나눴다.

- 작성자 `tuiella`로 커밋된 것: 재료 모델 18개, 해설자용 종이 8장, 기획 문서(GDD). 이 문서의 본문이다.
- 작성자 `Codex`로 커밋된 2D 아트(손님 초상화, 씬 1~3 UI, 타이틀 화면): 저장소만으로는 담당자를 알 수 없다. 8절에 파일 위치만 정리했다. **담당자 확인 필요.**

## 1. 맡은 범위와 결과물

### 재료 모델 18개 (커밋 `cec0e4b`)

| 경로 | 내용 |
| --- | --- |
| `Assets/Arts/Models/Ingredients/Models/` | `ING_A1.fbx` ~ `ING_F3.fbx` 18개 |
| `Assets/Arts/Models/Ingredients/Textures/` | 모델별 베이스 컬러 `ING_A1_tex.png` 등 18장 |
| `Assets/Arts/Models/Ingredients/Thumbnails/` | Unity용 썸네일 스프라이트 `ING_A1.png` 등 |
| `Assets/Arts/Models/Ingredients/README.md` | 규격, 재료 목록, 알아 둘 점 |
| `Docs/Ingredients/Thumbnails_Light/`, `Thumbnails_Dark/` | 책자용 썸네일 (1024×1024) |
| `Docs/Ingredients/MVP_Data.md` | MVP 데이터 문서 사본 (2026-11-19 내보냄) |
| `Docs/Ingredients/README.md` | 위 README와 같은 내용 |

`Assets/Prefabs/Workshop/IngredientS/`의 `ING_*.prefab`과 `ING_*.mat`은 이 모델을 쓰는 프리팹이다. 누가 만들었는지는 확인 필요.

### 해설자용 종이 (`Docs/PotionMemos/`)

| 파일 | 내용 |
| --- | --- |
| `POT_01.png` ~ `POT_06.png` | 약물공정 안내표 6장. 한 장에 약 하나 |
| `CounterManual.png`, `CounterManual_A4.pdf` | 씬 3에서 쓰는 "건넬 때" 메모. 병과 라벨 잉크 고르는 법 |
| `Cover.png` | 표지이자 인수인계 쪽지. 두 사람의 역할과 순서 4단계 |
| `NarratorSheets_Print_A4_8pages.pdf` | **인쇄용.** 표지, 안내표 6장, "건넬 때" 순서의 8쪽 |
| `NarratorSheets_A4_8pages.pdf` | 여백 처리 없는 8쪽 |
| `Cover_and_CounterManual_Print_A4_2pages.pdf` | 표지와 "건넬 때" 두 장만 담은 인쇄용 |
| `PotionMemos_A4_6pages.pdf` | 안내표 6장 묶음 |
| `prompts/*.txt` | 장마다 이미지 생성에 쓴 지시문 8개 |
| `README.md` | 장별 약과 재료 표, 읽는 법, 잉크 그림 표 |

**브랜치 차이에 주의.** 시연 피드백을 받아 다시 만든 표지(인수인계서)와 "건넬 때" 최종판은 `art/ingredients`의 커밋 `5b5b151`, `fe41374`에만 있고 `main`에는 아직 없다. `main`의 `Docs/PotionMemos/`에는 이전 판과 `NarratorSheets_A4_7pages.pdf`가 남아 있다. 최신 종이는 `art/ingredients`에서 받는다.

### 기획 문서

- `Docs/GDD.md`: 게임 기획서 (커밋 `0424a7d`, `8f87574`, `9364bbf`).

## 2. 소스 코드 구조

담당 범위에 런타임 C# 구현은 해당 없음. 결과물이 연결되는 지점만 적는다.

- 씬 흐름: `ProjectSettings/EditorBuildSettings.asset`에서 `00_Title`, `01_Order`, `02_Workshop`이 활성이고 `SampleScene`은 비활성이다.
- 재료 모델은 `Assets/Scripts/Data/IngredientCatalog.cs`의 `IngredientVariant.prefab`으로 참조된다.
- 해설자용 종이는 게임이 읽지 않는다. 인쇄해서 쓰는 실물이다.

## 3. 데이터 구조

- `Assets/GameData/Ingredients.asset` (`IngredientCatalog`)
  - `kinds[]`: 재료 계열 6개(A~F). `id`, `displayName`, `baseAppearance`, `featureA`, `featureB`, `featureC`, `variants[]`.
  - `variants[]`: 계열마다 3개. `id`(A1~F3), `displayName`, `prefab`, `features`(Color, Pattern, Shape 중 가진 것), `distinctPoint`, `bookletThumbnail`, `usesPlaceholderArt`.
- 종이의 정답(약별 재료, 병과 잉크 규칙)은 게임 데이터와 같아야 한다. 기준 사본은 `Docs/Ingredients/MVP_Data.md`이고, 게임 쪽은 `Assets/GameData/`의 `DiseasesAndRecipes.asset`, `PotionCatalog.asset`, `PrescriptionRules.asset`이다. 두 쪽이 지금 일치하는지는 확인 필요.

## 4. 아트 파이프라인

### 재료 모델

1. Meshy로 형태와 텍스처를 생성했다. 대부분 텍스트 기반이고 소라 3개는 참고 그림 기반이다.
2. Blender 스크립트로 손봤다. 알의 형태, 깃털의 갈라진 끝, 두꺼비뿌리의 단면, 마녀뿌리의 혹, 버섯 갓과 기둥, 노을수정의 끝이 대상이다. 알 3개의 텍스처는 스크립트로 새로 그렸다.
3. 썸네일은 Blender로 렌더링했다.
4. 규격: 높이 약 0.5m, 피벗은 바닥 가운데. 모델당 삼각형 약 2만 개(C1 약 8천, B3 약 1만 4천). 텍스처는 베이스 컬러 한 장이고 PBR 맵은 없다.
5. Unity 임포트 설정 (`.meta`에서 확인)
   - FBX: `globalScale 1`, `useFileScale 1`, `meshCompression 0`, `isReadable 0`, `addColliders 0`.
   - 텍스처: 기본 타입, sRGB, 밉맵 켬, 최대 2048.
   - 썸네일: 스프라이트(Single), 알파 투명, 최대 2048.
- Meshy 프롬프트와 생성 및 Blender 스크립트는 **저장소에 없다.** 담당자 로컬에 있다. 팀 공유 위치는 확인 필요.

### 해설자용 종이

1. 글과 그림을 이미지 생성(gpt-image)으로 한 번에 만들었다. 지시문은 `Docs/PotionMemos/prompts/`에 있다. 한글 문구를 글자 그대로 쓰라고 지시하고, 장마다 눈으로 검수했다.
2. 후처리: 밝기 248 이상을 순백으로 올렸다. 인쇄용은 그림을 92%로 줄여 가운데에 놓고 가장자리 종이 질감을 흰색으로 풀었다.
3. PNG를 A4 PDF로 묶었다. 인쇄는 "실제 크기"로 한다.
- 생성과 후처리 스크립트는 저장소에 없다(담당자 로컬). 작업 스크립트 기준 생성 크기는 1440×2032다.
- 파츠와 레이어 구조: 해당 없음. 장마다 통짜 그림 한 장이다.

## 5. 에디터 설정

- Unity `6000.3.28f1` (`ProjectSettings/ProjectVersion.txt`).
- `Packages/manifest.json`: URP `17.3.0`, Input System `1.20.1`, uGUI `2.0.0`, Unity Pipeline `1.0.0`, spine-csharp `4.3`(Git 주소).
- 빌드 씬은 2절과 같다. 배포 빌드 대상은 확인 필요.
- 담당자가 만든 Unity 에디터 도구는 해당 없음. `Assets/Editor/`에 `ArtPipelineMenu.cs` 등이 있으나 `tuiella` 커밋에는 들어 있지 않다.
- PNG, PDF, FBX는 Git LFS로 올라간다. 받을 때 LFS 원본을 내려받는다.

## 6. 외부 의존성

- 제작에만 쓴 온라인 서비스: Meshy(재료 모델), OpenAI 이미지 생성(종이). 다시 생성하려면 각각 **키 필요.**
- 결과물 자체(FBX, PNG, PDF)는 오프라인으로 쓴다. 재료와 종이는 실행 중에 온라인 API를 부르지 않는다.
- Blender 수정, 후처리, PDF 묶기는 오프라인 작업이다.

## 7. 알려진 문제와 주의점

- **재료 이름 불일치.** `Ingredients.asset`의 A1 `displayName`은 "달빛버섯"인데 README와 종이에는 "별밤버섯"이다. 재료 이름은 종이에만 나오므로 화면에는 영향이 없지만 한쪽으로 맞춰야 한다.
- **뿌리 특징 불일치.** `Ingredients.asset`의 B 계열 `featureB`는 "사람 얼굴 같은 무늬"인데 README는 "단면 나이테"다. 모델은 README 쪽으로 만들었다.
- **README의 낡은 문장.** 재료 README는 `.meta`를 넣지 않았다고 하지만 지금은 `.meta`가 저장소에 있다.
- 멍뿌리(B1)는 껍질이 위는 보라, 아래는 주황이다. 수정(D) 텍스처는 불투명하다. 새벽수정(D1)과 노을수정(D3)은 색 조합이 같아 돌 유무와 끝 모양으로 구분한다.
- **"건넬 때" 메모의 오타 두 곳.** 최종판(`art/ingredients`)에 "옆으로 걸고"(→ 길고), "굴려감"(→ 굴러감)이 남아 있다. 이미지라서 그림 편집으로 고쳐야 한다.
- 종이는 이미지 생성 결과라 다시 뽑으면 글자와 배치가 달라진다. 지시문을 고쳐 다시 뽑을 때는 장마다 글자를 검수한다. 줄 긋기가 생기면 지시문에 금지 문장을 넣는다.
- 잉크는 번호가 아니라 그림(잎, 별, 불, 송곳니, 돌멩이, 빈 동그라미)으로 찾는다. 게임 화면의 잉크병에도 같은 그림이 있어야 한다. 현재 화면이 그런지는 확인 필요.
- 시연 피드백: 제조자가 같은 종류의 재료를 아무거나 집는 일이 있었다. 표지에 "한 선반에 같은 종류 세 개"를 넣어 대응했다. 게임 화면에서도 안내하는지는 확인 필요.
- 에셋을 바꿀 때는 경로와 기존 `.meta`를 그대로 둔다.

## 8. 작성자 `Codex`로 커밋된 2D 아트 (담당자 확인 필요)

`main`에 있는 것:

- 손님 초상화 5명: `Assets/Arts/Portraits/`의 `ELF_F1`, `ELF_M1`, `HUM_F3`, `BST_M2`, `GOL_G1`. 인물마다 `HAIR_BACK`, `IRIS_SOLID_MASK`, `BASE`, `EYE_CLOSED`, `HAIR_FRONT` 5층과 `EXPRESSION_HAPPY`, `EXPRESSION_ANGRY`, Spine JSON이 있다. 전부 1024×1536 RGBA, 중앙 피벗이다. 설명은 `Docs/Portraits/README.md`, 경로 목록은 `Docs/Portraits/manifest.json`.
- 엔딩 슬라임: `Assets/Arts/Portraits/SLIME/`.
- 씬 1 UI: `Assets/Arts/UI/scene1_ui/`, 설명은 `Docs/SCENE1_PANEL_UI.md`.
- 씬 2 UI: `Assets/Arts/UI/scene2_ui/`. 설명 문서는 확인 필요.
- 씬 3 UI: `Assets/Arts/UI/scene3_ui/`, 설명은 `Docs/SCENE3_RESULT_UI.md`.

`main`에 없는 브랜치:

- `art/portrait-engine-export`: 나머지 손님 17명 초상화 (`b1703aa`).
- `art/title-screen-assets`: 타이틀 화면 그림과 시작 버튼 (`277345d`).
- `art/scene3-ui`, `art/scene1-layered-materials`: `main`에 없는 커밋이 남아 있다.

이 브랜치들이 빌드에 들어갔는지, 초상화 제작 도구와 프롬프트가 어디 있는지는 확인 필요.
