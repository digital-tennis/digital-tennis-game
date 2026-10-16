# 15-athlete persona, visual and audio upgrade

The roster previously had brief character descriptions, procedural faces and generic court audio. This change gives all 15 existing adult athletes individual formative incidents, motivations, relationships and growth arcs, exposes those stories in the Korean roster UI, and connects their spoken cues to those personas. Established names, identities and balance values remain intact. KOR/JPN were reviewed first, followed by the other 13 athletes.

## Changes

- Adapt actual free Quaternius Standard character faces, five hairstyle sources and an Idle_Loop head track into the existing sportswear and contact rig. Each athlete retains individual proportions, clothing details and presentation. No imported demo controller, root motion or renderer/input architecture replacement.
- Add nine Kenney-derived movement/skid/accepted-contact variations and 60 distinct offline English Flite synthesized lines with Korean subtitles. These are synthesized voices, not bespoke actors or native-language performances. Cloth friction approximates skids; it is not a tennis-shoe field recording.
- Preserve user audio CSV values, edited recordings and subtitles on existing installs. Legacy factory hashes redirect only unchanged factory recordings; missing new cues are supplemented without overwriting customization.
- Keep host-only gameplay authority, buffered guest presentation, normal input acceptance and grip/contact/foot anchors. Match/serve/rally/skill/pause BGM stays silent; each actual point cheers once and stops before the next serve.
- Extend native validation and fix validation-only frame timing/timeout issues without shortening match rules or fabricating results.

See [athlete stories](athlete-stories.md), [world characters](world-characters.md), [asset adaptations](athlete-cc0-assets.md), [audio behavior and migration](court-audio-upgrade.md) and [native verification details](athlete-native-validation.md).

## Licenses and provenance

- Quaternius Universal Base Characters Standard FREE and Universal Animation Library Standard FREE: **CC0-1.0**. Actual archive/member hashes, retained source files and adaptations are recorded in [provenance.json](../Assets/ThirdParty/Quaternius/provenance.json); original licenses accompany the sources. [Runtime bindings](../Assets/ThirdParty/Quaternius/runtime-bindings.json) map the generated athlete meshes.
- Kenney Impact Sounds and RPG Audio: **CC0-1.0**. Original license files, download URLs, archive/source/output SHA-256 and trim/filter/normalization adaptations are retained in [factory provenance](../Assets/StreamingAssets/Audio/factory-provenance.json) and the adjacent `KENNEY-*-LICENSE.txt` files.
- Flite speech: permissive BSD-style terms, retained [COPYING](../Assets/StreamingAssets/Audio/FLITE-COPYING.txt); synthesis engine binaries are not distributed. Voice language is English, subtitle language Korean.

No paid purchases, restricted Asset Store redistribution or copied anime IP are included. The result is stylized low-poly work, with no AAA-equivalence claim.

## Validation

Before delivery, rerun:

```sh
python3 tools/verify-athlete-upgrade.py
python3 tools/verify-athlete-assets.py --archives .work/athlete-assets
python3 tools/verify-court-audio.py --native .work/ac4/audio
bash tools/unity.sh test-edit
bash tools/unity.sh check
python3 tools/verify-native-athlete-review.py --out .work/ac4
git diff --check
```

The narrative verifier passed 5 tests, the archive/asset verifier passed 4, and Unity EditMode passed 165 with zero failures. Audio verification matched 68 actual AudioListener recordings to the source waveforms and checked fresh/existing installation behavior. The native receipt checks the actual build hash, Unity XML and 462 native evidence files; it does not generate successful gameplay outcomes.

The accepted preceding native verification ran Unity **6000.3.26f1**, Built-in/Mono on Mac/Metal: project check, Mac development build, EditMode **165**, PlayMode **37**, motion **4**, front **6**, all passing. Actual adapter-driven six-stroke motion, all 15 roster previews, opening audio/video, full-match menu/results and two native processes with a local WebSocket server were exercised. The host advanced physics; the buffered guest advanced zero physics ticks. Detailed measurements, initial failed attempts and their fixes are recorded in the native verification document.

## Evidence and limitations

Raw camera PNGs, AudioListener WAVs, XML, logs, gallery and opening MP4 remain local under `.work/ac4`; they are intentionally excluded from Git along with `Builds`, `Library`, `Temp` and backup captures. This PR contains source, licenses, provenance and reproducible validation instructions, not embedded raw captures. The local host can inspect `.work/ac4/review.html` and `.work/ac4/verified.json`.

Native runtime DLL SHA-256: `dc4b7f1ea440dd8f2015bd60c92c416f2b418042f7c616c66a65c69bb00b30e8`. This is a development build with debugging enabled, not release certification. The delivery step rechecks existing native evidence rather than claiming a second native gameplay run.

Roster skill stock is explicitly staged visual preview stock, not proof of gameplay charging. Speech waveform comparisons are automated playback checks, not a human acting-quality evaluation. The attempted CUA keyboard observation could not attach to the player; no physical keyboard/pointer success is claimed, and the Mac lock was not bypassed. Automated input/UI callback coverage and PlayMode keyboard-source gate tests are identified separately.

Physical Windows machines, BLE, the separately attached external IMU and two-PC/WAN validation remain unavailable/unverified. Same-Mac two-process relay testing is not two-PC testing. The NU40-DK plus separately attached external IMU requirement and original 12-day/three-developer plan remain unchanged.

Delivery is a feature-branch draft PR against `main`; do not merge as part of this task. No Codex PR-attachment tool is exposed in this session, so return the PR URL for the host to attach.
