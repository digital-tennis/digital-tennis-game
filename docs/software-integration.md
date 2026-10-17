# 장비를 유예한 소프트웨어 통합

2026-10-18. 기준 main 은 PR22 의 555be11 이다. 이번 변경은 검토용 feature 이며 자동 병합하지 않는다. NU40-DK + **별도로 부착하는 외장 IMU**, 원안 12 일·3 명·36 인일은 유지한다.

## 제품 연결

`외장 IMU → 펌웨어 드라이버/보정/모션 → 20 바이트 코덱 → BLE Peripheral → PC 네이티브 helper → BLE SDK → NativeControllerBackend → InputDeviceManager → PlayerInputRouter → PlayerInputAdapter → SubmitInput` 순서다. 기기는 Near/Far 나 온라인 권한을 결정하지 않는다.

- protocol PR1: C99/C# 공통 코덱. v1 의 FH/BH/서브를 보존하고 v2 에스매시/발리/슬라이스/로브/스킬을 추가한다.
- firmware PR1: [nu40dk-kit](https://github.com/chcbaram/nu40dk-kit)의앱/드라이버/보드 분리와 MIT 보드 정의를참고한다. 참고 Xbox 는 Central, 라켓은 Peripheral 이다. 센서 모델·핀을 추측하지 않고`external-imu`overlay 로 분리한다. 센서 부재를 정상 입력으로 대체하지 않는다.
- ble-unity PR1: Mac CoreBluetooth/Windows GATT 와 Unity 비의존 SDK. 4 초 검색, 현재 발견기기 연결, OS 권한/암호화, 보정 ACK·입력 중복/순서/유효시간 검사를 담당한다.
- game: 연결→정지 보정→서브/스윙/스킬 테스트→저장. 재보정·끊김은 테스트를 무효화하고 저장 전에는 입력 소스를 바꾸지 않는다. 선택한 센서가 끊기면 오프라인 경기는 일시정지하며 키보드로 몰래 전환하지 않는다. 메뉴 키보드는 계속 사용할 수 있다.

`Packages/manifest.json`은 protocol/SDK/AI 를 40 자리 Git SHA 로 고정한다. protocol→firmware/SDK→game 순으로 검토하고, **스쿼시 병합한 SHA 로 종속 PR 을 갱신한 뒤** 병합한다. feature SHA 를 최종 main SHA 로 오인하지 않는다.

## 직접 연결과 릴레이

WebSocket 은 방·역할·토큰·시그널링·연결 협의에 유지한다. 실제 양측 DataChannel 이 열렸다는 동의를 받아야 P2P 로 시작한다. 8 초 내 실패하면릴레이, 경기 중 실제 채널 실패도 새 epoch 의릴레이로 합의한다. 같은 방에서 실패한 직접 연결을 계속 재시도하지 않는다. 로컬 메뉴는 상대 경기를 정지시키지 않는다.

호스트만 물리/규칙/점수/CSV/스킬을 계산한다. 게스트는 동일한 ID·좌표·타격·스냅샷을 표시하고 자기 선수 아래 시점을 사용한다. 오래된 epoch/순번/입력은 버린다. 최종 승패는검증된 MatchEnd 스냅샷에서 결정한다.

기본`StreamingAssets/Online/transport.json`의 STUN 설정은 외부 CSV 와 별도다. 보통실행에서는 persistentDataPath 의같은파일로덮어쓸수있으며 검사/배치실행은기본파일만쓴다. TURN 계정/비밀정보는포함하지않는다. 직접연결이불가능한네트워크에는 WebSocket 릴레이가필요하다. 공용 WSS·도메인은이번에만들었다고주장하지않는다.

## 재현

Unity6000.3.26f1, Python3, Mac 용 XcodeCommandLineTools, Windowshelper 교차빌드용.NET10SDK 가필요하다. Editor 와같은프로젝트의배치작업은동시에실행하지않는다.

```
python3 tools/check-source.py
bash tools/unity.sh check
bash tools/unity.sh test-edit
bash tools/unity.sh test-play
bash tools/unity.sh build-verification-mac
python3 tools/verify-packet-controller.py --help
node tools/verify-online.mjs --server-repo /absolute/server --player '/absolute/DigitalTennis.app/Contents/MacOS/Digital Tennis' --menus --offscreen --transport p2p --out .work/online-direct
node tools/verify-online.mjs --server-repo /absolute/server --player '/absolute/DigitalTennis.app/Contents/MacOS/Digital Tennis' --menus --offscreen --exercise-peer-failure --exercise-reconnect --exercise-characters --exercise-balance --exercise-officials --exercise-effects --exercise-rematch --exercise-misses --out .work/online-fallback
```

서버에서 먼저`npm ci`를 실행한다. 위 검증은 같은 Mac 의 실제 두 Unity 프로세스를 시작하고 게임카메라만 PNG 로저장한다. 검증입력은일반 adapter 경유이며 물리·점수를 직접바꾸지않는다. 메뉴 callback 과 실제마우스 조작의 성공을구분한다.

패킷 검증은**MOCK 전파**를명시한전송포트와실제코덱/SDK/UI/게임입력을사용한다. 실제 BLE 페어링과센서인식률의증거가아니다. 명령행검증은명시적인개발옵트인으로일반플레이에개입하지않는다.

## 배포와 고지

빌드후처리가현재고정 SDK 에서 Mac universal helper 와 Windows self-containedx64helper 를생성한다. Macpurpose 문자열·실행권한·전체앱서명을갱신하며 Windows 패키지에는필요한.NET 런타임을포함한다. SDK/native 출처와.NET 라이선스/NOTICE 를함께복사한다. 바이너리는 Git 에커밋하지않는다.

Unity WebRTC3.0.0 은공식패키지를사용하며패키지의 LICENSE/ThirdPartyNotices 를배포에보존한다. 기존 Quaternius/KenneyCC0, flag-iconsMIT, NaturalEarthPublicDomain, 폰트·Flite 고지는계속유지한다. 팀자체코드의전체공개라이선스선택은별도결정이다.

```
bash tools/unity.sh release-mac
bash tools/unity.sh release-windows
python3 tools/package-release.py --platform mac --out /absolute/delivery
python3 tools/package-release.py --platform windows --out /absolute/delivery
python3 tools/prepare-audio-review.py --out /absolute/audio-review
```

배포는 clean 커밋과 Development/AllowDebugging=false 를검사하고 ZIP·SHA256·실제소스커밋을기록한다. Mac 은로컬 adhoc 서명으로 DeveloperID 공증배포가아니다. Windows 는교차빌드이며실물실행과구분한다.현재 CSV 의 160 개 cue 청음은자동재생하지않고곡 A/B 는사용자결정미정이다. 경기중 BGM0/득점함성/다음서브정지정책을유지한다.

## 실물/계정 때문에 남겨 둔 항목

외장 IMU 모델/배선/장착축확정, NCS/ZephyrMCU 빌드·flash, 실제 OS 동의·전파·센서제스처정확도, Windows 실기, 서로다른두 PC/WAN, 공용호스팅/GitBook 게시, 사람의음악/음성/밸런스채택은본소프트웨어검사로완료표시하지않는다. 최종 실행 수치·소스 커밋·실행 자료는 이 게임 PR의 검증 항목과 동반 문서 PR에 기록한다.

## 최종 소프트웨어 검증

2026-10-18에 Unity Edit189/189·Play43/43을 통과했다. 실제 두 Mac 게임 프로세스의 P2P 전체86포인트에서 2–0세트(7–6/6–0), 서버 게임 중계0을 확인했다. 별도 경기에서는 실제 채널을 닫아 p2p:1→relay:2로 복구한 뒤 WebSocket 재접속으로 relay:3을 합의하고, 같은 전체 경기와 양측 재경기의 첫 포인트[1,0]까지 일치했다. 종료 후까지 플레이어 로그에 예외가 없는지 검사했다.

명시적인 MOCK 전파와 실제 코덱/SDK/UI/adapter의14장 검증에서 보정·테스트·저장, 실제 AI 랠리/득점/다음 서브, 끊김 후 일시정지를 통과했다. 실제 BLE/OS 페어링의 증거는 아니다.15명/15코트·6종 타격74접촉·선수×난이도45개 실제 다음 서브,12150사례/48600포인트의 고정 조건 밸런스 실험과 배포 Mac의 인트로·경기 중 BGM0/득점 함성 검사를 통과했다. CSV 밸런스 수치는 이번에 임의로 바꾸지 않았다.

펌웨어 PR1은 설치된 NCS3.4.1/Zephyr4.4.2에서 기본 구성과 합성 외장 센서 드라이버 구성을 모두 교차 컴파일했다. 실제 부팅/flash/전파/센서 정확도는 별도다. Mac 일반 창 실행은 확인했으나 UI 도구가 창 프레임을 읽지 못해 실제 키보드·마우스 조작은 미검증이다. 메뉴callback·일반adapter 자동 검사와 물리 입력을 혼동하지 않는다.
