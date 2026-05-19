using UnityEngine;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class PrototypeControlPanel : MonoBehaviour
{
    [Header("Use This Panel")]
    [SerializeField] private InteractionCondition condition = InteractionCondition.RaycastBaseline;
    [SerializeField] private DisplayLayoutPreset layout = DisplayLayoutPreset.StrongOcclusion;
    [SerializeField] private GazeSource gazeSource = GazeSource.EyeTracking;
    [SerializeField] private RayVisualizationMode rayMode = RayVisualizationMode.Hidden;

    [Header("Debug View")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private bool showWorldConditionLabel = true;
    [SerializeField] private bool showGazeRayOnlyWhileGripHeld = true;

    [Header("Explicit Focus")]
    [SerializeField] private bool allowStickCursorMovement = true;

    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private PrototypeDebugVisualizer debugVisualizer;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private FocusPointingTaskManager focusPointingTaskManager;

    private InteractionCondition appliedCondition;
    private DisplayLayoutPreset appliedLayout;
    private GazeSource appliedGazeSource;
    private RayVisualizationMode appliedRayMode;
    private bool appliedShowDebugOverlay;
    private bool appliedShowWorldConditionLabel;
    private bool appliedShowGazeRayOnlyWhileGripHeld;
    private bool appliedAllowStickCursorMovement;
    private bool hasApplied;

    private void Awake()
    {
        ResolveReferences();
        ApplyAll();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        ApplyIfChanged();
    }

    public void ApplyAll()
    {
        bool canChangeConditionAndLayout = inputManager == null || !inputManager.IsConditionLocked;

        if (focusPointingTaskManager != null)
        {
            focusPointingTaskManager.SetSelectedCondition(condition);
            focusPointingTaskManager.SetSelectedLayout(layout);
        }

        if (canChangeConditionAndLayout && inputManager != null)
        {
            inputManager.SetCondition(condition);
        }

        if (canChangeConditionAndLayout && experimentManager != null)
        {
            experimentManager.ApplyLayout(layout);
        }

        if (gazeProvider != null)
        {
            gazeProvider.SetGazeSource(gazeSource);
        }

        if (debugVisualizer != null)
        {
            debugVisualizer.SetRayVisualizationMode(rayMode);
            debugVisualizer.SetOverlayVisible(showDebugOverlay);
            debugVisualizer.SetWorldConditionLabelVisible(showWorldConditionLabel);
            debugVisualizer.SetGazeRayOnlyWhileGripHeld(showGazeRayOnlyWhileGripHeld);
        }

        if (virtualCursorController != null)
        {
            virtualCursorController.SetStickCursorMovementEnabled(allowStickCursorMovement);
        }

        CacheAppliedValues();
    }

    [ContextMenu("Task/Begin Training Task")]
    public void BeginTrainingTask()
    {
        ResolveReferences();
        ApplyAll();
        if (focusPointingTaskManager != null)
        {
            focusPointingTaskManager.BeginTrainingTask();
        }
    }

    [ContextMenu("Task/Begin Main Task")]
    public void BeginMainTask()
    {
        ResolveReferences();
        ApplyAll();
        if (focusPointingTaskManager != null)
        {
            focusPointingTaskManager.BeginMainTask();
        }
    }

    [ContextMenu("Task/Return To Condition Selection")]
    public void ReturnToConditionSelection()
    {
        ResolveReferences();
        if (focusPointingTaskManager != null)
        {
            focusPointingTaskManager.ReturnToConditionSelection();
        }
    }

    private void ApplyIfChanged()
    {
        if (!hasApplied
            || appliedCondition != condition
            || appliedLayout != layout
            || appliedGazeSource != gazeSource
            || appliedRayMode != rayMode
            || appliedShowDebugOverlay != showDebugOverlay
            || appliedShowWorldConditionLabel != showWorldConditionLabel
            || appliedShowGazeRayOnlyWhileGripHeld != showGazeRayOnlyWhileGripHeld
            || appliedAllowStickCursorMovement != allowStickCursorMovement)
        {
            ApplyAll();
        }
    }

    private void CacheAppliedValues()
    {
        appliedCondition = condition;
        appliedLayout = layout;
        appliedGazeSource = gazeSource;
        appliedRayMode = rayMode;
        appliedShowDebugOverlay = showDebugOverlay;
        appliedShowWorldConditionLabel = showWorldConditionLabel;
        appliedShowGazeRayOnlyWhileGripHeld = showGazeRayOnlyWhileGripHeld;
        appliedAllowStickCursorMovement = allowStickCursorMovement;
        hasApplied = true;
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

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }

        if (debugVisualizer == null)
        {
            debugVisualizer = FindObjectOfType<PrototypeDebugVisualizer>();
        }

        if (virtualCursorController == null)
        {
            virtualCursorController = FindObjectOfType<VirtualCursorController>();
        }

        if (focusPointingTaskManager == null)
        {
            focusPointingTaskManager = FindObjectOfType<FocusPointingTaskManager>();
        }
    }
}
