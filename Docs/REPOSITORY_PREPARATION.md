# Repository preparation

Prepared locally on 2026-11-07. The repository root is the Unity project folder, containing Assets, Packages and ProjectSettings.

- Git initialized on main; no remote or commit created during preparation.
- Git LFS installed locally, with 168 binary art/font/audio/video files verified against their local SHA-256 object contents.
- Unity scenes, prefabs and meta files remain normal text; the large sample skybox is an LFS pointer in the index.
- Four editable Blender sources copied outside Assets; imported FBX paths and existing asset GUIDs preserved.
- Local caches, recordings, historical scene backups, raw CSV/TSV exports, study-data folders and credential/signing files excluded.
- Candidate audit found no missing/orphan asset meta files, no raw CSV/TSV or local-cache candidates, and no matches for the targeted credential patterns. A pattern scan is not a general guarantee about arbitrary private content.
- 28 legacy load-time automation constructors now require explicit session opt-in; four visual checks use project-relative ignored output paths.
- Unity 6000.3.25f1 batch import/compilation completed and exited successfully (code 0). No clean-machine clone or headset run was performed for this repository-only preparation.

The candidate source content is about 113 MiB before Git/LFS storage. Unity-generated YAML has existing trailing spaces in empty scalar fields; those files were preserved rather than reformatted. The candidate snapshot is staged for review. Select the GitHub destination and visibility before committing/publishing.
