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
    [SerializeField] private FocusPointingTaskManager focusPointingTaskManager;
    [SerializeField] private VRTaskMenuManager vrTaskMenuManager;
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

        if (vrTaskMenuManager != null && raycastPointer != null && vrTaskMenuManager.TryHandleWorldClick(raycastPointer.CurrentRay))
        {
            LastClickResult = "WorldMenu menu=True";
            return;
        }

        if (focusPointingTaskManager != null
            && raycastPointer != null
            && displayManager.TryGetForemostHit(raycastPointer.CurrentRay, out DisplayHit taskControlHit)
            && focusPointingTaskManager.TryHandleTaskControlClick(taskControlHit.DisplayId, taskControlHit.Normalized))
        {
            LastClickResult = $"{taskControlHit.DisplayId} taskControl=True";
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
            LastClickResult = "None valid=False target=None";
            NotifyTaskLayer("None", Vector2.zero, false);
            return;
        }

        DisplayHit hit = displayManager.CurrentRaycastHit;
        if (vrTaskMenuManager != null && vrTaskMenuManager.TryHandleClick(hit.DisplayId, hit.Normalized))
        {
            LastClickResult = $"{hit.DisplayId} menu=True";
            return;
        }

        if (focusPointingTaskManager != null && focusPointingTaskManager.TryHandleTaskControlClick(hit.DisplayId, hit.Normalized))
        {
            LastClickResult = $"{hit.DisplayId} taskControl=True";
            return;
        }

        string targetId = string.Empty;
        bool validTarget = hit.Display != null && hit.Display.TryClickDebugTarget(hit.Normalized, out targetId);
        LastClickResult = $"{hit.DisplayId} valid={validTarget} target={targetId}";
        NotifyTaskLayer(hit.DisplayId, hit.Normalized, hit.Display != null);
        Ray ray = raycastPointer != null ? raycastPointer.CurrentRay : default;
        LogClick(hit.DisplayId, hit.Normalized, validTarget, targetId, ray);
    }

    private void DispatchExplicitFocusClick()
    {
        DisplaySurface focusedDisplay = displayManager.FocusedDisplay;
        if (focusedDisplay == null || virtualCursorController == null)
        {
            LastClickResult = "None valid=False target=None";
            NotifyTaskLayer("None", Vector2.zero, false);
            return;
        }

        Vector2 normalized = virtualCursorController.NormalizedPosition;
        if (vrTaskMenuManager != null && vrTaskMenuManager.TryHandleClick(focusedDisplay.name, normalized))
        {
            LastClickResult = $"{focusedDisplay.name} menu=True";
            return;
        }

        if (focusPointingTaskManager != null && focusPointingTaskManager.TryHandleTaskControlClick(focusedDisplay.name, normalized))
        {
            LastClickResult = $"{focusedDisplay.name} taskControl=True";
            return;
        }

        string targetId = string.Empty;
        bool validTarget = focusedDisplay.TryClickDebugTarget(normalized, out targetId);
        LastClickResult = $"{focusedDisplay.name} valid={validTarget} target={targetId}";
        NotifyTaskLayer(focusedDisplay.name, normalized, true);
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

        if (focusPointingTaskManager == null)
        {
            focusPointingTaskManager = FindObjectOfType<FocusPointingTaskManager>();
        }

        if (vrTaskMenuManager == null)
        {
            vrTaskMenuManager = FindObjectOfType<VRTaskMenuManager>();
        }

        if (logger == null)
        {
            logger = FindObjectOfType<Logger>();
        }
    }

    private void NotifyTaskLayer(string clickedDisplayId, Vector2 normalized, bool hasValidDisplay)
    {
        if (focusPointingTaskManager == null || inputManager == null)
        {
            return;
        }

        focusPointingTaskManager.HandleClick(new FocusPointingClickEvent
        {
            Condition = inputManager.CurrentCondition,
            ClickedDisplayId = clickedDisplayId,
            ClickedNormalizedPosition = normalized,
            Timestamp = Time.time,
            HasValidDisplay = hasValidDisplay
        });
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
