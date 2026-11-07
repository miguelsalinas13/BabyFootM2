# Moving between home and university

Use the same Unity version on both computers. The authoritative editor version is in ProjectSettings/ProjectVersion.txt, currently 6000.3.25f1.

## Before switching computers

1. Save scenes/assets and Blender sources; exit Play and close Unity before switching branches or pulling scene changes.
2. Review changes in Git, including .meta files. Do not add Library, raw participant exports or local recordings.
3. Commit and push the work after the repository/remote has been selected.
4. At the other computer: pull, run `git lfs pull`, then open Unity and allow imports to finish.

For the first setup, install Git LFS before cloning. GitHub ZIP downloads are not a substitute for checking out the full LFS files. Verify models/textures are real files rather than small LFS pointer text before opening Unity.

Prefer one person editing a scene at a time. Keep text serialization and visible meta files enabled. Do not resolve scene conflicts by discarding GUIDs or regenerating .meta files. Binary Blender/model/image changes need coordination because ordinary text merging does not combine their edits.

## Legacy editor automation

Older construction scripts used InitializeOnLoad plus Library markers to modify scenes or automatically start checks. Library is intentionally absent in a fresh clone. Their load-time scheduling is now guarded by `BabyFootLegacyAutoSetup.Enabled`, false by default. A maintainer can intentionally enable it for the current editor session with `Tools > BabyFoot > Development > Enable legacy auto setup for this session`; this requests a script reload. Restarting Unity resets that opt-in. Enabling it can run historical scene modifications/checks, so it is not part of normal setup.

Modern explicit Run() checks retain their own pending-file guards and remain available without enabling old setup scripts. Local previews are created below Library/BabyFootChecks/Previews and are ignored.

## Publication status

The repository is prepared locally. No GitHub remote, public/private choice, initial commit or push is made by this preparation. Review the candidate snapshot and choose the destination before publishing. There is no need to include cached packages, unrelated art projects or development-chat history.
