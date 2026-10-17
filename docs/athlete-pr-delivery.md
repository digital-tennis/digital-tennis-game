# 한국·일본 선수 외형과 전신 모션 PR 검토

PR18 (`69bdf9c`)의 머리 중심 개선 이후 남아 있던 몸 비율과 이동 표현을
한국 Han Seo-yun(26)·일본 Ren Aoyama(24)에 한정해 보완했다.
CC0 Quaternius의 보관 원본을 검사해 연결된 성인 전신 표면과 얼굴·헤어를
사용하고, 의상 경계·그립·리타깃 설정을 authored JSON에 보관한다.
나머지 13명 프로필과 경기 능력치·음성·자막은 유지한다.

취득한 `Idle_Loop`, `Walk_Loop`, `Jog_Fwd_Loop`, `Sprint_Loop`의 전신
트랙을 이동 거리에 맞춰 샘플링한다. 테니스 모캡을 취득했다고 주장하지 않는다.
타격은 기존 authored 테니스 동작과 접촉 IK를 결합하며 승인 타이밍을 유지한다.
고정 손가락, 로우폴리 면, 한계 런지의 발 앵커 보정과 짧은 서브 준비는 남는다.

## 비교 자료와 재현

- [원본·라이선스·해시·런타임 연결](athlete-cc0-assets.md)
- [중립 전신/얼굴 비율 검토](athlete-proportions-review.md)
- [실제 입력 어댑터 경기·모션·온라인 검토](athlete-motion-review.md)
- [같은 카메라 전후 비교와 원본 PNG 열기](athlete-comparison-review.md)
- [Unity 및 동작 보존 회귀](athlete-regression-review.md)

이 작업 트리의 `.work/athlete-comparison/index.html`에서 PNG 원본과
비교표를 연다. 정면·측면·얼굴의 중립 비교는 **staged art preview**이며,
동작 프레임은 새 검증 앱의 실제 CLI 입력 어댑터 경기에서 촬영했다.
로컬 자료는 PR의 웹 첨부가 아니다. 실제 이미지와 일반 Mac 창의 메뉴·방향키·선수 확정을 확인했으며, 경기 동작은 새 native 앱의 정상 입력 어댑터로 검증했다. 원본 캡처·빌드·로그는 Git에 넣지 않는다.
PR18의 447개 자료를 이번 새 메시 증거로 재사용하지 않는다.

앞선 AC에서 Unity 6000.3.26f1 check, EditMode 169개, PlayMode 37개,
별도 ID의 Mac 빌드와 native 모션/버퍼 온라인 검사를 실행했다.
정확한 실행 범위와 측정치는 위 문서에 기록되어 있다.
이번 전달 단계에서는 보존 회귀 4개를 재실행하고, 아래 읽기 전용 검사로
로컬 HEAD·원격 브랜치·열린 초안 PR head의 일치를 확인한다.

```sh
./.work/verification-venv/bin/python3 -B -m unittest discover -s tools -p test_athlete_delivery.py -v
```

실행 중인 사용자 일반 앱은 종료하거나 덮어쓰지 않았다. 별도 검증 앱은
batchmode와 스피커 모니터 뮤트를 사용했다. Windows 실물·BLE·별도 부착
외장 IMU·물리적 두 PC/WAN은 미검증이다. NU40-DK + 별도 부착 외장 IMU와
12일·3인 계획은 유지한다. PR은 초안으로 제출하고 병합하지 않는다.

## 최종 보완 검증

- 헤어 정수리의 보간 범위 고정 문제를 고치고 원본 glTF 정점과 대조하는 검사를 추가했다.
- 최종 모델로 Unity check·EditMode 169개와 native 75회 접촉·6종 타격·양 선수 이동/서브/회복을 확인했다.
- 새 패키지의 실제 모델 이름과 Body JSON 전체 바이트, DLL 및 리소스 해시를 검증한다. 캐시된 DLL의 날짜를 인위적으로 바꾸지 않는다.
- 같은 최종 앱으로 온라인 2–0 세트와 재접속 epoch 2, 호스트 물리/게스트 비권한, 상대 포즈·타격·좌우 시점 오류 0을 확인했다.
- 두 CSV의 줄바꿈만 LF로 정규화했다. 모든 게임 밸런스 값과 문서 행은 유지한다. 전달 검사는 전체 작업 트리 clean을 요구한다.
- `tools/athlete-current-mechanical.toml`의 읽기 전용 통합 검사는 실제 캡처·출처·기존 동작·Unity 결과·GitHub 전달을 함께 확인한다. 실행 전 최신 캡처와 인증된 GitHub 접근이 필요하다.

자동 실행의 마지막 검사는 구형 head/hair 전용 고정 검사와 body 바인딩 형식 충돌, 이전 CRLF 파일의 clean 검사 충돌로 실패했다. 이 과거 실행을 승인됐다고 주장하지 않는다. 원래 출처/라이선스 요건을 유지하는 body-aware 검사와 실제 Unity 증거로 최종 결과를 별도 검증하며, 보호된 검사 파일은 수정하지 않았다.
