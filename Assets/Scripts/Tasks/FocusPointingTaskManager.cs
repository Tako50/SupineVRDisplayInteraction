using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class FocusPointingTrialConfig
{
    public int trialIndex;
    public InteractionCondition condition = InteractionCondition.RaycastBaseline;
    public DisplayLayoutPreset layoutPreset = DisplayLayoutPreset.StrongOcclusion;
    public string targetDisplayId = "Display_A_Front";
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

[DisallowMultipleComponent]
public class FocusPointingTaskManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private ErrorEvaluator errorEvaluator;
    [SerializeField] private Logger logger;

    [Header("Session")]
    [SerializeField] private string participantId = "P000";
    [SerializeField] private string sessionId = "S000";
    [SerializeField] private bool autoStartOnPlay = false;
    [SerializeField] private bool loopTrainingTrials = true;
    [SerializeField] private bool returnToConditionSelectionAfterMainTask = true;
    [SerializeField] private InteractionCondition selectedCondition = InteractionCondition.RaycastBaseline;
    [SerializeField] private DisplayLayoutPreset selectedLayout = DisplayLayoutPreset.StrongOcclusion;

    [Header("Targets")]
    [SerializeField] private Color targetColor = new Color(0.10f, 1f, 0.30f, 0.95f);
    [SerializeField] private Color inactiveTargetColor = new Color(0.10f, 1f, 0.30f, 0.35f);
    [SerializeField] private List<float> targetSizesNormalized = new List<float> { 0.10f, 0.14f };
    [SerializeField] private List<Vector2> targetPositionsNormalized = new List<Vector2>
    {
        new Vector2(0.25f, 0.35f),
        new Vector2(0.72f, 0.62f)
    };
    [SerializeField] private List<FocusPointingTrialConfig> trials = new List<FocusPointingTrialConfig>();

    private readonly Dictionary<DisplaySurface, RectTransform> targetRects = new Dictionary<DisplaySurface, RectTransform>();
    private int currentTrialListIndex = -1;
    private FocusPointingTrialConfig currentTrial;
    private FocusPointingTaskPhase currentPhase = FocusPointingTaskPhase.ConditionSelection;
    private float trialStartTime;
    private bool trialRunning;
    private bool taskRunning;

    public bool IsRunning => trialRunning;
    public bool IsTaskRunning => taskRunning;
    public FocusPointingTaskPhase CurrentPhase => currentPhase;
    public InteractionCondition SelectedCondition => selectedCondition;
    public DisplayLayoutPreset SelectedLayout => selectedLayout;
    public string CurrentTargetDisplayId => currentTrial != null ? currentTrial.targetDisplayId : "None";
    public int CurrentTrialIndex => currentTrial != null ? currentTrial.trialIndex : -1;
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
        HideAllTargets();
        SetDisplayContentMode(DisplayContentMode.ConditionSelection);

        if (inputManager != null)
        {
            inputManager.UnlockCondition();
        }

        Debug.Log("[FocusPointingTask] returned to condition selection");
    }

    private void BeginTask(FocusPointingTaskPhase phase, bool lockCondition)
    {
        ResolveReferences();
        EnsureDefaultTrials();
        EnsureTargets();
        HideAllTargets();
        SetDisplayContentMode(DisplayContentMode.PointingTask);

        currentPhase = phase;
        taskRunning = true;
        currentTrialListIndex = -1;

        if (inputManager != null)
        {
            if (lockCondition)
            {
                inputManager.LockCondition(selectedCondition);
            }
            else
            {
                inputManager.SetCondition(selectedCondition, true);
            }
        }

        if (experimentManager != null)
        {
            experimentManager.ApplyLayout(selectedLayout);
        }

        Debug.Log($"[FocusPointingTask] begin phase={currentPhase}, condition={selectedCondition}, layout={selectedLayout}, lockCondition={lockCondition}");
        StartNextTrial();
    }

    public void StartNextTrial()
    {
        ResolveReferences();
        EnsureDefaultTrials();
        EnsureTargets();
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

        currentTrial = trials[currentTrialListIndex];
        if (currentTrial.trialIndex <= 0)
        {
            currentTrial.trialIndex = currentTrialListIndex + 1;
        }

        currentTrial.condition = selectedCondition;
        currentTrial.layoutPreset = selectedLayout;

        trialStartTime = Time.time;
        trialRunning = true;
        ShowTargetForCurrentTrial();
        Debug.Log($"[FocusPointingTask] start phase={currentPhase}, trial={currentTrial.trialIndex}, condition={selectedCondition}, layout={selectedLayout}, targetDisplay={currentTrial.targetDisplayId}, target={Format(currentTrial.targetNormalizedPosition)}, size={currentTrial.targetSizeNormalized:0.000}");
    }

    public void HandleClick(FocusPointingClickEvent clickEvent)
    {
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

        if (logger != null)
        {
            logger.LogFocusPointingTrial(result);
        }

        Debug.Log($"[FocusPointingTask] result trial={result.TrialIndex}, result={result.ResultType}, clickedDisplay={result.ClickedDisplayId}, clicked={Format(result.ClickedNormalizedPosition)}, completion={result.CompletionTime:0.000}");
        trialRunning = false;
        HideAllTargets();
        StartNextTrial();
    }

    private void CompleteCurrentTask()
    {
        trialRunning = false;
        taskRunning = false;
        HideAllTargets();
        SetDisplayContentMode(DisplayContentMode.ConditionSelection);

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
        if (trials.Count > 0)
        {
            return;
        }

        string[] displayIds = { "Display_A_Front", "Display_B_Back" };

        int index = 1;
        for (int displayIndex = 0; displayIndex < displayIds.Length; displayIndex++)
        {
            for (int positionIndex = 0; positionIndex < targetPositionsNormalized.Count; positionIndex++)
            {
                float size = targetSizesNormalized.Count > 0
                    ? targetSizesNormalized[positionIndex % targetSizesNormalized.Count]
                    : 0.12f;

                trials.Add(new FocusPointingTrialConfig
                {
                    trialIndex = index,
                    condition = selectedCondition,
                    layoutPreset = selectedLayout,
                    targetDisplayId = displayIds[displayIndex],
                    targetNormalizedPosition = targetPositionsNormalized[positionIndex],
                    targetSizeNormalized = size
                });
                index++;
            }
        }
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

        float radius = currentTrial.targetSizeNormalized * 0.5f;
        return Vector2.Distance(normalized, currentTrial.targetNormalizedPosition) <= radius;
    }

    private DisplaySurface FindDisplay(string displayId)
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

    private void SetDisplayContentMode(DisplayContentMode mode)
    {
        if (displayManager == null)
        {
            return;
        }

        displayManager.SetAllDisplayContentMode(mode);
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
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
