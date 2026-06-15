# AGENTS.md

## Project
This repository is a Unity VR research prototype for studying **Explicit Display Focus** in lying-down VR.

The research theme is:

> Input focus management for multiple 2D virtual displays in VR while lying down.

The prototype compares conventional controller raycasting with a proposed method that explicitly manages which virtual display receives input.

## Source of truth
Read and follow these files before making changes:

1. `Docs/experiment_design_sync.md`
2. `Docs/prototype_spec.md`
3. `Docs/implementation_plan.md`

`Docs/prototype_spec.md` and `Docs/implementation_plan.md` contain the initial
prototype design and may lag behind the current Notion experiment design.
When they conflict with `Docs/experiment_design_sync.md`, do not silently
implement the older behavior. Treat items marked as unresolved in the sync
document as design decisions that must be fixed before large implementation
changes.

Do not reinterpret the project as a generic gaze-pointing prototype. The central research concept is **explicit input focus management across multiple 2D virtual displays**.

## Target environment
- Unity 2022 LTS or Unity 2021 LTS
- Meta Quest Pro
- Meta XR SDK
- OpenXR
- XR Interaction Toolkit or Meta XR Interaction SDK
- TextMeshPro
- C# scripts

## Core interaction conditions
Implement and compare these conditions first:

### 1. RaycastBaseline
Standard VR controller raycasting.

- The right controller ray determines the input target.
- The first / closest hit display becomes the implicit input target.
- A button performs click.
- Right stick scrolls the display while the ray is hitting it.
- This condition should reproduce occlusion: a front display can block interaction with a rear display.

### 2. ExplicitDisplayFocus
Proposed method.

- Gaze ray obtains candidate displays.
- Gaze is used only during an explicit grip focus gesture.
- The focus gesture updates the focused display and warps the virtual cursor to the gaze hit position.
- Gaze alone, outside the focus gesture, must not change focus or cursor position.
- Stick moves the virtual cursor inside the focused display.
- A button clicks at the virtual cursor.
- Trigger + stick scrolls the focused display.
- The focused display remains the input target until the next explicit focus gesture.
- The exact focus commit timing (`grip down`, continuous `grip held`, or `grip release`) is currently unresolved. Follow `Docs/experiment_design_sync.md` and do not change it implicitly.

## Eye tracking policy
Use Meta Quest Pro Eye Tracking for experiment use.

`HmdForward` gaze may be implemented only as a development fallback and debugging mode. It must not be treated as the main experimental implementation.

The `GazeProvider` architecture must support both:

```csharp
public enum GazeSource
{
    HmdForward,
    EyeTracking
}
```

## Implementation rules
- Keep code simple, readable, and research-prototype oriented.
- Prefer inspector-configurable fields.
- Avoid unnecessary third-party dependencies.
- Do not implement a full browser, real video player, or polished product UI.
- Use pseudo 2D content sufficient for clicking, scrolling, and switching displays.
- Do not implement participant-controlled free display movement.
- Display placement must be fixed during experiments.
- Use CSV logs for events, trials, and frame-level samples where needed.

## Scene priorities
Implement in this order:

1. `Dev_Prototype`
2. `LayoutPreferenceStudy`
3. `Calibration_Debug`
4. T1 multiple-display target selection
5. T2-A reference-while-list-selection
6. T2-B seek-bar adjustment, if retained after design review

`Dev_Prototype` is the first priority and should be kept usable throughout development.
Do not create a standalone `Exp_AttentionFocus` scene unless the experiment
design explicitly restores it; the current design folds that question into T2-A.

## Required core scripts
Create or maintain these modules:

- `ExperimentManager`
- `InputManager`
- `GazeProvider`
- `RaycastPointer`
- `DisplaySurface`
- `DisplayManager`
- `DisplayLayoutManager`
- `FocusManager`
- `FocusStateTracker`
- `VirtualCursorController`
- `ClickDispatcher`
- `ScrollController`
- `TaskManager`
- `ErrorEvaluator`
- `Logger`

## Done criteria for each phase
Every phase is done only when:

- The Unity project compiles.
- The relevant scene runs in Play Mode.
- The behavior is documented in `README.md` or a phase note.
- Any new public fields are named clearly for inspector use.
- No unrelated refactoring or package changes are introduced.
