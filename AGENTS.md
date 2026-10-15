# Digital Tennis game development

- The Unity project is at the repository root. Use Unity **6000.3.26f1**.
- The initial project uses the **Built-in Render Pipeline** and Mono for desktop development.
- Preserve the NU40-DK plus **separately attached external IMU** requirement and the 12-day, three-developer plan.
- Always squash-merge approved PRs. After merging, delete the merged remote and local feature branches; preserve any unmerged work.
- GM-02 implements ball motion. GM-03/04 add a one-game serve/rally state machine and pure scoring. The first server stays fixed and the serve side alternates after points, as specified in the MVP assignments. GM-05 adds normalized racket inputs, hit gating and automatic player movement. Single-player AI policies live in digital-tennis-ai and are consumed as a pinned Git UPM dependency. Sets/tiebreaks, real BLE input and direct P2P remain separate tasks. OnlineTennisSession currently uses an explicit WebSocket relay with digital-tennis-match-server.
- SinglePlayerCourt is the default launch scene: human Near, AI Far. Keep AI decisions in the external DigitalTennis.AI assembly and conversions in TennisAiOpponent. Single-player mode must disable practice auto-swings and manual Far controls; online mode must not run AI.
- Use MatchCourt for serve/rally/score development and preserve DevelopmentCourt as the standalone ball lab. Keep scoring in TennisScore/PointLedger, rules in TennisMatch and scene/input presentation in TennisMatchController.
- Keep normalized racket input and hit validation in RacketInput, shot math in RacketShot, movement prediction in AutomaticCourtMovement and avatar presentation in RacketAvatar. Every source must use SubmitInput; do not bypass reach, duplicate or stale-input checks for demos.
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

- Keyboard and future decoded board events belong in IPlayerInputSource → PlayerInputRouter → PlayerInputAdapter. Source switches clear backlogs; use the local PC receipt clock. Bind player roles in the game/session, never in a device packet.
- Only the online host runs rules/physics. Guest snapshots are presentation only; validate epoch and wrapping sequence numbers. Pause physics/input on loss; resume requires rotated token, new epoch and a fresh snapshot. Never log reconnect tokens.
- Keep the standalone CLI validation source opt-in, routed through the real input adapter. It must never write physics/score or run in normal user play. `node tools/verify-online.mjs` requires the sibling server installed and a Mac build.
