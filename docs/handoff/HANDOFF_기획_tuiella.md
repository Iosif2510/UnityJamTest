# 기획 인수인계 (tuiella)

기획 담당의 인수인계서다. `Docs/GDD.md`의 팀 구성표에 따라 기획은 문서, 데이터 값, 종이 자료, 대사를 맡았고, 아티스트 C를 겸해 재료 3D 모델 18개와 해설자 종이를 만들었다.

확인 기준은 `main` 커밋 `94550f6`과 `art/ingredients` 커밋 `fe41374`다. 저장소 파일에서 확인한 것만 사실로 적었고, 확인하지 못한 것은 "확인 필요"로 표시했다. 저장소에 없는 제작 과정은 작업 스크립트를 참고했고 그 자리에 밝혔다.

## 1. 맡은 범위와 결과물

### 기획 문서

| 파일 | 내용 |
| --- | --- |
| `Docs/GDD.md` | 게임 기획서. 개요, 팀 구성, 씬별 구성 요소와 작업 가이드, 조정 값, 확정 사항과 미확정, MVP 테스트 항목 |
| `Docs/Ingredients/MVP_Data.md` | MVP 데이터 문서 사본(2026-11-19). 재료 계열, 색상, 질병 6개, 밸런스, 재료 18개, 인물 22명, 포션 6개, 인물 목소리, 판정 규칙, 대체 대사 15줄, 필드 정의 |

두 문서가 다르면 MVP 데이터가 기준이다(GDD 공통 규칙). 원본은 저장소 밖의 문서이고 저장소에는 내보낸 사본만 있다. 레퍼런스 이미지는 원본에만 있다.

### 해설자 종이 (`Docs/PotionMemos/`)

| 파일 | 내용 |
| --- | --- |
| `POT_01.png` ~ `POT_06.png` | 약물공정 안내표 6장. 한 장에 약 하나. 손님이 하는 말, 약 이름, 재료 3개의 낙서와 메모 |
| `CounterManual.png`, `CounterManual_A4.pdf` | 씬 3에서 쓰는 "건넬 때" 메모. 병과 라벨 잉크 고르는 법 |
| `Cover.png` | 표지이자 인수인계 쪽지. 두 사람의 역할과 순서 4단계 |
| `NarratorSheets_Print_A4_8pages.pdf` | **인쇄용.** 표지, 안내표 6장, "건넬 때" 순서의 8쪽 |
| `NarratorSheets_A4_8pages.pdf` | 여백 처리 없는 8쪽 |
| `Cover_and_CounterManual_Print_A4_2pages.pdf` | 표지와 "건넬 때" 두 장만 담은 인쇄용 |
| `PotionMemos_A4_6pages.pdf` | 안내표 6장 묶음 |
| `prompts/*.txt` | 장마다 이미지 생성에 쓴 지시문 8개 |
| `README.md` | 장별 약과 재료 표, 읽는 법, 잉크 그림 표 |

**브랜치 차이에 주의.** 시연 피드백을 받아 다시 만든 표지(인수인계서)와 "건넬 때" 최종판은 `art/ingredients`의 커밋 `5b5b151`, `fe41374`에만 있다. `main`에는 이전 판과 `NarratorSheets_A4_7pages.pdf`가 남아 있다. 최신 종이는 `art/ingredients`에서 받는다.

### 재료 모델 18개 (아티스트 C 겸임, 커밋 `cec0e4b`)

| 경로 | 내용 |
| --- | --- |
| `Assets/Arts/Models/Ingredients/Models/` | `ING_A1.fbx` ~ `ING_F3.fbx` |
| `Assets/Arts/Models/Ingredients/Textures/` | 모델별 베이스 컬러 `ING_A1_tex.png` 등 |
| `Assets/Arts/Models/Ingredients/Thumbnails/` | Unity용 썸네일 스프라이트 |
| `Docs/Ingredients/Thumbnails_Light/`, `Thumbnails_Dark/` | 책자용 썸네일 (1024×1024) |
| `Assets/Arts/Models/Ingredients/README.md`, `Docs/Ingredients/README.md` | 규격, 재료 목록, 알아 둘 점 |

### 기획이 값을 정하고 다른 담당이 넣은 것

아래 파일의 커밋 작성자는 프로그래머다. 내용의 출처는 MVP 데이터 문서다.

- `Assets/GameData/`의 데이터 파일 (3절).
- `Tools/OrderDialogueRelay/order-dialogue.ko.txt`: 손님 대사 생성 지시문. 누가 문안을 썼는지는 확인 필요.

## 2. 소스 코드 구조

기획 담당이 쓴 런타임 코드는 해당 없음. 기획서와 실제 코드가 어떻게 대응하는지만 적는다.

- **씬 흐름.** 빌드 설정에서 `00_Title`, `01_Order`, `02_Workshop`이 활성이다. GDD는 Unity 씬을 `Counter`, `Storage` 둘로 적었으나 실제 이름은 다르다.
- **컴포넌트 이름.** GDD의 기술 구조 표(`GameFlowManager`, `Judge`, `CustomerGenerator` 등)는 설계 시점의 이름이다. 실제 스크립트는 `Assets/Scripts/` 아래에 있다.

| GDD의 이름 | 실제 스크립트 |
| --- | --- |
| GameFlowManager, GameSession | `Core/RoundDirector.cs`, `Core/SceneFlow.cs` |
| CustomerGenerator | `Data/OrderGenerator.cs` |
| Judge, ScoreCalculator | `Core/RoundJudge.cs` |
| ResultPopup | `Core/RoundResultView.cs` |
| DialogueProvider | `Core/OpenAiDialogueProvider.cs` |
| DialogueView | `Core/OrderDialogueView.cs`, `Core/DialogueTimeline.cs` |
| PortraitView | `Portrait/PortraitView.cs` |
| Interactor, Pocket, Cauldron | `Workshop/WorkshopInteraction.cs`, `Workshop/PocketModelDisplay.cs`, `Workshop/WorkshopCauldron.cs` |
| BalanceConfig | `Data/RoundTuning.cs` |

이 대응은 파일 이름과 역할로 맞춘 것이다. 스크립트별 세부 동작은 프로그래머 인수인계서를 본다.

## 3. 데이터 구조

기획 값의 기준은 `Docs/Ingredients/MVP_Data.md`다. 표마다 칸의 키와 형식은 그 문서 끝의 "필드 정의"에 있다. 게임은 `Assets/GameData/`의 ScriptableObject를 읽고, 불러오는 곳은 `Assets/Scripts/Data/CatalogLoader.cs`다.

| MVP 데이터의 표 | 게임 데이터 파일 | 확인한 것 |
| --- | --- | --- |
| 재료 계열, 재료 | `Ingredients.asset` | 계열 6개, 계열마다 재료 3개 |
| 질병, 포션 | `DiseasesAndRecipes.asset`, `PotionCatalog.asset` | 파일 있음. 값 대조는 확인 필요 |
| 인물 22명 | `Customers.asset` | 프로필 22개. `spawnPool`은 5명(`HUM_F3`, `ELF_M1`, `ELF_F1`, `BST_M2`, `GOL_G1`) |
| 인물 목소리 | `CharacterVoices.asset` | 파일 있음 |
| 색상 테이블 | `PortraitColors.asset` | 파일 있음 |
| 병 규칙, 잉크 규칙 | `PrescriptionRules.asset` | 파일 있음 |
| 대체 대사 15줄 | `DialogueCatalog.asset` | 항목 15개 |
| 밸런스 설정 | `RoundTuning.asset` | 아래 표 |

### 기획 규칙 요약 (MVP 데이터 문서)

- **재료.** 계열마다 특징 a(색), b(무늬), c(모양)가 있고, 재료 1번은 ab, 2번은 bc, 3번은 ca를 가진다. 구별 포인트는 그 재료에만 빠진 특징이다.
- **포션.** 6개, 질병과 1:1. 재료 3개는 서로 다른 계열이고, 1번, 2번, 3번 재료를 하나씩 넣는다. 18개 재료가 한 번씩 쓰인다.
- **병.** 성별만 본다. 남은 네모, 여는 하트, 없음은 동그라미.
- **잉크.** 위에서부터 먼저 맞는 규칙 하나. 녹색 눈은 숲, 파란 눈과 옆으로 길고 뾰족한 귀는 별빛, 빨강이나 검정 머리(송곳니 있으면 건너뜀)는 불꽃, 송곳니는 짐승, 귀 없음은 돌, 나머지는 공용어.
- **손님 생성.** 정답 잉크를 먼저 고르게 뽑고, 그 정답이 나오는 인물과 색을 고른다. 질병은 따로 무작위다.
- **대사.** 손님 한 명당 한 번 호출해 9줄(증상 1, 받을 때 4, 마신 후 4)을 받는다. 실패하면 대체 대사를 쓴다.

### 조정 값 (`RoundTuning.asset`에서 확인)

| 키 | 값 | MVP 데이터의 초기값 |
| --- | --- | --- |
| `timeLimitSeconds` | 180 | 180초 |
| `pointsPerIngredient` | 20 | 재료 1개당 20점 |
| `bottlePoints`, `inkPoints` | 20, 20 | 20점, 20점 |
| `timeBonusMultiplier` | 0.1 | 10초마다 5점 (**다름**) |
| `passingStampThreshold`, `perfectStampThreshold` | 60, 100 | 60점, 100점 |
| `textSpeed`, `lineLength`, `reactionGap` | 23, 200, 1.5 | 같음 |
| `brewingSeconds`, `pickupDistance` | 4, 2.5 | 문서에 없음 |

## 4. 아트 파이프라인

기획 담당이 직접 만든 아트는 해설자 종이와 재료 모델이다.

### 해설자 종이

1. MVP 데이터에서 내용을 가져온다. 안내표는 질병의 증상, 포션 이름, 재료 3개의 특징이고, "건넬 때"는 병 규칙과 잉크 규칙이다.
2. 글과 그림을 이미지 생성(gpt-image)으로 한 번에 만든다. 지시문은 `Docs/PotionMemos/prompts/`에 있다. 한글 문구를 글자 그대로 쓰라고 지시하고 장마다 눈으로 검수한다.
3. 후처리: 밝기 248 이상을 순백으로 올린다. 인쇄용은 그림을 92%로 줄여 가운데에 놓고 가장자리 종이 질감을 흰색으로 푼다.
4. PNG를 A4 PDF로 묶는다. 인쇄는 "실제 크기"로 한다.
- 파츠와 레이어 구조: 해당 없음. 장마다 통짜 그림이다.
- 생성과 후처리 스크립트는 저장소에 없다(담당자 로컬). 작업 스크립트 기준 생성 크기는 1440×2032다.

### 재료 모델

1. Meshy로 형태와 텍스처를 생성했다. 대부분 텍스트 기반이고 소라 3개는 참고 그림 기반이다.
2. Blender 스크립트로 알의 형태, 깃털의 갈라진 끝, 두꺼비뿌리의 단면, 마녀뿌리의 혹, 버섯 갓과 기둥, 노을수정의 끝을 손봤다. 알 3개의 텍스처는 스크립트로 새로 그렸다. 썸네일은 Blender로 렌더링했다.
3. 규격: 높이 약 0.5m, 피벗은 바닥 가운데, 모델당 삼각형 약 2만 개, 텍스처는 베이스 컬러 한 장.
4. Unity 임포트 설정(`.meta`에서 확인): FBX는 `globalScale 1`, `useFileScale 1`, 압축 없음. 텍스처는 sRGB, 밉맵 켬, 최대 2048. 썸네일은 스프라이트(Single).
- Meshy 프롬프트와 Blender 스크립트는 저장소에 없다(담당자 로컬). 팀 공유 위치는 확인 필요.

초상화, 씬 UI, 환경 모델은 아티스트 A와 B의 범위다. 이 문서에서 다루지 않는다.

## 5. 에디터 설정

- Unity `6000.3.28f1`. `Packages/manifest.json`에 URP `17.3.0`, Input System `1.20.1`, uGUI `2.0.0`, Unity Pipeline `1.0.0`, spine-csharp `4.3`이 있다.
- 빌드 씬은 2절과 같다. 배포 빌드 대상은 확인 필요.
- 기획 담당이 만든 에디터 도구는 해당 없음. GDD가 요청한 Validator에 해당하는 것으로 `Assets/Editor/CatalogValidationMenu.cs`가 있다. 검사 범위는 확인 필요.
- PNG, PDF, FBX는 Git LFS로 올라간다.

## 6. 외부 의존성

- **손님 대사.** 실행 중에 LLM으로 만든다. `Assets/Scripts/Core/OpenAiDialogueProvider.cs`와 `Tools/OrderDialogueRelay/relay.mjs`가 담당한다. 쓰려면 **키 필요.** 호출이 실패하면 `DialogueCatalog.asset`의 대체 대사 15줄이 나간다. 이 부분은 오프라인에서도 돈다.
- **제작에만 쓴 서비스.** Meshy(재료 모델), OpenAI 이미지 생성(종이). 다시 만들려면 각각 **키 필요.**
- **오프라인 전용.** 해설자 종이는 인쇄물이다. 재료 모델과 데이터 파일은 실행 중에 온라인을 부르지 않는다.

## 7. 알려진 문제와 주의점

### 문서와 게임이 다른 곳

- **시간 보너스.** MVP 데이터는 10초마다 5점인데 `RoundTuning.asset`은 `timeBonusMultiplier: 0.1`이다. 어느 쪽이 의도인지 정하고 문서를 맞춘다.
- **재료 이름.** `Ingredients.asset`의 A1은 "달빛버섯"인데 MVP 데이터와 종이는 "별밤버섯"이다.
- **뿌리 특징.** `Ingredients.asset`의 B 계열 `featureB`는 "사람 얼굴 같은 무늬"인데 MVP 데이터는 "잘린 단면에 나이테"다. 모델과 종이는 MVP 데이터 쪽이다.
- **등장 인물 수.** GDD의 확정 사항은 "MVP는 인물 5명"이고 `main`의 `spawnPool`도 5명이다. 나머지 17명 초상화는 `art/portrait-engine-export` 브랜치에 있다. 시연 빌드에 22명이 들어갔는지는 확인 필요.
- **GDD의 낡은 내용.** 해설자 종이를 "메모 7장", 인쇄 파일을 `NarratorSheets_A4_7pages.pdf`로 적고 있다. 지금은 표지를 더한 8장이고 인쇄 파일은 `NarratorSheets_Print_A4_8pages.pdf`다. 기술 구조의 컴포넌트 이름과 씬 이름도 실제와 다르다(2절).
- **MVP 데이터 6단계.** 질병 도감, 제조법, 재료 도감을 따로 만드는 것으로 적혀 있으나, 실제로는 안내표 6장과 "건넬 때" 1장으로 합쳤다(GDD 쪽이 맞다).

### 종이

- 최신 종이가 `main`에 없다(1절).
- "건넬 때" 최종판에 오타 두 곳이 있다. "옆으로 걸고"(→ 길고), "굴려감"(→ 굴러감). 이미지라서 그림 편집으로 고친다.
- 데이터를 바꾸면 종이도 다시 만들어야 한다. 이미지 생성 결과라 다시 뽑으면 글자와 배치가 달라지므로 장마다 검수한다.
- 잉크는 번호가 아니라 그림(잎, 별, 불, 송곳니, 돌멩이, 빈 동그라미)으로 찾는다. 게임 화면의 잉크병에 같은 그림이 있는지는 확인 필요.

### 시연에서 나온 피드백 (담당자 기록, 한 판 2분 40초)

- 해설자 종이의 정보가 많아 무엇을 봐야 할지 몰랐다. 표지를 인수인계서로 다시 써서 역할과 순서를 먼저 읽게 했다.
- 어느 대사가 중요한지 몰라 잡담까지 전달하려 했다. 표지에 "굵은 글씨 세 군데만"을 넣었다.
- 제조자가 같은 종류의 재료를 아무거나 집었다. 표지에 "한 선반에 같은 종류 세 개"를 넣었다. 게임 화면에서도 안내하는지는 확인 필요.
- 반복 플레이를 위한 무작위 요소 확장은 다음 단계 과제로 남겼다.

### 남은 미확정 (GDD)

- 잡담에도 증상 이펙트를 섞을지.
- 메모의 재료당 메모 수(지금 6~7개).
- 잉크별 메모 방식이 20초 안에 풀리는지.
- 밸런스 최종값(MVP 데이터 7단계).
