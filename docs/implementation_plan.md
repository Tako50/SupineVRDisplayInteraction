# Codex実装計画

このファイルは、Codexに段階的に実装させるための作業分解である。

`Docs/prototype_spec.md` を仕様の正とし、以下のPhase順に実装する。

---

## Phase 0: 仕様固定

### 目的

Notionのプロトタイプ設計をリポジトリ内に固定し、Codexが毎回同じ仕様を参照できるようにする。

### 置くファイル

```plain text
AGENTS.md
Docs/prototype_spec.md
Docs/implementation_plan.md
```

### Done

- Codexが `AGENTS.md` を読める
- `Docs/prototype_spec.md` が研究プロトタイプの仕様として存在する
- `Docs/implementation_plan.md` にPhase別の実装順が書かれている

---

## Phase 1: 2枚Display + Layout + Hit検出

### Codex依頼文

```text
Goal:
Implement Phase 1 of the Unity VR research prototype based on Docs/prototype_spec.md.

Context:
This is a research prototype for Explicit Display Focus in lying-down VR.
The core concept is explicit input focus management across multiple 2D virtual displays.
Do not implement a generic gaze pointer. Follow the display focus architecture in the spec.

Implement:
- Dev_Prototype scene
- Two World Space Canvas displays:
  - Display_A_Front: near, lower, smaller
  - Display_B_Back: far, upper, larger
- Transparent HitPlane for each display
- DisplaySurface component
- DisplayManager component
- DisplayLayoutManager component
- DisplayLayoutPreset enum:
  - NoOcclusion
  - PartialOcclusion
  - StrongOcclusion
- HMD-relative placement calculation
- Ray/display hit detection
- Conversion from world hit position to display-local normalized coordinates
- Debug visualization:
  - controller ray
  - gaze ray
  - hit point
  - currently hit display
- Logger skeleton

GazeProvider:
- Implement both HmdForward and EyeTracking modes in the architecture.
- HmdForward is development fallback only.
- EyeTracking may be a placeholder/adapter if Meta Quest Pro API integration is not available yet.

Constraints:
- Do not implement full browser or video playback.
- Do not implement participant-controlled display movement.
- Keep display placement fixed during experiments.
- Keep code simple and inspector-configurable.

Done when:
- Dev_Prototype runs in Play Mode.
- Two display panels appear in front of the user.
- Layout presets can be switched.
- Ray/display hit detection works.
- Display hit normalized coordinates are visible in debug output.
- README explains how to run the scene.
```

---

## Phase 2: RaycastBaseline

### Codex依頼文

```text
Goal:
Implement Phase 2: RaycastBaseline.

Follow Docs/prototype_spec.md.

Implement:
- RaycastPointer
- ClickDispatcher baseline path
- ScrollController baseline path

Requirements:
- Right controller ray selects the first DisplaySurface hit.
- The closest hit display becomes the implicit input target.
- Show cursor at the ray hit position on that display.
- A button clicks the target under the ray cursor. Right trigger alone never clicks.
- Right stick vertical scrolls the display only while the ray is hitting it.
- In StrongOcclusion layout, the front display should block interaction with the back display if the ray hits the front display first.
- Log click events, scroll events, hit display ID, and controller pose samples.

Done when:
- I can use controller raycasting to click and scroll both displays.
- Occlusion behavior is reproduced.
- CSV event logs are generated.
```

---

## Phase 3: ExplicitDisplayFocus

### Codex依頼文

```text
Goal:
Implement Phase 3: ExplicitDisplayFocus.

Follow Docs/prototype_spec.md.

Implement:
- FocusManager
- FocusStateTracker
- VirtualCursorController
- ClickDispatcher proposed path
- ScrollController proposed path

Requirements:
- Gaze ray retrieves display candidates.
- Grip button confirms the current gaze candidate as the focused display.
- Focus confirmation also warps the virtual cursor to the gaze hit position on that display.
- Gaze alone must not change focus.
- After focus is confirmed, stick movement controls the virtual cursor on the focused display.
- A button clicks at the virtual cursor position. Right trigger alone never clicks.
- Trigger release never clicks; trigger is reserved for modified operations such as scrolling.
- Trigger + stick vertical scrolls the focused display.
- The focused display remains the input target even if gaze moves to another display.
- Grip can be pressed again to refocus based on the current gaze candidate.
- When gaze hits overlapping displays, use RaycastAll-like candidate detection and visually indicate candidates.
- Optionally make the front display semi-transparent during overlap preview.

Done when:
- I can focus a display with gaze + grip.
- Cursor warps to gaze position.
- Stick moves the cursor.
- A button clicks; right trigger alone does not.
- Trigger + stick scrolls the focused display.
- Input focus remains locked until grip is pressed again.
```

---

## Phase 4: T1 FocusPointing task

### Codex依頼文

```text
Goal:
Implement Phase 4: T1 FocusPointing task.

Follow Docs/prototype_spec.md.

Implement:
- Exp_FocusPointing scene or task mode inside Dev_Prototype first
- TaskManager for pointing trials
- Task A: Left/Right, Task B: Front/Back Clear, Task C: Front/Back Input Occlusion
- 48 main trials per task/method block (2 displays x 6 positions x 2 sizes x 2 cycles)
- Constrained deterministic trial-order generation and counterbalance metadata
- Target generation on Display_A_Front and Display_B_Back
- Target size conditions
- Target display condition
- Trial start/end timing
- ErrorEvaluator for Display error and Target error
- Trial CSV logging
- Per-trial controller movement and rotation accumulation

Definitions:
- Display error: user input is sent to a display different from the instructed target display.
- Target error: user clicks the wrong target within the correct display.

Done when:
- A trial instructs the participant which display and target to click.
- Both RaycastBaseline and ExplicitDisplayFocus can run the same task.
- Completion time, Display error, Target error, focus switch count, and cursor warp count are logged.
```

---

## Phase 5: T2 Web Browsing task

### Codex依頼文

```text
Goal:
Implement Phase 5: T2 YouTube + comparison Web task.

Follow Docs/experiment_design_sync.md.

Implement:
- T2 Web Browsing mode inside Dev_Prototype
- Display_B_Back loads the YouTube page
- Display_A_Front loads the fixed local comparison page
- Unified practice steps: YouTube play, pause, skip forward by 10 seconds, seek backward with the seek bar, Web scroll, product detail, candidate add, candidate remove
- Keep content-set, year, item ID, and video timestamps internal; do not render them on the comparison Web
- Use the fixed category order from the current Notion T2 specification
- Use the Notion-specified video time ranges for V2D and category positions for D2V
- Continue from practice to Main without closing or reloading either WebView
- 10-minute unified countdown with V2D, D2V, semi-free, and finalization instructions
- Completion when the participant confirms 2–3 candidates on the comparison page
- YouTube operation prerequisite before candidate selection
- T2 event/result CSV logging

Input:
- A = click at the current ray cursor or explicit virtual cursor
- RaycastBaseline stick = scroll / seek on the ray-hit WebView
- ExplicitDisplayFocus trigger + stick = scroll / seek on the focused WebView

Done when:
- Both RaycastBaseline and ExplicitDisplayFocus can run the same WebView task.
- Practice automatically continues to Main; completion or abort returns cleanly to condition selection.
- T2 CSV files record completion, YouTube operations, scrolling/clicking, display switches, controller movement/rotation, and clutch count.
```

---

## Phase 6: Eye Tracking integration

### Codex依頼文

```text
Goal:
Integrate Meta Quest Pro Eye Tracking into GazeProvider.

Follow Docs/prototype_spec.md.

Requirements:
- Keep HmdForward as a development fallback.
- Add real EyeTracking gaze source using available Meta Quest Pro / Meta XR APIs in the project.
- IsEyeTrackingAvailable() should report availability.
- CurrentGazeSource should be visible in debug UI.
- Calibration_Debug scene should visualize the real gaze ray.
- If EyeTracking is not available, fail gracefully and show a clear warning.

Done when:
- GazeProvider can switch between HmdForward and EyeTracking.
- EyeTracking gaze ray is visualized in Calibration_Debug.
- ExplicitDisplayFocus uses EyeTracking when selected.
```

---

## Phase 7: 実験用Scene分割・ログ強化

### Codex依頼文

```text
Goal:
Prepare the prototype for pilot experiments.

Follow Docs/prototype_spec.md.

Implement:
- Separate scenes:
  - Dev_Prototype
  - Calibration_Debug
  - Exp_FocusPointing
- T2 remains in Dev_Prototype unless a later design explicitly creates a separate scene
- Participant ID input
- Condition selection
- Trial randomization or counterbalancing placeholder
- Event log
- Trial log
- Optional frame log
- README experiment operation instructions

Done when:
- A pilot participant can run through calibration, T1 pointing, and T2 Web Browsing.
- Logs are saved with participant ID, condition, task type, and trial index.
- README explains the experiment flow.
```

---

## Implementation priority summary

```plain text
Phase 0: Fix spec in repository
Phase 1: Two displays + layout + hit detection
Phase 2: RaycastBaseline
Phase 3: ExplicitDisplayFocus
Phase 4: T1 FocusPointing
Phase 5: T2 Web Browsing
Phase 6: Meta Quest Pro Eye Tracking
Phase 7: Experiment scenes and logging polish
```

T3 AttentionFocus is important for the paper argument, but it should be implemented after T1/T2 are stable.
