# Digital Tennis game development

- The Unity project is at the repository root. Use Unity **6000.3.26f1**.
- The initial project uses the **Built-in Render Pipeline** and Mono for desktop development.
- Preserve the NU40-DK plus **separately attached external IMU** requirement and the 12-day, three-developer plan.
- GM-02 implements 60Hz ball flight, swept ground/net contacts and bounce/out events. Scoring, rally phases, player movement, AI, BLE and online transport remain separate tasks.
- Keep ball motion in BallSimulation, landing bounds in CourtBounds and Unity presentation in BallFlight/BallBounceDemo. Only the authoritative BallFlight may advance physics or publish events.
- Treat `Assets/`, `Packages/`, `ProjectSettings/` and matching `.meta` files as source. Do not commit `Library/`, `Temp/`, `UserSettings/`, `.work/` or `Builds/`.
- Generate or change scenes and materials using Unity Editor APIs. Preserve asset GUIDs and `.meta` files when moving or renaming files.
- See the product assignments linked in README before changing module responsibilities. Keep AI, controller protocol, BLE input and the match server in their planned repositories.
- Close the project in the Unity Editor before batch operations; never force-close an Editor with unsaved work.
- Run `bash tools/unity.sh check` after code or scene changes. Use `build-mac` or `build-windows` when a change needs a standalone build check.
- Run `test-edit` and `test-play` after ball physics or event changes. Preserve the inclusive line boundaries and unique event IDs across shots.
- `setup` creates the development scene only if it is missing and preserves an existing scene. Do not delete an existing scene to force regeneration.
- Configure the current machine's Editor with `UNITY_EDITOR_PATH` when it is installed outside the default macOS location.
- Keep actual test/build evidence and known limitations separate. Windows BLE and two-PC online validation still require physical Windows test machines.
