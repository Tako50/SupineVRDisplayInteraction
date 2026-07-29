using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// プロトタイプ全体の現在条件・配置プリセット・デバッグ状態を束ねる軽い管理役。
/// タスク本体はFocusPointingTaskManagerに任せ、ここでは条件とレイアウトの適用を中心に扱う。
/// </summary>
public class ExperimentManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private DisplayLayoutManager layoutManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private FocusManager focusManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private ScrollController scrollController;
    [SerializeField] private GazeDisplayFocusManager gazeDisplayFocusManager;
    [SerializeField] private EditorDebugInputProvider debugInputProvider;
    [SerializeField] private Logger logger;

    [Header("Experiment State")]
    [SerializeField] private InteractionCondition startingCondition = InteractionCondition.RaycastBaseline;
    [SerializeField] private DisplayLayoutPreset startingLayout = DisplayLayoutPreset.UpDownDepth;
    [SerializeField] private bool applyStartingLayoutOnStart = true;
    [SerializeField] private bool allowLayoutSwitching = false;
    [SerializeField] private bool lockDisplaysAfterStart = true;
    [SerializeField] private float debugLogIntervalSeconds = 0.5f;

    private DisplayLayoutPreset currentLayout;
    private bool currentRayVisualEnabled;
    private RayVisualLengthLevel currentRayLengthLevel;
    private InteractionCondition lastRayVisualCondition;
    private bool hasAppliedRayVisualCondition;
    private float nextDebugLogTime;

    public InteractionCondition CurrentCondition => inputManager != null ? inputManager.CurrentCondition : startingCondition;
    public bool HighlightEnabled => gazeDisplayFocusManager != null
        ? gazeDisplayFocusManager.HighlightEnabled
        : true;
    public DisplayLayoutPreset CurrentLayout => currentLayout;
    public bool RayVisualEnabled => currentRayVisualEnabled;
    public RayVisualLengthLevel RayLengthLevel => currentRayLengthLevel;
    public float RayVisualLengthMeters => raycastPointer != null ? raycastPointer.VisibleRayLengthMeters : 0f;

    private void Awake()
    {
        ResolveReferences();
        currentLayout = startingLayout;

        if (inputManager != null)
        {
            inputManager.SetCondition(startingCondition);
        }

        SetHighlightEnabled(true);
        ApplyRayVisualForCurrentCondition();
    }

    private void Start()
    {
        if (applyStartingLayoutOnStart)
        {
            ApplyLayout(startingLayout);
        }
    }

    private void Update()
    {
        ResolveReferences();
        ApplyRayVisualIfConditionChanged();
        HandleKeyboardShortcuts();
        SampleDebugState();
    }

    public void ApplyLayout(DisplayLayoutPreset preset)
    {
        // 通常は開始時HMD基準で固定。必要なときだけHMD正面リセットで配置基準を取り直す。
        currentLayout = preset;
        if (!allowLayoutSwitching)
        {
            Debug.Log($"[ExperimentManager] layout switching disabled. Kept fixed display transforms. requested={preset}");
            return;
        }

        if (layoutManager != null)
        {
            if (lockDisplaysAfterStart)
            {
                layoutManager.ApplyLayout(preset);
            }
            else
            {
                layoutManager.ResetLayoutFromCurrentHmd(preset);
            }
        }

        Debug.Log($"[ExperimentManager] layout={preset}");
    }

    public void SetApplyStartingLayoutOnStart(bool enabled)
    {
        applyStartingLayoutOnStart = enabled;
    }

    public void SetAllowLayoutSwitching(bool enabled)
    {
        allowLayoutSwitching = enabled;
    }

    public void SetHighlightEnabled(bool enabled)
    {
        // Highlight is fixed ON for both experiment methods.
        enabled = true;
        if (gazeDisplayFocusManager != null)
        {
            gazeDisplayFocusManager.SetHighlightEnabled(enabled);
        }

        ApplyRayVisualForCurrentCondition();
    }

    public void ApplyRayVisualForCurrentCondition()
    {
        bool showBaselineLongRay = CurrentCondition == InteractionCondition.RaycastBaseline
            || CurrentCondition == InteractionCondition.GazeRay;
        ApplyRayVisualSettings(showBaselineLongRay, RayVisualLengthLevel.Long);
        lastRayVisualCondition = CurrentCondition;
        hasAppliedRayVisualCondition = true;
    }

    private void ApplyRayVisualSettings(bool enabled, RayVisualLengthLevel level)
    {
        bool changed = currentRayVisualEnabled != enabled || currentRayLengthLevel != level;
        currentRayVisualEnabled = enabled;
        currentRayLengthLevel = level;

        if (raycastPointer != null)
        {
            raycastPointer.SetRayVisualSettings(enabled, level);
        }

        if (changed)
        {
            Debug.Log(
                $"[ExperimentManager] rayVisualEnabled={enabled}, "
                + $"rayLengthLevel={level}, rayLengthMeters={RayVisualLengthMeters:0.00}");
        }
    }

    private void ApplyRayVisualIfConditionChanged()
    {
        if (!HighlightEnabled)
        {
            SetHighlightEnabled(true);
        }

        if (!hasAppliedRayVisualCondition
            || lastRayVisualCondition != CurrentCondition)
        {
            ApplyRayVisualForCurrentCondition();
        }
    }

    public void SetInteractionAndHighlight(InteractionCondition interactionMethod, bool enabled)
    {
        if (inputManager != null)
        {
            inputManager.SetCondition(interactionMethod);
        }

        SetHighlightEnabled(enabled);
    }

    private void HandleKeyboardShortcuts()
    {
        if (!allowLayoutSwitching || debugInputProvider == null || !debugInputProvider.IsEnabled)
        {
            return;
        }

        if (debugInputProvider.UpDownDepthPressed)
        {
            ApplyLayout(DisplayLayoutPreset.UpDownDepth);
        }
        else if (debugInputProvider.LeftRightPressed)
        {
            ApplyLayout(DisplayLayoutPreset.LeftRight);
        }
        else if (debugInputProvider.UpDownPressed)
        {
            ApplyLayout(DisplayLayoutPreset.UpDown);
        }
    }

    private void SampleDebugState()
    {
        if (displayManager == null || gazeProvider == null || inputManager == null)
        {
            return;
        }

        bool hasRayHit = displayManager.HasCurrentRaycastHit;
        DisplayHit rayHit = hasRayHit ? displayManager.CurrentRaycastHit : default;
        Ray controllerRay = raycastPointer != null ? raycastPointer.CurrentRay : default;

        if (logger != null && logger.ShouldSample())
        {
            bool gazeValid = gazeProvider.TryGetValidGazeRay(out Ray gazeRay);
            DisplayHit gazeHit = default;
            bool hasGazeHit = gazeValid
                && displayManager.TryGetForemostHit(gazeRay, out gazeHit);

            bool hasCursor = false;
            string cursorDisplayId = "None";
            Vector2 cursorNormalized = Vector2.zero;
            if ((inputManager.CurrentCondition == InteractionCondition.RaycastBaseline
                    || inputManager.CurrentCondition == InteractionCondition.GazeRay)
                && hasRayHit)
            {
                hasCursor = true;
                cursorDisplayId = rayHit.DisplayId;
                cursorNormalized = rayHit.Normalized;
            }
            else if (inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus
                && displayManager.FocusedDisplay != null
                && virtualCursorController != null)
            {
                hasCursor = true;
                cursorDisplayId = displayManager.FocusedDisplay.name;
                cursorNormalized = virtualCursorController.NormalizedPosition;
            }

            logger.LogFrame(
                inputManager.CurrentCondition,
                currentLayout,
                rayHit,
                hasRayHit,
                gazeHit,
                hasGazeHit,
                displayManager.FocusedDisplay,
                cursorDisplayId,
                cursorNormalized,
                hasCursor,
                controllerRay.origin,
                controllerRay.direction);

            if (inputManager.CurrentCondition == InteractionCondition.GazeRay && raycastPointer != null)
            {
                logger.LogGazeRayFrame(
                    raycastPointer.GazeValid,
                    raycastPointer.GazeHitDisplayId,
                    raycastPointer.GazeSelectedDisplay != null ? raycastPointer.GazeSelectedDisplay.name : "None",
                    rayHit,
                    hasRayHit,
                    controllerRay.origin,
                    controllerRay.direction,
                    raycastPointer.PenetratedDisplayIds,
                    raycastPointer.GazeDisplaySwitchCount,
                    raycastPointer.TriggerPressCount,
                    scrollController != null ? scrollController.LastScrollDisplayId : "None",
                    raycastPointer.InvalidGazePolicy,
                    raycastPointer.GazeDisplaySwitchDwellSeconds);
            }
        }

        if (Time.time >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.time + debugLogIntervalSeconds;
            string rayDisplay = hasRayHit ? $"{rayHit.DisplayId} {Format(rayHit.Normalized)}" : "None";
            string focusDisplay = displayManager.FocusedDisplay != null ? displayManager.FocusedDisplay.name : "None";
            string focusState = focusManager != null ? focusManager.CurrentState.ToString() : "Unknown";
            string candidateIds = focusManager != null ? focusManager.CurrentCandidateIds : "None";
            Debug.Log($"[ExperimentManager] condition={inputManager.CurrentCondition}, layout={currentLayout}, rayHit={rayDisplay}, focused={focusDisplay}, focusState={focusState}, gazeCandidates={candidateIds}, controllerRayOrigin={controllerRay.origin}, controllerRayDirection={controllerRay.direction}");
        }
    }

    private void ResolveReferences()
    {
        if (inputManager == null)
        {
            inputManager = PrototypeInputManager.Instance != null ? PrototypeInputManager.Instance : FindObjectOfType<PrototypeInputManager>();
        }

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }

        if (displayManager == null)
        {
            displayManager = DisplayManager.Instance != null ? DisplayManager.Instance : FindObjectOfType<DisplayManager>();
        }

        if (layoutManager == null)
        {
            layoutManager = FindObjectOfType<DisplayLayoutManager>();
        }

        if (raycastPointer == null)
        {
            raycastPointer = FindObjectOfType<RaycastPointer>();
        }

        if (focusManager == null)
        {
            focusManager = FindObjectOfType<FocusManager>();
        }

        if (virtualCursorController == null)
        {
            virtualCursorController = FindObjectOfType<VirtualCursorController>();
        }

        if (scrollController == null)
        {
            scrollController = FindObjectOfType<ScrollController>();
        }

        if (gazeDisplayFocusManager == null)
        {
            gazeDisplayFocusManager = FindObjectOfType<GazeDisplayFocusManager>();
        }

        if (debugInputProvider == null)
        {
            debugInputProvider = FindObjectOfType<EditorDebugInputProvider>();
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
