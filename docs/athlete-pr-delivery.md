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
로컬 자료는 PR의 웹 첨부가 아니며, 부모 검토자가 같은 작업 트리에서
이미지와 실제 Mac 창을 별도로 확인해야 한다. 원본 캡처·빌드·로그는 Git에 넣지 않는다.
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
