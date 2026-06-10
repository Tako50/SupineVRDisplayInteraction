# SupineVRDisplayInteraction

仰臥位VRにおける2D仮想ディスプレイ操作を検討するための研究用プロトタイプです。

This repository is currently private and under active development.
No license is granted at this stage.

## Phase 1 Prototype

The first working prototype is implemented in `Assets/Scenes/Dev_Prototype.unity`.

It is being built in phases from `Docs/prototype_spec.md`:

- `RaycastBaseline`: standard controller raycasting. The foremost display hit by the controller ray is the active operation target.
- `ExplicitDisplayFocus`: gaze candidate selection, grip-confirmed display focus, cursor warp, stick cursor movement, and trigger+stick scrolling.

The scene contains:

- `ExperimentManager`
- `PrototypeInputManager`
- `GazeProvider`
- `DisplaySurface`
- `DisplayManager`
- `DisplayLayoutManager`
- `Logger`
- supporting scripts for raycast pointing, baseline click/scroll logging, and debug visualization
- `FocusManager`
- `VirtualCursorController`
- `ClickDispatcher`
- `ScrollController`
- two World Space Canvas displays:
  - `Display_A_Front`: near, lower, smaller
  - `Display_B_Back`: far, upper, larger
- transparent `TransparentHitPlane` colliders on both displays
- layout presets: `NoOcclusion`, `PartialOcclusion`, `StrongOcclusion`
  - `NoOcclusion`: vertical placement, no depth difference, no rotation offset
  - `PartialOcclusion`: vertical placement, slight depth difference, no rotation offset
  - `StrongOcclusion`: front/back occlusion placement for the main ray-blocking condition

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
- `Tab`: switch between `RaycastBaseline` and `ExplicitDisplayFocus`
- `G`: grip fallback for focus confirmation
- `Left Shift`: trigger fallback for ExplicitDisplayFocus selection and trigger+stick scrolling
- `R`: clear the focused display
- right stick vertical, or `W` / `S`: scroll the currently ray-hit display in `RaycastBaseline`

## Phase 2 RaycastBaseline

- the right controller ray uses a single `Physics.Raycast`
- only the first `DisplaySurface` hit receives input
- the cursor follows the ray hit position on the hit display
- A button / right trigger release / `Space` / `Left Shift` release clicks at the ray-hit position
- right stick vertical / `W` / `S` scrolls only the currently hit display
- `StrongOcclusion` preserves input-ray occlusion, so the front display blocks the back display when it is hit first

## Phase 3 ExplicitDisplayFocus MVP

`ExplicitDisplayFocus` is implemented as the first proposed-method MVP:

- gaze uses `RaycastAll`-style candidate detection through `DisplayManager.GetDisplayHitsAll`
- gaze candidates are sorted by hit distance
- grip / `G` confirms the current candidate as the focused display
- when multiple candidates are under gaze, the nearest candidate is selected for this MVP
- focus does not change from gaze movement alone
- focus remains locked until grip is pressed again on another candidate
- cursor warps to the gaze hit position when focus is confirmed
- right stick / `WASD` moves the virtual cursor inside the focused display
- A button / right trigger release, or `Space` / `Left Shift` release, clicks at the virtual cursor on the focused display
- right trigger + right stick vertical, or `Left Shift` + `W` / `S`, scrolls the focused display
- if trigger+stick scrolling occurred during a trigger press, releasing the trigger does not also click
- moving gaze to another display after focus is locked does not redirect click or scroll input

The debug overlay shows the current condition, focus state, focused display, and gaze candidate ids. Candidate and focus visuals are intentionally simple: candidate displays are highlighted, overlapping gaze candidates make the nearest display semi-transparent, and the focused display is highlighted.

## Phase 3.5 Editor Validation

`Dev_Prototype` includes editor-only validation helpers for testing `ExplicitDisplayFocus` without Quest hardware:

- `EditorDebugInputProvider` keeps keyboard input separate from the real XR input path.
- `GazeProvider` has `DebugCameraForward` for driving the same gaze candidate path from the Main Camera in Play Mode.
- The debug overlay shows condition, gaze source, focus state, focused display, gaze candidates, cursor position, layout, debug input state, last click result, and last scroll amount.
- `FocusManager` exposes safe debug utilities: `ClearFocus`, `ForceRefocusFromCurrentGazeCandidate`, `GetFocusedDisplayId`, `GetCurrentCandidateIds`, and `GetFocusState`.

For editor validation:

1. Open `Assets/Scenes/Dev_Prototype.unity`.
2. Press Play.
3. Use `Tab` to switch to `ExplicitDisplayFocus`.
4. Aim the Game view / Main Camera at a display.
5. Press `G` to confirm focus and warp the cursor.
6. Use `WASD` or arrow keys to move the focused cursor.
7. Press `Space` or tap and release `Left Shift` to click.
8. Hold `Left Shift` and press `W` / `S` to scroll the focused display.

## Phase 4 FocusPointing Task Skeleton

`Dev_Prototype` now includes the first task layer for T1 display selection + target selection:

- `FocusPointingTaskManager` manages the target-selection trial list.
- `Display A` and `Display B` are assignable from the Inspector.
- Main-task trials are generated as 2 conditions x 80 trials = 160 trials total.
- Each condition uses the same 80-trial set: `Front` 40 trials, `BackClear` 20 trials, and `BackOccluded` 20 trials.
- `BackOccluded` trials place targets on the back display in the lower/central region intended to remain visually visible while being difficult for controller raycasting because the front display hit plane can occlude the ray.
- Starting a task runs only the currently selected condition block. After that 80-trial block finishes, the task returns to condition selection instead of automatically continuing to the next condition.
- Trial rows include `trialSetId`, `trialIndexInSet`, and `occlusion_type` so the matching RaycastBaseline and ExplicitDisplayFocus trials can be compared directly.
- Target positions for the experiment set are configured on `FocusPointingTaskManager` as Front, BackClear, and BackOccluded normalized position lists.
- Condition order is selectable from the Inspector as `AThenB` or `BThenA`.
- Trial order is selectable from the VR menu as `Fixed Order` or `Random Order`.
- Random order uses the Inspector-configurable seed and applies the same shuffled 80-trial sequence to both interaction conditions.
- T1 result CSVs include `trialOrder` and `randomSeed` for reproducibility.
- `ConditionA` maps to `RaycastBaseline` by default, and `ConditionB` maps to `ExplicitDisplayFocus` by default.
- A visible `FocusPointingTarget` is generated on the active target display.
- Starting Training or Main Task first shows a centered `START` button on `Display_B_Back`; pressing it starts a 3-second countdown before the first target appears.
- `ClickDispatcher` reports click attempts to the task layer for both `RaycastBaseline` and `ExplicitDisplayFocus`.
- `ErrorEvaluator` classifies clicks as `Correct`, `DisplayError`, `TargetError`, or `Miss`.
- `Logger` writes `TrialResult` rows with participant/session placeholders, target info, click info, result flags, start time, click time, and completion time.
- A compatibility target-selection CSV is written to `Application.persistentDataPath/Logs/target_selection_*.csv` with correct target selections.
- The analysis-oriented T1 CSV is written to `Application.persistentDataPath/Logs/t1_results_*.csv`. It records every click attempt, including `Correct`, `DisplayError`, `TargetError`, and `Miss`, with participant/session ids, task phase, condition, layout, trial-set ids, occlusion type, target position, clicked position, attempt index, response time, and analysis flags.

This does not implement T2-B seek-bar adjustment, questionnaires, or full participant-flow screens.

## Phase 5 T2-A Reference List Task MVP

`Dev_Prototype` includes an MVP of the updated T2-A task:

- Select `T2-A Reference List` from the VR task menu, then choose Training or Main Task.
- One display shows the requested item id while the other shows a 40-item scrollable list.
- The list and instruction displays alternate between `Display_A_Front` and `Display_B_Back`.
- Training uses four looping trials by default.
- Main Task uses five trials per list display, for ten trials total. These counts are provisional until the experiment design is fixed.
- `RaycastBaseline` scrolls the display currently hit by the controller ray.
- `ExplicitDisplayFocus` uses trigger + stick to scroll the focused display and clicks at the display-local virtual cursor.
- Click attempts are classified as `Correct`, `DisplayError`, `TargetError`, or `Miss`.
- Scroll gestures on the non-target display are counted as wrong-display scrolls.
- Correct selection advances the trial; incorrect attempts remain in the current trial.

`ReferenceListTaskManager` writes:

```text
Application.persistentDataPath/Logs/t2a_results_*.csv
Application.persistentDataPath/Logs/t2a_events_*.csv
```

The result CSV records every click attempt and trial-level wrong-display scroll counts. The event CSV records trial starts and aggregated scroll gestures.

## T1 Ray Occlusion Layout Probe

`T1RayOcclusionLayoutProbe` on `Prototype_Managers` helps tune the T1 front/back display geometry:

- `Search Best T1 Ray Occlusion Layout` scans `h1`, `h2`, and `eta2Degrees` for a layout close to the target ray-occlusion ratio.
- `Search Best And Apply To Displays` searches and applies the best D1/D2 pose and angular-size-derived physical size to `Display_B_Back` and `Display_A_Front`.
- `Apply Current T1 Layout To Displays` applies the current Inspector values.
- The key log fields are `projectedOcclusionRatio`, `visualOcclusionRatio`, `visualOcclusionClear`, and `nearTarget30Percent`.

Use the context menu after entering Play Mode if `DisplayLayoutManager` has just applied its normal startup layout.

`GazeProvider` supports `HmdForward` for development only and an `EyeTracking` placeholder source for a later Meta Quest Pro eye-tracking adapter. HMD forward should not be used for experiments.

## Quest Pro Eye Tracking

The first OpenXR eye-gaze adapter is included as `EyeTrackingRayAdapter`.

To use real Quest Pro gaze:

1. In Unity, confirm `Project Settings > XR Plug-in Management > OpenXR` has `Eye Gaze Interaction Profile` enabled for Android.
2. Build and run on Meta Quest Pro.
3. In `Dev_Prototype`, set `Prototype_Managers > GazeProvider > Gaze Source` to `EyeTracking`.
4. Keep `Eye Tracking Adapter` assigned to the `EyeTrackingRayAdapter` on `Prototype_Managers`.
5. Check the debug overlay or logs. `CurrentGazeSource` should report `EyeTracking` only when an `EyeGazeDevice` is tracked.

If the OpenXR eye-gaze device is not available or not tracked, `GazeProvider` logs a warning and falls back to `HmdForward` for development only.

CSV logs are written under `Application.persistentDataPath/Logs` with a `prototype_*.csv` filename.

## Pulling Quest Logs To Mac

When the app runs on Quest, `Application.persistentDataPath` is on the headset, not in the Unity project folder. Use these helper scripts to copy logs into the local project.

Manual copy:

```bash
Tools/pull_quest_logs.sh --open
```

This copies Quest logs from:

```text
/sdcard/Android/data/com.DefaultCompany.SupineVRDisplayInteraction/files/Logs
```

to:

```text
Logs/Quest
```

and opens the folder in Finder.

For a lightweight auto-copy workflow, start this watcher before connecting the Quest:

```bash
Tools/watch_quest_logs.sh
```

It waits for an adb device, copies logs to `Logs/Quest`, opens Finder, then waits for the next reconnect. Stop it with `Ctrl+C`.

## Analyzing T1 Results

After pulling Quest logs to `Logs/Quest`, run:

```bash
Tools/analyze_t1_results.py Logs/Quest --out Analysis/T1
```

The analyzer uses only Python's standard library and writes:

- `Analysis/T1/cleaned_attempts.csv`: all T1 click attempts.
- `Analysis/T1/cleaned_completed_trials.csv`: attempts that advanced the trial.
- `Analysis/T1/summary_by_condition.csv`: accuracy, error rates, attempts per completed trial, and response-time summaries per condition.
- `Analysis/T1/summary_by_condition_display.csv`: the same metrics split by target display.
- `Analysis/T1/summary_by_condition_occlusion.csv`: the same metrics split by `occlusion_type` values `Front`, `BackClear`, and `BackOccluded`.

By default it analyzes `MainTask` rows only. To include training rows too:

```bash
Tools/analyze_t1_results.py Logs/Quest --phase all --out Analysis/T1_All
```

To rebuild the development scene wiring, run Unity menu item `Prototype > Build Dev Prototype Scene`.

## Study 1 Layout Preference Study

Study 1 is an independent scene for checking layout preference in supine VR. It is not a raycasting, clicking, scrolling, gaze, or display-focus task. Participants only observe two 16:9 virtual displays in three layout candidates and answer the questionnaire outside Unity.

Build the scene from the Unity menu:

```text
Prototype > Build Layout Preference Study Scene
```

Then run:

1. Open `Assets/Scenes/LayoutPreferenceStudy.unity`.
2. Press Play.
3. Observe each layout without interacting with the displays.

Controls:

- A / RightArrow / Space: next layout
- B / LeftArrow / Backspace: previous layout
- R: recenter the current layout from the current HMD pose
- Hold Menu: recenter on Quest, if the runtime exposes the menu button through Unity XR input

The scene shows:

- `LeftRight`: two displays placed left and right at the same depth
- `UpDown`: two displays placed lower and upper at the same depth
- `UpDownDepth`: lower/front and upper/back displays with equal apparent angular size compensation

Per-condition placement can be tuned in `LayoutPreferenceStudy_Root > Study_Managers > LayoutPreferenceStudyManager > Layouts`. Each layout has `Condition Center Offset Meters` plus Display A/B `Distance Meters`, `Horizontal Angle Degrees`, and `Vertical Angle Degrees`. Display width is computed from a fixed 40-degree apparent width, and the physical display size preserves a 16:9 aspect ratio by default so displays at different distances keep the same apparent scale.

The Study 1 scene uses only the existing `DisplaySurface` display representation plus new `LayoutPreference*` scripts. It does not include `RaycastPointer`, `FocusManager`, `ClickDispatcher`, `ScrollController`, `FocusPointingTaskManager`, controller rays, gaze rays, hit selection, or task targets.

Study 1 logs are written to:

```text
Application.persistentDataPath/Logs/layout_preference_*.csv
```

Rows are written when a condition is entered, left, or recentered. The CSV records participant/session ids, condition order, display poses, display sizes, and the HMD anchor pose used for the layout.
