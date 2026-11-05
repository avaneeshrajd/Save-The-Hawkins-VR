# Vecna's Curse (Part 2): Development Progress

Overall estimated progress: **about 20%** (audit, scene setup, small props, Creel House exterior model, Hawkins street foundation; no gameplay built).
Current phase: **Phase 2. Milestones 1 to 4 done. Waiting for approval of Milestone 5.**

## Session log

### Session 1 (2026-11-05): Phase 1 audit

- Milestone completed: read-only audit of project, scenes, scripts, settings and downloaded assets.
- Assets imported or created: none.
- Scripts created or modified: none. Scenes and project settings: unchanged.
- Files created: this file.
- Testing performed: static inspection of files and of `~/Library/Logs/Unity/Editor.log`. Nothing was run in Play Mode or on a Quest. Editor.log showed no `error CS` or compilation failure lines.

### Session 2 (2026-11-05): Milestone 1, scene setup

Status: **setup run in the Editor and saved results verified. Play Mode and headset behavior not yet confirmed.**

Verified from `Editor.log` and saved files:

- Log shows `Scene checks passed: XROrigin=1, AudioListener=1, EventSystem=1` and `Milestone 1 setup finished`, with no `error CS` lines.
- Scene is `Vecna Curse.unity`, with its GUID unchanged. It holds one XR rig prefab instance plus the expected objects.
- Rig overrides saved: floor tracking, far clip 200, solid dark background.
- Build Settings lists Home, Hawkin City and Vecna Curse at current paths.
- Home tile has one Button calling `OnVecnaCurseClick` on the same `StartGame` object as Demo Hunt. Its `Wait` overlay is inactive.

Not yet confirmed: tile click loads the scene, and move and turn work in the scene.

Details of the work:

- Decisions used (recommended defaults): editor builder script instead of MCP; rename scene to `Vecna Curse`.
- No commit made. No files deleted.
- Script created: `Assets/VecnaCurse/Scripts/Editor/VecnaCurseSceneSetup.cs` (menu: `Save The Hawkins > Vecna Curse > Run Milestone 1 Setup`). It can run more than once.
- Script modified: `Assets/Scripts/StartGame.cs` (added `OnVecnaCurseClick`).
- Backup created: `Backups/Milestone1/Home.unity`.
- Testing performed: script compile check, Editor run of the setup, and inspection of saved files.
- The setup did the following:
  - Creates the `Assets/VecnaCurse/` folders.
  - Renames the scene to `Vecna Curse.unity`.
  - Adds a Button to the Home `Vecna's Curse` tile, targeting `StartGame.OnVecnaCurseClick`, and hides its `Wait` overlay.
  - Lists Home, Hawkin City and Vecna Curse in Build Settings.
  - In Vecna Curse: removes the default camera; adds the XR rig prefab (floor tracking, far clip 200), `VC_PlayerSpawn`, XR Interaction Manager and EventSystem with XRUIInputModule.
  - Adds `TEMP_SpawnGround_Placeholder` (60 x 60 m, temporary) and dark placeholder lighting.
- Known limits: no gun in this scene yet, no in-scene way back to Home, and the player can walk off the placeholder ground.

### Session 3 (2026-11-05): Milestone 2, small prop setup

Status: **builder run in the Editor and saved results verified. One re-run needed for the door yaw fix. Owner visual check reported only as "done".**

Verified from `Editor.log` and saved files:

- Log shows `Milestone 2 prop build finished: 6 prefabs`, and 6 prefabs and 12 materials exist. The scene holds the rig plus 6 lineup instances.
- Final sizes in metres: door 0.139 x 2.05 x 0.967, wall clock 0.45 x 0.45 x 0.036, flashlight 0.066 x 0.066 x 0.24, CRT 0.6 x 0.39 x 0.318, grandfather clock 0.422 x 2.2 x 0.345, cassette player 0.229 x 0.25 x 0.06.
- Triangles as built: door 1,580, wall clock 640, flashlight 688, CRT 3,812, grandfather clock 40,722, cassette player 4,487 (brand text meshes hidden).
- Sampled imports confirm cameras, lights and blend shapes off, and door albedo at Android ASTC 6x6, max 1024.
- `Editor.log` also shows terrain template shader dependency lines from URP and Shader Graph packages. They do not come from this work.

Door yaw fix: the log showed the door's broad face along X, so `ModelYaw = 90` was added and compile-checked. It takes effect after the next builder run. Which side is the front is unverified.

Build details:

- Milestone 1 test: owner replied "done"; treated as passed, without detail on tile click or locomotion.
- Unity MCP: `claude mcp list` shows `UnityMCP` connected at `http://127.0.0.1:8080/mcp`, and `com.coplaydev.unity-mcp` is installed. Its tools are not loaded in this Claude Code session, so a restart is needed before they can be used.
- The package tracks the moving `#main` branch. Pin a release before relying on it.
- Script created: `Assets/VecnaCurse/Scripts/Editor/VecnaCursePropBuilder.cs` (menu: `Save The Hawkins > Vecna Curse > Run Milestone 2 Prop Build`).
- The builder did the following:
  - Sets model import options: no cameras, lights, animation or blend shapes; not readable; low mesh compression.
  - Sets Android texture overrides on used textures: ASTC 6x6, max 512 or 1024.
  - Creates Simple Lit materials in `Assets/VecnaCurse/Materials/Props/`.
  - Creates six prefabs in `Assets/VecnaCurse/Prefabs/Props/`, each scaled to real-world size with a box collider.
  - Hides the wall clock glass and the third-party brand text meshes on the cassette player.
  - Adds `TEMP_PropLineup` to the Vecna Curse scene for a visual check.
- Unverified until viewed: FBX orientation; CRT and cassette material-to-slot mapping (inferred, not read from FBX connections).
- Still heavy, not reduced: grandfather clock (40,722 triangles).
- Not done: LOD groups, Rigidbody and grab setup (planned with gameplay), door hinge pivot.

### Session 4 (2026-11-05): Milestone 3, Creel House exterior

Status: **builder run in the Editor and saved results verified. Owner visual check reported only as "done". Not tested in a headset.**

Verified from `Editor.log` and saved files:

- Log shows `Milestone 3 Creel House build finished`, with no builder errors and no `error CS` lines.
- Imported triangles: LOD0 65,602, LOD1 38,126, LOD2 8,155. LODGroup present.
- Size 13.18 x 17.12 x 16 m, lowest point y 0. Door marker (0, 1.70, -2.94), so the door side faces -Z.
- 10 materials created and remapped. Base diffuse confirmed at Android ASTC 8x8, max 1024.
- Door fix applied: door is now 0.967 x 2.05 x 0.139 m.
- Scene holds `VC_CreelHouse_Exterior` at z = 15, plus the rig and prop lineup.

Build details:

- Owner decisions: Blender yes, commit yes, cleanup yes, original boss. The downloaded Vecna model stays out of Part 2 and remains on disk. The two mp3 files were not touched.
- Commits: work is on branch `vecna-curse`, created from `main`. Not pushed. The owner had already committed the folder move and Milestones 1 and 2 to `main`.
- Cleanup: 40 files removed (182 MB): zips, `hauntedhouse.blend`, `hauntedhouse.obj`, `.DS_Store`, same-named duplicate textures. Also removed `Backups/`. Tracked files can be restored from git history.
- Blender 5.2.3 installed with Homebrew.
- Finding: the model is a hollow shell with no interior rooms, 16 x 13.18 x 17.12 m, with its front along Blender +X.
- Interior decision (recommended default, reversible): build the seven rooms as a separate modular set, entered from the front door with a fade transition. The exterior can then be disabled while inside.
- Created `Tools/Blender/creel_reduce.py`, which outputs `Assets/VecnaCurse/Environments/CreelHouse/CreelHouse.fbx`:
  - Triangles: source 216,735; LOD0 65,625; LOD1 38,145; LOD2 8,173. LOD1 is higher than planned.
  - Roof fence removed to avoid alpha cutout. Lowest point at ground level. Door marker included.
- Created `Assets/VecnaCurse/Scripts/Editor/VecnaCurseCreelBuilder.cs` (menu: `Save The Hawkins > Vecna Curse > Run Milestone 3 Creel House Build`). It re-runs the prop build for the door yaw fix.
- Not done: walkable porch and stairs (body box collider only), lighting, license verification of the house model.

### Session 5 (2026-11-05): Milestone 4, Hawkins street foundation

Status: **built and checked through UnityMCP in the Editor, including a Play Mode collision test. Not tested in a headset. No Quest measurements.**

- UnityMCP: connected to this project and used for builds, console checks, screenshots and Play Mode.
- Built by `VecnaCurseHawkinsBuilder.cs` (menu: `Save The Hawkins > Vecna Curse > Run Milestone 4 Hawkins Build`), with models from `Tools/Blender/hawkins_env.py`.
- Terrain: `VC_Terrain`, 7,200-triangle mesh with collider, flat in the play area, rising to hills. A mesh was used instead of Unity Terrain to keep rendering cheap.
- Sky: original procedural night sky with moon, stars, cloud and low red glow, as a cubemap skybox.
- Atmosphere: linear fog 12 to 135 m. Moon directional light without shadows. Flat ambient.
- Street, running along +Z from spawn to the Creel House (moved to z = 138):
  - Road, sidewalks and dirt path.
  - 6 reused American houses, reduced from 16,951 to LOD0 7,000 and LOD1 1,500 triangles, facing the road. Decal faces removed to avoid alpha cutout.
  - 5 original streetlights with emissive lens and additive ground glow. They do not cast real light.
  - 34 original dead trees, 454 triangles each.
  - Invisible play bounds.
- Removed `TEMP_SpawnGround_Placeholder` and `TEMP_PropLineup`. The prop prefabs remain but are not placed. The prop builder no longer creates the lineup.
- Bug found and fixed: streetlights were upside down because of a 180-degree rotation case.

Testing performed:

- Console: no errors after the final build.
- Scene counts: XROrigin 1, AudioListener 1, EventSystem 1.
- Play Mode with direct `CharacterController.Move` calls: rig stays on terrain; moves along road; stops at house collider (x = -9.49), left bound (x = -23.32) and Creel body (z = 134.88). Controller input not tested.
- Ground raycasts hit `VC_Terrain` at four sampled points.
- Screenshots checked from several street positions, using desktop cameras.
- Worst-case triangles if every LOD0 were visible at once: 144,372. Actual frame cost and draw calls not measured.

Side effect on Part 1: Android overrides were set on textures shared with Hawkin City: `american_house_DIFF` (max 1024, ASTC 8x8), `RoadLines` (max 1024), `Grass02` and `Sidewalk01` (max 512). Check Demo Hunt visuals on Quest.

Not done yet: abandoned cars, bicycles, poles and wires, broken windows, leaves, newspapers, posters, radios, floating particles, red emergency lighting, red lightning, light flicker. Baked lighting not attempted.

## Audit findings

### Tooling

- Project editor version is 6000.3.19f1 (the brief says 6000.3.5f2).
- No Unity MCP server is configured globally or for this project, and no MCP package is in `Packages/manifest.json`.
- The Unity Editor was open during the audit, so batch-mode runs on this project will fail until it closes.
- Blender and `adb` are not on PATH, and neither Blender nor Meta Quest Developer Hub is in `/Applications`.

### Scenes and Home menu

- The target scene file is misspelled: `Assets/Save The Hawkins/Scenes/Vecan Curse.unity`.
- That scene is empty: Main Camera and Directional Light only. It has no XR rig.
- The scene is not in Build Settings. Build Settings lists Home and Hawkin City at their old `Assets/Scenes/` paths, with correct GUIDs.
- In `Home.unity`, the `Vecna's Curse` and `MindFlyer Survival` mode tiles have no Button component. Only `Demo Hunt` has one (`StartGame.OnDemoHuntClick`).

### Existing systems to reuse

- XR rig: XRI Starter Assets `XR Origin (XR Rig)` prefab, used in Hawkin City.
- Weapon: `VRGun.cs` has trigger fire, magazine, reload, muzzle flash, haptics, ammo HUD and hand swapping. It has no recoil.
- Support scripts: `PlayerHealth`, `GameFx`, `GameHUD`, `ScoreSystem`, `Atmosphere`, `SceneFadeIn`.
- Bullet prefab, Demogorgon prefab, RedGroundMist and bat prefabs.

### Code issues that affect Part 2

- `Bullet.cs` checks for concrete types (`DemogorgonHealth`, `BatFlyer`, `UpsideDownGate`, `Pickup`). New enemies need a shared damage interface.
- `VRGun.Shoot` calls `Instantiate` for each bullet, and `Bullet` calls `Destroy`. This needs a pool.
- `VRGun` comments and the `XRI LeftHand/Shoot` fallback indicate that the gun starts in the left hand. The brief wants the right hand. Confirm in the scene.
- `PlayerHealth.TakeDamage` needs `UIManager.Instance`, `lowHealth` and `heartBeat`, with no null checks. It fails in a scene without them.
- `UIManager.OnReplayBtnClick` hardcodes the scene `Hawkin City`.

### Project settings (Mobile_RPAsset, OpenXR Android)

- HDR is off, MSAA is 2x, render scale is 0.8, main light shadows are on at 1024.
- `m_AdditionalLightsRenderingMode: 0` disables additional lights, so point and spot lights will not light surfaces. Lamps need baked light, emissive materials, or a settings change.
- The Android OpenXR foveated rendering feature is not enabled, and symmetric projection is off. Both are candidates for a measured test.
- Android build: IL2CPP, min SDK 25, target SDK 34, OpenXR loader.

### Downloaded assets (`Assets/Save The Hawkins/Vecna's Curse/`)

Triangle counts come from parsing the FBX files.

| Asset | Triangles | Textures | Notes |
| --- | --- | --- | --- |
| Vecna rigged | 106,772 | 2048 | 2 meshes, 223 bones, blend shapes, no animation clips |
| Haunted Creel House | 215,335 | 32 at 4096, 2 at 3840x2160 (360 MB) | 91 meshes, 11 materials; interior not yet verified |
| Grandfather Clock | 40,722 | none | 1 mesh, no materials, contains stray camera and light |
| Radio and Cassette Player | 11,215 | up to 1500x1600 | "sony" in filename |
| CRT Television | 3,812 | up to 2000 | usable |
| Locked door | 1,580 | 1024 | usable |
| Wall clock | 798 | 2048 | usable |
| Flashlight | 688 | 1024 | usable |

- All FBX files use default import settings.
- No license file is present for any download.
- Duplicate source files sit in `Assets/`: zip archives, `hauntedhouse.blend`, `hauntedhouse.obj`, and duplicate texture sets.
- `.DS_Store` files are also in these folders.
- Hawkin City assets (American house, road, pine tree, police car, school bus, damaged wall) can be reused for the Hawkins area after a triangle check. Two source FBX files exceed 60 MB (`basilica2.fbx`, `pine5.fbx`), so the pine tree needs a check before reuse.

### Repository state

- 399 tracked files show as deleted and `Assets/Save The Hawkins/` is untracked. This is the folder move, not yet committed.
- `.gitattributes` sends fbx, obj, zip, png and jpg to Git LFS. It does not cover `.jpeg` or `.blend`.

### Legal risk to resolve before store submission

- The Vecna character, Hawkins, Creel House and the "Stranger Things" name belong to their rights holders.
- `Assets/Audio/stranger_things.mp3` and `Assets/Audio/vecna_s_theme (1).mp3` appear to conflict with the brief's rule against unauthorized soundtrack recordings. Their origin is not verified.
- The downloaded Vecna model appears to be based on a copyrighted character, with no license on file.

## Open decisions (owner)

Items 1 and 2 used recommended defaults in Session 2: editor-script builders, scene renamed to `Vecna Curse.unity`.

1. Commit the folder move and Milestone 1 work.
2. Use of the Vecna model and the two audio files.
3. Removal of duplicate source files from `Assets/`.
4. Install Blender for mesh reduction.

## Proposed milestones (each about 5%)

1. Done: scene setup.
2. Done: small prop setup.
3. Done: Creel House exterior model.
4. Done: Hawkins terrain, sky, fog, street.
5. Hawkins set dressing: cars, bicycles, poles and wires, debris, posters, ash particles.
6. Creel House grounds and walkable porch.
7. Creel House interior rooms, as a separate modular set.
8. Upside Down environment and sky.
9. Mindscape arena and sky.

Later phases follow the master brief: gameplay, enemies, VFX and audio, UI, optimization.

## Manual work still required

- Approve this audit and answer the open decisions.
- Check the license of each downloaded asset at its source.
- Install Blender if mesh reduction goes through Blender.
- Measure performance on a Quest 2. No device measurements exist for Part 2.

## Recommended next milestone

Milestone 5: Hawkins set dressing: cars, bicycles, poles and wires, debris, posters, ash particles.
