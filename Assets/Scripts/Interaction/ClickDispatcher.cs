using UnityEngine;

[DisallowMultipleComponent]
public class ClickDispatcher : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private FocusManager focusManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private Logger logger;

    public string LastClickResult { get; private set; } = "None";

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (inputManager == null || displayManager == null || !inputManager.SubmitPressed)
        {
            return;
        }

        if (inputManager.CurrentCondition == InteractionCondition.RaycastBaseline)
        {
            DispatchRaycastBaselineClick();
            return;
        }

        if (inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus)
        {
            DispatchExplicitFocusClick();
        }
    }

    private void DispatchRaycastBaselineClick()
    {
        if (!displayManager.HasCurrentRaycastHit)
        {
            return;
        }

        DisplayHit hit = displayManager.CurrentRaycastHit;
        string targetId = string.Empty;
        bool validTarget = hit.Display != null && hit.Display.TryClickDebugTarget(hit.Normalized, out targetId);
        LastClickResult = $"{hit.DisplayId} valid={validTarget} target={targetId}";
        Ray ray = raycastPointer != null ? raycastPointer.CurrentRay : default;
        LogClick(hit.DisplayId, hit.Normalized, validTarget, targetId, ray);
    }

    private void DispatchExplicitFocusClick()
    {
        DisplaySurface focusedDisplay = displayManager.FocusedDisplay;
        if (focusedDisplay == null || virtualCursorController == null)
        {
            return;
        }

        Vector2 normalized = virtualCursorController.NormalizedPosition;
        string targetId = string.Empty;
        bool validTarget = focusedDisplay.TryClickDebugTarget(normalized, out targetId);
        LastClickResult = $"{focusedDisplay.name} valid={validTarget} target={targetId}";
        Ray gazeRay = gazeProvider != null ? gazeProvider.GetGazeRay() : default;
        bool gazeOnDifferentDisplay = focusManager != null && focusManager.IsGazeOnDifferentDisplay(focusedDisplay);

        if (logger != null)
        {
            logger.LogExplicitClick(
                inputManager.CurrentCondition,
                gazeProvider != null ? gazeProvider.CurrentGazeSource : GazeSource.HmdForward,
                focusManager != null ? focusManager.CurrentCandidateIds : "None",
                focusedDisplay.name,
                normalized,
                validTarget,
                targetId,
                gazeOnDifferentDisplay,
                gazeRay.origin,
                gazeRay.direction);
        }
        else
        {
            Debug.Log($"[ClickDispatcher] condition={inputManager.CurrentCondition}, focusedDisplay={focusedDisplay.name}, normalized={Format(normalized)}, validTarget={validTarget}, targetId={targetId}, gazeOnDifferentDisplay={gazeOnDifferentDisplay}");
        }
    }

    private void ResolveReferences()
    {
        if (inputManager == null)
        {
            inputManager = PrototypeInputManager.Instance != null
                ? PrototypeInputManager.Instance
                : FindObjectOfType<PrototypeInputManager>();
        }

        if (displayManager == null)
        {
            displayManager = DisplayManager.Instance != null
                ? DisplayManager.Instance
                : FindObjectOfType<DisplayManager>();
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

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }

        if (logger == null)
        {
            logger = FindObjectOfType<Logger>();
        }
    }

    private void LogClick(string displayId, Vector2 normalized, bool validTarget, string targetId, Ray ray)
    {
        if (logger != null)
        {
            logger.LogRaycastClick(inputManager.CurrentCondition, displayId, normalized, validTarget, targetId, ray.origin, ray.direction);
        }
        else
        {
            Debug.Log($"[ClickDispatcher] condition={inputManager.CurrentCondition}, displayId={displayId}, normalized={Format(normalized)}, validTarget={validTarget}, targetId={targetId}");
        }
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
