# BabyFoot

VR table-football prototype for the M2 MTI3D senior project in Chalon-sur-Saone. The current direction is performance training for experienced players: receiving, retaining and recovering possession, then passing between rods. Code, object names, data labels and UI are maintained in English.

## Open the project

1. Install Git and Git LFS. Run `git lfs install` on each computer before cloning.
2. Clone the repository, then run `git lfs pull` inside it. Downloading the ZIP is not the supported workflow for LFS art assets.
3. In Unity Hub, add the repository root: the folder containing `Assets`, `Packages` and `ProjectSettings`.
4. Use **Unity 6000.3.25f1**, as recorded in `ProjectSettings/ProjectVersion.txt`. The earlier university setup used 6000.3.24; align the editor versions before moving work between computers. This preparation does not upgrade or downgrade Unity.
5. Let Unity restore the packages pinned in `Packages/manifest.json` and `packages-lock.json` and rebuild its local Library. Do not copy another computer's Library into the repository.

URP, Input System, XR Interaction Toolkit and OpenXR are configured in the project. Current headset testing uses **Meta Quest Pro through Quest Link on Windows**. Connect the headset, establish Quest Link, open a VR scene, and enter Play. Stand with the headset floor correctly calibrated. Eye tracking is a separate diagnostic and is not assumed available simply because Link is connected.

## Scenes

All project scenes are under `Assets/BabyFoot/Scenes/`:

| Scene | Purpose |
| --- | --- |
| `BabyFoot_FreeMatch.unity` | Continuous player-vs-AI match, receiving warm-up and AI-vs-AI spectator demo. Recommended starting point. |
| `BabyFoot_VR.unity` | Reaction defense, anticipation defense and Find the Gap attack exercises with session menus. |
| `BabyFoot_Prototype.unity` | Earlier desktop prototype and keyboard exercise workflow. |
| `BabyFoot_EyeTracking_Test.unity` | Independent eye-tracking availability/calibration diagnostic. |

## Free match controls

- Use controller rays and the index trigger to select menu buttons.
- Hold the side grip near a Blue handle. Slide to translate the rod; twist your wrist to rotate it continuously. Goalkeepers can rotate too.
- Incline a foot over an arriving ball to pin it against the pitch. Keep contact and move gently to retain it; lift or make a physical wrist strike to release/pass.
- **A / X:** new ball. **B / Y:** pause/menu.
- **Receive a pass / warm-up:** repeat slow feeds to the Blue two-player defensive rod, without participant recording.
- **Watch AI vs AI:** both teams play autonomously. Without an active headset, mouse controls provide pause, restart, three fixed monitor views and a return to playing as Blue. VR keeps natural head tracking.

The shared AI predicts incoming balls, attempts receptions, carries/feints and plans forward passes and shots. Balls use a simplified grounded XZ contact/friction model; full spin, airborne dynamics and expert-skill transfer have not been validated. AI intentions are not guaranteed outcomes. Real headset comfort, latency and FPS require hardware testing.

## Repository layout

- `Assets/`: Unity scenes, scripts, imported models, materials, fonts and required samples, including their `.meta` files.
- `Packages/` and `ProjectSettings/`: reproducible package and editor/project configuration.
- `ArtSource/Blender/`: editable source snapshots for Arena, Players, Handles and Table. Runtime FBX files stay in `Assets`; Blender is not required to open/play the Unity project.
- `Docs/`: collaboration and art-source notes.

Unity is already configured for **Visible Meta Files** and **Force Text** serialization. Keep every asset paired with its `.meta` file; move assets through Unity when possible. Do not put YAML scenes/prefabs or `.meta` files in LFS.

## Data and local output

Participant sessions and gaze exports are local research data, not repository content. The default Windows player data root is `%USERPROFILE%/AppData/LocalLow/DefaultCompany/BabyFoot/`, with `Sessions/` and `EyeTrackingTests/` subfolders. Synthetic check output is under the ignored `Library/`. The ignore rules also exclude CSV/TSV exports, recordings, credentials, signing keys, build output and historical scene backup copies.

## Editor utilities and checks

Current checks under `Assets/BabyFoot/Editor/` include `BabyFootContinuousPinCheck.Run()`, `BabyFootBallControlCheck.Run()`, `BabyFootFreeMatchCheck.Run(false)` and `BabyFootSpectatorCheck.Run()`. They start/stop Play and use synthetic input; run them intentionally outside participant sessions. Reports are written under `Library/`. Portable visual check captures go to `Library/BabyFootChecks/Previews/`.

Historical installers/migrations and automatic legacy checks are disabled by default on editor load. They depend on old construction stages and must not rebuild a committed scene automatically on a fresh clone. Explicitly invoking a builder can still replace content; use a separate branch and inspect its result. See `Docs/COLLABORATION.md`.

## Asset notices

Keep bundled upstream notices. Source Sans 3 licensing is in `Assets/BabyFoot/Resources/BabyFootUI/FONT_LICENSE.md`; Unity template/sample assets and packages retain their upstream terms and notices. No repository-wide license has been selected as part of this preparation.
