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
    [SerializeField] private GazeDisplayFocusManager gazeDisplayFocusManager;
    [SerializeField] private EditorDebugInputProvider debugInputProvider;
    [SerializeField] private Logger logger;

    [Header("Experiment State")]
    [SerializeField] private InteractionCondition startingCondition = InteractionCondition.RaycastBaseline;
    [SerializeField] private bool startingHighlightEnabled = true;
    [SerializeField] private DisplayLayoutPreset startingLayout = DisplayLayoutPreset.UpDownDepth;
    [SerializeField] private bool applyStartingLayoutOnStart = true;
    [SerializeField] private bool allowLayoutSwitching = false;
    [SerializeField] private bool lockDisplaysAfterStart = true;
    [SerializeField] private float debugLogIntervalSeconds = 0.5f;

    private DisplayLayoutPreset currentLayout;
    private float nextDebugLogTime;

    public InteractionCondition CurrentCondition => inputManager != null ? inputManager.CurrentCondition : startingCondition;
    public bool HighlightEnabled => gazeDisplayFocusManager != null
        ? gazeDisplayFocusManager.HighlightEnabled
        : startingHighlightEnabled;
    public DisplayLayoutPreset CurrentLayout => currentLayout;

    private void Awake()
    {
        ResolveReferences();
        currentLayout = startingLayout;

        if (inputManager != null)
        {
            inputManager.SetCondition(startingCondition);
        }

        SetHighlightEnabled(startingHighlightEnabled);
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
        startingHighlightEnabled = enabled;
        if (gazeDisplayFocusManager != null)
        {
            gazeDisplayFocusManager.SetHighlightEnabled(enabled);
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

        DisplayHit gazeHit = default;
        bool hasGazeHit = false;

        bool hasRayHit = displayManager.HasCurrentRaycastHit;
        DisplayHit rayHit = hasRayHit ? displayManager.CurrentRaycastHit : default;
        Ray controllerRay = raycastPointer != null ? raycastPointer.CurrentRay : default;

        if (logger != null && logger.ShouldSample())
        {
            logger.LogFrame(inputManager.CurrentCondition, currentLayout, gazeProvider.CurrentGazeSource, rayHit, hasRayHit, gazeHit, hasGazeHit, displayManager.FocusedDisplay, controllerRay.origin, controllerRay.direction);
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
