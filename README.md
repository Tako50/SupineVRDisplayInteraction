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
- Release `Space`: A-button submit/click fallback
- `Tab`: switch between `RaycastBaseline` and `ExplicitDisplayFocus`
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
- Releasing A / `Space` clicks at the virtual cursor on the focused display; right trigger alone does not click
- in the default T2 WebView mode, A is a standalone click; trigger + vertical stick scrolls, while trigger + horizontal stick latches Web UI drag from the virtual cursor until trigger release
- outside a running WebView session, right trigger + right stick vertical, or `Left Shift` + `W` / `S`, keeps the existing relative-scroll behavior
- moving gaze to another display after focus is locked does not redirect click or scroll input

The debug overlay shows the current condition, focus state, focused display, and gaze candidate ids. Gaze highlight is managed separately from interaction focus by `GazeDisplayFocusManager`.

## Fixed Visual Feedback

`GazeDisplayFocusManager` highlights the current pointing target as visual feedback only:

- `RaycastBaseline`: the first display hit by the controller Ray
- `ExplicitDisplayFocus`: the foremost display under the gaze ray

It does not change the input target, explicit input focus, cursor, click, or scroll destination. Highlight is fixed ON for both methods and is no longer selectable in the task menu.

Controller-Ray feedback is also fixed by method:

- `RaycastBaseline`: display the `Long` controller Ray (`4.0m` by default).
- `ExplicitDisplayFocus`: hide the controller Ray.
- The controller Ray uses normal world-space depth testing, so a display visually occludes any Ray segment located behind it from the participant's viewpoint.

The task menu therefore contains only task, interaction method, T1 layout/T2 content set, and session-start controls. Its status card shows the fixed highlight and Ray behavior for the selected method.

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
- `ExplicitDisplayFocus`: trigger + vertical stick scrolls the focused WebView; trigger + horizontal stick starts Web UI drag from the virtual cursor, stick movement drags it, and trigger release ends it.
- Web UI drag starts pointer down only after movement exceeds a small threshold, then sends pointer move / up to the WebView. This prevents a stationary trigger press from becoming a Web click while allowing YouTube's seek bar to be manipulated through the page UI.
- Scroll directly updates the WebView page's relative scroll position. Web UI drag depends on the active page element under the pointer.
- Stick cursor and Web UI drag speeds are pixel-based so horizontal and vertical motion have the same canvas-pixel speed. Tune `Prototype_Managers > VirtualCursorController > Cursor Speed Pixels Per Second` and `Prototype_Managers > ClickDispatcher > Web View Stick Gesture Speed Pixels Per Second`. Current defaults are `220` px/s for cursor movement and `260` px/s for Web UI drag.
- `Stick Touch Gesture` remains available in the Inspector for comparison, and `Legacy Direct Scroll And Submit Drag` remains available for rollback.
- To restore the previous direct `ScrollBy` plus A-held drag path, set `WebViewSessionManager > Input Mode = Legacy Direct Scroll And Submit Drag`.
- During a WebView session, the right controller B button navigates back one page on the display currently under the Baseline ray or focused by `ExplicitDisplayFocus`. In the Editor, the equivalent debug key is `Tab`. Outside a running WebView session, B / `Tab` retains its existing condition-toggle behavior.
- T2 forces the YouTube HTML video element to `muted = false` and `volume = 1.0` when telemetry attaches or playback starts. Tune `WebViewSessionManager > Force Youtube Unmuted` and `Youtube Forced Volume` if this needs to be disabled or lowered.

This keeps WebView browsing independent from the interaction method. TLab is pulled through UPM git dependencies rather than copying package sources into `Assets/`.

In `Dev_Prototype`, the T2 menu launches only the current YouTube + comparison task. To try it:

1. Select `T2 Web Browsing` in the VR task menu.
2. Select `Content A` or `Content B`.
3. Select `Raycast Baseline` or `Explicit Display Focus`. Highlight/Ray feedback is applied automatically, and T2 always uses `UpDownDepth` regardless of the T1 layout selection.
4. Press `START T2 TASK`.
5. Press the in-display `START` gate and wait for the countdown.
6. `Display_B_Back` opens the selected content's YouTube URL. `Display_A_Front` opens the common 20-product comparison Web through `WebViewSessionManager.Secondary Initial Url`.

T2 also shows a passive world-space instruction panel above `Display_B_Back`. It mirrors the current training step, main-task instruction, completion state, and late-task reminder without adding a collider or receiving controller input. The panel uses the back display's transform, so it stays fixed with the experiment displays and does not follow head direction. Panel size, scale, and phase/message font sizes are adjustable on `WebViewSessionManager`. T2 is currently fixed to `UpDownDepth`; the other layout presets remain available only for T1.

T2 runs as one continuous, task-completion-driven session with a 10-minute overall limit. Practice follows eight checks in the current Notion order: YouTube pause, play, skip forward by 10 seconds, seek backward, Web scroll, product-detail open, candidate add, and candidate remove. Completing P08 immediately starts Main; no fixed waiting window is used after completion. Practice candidates are cleared at the transition, while the pages remain loaded. The instruction panel shows the interaction condition and the overall countdown.

Main advances immediately when each task is achieved. V2D completes when the designated item detail is opened and that item is added to the candidate list: Content A uses 4:44–6:20 and `A03`; Content B uses 8:46–10:30 and `B05`. D2V completes when the specified Web detail has been opened and YouTube is paused within its appearance range: Content A uses the second item in `居住・寝具` and `A08` (11:28–12:23); Content B uses the fourth item in `火器・調理用品` and `B09` (15:48–16:51). Semi-free comparison completes when the participant adds one additional candidate after D2V. The participant then confirms one final candidate. That click immediately fixes the T2 end time and writes the result CSV, while the comparison Web remains visible on a task-complete screen showing the selected product image, name, brand, price, size, and weight for the post-task questionnaire. After the questionnaire, `候補リストを削除してホームに戻る` clears persisted candidates, restores the Web home state, and returns Unity to condition selection for the next condition. The 10-minute and stage timers no longer run while this post-task review screen is open. Practice, V2D, D2V, semi-free, and finalization each have a separate inspector-configurable stage timeout; these values remain `0` (disabled) until the experiment values are confirmed. Elapsed time is still recorded for analysis, and at least one Main-phase YouTube play, pause, or seek is required before confirmation.

T2 writes separate `Logs/T2/t2_events_*.csv` and `t2_results_*.csv` files under `Application.persistentDataPath`. The summary includes participant/session/counterbalance identifiers, method, content set, duration, selected candidates, V2D/D2V completion times, candidate add/remove and detail counts, YouTube operation counts and pause time, Web scroll/click amount, display switches, controller movement/rotation, clutch count, and result.

The T2 completion telemetry is implemented through TLab's `window.tlab.unitySendMessage` bridge.

During the unified T2 session, the normal condition menu is hidden. The exit panel is fixed 90 degrees to the left of the HMD pose captured when the task starts. The left-controller ray is shown only while it is hitting this panel. Aim at it and hold the left trigger for `0.6` seconds to abort and return to condition selection; hover, hold progress, and haptics provide feedback. B is not used for exiting. Returning disables both WebViews.

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
- Each main block contains 48 trials: 2 displays x 6 positions x 2 sizes x 2 cycles.
- Each 24-trial cycle contains every display/position/size combination exactly once.
- Target positions use `x = 0.10 / 0.50 / 0.90` and `y = 0.20 / 0.80`.
- Target sizes `Small` and `Large` are rendered at approximately 1.5 and 3 degrees based on the current head-to-display-center distance.
- `T1TrialSequenceGenerator` uses a logged deterministic seed to choose a constrained random order. D1→D2 and D2→D1 counts are balanced, long same-display/size runs are penalized, and Task C input-occluded trials are spread through the block.
- Task C marks the three Display 2 positions at `y = 0.80` as `inputOccluded`, giving 12 input-occluded trials and 36 `none` trials per block. Tasks A/B log all 48 trials as `none`.
- Training is Inspector-configurable from 6 to 12 trials and may loop until the experimenter ends it.
- Task order is recorded as one of `ABC / BCA / CAB / ACB / CBA / BAC`; method order is recorded as `RayFirst` or `ExplicitFirst`.
- Starting a task runs only the selected task/method block. After its 48-trial main block finishes, the task returns to condition selection.
- The old `Assets/Resources/T1/target_orders_ABCDEFG.csv` path remains as an optional compatibility mode when `Use Latest 48 Trial Design` is disabled.
- `ConditionA` maps to `RaycastBaseline` by default, and `ConditionB` maps to `ExplicitDisplayFocus` by default.
- A visible `FocusPointingTarget` is generated on the active target display.
- Starting Training or Main Task first shows a centered `START` button on `Display_B_Back`; pressing it starts a 3-second countdown before the first target appears.
- `ClickDispatcher` reports click attempts to the task layer for both `RaycastBaseline` and `ExplicitDisplayFocus`.
- `ErrorEvaluator` classifies clicks as `Correct`, `DisplayError`, `TargetError`, or `Miss`.
- `Logger` writes `TrialResult` rows with participant/session placeholders, task/method assignment, target info, click info, result flags, start time, click time, and completion time.
- A compatibility target-selection CSV is written to `Application.persistentDataPath/Logs/target_selection_*.csv` with correct target selections.
- The analysis-oriented T1 CSV is written to `Application.persistentDataPath/Logs/t1_results_*.csv`. It records every click attempt, including `Correct`, `DisplayError`, `TargetError`, and `Miss`, with participant/session ids, T1 task, task/method order, deterministic sequence seed, cycle, position, size, `inputOccluded`/`none`, target/click positions, attempt index, response time, controller movement meters, controller rotation degrees, and analysis flags.

This does not implement questionnaires or full participant-flow screens.

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
- `UpDownDepth`: lower/front and upper/back displays with equal apparent angular size compensation

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
npm run dev
```

表示されたローカルURLをブラウザで開きます。本番用の静的ファイルは次で `dist/` に生成できます。

```bash
npm run build
npm run preview
```

Viteの `base` は相対パスに設定しているため、生成物は後からUnity WebView用のローカルコンテンツとして組み込みやすい構成です。

Quest上のUnity WebViewから開発用HTTPサーバーを表示するため、`Assets/Plugins/Android/T2Cleartext.androidlib` がAndroid ManifestへローカルHTTP許可設定をマージします。これは実験・開発用設定です。MacとQuestを同じネットワークへ接続し、`npm run dev -- --host 0.0.0.0` で起動してください。`WebViewSessionManager` の `Secondary Initial Url` には `Network:` を含めず、`http://<MacのIP>:5173/` のみを設定します。外部公開用ビルドではHTTPSへ移行し、この許可設定を見直してください。

### 画面構成

- ホーム: カテゴリ一覧または候補リストへ移動
- カテゴリ一覧: 5カテゴリから1つを選択
- カテゴリ内の商品: 選択カテゴリの商品カードを表示
- 商品詳細: 商品情報と商品紹介を確認して候補へ追加
- 候補リスト: 候補の削除と、最終候補1つの確定

### `items.json` の編集方法

商品データは `src/data/items.json` のJSON配列です。既存商品と同じフィールドを持つオブジェクトを追加してください。`category` は画面に定義済みの5カテゴリのいずれかを指定し、`item_order` は各動画内での紹介順を保持します。カテゴリ内の固定表示順は `src/App.tsx` の `CATEGORY_DISPLAY_ORDER` で管理します。画像は `public/images/` に置き、`image` には `/images/example.jpg` の形式で記載します。画像が存在しない場合は商品名のみの代替表示へ自動的に切り替わります。`video_set`、`video_year`、`item_id`、動画内時刻は内部データ・ログ用で、Web画面には表示しません。

### ログの保存・書き出し方法

`category_open`、`product_open`、`add_candidate`、`remove_candidate`、`candidate_list_open`、`confirm_candidates`、`back` をブラウザの `localStorage` に保存します。保存キーは `t2_web_logs` です。参加者画面にはログ書き出しボタンを表示しません。候補リストは同じブラウザの `t2_web_candidates` に保存されます。
