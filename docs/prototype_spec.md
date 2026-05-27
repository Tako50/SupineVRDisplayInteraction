# プロトタイプ実装仕様書

## 目的

このプロトタイプは、研究「仰臥位VRにおける複数2D仮想ディスプレイの入力フォーカス管理」のためのUnity実験環境である。

目的は、WISS投稿に向けて、標準レイキャスティング操作と提案手法をUnity上で比較できる最小実験環境を作ること。

本プロトタイプでは、仰臥位VRにおける以下の問題を実験可能な形で再現する。

- コントローラを上向きに維持し続けることによる身体的負荷
- 複数の2D仮想ディスプレイの中から、どのディスプレイに入力を送るかが未確定になる問題
- 入力先ディスプレイの決定と、そのディスプレイ内のターゲット接近を同時に行う必要がある問題
- 近方・遠方に配置された複数ディスプレイが視野上で部分的に重畳し、レイキャスティングで意図しないディスプレイに入力が送られる問題
- 視覚的注意と入力フォーカスがズレたときに、誤スクロールや誤フォーカスが発生する問題

## 実装対象

### 対象にするもの

- Meta Quest Pro上で動作するUnity VRアプリ
- 2枚の2D仮想ディスプレイ
- 標準レイキャスティング操作
- 提案手法：明示的ディスプレイフォーカス操作
- ディスプレイレベルの入力フォーカス管理
- 視線による入力先ディスプレイ決定とカーソル初期配置
- スティックによるフォーカス後のカーソル移動・クリック・スクロール
- ディスプレイ選択＋ターゲット選択タスク
- 視覚的注意と入力フォーカスがズレるタスク
- Display error / Target error / 誤スクロールを含む実験ログ出力

### 対象にしないもの

- 実ブラウザの完全再現
- Webページレンダリング
- 動画プレイヤーの完全実装
- ディスプレイの自由配置・リサイズ・角度調整
- 高度なUIデザイン

実験の目的は「日常利用に近い2D操作の負荷比較」であるため、ブラウザや動画アプリそのものを作り込むのではなく、クリック・スクロール・画面切替が発生する疑似2Dコンテンツを作る。

## 開発環境

- Unity 2022 LTS または Unity 2021 LTS
- Meta Quest Pro
- Meta XR SDK
- XR Interaction Toolkit または Meta XR Interaction SDK
- OpenXR
- TextMeshPro
- CSVログ出力用の自作Logger

## 入力デバイス

- HMD
- 右コントローラ
- 右スティック
- 右グリップ
- 右トリガー
- Aボタン
- 視線入力

## 視線入力の扱い

Meta Quest ProのEye Trackingを原則として使用する。

本研究の主張は「複数2D仮想ディスプレイにおける入力フォーカス管理」であるため、予備実験および本実験では、HMD forwardではなく実視線を用いる。視線は単なるカーソルワープではなく、入力先ディスプレイの候補取得とディスプレイ内カーソル初期位置の指定に用いる。

ただし、開発初期のデバッグやEye Trackingが一時的に不安定な場合に備えて、`GazeProvider` は以下の2種類の入力源を切り替えられるようにする。

1. 開発用フォールバック：HMDのforward方向をgaze rayとして扱う
2. 実験用：Meta Quest ProのEye Tracking APIからgaze rayを取得する

HMD forwardは実験評価用ではなく、実装確認・デバッグ・故障時のフォールバックに限定する。予備実験以降は、原則としてEye Tracking版でフォーカス切替・カーソルワープ・スクロール・ログ記録を行う。

```csharp
public enum GazeSource
{
    HmdForward,
    EyeTracking
}

public interface IGazeProvider
{
    Ray GetGazeRay();
    bool IsEyeTrackingAvailable();
    GazeSource CurrentGazeSource { get; }
}
```

## シーン構成

### 1. Dev_Prototype

開発確認用シーン。

- 2枚の仮想ディスプレイを配置
- レイキャスト条件と提案手法条件をキーまたはUIで切替
- ログは簡易出力
- タスク成功・失敗の確認用

### 2. Exp_FocusPointing

ディスプレイフォーカス＋ターゲット選択タスク用シーン。

- 近方・遠方に配置された2枚のディスプレイ
- 指定されたディスプレイ上にターゲットを表示
- 被験者は入力先ディスプレイをフォーカスし、その中のターゲットをクリックする
- ターゲットサイズと重畳条件を複数条件で変える
- 操作時間、Display error、Target error、フォーカス切替回数、カーソルワープ回数を記録する

### 3. Exp_FocusScroll

フォーカス維持・スクロールタスク用シーン。

- 長い疑似Webページやコメント欄を2枚のディスプレイに表示
- 指定されたディスプレイをフォーカスし、指定項目までスクロールする
- 視線が別ディスプレイへ移っても入力フォーカスが維持されるかを確認する
- 一定時間または一定試行数を連続で行う
- 誤スクロール、フォーカス維持失敗、前半・後半でのパフォーマンスと疲労変化を記録する

### 3.5. Exp_AttentionFocus

視覚的注意と入力フォーカスの分離を評価するシーン。

- 遠方ディスプレイに動画・文章・注視対象を表示する
- 近方ディスプレイにコメント欄・リスト・操作パネルを表示する
- 被験者は遠方ディスプレイを見ながら、近方ディスプレイをスクロールまたは操作する
- 視線が操作対象から外れたときに、入力フォーカスが維持されるかを記録する

### 4. Calibration_Debug

視線・入力・座標確認用シーン。

- gaze rayの可視化
- controller rayの可視化
- display hit位置の可視化
- local座標とnormalized座標の表示
- スティック入力値の表示

## オブジェクト構成

```plain text
XR Rig
├── Camera Offset
│   └── Main Camera
├── Right Controller
└── Left Controller

ExperimentManager
InputManager
GazeProvider
RaycastPointer
DisplayManager
DisplayLayoutManager
FocusManager
FocusStateTracker
VirtualCursorController
ClickDispatcher
ScrollController
TaskManager
ErrorEvaluator
Logger

Displays
├── Display_A_Front
│   ├── Canvas
│   ├── ContentRoot
│   ├── Cursor
│   └── HitPlane
└── Display_B_Back
    ├── Canvas
    ├── ContentRoot
    ├── Cursor
    └── HitPlane
```

## 2D仮想ディスプレイ設計

### 表現方法

各ディスプレイはWorld Space Canvasで作る。

- Canvas Render Mode: World Space
- Canvasサイズ: 1920 x 1080相当
- 物理サイズ: 実験中に見やすい大きさに調整
- Canvasの前面に透明なHitPlaneを置く
- HitPlaneとの交点をCanvas内座標へ変換する

### 配置

仰臥位を想定し、ユーザーの顔の上方に2枚のディスプレイを置く。

基本配置：

- `Display_A_Front`: 手前・下側・小さめ
- `Display_B_Back`: 奥・上側・大きめ

または、実験条件に合わせて以下の役割に固定する。

- 遠方ディスプレイ：動画・大画面相当
- 近方ディスプレイ：ブラウザ・コメント欄・操作パネル相当

重要なのは、視覚的には両方見えるが、コントローラレイでは手前ディスプレイが奥ディスプレイへのレイを遮る状況を作ることである。

### 遮蔽条件の実装方針

ベースラインと提案手法でray hitの扱いを明確に分ける。

#### RaycastBaseline

- controller rayに対して最前面のhitのみを操作対象にする。
- 標準的なVR UIで起こる「手前Displayにrayが遮られ、奥Displayを直接操作しにくい」状況を再現する。

#### ExplicitDisplayFocus

- gaze rayに対してRaycastAll相当の処理を行い、視線方向上に存在する複数のDisplay候補を取得する。
- 重なり領域では、前面Displayを一時的に半透明化し、候補提示を確認したうえでグリップ押下によって入力先Displayを確定できるようにする。
- これは「視線なら物理的遮蔽を無視できる」という主張ではない。視線をフォーカス候補の取得に使い、操作対象の確定はグリップ入力で行うという設計差分である。

### Display配置プリセット

`DisplayLayoutManager` が配置プリセットを管理する。

```csharp
public enum DisplayLayoutPreset
{
    NoOcclusion,
    PartialOcclusion,
    StrongOcclusion
}
```

- `NoOcclusion`: 2枚のDisplayを上下に配置し、前後差と角度差はつけない。座標変換・クリック・スクロール確認用。
- `PartialOcclusion`: 2枚のDisplayを上下に配置し、やや前後差をつける。角度差はつけない。開発・練習・調整用。
- `StrongOcclusion`: 手前Displayが奥Displayの一部をray的に遮る。実験用の基本配置。

Displayの位置は、実験中に参加者が自由に動かす機能は作らない。自由移動を許すと、遮蔽の強さ、Display距離、角度、ターゲットサイズの見え方、rayの当たりやすさが変化し、実験条件が揺れるためである。

配置はワールド絶対座標ではなく、HMD基準で決める。

```plain text
center = HMD position + HMD forward * distance
```

そのうえで、HMDのup/down方向とforward方向にDisplay_A_FrontとDisplay_B_Backをずらす。

## 入力マッピング

### RaycastBaseline：標準レイキャスティング

| 操作 | 入力 | 実装 |
|---|---|---|
| 対象指定 | 右コントローラの向き | controller rayをHitPlaneへ飛ばす |
| カーソル移動 | controller rayの交点 | ray hit位置にカーソルを表示 |
| クリック | Aボタンまたは右トリガー短押し | hit中のUI要素をクリック |
| スクロール | rayを当てた状態でスティック | hit中のディスプレイをスクロール |
| フォーカス切替 | rayが当たったディスプレイ | 最前面hitを操作対象にする |

この条件では、常にコントローラをディスプレイ方向へ向ける必要がある。

### ExplicitDisplayFocus：提案手法

| 操作 | 入力 | 実装 |
|---|---|---|
| 入力候補取得 | 視線 | gaze rayと複数Displayの交点候補を取得する |
| ディスプレイフォーカス確定 | 右グリップ | gaze候補から入力先Displayを確定する |
| カーソル初期配置 | 右グリップ確定時 | focus確定と同時に、該当Display内のgaze hit位置へcursorを配置する |
| カーソル微調整 | 右スティック | focus中のDisplay内で相対移動する |
| クリック | Aボタンまたは右トリガー短押し | focus中Displayのcursor位置にあるUI要素をクリックする |
| スクロール | 右トリガー＋右スティック上下 | focus中Displayをスクロールする。視線が他Displayへ移っても、明示的にfocus変更されるまでは入力先を維持する |
| フォーカス解除・再指定 | 再度右グリップ | 現在のgaze候補に基づいて入力先Displayとcursor位置を更新する |

この条件では、コントローラの位置・向きは操作対象の指定に使わない。

視線は、入力先ディスプレイ候補の取得と、ディスプレイ内カーソル初期位置の指定に使う。ただし、視線が当たっただけではフォーカスを変更しない。グリップボタンが押された時点で、現在の視線候補を入力先ディスプレイとして確定する。

## 実装モジュール設計

### 1. InputManager

責務：

- 右スティック値の取得
- Aボタン押下の取得
- 右グリップ押下の取得
- 右トリガー押下量の取得
- 現在の操作条件を返す

公開値：

```csharp
Vector2 RightStick;
bool AButtonDown;
bool GripDown;
float TriggerValue;
bool IsTriggerPressed;
```

### 2. GazeProvider

責務：

- Eye Tracking APIからgaze rayを取得
- HMD forwardを使った開発用フォールバックgaze rayの生成
- gaze rayの安定化
- gaze rayの可視化
- gaze sourceの切替

最低限のインターフェース：

```csharp
public enum GazeSource
{
    HmdForward,
    EyeTracking
}

Ray GetGazeRay();
bool IsEyeTrackingAvailable();
GazeSource CurrentGazeSource { get; }
```

### 3. DisplaySurface

1枚の仮想ディスプレイを表す。

責務：

- HitPlaneとの交点計算
- world座標からdisplay local座標への変換
- normalized座標への変換
- カーソル表示
- クリック対象の判定
- スクロール量の反映
- 透明度変更

保持情報：

```csharp
string DisplayId;
Canvas Canvas;
RectTransform ContentRoot;
RectTransform Cursor;
Collider HitPlane;
float Alpha;
bool IsFocused;
```

### 4. DisplayManager

責務：

- すべてのDisplaySurfaceを保持
- rayとの交差判定
- 最前面hitの取得
- gaze hit候補の取得
- 現在focus中のDisplayを返す

### 4.5. DisplayLayoutManager

責務：

- `NoOcclusion / PartialOcclusion / StrongOcclusion` のpresetを保持する
- HMD基準でDisplay群の基準位置を計算する
- Display_A_FrontとDisplay_B_Backの位置・回転・サイズをpresetに応じて設定する
- 実験中はDisplay配置を固定する
- 開発中のみpreset切替によって配置を調整できるようにする

### 5. FocusManager

提案手法の明示的ディスプレイフォーカスを管理する。

責務：

- gaze rayがどのDisplay候補に当たっているか判定する
- 視線方向上に複数Display候補がある場合、候補リストを保持する
- 入力先Displayの確定と、Display内cursor初期位置の決定を同時に行う
- focus確定後は、視線が別Displayへ移っても入力先Displayを維持する
- gaze hitだけではfocusを変更せず、grip入力によってのみ入力先Displayを確定・変更する
- overlap領域では、前面ディスプレイの一時透過や候補ハイライトを行う
- グリップ押下時にfocusを切り替える
- focus切替時にcursorをgaze位置へwarpする

状態：

```plain text
NoCandidate
GazeOnSingleDisplay
GazeOnMultipleDisplays
FocusPreview
FocusedLocked
```

基本フロー：

1. gaze rayを取得
2. DisplayManagerでhit候補を取得する
3. 候補が1つなら、そのDisplayをfocus candidateにする
4. 候補が複数なら、近方Display/遠方Displayの候補を記録し、必要に応じて前面Displayを半透明化する
5. gripが押されたら、現在の視線候補から入力先Displayを確定する。gaze hitだけではfocusを変更しない
6. focus変更と同時に、該当Display内のgaze hit位置へcursorを初期配置する
7. focus確定後はFocusedLocked状態になり、stick / click / scrollはfocus中Displayへ送る
8. 視線が別Displayへ移っても、gripで再指定されるまでは入力先Displayを維持する

初期値案：

- dwell time: 0.3〜0.5秒。ただし、透過プレビューや候補提示の安定化にのみ用い、フォーカス確定には用いない
- 透明化alpha: 0.35〜0.5
- focus確定後のalpha復帰: 即時または0.2秒補間
- focus lock: 明示的な再フォーカスまで維持

### 6. VirtualCursorController

責務：

- focus中Displayのcursor座標を保持
- grip確定時にgaze位置へwarp
- stick入力でcursorを相対移動
- Display範囲外へ出ないようにclamp
- カーソル速度を調整

カーソル移動式：

```plain text
cursor += rightStick * cursorSpeed * deltaTime
cursor = clamp(cursor, displayRect)
```

最初は加速度なしで実装する。

### 7. ClickDispatcher

責務：

- Aボタン押下または右トリガー短押しを検出
- 現在の条件に応じてクリック位置を決める
- ベースラインではray hit位置
- 提案手法ではvirtual cursor位置。右トリガー＋スティック上下でスクロールした場合、トリガーを離してもクリック扱いにしない
- UI要素またはタスクターゲットへクリックイベントを送る
- ミスクリックをLoggerへ送る

### 8. ScrollController

責務：

- ベースラインではray hit中Displayをスクロール
- 提案手法ではfocus中Displayをスクロール
- triggerを押している間だけstick上下をscrollに割り当てる
- スクロール位置を記録する

スクロール式：

```plain text
scrollDelta = rightStick.y * scrollSpeed * deltaTime
scrollPosition += scrollDelta
```

### 9. RaycastPointer

責務：

- 右コントローラからrayを飛ばす
- 最初に当たったDisplayを操作対象にする
- hit位置へカーソルを表示する
- ray lineを表示する
- rayが遮蔽される状況をそのまま再現する

提案手法では、このrayは操作対象指定には使わない。ただしログ用にcontroller rayの向きは記録する。

### 10. Logger

責務：

- フレームごとの入力・姿勢ログ
- イベントログ
- タスク結果ログ
- 条件・参加者ID・試行番号の記録

## 条件切替

```csharp
enum InteractionCondition
{
    RaycastBaseline,
    GazeFollowFocus,          // optional
    ExplicitDisplayFocus      // proposed
}
```

最小実装では `RaycastBaseline` と `ExplicitDisplayFocus` の2条件を優先する。時間があれば、視線追従型フォーカスとの比較として `GazeFollowFocus` を追加する。

## タスク設計

### T1: FocusPointing / ディスプレイ選択＋ターゲット選択

目的：

- 2枚のディスプレイを切り替えながら、指定ターゲットをクリックする性能を測る。
- Display error と Target error を分けて測る。

測定：

- task completion time
- Display error
- Target error
- focus switch count
- cursor warp count
- cursor correction distance after warp
- controller movement amount

### T2: FocusScroll / フォーカス維持スクロール

目的：

- 指定Displayをスクロールするタスクで、誤スクロールとフォーカス維持を評価する。
- 視線が別Displayへ移っても、入力フォーカスが維持されるかを見る。

測定：

- scroll completion time
- wrong display scroll count
- focus maintenance success rate
- scroll interruption count
- first-half / second-half performance change
- subjective fatigue after block

### T3: AttentionFocus / 視覚的注意と入力フォーカスの分離

目的：

- 遠方Displayを見ながら近方Displayをスクロールまたは操作する。
- 視覚的注意と入力フォーカスがズレた状況で、提案手法が有効かを評価する。

測定：

- wrong scroll rate
- focus maintenance success rate
- gaze transition count
- task completion time
- subjective workload

T3は後回しでよい。最初はT1/T2を優先する。

## エラー定義

### Display error

指定された入力先Displayとは異なるDisplayに入力した回数または割合。

### Target error

正しいDisplay内で、指定ターゲット以外をクリックした回数または割合。

### 誤スクロール

指定外のDisplayをスクロールした回数または割合。

### フォーカス維持失敗

明示的にfocus変更していないにもかかわらず、入力が意図しないDisplayへ送られた状態。

## ログ設計

### Event log

クリック、スクロール、フォーカス変更、エラー発生などのイベント単位ログ。

推奨列：

```csv
timestamp,participantId,condition,scene,taskType,trialIndex,eventType,targetDisplayId,actualDisplayId,displayError,targetError,wrongScroll,cursorX,cursorY,gazeHitDisplayId,rayHitDisplayId
```

### Trial log

1試行ごとの結果ログ。

推奨列：

```csv
participantId,condition,scene,taskType,trialIndex,targetDisplayId,targetId,success,completionTime,displayErrorCount,targetErrorCount,wrongScrollCount,focusSwitchCount,cursorWarpCount,cursorCorrectionDistance,controllerMovementDistance
```

### Frame log

必要に応じて、身体動作量や入力状態を分析するためのフレームログを出す。

推奨列：

```csv
timestamp,participantId,condition,scene,trialIndex,hmdPosX,hmdPosY,hmdPosZ,controllerPosX,controllerPosY,controllerPosZ,controllerRotX,controllerRotY,controllerRotZ,controllerRotW,rightStickX,rightStickY,triggerValue,currentFocusDisplayId,gazeCandidateDisplayId,rayHitDisplayId
```
