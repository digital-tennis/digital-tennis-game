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
  setup) method="DigitalTennis.Editor.ProjectSetup.Prepare" ;;
  check) method="DigitalTennis.Editor.ProjectSetup.Validate" ;;
  build-mac) method="DigitalTennis.Editor.ProjectSetup.BuildMac" ;;
  build-windows) method="DigitalTennis.Editor.ProjectSetup.BuildWindows" ;;
  preview) method="DigitalTennis.Editor.ProjectSetup.ExportPreview" ;;
  *) echo "Usage: tools/unity.sh {open|setup|check|build-mac|build-windows|preview}"; exit 2 ;;
esac

mkdir -p "$project_dir/.work/logs"
log_file="$project_dir/.work/logs/$action.log"
echo "Unity $project_version: $action"
echo "Close this project in Unity before a batch operation. Log: $log_file"
batch_args=(-batchmode -nographics)
if [[ "$action" == "preview" ]]; then batch_args=(-batchmode); fi
"$unity_editor_path" "${batch_args[@]}" -quit -projectPath "$project_dir" -executeMethod "$method" -logFile "$log_file"
echo "Completed: $action"
