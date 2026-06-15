using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
/// <summary>
/// T1の1試行ぶんの設定。どの条件・どの表示・どの正規化位置にターゲットを出すかを持つ。
/// </summary>
public class FocusPointingTrialConfig
{
    public int trialIndex;
    public int globalTrialIndex;
    public int trialIndexInCondition;
    public int trialSetId;
    public int trialIndexInSet;
    public T1TargetOrderList targetOrderList = T1TargetOrderList.A;
    public string sourcePhase = "main";
    public int sourceTrialIndex;
    public int cycleIndex;
    public int positionId;
    public int repetition;
    public string conditionName = "ConditionA";
    public InteractionCondition condition = InteractionCondition.RaycastBaseline;
    public DisplayLayoutPreset layoutPreset = DisplayLayoutPreset.UpDownDepth;
    public T1OcclusionType occlusionType = T1OcclusionType.Front;
    public string targetDisplayId = "Display_A_Front";
    public string targetPositionId = "Display_A_Front_C1_R1";
    public Vector2 targetNormalizedPosition = new Vector2(0.5f, 0.5f);
    public T1TargetSize targetSize = T1TargetSize.Medium;
    public float targetSizeDegrees = 2f;
    public float targetSizeNormalized = 0.12f;
    public string previousDisplay = string.Empty;
    public int previousPositionId = -1;
    public string transitionDirection = string.Empty;
    public string transitionType = string.Empty;
    public string occlusionPrevType = string.Empty;
}

public struct FocusPointingClickEvent
{
    public InteractionCondition Condition;
    public string ClickedDisplayId;
    public Vector2 ClickedNormalizedPosition;
    public float Timestamp;
    public bool HasValidDisplay;
}

public struct FocusPointingTrialResult
{
    public string ParticipantId;
    public string SessionId;
    public int TrialIndex;
    public int TrialSetId;
    public int TrialIndexInSet;
    public InteractionCondition Condition;
    public DisplayLayoutPreset LayoutPreset;
    public T1OcclusionType OcclusionType;
    public string TargetDisplayId;
    public Vector2 TargetNormalizedPosition;
    public float TargetSizeNormalized;
    public string ClickedDisplayId;
    public Vector2 ClickedNormalizedPosition;
    public FocusPointingResultType ResultType;
    public bool IsCorrect;
    public bool IsDisplayError;
    public bool IsTargetError;
    public float TrialStartTime;
    public float ClickTime;
    public float CompletionTime;
}

public enum FocusPointingTaskPhase
{
    ConditionSelection,
    Training,
    MainTask
}

public enum TargetSelectionConditionOrder
{
    AThenB,
    BThenA
}

public enum T1OcclusionType
{
    Front,
    BackClear,
    BackOccluded
}

public enum T1TargetOrderList
{
    A,
    B,
    C,
    D,
    E
}

public enum T1MainTargetOrderList
{
    A,
    B,
    C,
    D
}

public enum T1TargetSize
{
    Small,
    Medium,
    Large
}

[DisallowMultipleComponent]
/// <summary>
/// T1 FocusPointingタスクを管理する。
/// 条件選択、Training/Mainの開始、ターゲット生成、クリック評価、CSV保存までを担当する。
/// </summary>
public class FocusPointingTaskManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private ErrorEvaluator errorEvaluator;
    [SerializeField] private Logger logger;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private Transform displayA;
    [SerializeField] private Transform displayB;

    [Header("Session")]
    [SerializeField] private string participantId = "P000";
    [SerializeField] private string sessionId = "S000";
    [SerializeField] private bool autoStartOnPlay = false;
    [SerializeField] private bool loopTrainingTrials = true;
    [SerializeField] private bool returnToConditionSelectionAfterMainTask = true;
    [SerializeField] private InteractionCondition selectedCondition = InteractionCondition.RaycastBaseline;
    [SerializeField] private DisplayLayoutPreset selectedLayout = DisplayLayoutPreset.UpDownDepth;

    [Header("Experiment Conditions")]
    [SerializeField] private TargetSelectionConditionOrder conditionOrder = TargetSelectionConditionOrder.AThenB;
    [SerializeField] private string conditionAName = "ConditionA";
    [SerializeField] private string conditionBName = "ConditionB";
    [SerializeField] private InteractionCondition conditionAInteraction = InteractionCondition.RaycastBaseline;
    [SerializeField] private InteractionCondition conditionBInteraction = InteractionCondition.ExplicitDisplayFocus;

    [Header("Targets")]
    [SerializeField] private Color targetColor = new Color(0.10f, 1f, 0.30f, 0.95f);
    [SerializeField] private Color targetHoverColor = new Color(0.10f, 0.55f, 1f, 0.95f);
    [SerializeField] private Color inactiveTargetColor = new Color(0.10f, 1f, 0.30f, 0.35f);
    [SerializeField] private Transform angularSizeReference;
    [SerializeField] private float smallTargetSizeDegrees = 1f;
    [SerializeField] private float mediumTargetSizeDegrees = 2f;
    [SerializeField] private float largeTargetSizeDegrees = 3f;
    [SerializeField] private float fallbackTargetSizeNormalized = 0.05f;
    [SerializeField] private List<FocusPointingTrialConfig> trials = new List<FocusPointingTrialConfig>();

    [Header("T1 Target Order Lists")]
    [SerializeField] private TextAsset targetOrderCsv;
    [SerializeField] private string targetOrderResourcePath = "T1/target_orders_ABCDE";
    [SerializeField] private T1MainTargetOrderList conditionAMainOrder = T1MainTargetOrderList.A;
    [SerializeField] private T1MainTargetOrderList conditionBMainOrder = T1MainTargetOrderList.A;

    [Header("Task Start Gate")]
    [SerializeField] private bool requireStartButtonBeforeTask = true;
    [SerializeField] private string startGateDisplayId = "Display_B_Back";
    [SerializeField] private Vector2 startButtonNormalizedPosition = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 startButtonNormalizedSize = new Vector2(0.28f, 0.14f);
    [SerializeField] private float startCountdownSeconds = 3f;
    [SerializeField] private Color startGateButtonColor = new Color(0.10f, 0.70f, 0.32f, 0.95f);
    [SerializeField] private Color startGateCountdownColor = new Color(0.15f, 0.48f, 0.85f, 0.95f);

    private readonly Dictionary<DisplaySurface, RectTransform> targetRects = new Dictionary<DisplaySurface, RectTransform>();
    private int currentTrialListIndex = -1;
    private FocusPointingTrialConfig currentTrial;
    private FocusPointingTaskPhase currentPhase = FocusPointingTaskPhase.ConditionSelection;
    private float trialStartTime;
    private bool trialRunning;
    private bool taskRunning;
    private bool trialsGenerated;
    private StreamWriter targetSelectionWriter;
    private string targetSelectionCsvPath;
    private StreamWriter t1ResultsWriter;
    private string t1ResultsCsvPath;
    private int currentTrialAttemptIndex;
    private InteractionCondition activeTaskCondition;
    private TaskStartGate startGate;
    private Vector2 currentTargetNormalizedSize;

    public bool IsRunning => trialRunning;
    public bool IsTaskRunning => taskRunning;
    public FocusPointingTaskPhase CurrentPhase => currentPhase;
    public InteractionCondition SelectedCondition => selectedCondition;
    public DisplayLayoutPreset SelectedLayout => selectedLayout;
    public string CurrentTargetDisplayId => currentTrial != null ? currentTrial.targetDisplayId : "None";
    public int CurrentTrialIndex => currentTrial != null ? currentTrial.trialIndex : -1;
    public int CurrentGlobalTrialIndex => currentTrial != null ? currentTrial.globalTrialIndex : -1;
    public int CurrentTrialIndexInCondition => currentTrial != null ? currentTrial.trialIndexInCondition : -1;
    public int CurrentTrialSetId => currentTrial != null ? currentTrial.trialSetId : -1;
    public int CurrentTrialIndexInSet => currentTrial != null ? currentTrial.trialIndexInSet : -1;
    public T1OcclusionType CurrentOcclusionType => currentTrial != null ? currentTrial.occlusionType : T1OcclusionType.Front;
    public string CurrentConditionName => currentTrial != null ? currentTrial.conditionName : "None";
    public Vector2 CurrentTargetNormalizedPosition => currentTrial != null ? currentTrial.targetNormalizedPosition : Vector2.zero;
    public float CurrentTargetSizeNormalized => currentTrial != null ? currentTrial.targetSizeNormalized : 0f;
    public T1TargetOrderList CurrentTargetOrderList => currentTrial != null
        ? currentTrial.targetOrderList
        : (T1TargetOrderList)GetMainOrderForCondition(selectedCondition);

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureTargets();

        if (autoStartOnPlay)
        {
            BeginTrainingTask();
        }
    }

    private void Update()
    {
        UpdateTargetHoverVisual();
    }

    private void OnDisable()
    {
        HideStartGate();
        CloseTargetSelectionCsv();
        CloseT1ResultsCsv();
    }

    public void SetSelectedCondition(InteractionCondition condition)
    {
        if (taskRunning)
        {
            Debug.Log($"[FocusPointingTask] condition change ignored while task is running. requested={condition}, active={selectedCondition}");
            return;
        }

        selectedCondition = condition;
    }

    public void SetSelectedLayout(DisplayLayoutPreset layout)
    {
        if (taskRunning)
        {
            Debug.Log($"[FocusPointingTask] layout change ignored while task is running. requested={layout}, active={selectedLayout}");
            return;
        }

        selectedLayout = layout;
    }

    public string GetTargetOrderSummary(InteractionCondition condition)
    {
        return $"Main List {GetMainOrderForCondition(condition)} / Training List E";
    }

    public void BeginTrainingTask()
    {
        BeginTask(FocusPointingTaskPhase.Training, false);
    }

    public void BeginMainTask()
    {
        BeginTask(FocusPointingTaskPhase.MainTask, true);
    }

    public void ReturnToConditionSelection()
    {
        trialRunning = false;
        taskRunning = false;
        currentTrial = null;
        currentTrialListIndex = -1;
        currentPhase = FocusPointingTaskPhase.ConditionSelection;
        activeTaskCondition = selectedCondition;
        HideStartGate();
        HideAllTargets();
        SetDisplayContentMode(DisplayContentMode.ConditionSelection);
        CloseTargetSelectionCsv();
        CloseT1ResultsCsv();

        if (inputManager != null)
        {
            inputManager.UnlockCondition();
        }

        Debug.Log("[FocusPointingTask] returned to condition selection");
    }

    private void BeginTask(FocusPointingTaskPhase phase, bool lockCondition)
    {
        // 本タスクでは開始時の条件をロックし、選択した順序リストが終わるまで途中変更させない。
        ResolveReferences();
        currentPhase = phase;
        trialsGenerated = false;

        EnsureDefaultTrials();
        EnsureTargets();
        HideAllTargets();
        SetDisplayContentMode(DisplayContentMode.PointingTask);
        OpenTargetSelectionCsv();
        OpenT1ResultsCsv();

        taskRunning = true;
        activeTaskCondition = selectedCondition;
        currentTrialListIndex = FindIndexBeforeFirstTrialForCondition(activeTaskCondition);

        if (inputManager != null)
        {
            if (lockCondition)
            {
                inputManager.LockCondition(activeTaskCondition);
            }
            else
            {
                inputManager.SetCondition(activeTaskCondition, true);
            }
        }

        if (experimentManager != null)
        {
            experimentManager.ApplyLayout(selectedLayout);
        }

        Debug.Log($"[FocusPointingTask] begin phase={currentPhase}, condition={activeTaskCondition}, layout={selectedLayout}, lockCondition={lockCondition}");
        if (requireStartButtonBeforeTask)
        {
            // 実験開始前の構えを揃えるため、中央Startボタンと3秒カウントダウンを挟む。
            ShowStartGate();
            return;
        }

        StartNextTrial();
    }

    public bool TryHandleTaskControlClick(string displayId, Vector2 normalizedPosition)
    {
        return startGate != null && startGate.TryHandleClick(displayId, normalizedPosition);
    }

    public void StartNextTrial()
    {
        ResolveReferences();
        EnsureDefaultTrials();
        EnsureTargets();
        HideStartGate();
        HideAllTargets();

        if (trials.Count == 0)
        {
            Debug.LogWarning("[FocusPointingTask] No trials configured.");
            trialRunning = false;
            taskRunning = false;
            return;
        }

        currentTrialListIndex++;
        bool outsideActiveCondition = currentTrialListIndex >= trials.Count
            || trials[currentTrialListIndex].condition != activeTaskCondition;
        if (outsideActiveCondition)
        {
            bool shouldLoop = currentPhase == FocusPointingTaskPhase.Training && loopTrainingTrials;
            if (!shouldLoop)
            {
                CompleteCurrentTask();
                return;
            }

            currentTrialListIndex = FindIndexBeforeFirstTrialForCondition(activeTaskCondition) + 1;
        }

        currentTrial = trials[currentTrialListIndex];
        if (currentTrial.trialIndex <= 0)
        {
            currentTrial.trialIndex = currentTrialListIndex + 1;
        }

        currentTrial.layoutPreset = selectedLayout;
        selectedCondition = currentTrial.condition;
        if (inputManager != null)
        {
            inputManager.SetCondition(currentTrial.condition, true);
        }

        trialStartTime = Time.time;
        currentTrialAttemptIndex = 0;
        trialRunning = true;
        ShowTargetForCurrentTrial();
        Debug.Log(
            $"[FocusPointingTask] start phase={currentPhase}, list={currentTrial.targetOrderList}, "
            + $"conditionName={currentTrial.conditionName}, trialInCondition={currentTrial.trialIndexInCondition}, "
            + $"cycle={currentTrial.cycleIndex}, condition={selectedCondition}, layout={selectedLayout}, "
            + $"occlusion={currentTrial.occlusionType}, targetDisplay={currentTrial.targetDisplayId}, "
            + $"targetId={currentTrial.targetPositionId}, target={Format(currentTrial.targetNormalizedPosition)}, "
            + $"size={currentTrial.targetSize} ({currentTrial.targetSizeDegrees:0.0}deg)");
    }

    public void HandleClick(FocusPointingClickEvent clickEvent)
    {
        // ClickDispatcherから条件に依存しないクリックイベントとして受け取り、同じ評価器で判定する。
        ResolveReferences();

        if (!trialRunning || currentTrial == null)
        {
            return;
        }

        if (errorEvaluator == null)
        {
            Debug.LogWarning("[FocusPointingTask] Missing ErrorEvaluator.");
            return;
        }

        FocusPointingEvaluation evaluation = errorEvaluator.EvaluateFocusPointingClick(
            currentTrial.targetDisplayId,
            currentTrial.targetNormalizedPosition,
            GetCurrentTargetNormalizedSize(),
            clickEvent.ClickedDisplayId,
            clickEvent.ClickedNormalizedPosition,
            clickEvent.HasValidDisplay);

        FocusPointingTrialResult result = new FocusPointingTrialResult
        {
            ParticipantId = participantId,
            SessionId = sessionId,
            TrialIndex = currentTrial.trialIndex,
            TrialSetId = currentTrial.trialSetId,
            TrialIndexInSet = currentTrial.trialIndexInSet,
            Condition = clickEvent.Condition,
            LayoutPreset = currentTrial.layoutPreset,
            OcclusionType = currentTrial.occlusionType,
            TargetDisplayId = currentTrial.targetDisplayId,
            TargetNormalizedPosition = currentTrial.targetNormalizedPosition,
            TargetSizeNormalized = currentTrial.targetSizeNormalized,
            ClickedDisplayId = clickEvent.HasValidDisplay ? clickEvent.ClickedDisplayId : "None",
            ClickedNormalizedPosition = clickEvent.ClickedNormalizedPosition,
            ResultType = evaluation.ResultType,
            IsCorrect = evaluation.IsCorrect,
            IsDisplayError = evaluation.IsDisplayError,
            IsTargetError = evaluation.IsTargetError,
            TrialStartTime = trialStartTime,
            ClickTime = clickEvent.Timestamp,
            CompletionTime = clickEvent.Timestamp - trialStartTime
        };
        currentTrialAttemptIndex++;
        LogT1Result(result, currentTrialAttemptIndex, evaluation.IsCorrect);

        if (!evaluation.IsCorrect)
        {
            // 誤反応も分析できるようにCSVへ残すが、試行は正答するまで続ける。
            if (logger != null)
            {
                logger.LogFocusPointingTrial(result);
            }

            Debug.Log($"[FocusPointingTask] miss trial={result.TrialIndex}, result={result.ResultType}, clickedDisplay={result.ClickedDisplayId}, clicked={Format(result.ClickedNormalizedPosition)}");
            return;
        }

        LogTargetSelectionResult(clickEvent.Timestamp);
        if (logger != null)
        {
            logger.LogFocusPointingTrial(result);
        }

        Debug.Log($"[FocusPointingTask] result conditionName={currentTrial.conditionName}, trialInCondition={currentTrial.trialIndexInCondition}, trialSetId={currentTrial.trialSetId}, global={currentTrial.globalTrialIndex}, occlusion={currentTrial.occlusionType}, result={result.ResultType}, clickedDisplay={result.ClickedDisplayId}, clicked={Format(result.ClickedNormalizedPosition)}, completion={result.CompletionTime:0.000}");
        trialRunning = false;
        HideAllTargets();
        StartNextTrial();
    }

    private void CompleteCurrentTask()
    {
        trialRunning = false;
        taskRunning = false;
        HideAllTargets();
        HideStartGate();
        SetDisplayContentMode(DisplayContentMode.ConditionSelection);
        CloseTargetSelectionCsv();
        CloseT1ResultsCsv();

        if (currentPhase == FocusPointingTaskPhase.MainTask && returnToConditionSelectionAfterMainTask)
        {
            ReturnToConditionSelection();
            return;
        }

        if (inputManager != null)
        {
            inputManager.UnlockCondition();
        }

        currentPhase = FocusPointingTaskPhase.ConditionSelection;
        Debug.Log("[FocusPointingTask] completed task");
    }

    private void EnsureDefaultTrials()
    {
        if (trialsGenerated && trials.Count > 0)
        {
            return;
        }

        trials.Clear();
        List<ConditionBlock> conditionBlocks = GetConditionBlocksInOrder();
        int globalIndex = 1;
        for (int conditionIndex = 0; conditionIndex < conditionBlocks.Count; conditionIndex++)
        {
            ConditionBlock conditionBlock = conditionBlocks[conditionIndex];
            T1TargetOrderList orderList = currentPhase == FocusPointingTaskPhase.Training
                ? T1TargetOrderList.E
                : (T1TargetOrderList)conditionBlock.MainOrder;
            List<FocusPointingTrialConfig> sourceTrials = BuildTrialSetFromCsv(orderList, currentPhase);
            for (int i = 0; i < sourceTrials.Count; i++)
            {
                FocusPointingTrialConfig trial = CloneTrialForCondition(sourceTrials[i], conditionBlock);
                trial.globalTrialIndex = globalIndex;
                trial.trialIndex = globalIndex;
                trial.trialIndexInCondition = i + 1;
                trial.layoutPreset = selectedLayout;
                trials.Add(trial);
                globalIndex++;
            }
        }

        trialsGenerated = true;
        Debug.Log(
            $"[FocusPointingTask] loaded phase={currentPhase}, totalTrials={trials.Count}, "
            + $"conditionA={conditionAMainOrder}, conditionB={conditionBMainOrder}, training=E");
    }

    private List<FocusPointingTrialConfig> BuildTrialSetFromCsv(
        T1TargetOrderList orderList,
        FocusPointingTaskPhase phase)
    {
        List<FocusPointingTrialConfig> trialSet = new List<FocusPointingTrialConfig>();
        TextAsset csv = targetOrderCsv != null
            ? targetOrderCsv
            : Resources.Load<TextAsset>(targetOrderResourcePath);
        if (csv == null)
        {
            Debug.LogError(
                $"[FocusPointingTask] T1 target order CSV was not found. "
                + $"Assign Target Order Csv or add Resources/{targetOrderResourcePath}.csv.");
            return trialSet;
        }

        string expectedPhase = phase == FocusPointingTaskPhase.Training ? "training" : "main";
        string frontDisplayId = GetDisplayName(displayA, "Display_A_Front");
        string backDisplayId = GetDisplayName(displayB, "Display_B_Back");
        using (StringReader reader = new StringReader(csv.text))
        {
            string headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                Debug.LogError("[FocusPointingTask] T1 target order CSV has no header.");
                return trialSet;
            }

            Dictionary<string, int> columns = BuildCsvColumnMap(headerLine);
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] values = line.TrimEnd('\r').Split(',');
                string listName = ReadCsv(values, columns, "listName");
                string sourcePhase = ReadCsv(values, columns, "phase");
                if (!string.Equals(listName, orderList.ToString(), StringComparison.Ordinal)
                    || !string.Equals(sourcePhase, expectedPhase, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    bool isBackDisplay = ParseCsvBool(ReadCsv(values, columns, "isBackDisplay"));
                    bool inputOccluded = ParseCsvBool(ReadCsv(values, columns, "inputOccluded"));
                    T1TargetSize targetSize = ParseTargetSize(ReadCsv(values, columns, "size"));
                    int sourceTrialIndex = ParseCsvInt(values, columns, "trialIndex");
                    int positionId = ParseCsvInt(values, columns, "positionId");
                    trialSet.Add(new FocusPointingTrialConfig
                    {
                        trialSetId = sourceTrialIndex,
                        trialIndexInSet = sourceTrialIndex,
                        targetOrderList = orderList,
                        sourcePhase = sourcePhase,
                        sourceTrialIndex = sourceTrialIndex,
                        cycleIndex = ParseCsvInt(values, columns, "cycleIndex"),
                        positionId = positionId,
                        repetition = ParseCsvInt(values, columns, "repetition"),
                        conditionName = "BaseTrialSet",
                        condition = InteractionCondition.RaycastBaseline,
                        layoutPreset = selectedLayout,
                        occlusionType = !isBackDisplay
                            ? T1OcclusionType.Front
                            : inputOccluded
                                ? T1OcclusionType.BackOccluded
                                : T1OcclusionType.BackClear,
                        targetDisplayId = isBackDisplay ? backDisplayId : frontDisplayId,
                        targetPositionId = ReadCsv(values, columns, "targetId"),
                        targetNormalizedPosition = new Vector2(
                            ParseCsvFloat(values, columns, "x"),
                            ParseCsvFloat(values, columns, "y")),
                        targetSize = targetSize,
                        targetSizeDegrees = GetTargetSizeDegrees(targetSize),
                        previousDisplay = ReadCsv(values, columns, "previousDisplay"),
                        previousPositionId = ParseOptionalCsvInt(values, columns, "previousPositionId", -1),
                        transitionDirection = ReadCsv(values, columns, "transitionDirection"),
                        transitionType = ReadCsv(values, columns, "transitionType"),
                        occlusionPrevType = ReadCsv(values, columns, "occlusionPrevType")
                    });
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[FocusPointingTask] Failed to parse T1 target order CSV line {lineNumber}: "
                        + exception.Message);
                    return new List<FocusPointingTrialConfig>();
                }
            }
        }

        const int expectedCount = 108;
        if (trialSet.Count != expectedCount)
        {
            Debug.LogError(
                $"[FocusPointingTask] List {orderList} phase={expectedPhase} contains "
                + $"{trialSet.Count} trials; expected {expectedCount}.");
            trialSet.Clear();
        }

        return trialSet;
    }

    private FocusPointingTrialConfig CloneTrialForCondition(FocusPointingTrialConfig source, ConditionBlock conditionBlock)
    {
        return new FocusPointingTrialConfig
        {
            trialSetId = source.trialSetId,
            trialIndexInSet = source.trialIndexInSet,
            targetOrderList = source.targetOrderList,
            sourcePhase = source.sourcePhase,
            sourceTrialIndex = source.sourceTrialIndex,
            cycleIndex = source.cycleIndex,
            positionId = source.positionId,
            repetition = source.repetition,
            conditionName = conditionBlock.Name,
            condition = conditionBlock.InteractionCondition,
            layoutPreset = selectedLayout,
            occlusionType = source.occlusionType,
            targetDisplayId = source.targetDisplayId,
            targetPositionId = source.targetPositionId,
            targetNormalizedPosition = source.targetNormalizedPosition,
            targetSize = source.targetSize,
            targetSizeDegrees = source.targetSizeDegrees,
            targetSizeNormalized = source.targetSizeNormalized,
            previousDisplay = source.previousDisplay,
            previousPositionId = source.previousPositionId,
            transitionDirection = source.transitionDirection,
            transitionType = source.transitionType,
            occlusionPrevType = source.occlusionPrevType
        };
    }

    private List<ConditionBlock> GetConditionBlocksInOrder()
    {
        ConditionBlock conditionA = new ConditionBlock(conditionAName, conditionAInteraction, conditionAMainOrder);
        ConditionBlock conditionB = new ConditionBlock(conditionBName, conditionBInteraction, conditionBMainOrder);
        return conditionOrder == TargetSelectionConditionOrder.AThenB
            ? new List<ConditionBlock> { conditionA, conditionB }
            : new List<ConditionBlock> { conditionB, conditionA };
    }

    private int FindIndexBeforeFirstTrialForCondition(InteractionCondition condition)
    {
        for (int i = 0; i < trials.Count; i++)
        {
            if (trials[i] != null && trials[i].condition == condition)
            {
                return i - 1;
            }
        }

        Debug.LogWarning($"[FocusPointingTask] No trials found for condition={condition}. Falling back to the first trial.");
        return -1;
    }

    private void EnsureTargets()
    {
        if (displayManager == null || displayManager.Displays == null)
        {
            return;
        }

        for (int i = 0; i < displayManager.Displays.Length; i++)
        {
            DisplaySurface display = displayManager.Displays[i];
            if (display == null || display.WorldSpaceCanvas == null)
            {
                continue;
            }

            if (!targetRects.ContainsKey(display) || targetRects[display] == null)
            {
                targetRects[display] = CreateTarget(display);
            }
        }
    }

    private RectTransform CreateTarget(DisplaySurface display)
    {
        Transform canvasTransform = display.WorldSpaceCanvas.transform;
        Transform existing = canvasTransform.Find("FocusPointingTarget");
        GameObject targetObject = existing != null ? existing.gameObject : new GameObject("FocusPointingTarget", typeof(RectTransform));
        targetObject.transform.SetParent(canvasTransform, false);

        RectTransform rectTransform = targetObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        Image image = targetObject.GetComponent<Image>();
        if (image == null)
        {
            image = targetObject.AddComponent<Image>();
        }

        image.color = targetColor;
        targetObject.SetActive(false);
        return rectTransform;
    }

    private void ShowTargetForCurrentTrial()
    {
        DisplaySurface display = FindDisplay(currentTrial.targetDisplayId);
        if (display == null)
        {
            Debug.LogWarning($"[FocusPointingTask] Target display not found: {currentTrial.targetDisplayId}");
            return;
        }

        if (!targetRects.TryGetValue(display, out RectTransform targetRect) || targetRect == null)
        {
            targetRect = CreateTarget(display);
            targetRects[display] = targetRect;
        }

        Vector2 canvasSize = display.GetCanvasSize();
        Vector2 targetPosition = currentTrial.targetNormalizedPosition;
        Vector2 targetCanvasSize = CalculateTargetCanvasSize(display, currentTrial.targetSizeDegrees);
        currentTargetNormalizedSize = new Vector2(
            targetCanvasSize.x / Mathf.Max(1f, canvasSize.x),
            targetCanvasSize.y / Mathf.Max(1f, canvasSize.y));
        currentTrial.targetSizeNormalized = Mathf.Min(
            currentTargetNormalizedSize.x,
            currentTargetNormalizedSize.y);

        targetRect.anchoredPosition = display.NormalizedToCanvasPosition(targetPosition);
        targetRect.sizeDelta = targetCanvasSize;
        targetRect.gameObject.SetActive(true);
        targetRect.SetAsLastSibling();

        Image image = targetRect.GetComponent<Image>();
        if (image != null)
        {
            image.color = targetColor;
        }

    }

    private bool ContainsTarget(Vector2 normalized)
    {
        if (currentTrial == null)
        {
            return false;
        }

        Vector2 halfSize = GetCurrentTargetNormalizedSize() * 0.5f;
        Vector2 delta = normalized - currentTrial.targetNormalizedPosition;
        return Mathf.Abs(delta.x) <= halfSize.x && Mathf.Abs(delta.y) <= halfSize.y;
    }

    private Vector2 GetCurrentTargetNormalizedSize()
    {
        if (currentTrial == null)
        {
            return Vector2.zero;
        }

        if (currentTargetNormalizedSize.x > 0f && currentTargetNormalizedSize.y > 0f)
        {
            return currentTargetNormalizedSize;
        }

        DisplaySurface display = FindDisplay(currentTrial.targetDisplayId);
        if (display == null)
        {
            return Vector2.one * Mathf.Max(0f, fallbackTargetSizeNormalized);
        }

        Vector2 canvasSize = display.GetCanvasSize();
        if (canvasSize.x <= 0f || canvasSize.y <= 0f)
        {
            return Vector2.one * Mathf.Max(0f, fallbackTargetSizeNormalized);
        }

        Vector2 targetCanvasSize = CalculateTargetCanvasSize(display, currentTrial.targetSizeDegrees);
        return new Vector2(targetCanvasSize.x / canvasSize.x, targetCanvasSize.y / canvasSize.y);
    }

    private Vector2 CalculateTargetCanvasSize(DisplaySurface display, float targetDegrees)
    {
        Vector2 canvasSize = display.GetCanvasSize();
        Vector2 physicalSize = display.PhysicalSizeMeters;
        if (angularSizeReference == null
            || physicalSize.x <= 0f
            || physicalSize.y <= 0f
            || targetDegrees <= 0f)
        {
            float fallbackPixels = Mathf.Max(
                8f,
                fallbackTargetSizeNormalized * Mathf.Min(canvasSize.x, canvasSize.y));
            return Vector2.one * fallbackPixels;
        }

        float distanceMeters = Vector3.Distance(angularSizeReference.position, display.transform.position);
        float targetMeters = 2f
            * Mathf.Max(0.01f, distanceMeters)
            * Mathf.Tan(targetDegrees * 0.5f * Mathf.Deg2Rad);
        return new Vector2(
            Mathf.Max(8f, targetMeters / physicalSize.x * canvasSize.x),
            Mathf.Max(8f, targetMeters / physicalSize.y * canvasSize.y));
    }

    private void UpdateTargetHoverVisual()
    {
        if (!trialRunning || currentTrial == null || inputManager == null || displayManager == null)
        {
            return;
        }

        DisplaySurface targetDisplay = FindDisplay(currentTrial.targetDisplayId);
        if (targetDisplay == null
            || !targetRects.TryGetValue(targetDisplay, out RectTransform targetRect)
            || targetRect == null
            || !targetRect.gameObject.activeInHierarchy)
        {
            return;
        }

        bool hovered = false;
        if (inputManager.CurrentCondition == InteractionCondition.RaycastBaseline)
        {
            hovered = displayManager.HasCurrentRaycastHit
                && displayManager.CurrentRaycastHit.Display == targetDisplay
                && ContainsTarget(displayManager.CurrentRaycastHit.Normalized);
        }
        else if (inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus
            && virtualCursorController != null)
        {
            hovered = displayManager.FocusedDisplay == targetDisplay
                && ContainsTarget(virtualCursorController.NormalizedPosition);
        }

        Image image = targetRect.GetComponent<Image>();
        if (image != null)
        {
            image.color = hovered ? targetHoverColor : targetColor;
        }
    }

    private DisplaySurface FindDisplay(string displayId)
    {
        DisplaySurface transformDisplay = FindDisplayFromConfiguredTransforms(displayId);
        if (transformDisplay != null)
        {
            return transformDisplay;
        }

        if (displayManager == null || displayManager.Displays == null)
        {
            return null;
        }

        for (int i = 0; i < displayManager.Displays.Length; i++)
        {
            DisplaySurface display = displayManager.Displays[i];
            if (display != null && display.name == displayId)
            {
                return display;
            }
        }

        return null;
    }

    private DisplaySurface FindDisplayFromConfiguredTransforms(string displayId)
    {
        DisplaySurface displayASurface = displayA != null ? displayA.GetComponent<DisplaySurface>() : null;
        if (displayASurface != null && displayASurface.name == displayId)
        {
            return displayASurface;
        }

        DisplaySurface displayBSurface = displayB != null ? displayB.GetComponent<DisplaySurface>() : null;
        if (displayBSurface != null && displayBSurface.name == displayId)
        {
            return displayBSurface;
        }

        return null;
    }

    private void HideAllTargets()
    {
        currentTargetNormalizedSize = Vector2.zero;
        foreach (KeyValuePair<DisplaySurface, RectTransform> pair in targetRects)
        {
            if (pair.Value != null)
            {
                Image image = pair.Value.GetComponent<Image>();
                if (image != null)
                {
                    image.color = inactiveTargetColor;
                }

                pair.Value.gameObject.SetActive(false);
            }
        }
    }

    private void ShowStartGate()
    {
        HideAllTargets();
        DisplaySurface display = FindDisplay(startGateDisplayId);
        startGate = TaskStartGate.GetOrCreate(display);
        if (startGate == null)
        {
            Debug.LogWarning("[FocusPointingTask] Start gate display was not found. Starting task without start gate.");
            StartNextTrial();
            return;
        }

        startGate.Show(
            display,
            startButtonNormalizedPosition,
            startButtonNormalizedSize,
            startCountdownSeconds,
            startGateButtonColor,
            startGateCountdownColor,
            StartNextTrial,
            "[FocusPointingTask]");

        Debug.Log($"[FocusPointingTask] waiting for start button phase={currentPhase}, display={startGateDisplayId}");
    }

    private void HideStartGate()
    {
        startGate?.Hide();
    }

    private void SetDisplayContentMode(DisplayContentMode mode)
    {
        if (displayManager == null)
        {
            return;
        }

        displayManager.SetAllDisplayContentMode(mode);
    }

    private void LogTargetSelectionResult(float timestamp)
    {
        if (currentTrial == null)
        {
            return;
        }

        OpenTargetSelectionCsv();
        float responseTime = timestamp - trialStartTime;
        if (targetSelectionWriter != null)
        {
            targetSelectionWriter.WriteLine(string.Join(",",
                Escape(participantId),
                Escape(currentTrial.conditionName),
                $"List{currentTrial.targetOrderList}",
                GetOrderSeed(currentTrial.targetOrderList).ToString(CultureInfo.InvariantCulture),
                Escape(currentTrial.targetOrderList.ToString()),
                Escape(currentTrial.sourcePhase),
                currentTrial.cycleIndex.ToString(CultureInfo.InvariantCulture),
                currentTrial.positionId.ToString(CultureInfo.InvariantCulture),
                Escape(currentTrial.targetSize.ToString()),
                currentTrial.targetSizeDegrees.ToString("0.0", CultureInfo.InvariantCulture),
                currentTrial.repetition.ToString(CultureInfo.InvariantCulture),
                currentTrial.trialIndexInCondition.ToString(CultureInfo.InvariantCulture),
                currentTrial.globalTrialIndex.ToString(CultureInfo.InvariantCulture),
                currentTrial.trialSetId.ToString(CultureInfo.InvariantCulture),
                currentTrial.trialIndexInSet.ToString(CultureInfo.InvariantCulture),
                Escape(currentTrial.occlusionType.ToString()),
                Escape(currentTrial.targetDisplayId),
                Escape(currentTrial.targetPositionId),
                currentTrial.targetNormalizedPosition.x.ToString("0.000", CultureInfo.InvariantCulture),
                currentTrial.targetNormalizedPosition.y.ToString("0.000", CultureInfo.InvariantCulture),
                responseTime.ToString("0.000", CultureInfo.InvariantCulture),
                DateTime.Now.ToString("o", CultureInfo.InvariantCulture)));
            targetSelectionWriter.Flush();
        }
    }

    private void LogT1Result(FocusPointingTrialResult result, int attemptIndex, bool advancesTrial)
    {
        if (currentTrial == null)
        {
            return;
        }

        OpenT1ResultsCsv();
        if (t1ResultsWriter == null)
        {
            return;
        }

        t1ResultsWriter.WriteLine(string.Join(",",
            Escape(participantId),
            Escape(sessionId),
            Escape(currentPhase.ToString()),
            Escape(currentTrial.conditionName),
            Escape(result.Condition.ToString()),
            Escape(result.LayoutPreset.ToString()),
            $"List{currentTrial.targetOrderList}",
            GetOrderSeed(currentTrial.targetOrderList).ToString(CultureInfo.InvariantCulture),
            Escape(currentTrial.targetOrderList.ToString()),
            Escape(currentTrial.sourcePhase),
            currentTrial.cycleIndex.ToString(CultureInfo.InvariantCulture),
            currentTrial.positionId.ToString(CultureInfo.InvariantCulture),
            Escape(currentTrial.targetSize.ToString()),
            currentTrial.targetSizeDegrees.ToString("0.0", CultureInfo.InvariantCulture),
            currentTrial.repetition.ToString(CultureInfo.InvariantCulture),
            Escape(currentTrial.previousDisplay),
            currentTrial.previousPositionId >= 0
                ? currentTrial.previousPositionId.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            Escape(currentTrial.transitionDirection),
            Escape(currentTrial.transitionType),
            Escape(currentTrial.occlusionPrevType),
            currentTrial.trialIndexInCondition.ToString(CultureInfo.InvariantCulture),
            currentTrial.globalTrialIndex.ToString(CultureInfo.InvariantCulture),
            currentTrial.trialSetId.ToString(CultureInfo.InvariantCulture),
            currentTrial.trialIndexInSet.ToString(CultureInfo.InvariantCulture),
            Escape(currentTrial.occlusionType.ToString()),
            Escape(currentTrial.targetDisplayId),
            Escape(currentTrial.targetPositionId),
            currentTrial.targetNormalizedPosition.x.ToString("0.000", CultureInfo.InvariantCulture),
            currentTrial.targetNormalizedPosition.y.ToString("0.000", CultureInfo.InvariantCulture),
            currentTrial.targetSizeNormalized.ToString("0.000", CultureInfo.InvariantCulture),
            attemptIndex.ToString(CultureInfo.InvariantCulture),
            Escape(result.ClickedDisplayId),
            result.ClickedNormalizedPosition.x.ToString("0.000", CultureInfo.InvariantCulture),
            result.ClickedNormalizedPosition.y.ToString("0.000", CultureInfo.InvariantCulture),
            Escape(result.ResultType.ToString()),
            Bool(result.IsCorrect),
            Bool(result.IsDisplayError),
            Bool(result.IsTargetError),
            Bool(result.ResultType == FocusPointingResultType.Miss),
            Bool(advancesTrial),
            result.TrialStartTime.ToString("0.000", CultureInfo.InvariantCulture),
            result.ClickTime.ToString("0.000", CultureInfo.InvariantCulture),
            result.CompletionTime.ToString("0.000", CultureInfo.InvariantCulture),
            DateTime.Now.ToString("o", CultureInfo.InvariantCulture)));
        t1ResultsWriter.Flush();
    }

    private void OpenTargetSelectionCsv()
    {
        if (targetSelectionWriter != null)
        {
            return;
        }

        string directory = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(directory);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        targetSelectionCsvPath = Path.Combine(directory, $"target_selection_{participantId}_{timestamp}.csv");
        targetSelectionWriter = new StreamWriter(targetSelectionCsvPath);
        targetSelectionWriter.WriteLine("participantId,conditionName,trialOrder,randomSeed,targetOrderList,sourcePhase,cycleIndex,positionId,targetSizeName,targetSizeDegrees,repetition,trialIndexInCondition,globalTrialIndex,trialSetId,trialIndexInSet,occlusion_type,displayName,targetPositionId,targetLocalX,targetLocalY,responseTime,timestamp");
        Debug.Log($"[FocusPointingTask] target selection CSV logging to {targetSelectionCsvPath}");
    }

    private void OpenT1ResultsCsv()
    {
        if (t1ResultsWriter != null)
        {
            return;
        }

        string directory = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(directory);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        t1ResultsCsvPath = Path.Combine(directory, $"t1_results_{participantId}_{sessionId}_{timestamp}.csv");
        t1ResultsWriter = new StreamWriter(t1ResultsCsvPath);
        t1ResultsWriter.WriteLine("participantId,sessionId,taskPhase,conditionName,interactionCondition,layoutPreset,trialOrder,randomSeed,targetOrderList,sourcePhase,cycleIndex,positionId,targetSizeName,targetSizeDegrees,repetition,previousDisplay,previousPositionId,transitionDirection,transitionType,occlusionPrevType,trialIndexInCondition,globalTrialIndex,trialSetId,trialIndexInSet,occlusion_type,targetDisplayId,targetPositionId,targetLocalX,targetLocalY,targetSize,attemptIndex,clickedDisplayId,clickedLocalX,clickedLocalY,resultType,isCorrect,displayError,targetError,miss,advancesTrial,trialStartTime,clickTime,responseTime,timestamp");
        Debug.Log($"[FocusPointingTask] T1 result CSV logging to {t1ResultsCsvPath}");
    }

    private void CloseTargetSelectionCsv()
    {
        if (targetSelectionWriter == null)
        {
            return;
        }

        targetSelectionWriter.Flush();
        targetSelectionWriter.Close();
        targetSelectionWriter = null;
    }

    private void CloseT1ResultsCsv()
    {
        if (t1ResultsWriter == null)
        {
            return;
        }

        t1ResultsWriter.Flush();
        t1ResultsWriter.Close();
        t1ResultsWriter = null;
    }

    private static string Bool(bool value)
    {
        return value ? "1" : "0";
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static Dictionary<string, int> BuildCsvColumnMap(string headerLine)
    {
        string[] headers = headerLine.TrimEnd('\r').Split(',');
        Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            columns[headers[i]] = i;
        }

        return columns;
    }

    private static string ReadCsv(
        string[] values,
        Dictionary<string, int> columns,
        string columnName)
    {
        if (!columns.TryGetValue(columnName, out int index))
        {
            throw new InvalidDataException($"Missing column '{columnName}'.");
        }

        return index >= 0 && index < values.Length ? values[index] : string.Empty;
    }

    private static int ParseCsvInt(
        string[] values,
        Dictionary<string, int> columns,
        string columnName)
    {
        string value = ReadCsv(values, columns, columnName);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            throw new InvalidDataException($"Invalid integer in '{columnName}': '{value}'.");
        }

        return parsed;
    }

    private static int ParseOptionalCsvInt(
        string[] values,
        Dictionary<string, int> columns,
        string columnName,
        int fallback)
    {
        string value = ReadCsv(values, columns, columnName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;
    }

    private static float ParseCsvFloat(
        string[] values,
        Dictionary<string, int> columns,
        string columnName)
    {
        string value = ReadCsv(values, columns, columnName);
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            throw new InvalidDataException($"Invalid number in '{columnName}': '{value}'.");
        }

        return parsed;
    }

    private static bool ParseCsvBool(string value)
    {
        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }

        if (value == "1")
        {
            return true;
        }

        if (value == "0")
        {
            return false;
        }

        throw new InvalidDataException($"Invalid boolean: '{value}'.");
    }

    private static T1TargetSize ParseTargetSize(string value)
    {
        if (Enum.TryParse(value, true, out T1TargetSize parsed))
        {
            return parsed;
        }

        throw new InvalidDataException($"Unknown target size: '{value}'.");
    }

    private float GetTargetSizeDegrees(T1TargetSize targetSize)
    {
        switch (targetSize)
        {
            case T1TargetSize.Small:
                return Mathf.Max(0.1f, smallTargetSizeDegrees);
            case T1TargetSize.Large:
                return Mathf.Max(0.1f, largeTargetSizeDegrees);
            default:
                return Mathf.Max(0.1f, mediumTargetSizeDegrees);
        }
    }

    private T1MainTargetOrderList GetMainOrderForCondition(InteractionCondition condition)
    {
        if (condition == conditionBInteraction && condition != conditionAInteraction)
        {
            return conditionBMainOrder;
        }

        return conditionAMainOrder;
    }

    private static int GetOrderSeed(T1TargetOrderList orderList)
    {
        switch (orderList)
        {
            case T1TargetOrderList.A:
                return 101;
            case T1TargetOrderList.B:
                return 202;
            case T1TargetOrderList.C:
                return 303;
            case T1TargetOrderList.D:
                return 404;
            default:
                return 505;
        }
    }

    private static string GetDisplayName(Transform displayTransform, string fallback)
    {
        return displayTransform != null ? displayTransform.name : fallback;
    }

    private void ResolveReferences()
    {
        if (inputManager == null)
        {
            inputManager = PrototypeInputManager.Instance != null ? PrototypeInputManager.Instance : FindObjectOfType<PrototypeInputManager>();
        }

        if (experimentManager == null)
        {
            experimentManager = FindObjectOfType<ExperimentManager>();
        }

        if (displayManager == null)
        {
            displayManager = DisplayManager.Instance != null ? DisplayManager.Instance : FindObjectOfType<DisplayManager>();
        }

        if (errorEvaluator == null)
        {
            errorEvaluator = FindObjectOfType<ErrorEvaluator>();
        }

        if (virtualCursorController == null)
        {
            virtualCursorController = FindObjectOfType<VirtualCursorController>();
        }

        if (angularSizeReference == null && Camera.main != null)
        {
            angularSizeReference = Camera.main.transform;
        }

        if (logger == null)
        {
            logger = FindObjectOfType<Logger>();
        }

        if (displayA == null)
        {
            DisplaySurface foundA = FindDisplayByName("Display_A_Front");
            if (foundA != null)
            {
                displayA = foundA.transform;
            }
        }

        if (displayB == null)
        {
            DisplaySurface foundB = FindDisplayByName("Display_B_Back");
            if (foundB != null)
            {
                displayB = foundB.transform;
            }
        }
    }

    private DisplaySurface FindDisplayByName(string displayId)
    {
        if (displayManager == null || displayManager.Displays == null)
        {
            return null;
        }

        for (int i = 0; i < displayManager.Displays.Length; i++)
        {
            DisplaySurface display = displayManager.Displays[i];
            if (display != null && display.name == displayId)
            {
                return display;
            }
        }

        return null;
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }

    private struct ConditionBlock
    {
        public string Name;
        public InteractionCondition InteractionCondition;
        public T1MainTargetOrderList MainOrder;

        public ConditionBlock(
            string name,
            InteractionCondition interactionCondition,
            T1MainTargetOrderList mainOrder)
        {
            Name = name;
            InteractionCondition = interactionCondition;
            MainOrder = mainOrder;
        }
    }
}
