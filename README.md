# SupineVRDisplayInteraction

仰臥位VRにおける2D仮想ディスプレイ操作を検討するための研究用プロトタイプです。

This repository is currently private and under active development.
No license is granted at this stage.

## Phase 1 Prototype

The first working prototype is implemented in `Assets/Scenes/Dev_Prototype.unity`.

It is being built in phases from `Docs/prototype_spec.md`:

- `RaycastBaseline`: standard controller raycasting. The foremost display hit by the controller ray is the active operation target.
- `ExplicitDisplayFocus`: planned for a later phase. It remains disabled during Phase 2.

The scene contains:

- `ExperimentManager`
- `PrototypeInputManager`
- `GazeProvider`
- `DisplaySurface`
- `DisplayManager`
- `DisplayLayoutManager`
- `Logger`
- supporting scripts for raycast pointing, baseline click/scroll logging, and debug visualization
- two World Space Canvas displays:
  - `Display_A_Front`: near, lower, smaller
  - `Display_B_Back`: far, upper, larger
- transparent `TransparentHitPlane` colliders on both displays
- layout presets: `NoOcclusion`, `PartialOcclusion`, `StrongOcclusion`

Display placement is calculated from the HMD pose when the experiment starts. Displays are fixed during play by default, so participants cannot freely move them.

## Running Dev_Prototype

1. Open `Assets/Scenes/Dev_Prototype.unity`.
2. Press Play.
3. Use the Scene/Game view debug overlay and console logs to inspect controller-ray hits, normalized display coordinates, click events, and scroll events.

Useful development controls:

- `1`: apply `NoOcclusion`
- `2`: apply `PartialOcclusion`
- `3`: apply `StrongOcclusion`
- `WASD` or arrow keys: stick fallback for cursor movement
- `Space`: submit/click fallback
- right stick vertical, or `W` / `S`: scroll the currently ray-hit display in `RaycastBaseline`

Phase 2 implements `RaycastBaseline` only:

- the right controller ray uses a single `Physics.Raycast`
- only the first `DisplaySurface` hit receives input
- the cursor follows the ray hit position on the hit display
- A button / `Space` clicks the debug target on the hit display
- right stick vertical / `W` / `S` scrolls only the currently hit display
- `StrongOcclusion` preserves input-ray occlusion, so the front display blocks the back display when it is hit first

`GazeProvider` supports `HmdForward` for development only and an `EyeTracking` placeholder source for a later Meta Quest Pro eye-tracking adapter. HMD forward should not be used for experiments.

CSV logs are written under `Application.persistentDataPath/Logs` with a `prototype_phase1_*.csv` filename.

To rebuild the development scene wiring, run Unity menu item `Prototype > Build Dev Prototype Scene`.
