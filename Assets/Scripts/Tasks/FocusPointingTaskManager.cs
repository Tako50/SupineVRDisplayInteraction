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
    public string conditionName = "ConditionA";
    public InteractionCondition condition = InteractionCondition.RaycastBaseline;
    public DisplayLayoutPreset layoutPreset = DisplayLayoutPreset.StrongOcclusion;
    public T1OcclusionType occlusionType = T1OcclusionType.Front;
    public string targetDisplayId = "Display_A_Front";
    public string targetPositionId = "Display_A_Front_C1_R1";
    public Vector2 targetNormalizedPosition = new Vector2(0.5f, 0.5f);
    public float targetSizeNormalized = 0.12f;
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
    [SerializeField] private DisplayLayoutPreset selectedLayout = DisplayLayoutPreset.StrongOcclusion;

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
    [SerializeField] private float targetSizeNormalized = 0.10f;
    [SerializeField] private int repetitionsPerTargetPosition = 4;
    [SerializeField] private bool rebuildTrialsOnStart = true;
    [SerializeField] private bool randomizeTrialsWithinCondition = false;
    [SerializeField] private int randomSeed = 20260519;
    [SerializeField] private List<FocusPointingTrialConfig> trials = new List<FocusPointingTrialConfig>();

    [Header("T1 Experiment Trial Set")]
    [SerializeField] private int frontTrialsPerCondition = 40;
    [SerializeField] private int backClearTrialsPerCondition = 20;
    [SerializeField] private int backOccludedTrialsPerCondition = 20;
    [SerializeField] private Vector2[] frontTargetPositions =
    {
        new Vector2(0.18f, 0.18f),
        new Vector2(0.42f, 0.18f),
        new Vector2(0.66f, 0.18f),
        new Vector2(0.82f, 0.34f),
        new Vector2(0.18f, 0.50f),
        new Vector2(0.42f, 0.66f),
        new Vector2(0.66f, 0.82f),
        new Vector2(0.82f, 0.82f)
    };
    [SerializeField] private Vector2[] backClearTargetPositions =
    {
        new Vector2(0.18f, 0.72f),
        new Vector2(0.38f, 0.84f),
        new Vector2(0.62f, 0.84f),
        new Vector2(0.82f, 0.72f)
    };
    [SerializeField] private Vector2[] backOccludedTargetPositions =
    {
        new Vector2(0.38f, 0.28f),
        new Vector2(0.62f, 0.28f),
        new Vector2(0.38f, 0.46f),
        new Vector2(0.62f, 0.46f)
    };

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
    private RectTransform startGateRect;
    private Image startGateImage;
    private Text startGateText;
    private bool waitingForStartButton;
    private bool startCountdownRunning;
    private float startCountdownEndTime;

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

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureDefaultTrials();
        EnsureTargets();

        if (autoStartOnPlay)
        {
            BeginTrainingTask();
        }
    }

    private void Update()
    {
        UpdateStartGateCountdown();
        UpdateTargetHoverVisual();
    }

    private void OnDisable()
    {
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

    public void SetRandomizeTrialsWithinCondition(bool randomize)
    {
        if (taskRunning)
        {
            Debug.Log(
                $"[FocusPointingTask] trial order change ignored while task is running. "
                + $"requestedRandom={randomize}, activeRandom={randomizeTrialsWithinCondition}");
            return;
        }

        if (randomizeTrialsWithinCondition == randomize)
        {
            return;
        }

        randomizeTrialsWithinCondition = randomize;
        trialsGenerated = false;
        Debug.Log(
            $"[FocusPointingTask] trialOrder="
            + $"{(randomizeTrialsWithinCondition ? "Random" : "Fixed")}, seed={randomSeed}");
    }

    public bool RandomizeTrialsWithinCondition => randomizeTrialsWithinCondition;
    public int RandomSeed => randomSeed;

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
        waitingForStartButton = false;
        startCountdownRunning = false;
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
        // 本タスクでは開始時の条件をロックし、1条件80試行が終わるまで途中変更させない。
        ResolveReferences();
        if (rebuildTrialsOnStart)
        {
            trialsGenerated = false;
        }

        EnsureDefaultTrials();
        EnsureTargets();
        HideAllTargets();
        SetDisplayContentMode(DisplayContentMode.PointingTask);
        OpenTargetSelectionCsv();
        OpenT1ResultsCsv();

        currentPhase = phase;
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
        if (!waitingForStartButton || startCountdownRunning || string.IsNullOrEmpty(displayId))
        {
            return false;
        }

        if (displayId != startGateDisplayId || !GetStartButtonRect().Contains(normalizedPosition))
        {
            return false;
        }

        BeginStartGateCountdown();
        return true;
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
        if (currentTrialListIndex >= trials.Count)
        {
            bool shouldLoop = currentPhase == FocusPointingTaskPhase.Training && loopTrainingTrials;
            if (!shouldLoop)
            {
                CompleteCurrentTask();
                return;
            }

            currentTrialListIndex = 0;
        }

        if (trials[currentTrialListIndex].condition != activeTaskCondition)
        {
            Debug.Log($"[FocusPointingTask] completed condition block condition={activeTaskCondition}, phase={currentPhase}");
            CompleteCurrentTask();
            return;
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
        Debug.Log($"[FocusPointingTask] start phase={currentPhase}, conditionName={currentTrial.conditionName}, trialInCondition={currentTrial.trialIndexInCondition}, trialSetId={currentTrial.trialSetId}, global={currentTrial.globalTrialIndex}, condition={selectedCondition}, layout={selectedLayout}, occlusion={currentTrial.occlusionType}, targetDisplay={currentTrial.targetDisplayId}, targetId={currentTrial.targetPositionId}, target={Format(currentTrial.targetNormalizedPosition)}, size={currentTrial.targetSizeNormalized:0.000}");
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
        waitingForStartButton = false;
        startCountdownRunning = false;
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
        // 本実験仕様: 1条件あたり Front 40 / BackClear 20 / BackOccluded 20 = 80試行。
        // まず条件非依存のtrial setを作り、それを各条件へ複製して同一trial setを保証する。
        if (trialsGenerated && trials.Count > 0)
        {
            return;
        }

        if (trials.Count > 0 && !rebuildTrialsOnStart)
        {
            trialsGenerated = true;
            return;
        }

        trials.Clear();
        List<ConditionBlock> conditionBlocks = GetConditionBlocksInOrder();
        List<FocusPointingTrialConfig> baseTrialSet = BuildT1ExperimentTrialSet();
        if (randomizeTrialsWithinCondition)
        {
            Shuffle(baseTrialSet, randomSeed);
        }

        int globalIndex = 1;
        for (int conditionIndex = 0; conditionIndex < conditionBlocks.Count; conditionIndex++)
        {
            ConditionBlock conditionBlock = conditionBlocks[conditionIndex];
            for (int i = 0; i < baseTrialSet.Count; i++)
            {
                FocusPointingTrialConfig trial = CloneTrialForCondition(baseTrialSet[i], conditionBlock);
                trial.globalTrialIndex = globalIndex;
                trial.trialIndex = globalIndex;
                trial.trialIndexInCondition = i + 1;
                trial.layoutPreset = selectedLayout;
                trials.Add(trial);
                globalIndex++;
            }
        }

        trialsGenerated = true;
    }

    private List<FocusPointingTrialConfig> BuildT1ExperimentTrialSet()
    {
        List<FocusPointingTrialConfig> trialSet = new List<FocusPointingTrialConfig>();
        string frontDisplayId = GetDisplayName(displayA, "Display_A_Front");
        string backDisplayId = GetDisplayName(displayB, "Display_B_Back");

        AppendTrialsForOcclusionType(trialSet, T1OcclusionType.Front, frontDisplayId, frontTargetPositions, frontTrialsPerCondition);
        AppendTrialsForOcclusionType(trialSet, T1OcclusionType.BackClear, backDisplayId, backClearTargetPositions, backClearTrialsPerCondition);
        AppendTrialsForOcclusionType(trialSet, T1OcclusionType.BackOccluded, backDisplayId, backOccludedTargetPositions, backOccludedTrialsPerCondition);

        for (int i = 0; i < trialSet.Count; i++)
        {
            trialSet[i].trialSetId = i + 1;
            trialSet[i].trialIndexInSet = i + 1;
            trialSet[i].trialIndexInCondition = i + 1;
        }

        return trialSet;
    }

    private void AppendTrialsForOcclusionType(
        List<FocusPointingTrialConfig> destination,
        T1OcclusionType occlusionType,
        string displayId,
        Vector2[] configuredPositions,
        int requestedCount)
    {
        int count = Mathf.Max(0, requestedCount);
        if (count == 0)
        {
            return;
        }

        Vector2[] positions = configuredPositions != null && configuredPositions.Length > 0
            ? configuredPositions
            : new[] { new Vector2(0.5f, 0.5f) };

        for (int i = 0; i < count; i++)
        {
            int positionIndex = i % positions.Length;
            int repetition = i / positions.Length + 1;
            destination.Add(new FocusPointingTrialConfig
            {
                conditionName = "BaseTrialSet",
                condition = InteractionCondition.RaycastBaseline,
                layoutPreset = selectedLayout,
                occlusionType = occlusionType,
                targetDisplayId = displayId,
                targetPositionId = BuildTargetPositionId(displayId, occlusionType, positionIndex, repetition),
                targetNormalizedPosition = positions[positionIndex],
                targetSizeNormalized = this.targetSizeNormalized
            });
        }
    }

    private FocusPointingTrialConfig CloneTrialForCondition(FocusPointingTrialConfig source, ConditionBlock conditionBlock)
    {
        return new FocusPointingTrialConfig
        {
            trialSetId = source.trialSetId,
            trialIndexInSet = source.trialIndexInSet,
            conditionName = conditionBlock.Name,
            condition = conditionBlock.InteractionCondition,
            layoutPreset = selectedLayout,
            occlusionType = source.occlusionType,
            targetDisplayId = source.targetDisplayId,
            targetPositionId = source.targetPositionId,
            targetNormalizedPosition = source.targetNormalizedPosition,
            targetSizeNormalized = source.targetSizeNormalized
        };
    }

    private List<ConditionBlock> GetConditionBlocksInOrder()
    {
        ConditionBlock conditionA = new ConditionBlock(conditionAName, conditionAInteraction);
        ConditionBlock conditionB = new ConditionBlock(conditionBName, conditionBInteraction);
        return conditionOrder == TargetSelectionConditionOrder.AThenB
            ? new List<ConditionBlock> { conditionA, conditionB }
            : new List<ConditionBlock> { conditionB, conditionA };
    }

    private static void Shuffle(List<FocusPointingTrialConfig> list, int seed)
    {
        System.Random random = new System.Random(seed);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            FocusPointingTrialConfig temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
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

        RectTransform canvasRect = display.WorldSpaceCanvas.GetComponent<RectTransform>();
        Vector2 canvasSize = canvasRect != null ? canvasRect.sizeDelta : display.CanvasPixelSize;
        Vector2 targetPosition = currentTrial.targetNormalizedPosition;
        float targetPixelSize = Mathf.Max(8f, currentTrial.targetSizeNormalized * Mathf.Min(canvasSize.x, canvasSize.y));

        targetRect.anchoredPosition = new Vector2(
            (Mathf.Clamp01(targetPosition.x) - 0.5f) * canvasSize.x,
            (Mathf.Clamp01(targetPosition.y) - 0.5f) * canvasSize.y);
        targetRect.sizeDelta = Vector2.one * targetPixelSize;
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

        DisplaySurface display = FindDisplay(currentTrial.targetDisplayId);
        if (display == null)
        {
            return Vector2.one * Mathf.Max(0f, currentTrial.targetSizeNormalized);
        }

        RectTransform canvasRect = display.WorldSpaceCanvas != null
            ? display.WorldSpaceCanvas.GetComponent<RectTransform>()
            : null;
        Vector2 canvasSize = canvasRect != null ? canvasRect.sizeDelta : display.CanvasPixelSize;
        if (canvasSize.x <= 0f || canvasSize.y <= 0f)
        {
            return Vector2.one * Mathf.Max(0f, currentTrial.targetSizeNormalized);
        }

        float targetPixelSize = Mathf.Max(
            8f,
            currentTrial.targetSizeNormalized * Mathf.Min(canvasSize.x, canvasSize.y));
        return new Vector2(targetPixelSize / canvasSize.x, targetPixelSize / canvasSize.y);
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
        EnsureStartGate();
        HideAllTargets();
        waitingForStartButton = true;
        startCountdownRunning = false;

        if (startGateRect == null)
        {
            Debug.LogWarning("[FocusPointingTask] Start gate display was not found. Starting task without start gate.");
            waitingForStartButton = false;
            StartNextTrial();
            return;
        }

        ApplyStartGateRect();
        startGateRect.gameObject.SetActive(true);
        startGateRect.SetAsLastSibling();

        if (startGateImage != null)
        {
            startGateImage.color = startGateButtonColor;
        }

        if (startGateText != null)
        {
            startGateText.text = "START";
        }

        Debug.Log($"[FocusPointingTask] waiting for start button phase={currentPhase}, display={startGateDisplayId}");
    }

    private void BeginStartGateCountdown()
    {
        if (!waitingForStartButton)
        {
            return;
        }

        startCountdownRunning = true;
        startCountdownEndTime = Time.time + Mathf.Max(0f, startCountdownSeconds);
        if (startGateImage != null)
        {
            startGateImage.color = startGateCountdownColor;
        }

        UpdateStartGateCountdownText();
        Debug.Log($"[FocusPointingTask] start button pressed. countdown={startCountdownSeconds:0.0}s");
    }

    private void UpdateStartGateCountdown()
    {
        if (!startCountdownRunning)
        {
            return;
        }

        UpdateStartGateCountdownText();
        if (Time.time < startCountdownEndTime)
        {
            return;
        }

        waitingForStartButton = false;
        startCountdownRunning = false;
        HideStartGate();
        StartNextTrial();
    }

    private void UpdateStartGateCountdownText()
    {
        if (startGateText == null)
        {
            return;
        }

        float remaining = Mathf.Max(0f, startCountdownEndTime - Time.time);
        int count = Mathf.Max(1, Mathf.CeilToInt(remaining));
        startGateText.text = count.ToString(CultureInfo.InvariantCulture);
    }

    private void HideStartGate()
    {
        if (startGateRect != null)
        {
            startGateRect.gameObject.SetActive(false);
        }
    }

    private void EnsureStartGate()
    {
        if (startGateRect != null)
        {
            return;
        }

        DisplaySurface display = FindDisplay(startGateDisplayId);
        if (display == null || display.WorldSpaceCanvas == null)
        {
            return;
        }

        Transform canvasTransform = display.WorldSpaceCanvas.transform;
        Transform existing = canvasTransform.Find("TaskStartGate");
        GameObject gateObject = existing != null ? existing.gameObject : new GameObject("TaskStartGate", typeof(RectTransform));
        gateObject.transform.SetParent(canvasTransform, false);

        startGateRect = gateObject.GetComponent<RectTransform>();
        startGateRect.anchorMin = new Vector2(0.5f, 0.5f);
        startGateRect.anchorMax = new Vector2(0.5f, 0.5f);
        startGateRect.pivot = new Vector2(0.5f, 0.5f);
        ApplyStartGateRect();

        startGateImage = gateObject.GetComponent<Image>();
        if (startGateImage == null)
        {
            startGateImage = gateObject.AddComponent<Image>();
        }

        startGateImage.color = startGateButtonColor;

        Transform textTransform = gateObject.transform.Find("Label");
        GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(gateObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        startGateText = textObject.GetComponent<Text>();
        if (startGateText == null)
        {
            startGateText = textObject.AddComponent<Text>();
        }

        startGateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        startGateText.fontSize = 42;
        startGateText.fontStyle = FontStyle.Bold;
        startGateText.alignment = TextAnchor.MiddleCenter;
        startGateText.color = Color.white;
        startGateText.text = "START";
        gateObject.SetActive(false);
    }

    private void ApplyStartGateRect()
    {
        if (startGateRect == null)
        {
            return;
        }

        DisplaySurface display = FindDisplay(startGateDisplayId);
        if (display == null || display.WorldSpaceCanvas == null)
        {
            return;
        }

        RectTransform canvasRect = display.WorldSpaceCanvas.GetComponent<RectTransform>();
        Vector2 canvasSize = canvasRect != null ? canvasRect.sizeDelta : display.CanvasPixelSize;
        startGateRect.anchoredPosition = new Vector2(
            (Mathf.Clamp01(startButtonNormalizedPosition.x) - 0.5f) * canvasSize.x,
            (Mathf.Clamp01(startButtonNormalizedPosition.y) - 0.5f) * canvasSize.y);
        startGateRect.sizeDelta = new Vector2(
            Mathf.Max(32f, startButtonNormalizedSize.x * canvasSize.x),
            Mathf.Max(32f, startButtonNormalizedSize.y * canvasSize.y));
    }

    private Rect GetStartButtonRect()
    {
        Vector2 size = startButtonNormalizedSize;
        Vector2 center = startButtonNormalizedPosition;
        return new Rect(center - size * 0.5f, size);
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
                randomizeTrialsWithinCondition ? "Random" : "Fixed",
                randomSeed.ToString(CultureInfo.InvariantCulture),
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
            randomizeTrialsWithinCondition ? "Random" : "Fixed",
            randomSeed.ToString(CultureInfo.InvariantCulture),
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
        targetSelectionWriter.WriteLine("participantId,conditionName,trialOrder,randomSeed,trialIndexInCondition,globalTrialIndex,trialSetId,trialIndexInSet,occlusion_type,displayName,targetPositionId,targetLocalX,targetLocalY,responseTime,timestamp");
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
        t1ResultsWriter.WriteLine("participantId,sessionId,taskPhase,conditionName,interactionCondition,layoutPreset,trialOrder,randomSeed,trialIndexInCondition,globalTrialIndex,trialSetId,trialIndexInSet,occlusion_type,targetDisplayId,targetPositionId,targetLocalX,targetLocalY,targetSize,attemptIndex,clickedDisplayId,clickedLocalX,clickedLocalY,resultType,isCorrect,displayError,targetError,miss,advancesTrial,trialStartTime,clickTime,responseTime,timestamp");
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

    private static string BuildTargetPositionId(string displayId, int positionIndex)
    {
        int column = positionIndex % 2 + 1;
        int row = positionIndex / 2 + 1;
        return $"{displayId}_C{column}_R{row}";
    }

    private static string BuildTargetPositionId(string displayId, T1OcclusionType occlusionType, int positionIndex, int repetition)
    {
        return $"{displayId}_{occlusionType}_P{positionIndex + 1}_Rep{repetition}";
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

        public ConditionBlock(string name, InteractionCondition interactionCondition)
        {
            Name = name;
            InteractionCondition = interactionCondition;
        }
    }
}
