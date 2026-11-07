# BabyFoot prototype

Project language: English. Keep code identifiers, comments, UI, object names, logs and data labels in English. BabyFoot is the project name.

Open `Assets/BabyFoot/Scenes/BabyFoot_Prototype.unity`.

## Defense exercise

- A/D or left/right arrows: move the goalkeeper.
- SPACE or Start trial: begin a trial with a random waiting interval.
- R: reset the position.
- New CSV files use `save`, `goal` and `false_start` as result values. Existing CSV files retain their original contents.
- Results are written to `Application.persistentDataPath` in a separate CSV file per Play session.
- `response_s` measures time from the software shot event to a goalkeeper displacement of at least 5 mm. A blank value means no post-shot movement response was recorded. This is not calibrated hardware latency.

The exercise uses deterministic trajectories and simplified collision checks. Only the goalkeeper and attacking rod are active. Other rods remain available in the scene. Eye tracking and VR controller interaction are pending hardware validation.

## VR setup

The template VR rig is available. When using a headset, enable the VR rig and disable the desktop preview camera as appropriate. Check that only one intended camera and AudioListener are active. Quest Pro operation has not yet been validated.

## Reference dimensions (metres)

- Cabinet: 1.43 long, 0.74 wide, rail height 0.90. Manufacturer: https://original-leonhart.com/tischkicker/einsteiger-kicker/leo-tournament/
- Field: 1.113 x 0.68; rod diameter: 0.016; side wall: 0.0315. Retailer specification, exact variant pending confirmation: https://www.kickerkult.de/Leonhart-Tournament-ITSF-Tischkicker/7040.16
- Goal opening: 0.205 x 0.08, based on a spare part: https://kicker-klaus.de/kicker-torrahmen-leonhart . Assembly and corner shapes are simplified.
- Field height 0.755, rod height 0.84, rod positions, player dimensions, movement range, ball diameter 0.035 and mass 0.023 kg are provisional prototype values.

Each rod has a parent at unit scale, with local X used for sliding and rotation. Visual geometry and collision shapes can be refined independently.
