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
    public string conditionName = "ConditionA";
    public InteractionCondition condition = InteractionCondition.RaycastBaseline;
    public DisplayLayoutPreset layoutPreset = DisplayLayoutPreset.StrongOcclusion;
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
    public InteractionCondition Condition;
    public DisplayLayoutPreset LayoutPreset;
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
    [SerializeField] private Color inactiveTargetColor = new Color(0.10f, 1f, 0.30f, 0.35f);
    [SerializeField] private float targetSizeNormalized = 0.10f;
    [SerializeField] private int repetitionsPerTargetPosition = 4;
    [SerializeField] private int targetColumns = 2;
    [SerializeField] private int targetRows = 3;
    [SerializeField] private Vector2 gridMinNormalized = new Vector2(0.15f, 0.08f);
    [SerializeField] private Vector2 gridMaxNormalized = new Vector2(0.85f, 0.92f);
    [SerializeField] private bool autoGenerateTrials = true;
    [SerializeField] private bool rebuildTrialsOnStart = true;
    [SerializeField] private bool randomizeTrialsWithinCondition = false;
    [SerializeField] private int randomSeed = 20260519;
    [SerializeField] private List<FocusPointingTrialConfig> trials = new List<FocusPointingTrialConfig>();

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
        // 本タスクでは開始時の条件をロックし、1条件48試行が終わるまで途中変更させない。
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
        Debug.Log($"[FocusPointingTask] start phase={currentPhase}, conditionName={currentTrial.conditionName}, trialInCondition={currentTrial.trialIndexInCondition}, global={currentTrial.globalTrialIndex}, condition={selectedCondition}, layout={selectedLayout}, targetDisplay={currentTrial.targetDisplayId}, targetId={currentTrial.targetPositionId}, target={Format(currentTrial.targetNormalizedPosition)}, size={currentTrial.targetSizeNormalized:0.000}");
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
            currentTrial.targetSizeNormalized,
            clickEvent.ClickedDisplayId,
            clickEvent.ClickedNormalizedPosition,
            clickEvent.HasValidDisplay);

        FocusPointingTrialResult result = new FocusPointingTrialResult
        {
            ParticipantId = participantId,
            SessionId = sessionId,
            TrialIndex = currentTrial.trialIndex,
            Condition = clickEvent.Condition,
            LayoutPreset = currentTrial.layoutPreset,
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
        LogT1Result(result, currentTrialAttemptIndex, evaluation.IsCorrect, false);

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

        Debug.Log($"[FocusPointingTask] result conditionName={currentTrial.conditionName}, trialInCondition={currentTrial.trialIndexInCondition}, global={currentTrial.globalTrialIndex}, result={result.ResultType}, clickedDisplay={result.ClickedDisplayId}, clicked={Format(result.ClickedNormalizedPosition)}, completion={result.CompletionTime:0.000}");
        trialRunning = false;
        HideAllTargets();
        StartNextTrial();
    }

    public void SelectCurrentTarget()
    {
        SelectCurrentTarget(Time.time);
    }

    public void SelectCurrentTarget(float timestamp)
    {
        if (!trialRunning || currentTrial == null)
        {
            return;
        }

        currentTrialAttemptIndex++;
        FocusPointingTrialResult result = new FocusPointingTrialResult
        {
            ParticipantId = participantId,
            SessionId = sessionId,
            TrialIndex = currentTrial.trialIndex,
            Condition = currentTrial.condition,
            LayoutPreset = currentTrial.layoutPreset,
            TargetDisplayId = currentTrial.targetDisplayId,
            TargetNormalizedPosition = currentTrial.targetNormalizedPosition,
            TargetSizeNormalized = currentTrial.targetSizeNormalized,
            ClickedDisplayId = currentTrial.targetDisplayId,
            ClickedNormalizedPosition = currentTrial.targetNormalizedPosition,
            ResultType = FocusPointingResultType.Correct,
            IsCorrect = true,
            TrialStartTime = trialStartTime,
            ClickTime = timestamp,
            CompletionTime = timestamp - trialStartTime
        };
        LogT1Result(result, currentTrialAttemptIndex, true, true);
        LogTargetSelectionResult(timestamp);
        Debug.Log($"[FocusPointingTask] selected current target conditionName={currentTrial.conditionName}, trialInCondition={currentTrial.trialIndexInCondition}, global={currentTrial.globalTrialIndex}, target={currentTrial.targetPositionId}, completion={timestamp - trialStartTime:0.000}");
        trialRunning = false;
        HideAllTargets();
        StartNextTrial();
    }

    public void OnTargetSelected(TargetSelectable selectable)
    {
        if (selectable == null || currentTrial == null)
        {
            return;
        }

        if (selectable.DisplayName != currentTrial.targetDisplayId || selectable.TargetPositionId != currentTrial.targetPositionId)
        {
            Debug.Log($"[FocusPointingTask] ignored non-current target selectable display={selectable.DisplayName}, target={selectable.TargetPositionId}");
            return;
        }

        SelectCurrentTarget();
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
        // 2条件それぞれに対して、2表示 x 2列 x 3行 x 4反復 = 48試行を生成する。
        if (!autoGenerateTrials)
        {
            if (trials.Count == 0)
            {
                BuildLegacyDefaultTrials();
            }

            return;
        }

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
        int globalIndex = 1;
        for (int conditionIndex = 0; conditionIndex < conditionBlocks.Count; conditionIndex++)
        {
            List<FocusPointingTrialConfig> conditionTrials = BuildTrialsForCondition(conditionBlocks[conditionIndex]);
            if (randomizeTrialsWithinCondition)
            {
                Shuffle(conditionTrials, randomSeed + conditionIndex);
            }

            for (int i = 0; i < conditionTrials.Count; i++)
            {
                FocusPointingTrialConfig trial = conditionTrials[i];
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

    private void BuildLegacyDefaultTrials()
    {
        string[] displayIds = { GetDisplayName(displayA, "Display_A_Front"), GetDisplayName(displayB, "Display_B_Back") };
        List<Vector2> positions = GenerateGridPositions();

        int index = 1;
        for (int displayIndex = 0; displayIndex < displayIds.Length; displayIndex++)
        {
            for (int positionIndex = 0; positionIndex < positions.Count; positionIndex++)
            {
                trials.Add(new FocusPointingTrialConfig
                {
                    trialIndex = index,
                    globalTrialIndex = index,
                    trialIndexInCondition = index,
                    conditionName = "SelectedCondition",
                    condition = selectedCondition,
                    layoutPreset = selectedLayout,
                    targetDisplayId = displayIds[displayIndex],
                    targetPositionId = BuildTargetPositionId(displayIds[displayIndex], positionIndex),
                    targetNormalizedPosition = positions[positionIndex],
                    targetSizeNormalized = this.targetSizeNormalized
                });
                index++;
            }
        }
    }

    private List<FocusPointingTrialConfig> BuildTrialsForCondition(ConditionBlock conditionBlock)
    {
        List<FocusPointingTrialConfig> conditionTrials = new List<FocusPointingTrialConfig>();
        string[] displayIds = { GetDisplayName(displayA, "Display_A_Front"), GetDisplayName(displayB, "Display_B_Back") };
        List<Vector2> positions = GenerateGridPositions();

        for (int repetition = 0; repetition < Mathf.Max(1, repetitionsPerTargetPosition); repetition++)
        {
            for (int displayIndex = 0; displayIndex < displayIds.Length; displayIndex++)
            {
                for (int positionIndex = 0; positionIndex < positions.Count; positionIndex++)
                {
                    conditionTrials.Add(new FocusPointingTrialConfig
                    {
                        conditionName = conditionBlock.Name,
                        condition = conditionBlock.InteractionCondition,
                        layoutPreset = selectedLayout,
                        targetDisplayId = displayIds[displayIndex],
                        targetPositionId = BuildTargetPositionId(displayIds[displayIndex], positionIndex),
                        targetNormalizedPosition = positions[positionIndex],
                        targetSizeNormalized = this.targetSizeNormalized
                    });
                }
            }
        }

        return conditionTrials;
    }

    private List<Vector2> GenerateGridPositions()
    {
        // Inspectorのmin/maxでターゲット配置範囲を調整できる。現在は端寄り配置。
        List<Vector2> positions = new List<Vector2>();
        int columns = Mathf.Max(1, targetColumns);
        int rows = Mathf.Max(1, targetRows);
        for (int row = 0; row < rows; row++)
        {
            float y = rows == 1 ? 0.5f : Mathf.Lerp(gridMinNormalized.y, gridMaxNormalized.y, row / (float)(rows - 1));
            for (int column = 0; column < columns; column++)
            {
                float x = columns == 1 ? 0.5f : Mathf.Lerp(gridMinNormalized.x, gridMaxNormalized.x, column / (float)(columns - 1));
                positions.Add(new Vector2(x, y));
            }
        }

        return positions;
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
        TargetSelectable selectable = targetObject.GetComponent<TargetSelectable>();
        if (selectable == null)
        {
            selectable = targetObject.AddComponent<TargetSelectable>();
        }

        selectable.Initialize(this, display.name, string.Empty);
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

        TargetSelectable selectable = targetRect.GetComponent<TargetSelectable>();
        if (selectable != null)
        {
            selectable.Initialize(this, display.name, currentTrial.targetPositionId);
        }
    }

    private bool ContainsTarget(Vector2 normalized)
    {
        if (currentTrial == null)
        {
            return false;
        }

        float radius = currentTrial.targetSizeNormalized * 0.5f;
        return Vector2.Distance(normalized, currentTrial.targetNormalizedPosition) <= radius;
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
                currentTrial.trialIndexInCondition.ToString(CultureInfo.InvariantCulture),
                currentTrial.globalTrialIndex.ToString(CultureInfo.InvariantCulture),
                Escape(currentTrial.targetDisplayId),
                Escape(currentTrial.targetPositionId),
                currentTrial.targetNormalizedPosition.x.ToString("0.000", CultureInfo.InvariantCulture),
                currentTrial.targetNormalizedPosition.y.ToString("0.000", CultureInfo.InvariantCulture),
                responseTime.ToString("0.000", CultureInfo.InvariantCulture),
                DateTime.Now.ToString("o", CultureInfo.InvariantCulture)));
            targetSelectionWriter.Flush();
        }
    }

    private void LogT1Result(FocusPointingTrialResult result, int attemptIndex, bool advancesTrial, bool directSelect)
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
            currentTrial.trialIndexInCondition.ToString(CultureInfo.InvariantCulture),
            currentTrial.globalTrialIndex.ToString(CultureInfo.InvariantCulture),
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
            Bool(directSelect),
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
        targetSelectionWriter.WriteLine("participantId,conditionName,trialIndexInCondition,globalTrialIndex,displayName,targetPositionId,targetLocalX,targetLocalY,responseTime,timestamp");
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
        t1ResultsWriter.WriteLine("participantId,sessionId,taskPhase,conditionName,interactionCondition,layoutPreset,trialIndexInCondition,globalTrialIndex,targetDisplayId,targetPositionId,targetLocalX,targetLocalY,targetSize,attemptIndex,clickedDisplayId,clickedLocalX,clickedLocalY,resultType,isCorrect,displayError,targetError,miss,advancesTrial,directSelect,trialStartTime,clickTime,responseTime,timestamp");
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
