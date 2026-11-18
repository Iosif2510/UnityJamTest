# UnityJamTest 작업 지침

## 프로젝트 전반의 구현 원칙

- 컴포넌트 모듈화를 우선시하고, 지나친 스크립트 통합을 지양한다.
- 스크립트 단에서 간단한 애니메이션을 적용할 때는 LitMotion 사용을 우선시한다.
- 비동기 동작을 구현할 때는 UniTask 사용을 우선시한다.
- 이 원칙은 프로젝트 전체의 신규 구현과 기존 코드 수정에 적용한다.

## 열려 있는 Unity Editor 사용

- 씬이나 에셋을 수정하기 전에 `unity status --project-path C:/Development/UnityJamTest --format json`으로 이 프로젝트의 기존 Editor를 확인한다.
- 기본 샌드박스에서 `STATUS_NO_INSTANCES` 또는 연결 실패가 나오면, 새 Editor를 열기 전에 해당 조회와 `unity pipeline list --format json`을 권한을 올린 환경에서 다시 실행한다. Windows 샌드박스의 검색 실패는 Editor가 닫혀 있다는 증거가 아니다.
- 이미 열려 있으면 확인한 프로젝트 경로나 현재 PID로 명령을 지정한다. Pipeline이 아직 준비 중이면 그 인스턴스가 준비될 때까지 기다린다. 연결이 계속 실패하면 원인을 보고하며, 동일 프로젝트를 다시 열거나 별도 배치 Editor를 시작하지 않는다.
- 프로젝트를 실제로 사용 중인 Editor가 없음을 확인한 뒤에만, 작업에 필요하면 Editor를 연다.
