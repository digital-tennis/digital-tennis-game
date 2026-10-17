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
  test-motion) platform="PlayMode"; test_filter="DigitalTennis.Tests.PresentationPolishSceneTests" ;;
  test-front) platform="PlayMode"; test_filter="DigitalTennis.Tests.FrontEndSceneTests" ;;
  *) echo "Usage: tools/unity.sh {open|setup|check|build-mac|build-verification-mac|build-windows|release-mac|release-windows|balance-experiment|preview|test-edit|test-play|test-front|test-controller|test-motion|test-athlete-art}"; exit 2 ;;
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
  "$unity_editor_path" -batchmode -nographics -projectPath "$project_dir" -dt-rules-file "$project_dir/.work/test-rules.csv" -dt-gameplay-file "$project_dir/.work/test-gameplay.csv" -dt-balance-file "$project_dir/.work/test-balance.csv" -dt-effects-file "$project_dir/.work/test-effects.csv" -runTests -testPlatform "$platform" "${test_args[@]}" -testResults "$project_dir/.work/$action.xml" -logFile "$log_file"
  echo "Completed: $action"
  exit 0
fi
batch_args=(-batchmode -nographics)
if [[ "$action" == "preview" ]]; then batch_args=(-batchmode); fi
"$unity_editor_path" "${batch_args[@]}" -quit -projectPath "$project_dir" -executeMethod "$method" -logFile "$log_file"
echo "Completed: $action"
