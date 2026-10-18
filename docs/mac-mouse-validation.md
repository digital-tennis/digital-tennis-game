# Mac 일반 창의 마우스 검증

## 검증 경계

키보드로 이동하거나 `Button.onClick`을 직접 호출한 결과를 실제 마우스 클릭의 성공으로 기록하지 않는다. OS 입력, Unity의 포인터 좌표와 포커스, 누름·놓음, UI 판정과 화면 전환을 함께 확인한다. 실제 BLE/외장 IMU 검증과는 별도다.

## GPU 회귀 검사

```sh
bash tools/unity.sh test-mouse
```

이 검사는 실제로 렌더링한 Canvas와 `StandaloneInputModule`의 마우스 처리 경로를 사용한다. 테스트용 BaseInput에서 버튼 누름과 놓음을 서로 다른 프레임에 전달한다. 시작 메뉴, 일본 선수 선택과 확정, 상대 선택, 설정의 키보드/무선 상세, 입력 테스트·취소·저장 후 부모 화면 복귀를 검사한다. 마우스를 누른 상태에서 화면이 전환되거나 한 번의 클릭이 다음 화면까지 연속 확정하면 실패한다. 게임의 `View.Invoke`나 버튼 콜백은 호출하지 않는다.

테스트에서만 입력/포커스를 공급하므로 이 검사의 통과를 실제 OS 클릭의 통과로 바꾸지 않는다. Null graphics의 일반 PlayMode 실행에서는 이 사례를 건너뛰며, 위 GPU 명령으로 반드시 별도 실행한다.

## 일반 Mac 창에서 관찰

```sh
bash tools/unity.sh build-verification-mac
open -n -a "$PWD/Builds/verification/mac/DigitalTennis.app" --args \
  -screen-fullscreen 0 -screen-width 1280 -screen-height 800 \
  -dt-input-observe "$PWD/.work/native-mouse" \
  -logFile "$PWD/.work/native-mouse.log"
```

`-dt-input-observe`는 일반 창에서 수신한 입력만 관찰한다. 게임 버튼을 누르거나 입력 소스를 교체하거나 점수를 바꾸거나 앱을 자동 종료하지 않는다. 관찰 전용 빌드는 별도 앱 식별자와 컨트롤러 설정 경로를 사용한다.

`native-pointer.ndjson`에는 각 MouseDown/MouseUp의 단계·프레임·버튼·포커스, 창 내부 여부, 원시 이벤트 좌표와 폴링 좌표, UI 판정의 첫 대상을 기록한다. 입력 필드의 내용이나 네트워크 토큰은 수집하지 않는다. [Unity Event.mousePosition](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Event-mousePosition.html)의 좌상단 원점을 UI 판정의 좌하단 원점으로 변환해 비교한다. 선수 선택과 기기 설정의 변화도 관찰 프레임을 남긴다.

실제 클릭 검수 경로:

1. 시작하기 → 1인 플레이 → 일본 선택 → 선수 확정. 한 번의 클릭이 다음 화면까지 확정하지 않는지 확인한다.
2. 뒤로 → 설정 → 입력 컨트롤러 → 키보드 상세. 입력 테스트와 취소·저장 후 복귀를 확인한다.
3. 무선 상세 → 기기 찾기. 장비가 없으면 미연결·테스트/저장 제한을 확인하고 취소한다. 기기 연결 성공으로 기록하지 않는다.

## 자동화 도구의 좌표/권한 문제 구분

여러 모니터와 Retina 배율이 있는 Mac에서 이번 앱 대상 클릭 도구의 이벤트는 창 밖 좌표(예: x=-5074)로 전달됐다. 이 결과는 버튼이 고장났다는 증거가 아니다. 창을 캡처한 픽셀과 macOS의 화면 좌표를 동일하게 취급하지 않는다. 실제 클릭은 게임 창의 위치·크기와 배율을 확인하고 창 안에서 진행한다.

macOS 기본 이벤트 전송 API의 접근 상태가 false라면 OS 승인이 필요하다. 사용자의 텍스트 승인은 OS 인증을 대신하지 않는다. 암호·Touch ID는 사용자가 직접 처리하며, 권한을 우회하거나 다른 앱의 이벤트를 수집하지 않는다.

## 이번 작업의 결과

- GPU 마우스 경로 검사 1개 통과. 실제 버튼 콜백을 직접 호출하지 않았다.
- 기존 메뉴·키보드 PlayMode 검사 6개, Unity 프로젝트·소스 검사를 통과했다.
- 일반 Mac 창에서 도구가 보내는 잘못된 좌표를 원시 이벤트로 재현했다.
- 실제 OS 마우스 검수는 사용자의 macOS 권한 인증을 기다리고 있다. 최종 결과는 확인 후 갱신한다.
