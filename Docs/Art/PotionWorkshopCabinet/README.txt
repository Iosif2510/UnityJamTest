포션공방 새 생성본 파츠 패키지 v3

color/ : 빈 서랍장 1개 + 병 8개, 총 9개 투명 PNG
normal/opengl/ : 각 컬러 PNG와 동일 크기 및 투명 영역, +Y
normal/directx/ : 위 맵의 녹색 채널을 반전한 -Y 버전
mask/ : 파츠별 alpha와 전체 빈 홈 마스크
height/ : 병 8개 형상 기반 높이 보조 맵
preview/ : 조립 이미지, 조립 노말, 파츠 목록
source/new-master.png : 이번 요청에 따라 새로 생성한 원본
layout.json : 1729x910 공통 캔버스 기준 각 PNG의 좌상단 배치 좌표
workshop-color-layers.ora / workshop-normal-layers.ora : 분리 레이어 파일(Krita/GIMP 등 OpenRaster 지원 편집기)

모든 병은 새 생성 원본에서 1:1 픽셀로 분리했습니다. 가로세로 변형이나 크기 변경이 없습니다.
병 8개는 각각 현재 빈 홈 안에 들어가도록 분리되었으며, 노말맵도 같은 좌표에 정렬됩니다.
노말맵은 2D 실루엣/추정 높이와 서랍장 평면에 기반한 보조 맵입니다. 3D 메시 베이크나 유리 굴절 맵이 아닙니다.
노말맵은 컬러와 다르게 sRGB를 끄고 linear 텍스처로 사용하세요. 브라우저 기본값은 OpenGL(+Y)입니다.
로컬 미리보기: 이 폴더에서 HTTP 서버를 실행한 뒤 index.html을 여세요.
