# Unity 6 migration

Validated in the existing Unity 6.3 LTS editor, version **6000.3.24f1**, with
URP **17.3.0** on Direct3D 11. Scene: `Assets/scenes/demo_Island.unity`.

## Changes

- Updated Graphics Tests Framework to 8.14.0-exp.2 and stopped compiling the
  framework package's own tests as project tests.
- Removed `USE_CUSTOM_METADATA`, which referenced an unavailable internal test
  metadata package.
- Embedded Gameplay Ingredients 2020.2.10 with fixes for the Discover window's
  serialized auto-property and the Unity 6 horizontal Scene view overlay API.
  Its original asset GUIDs and runtime behavior are preserved.
- Added native Render Graph passes for the water-effects map and underwater
  caustics. Color effects skip the depth-only shoreline camera.
- Moved global post-processing volume setup out of the old render pass, with
  bounded quality-profile indexing and lifecycle cleanup.
- Updated the water shader's `DirectBRDF` call for URP 17.
- Deferred shoreline depth capture until scene initialization is complete and
  changed it to a supported single-camera render request.
- Made buoyancy job initialization/cleanup repeatable and guarded editor updates
  against uninitialized native arrays.
- Applied Unity's API updater for Rigidbody and quality-setting API changes.
- Accepted the editor's URP material and TextMesh Pro Essentials upgrades;
  repaired three leftover TMP shaders' references to the relocated includes.
- Enabled depth/stencil buffers and explicitly disabled Render Graph
  compatibility mode in the migrated global settings.

The project already contained user changes when this migration started. These
were preserved; the overall Git diff also includes those pre-existing changes
and Unity's generated asset upgrades.

## Validation

The scene was opened and run through the editor UI. A fresh Play session at
1920 x 1080, Low quality, was observed for at least 60 seconds. Three samples at
20-second intervals confirmed:

- Render Graph enabled and the race started.
- Four boats, with changing positions between samples.
- Zero captured errors, exceptions, or assertions, including startup.
- Zero missing MonoBehaviour scripts in the scene.
- Zero scene renderer materials with unsupported or failed shaders.
- Visible island geometry, lighting, clouds, reflective animated water, wakes,
  spray, and readable race HUD.

Reports and full-resolution Game-view captures are in `Logs/Unity6Validation/`.
The editor helper `Assets/Scripts/Editor/Unity6Validation.cs` repeats those three
samples when this scene enters Play mode. A manual report is available under
**Tools > Boat Attack > Write Unity 6 Validation Report**.

Nonfatal legacy-package deprecation and duplicate NavMesh-menu warnings remain.
Gameplay Ingredients also warns about managers intentionally excluded by this
project's settings. No standalone player build or other platform was validated.
