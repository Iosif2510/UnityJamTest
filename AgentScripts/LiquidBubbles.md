# Liquid_Bubble 적용 및 조정

`SampleScene`의 `Sphere (1)`에 `LiquidBubbleController`가 적용되어 있습니다.
Edit 모드에서도 `Preview In Edit Mode`가 켜져 있으면 미리보기가 재생됩니다.

- 머티리얼 `Bubble Amount`: 생성량, `Bubble Size`: 크기, `Bubble Scale`: 밀도와 크기 기준,
  `Bubble Speed`: 전체 생명주기 속도입니다. Speed가 0이면 거품과 파형이 정지합니다.
- 컴포넌트 `Emission Rate`, `Rise Speed`, `Surface Lifetime`: 생성 빈도, 상승 속도, 수면 대기 시간입니다.
- `Surface Flattening`의 기본값은 **0**입니다. 수면에서 구형을 유지하며 옆으로 확대하지 않습니다.
  필요하면 0.05~0.1 정도의 작은 값으로 약한 찌그러짐을 추가할 수 있습니다.
- `Ripple Lifetime`, `Ripple Speed`, `Ripple Strength`: 파형의 수명, 전파 속도, 노멀 변화입니다.
- `Subsurface Visibility`: 액체 속 거품의 가시성입니다.
  수면 거품의 중심은 셰이더에서 수면에 맞추며, 잠긴 아래쪽 반구에도 이 값이 적용됩니다.
  아래쪽은 액체 색이 더 비치고 반사광이 약해집니다. 위/아래 구분은 기울어진 수면을 따릅니다.
- 컴포넌트 컨텍스트 메뉴 `Restart bubbles`: 지정된 시드로 다시 시작합니다.

거품 생명주기는 `LiquidBubbleSimulation`, Renderer 연결과 미리보기는 `LiquidBubbleController`,
곡면 반사와 파열 파형은 `LiquidBubbleEffects.hlsl`로 분리되어 있습니다.
기존 `LiquidMotion`의 MaterialPropertyBlock을 보존하며 머티리얼 인스턴스를 만들지 않습니다.

현재 방식은 기존 Unlit/Opaque 액체 안에서 시선과 구체의 교차를 계산하는 시각 효과입니다.
실제 투명 굴절이나 유체 시뮬레이션은 아니며, 거품과 파형은 기존 액체 외곽선 안에 그려집니다.
현재 구형 데모에 맞춰 mesh bounds를 타원체로 간주합니다. 임의 모양의 병에는 별도 내부 경계가 필요합니다.
Renderer당 거품 24개, 파형 16개로 제한하며 대상 플랫폼에서의 GPU 성능은 별도 측정이 필요합니다.

`M_Liquid_Bubble`의 수면 HDR 최대값을 4에서 1로 낮춰 반사와 파형 명암을 보이게 했습니다.
기존 색조와 비교용 `M_Liquid` 머티리얼은 유지합니다.

기존 Editor를 대상으로 적용/검증하는 명령:

```powershell
unity command script run --project-path C:/Development/UnityJamTest --caller plugin --skill unity-pipeline -- --file AgentScripts/ApplyLiquidBubbles.cs --entry ApplyLiquidBubbles.Apply
unity command script run --project-path C:/Development/UnityJamTest --caller plugin --skill unity-pipeline -- --file AgentScripts/VerifyLiquidBubbles.cs --entry VerifyLiquidBubbles.Run
```

적용 전 파일은 로컬 `tmp/LiquidBubbleBackup`에 있습니다. `SampleScene.unity` 백업에는 작업 전 사용자의 위치 변경도 포함되어 있습니다.
검증 미리보기는 `tmp/LiquidBubblePreview.gif`입니다.
