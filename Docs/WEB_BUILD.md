# 웹(WebGL) 빌드와 Unity Play 업로드

Unity Play에는 웹 빌드만 올릴 수 있습니다. 이 문서는 받은 프로젝트로 웹 빌드를 만들어 Unity Play에 올리기까지 확인할 것을 정리한 것입니다.

## 웹 빌드에서 달라지는 점 (이미 코드에 반영됨)

- **방 연결**: 웹 빌드에서는 Relay가 자동으로 WebSocket(wss)으로 연결합니다(Multiplayer Services 2.5의 기본값). PC판과 웹판이 같은 방에 들어갈 수 있습니다.
- **직접 연결(IP)**: 브라우저에서는 방을 열 수도, 주소로 들어갈 수도 없어서 웹에서는 숨겼습니다.
- **게임끄기 버튼**: 브라우저는 탭을 닫으면 되므로 웹에서는 숨겼습니다.
- **음성(Vivox)**: 웹에서도 연결은 됩니다. 다만 웹 SDK는 "말하는 중" 표시를 지원하지 않아 그 표시만 꺼집니다.
- **일본어·중국어 글꼴**: 브라우저는 컴퓨터 글꼴을 빌려 쓸 수 없어서, 게임에 쓰인 글자만 잘라 낸 `Assets/Resources/Fonts/WebCJK.ttf`(LXGW WenKai, OFL)를 넣었습니다.
  - TextMeshPro와 메이플스토리 폰트(uGUI)가 이 글꼴을 대체 글꼴로 씁니다.
  - **일본어·중국어 문장을 새로 넣으면** `python Tools/WebFont/subset_web_cjk.py`를 다시 실행해야 합니다. 안 하면 새 글자가 네모로 나옵니다.

## 프로그래머 확인 목록

1. **Unity 버전과 모듈**: Unity 6000.3.28f1에 **Web Build Support** 모듈이 깔려 있어야 합니다(Unity Hub > Installs > 톱니 > Add modules).
2. **Unity 계정 로그인**: 에디터 오른쪽 위 계정으로 로그인합니다. Unity Play 업로드와 Relay·Vivox 모두 이 로그인을 씁니다.
3. **클라우드 프로젝트 연결** (Edit > Project Settings > Services)
   - 지금은 `Jiudev` 프로젝트(조직 Jiu)에 연결돼 있고, 거기에 Relay·Lobby·Vivox가 켜져 있습니다.
   - 그대로 쓰려면 Jiudev 조직에 멤버로 초대받아야 합니다.
   - 팀 조직의 프로젝트로 바꾸려면 그 프로젝트의 대시보드에서 **Relay, Lobby, Vivox**를 켜야 온라인 협동이 됩니다. 바꾸면 `ProjectSettings/ProjectSettings.asset`과 `ProjectSettings/Packages/com.unity.services.*`가 바뀝니다.
4. **회사·제품 이름** (Project Settings > Player): 지금 `DoSiSa` / `NoisyPotionStore`입니다. Unity Play에 보일 이름이 다르면 바꿉니다.
5. **웹용 에셋 최적화(처음 한 번)**: 메뉴 **Potion Shop > Build > Optimize Assets For Web**를 누릅니다.
   - 텍스처·아틀라스에 웹 전용 압축(DXT)을 걸고, 재료 모델에 Mesh LOD를 켭니다. PC판에는 영향이 없습니다.
   - 이미 적용된 것은 건너뛰므로 여러 번 눌러도 됩니다. 저장소에는 적용된 상태로 올라가 있습니다.
6. **웹 설정 적용**: 메뉴 **Potion Shop > Build > Web Settings**를 누릅니다.
   - Brotli 압축, 압축 해제 대체(서버 설정과 상관없이 열림), 데이터 캐시, 백그라운드 실행, 웹 정적 배칭이 켜집니다.
7. **빌드**: 메뉴 **Potion Shop > Build > WebGL Build**를 누르면 웹으로 전환한 뒤 프로젝트 옆 `Build/WebGL`에 빌드합니다.
   - 처음 전환할 때 모든 에셋을 다시 가져오느라 오래 걸립니다.
8. **Unity Play 업로드**
   - File > Build Profiles > Web에서 **Publish to Play**로 올립니다. 이미 올린 게임을 바꿀 때는 **Update existing game**에서 그 게임을 고릅니다(업로드 페이지에 zip을 다시 올리면 새 게임이 생길 수 있음).
   - 버튼이 없으면 Package Manager에서 **WebGL Publisher**(`com.unity.connect.share`)를 설치합니다.
   - 용량 한도는 현재 문서 기준 1 GB이고, 예전 문서에는 압축 파일 500 MB로 나옵니다. 넘으면 텍스처 크기를 줄여야 합니다.
9. **업로드 후 확인**: Unity Play 페이지에서 다음을 확인합니다.
   - 혼자하기 한 판
   - 언어를 일본어나 중국어로 바꿔서 글자가 네모로 나오지 않는지
   - 함께하기 방 만들기와 다른 PC나 브라우저에서 코드로 들어가기
   - 마이크 권한 허용 후 음성
