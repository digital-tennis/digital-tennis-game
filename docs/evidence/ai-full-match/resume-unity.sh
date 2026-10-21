#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
project_version="$(sed -n 's/^m_EditorVersion: //p' "$project_dir/ProjectSettings/ProjectVersion.txt")"
unity_editor_path="${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/$project_version/Unity.app/Contents/MacOS/Unity}"

if [[ ! -x "$unity_editor_path" ]]; then
  echo "Unity $project_version is missing. Install it with Unity Hub, or set UNITY_EDITOR_PATH."
  exit 1
fi

action="${1:-check}"
case "$action" in
  open)
    exec "$unity_editor_path" -projectPath "$project_dir"
    ;;
  setup) method="DigitalTennis.Editor.SinglePlayerProjectSetup.Prepare" ;;
  check) method="DigitalTennis.Editor.SinglePlayerProjectSetup.Validate" ;;
  build-verification-mac) method="DigitalTennis.Editor.ProjectSetup.BuildVerificationMac" ;;
  build-mac) method="DigitalTennis.Editor.ProjectSetup.BuildMac" ;;
  release-mac) method="DigitalTennis.Editor.ProjectSetup.BuildReleaseMac" ;;
  release-windows) method="DigitalTennis.Editor.ProjectSetup.BuildReleaseWindows" ;;
  balance-experiment) method="DigitalTennis.Editor.BalanceExperiment.Run" ;;
  build-windows) method="DigitalTennis.Editor.ProjectSetup.BuildWindows" ;;
  preview) method="DigitalTennis.Editor.ProjectSetup.ExportPreview" ;;
  test-edit) platform="EditMode" ;;
  test-play) platform="PlayMode" ;;
  test-athlete-art) platform="PlayMode"; test_filter="DigitalTennis.Tests.CharacterSceneTests.EveryRosterSelectionKeepsTheGripAttachedOnItsFirstPresentedFrame" ;;
  test-controller) platform="PlayMode"; test_filter="DigitalTennis.Tests.PacketControllerSceneTests" ;;
  test-ai-resume) platform="PlayMode"; test_filter="DigitalTennis.Tests.FullMatchResumeTests.AuthoritativeResumeBridge" ;;
  test-ai-self-play) platform="PlayMode"; test_filter="DigitalTennis.Tests.FullMatchSelfPlayTests.AuthoritativeSelfPlayBridge" ;;
  test-ai-training) platform="PlayMode"; test_filter="DigitalTennis.Tests.FullMatchTrainingTests.AuthoritativeGymBridge" ;;
  test-ai-baseline) platform="PlayMode"; test_filter="DigitalTennis.Tests.FullMatchBaselineTests.ExpandedPreTrainingBaseline" ;;
  test-ai-evaluation) platform="PlayMode"; test_filter="DigitalTennis.Tests.SinglePlayerSceneTests.SeededLearnedAiEvaluationUsesRealScenesAndServiceChanges" ;;
  test-ai) platform="PlayMode"; test_filter="DigitalTennis.Tests.SinglePlayerSceneTests" ;;
  test-motion) platform="PlayMode"; test_filter="DigitalTennis.Tests.PresentationPolishSceneTests" ;;
  test-front) platform="PlayMode"; test_filter="DigitalTennis.Tests.FrontEndSceneTests" ;;
  test-mouse) platform="PlayMode"; test_filter="DigitalTennis.Tests.FrontEndPointerSceneTests" ;;
  *) echo "Usage: tools/unity.sh {open|setup|check|build-mac|build-verification-mac|build-windows|release-mac|release-windows|balance-experiment|preview|test-edit|test-play|test-front|test-mouse|test-controller|test-ai|test-ai-evaluation|test-ai-baseline|test-ai-training|test-ai-self-play|test-ai-resume|test-motion|test-athlete-art}"; exit 2 ;;
esac

mkdir -p "$project_dir/.work/logs"
log_file="$project_dir/.work/logs/$action.log"
echo "Unity $project_version: $action"
echo "Close this project in Unity before a batch operation. Log: $log_file"
if [[ "$action" == test-* ]]; then
  cp "$project_dir/Assets/StreamingAssets/Balance/characters.csv" "$project_dir/.work/test-balance.csv"
  cp "$project_dir/Assets/StreamingAssets/Effects/effects.csv" "$project_dir/.work/test-effects.csv"
  cp "$project_dir/Assets/StreamingAssets/Balance/gameplay.csv" "$project_dir/.work/test-gameplay.csv"
  cp "$project_dir/Assets/StreamingAssets/Balance/rules.csv" "$project_dir/.work/test-rules.csv"
  test_args=()
  if [[ -n "${test_filter:-}" ]]; then test_args=(-testFilter "$test_filter"); fi
  if [[ "$action" == "test-ai-evaluation" ]]; then test_args+=(-dt-ai-evaluation-out "$project_dir/.work/ai-evaluation.json"); fi
  if [[ "$action" == "test-ai-baseline" ]]; then test_args+=(-dt-ai-baseline-out "$project_dir/.work/ai-full-match-baseline-raw.json"); fi
  if [[ "$action" == "test-ai-training" ]]; then test_args+=(-dt-gym-port "${DT_GYM_PORT:-18571}"); fi
  if [[ "$action" == "test-ai-resume" ]]; then test_args+=(-dt-resume-port "${DT_RESUME_PORT:-18572}"); fi
  if [[ "$action" == "test-ai-self-play" ]]; then test_args+=(-dt-self-play-port "${DT_SELF_PLAY_PORT:-18572}"); fi
  test_graphics=(-batchmode -nographics)
  if [[ "$action" == "test-mouse" ]]; then test_graphics=(-batchmode); fi
  "$unity_editor_path" "${test_graphics[@]}" -projectPath "$project_dir" -dt-rules-file "$project_dir/.work/test-rules.csv" -dt-gameplay-file "$project_dir/.work/test-gameplay.csv" -dt-balance-file "$project_dir/.work/test-balance.csv" -dt-effects-file "$project_dir/.work/test-effects.csv" -runTests -testPlatform "$platform" "${test_args[@]}" -testResults "$project_dir/.work/$action.xml" -logFile "$log_file"
  echo "Completed: $action"
  exit 0
fi
batch_args=(-batchmode -nographics)
if [[ "$action" == "preview" ]]; then batch_args=(-batchmode); fi
"$unity_editor_path" "${batch_args[@]}" -quit -projectPath "$project_dir" -executeMethod "$method" -logFile "$log_file"
echo "Completed: $action"
