# Project Overview
- **Game Title:** `UnityJamTest` (current Unity product name); potion-workshop gameplay context.
- **High-Level Concept:** Dress the existing first-person potion workshop with textured toon mushrooms, ivy, and potion bottles. Add atmosphere without making decorative props compete with ingredient selection, brewing, or navigation.
- **Players:** Existing single-player first-person Workshop flow; no multiplayer changes.
- **Inspiration / Reference Games:** The project's existing toon shelf, cauldron, wall, and ivy materials are the visual reference. No new external art reference is required.
- **Tone / Art Direction:** Stylized fantasy workshop; supplied model textures, crisp shade bands, cool-toned shadows, and readable silhouettes matching existing `Toon/Toon` materials.
- **Target Platform:** Standalone Windows 64-bit; Unity `6000.3.28f1`.
- **Screen Orientation / Resolution:** Existing desktop configuration retained; current default player resolution is `1024x768`. No camera, UI scaling, or resolution changes.
- **Render Pipeline:** URP `17.3.0`, existing `PC_RPAsset`; Unity Toon Shader `0.16.1-preview`.

## Confirmed User Decisions
1. **Balanced scatter**, not dense overgrowth.
2. **Reuse the existing Toon shader**, including its transparent mode for bottle glass, before considering an additional shader.
3. **Critical: create reusable prefabs first; reuse linked prefab instances for all decorations.** Do not scatter raw FBX instances, unpacked model copies, or scene-only material overrides.
4. The user approved creating a new plan because no plan file existed under `Assets/Plans/`.
5. Keep verification minimal and focused; ask if a consequential fallback or scope change is needed.

# Game Mechanics
## Core Gameplay Loop
Preserve the existing loop: navigate the workshop, choose ingredients from shelves, carry/store them, and brew at the cauldron. Decorative models are visual-only and must not become pickup targets, cover ingredient slots, or obstruct the player.

From an MDA perspective, this change improves atmosphere while keeping gameplay affordances legible. Use clustered accents around edges rather than indiscriminate placement across the floor.

## Controls and Input Methods
Retain the existing New Input System, first-person controller, Cinemachine camera, arm animation, and independent held-item bob. Add no controls, runtime spawner, physics bodies, or interaction scripts. Keep decorative renderers outside the `Interactable` layer and do not add decoration colliders.

# UI
No UI changes. Existing HUD, prompts, crosshair, pocket display, and result panels remain unchanged.

Placement guide, not a replacement scene layout:

```text
                  Back wall / selected ivy patches
        mushroom accents                   mushroom accents
                  [Shelf 01]   [Shelf 02]
                   bottles on safe shelf tops

 ivy / mushrooms   [Shelf 03]   [Shelf 05]   ivy / mushrooms
                          [Cauldron]
                   [Shelf 04]   [Shelf 06]
                         clear routes

        mushroom accents    Player spawn    mushroom accents
                  Front wall / selected ivy patches
```

Keep central routes, ingredient-facing sides, cauldron access, and lantern visibility clear.

# Key Asset & Context
## Resume State Verified
- Active scene: `Assets/Scenes/02_Workshop.unity`, not dirty at inspection.
- No `Assets/Plans/` directory or prior decoration plan existed.
- No `Assets/Prefabs/Decorations/` or newly authored mushroom/bottle toon materials existed.
- Two original ivy model instances already exist: `HangingIvy` at approximately `(7.80, 0.08, 8.69)` and `IvyVine` at `(1.33, 1.75, 8.69)`. Both have existing ivy toon materials assigned.
- Earlier work completed discovery and preference selection, not decoration implementation.

## Existing Source Models — Read Only
All are under `Assets/Arts/Models/gg/`:
- `Mushrooms/PinkMushroom/PinkMushroom.fbx`
- `Mushrooms/GreenMushroom/GreenMushroom.fbx`
- `Mushrooms/SmallMushroom/SmallMushroom.fbx`
- `Ivy/IvyVine/IvyVine.fbx`
- `Ivy/HangingIvy/HangingIvy.fbx`
- `PotionBottle/PotionBottle/PotionBottle.fbx`

The bottle has separate `Potion Bottle Cork` and `Potion Bottle Glass` renderers. Imported models already include the FBX axis correction; mushroom and ivy model roots are approximately X = 270 degrees. Preserve this correction inside neutral wrapper prefabs rather than resetting model rotations.

Approximate imported upright sizes: Pink mushroom 0.20 m tall, Green 0.26 m, Small 0.10 m, bottle including cork 0.40 m, IvyVine 1 m, HangingIvy 2.8 m. Use actual renderer bounds when grounding or wall-mounting, not assumed pivots.

## New Prefabs — Must Exist Before Scatter
Create neutral, unit-scale wrapper prefabs with nested source-model references:
- `Assets/Prefabs/Decorations/PinkMushroom_Decoration.prefab`
- `Assets/Prefabs/Decorations/GreenMushroom_Decoration.prefab`
- `Assets/Prefabs/Decorations/SmallMushroom_Decoration.prefab`
- `Assets/Prefabs/Decorations/IvyVine_Decoration.prefab`
- `Assets/Prefabs/Decorations/HangingIvy_Decoration.prefab`
- `Assets/Prefabs/Decorations/PotionBottle_Decoration.prefab`

Each prefab stores renderer material assignments, imported model transforms, and visual-only configuration. Final scene instances reference these prefabs and shared materials. Placement overrides may contain only necessary transform adjustments; avoid material overrides and unlinked copies.

## New Materials
Use model-local material paths, consistent with the existing ivy materials:
- `Assets/Arts/Models/gg/Mushrooms/PinkMushroom/M_PinkMushroom_Toon.mat`
- `Assets/Arts/Models/gg/Mushrooms/GreenMushroom/M_GreenMushroom_Toon.mat`
- `Assets/Arts/Models/gg/Mushrooms/SmallMushroom/M_SmallMushroom_Toon.mat`
- `Assets/Arts/Models/gg/PotionBottle/PotionBottle/M_PotionCork_Toon.mat`
- `Assets/Arts/Models/gg/PotionBottle/PotionBottle/M_PotionGlass_Toon.mat`

Reuse, without globally restyling:
- `Assets/Arts/Models/gg/Ivy/IvyVine/M_Ivy_Toon.mat`
- `Assets/Arts/Models/gg/Ivy/HangingIvy/M_HangingIvy_Toon.mat`

Material reference: `Assets/Arts/Models/M_Shelf_Toon.mat`, with `Toon/Toon`, shade steps approximately `0.435` and `0.301`, and feather widths `0.0001`. Preserve its shade-band/color conventions when authoring mushroom/cork materials, but replace borrowed textures rather than inheriting unrelated wall maps.

For each mushroom and the cork, bind the matching supplied `*_BaseColor.png` and `*_Normal.png`. Populate applicable base/shade texture aliases consistently so no new material retains shelf/wall textures. Do not force PBR metallic-smoothness maps into unrelated Toon slots; use them only if an appropriate supported mask is needed and its channel meaning is verified.

The three mushroom normal textures currently import as `Default`, sRGB enabled. Correct only their importer metadata to `NormalMap`/linear before assigning them. Cork and ivy normals already import correctly. Do not alter source pixels or unrelated texture imports.

No glass-specific texture is supplied. Use a pale glass tint and the existing Toon transparency/highlight controls; do not assign the cork texture to glass.

## Verified Transparent Toon Support
The installed package implements transparent mode in `Packages/com.unity.toonshader/Editor/UTS3GUI.cs`, and the URP shader computes alpha from the clipping mask plus `_Tweak_transparency`.

Configure a new glass material using that supported mode: transparent rendering, correct clipping keywords and render queue, depth writing off, restrained toon highlights/rim, and no opaque outline artifacts. A white clipping mask with an initial transparency adjustment around `-0.7` is a starting point, not a guaranteed final look. Validate the result against the room and cork. Do not assume changing color alpha alone configures transparency.

Leave the existing `PotionGlass.mat` and `PotionGlass.shader` unchanged. No package source changes, feature-guard bypasses, additional packages, or new shader are planned. If the supported transparent Toon mode proves unsuitable, ask before switching to custom toon glass.

## Scene and Impact Scope
Modify only `Assets/Scenes/02_Workshop.unity` for placements. Existing source-model dependencies and scene references were inspected; the scene currently references the two ivy models/materials but not the new mushroom/bottle decoration assets.

Before implementation, recheck dependencies/dependents for any asset being modified, especially the three normal texture importers and existing ivy scene instances. Keep FBX files, model material remaps, shared reference materials, gameplay prefabs/scripts, render pipeline settings, lighting, and APV/light-bake data unchanged.

# Implementation Steps
## 1. Recheck Context and Create the Six Prefabs First
**Files/items:** all six new `Assets/Prefabs/Decorations/*_Decoration.prefab` files; existing source FBXs.
**Dependencies:** user approval of this plan; current scene and asset checks.
- Confirm paths, renderer names, existing output assets, and source model orientations before editing.
- If an output already exists after interruption, inspect and reuse it rather than blindly overwriting it.
- Create the six neutral wrapper prefabs in an isolated authoring context, retaining linked model children and their axis corrections.
- Make them visual-only: no pickup/ingredient components, rigidbodies, or colliders; use the existing non-interactable/default layer.
- Save each prefab and verify its source-model link.
- **Do not place any new decorative scene instances yet.** Prefab creation is the first asset-authoring stage and a hard gate for scattering.

## 2. Author the Matching Toon Materials
**Files/items:** five new materials listed above; importer metadata for the three mushroom normal PNGs.
**Dependencies:** Step 1; dependency/impact checks for changed normal importers.
- Correct the three mushroom normal imports in one batch.
- Create mushroom and cork materials from the existing Toon visual reference and bind their supplied base-color and normal textures.
- Replace/clear unrelated inherited texture references and validate shader keywords/settings using the installed package's supported material workflow.
- Create the transparent glass material using `Toon/Toon` transparent mode; configure alpha/clipping, queue/depth state, and modest highlights/rim.
- Keep the glass renderer from casting an inappropriate opaque shadow; retain normal opaque shadow behavior for cork and mushrooms.
- Reuse the two existing ivy materials. If a shared-material change appears necessary, assess its impact before changing it.

## 3. Store and Validate Final Assignments in the Prefabs
**Files/items:** the six decoration prefabs.
**Dependencies:** Steps 1–2.
- Assign matching mushroom materials, existing ivy materials, and separate cork/glass materials inside prefab assets.
- Save the completed prefabs and confirm there are no missing meshes, textures, or renderer materials.
- Check that transparent glass renders in the current URP setup and remains readable rather than opaque, invisible, or magenta.
- **Only after all six prefabs are saved and usable may the scene-scatter stage begin.**

## 4. Scatter Linked Prefab Instances in the Workshop
**Files/items:** `Assets/Scenes/02_Workshop.unity`; new `Environment/Decorations_Toon` hierarchy.
**Dependencies:** Step 3 complete; fresh scene support/exclusion bounds.
- Organize instances under `Mushrooms`, `Ivy`, and `PotionBottles` child groups.
- Use deterministic, lightly varied placement rather than a runtime scatter script. Adjust root transforms without unpacking prefabs.
- Balanced initial budget: **24 mushrooms**, **8 new ivy placements**, and **12 bottles**. The two existing ivy placements are retained visually, bringing ivy to approximately 10 total. Reduce counts if safe, uncluttered placement is unavailable rather than forcing overlaps.
- Mushrooms: approximately six small clusters near perimeter corners/wall bases, using all three models with restrained uniform scale and yaw variation. Keep spawn and central walking routes clear.
- Ivy: selected wall sections and upper wall/shelf edges, avoiding lantern faces and ingredient-facing areas. Account for the 2.8 m hanging model; orient/scale it to hang downward without clipping the floor or ceiling.
- Bottles: approximately two per ingredient-shelf top, using actual supporting surface height/bounds rather than occupied ingredient trays. Leave room between bottles, walls, and ceiling; do not use the empty PackagingTable/SubmissionCounter transforms as unsupported placement surfaces.
- Convert the two existing ivy placements to instances of the matching authored decoration prefabs, preserving their visible transform, material, and name. Inspect for custom components/children first; retain or ask about any user-authored extras instead of discarding them.
- Use bounds-based grounding and wall offsets to avoid floating props, buried stems, z-fighting, or intersecting bottles.
- Add no decoration interaction colliders. Keep clearance around shelves and the cauldron, and preserve the camera/player/held-item hierarchy.

## 5. Focused Review and Save
**Files/items:** new materials/prefabs and Workshop scene.
**Dependencies:** Step 4.
- Perform one consolidated asset/link validation and visual inspection from an overview and player-eye perspective.
- Make only necessary placement/material refinements; avoid repeated test harnesses or broad project changes.
- Save prefab/material assets and the scene, and report final counts and paths.
- Confirm no unlinked raw-model copies or per-instance material overrides were introduced.

# Verification & Testing
Keep testing minimal:
1. **Asset/prefab validation:** all six prefabs and five new materials exist; each prefab stores the expected renderer assignments; all final decoration objects are linked to the authored decoration prefabs; scene instances share materials.
2. **Texture/shader validation:** correct supplied base colors and normal maps, corrected mushroom normal import settings, no borrowed wall maps in new materials, and no relevant shader/import errors. Verify glass queue/alpha/depth behavior in URP.
3. **Single visual review:** inspect the overall scatter plus a player-eye view for consistent toon bands, readable transparent glass, visible ivy faces, grounded props, wall offsets, and unobstructed ingredient/cauldron access.
4. **Scope check:** no gameplay scripts/components, model remaps, lighting/APV settings, source shader/package files, or UI changed. No decoration colliders or Interactable-layer objects added.
5. **No automated Play Mode harness or lighting rebake by default.** Enter Play Mode only if a concrete issue cannot be checked in Edit Mode; ask before an unplanned custom-glass fallback or significant scope expansion.

## Acceptance Criteria
- Prefabs were created, completed, and saved before scattering.
- Mushrooms, ivy, and bottles are placed as reusable linked decoration-prefab instances.
- Mushroom/cork supplied textures render with the existing project Toon style; bottle glass uses the verified existing transparent Toon mode.
- Balanced decoration improves atmosphere without confusing or blocking gameplay.
- Existing ivy placements retain their appearance; all changes are saved and confined to the listed assets and Workshop scene.
