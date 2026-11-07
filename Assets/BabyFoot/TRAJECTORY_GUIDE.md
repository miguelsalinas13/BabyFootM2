# Trajectory Anticipation prototype

Choose Reaction Defense or Trajectory Anticipation on the session setup screen. Both support separate practice and measurement blocks and retain the existing lateral goalkeeper controls.

## Long shots

Trajectory Anticipation enables all eight rods and launches from a figure on the distant opposing defensive rod. Intermediate rods are held still at 75 degrees to lift their feet above the ball and keep passing lanes open. The goalkeeper remains vertical. The distant goalkeeper remains visible behind the source. The source, intermediate poses and launch animation do not change with the upcoming target direction.

Each trial is direct or has one specular reflection from a side wall. The trajectory is fully visible. The sphere centre reflects at the inner wall minus its radius. Travel uses constant arc-length speed. The same fixed strike begins on the launch event. Routes are checked against the expanded bounds of intermediate player colliders before starting. A blocked route is rejected with an error, rather than making the ball pass through a player.

This first version does not simulate impacts against player feet, multiple rebounds, moving intermediate rods, spin, restitution or friction. It combines visual tracking, prediction and reaction; it is not a validated isolated measure of anticipation. Future work can add player deflections or occlude the final segment after the last rebound.

## Data

The CSV retains its original first 14 columns, then appends exercise_mode, route_kind, wall_side, start_x_m, start_z_m, path_length_m, planned_bounce_s, bounce_observed and keeper_error_m. The planned bounce time is computed from path length and speed; it is not a hardware timestamp. Keeper error is the absolute lateral difference from the planned goal target when the trial ends, and is blank for pre-shot false starts. Per-trial path metadata allows the controlled route to be reconstructed; table geometry and source parameters must also be retained.

Half of the planned measurement routes are direct and half have one side-wall reflection. Target directions are balanced separately and both sequences are shuffled with recorded, derived seeds. False starts consume a scheduled trial, so physically launched shots can be unbalanced. Session metadata records the exercise and the fixed raised-lane condition. Reaction Defense preserves its original short-shot layout.

Desktop checks exercise both session flows, all six direct/bank target combinations, specular reflection, sphere-centre wall limits, path endpoints, collision clearance, layout restoration and CSV labels. No Quest Pro validation has been performed.
