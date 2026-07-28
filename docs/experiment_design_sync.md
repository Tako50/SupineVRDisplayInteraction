# 実験設計とUnity実装の同期状況

更新日: 2026-07-28

明日使用するProposed操作プロトコル版: `EDF-2026-07-28-v1`

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

## 3. Proposedの確定仕様（2026-07-28）

通常時はfocused display内をstickで操作し、
入力先または概略位置を変更したいときだけgrip中にgazeを使う。

```text
通常時:
  focused displayを維持
  stickでdisplay-local cursorを操作

grip中:
  有効なEye Trackingでdisplay候補と概略位置を連続更新
  focusとcursorをgaze位置へ追従・再配置

grip release:
  gaze modeを終了
  最後に指定したdisplayとcursor位置を維持
```

focus確定タイミングは、`grip down`時の1回だけではなく
`grip held`中の連続更新に固定する。`grip release`時に追加の再計算はせず、
最後の有効な更新結果がそのまま確定状態になる。gripを保持していない間は、
gazeだけでfocusやcursorを変更しない。

画面外視線とEye Tracking瞬断の扱いは次に固定する。

```text
有効なgazeがDisplayへ直接hit:
  直接hitを最優先する
  grip中ならそのdisplayとhit位置へfocus/cursorを連続更新

有効なgazeが全Displayの外:
  gaze rayから各Display矩形までの角度距離を比較する
  3.0度以内なら最寄りDisplayを候補にし、cursorを最寄りの表示端へclampする
  画面間の境界では、別Displayが0.5度以上近くなるまで現在のfocused/candidate displayを優先する
  3.0度を超える場合は候補なしとし、既存focus/cursorは更新しない

Eye Trackingがinvalid / untracked:
  focus、candidate、cursorを更新せず、最後の有効状態を保持する
  実験中はHMD forwardへfallbackしない
  まだfocusがない場合は、invalid gazeから新規focusを作らない
```

この画面外救済は、参加者がターゲットを見ているつもりでもEye Trackingの
ずれによってDisplay境界の少し外側へgazeが出る場合を救うためのものである。
無制限の最近傍選択にはせず3.0度で打ち切ることで、Displayから明確に離れた視線が
意図しないfocus変更になることを避ける。角度距離を使うため、奥行きや物理サイズが
異なる配置でも同じ基準で運用できる。

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

開始画面はtask、interaction method、開始操作だけを表示する。T2 content setは選択せず2024年版へ固定する。

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
- controller movement distance
- controller rotation amount
- wrist / forearm fatigue

`pre-click cursor jitter`、`Press Drift`、`Down Target Error`の3指標は、
2026-07-28の判断で本実験の取得・主要評価から除外する。

2026-07-03の実装確認で、ディスプレイ配置は従来の3配置へ戻した。
直前に共有された前後配置中心の仕様は旧版だったため、配置の正本にはしない。

```text
Task A: LeftRight（左右配置）
Task B: UpDown（上下・同一平面配置）
Task C: UpDownDepth（上下 + 奥行き配置）

2 displays x 6 positions x 2 sizes x 4 cycles
= 96 trials / block

96 trials x 3 tasks
= 288 main trials / participant
```

Mainの `globalTrialIndex` / `trialIndex` は入力手法によらず、Task Aを
`1〜96`、Task Bを `97〜192`、Task Cを `193〜288` として1回ずつ記録する。
各Task内の `trialIndexInCondition` は `1〜96` とする。

各ブロックは24試行の4周で、各周はDisplay・位置・サイズの全組合せを
1回ずつ含む。位置は `x = 0.20 / 0.50 / 0.80`、
`y = 0.20 / 0.80` の3 x 2、サイズは視角ベースの
`Small 1.5度 / Large 3度` とする。

2026-07-16更新として、本実験のMainではタスク順を
`Task A: LeftRight → Task B: UpDown → Task C: UpDownDepth` に固定する。
Main開始時にタスク配置は選択せず、3ブロックを同じ入力手法の連続セッションとして実行する。
Task Aの96試行完了後はTask Bへ、Task B完了後はTask Cへ自動で切り替える。
Task BとTask Cでは、レイアウト切替後30秒間はSTARTボタンを無効にし、残り時間を表示する。
30秒経過後に参加者がSTARTボタンを押し、従来の3秒カウントダウン後に次ブロックを開始する。
操作手法は参加者間要因とし、各参加者は割り当てられた1手法だけを
TrainingとMainで使用する。T1内で手法を切り替えない。
各ブロックの試行順はseed付き制約ランダム化で生成し、D1→D2とD2→D1、
同一Display内移動とDisplay間移動、サイズ、Task CのinputOccluded試行が
偏らない候補を選ぶ。
同じDisplayの同じ位置が連続する並びは禁止し、24試行の周内だけでなく、
前周の最終試行と次周の第1試行の境界にもこの制約を適用する。
Mainの具体的なターゲット順序は全参加者、全操作手法、再実行で共通とする。
参加者番号、sessionId、runId、操作手法はMainのsequence seedへ含めない。
既定の `latestSequenceSeedBase = 3100` では、Task Aを `3100`、
Task Bを `4100`、Task Cを `5100` とする。各行へseedを保存し、
同じコードとseedから順序を再構成できるようにする。
Trainingは練習の反復を固定化する必要がないため、参加者・sessionと
12試行ブロック番号を含む従来のseed生成を維持する。

Task CではDisplay 2の下段3点 `(0.20, 0.80) / (0.50, 0.80) / (0.80, 0.80)`
をinputOccludedとし、1ブロック24試行を含む。Task A/BのinputOccludedは0である。
`FocusPointingTaskManager` はこの96試行設計を既定でコード生成し、旧CSV方式は
比較・ロールバック用に残す。Trainingは `LeftRight → UpDown → UpDownDepth` の順に
各12試行を行い、最低36試行とする。36試行後も参加者が操作に慣れたと回答するまで、
12試行ごとに同じ配置順を循環する。Training終了後のMainは `LeftRight` から開始する。

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

T2コンテンツは2024年版キャンプギア動画の1種類だけを使用する。
開始画面にcontent set選択肢は表示せず、Unity開始処理でも2024年版へ固定する。

T1のTask CとT2は、`DisplayLayoutManager`の同じ `UpDownDepth` 配置を使用する。
座標は配置適用時のHMDを原点とし、HMD正面を +Z、上方向を +Y とした局所座標である。

```text
奥 Display_B_Back:
  distance = 2.25 m
  horizontal angle = 0 degrees
  vertical angle = +11.8 degrees
  apparent size = 42.5 x 23.90625 degrees
  center ≈ (0, 0.460, 2.202)
  physical size ≈ 1.750 x 0.953 m

手前 Display_A_Front:
  distance = 0.75 m
  horizontal angle = 0 degrees
  vertical angle = -13.2 degrees
  apparent size = 37.5 x 21.09375 degrees
  center ≈ (0, -0.171, 0.730)
  physical size ≈ 0.509 x 0.279 m
```

Unityでは `DisplayLayoutPreset.UpDownDepth` の1設定だけで管理する。
T1 Task CとT2のどちらから開始しても、距離、角度、見かけサイズ、算出実寸は同一になる。
奥・手前とも水平角は0度とし、HMD正面中央から左右へずらさない。
他配置の基準 `40 x 22.5 degrees` をまたぐよう、手前は縦横同率で
`37.5 x 21.09375 degrees`、奥は `42.5 x 23.90625 degrees` とし、
両ディスプレイとも縦横比を変えない。
サイズ差を維持したまま、手前の下端を約 `-23.75 degrees`、奥の上端を
約 `+23.75 degrees` として、2画面全体の最上端・最下端をHMD正面に対してほぼ対称にする。
手前上端と奥下端の間には `2.5 degrees` の視覚的な隙間を残す。
垂直中心角の幾何計算値は手前 `-13.203125 degrees`、奥 `+11.796875 degrees` だが、
測定精度を示す値ではないため、実験条件とUnity実装では小数1桁の
`-13.2 degrees`、`+11.8 degrees` に丸める。丸めによる全体の角度移動は
`0.003125 degrees` であり、中央の `2.5 degrees` の隙間は維持される。

2026-07-26更新として、T2のTrainingでは本番とは異なる作品のYouTube動画と、
本番20商品を含まない練習専用Web内容を使用する。プレイヤーと比較WebのUI、Display配置、
入力方法はMainと同一にする。
開始後は `P01 YouTube停止 → P02 YouTube再生 → P03 10秒スキップ → P04 シークバーを使用して戻す → P05 Webスクロール → P06 商品詳細 → P07 候補追加 → P08 候補削除`
を最低1周行う。1周後も同じ操作列を反復し、参加者が「操作方法を理解し、本番へ進める」と申告し、
実験者も必須操作を自力で完了できたことを確認した時点で、左側の終了パネルを長押ししてTrainingを終了する。
追加練習は次の周の途中で終了してよい。
Training終了時はPractice結果を1行保存し、Practice用CSVを閉じてタスク選択画面へ戻る。
この操作からMainへ自動遷移してはならない。実験者がタスク選択画面の「本番を開始」を別に押した時点で、
Practiceとは異なる `runId` / `sessionId` / CSVを作成し、本番用YouTube動画と本番用比較Webを読み込む。
その後、画面内START操作を行ってからMainの12分を独立して計時する。Trainingの実所要時間、完了周回数、完了操作数、
最低1周を超えて行った追加操作数、準備完了確認は分析用ログへ記録する。
極端に長引いた場合は既存の終了操作でセッションを中止できる。
操作確認で追加した候補はMain移行時に消去する。MainではV2D達成後にD2V、D2V達成後に半自由比較へ進む。
半自由比較中の候補追加件数では段階を終了せず、Main開始11分30秒まで候補探索を継続する。V2DとD2Vの個別指示は維持する。
参加者向け指示文には `V2D`／`D2V` という内部課題名を表示せず、具体的な操作内容のみを表示する。
その後の半自由比較では「動画とWebを自由に見比べ、追加・買い替えの候補として良さそうな動画内の商品を、候補リストに追加してください。候補の数に制限はありません。」、
候補確定では「候補リストを確認し、今回のキャンプをより快適にするために最も良さそうな商品を1つ選び、候補を確定してください。」と指示する。
最終候補を確定した時点で計測と結果ログを終了し、選択商品の画像、商品名、ブランド、価格、サイズ、重量を手前Webへ表示したままにする。
最終候補の確定はMain開始から11分30秒経過後にのみ許可する。
11分30秒まではWebの「この商品に決定」ボタンを無効表示にして残り時間を表示し、Unity側でも確定イベントを拒否する。
11分30秒で候補確定指示へ変更し、ボタンを自動解禁すると同時に、手前Webを候補リスト画面へ自動遷移させる。
確定解禁済み・候補リスト自動遷移済みの内部フラグはTraining/Mainを問わず
各run開始時に初期化し、直前runの状態を引き継がない。
Main開始12分で未確定の場合はタイムアウトとして本番用の計測を終了する。ただし候補リスト画面は維持し、
参加者が最終商品を1つ選択・確定するまで操作を続ける。タイムアウト後の選択は別項目として保持し、
タイムアウト状態は変更しない。Unityでは本番の終了時刻と `timeout` の結果行を12分時点で直ちに保存し、
その後の確定商品と確定時刻は `t2_post_timeout_selections_*.csv` の
`post_timeout_final_selection` と `post_timeout_selection_time` に追記する。
通常時間内の確定ではYouTube操作を最低1回必須とするが、本番計測を固定した後の選択を終了不能にしないため、
タイムアウト後の最終商品選択ではこの前提条件を追加要求しない。
終了後アンケート回答後に `候補リストを削除してホームに戻る` を押すと、保存済み候補を全消去してWebをホーム状態へ戻し、Unityの条件選択へ戻る。
この終了後確認画面では全体・段階タイマーを進めない。
Training中は時間カウントダウンを表示せず、Mainでは12分のカウントダウンを表示する。Training終了時の
「本番開始」操作が完了するまでMainタイマーを開始しない。Practice、V2D、D2V、半自由比較、候補確定には
段階タイムアウトを設定できる構造を維持するが、各秒数は未決定であり、確定前に確認する。
Unityでは未決定値を `0 = 無効` として保持し、経過時間は分析用ログへ記録する。
参加者向け指示パネルは奥側Displayの直上に置き、上記T2専用Display配置自体は変更しない。上方向への視野の偏りを抑えるため、
奥側Displayとほぼ同じ幅の横長・低背形状とし、Display上端との隙間を最小限にする。

WebではA/Bセットを混在させ、カテゴリごとにNotionで指定した固定順で表示する。
`video_set`、`video_year`、`item_id`、`start_time`、`end_time`、`appearance_duration` は
内部データとログにのみ保持し、商品カード、詳細、候補、画像代替表示には出さない。
V2Dは4分44秒〜6分20秒付近からA03を探す。
D2Vは「居住・寝具」の上から2番目のA08を確認する。

Unityでは `WebViewSessionManager` がA/B、Training進行、各段階の完了条件、
YouTube/WebのJavaScript telemetry、T2専用CSVを管理する。
比較ページは、
Mac上のReact/Vite開発サーバーを `0.0.0.0:5173` で起動し、
Questから `secondaryInitialUrl = http://<MacのLAN IP>:5173/` へ接続して読み込む。
MacのLAN IPが変わった場合は `secondaryInitialUrl` をその都度更新する。
同梱した単一HTMLの `file://` 読み込み経路はフォールバックとして残すが、
現在の実機運用では使用しない。
固定YouTube動画は2024年版の `https://www.youtube.com/watch?v=oCKnZl-XT1Y` とする。
練習動画はInspectorの `practiceYoutubeUrl` で本番とは別作品を指定する。
既定値は、本番動画と同じ「幕人のキャンプやろうぜ!」チャンネルの
「【サイドテーブル特集】キャンプテーブルのサイドテーブル6選【レビュー・紹介】」
（`https://www.youtube.com/watch?v=HQRzNpPDk0k`、約9分33秒）とする。
実験前に広告、地域制限、公開状態、シーク操作を実機で再確認する。練習Webは同じViteサーバーの
`?mode=practice` を使用し、`TR01`〜`TR05`の架空商品だけを表示する。

本番の比較Webでは、動画とWebの照合性を保つため、許可を得た現行の商品画像を使用する。
画面上ではこれを「商品画像」と表示し、研究用参考画像であるという注意書きは付けない。
論文、発表資料、ポスター、公開デモなど外部に提示する画面については、公開前に
生成画像または権利処理済み画像へ差し替える。実験用画像を含むスクリーンショットを
そのまま発表資料へ流用しない。

### 4.4 参加者番号による手法割当と開始画面

2026-07-25更新として、3手法を参加者間要因として管理する。

- RaycastBaseline
- GazeRay（視線＋Ray）
- ExplicitDisplayFocus（視線＋スティック）

参加者割当は3名を1ブロックとするseed付きpermuted blockで生成する。
各ブロックは3手法を1回ずつ含み、ブロック内の順序だけを固定seed
`20260725`でランダム化する。参加者番号 `n` のブロック番号は
`floor((n - 1) / 3) + 1`、ブロック内枠は `((n - 1) mod 3) + 1`
とする。同じ参加者番号とseedからは常に同じ手法を再現し、番号を
欠番にした場合も後続参加者の割当を詰め直さない。

Unity開始画面では、実験者が参加者番号を入力して確定すると、
`participantId`、割当ブロック、ブロック内枠、操作手法を自動設定する。
操作手法の選択ボタンは本実験運用では表示しない。その後に実験者が
T1またはT2を選択し、Training/Mainの開始操作を行う。T1とT2は
同じ参加者番号から得た同一手法を使用する。

ログには `participantId` と `method` に加え、T2の
`conditionOrder` 欄へ `PB3-Bxxx-Sx-METHOD-Seed20260725` 形式の割当コードを
保存する。`METHOD` は `RAY`、`GAZERAY`、`GAZESTICK` のいずれかであり、
割当コードだけでも紙質問紙へ転記する手法を判別できる。
P001〜P030の事前割当表は `docs/participant_method_assignments.csv` を正本とする。
T1の既存 `methodOrder` は互換性のため残し、
RaycastBaselineは `RayFirst`、GazeRayは `GazeRayOnly`、
ExplicitDisplayFocusは `ExplicitFirst` と記録するが、
これらは現在の参加者間計画では実施順を意味しない。

実験CSVのファイル名は
`participantId_allocationCode_task_phase_runId_dataType_timestamp.csv` に統一する。
例は `P012_PB3-B004-S3-RAY_T2_Practice_R20260726T051234567Z_results_20260726_141234_567.csv`
である。PracticeとMainは必ず別の `runId` / `sessionId` / ファイルを使用する。
schema v2の各行には `participantId`、`allocationCode`、`sessionId`、`runId`、
`schemaVersion` を保存する（T2ではsnake_case列名）。
T1 results、T1 target selection、T2 events、T2 results、
T2 post-timeout selection、60 Hz trajectory、共通event、
GazeRay frameをそれぞれ別ファイルとして保存する。

### 4.5 旧T3 AttentionFocus

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
| grip中gaze mode | grip保持中に有効gazeでfocus/cursorを連続更新、release後は最後の位置を保持 | 仕様・実装済み、Quest Pro確認が必要 |
| 画面外gaze救済 | 3.0度以内の最寄りDisplay矩形へclamp、0.5度の切替hysteresis | 仕様・実装済み、Quest Pro確認が必要 |
| Eye Tracking invalid | focus/candidate/cursor保持、HMD forwardへfallbackしない | 仕様・実装済み、Quest Pro確認が必要 |
| T1 3タスク・96試行/ブロック | `FocusPointingTaskManager`、`T1TrialSequenceGenerator` | 実装済み |
| T1 Training 各配置12試行・最低36試行・3配置循環 | `FocusPointingTaskManager` | 実装済み |
| T1遮蔽幾何モデル | `T1RayOcclusionGeometry` | 実装済み |
| T1 movement/rotation集計 | `FocusPointingTaskManager` | 実装済み |
| pre-click jitter | なし | 指標から除外、実装しない |
| Press Drift | なし | 指標から除外、実装しない |
| Down Target Error | なし | 指標から除外、実装しない |
| T2 YouTube + 比較Web | `WebViewSessionManager` | 2024年版固定・Training・12分タイムアウト後の候補確定継続・CSV実装済み、Quest確認が必要 |
| participant flow | 簡易VR menuのみ | 未実装 |
| trial/event/trajectory統一ログ | 個別CSV + 共通identity schema v2 | 4種類への物理統合と未確定指標は未実装 |
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

`controller_movement_distance` / `controller_movement_amount` は、計測中の各フレーム間における
右コントローラTransformの `Vector3.Distance` の累積値（m）とする。
`controller_rotation_amount` / `controllerRotationDegrees` は同じTransformの
`Quaternion.Angle` の累積値（degree）とし、yaw・pitchに加えてrollも含める。
T1は試行単位、T2は計測セッション単位で累積するが、姿勢変化の定義は共通とする。

次の3指標は2026-07-28の判断で取得対象から除外する。

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
T2 Practiceは最低1周のP08完了後も自動でMainへ進まない。準備完了の自己申告と実験者確認後に、
実験者が終了操作を行う。追加練習中は次の周の途中でも終了できる。終了時はPractice結果を保存して
タスク選択へ戻り、Mainは別の「本番を開始」操作と新しいrunで開始する。

## 9. まだ固定が必要な項目

実装を大きく進める前に、次を研究設計として固定する必要がある。

1. T2完了後の選択理由・主観評価を紙、別端末、Quest内UIのどれで回収するか

Proposedのfocus timing、画面外gaze、Eye Tracking invalid時の挙動、および
3指標の除外は2026-07-28に固定済みである。

## 10. 推奨する次の整理順

1. T2の主観評価回収方法をNotion上で確定する
2. Proposedのgrip semantics、3.0度の画面外救済、invalid時保持をQuest Proで検証する
3. Quest Proで固定動画のYouTube telemetryと比較Web操作をパイロットする
4. Quest単体の実験進行UIを確認する

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
