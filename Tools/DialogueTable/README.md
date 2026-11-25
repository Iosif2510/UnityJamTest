# 사전 생성 대사 표

손님 대사를 실행 중에 LLM으로 만들지 않고, 미리 만들어 둔 표에서 불러오기 위한 도구다. 손님 22명 × 질병 6개 = 주문 132건이고, 주문 하나에 대사 9줄(상담 1, 받을 때 4, 마신 뒤 4)이 들어간다.

## 순서

1. `python Tools/DialogueTable/export_inputs.py`
   게임 데이터(`Assets/GameData`의 인물 목소리, 손님, 질병, 대체 대사)에서 `inputs.json`을 만든다. 인물·질병·금지어를 고치면 다시 실행한다.
2. `node Tools/DialogueTable/generate.mjs --model <모델> --out <표.json> --all`
   OpenAI 모델로 표를 채운다. `OPENAI_API_KEY` 환경 변수가 필요하다. 일부만 만들 때는 `--all` 대신 `--pairs HUM_M3:DIS_01,BST_M1:DIS_02`처럼 쓴다. 같은 `--out`으로 다시 실행하면 통과한 항목은 두고 빠졌거나 검증에 실패한 항목만 다시 만든다.
3. `node Tools/DialogueTable/validate.mjs <표.json>`
   게임이 생성 대사에 적용하던 것과 같은 규칙으로 검사한다. 오류가 있는 항목은 게임에 넣지 않는다.
4. `node Tools/DialogueTable/compare.mjs <비교.md> "이름=표.json" "이름=표.json"`
   여러 표를 나란히 놓은 검토용 문서를 만든다.

지시문은 실행 중 릴레이가 쓰던 `Tools/OrderDialogueRelay/order-dialogue.ko.txt`를 그대로 쓰고, 요청 형식도 같다.

## 표 형식

```json
{
  "schemaVersion": 1,
  "generator": { "provider": "openai", "model": "...", "promptRevision": "..." },
  "entries": {
    "HUM_M3|DIS_01": {
      "symptom": "잡담 <sym>증상 1</sym> 잡담 <sym>증상 2</sym> 잡담 <sym>증상 3</sym> 잡담",
      "rcv_ok": "...", "rcv_bottle": "...", "rcv_ink": "...", "rcv_both": "...",
      "aft_cured": "...", "aft_cured_ink": "...", "aft_odd": "...", "aft_slime": "..."
    }
  }
}
```

키는 `인물 코드|질병 ID`다. 시간 초과 대사는 표에 없고 기존 대체 대사를 쓴다.

## 검증 규칙

- 9줄이 모두 있고 줄바꿈이 없다.
- 상담 대사에 `<sym>` 구간이 정확히 3개이고 다른 꺾쇠가 없다.
- 상담 대사에 병명과 금지어가 없다. 어느 줄에도 정답 식별자(`DIS_01`, `A1` 등)가 없다.
- 길이: 상담 160~240자(300자 초과는 오류), 받을 때 30~60자, 마신 뒤 40~80자(120자 초과는 오류). 범위를 벗어나면 경고만 낸다.

검증기는 형식만 본다. 말투가 인물에 맞는지, 증상 세 개가 서로 구별되는지는 사람이 읽고 확인한다.

## 샘플

`samples/`에 같은 6건을 두 모델로 만든 표와 비교 문서(`COMPARE.md`)가 있다. `fable.json`은 `generate.mjs`가 아니라 Claude Code 세션에서 같은 지시문과 입력으로 쓴 것이다.
