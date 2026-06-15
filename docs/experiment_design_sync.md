# 実験設計とUnity実装の同期状況

更新日: 2026-06-12

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

2026-06-11版で、次の試行構成へ更新された。

```text
2 displays x 9 positions x 3 sizes x 2 cycles
= 108 trials / condition
```

各周は54試行で、Display・位置・サイズの全組合せを1回ずつ含む。
ターゲット位置は各Displayの正規化座標 `0.1 / 0.5 / 0.9` の3 x 3、
サイズは視角ベースの `Small 1度 / Medium 2度 / Large 3度` とする。

本番順序はList A / B / C / Dの4種類を事前生成し、条件の実施順に応じて
割り当てる。各Listは108試行である。

現在の `FocusPointingTaskManager` はCSVからこれらの順序を読み込む。
開発時の初期設定は両操作条件ともMain List Aである。
TrainingはList Eの108試行を使用する。

### 4.3 T2: 実利用マイクロ操作

旧仕様のT2は、指定Displayを連続スクロールする単一タスクだった。

最新案では、T2を次の2タスクとして整理している。

#### T2-A: 参照しながらリスト選択

- main displayに選択指示を表示
- sub displayに縦リストを表示
- main displayを参照しながらsub displayをscroll
- 指定項目を選択
- 視線を参照先へ移しても操作先を維持できるか評価

#### T2-B: 動画シークバー操作

- 動画プレイヤー風UIを使用
- seek barを目標位置へ合わせる
- 細長いUIでの連続的な位置調整を評価

旧T2 Continuous Scrollingは、最新T2-Aの一部として再利用できる。
T2-Bは未実装である。

### 4.4 旧T3 AttentionFocus

ローカル仕様書では、視覚的注意と入力フォーカスの分離を
独立したT3としていた。

最新案では、この研究課題は主にT2-Aへ統合されている。

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

現在の `T1RayOcclusionGeometry` と `T1RayOcclusionLayoutProbe` は
この幾何モデルを実装するためのコードである。

## 6. 実装との対応

| 項目 | 現在の実装 | 判定 |
|---|---|---|
| 3種類の配置予備実験 | `LayoutPreferenceStudy` | 実装済み |
| Raycast Baseline | `RaycastPointer` ほか | 実装済み |
| display-local cursor | `VirtualCursorController` | 実装済み |
| grip中gaze mode | 現コードはgrip中に更新 | 要仕様固定・テスト |
| T1 108試行・順序List A-D | `FocusPointingTaskManager` | 実装済み |
| T1 Training List E 108試行 | `FocusPointingTaskManager` | 実装済み |
| T1遮蔽幾何モデル | `T1RayOcclusionGeometry` | 実装済み |
| T1 movement/rotation集計 | なし | 未実装 |
| pre-click jitter | なし | 未実装 |
| Press Drift | なし | 未実装 |
| Down Target Error | なし | 未実装 |
| T2-A list task | `ReferenceListTaskManager` | MVP実装済み・試行数は暫定 |
| T2-B seek bar task | なし | 未実装 |
| participant flow | 簡易VR menuのみ | 未実装 |
| trial/event/trajectory統一ログ | 個別CSVのみ | 未実装 |
| EyeTracking実験利用確認 | Adapterあり | Quest Pro確認が必要 |
| 紙質問紙運用 | アプリ外 | UI案内と転記形式が未実装 |

T2-A MVPは、片方の表示に参照項目、もう片方にスクロール可能な
40項目リストを提示する。表示誤り、項目誤り、Miss、
誤表示スクロールジェスチャを記録し、TrainingとMainを
VRメニューから開始できる。Mainは現時点で各表示5試行、
合計10試行だが、正式な試行数はNotion側の決定後に更新する。

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

Practiceは最低回数に達しても自動終了せず、実験者が終了する。

## 9. まだ固定が必要な項目

実装を大きく進める前に、次を研究設計として固定する必要がある。

1. Proposedのfocus確定タイミング
   - grip downで確定
   - grip中に連続更新しreleaseで確定
2. T2はT2-AとT2-Bの両方を実施するか
3. T2-A/Bの試行数
4. T2-BでRaycastとProposedのcursor操作をどう対応させるか
5. Press DriftとDown Target Errorを主要評価指標に含めるか
6. 旧T3を正式に廃止し、T2-Aへ統合するか

## 10. 推奨する次の整理順

1. 上記8項目をNotion上で確定する
2. `prototype_spec.md` を最新設計へ更新する
3. `implementation_plan.md` を現実装からの移行計画へ置き換える
4. Proposedのgrip semanticsをPlay ModeとQuest Proで検証する
5. 統一ログ基盤を実装する
6. T2-Aの試行数を確定し、Quest Proでパイロットする
7. T2-Bの採用決定後に実装する
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
