# BabyFoot table

Low-poly reference-inspired cabinet and playing surface. English names; metre units. Separate body and field FBX exports keep Unity gameplay transforms and colliders independent.

## Import

Use Body for the cabinet, walls, goals, legs, bearings, score counters and ball-return details. Use Field for the pitch and markings. Replace only the corresponding Unity renderers; preserve the existing rod roots, colliders and exercise references. Disable old field renderers when adding the exported field to avoid z-fighting.

The nominal cabinet is 0.74 x 1.43 x 0.90 m. Score counters and return trays extend beyond it. Playfield 0.68 x 1.113 m, top at 0.755 m; shaft axes at 0.84 m. These internal dimensions match the prototype and are still provisional. Goal openings are 0.205 x 0.080 m. There are real shaft holes in the side wall geometry.

The Blender source includes all eight rods, 22 figures and eight handles as a separate preview collection using the existing shared meshes. They are not in the body/field FBX exports. Do not import the whole assembly over the existing gameplay assembly. No colliders, rigidbodies, lights or cameras are exported.

The table uses flat-colour materials without textures or subdivision. The studio lighting is presentation only. Profile the final Unity assembly on Quest Pro; polygon count alone does not establish VR performance.
