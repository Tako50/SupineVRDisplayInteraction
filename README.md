# SupineVRDisplayInteraction

仰臥位VRにおける2D仮想ディスプレイ操作を検討するための研究用プロトタイプです。

This repository is currently private and under active development.
No license is granted at this stage.

## Phase 1 Prototype

The first working prototype is implemented in `Assets/Scenes/Dev_Prototype.unity`.

It is being built in phases from `Docs/prototype_spec.md`:

- `RaycastBaseline`: standard controller raycasting. The foremost display hit by the controller ray is the active operation target.
- `GazeRay`: gaze selects only the operation display; the controller Ray intersection on that selected display supplies the pointer position and ignores intervening display colliders.
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
  - `UpDownDepth`: Layout 1. Shared by T1 Task C and T2.
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
- Release `Space`: A-button submit/click fallback
- `Tab`: cycle `RaycastBaseline` → `GazeRay` → `ExplicitDisplayFocus` outside a running WebView session
- `G`: grip fallback for focus confirmation
- `Left Shift`: trigger fallback; trigger alone never clicks, and in a WebView session hold it with `W` / `S` for Explicit scroll or `A` / `D` for Explicit Web UI drag
- `X`: simulate aiming at the left-side exit panel and holding the left trigger
- `R`: clear the focused display
- right stick vertical, or `W` / `S`: scroll the currently ray-hit display in `RaycastBaseline`

## Phase 2 RaycastBaseline

- the right controller ray uses a single `Physics.Raycast`
- only the first `DisplaySurface` hit receives input
- the cursor follows the ray hit position on the hit display
- Releasing A / `Space` clicks at the ray-hit position; right trigger alone does not click
- in the default T2 WebView mode, A is a standalone click, vertical stick scrolls, and trigger hold + Ray movement sends Web UI drag
- right stick vertical / `W` / `S` scrolls only the currently hit display
- `UpDownDepth` uses the same front/back geometry in T1 Task C and T2

## GazeRay condition

`GazeRay` is the third, intermediate interaction condition available in the T1/T2 task menu:

- a valid gaze hit immediately selects the operation display; the experiment default dwell is `0s`
- gaze never supplies the pointer coordinate, warps a cursor, invokes explicit focus, or requires Grip
- the pointer coordinate comes from a direct `BoxCollider.Raycast` against only the gaze-selected display
- a nearer display may be crossed without becoming the input target; the visible controller Ray ends at the selected display intersection
- if the controller Ray misses the selected display bounds, the pointer is invalid and is not clamped to an edge
- moving the controller moves the pointer; moving gaze within the same selected display does not move it
- A clicks, vertical Stick scrolls, and Trigger + controller-Ray movement performs the existing Web UI drag on the selected display
- when Eye Tracking becomes invalid, the default `KeepLastSelectedDisplay` policy keeps the selected display but records `gazeValid=false`; it never falls back to ordinary Ray targeting
- `HmdForward` and `DebugCameraForward` remain Editor/development modes. When `EyeTracking` is configured but unavailable, the task menu refuses to start a `GazeRay` experiment

The policy and optional dwell are Inspector fields on `RaycastPointer`. The debug overlay reports gaze validity, gaze-selected display, pointer validity, and penetrated display IDs. Dedicated `Logs/gazeray_*.csv` samples include controller-Ray origin/direction/intersection, normalized pointer coordinates, switch/trigger counts, scroll target, penetration, and gaze-invalid policy without changing existing CSV columns.

## Phase 3 ExplicitDisplayFocus MVP

`ExplicitDisplayFocus` is implemented as the first proposed-method MVP:

- gaze uses `RaycastAll`-style candidate detection through `DisplayManager.GetDisplayHitsAll`
- gaze candidates are sorted by hit distance
- while grip / `G` is held, valid gaze continuously updates the focused display and virtual cursor
- releasing grip ends gaze mode and keeps the last valid focused display and cursor position
- when multiple candidates are under gaze, the nearest candidate is selected for this MVP
- focus does not change from gaze movement alone
- focus remains locked until grip is held again on another candidate
- a direct display hit has priority; when gaze misses every display by no more than `3.0°`, the nearest display rectangle is selected and the cursor is clamped to its nearest edge
- off-display switching uses `0.5°` hysteresis in favor of the current focused/candidate display; gaze farther than `3.0°` from every display does not update focus or cursor
- when configured Eye Tracking is invalid or untracked, the current focus, candidate, and cursor are held; experimental interaction never substitutes HMD forward
- right stick / `WASD` moves the virtual cursor inside the focused display
- Releasing A / `Space` clicks at the virtual cursor on the focused display; right trigger alone does not click
- in the default T2 WebView mode, A is a standalone click; trigger + vertical stick scrolls, while trigger + horizontal stick latches Web UI drag from the virtual cursor until trigger release
- outside a running WebView session, right trigger + right stick vertical, or `Left Shift` + `W` / `S`, keeps the existing relative-scroll behavior
- moving gaze to another display after focus is locked does not redirect click or scroll input

The debug overlay shows the current condition, focus state, focused display, and gaze candidate ids. Gaze highlight is managed separately from interaction focus by `GazeDisplayFocusManager`.

## Fixed Visual Feedback

`GazeDisplayFocusManager` highlights the current pointing target as visual feedback only:

- `RaycastBaseline`: the first display hit by the controller Ray
- `GazeRay`: the display selected by valid gaze (the controller Ray only determines its in-display coordinate)
- `ExplicitDisplayFocus`: the same direct-hit or bounded nearest-display candidate used by `FocusManager`; invalid Eye Tracking holds the last valid highlight

It does not change the input target, explicit input focus, cursor, click, or scroll destination. Highlight is fixed ON for both methods and is no longer selectable in the task menu.

Controller-Ray feedback is also fixed by method:

- `RaycastBaseline`: display the `Long` controller Ray (`4.0m` by default).
- `GazeRay`: display the `Long` controller Ray through intervening displays to the selected-display intersection.
- `ExplicitDisplayFocus`: hide the controller Ray.
- The controller Ray uses normal world-space depth testing, so a display visually occludes any Ray segment located behind it from the participant's viewpoint. Visible Ray segments remain fully opaque to preserve depth cues against display content.

The task menu uses a reproducible between-participant assignment. Enter the numeric participant number and press `番号を確定`; `ParticipantMethodAssignment` assigns one of `RaycastBaseline`, `GazeRay`, or `ExplicitDisplayFocus` using three-participant permuted blocks with allocation seed `20260725`. Every consecutive block of three contains each method once, and the same number always reproduces the same assignment. The method buttons are hidden in experiment operation. After confirmation, choose T1 or T2; both tasks receive the same participant ID and assigned method. The status card and logs show a self-describing code such as `PB3-B004-S3-RAY-Seed20260725`; the P001–P030 paper-questionnaire lookup table is [docs/participant_method_assignments.csv](docs/participant_method_assignments.csv). T2 content remains fixed to the 2024 set.

Experiment CSV names use `participantId_allocationCode_task_phase_runId_dataType_timestamp.csv`, for example `P012_PB3-B004-S3-RAY_T2_Practice_R20260726T051234567Z_results_20260726_141234_567.csv`. Practice and Main always receive different `runId` and `sessionId` values. T1 results/target-selection, T2 events/results/post-timeout selections, 60 Hz trajectories, common events, and GazeRay frames remain separate files, but every schema-v2 row includes participant, allocation, session, run, and schema identifiers.

## T2 YouTube + Comparison Web Task

T2 implements the Notion `T2 指示セット案：YouTube＋自作Web` semi-free task. The back display runs YouTube and the front display runs a fixed experiment comparison page. The participant uses both displays, builds a candidate list, and completes the task by confirming one final candidate on the front page.

T2 uses `TLabWebViewDisplayBridge`, backed by the free MIT-licensed UPM packages from `TLabAltoh/TLabWebView` and `TLabAltoh/TLabVKeyborad`.

1. Let Unity resolve `Packages/manifest.json`. It contains `com.tlabaltoh.webview` and `com.tlabaltoh.vkeyborad` git dependencies.
2. Open `Assets/Scenes/Dev_Prototype.unity`.
3. The T2 Web Browsing session uses `TLabWebViewDisplayBridge` on both experiment displays.
4. Build to Quest Pro / Android. TLabWebView does not render real web pages in the Unity Editor; the bridge shows an Editor placeholder there.

When `Consume Experiment Input` is enabled, the current experiment input path is reused:

- `WebViewSessionManager > Input Mode = Direct Scroll And Seek` is the default trial mode.
- A is the only click/selection input in both conditions and in both T1 and T2. A is not combined with stick movement, and right trigger alone does not click.
- `RaycastBaseline`: vertical stick scrolls the WebView under the ray; trigger hold + Ray movement sends Web UI drag.
- `GazeRay`: vertical stick scrolls the gaze-selected WebView; trigger hold + controller-Ray movement sends Web UI drag on that same display even when another display is in front.
- `ExplicitDisplayFocus`: trigger + vertical stick scrolls the focused WebView; trigger + horizontal stick starts Web UI drag from the virtual cursor, stick movement drags it, and trigger release ends it.
- Web UI drag starts pointer down only after movement exceeds a small threshold, then sends pointer move / up to the WebView. This prevents a stationary trigger press from becoming a Web click while allowing YouTube's seek bar to be manipulated through the page UI.
- Moving the ray cursor or explicit virtual cursor over the YouTube seek bar sends button-free hover events to the WebView. YouTube can therefore show its normal time/thumbnail preview before selection, and A clicks the previewed seek position without requiring a drag.
- Scroll directly updates the WebView page's relative scroll position. Web UI drag depends on the active page element under the pointer.
- Stick cursor and Web UI drag speeds are pixel-based so horizontal and vertical motion have the same canvas-pixel speed. Tune `Prototype_Managers > VirtualCursorController > Cursor Speed Pixels Per Second` and `Prototype_Managers > ClickDispatcher > Web View Stick Gesture Speed Pixels Per Second`. Current defaults are `220` px/s for cursor movement and `260` px/s for Web UI drag.
- `Stick Touch Gesture` remains available in the Inspector for comparison, and `Legacy Direct Scroll And Submit Drag` remains available for rollback.
- To restore the previous direct `ScrollBy` plus A-held drag path, set `WebViewSessionManager > Input Mode = Legacy Direct Scroll And Submit Drag`.
- During a WebView session, the right controller B button navigates back one page on the display currently under the Baseline ray or focused by `ExplicitDisplayFocus`. The comparison Web stores its home, category, product-list, product-detail, and candidate-list transitions in browser history, so B also works between its internal screens. In the Editor, the equivalent debug key is `Tab`. Outside a running WebView session, B / `Tab` retains its existing condition-toggle behavior.
- The comparison page's `商品ページを開く` link navigates in the same WebView. TLab does not create a second Android WebView for `target="_blank"` links, so same-view navigation is required on Quest; press B to return to the comparison page.
- T2 forces the YouTube HTML video element to `muted = false` and `volume = 1.0` when telemetry attaches or playback starts. Tune `WebViewSessionManager > Force Youtube Unmuted` and `Youtube Forced Volume` if this needs to be disabled or lowered.

This keeps WebView browsing independent from the interaction method. TLab is pulled through UPM git dependencies rather than copying package sources into `Assets/`.

In `Dev_Prototype`, the T2 menu launches only the current YouTube + comparison task. To try it:

1. Enter the participant number and press `番号を確定`.
2. Select `T2 動画・Web比較`. The assigned method is applied automatically, and T2 always uses the shared `UpDownDepth` geometry.
3. Press `練習を開始` or `本番を開始`. Practice and Main are independent runs.
4. Press the in-display `START` gate and wait for the countdown.
5. `Display_B_Back` opens the fixed 2024 YouTube URL. `Display_A_Front` opens the comparison Web from the local Vite server configured in `Secondary Initial Url`.

T2 also shows a passive world-space instruction panel immediately above `Display_B_Back`. It uses a wide, low-profile shape so the instruction stays close to the display without shifting the two-display layout upward. It mirrors the current training step, main-task instruction, completion state, and late-task reminder without adding a collider or receiving controller input. The message stays at the configured 42 px instead of shrinking; P01-P08 and every Main instruction fit by wrapping within the unchanged panel geometry. When an instruction actually changes, the panel briefly pulses in color and size for three seconds so the participant notices the next step; periodic WebView refreshes do not retrigger the effect, and the first instruction is not emphasized. The panel uses the back display's transform, so it stays fixed with the experiment displays and does not follow head direction. Panel size, scale, phase/message font sizes, and the change-emphasis duration, frequency, scale, and color are adjustable on `WebViewSessionManager`. T2 is currently fixed to `UpDownDepth`; the other layout presets remain available only for T1.

T2 Practice now uses content that is separate from Main. `Display_B_Back` loads `Practice Youtube Url` (default: `https://www.youtube.com/watch?v=HQRzNpPDk0k`, the same channel's approximately 9:33 side-table review) rather than another segment of the 2024 Main video. `Display_A_Front` loads `Practice Comparison Initial Url`, currently the same local Vite server with `?mode=practice`; this mode shows five fictional `TR01`–`TR05` products and uses a separate candidate-storage key, so none of the Main 20 products are exposed during Practice. The player UI, comparison-Web UI, display layout, and input mappings remain the same as Main.

Practice follows eight checks: YouTube pause, play, skip forward by 10 seconds, seek backward, Web scroll, product-detail open, candidate add, and candidate remove. It has no time limit and requires at least one complete round. After each round the sequence starts again instead of advancing automatically. Once the participant says they understand the controls and the experimenter confirms that the required operations were completed independently, the experimenter aims the left controller at the left-side panel and holds the left trigger. The panel changes to `END PRACTICE` only after the minimum round is complete. Additional practice may end partway through the next round. Finishing Practice writes one structured Practice result row, closes its files, disables the WebViews, and returns to the task menu; it never starts Main automatically. The experimenter later presses `本番を開始`, which creates a new run, loads the fixed 2024 Main content, and presents a fresh START gate. The independent 12-minute Main timer begins only after that START gate. Practice duration, completed rounds, total completed steps, additional steps beyond the minimum round, and readiness confirmation are retained in the T2 result and event logs.

Main retains the V2D and D2V stage-specific instructions and advances when each target is achieved. With the fixed 2024 content, V2D uses 4:44–6:20 and `A03`. D2V uses the second item in `居住・寝具`, `A08` (11:28–12:23). After D2V, the instruction changes to unrestricted comparison: `動画とWebを自由に見比べ、追加・買い替えの候補として良さそうな動画内の商品を、候補リストに追加してください。候補の数に制限はありません。` Candidate additions do not end this stage, and the Web no longer imposes a three-item limit. At 11:30 from Main start, Unity changes to the final instruction, unlocks confirmation, and automatically opens the candidate-list screen: `候補リストを確認し、今回のキャンプをより快適にするために最も良さそうな商品を1つ選び、候補を確定してください。` This leaves 30 seconds before the 12-minute Main limit. A valid confirmation immediately fixes the T2 end time and writes the result CSV, while the comparison Web remains visible on a task-complete screen showing the selected product image, name, brand, price, size, and weight for the post-task questionnaire. If no item has been confirmed at 12:00, Unity freezes the Main metrics and immediately writes the durable `timeout` result row, but keeps the candidate list operable until one item is confirmed. The timeout result remains unchanged; the later item and confirmation time are written to `t2_post_timeout_selections_*.csv`. A missing YouTube operation still blocks an on-time confirmation, but does not deadlock the required post-timeout selection after Main metrics have been frozen. After the questionnaire, `候補リストを削除してホームに戻る` clears persisted candidates, restores the Web home state, and returns Unity to condition selection for the next condition. Phase and stage timers no longer run while this post-task review screen is open. Practice, V2D, D2V, semi-free, and finalization each retain a separate inspector-configurable stage timeout; these values remain `0` (disabled) until explicitly configured.

T2 writes `Logs/T2/*_T2_Practice_*_events_*.csv`, `*_T2_Practice_*_results_*.csv`, and corresponding `Main` files under `Application.persistentDataPath`. A timed-out Main run writes its summary row at exactly 12:00; its later required product choice is joined by participant/session/run from the matching `post_timeout_selections` file.

The T2 completion telemetry is implemented through TLab's `window.tlab.unitySendMessage` bridge.

During either T2 run, the normal condition menu is hidden. The exit panel is fixed 90 degrees to the left of the HMD pose captured when the task starts. The left-controller ray is shown only while it is hitting this panel. In Practice, the panel ends a ready Practice run and returns to the task menu; before readiness, or during Main, it aborts and returns to the menu. Hold the left trigger for `0.6` seconds; hover, hold progress, and haptics provide feedback. B is not used for exiting. Returning disables both WebViews.

Keyboard handling uses this project's display-local keyboard overlay because the bundled TLab keyboard is screen-layout oriented and does not line up well inside the VR display.

`TLabWebViewDisplayBridge` keeps both WebView sizes at the display's 16:9 aspect. `View Size` defaults to `960x540`, while `Texture Size` defaults to `1920x1080`. The smaller view size makes web-page text and controls use a larger layout; the higher texture size keeps the captured result sharp when shown in VR. These settings do not change the physical VR display size, which is still controlled by `DisplaySurface` / layout settings. The bridge defaults to `HardwareBuffer`, `24fps`, and `Limit Texture Updates To Fps` so HMD motion stays smoother on Quest. If the headset is still juddering during head movement, lower `Texture Size` to `1600x900` or `1280x720`, or reduce `Fps`. It disables TLab's bundled screen-layout keyboard by default and uses this project's display-local keyboard instead, so RaycastBaseline and ExplicitDisplayFocus can press keys through the same display-normalized click path. `Show Keyboard Only For Text Input` is enabled by default: the keyboard stays hidden while browsing and appears only when the active web element is an `input`, `textarea`, or `contenteditable` field. Key labels are drawn as a high-sorting-order Unity overlay with bold text and outlines, and the display cursor is raised above the keyboard while it is visible. The built-in TLab keyboard can be re-enabled from the Inspector with `Use Built In TLab Keyboard`, but it is not recommended for the current VR display layout.

Running two WebViews doubles the capture workload. If head-motion judder increases on Quest, lower `Texture Size` or `Fps` on both display bridges together so the two conditions keep the same visual settings.

The default `DirectScrollAndSeek` path changes the page's relative scroll position for scroll input and uses pointer down / move / up for Web UI drag. TLab performs page scrolling through JavaScript because its native `ScrollBy` path was unreliable on Quest. The older hidden-touch stick gesture remains available as the Inspector-selectable `StickTouchGesture` mode.

When the WebView session ends, both active bridges load `about:blank` and destroy their generated WebView prefabs. This is intentional: simply hiding a prefab can leave media playback, such as YouTube audio, running in the background.

## Phase 3.5 Editor Validation

`Dev_Prototype` includes editor-only validation helpers for testing `ExplicitDisplayFocus` without Quest hardware:

- `EditorDebugInputProvider` keeps keyboard input separate from the real XR input path.
- `GazeProvider` has `DebugCameraForward` for driving the same gaze candidate path from the Main Camera in Play Mode.
- The debug overlay shows condition, gaze source, focus state, focused display, gaze candidates, cursor position, layout, debug input state, last click result, and last scroll amount. Its Canvas uses `ScreenSpaceCamera` so it remains visible when VR rendering is enabled.
- `FocusManager` exposes safe debug utilities: `ClearFocus`, `ForceRefocusFromCurrentGazeCandidate`, `GetFocusedDisplayId`, `GetCurrentCandidateIds`, and `GetFocusState`.

For editor validation:

1. Open `Assets/Scenes/Dev_Prototype.unity`.
2. Press Play.
3. Use `Tab` to switch to `ExplicitDisplayFocus`.
4. Aim the Game view / Main Camera at a display.
5. Press `G` to confirm focus and warp the cursor.
6. Use `WASD` or arrow keys to move the focused cursor.
7. Press `Space` to click during a WebView session.
8. Hold `Left Shift` and press `W` / `S` to scroll the focused WebView, or hold `Left Shift` and press `A` / `D` for Web UI drag.
9. Hold `X` to validate the left-side exit panel without XR controllers.

## Phase 4 FocusPointing Task Skeleton

`Dev_Prototype` now includes the first task layer for T1 display selection + target selection:

- `FocusPointingTaskManager` manages the target-selection trial list.
- `Display A` and `Display B` are assignable from the Inspector.
- T1 currently maps its three task selections back to the established layouts: Task A = `LeftRight`, Task B = `UpDown`, and Task C = `UpDownDepth`. The temporary `FrontBackClear` / `FrontBackOccluded` layout presets were removed after the supplied specification was identified as an older version.
- Each main block contains 96 trials: 2 displays x 6 positions x 2 sizes x 4 cycles.
- Each 24-trial cycle contains every display/position/size combination exactly once.
- Target positions use `x = 0.20 / 0.50 / 0.80` and `y = 0.20 / 0.80`. The horizontal positions are inset from the display edges so the centered front display can occlude all three designated rear-row targets without a horizontal display offset.
- Target sizes `Small` and `Large` are rendered at approximately 1.5 and 3 degrees based on the current head-to-display-center distance.
- `T1TrialSequenceGenerator` uses a logged deterministic seed to choose a constrained random order. D1→D2 and D2→D1 counts are balanced, long same-display/size runs are penalized, and Task C input-occluded trials are spread through the block.
- Main uses the same concrete target order for every participant, interaction method, and rerun. With the default base seed, Task A/B/C use `3100` / `4100` / `5100`; participant, session, run, and method identifiers do not affect these Main seeds. Training remains participant/session/block dependent.
- Consecutive trials may not reuse the same position on the same display. This is a hard constraint within each 24-trial cycle and across cycle boundaries.
- Task C marks the three Display 2 positions at `y = 0.80` as `inputOccluded`, giving 24 input-occluded trials and 72 `none` trials per block. Tasks A/B log all 96 trials as `none`.
- Training runs 12 trials each in `LeftRight → UpDown → UpDownDepth` order. After the required first 36 trials, the same 12-trial layout cycle continues until the participant reports being accustomed to the method and the experimenter presses `End Training`. Main then starts again from Task A `LeftRight`.
- Main uses the fixed `Task A LeftRight → Task B UpDown → Task C UpDownDepth` order, so no layout selection is required before `START MAIN TASK`. The three 96-trial blocks run as one 288-trial session with the selected interaction method and share the same CSV files. `globalTrialIndex` and `trialIndex` are `1–96` for Task A, `97–192` for Task B, and `193–288` for Task C.
- During each 96-trial Main block, the existing World Space condition label above `Display_B_Back` expands downward and shows `T1 PROGRESS | 0/4` on its second line. It updates to `1/4`, `2/4`, and `3/4` after correct trials 24, 48, and 72, then resets to `0/4` when the next task block is prepared. The progress line does not pause progression, and trial 96 continues directly into the existing block-completion flow.
- Correct and incorrect T1 clicks play distinct, short 2D tones only after evaluation and CSV logging. `Feedback Volume` defaults to `0.8`; optional `Correct Feedback Clip` / `Incorrect Feedback Clip` and an optional `Feedback Audio Source` are configurable on `FocusPointingTaskManager`. Missing clips are generated at runtime.
- After Task A or Task B finishes, the next layout is applied automatically. Task B and Task C show their START button immediately, but it remains disabled with a visible countdown for 30 seconds. Once unlocked, pressing START runs the normal 3-second countdown and begins the next block. After Task C finishes, T1 returns to condition selection.
- The interaction method is a between-participant assignment and remains fixed across T1 and T2. The legacy T1 `methodOrder` column is retained for compatibility (`RayFirst`, `GazeRayOnly`, or `ExplicitFirst`), but no longer describes a within-participant method order. The fixed task order is recorded as `ABC`.
- The old `Assets/Resources/T1/target_orders_ABCDEFG.csv` path remains as an optional compatibility mode when `Use Generated Trial Design` is disabled.
- `ConditionA` maps to `RaycastBaseline` by default, and `ConditionB` maps to `ExplicitDisplayFocus` by default.
- A visible `FocusPointingTarget` is generated on the active target display.
- Starting Training or Main Task first shows a centered `START` button on `Display_B_Back`; pressing it starts a 3-second countdown before the first target appears.
- `ClickDispatcher` reports click attempts to the task layer for both `RaycastBaseline` and `ExplicitDisplayFocus`.
- `ErrorEvaluator` classifies clicks as `Correct`, `DisplayError`, `TargetError`, or `Miss`.
- `Logger` writes `TrialResult` rows with participant/session placeholders, task/method assignment, target info, click info, result flags, start time, click time, and completion time.
- A compatibility target-selection CSV is written below `Application.persistentDataPath/Logs` with `T1`, phase, run ID, and `target_selection` in its filename.
- The analysis-oriented T1 CSV uses the same identity-rich filename with `results` as its data type. It records every click attempt, including `Correct`, `DisplayError`, `TargetError`, and `Miss`, with participant/session/run ids, T1 task, task/method order, deterministic sequence seed, cycle, position, size, `inputOccluded`/`none`, target/click positions, attempt index, response time, controller movement meters, controller rotation degrees, and analysis flags.
- T1 and T2 use the same controller-motion definition: frame-to-frame `Vector3.Distance` of the right-controller Transform for movement and frame-to-frame `Quaternion.Angle` for rotation. Rotation is recorded in degrees and includes yaw, pitch, and roll. T1 accumulates these values per trial; T2 accumulates them over the measured session.

This does not implement questionnaires or full participant-flow screens.

## T1 Ray Occlusion Layout Probe

`T1RayOcclusionLayoutProbe` on `Prototype_Managers` helps tune the T1 front/back display geometry:

- The active `UpDownDepth` geometry is shared by T1 Task C and T2: back 2.25 m at 0° horizontal / +11.8° vertical with `42.5° x 23.90625°`, front 0.75 m at 0° horizontal / -13.2° vertical with `37.5° x 21.09375°`. Both display centers remain on the HMD's forward centerline horizontally. The vertical center angles round the derived values `+11.796875°` and `-13.203125°` to one decimal place; the outer edges remain approximately symmetric at `±23.75°`, and the inner edges retain a `2.5°` gap.
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

`Apparent Width Degrees` and `Apparent Height Degrees` control the shared visual angle. Each placement also has optional apparent-width and apparent-height overrides; zero uses the shared value. Physical display size is recalculated from each display's distance. `Preserve Display Aspect Ratio` is initially off so independently specified horizontal and vertical visual angles are retained.

T1 Task C and T2 use the same `UpDownDepth` preset. Relative to the HMD anchor, `Display_B_Back` is at 2.25 m, 0° horizontal, +11.8° vertical with a `42.5° x 23.90625°` apparent size (center approximately `(0, 0.460, 2.202)`, physical size approximately `1.750 x 0.953 m`). `Display_A_Front` is at 0.75 m, 0° horizontal, -13.2° vertical with a `37.5° x 21.09375°` apparent size (center approximately `(0, -0.171, 0.730)`, physical size approximately `0.509 x 0.279 m`). The complete vertical envelope remains approximately symmetric at `-23.75°` to `+23.75°`, with a `2.5°` gap between the displays.

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

If the OpenXR eye-gaze device is not available or not tracked while `EyeTracking` is configured, `GazeProvider` logs a warning and returns no valid experimental gaze. Focus, candidate, cursor, highlight, and gaze-driven world-menu input are held or disabled; they do not use HMD forward. To test without a headset, explicitly select `HmdForward` or `DebugCameraForward` as a development mode.

Common CSV logs are written under `Application.persistentDataPath/Logs` with participant, allocation, task, phase, run ID, data type, and timestamp in the filename.
The common frame log samples the controller-Ray hit, valid eye-gaze hit, focused display, and active cursor display/normalized position every `0.25s`. Invalid Eye Tracking produces no gaze hit; development gaze modes remain visible in Unity diagnostics.

While T1 or T2 is running, `TrajectoryLogger` writes a phase/run-specific file under `Logs/Trajectory` at a fixed 60 Hz. It records the HMD and right-controller world poses, valid Eye Tracking ray and display hit, active cursor position, trigger/grip state, and right-stick value together with participant, allocation, session, run, schema, condition, phase, and T1 trial identifiers. Gaze fields are left empty when Eye Tracking is unavailable, so no HMD-forward ray is analyzed as eye gaze. Rows are buffered and flushed once per second to avoid per-frame storage flushes on Quest.

Validate copied logs before analysis:

```bash
python3 Tools/validate_experiment_logs.py Logs/Quest
python3 Tools/validate_experiment_logs.py Logs/Quest --require-complete-t1
```

The first command checks CSV width, schema-v2 identity fields, timing consistency, normalized coordinates, T2 Practice/Main summaries, and trajectory ordering. The second additionally requires one complete 288-trial T1 Main run, its 96/96/96 task balance, `globalTrialIndex` coverage from 1 through 288, 24 Task-C `inputOccluded` trials, and the no-consecutive-identical-display-position rule inside each task, including cycle boundaries. `python3 Tools/validate_experiment_logs.py --self-test` verifies the validator itself.

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
- `Analysis/T1/summary_by_condition.csv`: accuracy, error rates, attempts per completed trial, response time, controller movement, and controller rotation summaries per T1 task and interaction method.
- `Analysis/T1/summary_by_condition_display.csv`: the same metrics split by target display.
- `Analysis/T1/summary_by_condition_occlusion.csv`: the same metrics split by `occlusion_type` values `none` and `inputOccluded`.

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
- `UpDownDepth`: lower/front and straight-ahead/back displays using the shared T1 Task C/T2 geometry

Per-condition placement can be tuned in `LayoutPreferenceStudy_Root > Study_Managers > LayoutPreferenceStudyManager > Layouts`. Each layout has `Condition Center Offset Meters` plus Display A/B `Distance Meters`, `Horizontal Angle Degrees`, and `Vertical Angle Degrees`. Display width is computed from a fixed 40-degree apparent width, and the physical display size preserves a 16:9 aspect ratio by default so displays at different distances keep the same apparent scale.

The Study 1 scene uses only the existing `DisplaySurface` display representation plus new `LayoutPreference*` scripts. It does not include `RaycastPointer`, `FocusManager`, `ClickDispatcher`, `ScrollController`, `FocusPointingTaskManager`, controller rays, gaze rays, hit selection, or task targets.

Study 1 logs are written to:

```text
Application.persistentDataPath/Logs/layout_preference_*.csv
```

Rows are written when a condition is entered, left, or recentered. The CSV records participant/session ids, condition order, display poses, display sizes, and the HMD anchor pose used for the layout.

## T2 キャンプギア比較Web

T2実験で手前ディスプレイに表示する、React + TypeScript + Vite製の静的Webアプリをルート直下に置いています。商品は必ず「カテゴリ一覧 → カテゴリ内の商品 → 商品詳細」の順に探し、詳細画面から候補へ追加します。検索機能はありません。

### 起動方法

Node.jsを用意し、リポジトリのルートで次を実行します。

```bash
npm install
npm run dev:quest
```

`npm run dev`と`npm run dev:quest`はいずれもViteを`0.0.0.0:5173`で起動し、同一LAN上のQuestから接続できるようにします。MacのLANアドレスが変わった場合は、`WebViewSessionManager > Secondary Initial Url`と`DevPrototypeSceneBuilder`のURLを新しい`http://<MacのIP>:5173/`へ更新します。

静的ファイルは次で `dist/` に生成され、CSS、JavaScript、商品画像をインライン化した単一HTMLが `Assets/Resources/T2/t2_content_2024.html` に自動生成されます。

```bash
npm run build
npm run preview
```

Unityの現在の`Secondary Initial Url`は`http://133.87.151.83:5173/`です。実機でT2を開始する前にMacで`npm run dev`を起動してください。IPアドレスが変わった場合は上記URLをその都度更新します。同梱HTMLの`file://`経路はフォールバックとしてコード上に残していますが、現在の実機運用では使用しません。

### 画面構成

- ホーム: カテゴリ一覧または候補リストへ移動
- カテゴリ一覧: 5カテゴリから1つを選択
- カテゴリ内の商品: 選択カテゴリの商品カードを表示
- 商品詳細: 商品情報と商品紹介を確認して候補へ追加
- 候補リスト: 候補の削除と、最終候補1つの確定

### `items.json` の編集方法

商品データは `src/data/items.json` のJSON配列です。既存商品と同じフィールドを持つオブジェクトを追加してください。`category` は画面に定義済みの5カテゴリのいずれかを指定し、`item_order` は各動画内での紹介順を保持します。カテゴリ内の固定表示順は `src/App.tsx` の `CATEGORY_DISPLAY_ORDER` で管理します。画像は `public/images/` に置き、`image` には `/images/example.jpg` の形式で記載します。画像が存在しない場合は商品名のみの代替表示へ自動的に切り替わります。`video_set`、`video_year`、`item_id`、動画内時刻は内部データ・ログ用で、Web画面には表示しません。

本番の比較Webは、動画との照合性を保つため、使用許可を得た現行の商品画像をそのまま使用します。論文、発表資料、ポスター、公開デモなど外部提示用の画面を作る際は、実験用画像を生成画像または権利処理済み画像へ差し替えてからビルドし、実験画面のスクリーンショットをそのまま流用しないでください。

### ログの保存・書き出し方法

`category_open`、`product_open`、`add_candidate`、`remove_candidate`、`candidate_list_open`、`confirm_candidates`、`back` をブラウザの `localStorage` に保存します。保存キーは `t2_web_logs` です。参加者画面にはログ書き出しボタンを表示しません。候補リストは同じブラウザの `t2_web_candidates` に保存されます。
