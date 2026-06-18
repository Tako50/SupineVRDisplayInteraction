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
- display-facing `A Front` / `B Back` captions are hidden; the internal object ids remain unchanged for task configuration and logs
- angle-based layout presets: `UpDownDepth`, `LeftRight`, and `UpDown`
  - `UpDownDepth`: Layout 1. The existing T1 ray-occlusion geometry.
  - `LeftRight`: Layout 2. Initial left/right values that can be tuned in the Inspector.
  - `UpDown`: Layout 3. Initial upper/lower values that can be tuned in the Inspector.

Display placement is calculated from the HMD pose when the experiment starts. Displays are fixed during play by default, so participants cannot freely move them.

## Running Dev_Prototype

1. Open `Assets/Scenes/Dev_Prototype.unity`.
2. Press Play.
3. Use the Scene/Game view debug overlay and console logs to inspect controller-ray hits, normalized display coordinates, click events, and scroll events.

Useful development controls:

- `1`: apply Layout 1 `UpDownDepth`
- `2`: apply Layout 2 `LeftRight`
- `3`: apply Layout 3 `UpDown`
- `WASD` or arrow keys: stick fallback for cursor movement
- Release `Space`: submit/click fallback
- `Tab`: switch between `RaycastBaseline` and `ExplicitDisplayFocus`
- `G`: grip fallback for focus confirmation
- `Left Shift`: trigger fallback for ExplicitDisplayFocus selection and trigger+stick scrolling
- `R`: clear the focused display
- right stick vertical, or `W` / `S`: scroll the currently ray-hit display in `RaycastBaseline`

## Phase 2 RaycastBaseline

- the right controller ray uses a single `Physics.Raycast`
- only the first `DisplaySurface` hit receives input
- the cursor follows the ray hit position on the hit display
- Releasing A / right trigger / `Space` / `Left Shift` clicks at the ray-hit position
- right stick vertical / `W` / `S` scrolls only the currently hit display
- `UpDownDepth` preserves the existing T1 front/back ray-occlusion geometry

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
- Releasing A / right trigger / `Space` / `Left Shift` clicks at the virtual cursor on the focused display
- right trigger + right stick vertical, or `Left Shift` + `W` / `S`, scrolls the focused display
- if trigger+stick scrolling occurred during a trigger press, releasing the trigger does not also click
- moving gaze to another display after focus is locked does not redirect click or scroll input

The debug overlay shows the current condition, focus state, focused display, and gaze candidate ids. Gaze highlight is managed separately from interaction focus by `GazeDisplayFocusManager`.

## Independent Gaze Highlight Conditions

`GazeDisplayFocusManager` highlights the current pointing target as visual feedback only:

- `RaycastBaseline`: the first display hit by the controller Ray
- `ExplicitDisplayFocus`: the foremost display under the gaze ray

It does not change the input target, explicit input focus, cursor, click, or scroll destination.

The existing `InteractionCondition` and the independent `highlightEnabled` flag form these four experiment conditions:

- `RaycastBaseline` + highlight OFF
- `RaycastBaseline` + highlight ON
- `ExplicitDisplayFocus` + highlight OFF
- `ExplicitDisplayFocus` + highlight ON

At the start of a condition, call either:

```csharp
gazeDisplayFocusManager.SetHighlightEnabled(true);
experimentManager.SetInteractionAndHighlight(
    InteractionCondition.RaycastBaseline,
    false);
```

In the in-app task menu, select `Highlight OFF` or `Highlight ON` before starting Training or Main. The selected state is displayed in the menu status card and is applied independently of the interaction method.

In `Dev_Prototype`, attach `GazeDisplayFocusManager` to `Prototype_Managers` and assign `PrototypeInputManager`, `GazeProvider`, `DisplayManager`, and `EditorDebugInputProvider`. `Highlight Enabled` controls the initial state, `Debug Toggle With Keyboard` enables the Editor-only `H` shortcut, and `Log State Changes` logs only target or enabled-state changes. Each display uses its existing `DisplaySurface`; its `Visible Panel` must reference the panel `Image`.

Turning highlight OFF immediately calls `SetFocused(false)` for every `DisplaySurface`. The display is a uGUI `Image`, so the highlight uses `Image.color` rather than changing a shared material; `MaterialPropertyBlock` is not applicable to this UI component.

## Per-Condition Controller Ray Display Settings

The visible controller Ray is configured per experiment condition, independently from the cursor:

- `Ray OFF` / `Ray ON`: hide or show the controller Ray visual.
- `Short` / `Medium` / `Long`: choose the visible Ray length for the currently selected method + highlight condition.

This is a visual setting only. `RaycastBaseline` still uses the existing controller-ray hit test for cursor position, clicking, scrolling, and occlusion reproduction. When the interaction method is `ExplicitDisplayFocus`, the controller Ray is not used for display selection.

`ExperimentManager` has four Inspector settings:

- `Raycast Highlight Off Ray`
- `Raycast Highlight On Ray`
- `Proposed Highlight Off Ray`
- `Proposed Highlight On Ray`

Each setting has its own `Ray Visual Enabled` and `Ray Length Level`, so the four experiment conditions can use different Ray lengths. In the in-app task menu, the `CONDITION RAY` buttons edit only the currently selected method + highlight condition.

External scripts can apply the same settings through:

```csharp
experimentManager.SetRayVisualEnabled(true);
experimentManager.SetRayLengthLevel(RayVisualLengthLevel.Medium);
experimentManager.SetRayVisualSettings(true, RayVisualLengthLevel.Long);
```

The default visible lengths are configured on `RaycastPointer`: Short `1.5m`, Medium `2.5m`, and Long `4.0m`.

## Optional WebView Probe

`WebViewDisplayBridge` is an optional probe for T3-style free browsing sessions. It lets the existing `DisplaySurface` click and scroll coordinates drive a Vuplex `WebViewPrefab` when the Vuplex package is imported.

The repository does not include Vuplex or any paid/trial WebView binaries. To test real browsing:

1. Download the Vuplex `3D WebView for Android` trial or package.
2. Import it into the Unity project locally.
3. Add `WebViewDisplayBridge` to a `DisplaySurface` GameObject, such as `Display_B_Back`.
4. Set `Enable On Start` to true and set `Initial Url` to a test page such as `https://www.youtube.com`.
5. Build to Quest Pro. Android WebView rendering must be verified on device; without the Vuplex package the bridge logs that WebView is not found and the project still compiles.

When `Consume Experiment Input` is enabled, the current experiment input path is reused:

- `RaycastBaseline`: controller-ray hit position clicks or scrolls the WebView.
- `ExplicitDisplayFocus`: the focused display's virtual cursor clicks or scrolls the WebView.

This keeps WebView browsing independent from the interaction method. Do not commit Vuplex plugin files or trial binaries to the public repository.

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
- Main-task trials are loaded from `Assets/Resources/T1/target_orders_ABCDE.csv`.
- Lists `A` through `D` each contain 108 main trials: 2 displays x 9 positions x 3 sizes x 2 cycles.
- List `E` contains 108 training trials.
- The current development configuration assigns List `A` to both interaction conditions.
- `Condition A Main Order` and `Condition B Main Order` are separate Inspector fields so lists `A` through `D` can be assigned per condition without changing task code.
- Starting a task runs only the currently selected condition block. After its 108-trial main block finishes, the task returns to condition selection instead of automatically continuing to the next condition.
- Training uses all 108 trials from List `E` and loops after trial 108 until the experimenter ends training.
- Target positions use the normalized 3 x 3 grid at x/y values `0.1`, `0.5`, and `0.9`.
- Back-display positions on the lower row (`y = 0.1`) are logged as `BackOccluded`; the other back-display positions are `BackClear`.
- Target sizes `Small`, `Medium`, and `Large` are rendered at approximately 1, 2, and 3 degrees based on the current head-to-display-center distance.
- Trial rows include the source list, cycle, position id, size, repetition, and transition metadata from the generated order CSV.
- Condition order is selectable from the Inspector as `AThenB` or `BThenA`.
- The VR menu shows the configured main/training list for the selected interaction condition; ad hoc Fixed/Random reshuffling is disabled.
- T1 result CSVs retain `trialOrder` and `randomSeed` compatibility columns and also record `targetOrderList`.
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
- As in T1, the task shows a centered `START` button on `Display_B_Back`; pressing it starts a 3-second countdown before the first trial appears.
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

- The current `h1=1.90`, `h2=1.15`, `eta2=25 degrees` geometry is Layout 1 (`UpDownDepth`).
- `Search Best T1 Ray Occlusion Layout` scans `h1`, `h2`, and `eta2Degrees` for a layout close to the target ray-occlusion ratio.
- `Search Best And Apply To Displays` searches and applies the best D1/D2 pose and angular-size-derived physical size to `Display_B_Back` and `Display_A_Front`.
- `Apply Current T1 Layout To Displays` always applies the current Inspector values for manual tuning and warns when the experiment validation targets are not met.
- The key log fields are `projectedOcclusionRatio`, `visualOcclusionRatio`, `visualOcclusionClear`, and `nearTarget30Percent`.

Use the context menu after entering Play Mode if `DisplayLayoutManager` has just applied its normal startup layout.

## Dev Task Display Layouts

Edit the three normal task layouts on `Prototype_Managers > DisplayLayoutManager`.

Each display has:

- `Distance Meters`: distance from the HMD anchor.
- `Horizontal Angle Degrees`: negative is left, positive is right.
- `Vertical Angle Degrees`: negative is lower, positive is upper.
- `Rotation Offset Degrees`: optional pitch, yaw, and roll adjustment.

`Apparent Width Degrees` and `Apparent Height Degrees` control the shared visual angle. Physical display size is recalculated from each display's distance, so layouts at different depths keep approximately the same apparent size. `Preserve Display Aspect Ratio` is initially off so Layout 1 exactly matches the previous T1 probe's independent `40 x 22.5 degree` sizing.

`DisplaySurface` applies the calculated physical size to both the visible World Space Canvas and its transparent HitPlane. The rendered display boundary and ray-hit boundary therefore remain aligned when distance or apparent size changes.

Shared apparent-size, HMD-anchor, angular-pose, and display-coordinate calculations live in `DisplayGeometry` and `DisplaySurface` so task and study scenes use the same geometry rules.

Use `DisplayLayoutManager > Apply Selected Layout Now` from the component context menu to preview the selected `Initial Preset` outside Play Mode.

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

- Release A / RightArrow / Space: next layout
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
