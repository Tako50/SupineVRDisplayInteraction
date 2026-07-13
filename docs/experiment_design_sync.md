# 実験設計とUnity実装の同期状況

更新日: 2026-07-06

## 1. この資料の目的

Notion側で実験設計が更新されているため、リポジトリ内の古い仕様書、
現在のUnity実装、最新の研究案を区別して整理する。

現時点では、次の順で参照する。

1. 研究目的・最新の実験構成: Notion「案1」2026-06-11版
2. 入力遮蔽と提案手法の解釈: Notion「研究メモ」2026-06-08版
3. T1試行構成・順序: Notion「Study 1：T1 ポインティングタスク案」2026-06-11版
4. T1遮蔽条件: Notion「T1 Ray遮蔽の幾何モデル」2026-06-02版
5. 実装・ログ・運用要件: Notionの2026-06-01版各ページ
6. 現在動く実装: Unityプロジェクトと `README.md`
7. `prototype_spec.md` と `implementation_plan.md`: 初期設計として参照

`prototype_spec.md` と `implementation_plan.md` は現在の研究設計を
完全には反映していないため、未同期の旧仕様を含む。

## 2. 現在の研究の中心

研究対象は、仰臥位VRで複数の2D仮想ディスプレイを閲覧しながら
断続的に操作する場面である。

最新案では、問題を次の2つの常時依存として整理している。

- controller poseへの常時依存
- gazeへの常時依存

Raycastでは、コントローラ姿勢が入力先DisplayとDisplay内位置の両方を
常に決める。このため、腕・手首の維持負荷、浅い入射角、入力遮蔽、
押下時のRay変位が問題になる。

一方、gazeを常時カーソルやフォーカスへ結びつけると、読む・見る・確認する
ための視線移動が操作へ干渉する。

提案手法の中心は、controller poseにもgazeにも常時依存しない
display-local操作である。

## 3. Proposedの最新解釈

最新のNotion案では、通常時はfocused display内をstickで操作し、
入力先または概略位置を変更したいときだけgrip中にgazeを使う。

```text
通常時:
  focused displayを維持
  stickでdisplay-local cursorを操作

grip中:
  gazeでdisplay候補と概略位置を指定
  cursorをgaze位置へ追従・再配置

grip release:
  gaze modeを終了
  最後に指定したdisplayとcursor位置を維持
```

この解釈では、gazeは「gripを押した瞬間だけ使う」のではなく、
grip中だけ一時的に有効になる。

現在のローカル仕様書は次の方式である。

```text
grip down:
  その瞬間のgaze候補をfocusとして確定

確定後:
  次のgrip downまでfocusを維持
```

現在のUnityコードは、gripを保持している間にfocusとcursorを更新するため、
挙動としては最新案に近い。ただし、focus確定のタイミングを
grip down、grip中、grip releaseのどこに置くかは明文化とテストが必要である。

### 3.1 クリック・WebView入力の試行仕様（2026-07-06更新）

T2 Web Browsingでは、YouTubeのseek bar操作感をRaycastBaselineに近づけるため、
Web UI dragを本番入力に戻す。ただし長いページスクロールはdragではなく相対scroll
として扱い、画面端でtouchを持ち直す問題を避ける。

```text
共通:
  A単独 = cursor位置のclick
  trigger単独 = clickしない
  Web UI drag = WebViewへpointer down / move / upを送る
  scroll = WebViewの相対scroll

RaycastBaseline:
  ray hit中の上下stick = scroll
  trigger hold + Ray移動 = Web UI drag

ExplicitDisplayFocus:
  trigger + 上下stick = focused displayをscroll
  stick押し込み開始 = focused displayのvirtual cursor位置でpointer down
  stick移動 = Web UI drag
  stickが短時間neutralを維持、またはstick再押し = pointer up
```

Unity実装では既存Sceneとの互換性のため `WebViewSessionManager.InputMode` の
`DirectScrollAndSeek` 名称を維持し、その既定挙動を上記のhybrid scroll / dragへ
更新する。比較・ロールバック用に旧 `StickTouchGesture` と
`LegacyDirectScrollAndSubmitDrag` も残す。`TLabWebViewDisplayBridge.TrySeekHorizontal`
は実装上残すが、既定T2操作では直接呼ばない。
クリック入力はT1、T2、疑似2Dコンテンツを含めてAボタンへ統一し、
trigger単独ではclickしない。T2のWeb UI dragは移動量が閾値を超えた時点で
pointer downを開始し、triggerまたはstick押し込みだけでWeb clickが発生しないようにする。
Quest Pro実機でWeb UI dragの安定性、scroll速度、TLabのtexture更新fpsを検証する必要がある。

### 3.2 視覚フィードバックの固定（2026-07-06更新）

実験開始画面ではhighlightとcontroller Rayを参加者・実験者が選択しない。

```text
共通:
  display highlight = 常にON

RaycastBaseline:
  controller Ray = Longを表示

ExplicitDisplayFocus:
  controller Ray = 非表示
```

開始画面はtask、interaction method、T1配置またはT2 content set、開始操作だけを表示する。

## 4. 実験構成の更新

### 4.1 予備実験: Display配置確認

最新案では、本実験前に次の3配置を比較する。

- LeftRight: 左右配置
- UpDown: 上下同一平面配置
- UpDownDepth: 上下 + 奥行き配置

目的は絶対的な最適配置を決めることではなく、本実験で扱う代表配置を
根拠を持って選定することである。

現在の `LayoutPreferenceStudy.unity` は、この更新に対応する実装である。

### 4.2 T1: 複数Display・Target選択

T1はFitts' Lawそのものではなく、複数Display環境で次を評価する。

- target selection time
- success rate
- display error / wrong display selection
- pre-click cursor jitter
- controller movement distance
- controller rotation amount
- wrist / forearm fatigue
- Press Drift
- Down Target Error

2026-07-03の実装確認で、ディスプレイ配置は従来の3配置へ戻した。
直前に共有された前後配置中心の仕様は旧版だったため、配置の正本にはしない。

```text
Task A: LeftRight（左右配置）
Task B: UpDown（上下・同一平面配置）
Task C: UpDownDepth（上下 + 奥行き配置）

2 displays x 6 positions x 2 sizes x 2 cycles
= 48 trials / block

48 trials x 3 tasks x 2 methods
= 288 main trials / participant
```

各ブロックは24試行の2周で、各周はDisplay・位置・サイズの全組合せを
1回ずつ含む。位置は `x = 0.10 / 0.50 / 0.90`、
`y = 0.20 / 0.80` の3 x 2、サイズは視角ベースの
`Small 1.5度 / Large 3度` とする。

タスク順は `ABC / BCA / CAB / ACB / CBA / BAC` の6種類、手法順は
`RayFirst / ExplicitFirst` の2種類を組み合わせてカウンターバランスする。
各ブロックの試行順はseed付き制約ランダム化で生成し、D1→D2とD2→D1、
同一Display内移動とDisplay間移動、サイズ、Task CのinputOccluded試行が
偏らない候補を選ぶ。

Task CではDisplay 2の下段3点 `(0.10, 0.80) / (0.50, 0.80) / (0.90, 0.80)`
をinputOccludedとし、1ブロック12試行を含む。Task A/BのinputOccludedは0である。
`FocusPointingTaskManager` はこの48試行設計を既定でコード生成し、旧CSV方式は
比較・ロールバック用に残す。TrainingはInspectorで6〜12試行に設定する。

### 4.3 T2: YouTube + 自作比較Webの半自由タスク

2026-07-02更新のNotion「T2 指示セット案：YouTube＋自作Web」を現在の正本とする。
T2では厳密なポインティング性能ではなく、
動画を流し見しながら複数Displayを継続利用する場面の使いやすさを評価する。

```text
奥 Display_B_Back:
  実際のYouTubeページ

手前 Display_A_Front:
  実験用の製品比較Webページ

完了:
  V2Dと半自由比較で候補を追加し、候補リストから最終候補1個を確定

制約:
  候補選択前にYouTubeを最低1回、再生・停止・シークする
```

コンテンツセットは次の2種類で、Webのセクション順、候補数、ボタン位置を揃える。

- Set A: 2024年版キャンプギア動画
- Set B: 2025年版キャンプギア動画

2026-07-13更新として、T2のTrainingとMainは1つの連続セッションへ統合し、
各段階は固定時間ではなく対応する操作の達成によって進行する。
開始後は `P01 YouTube停止 → P02 YouTube再生 → P03 10秒スキップ → P04 シークバー操作で少し戻す → P05 Webスクロール → P06 商品詳細 → P07 候補追加 → P08 候補削除`
の操作確認を行い、P08完了時にWebViewを閉じたり条件選択へ戻ったりせず、そのままMainへ移行する。
操作確認で追加した候補はMain移行時に消去する。MainではV2D達成後にD2V、D2V達成後に半自由比較へ進み、
半自由比較中に追加候補を1件登録すると候補確定へ進む。指示達成後の固定待機時間は使用しない。
最終候補を確定した時点で計測と結果ログを終了し、選択商品の画像、商品名、ブランド、価格、サイズ、重量を手前Webへ表示したままにする。
終了後アンケート回答後に `候補リストを削除してホームに戻る` を押すと、保存済み候補を全消去してWebをホーム状態へ戻し、Unityの条件選択へ戻る。
この終了後確認画面では全体・段階タイマーを進めない。
全体10分制限とカウントダウンは維持する。Practice、V2D、D2V、半自由比較、候補確定には
段階タイムアウトを設定できる構造を維持するが、各秒数は未決定であり、確定前に確認する。
Unityでは未決定値を `0 = 無効` として保持し、経過時間は分析用ログへ記録する。

WebではA/Bセットを混在させ、カテゴリごとにNotionで指定した固定順で表示する。
`video_set`、`video_year`、`item_id`、`start_time`、`end_time`、`appearance_duration` は
内部データとログにのみ保持し、商品カード、詳細、候補、画像代替表示には出さない。
V2DはSet Aで4分44秒〜6分20秒付近からA03、Set Bで8分46秒〜10分30秒付近からB05を探す。
D2VはSet Aで「居住・寝具」の上から2番目のA08、Set Bで「火器・調理用品」の上から4番目のB09を確認する。

Unityでは `WebViewSessionManager` がA/B、Training進行、各段階の完了条件、
YouTube/WebのJavaScript telemetry、T2専用CSVを管理する。比較ページは現在、
React/Viteで起動した共通20商品Webを `secondaryInitialUrl` から読み込む。
固定YouTube動画はSet Aを `https://www.youtube.com/watch?v=oCKnZl-XT1Y`、
Set Bを `https://www.youtube.com/watch?v=wIHPxl6OPOc` とする。

### 4.4 旧T3 AttentionFocus

ローカル仕様書では、視覚的注意と入力フォーカスの分離を
独立したT3としていた。

最新案では、この研究課題はYouTube視聴と比較Web閲覧を並行する現在のT2へ統合されている。

したがって `Exp_AttentionFocus` を独立Sceneとして作ることは、
現時点の優先実装ではない。

## 5. Display配置と入力遮蔽

T1のNotion幾何モデルでは次を区別する。

- 視覚的遮蔽: 目からDisplayが見えなくなること
- 入力遮蔽: controller rayが奥Displayへ届く前に手前Displayへ当たること

設計目標:

```text
見かけ角度:
  horizontal = 40 degrees
  vertical = 22.5 degrees

目から見たDisplay同士の視覚的遮蔽:
  0%

controller基準の奥Display全体のRay遮蔽率:
  約30%
```

ターゲット分類:

```text
BackClear:
  手位置サンプル全てでRayがclear

BackOccluded:
  手位置サンプル全てでRayがoccluded

Ambiguous:
  手位置によって判定が変わるため実験対象から除外
```

上記は配置調整時の幾何分類である。本番T1ログでは分析単位を単純化し、
Task Cの指定された奥側下段3点だけを `inputOccluded`、それ以外を `none`
として記録する。旧分類は `legacyOcclusionType` 列に互換情報として残す。

現在の `T1RayOcclusionGeometry` と `T1RayOcclusionLayoutProbe` は
この幾何モデルを実装するためのコードである。

## 6. 実装との対応

| 項目 | 現在の実装 | 判定 |
|---|---|---|
| 3種類の配置予備実験 | `LayoutPreferenceStudy` | 実装済み |
| Raycast Baseline | `RaycastPointer` ほか | 実装済み |
| 視覚フィードバック固定 | Highlight常時ON、Baseline Long Ray、Proposed Ray非表示 | 実装済み |
| display-local cursor | `VirtualCursorController` | 実装済み |
| grip中gaze mode | 現コードはgrip中に更新 | 要仕様固定・テスト |
| T1 3タスク・48試行/ブロック | `FocusPointingTaskManager`、`T1TrialSequenceGenerator` | 実装済み |
| T1 Training 6〜12試行 | `FocusPointingTaskManager` | 実装済み |
| T1遮蔽幾何モデル | `T1RayOcclusionGeometry` | 実装済み |
| T1 movement/rotation集計 | `FocusPointingTaskManager` | 実装済み |
| pre-click jitter | なし | 未実装 |
| Press Drift | なし | 未実装 |
| Down Target Error | なし | 未実装 |
| T2 YouTube + 比較Web | `WebViewSessionManager` | A/B・Training・完了条件・CSV実装済み、Quest確認が必要 |
| participant flow | 簡易VR menuのみ | 未実装 |
| trial/event/trajectory統一ログ | 個別CSVのみ | 未実装 |
| EyeTracking実験利用確認 | Adapterあり | Quest Pro確認が必要 |
| 紙質問紙運用 | アプリ外 | UI案内と転記形式が未実装 |

現在のT2は `Display_B_Back` にYouTube、`Display_A_Front` にA/Bの比較ページを表示する。
VRメニューのT2枠は `WebViewSessionManager` のみを起動する。

## 7. ログの最新要求

2026-06-01版の実装設計では、ログを次の4種類へ統一する。

```text
trial_log.csv
event_log.csv
trajectory_log.csv
questionnaire_log.csv
```

特に追加が必要な値:

- participant_id
- condition_order
- block_index
- task
- mode / run_status
- exclude_reason
- target_display
- occlusion_type
- controller_movement_distance
- controller_rotation_amount
- cursor_movement_distance
- focus_switch_count
- grip_count
- cursor_warp_count
- gaze_valid
- wrong_display_scroll

最新研究メモで追加された候補:

- pre-click cursor jitter
- Press Drift
- Down Target Error

## 8. 本実験の進行

2026-06-01版の管理UI仕様では、Quest単体で次を進める。

```text
Experiment Settings
Condition 1
  T1 Practice
  T1 Main
  T2 Practice
  T2 Main
  Questionnaire
Break
Condition 2
  T1 Practice
  T1 Main
  T2 Practice
  T2 Main
  Questionnaire
Final Questionnaire / Interview
Finish
```

必要な管理情報:

- participant_id
- condition_order: AB / BA
- block_index
- condition
- task
- mode: Practice / Main
- log status

T1 Practiceは最低回数に達しても自動終了せず、実験者が終了する。
T2の操作確認はP08完了時に自動でMainへ進む。

## 9. まだ固定が必要な項目

実装を大きく進める前に、次を研究設計として固定する必要がある。

1. Proposedのfocus確定タイミング
   - grip downで確定
   - grip中に連続更新しreleaseで確定
2. T2完了後の選択理由・主観評価を紙、別端末、Quest内UIのどれで回収するか
3. Press DriftとDown Target Errorを主要評価指標に含めるか

## 10. 推奨する次の整理順

1. 上記項目をNotion上で確定する
2. `prototype_spec.md` を最新設計へ更新する
3. `implementation_plan.md` を現実装からの移行計画へ置き換える
4. Proposedのgrip semanticsをPlay ModeとQuest Proで検証する
5. 統一ログ基盤を実装する
6. Quest Proで固定動画のYouTube telemetryと比較Web操作をパイロットする
7. T2の主観評価回収方法とログ値を確定する
8. Quest単体の実験進行UIを実装する

## 11. Notion参照ページ

- [案1 2026-06-09整理版](https://app.notion.com/p/2b186822de6e800e8b16cab7b2f330cb)
- [研究メモ: 入力遮蔽とフォーカス/ポインティング整理](https://app.notion.com/p/36086822de6e8136afdbd6db06a96536)
- [T1 Ray遮蔽の幾何モデル](https://app.notion.com/p/36686822de6e814d877cd98df7846fb0)
- [プロトタイプ実装設計書](https://app.notion.com/p/35d86822de6e8186bd56edf7714d6e30)
- [RQ・仮説・貢献](https://app.notion.com/p/36e86822de6e814caf26facc5ea86abf)
- [GitHub実装と実験計画の差分](https://app.notion.com/p/36d86822de6e8118a2a5e701aaae4139)
- [実装側画面遷移・管理UI仕様](https://app.notion.com/p/36d86822de6e81f4ba7ac00b485d0f51)
- [パイロット実験計画](https://app.notion.com/p/37286822de6e814db057d4d6c6df5c48)
- [T2 指示セット案：YouTube＋自作Web](https://app.notion.com/p/38886822de6e81ccb5e5fff94264f79f)
