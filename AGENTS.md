# AGENTS.md

## Project
This repository is a Unity VR research prototype for studying **Explicit Display Focus** in lying-down VR.

The research theme is:

> Input focus management for multiple 2D virtual displays in VR while lying down.

The prototype compares conventional controller raycasting with a proposed method that explicitly manages which virtual display receives input.

## Source of truth
Read and follow these files before making changes:

1. `Docs/prototype_spec.md`
2. `Docs/implementation_plan.md`

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
- Grip button confirms the focused display.
- Focus confirmation also warps the virtual cursor to the gaze hit position.
- Gaze alone must not change focus.
- Stick moves the virtual cursor inside the focused display.
- A button clicks at the virtual cursor.
- Trigger + stick scrolls the focused display.
- The focused display remains the input target until grip is pressed again.

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
2. `Calibration_Debug`
3. `Exp_FocusPointing`
4. `Exp_FocusScroll`
5. `Exp_AttentionFocus`

`Dev_Prototype` is the first priority and should be kept usable throughout development.

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
