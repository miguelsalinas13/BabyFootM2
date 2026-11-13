# Visual finish — November 8, 2026

Review scene: `Assets/BabyFoot/Scenes/BabyFoot_FreeMatch_Polished.unity`.
The original FreeMatch, VR, Prototype, eye-test scenes and original materials are preserved.

The visual variant uses photographic CC0 walnut and concrete textures from Poly Haven,
OpenGL normal maps, converted roughness/smoothness maps, metric planar UV copies,
chrome shafts, moulded rubber grips, enamel figures, a satin green bed and ivory markings.
The floor albedo is neutralized and its contrast reduced for an indoor polished surface.
Arena materials use a restrained charcoal, plum and brass palette.

`Assets/BabyFoot/VisualFinish/` contains all new materials, texture source notices,
mesh copies, render configuration, reflection map and lighting profile.
`BabyFootVisualProfile` selects the balanced PCVR URP asset while this scene is active
and restores the previous quality pipeline when it is disabled. It does not change XR
tracking, camera pose, input, physics, AI or participant data formats. Editor quality
settings may serialize the active visual pipeline while this scene is open.

The new pipeline enables HDR and a shadowless fill light, box-projected baked reflections,
two shadow cascades and subtle half-resolution, four-sample contact occlusion.
Natural Interior Grade uses ACES, modest contrast and restrained saturation.
No bloom, depth of field, motion blur, chromatic aberration or forced VR camera motion.

The static interior uses three directional lightmaps and 27 light probes, baked with
four warm rectangular lights. These lights add no realtime light calculations.
The existing spectator-camera creation now copies the player camera's post-processing
flag; this visual-only change lets the monitor retain the selected scene's color grade.

Verification on November 8: FreeMatchCheckV1 PASS on the polished scene (continuous
wall/player rebounds, both goals, independent two-hand slide, rotating keeper and live
AI strike). No participant data were generated. Render captures use 4x MSAA.

`BabyFootVisualFinish` is an explicit editor authoring utility. Its InitializeOnLoad
registration only polls an ignored Library request file; it never migrates a new clone
automatically. Inspect/capture is also available under Tools > BabyFoot > Visual Finish.
Application starts from the original free-match scene; do not rerun it over custom saved
changes in the polished variant without a backup, since it regenerates the variant.
Light baking and gameplay verification are separate explicit local commands.

Captures and checks: `Library/BabyFootChecks/VisualFinish/`.
The table collider/transform signature was unchanged: 93 colliders and eight rods.
Real headset performance, glare, eye comfort and texture aliasing require Quest testing.
This is an art-direction/material/lighting pass on the existing low-poly geometry;
it does not claim photographic reconstruction or commercial production certification.
