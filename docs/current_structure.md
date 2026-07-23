# 現在のプロジェクト構成

更新日: 2026-06-25

この資料は、現時点で「どのファイルが何を担当しているか」を確認するための索引です。
Notion側の最新実験設計との差分は `Docs/experiment_design_sync.md` を参照してください。

`Docs/prototype_spec.md` と `Docs/implementation_plan.md` は初期実装の基準ですが、
2026-06-09時点のNotion実験設計を完全には反映していません。

## 1. 現在地

現在の中心は `Assets/Scenes/Dev_Prototype.unity` です。

- Phase 1: 2枚Display、配置、Hit検出は実装済み
- Phase 2: `RaycastBaseline` は実装済み
- Phase 3: `ExplicitDisplayFocus` のMVPは実装済み
- Phase 4: T1 FocusPointing は試行生成、エラー判定、CSV出力まで実装済み
- T2: YouTube + 比較WebのWebViewセッションは実装済み
- Phase 6: OpenXR Eye Tracking Adapterは存在するが、実験利用にはQuest Pro実機確認が必要
- Phase 7: 実験Scene分割は未完了
- 追加実装: 独立したStudy 1レイアウト選好シーンが存在する

このPhase表現はローカルの旧 `implementation_plan.md` に基づきます。
最新Notion案では、配置予備実験、T1、YouTube + 比較WebのT2、統一ログ、
Quest単体の実験進行UIという単位へ再編されています。

仕様上の必須モジュールとの対応は次のとおりです。

| 仕様上の名前 | 現在の実装 | 状態 |
|---|---|---|
| `ExperimentManager` | `ExperimentManager` | あり |
| `InputManager` | `PrototypeInputManager` | 名前を変えて実装 |
| `GazeProvider` | `GazeProvider`、`EyeTrackingRayAdapter` | あり |
| `RaycastPointer` | `RaycastPointer` | あり |
| `DisplaySurface` | `DisplaySurface` | あり |
| `DisplayManager` | `DisplayManager` | あり |
| `DisplayLayoutManager` | `DisplayLayoutManager` | あり |
| `FocusManager` | `FocusManager` | あり |
| `FocusStateTracker` | `FocusManager` 内の `FocusState` | 独立クラスは未作成 |
| `VirtualCursorController` | `VirtualCursorController` | あり |
| `ClickDispatcher` | `ClickDispatcher` | あり |
| `ScrollController` | `ScrollController` | あり |
| `TaskManager` | `FocusPointingTaskManager`、`WebViewSessionManager` | T1、T2 WebViewとして実装 |
| `ErrorEvaluator` | `ErrorEvaluator` | T1用あり |
| `Logger` | `Logger`、`LayoutPreferenceLogger` | あり |

## 2. 実行時の流れ

### RaycastBaseline

```text
PrototypeInputManager
  -> RaycastPointer
  -> DisplayManager.TryGetFirstDisplayHit()
  -> 最前面の DisplaySurface
  -> ClickDispatcher / ScrollController
  -> FocusPointingTaskManager
  -> ErrorEvaluator
  -> Logger
```

コントローラRayに最初に当たったDisplayだけが対象です。
手前のDisplayが奥のDisplayを遮るベースライン条件をここで再現します。

### ExplicitDisplayFocus

```text
GazeProvider
  -> FocusManager
  -> DisplayManager.GetDisplayHitsAll()
  -> GripでDisplayを確定
  -> VirtualCursorControllerへカーソル位置を渡す
  -> AでClickDispatcher
  -> Trigger + StickでScrollController
  -> FocusPointingTaskManager / Logger
```

視線は候補取得に使われ、クリックとスクロールは
`DisplayManager.FocusedDisplay` に送られます。

現コードではグリップを保持している間、`FocusManager` と
`VirtualCursorController` が視線位置を継続反映します。
仕様の「グリップ押下時だけ再フォーカス」と完全に同じかは、
今後の動作確認対象です。

## 3. Scene

### `Assets/Scenes/Dev_Prototype.unity`

現在のメインSceneで、Build Settingsの有効なScene 0です。

主なHierarchy:

```text
Directional Light
XR Interaction Setup
  Input Action Manager
  XR Interaction Manager
  EventSystem
  XR Origin (XR Rig)
VR Test Environment
Prototype_Root
  Displays
    Display_A_Front
    Display_B_Back
Prototype_Managers
  ExperimentManager
  PrototypeInputManager
  GazeProvider
  DisplayManager
  DisplayLayoutManager
  FocusManager
  RaycastPointer
  VirtualCursorController
  ScrollController
  ClickDispatcher
  Logger
  PrototypeDebugVisualizer
  EyeTrackingRayAdapter
  EditorDebugInputProvider
  FocusPointingTaskManager
  ErrorEvaluator
  PrototypeControlPanel
  VRTaskMenuManager
  T1RayOcclusionLayoutProbe
```

### `Assets/Scenes/LayoutPreferenceStudy.unity`

Study 1用の独立Sceneです。Build Settingsには登録されていますが無効です。

- 3種類の表示配置を順に提示
- レイ、クリック、スクロール、明示フォーカスは使わない
- A/Bまたはキーボードで配置を切り替える
- 表示姿勢と滞在時間を専用CSVへ保存する

### `Assets/Scenes/SampleScene.unity`

Unityテンプレート由来のSceneです。現在の研究プロトタイプの実行経路では使いません。

## 4. 自作スクリプト

### Core

| ファイル | 役割 |
|---|---|
| `Core/InteractionCondition.cs` | 比較条件 `RaycastBaseline` / `ExplicitDisplayFocus` のenum |

### Display

| ファイル | 役割 |
|---|---|
| `Display/DisplaySurface.cs` | 1枚のWorld Space Canvas、HitPlane、カーソル、疑似クリック対象、スクロール状態を管理 |
| `Display/DisplayManager.cs` | Display一覧、Ray hit、フォーカス中Display、カーソル表示、候補表示を一括管理 |
| `Display/DisplayLayoutManager.cs` | HMD基準の2枚Display配置と3つの遮蔽プリセットを管理 |

### Gaze

| ファイル | 役割 |
|---|---|
| `Gaze/GazeProvider.cs` | 視線Rayの共通入口。Eye Tracking、HMD forward、Editor用Camera forwardを切替 |
| `Gaze/EyeTrackingRayAdapter.cs` | OpenXR/Input Systemの `EyeGazeDevice`、権限、追跡状態をGaze Rayへ変換 |

### Input

| ファイル | 役割 |
|---|---|
| `Input/PrototypeInputManager.cs` | Quest右コントローラ入力とEditor入力を統合し、条件ロックも管理 |
| `Input/EditorDebugInputProvider.cs` | Questなしで試すためのキーボード入力を提供 |

### Interaction

| ファイル | 役割 |
|---|---|
| `Interaction/RaycastPointer.cs` | BaselineのコントローラRay、最前面Hit、Rayカーソルを更新 |
| `Interaction/FocusManager.cs` | gaze候補取得、Gripによる明示フォーカス、フォーカス状態を管理 |
| `Interaction/VirtualCursorController.cs` | フォーカスDisplay内の仮想カーソルのwarpとstick移動を管理 |
| `Interaction/ClickDispatcher.cs` | 条件別にRay位置または仮想カーソル位置へクリックを配送 |
| `Interaction/ScrollController.cs` | BaselineはRay hit先、提案手法はフォーカス先へスクロールを配送 |

### Experiment and Task

| ファイル | 役割 |
|---|---|
| `Experiment/ExperimentManager.cs` | 現在条件、現在レイアウト、フレームサンプルを束ねる軽量管理役 |
| `Tasks/FocusPointingTaskManager.cs` | T1の3タスク選択、Training/Main、ターゲット表示、動作量集計、結果CSVを担当 |
| `Tasks/T1TrialSequenceGenerator.cs` | 2 Display × 6位置 × 2サイズ × 4周の96試行を制約付きランダム化 |
| `WebView/WebViewSessionManager.cs` | T2のYouTube + 比較Web統合セッション、操作確認からMainへの連続移行、完了条件、T2 CSVを担当 |
| `Tasks/ErrorEvaluator.cs` | T1クリックをCorrect、DisplayError、TargetError、Missに分類 |
| `Tasks/T1RayOcclusionGeometry.cs` | 手・目・2枚Displayの幾何モデルからRay遮蔽率を計算 |
| `Tasks/T1RayOcclusionLayoutProbe.cs` | T1遮蔽配置の探索、適用、可視化、Scene保存を行う調整用ツール |

### Logging

| ファイル | 役割 |
|---|---|
| `Logging/Logger.cs` | 共通イベント、フレーム、クリック、スクロール、フォーカス、T1結果をCSV保存 |

### Debug and UI

| ファイル | 役割 |
|---|---|
| `Debug/PrototypeDebugVisualizer.cs` | Ray、Hit、候補、フォーカス、現在状態をScene/Game上に可視化 |
| `Debug/PrototypeControlPanel.cs` | Inspectorから条件、視線源、可視化、Task開始をまとめて設定 |
| `Debug/VRTaskMenuManager.cs` | VR内の条件選択、Training/Main開始、終了ボタンを生成・処理 |

### Editor

| ファイル | 役割 |
|---|---|
| `Editor/DevPrototypeSceneBuilder.cs` | `Prototype > Build Dev Prototype Scene` でメインSceneを再構築 |
| `Editor/LayoutPreferenceStudySceneBuilder.cs` | `Prototype > Build Layout Preference Study Scene` でStudy 1 Sceneを再構築 |

### Layout Preference Study

| ファイル | 役割 |
|---|---|
| `LayoutPreference/LayoutPreferenceLayout.cs` | Study 1の配置条件と各Displayの角度・距離設定 |
| `LayoutPreference/LayoutPreferenceStudyManager.cs` | 配置順、HMD基準配置、入力、表示切替、滞在時間を管理 |
| `LayoutPreference/LayoutPreferenceLogger.cs` | Study 1専用CSVを保存 |

## 5. Assets以外

| パス | 役割 |
|---|---|
| `AGENTS.md` | Codex向けの研究目的、実装順、制約 |
| `Docs/prototype_spec.md` | 研究プロトタイプの仕様書 |
| `Docs/implementation_plan.md` | Phase 0から7までの実装計画 |
| `Docs/prototype_design_memo.md` | 仕様化前の設計メモ |
| `README.md` | 実行方法と実装済み機能の説明 |
| `Tools/pull_quest_logs.sh` | Questの `Application.persistentDataPath/Logs` をMacへコピー |
| `Tools/watch_quest_logs.sh` | Quest接続を待ち、接続ごとにログをコピー |
| `Tools/analyze_t1_results.py` | T1ログをcleaned CSVと条件別summaryへ集計 |
| `Analysis/T1/` | 上記Analyzerの出力結果 |
| `Packages/manifest.json` | OpenXR、XRI、TMP、Unity MCP等のPackage定義 |
| `ProjectSettings/` | Unity、Android、XR、Build Settings |

## 6. 外部・生成ファイル

次はプロトタイプの自作ロジックではありません。

| パス | 分類 |
|---|---|
| `Assets/Samples/` | OpenXR/XRI Packageの公式サンプル |
| `Assets/XR/`, `Assets/XRI/` | XR設定、Input Action、Starter Assets |
| `Assets/TextMesh Pro/` | TMP Essentials。現在はGit未追跡 |
| `unity-mcp/` | Unity MCPの外部リポジトリ参照 |
| `Library/`, `Temp/`, `obj/`, `Logs/`, `UserSettings/` | Unityや実行時の生成物。`.gitignore` 対象 |
| `*.csproj`, `*.sln` | Unityが再生成するIDE用ファイル |
| `Dev_Prototype.app/`, `SupineVRdev*.apk` | macOS/Questビルド成果物。現在はGit未追跡 |
| `Assets/Screenshots/` | Unity MCPまたはデバッグで生成した画像 |
| `mono_crash.*.json`, `nested_access.dll` | クラッシュ・一時調査由来と見られるルート直下の成果物 |

## 7. 現在の注意点

1. Git上のフォルダ名は `docs/` ですが、`AGENTS.md` と各説明は `Docs/` を参照しています。
   macOSの大小文字を区別しないファイルシステムでは同じ場所ですが、
   Linux等では別パスになるため、後で表記を統一する必要があります。
2. `Dev_Prototype.unity`、T1関連コード、T2 WebView関連コードには未コミット変更があります。
3. `LayoutPreferenceStudy`、T1遮蔽調整コード、TMP Essentials、分析結果、
   APK等が未追跡です。成果物ごとに「Git管理するもの」と「無視するもの」を決める必要があります。
4. Unity ConsoleにはC#コンパイルエラーはありません。
   OpenXR Androidライブラリの16KB alignment警告は残っています。
5. 仕様にある `Calibration_Debug`、`Exp_FocusPointing`、
   `Exp_AttentionFocus` は独立Sceneとしてまだ存在しません。
6. 最新Notion案では、旧 `Exp_AttentionFocus` の研究課題は主に
   T2「YouTube + 比較Web」へ統合されています。
7. ProposedのGrip操作は、旧仕様の「Grip Downで確定」と、
   最新案の「Grip中だけGaze Mode」の間で記述が異なります。
   現コードはGrip保持中に更新するため、最終仕様の固定と動作テストが必要です。
