using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// Aボタン/トリガー短押しを、現在の条件に応じたクリックイベントへ変換する。
/// BaselineではRayヒット位置、ExplicitDisplayFocusではフォーカス表示上の仮想カーソル位置でクリックする。
/// </summary>
public class ClickDispatcher : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private FocusManager focusManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private FocusPointingTaskManager focusPointingTaskManager;
    [SerializeField] private ReferenceListTaskManager referenceListTaskManager;
    [SerializeField] private VRTaskMenuManager vrTaskMenuManager;
    [SerializeField] private Logger logger;
    [SerializeField] private float triggerScrollDeadzone = 0.05f;

    public string LastClickResult { get; private set; } = "None";

    private bool triggerGestureScrolled;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (inputManager == null || displayManager == null)
        {
            return;
        }

        UpdateTriggerClickState();
        bool submitRequested = ShouldDispatchClickThisFrame();
        if (!submitRequested)
        {
            return;
        }

        if (vrTaskMenuManager != null && TryGetWorldMenuRay(out Ray menuRay) && vrTaskMenuManager.TryHandleWorldClick(menuRay))
        {
            LastClickResult = "WorldMenu menu=True";
            return;
        }

        if (TryHandleDisplayMenuClick())
        {
            return;
        }

        if (raycastPointer != null
            && displayManager.TryGetForemostHit(raycastPointer.CurrentRay, out DisplayHit taskControlHit)
            && TryHandleTaskControlClick(taskControlHit.DisplayId, taskControlHit.Normalized))
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

    private void UpdateTriggerClickState()
    {
        if (inputManager.TriggerPressed)
        {
            triggerGestureScrolled = false;
        }

        if (inputManager.TriggerHeld && Mathf.Abs(inputManager.Stick.y) >= triggerScrollDeadzone)
        {
            triggerGestureScrolled = true;
        }
    }

    private bool ShouldDispatchClickThisFrame()
    {
        // トリガーをスクロールに使った場合は、離した瞬間のクリックとして扱わない。
        bool triggerClick = inputManager.TriggerReleased && !triggerGestureScrolled;
        if (inputManager.TriggerReleased)
        {
            triggerGestureScrolled = false;
        }

        return inputManager.SubmitReleased || triggerClick;
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

        if (TryHandleTaskControlClick(hit.DisplayId, hit.Normalized))
        {
            LastClickResult = $"{hit.DisplayId} taskControl=True";
            return;
        }

        LastClickResult = hit.DisplayId;
        NotifyTaskLayer(hit.DisplayId, hit.Normalized, hit.Display != null);
        Ray ray = raycastPointer != null ? raycastPointer.CurrentRay : default;
        LogClick(hit.DisplayId, hit.Normalized, ray);
    }

    private void DispatchExplicitFocusClick()
    {
        // 明示フォーカス条件では、視線位置ではなくロック済み表示の仮想カーソル位置をクリックする。
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

        if (TryHandleTaskControlClick(focusedDisplay.name, normalized))
        {
            LastClickResult = $"{focusedDisplay.name} taskControl=True";
            return;
        }

        LastClickResult = focusedDisplay.name;
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
                gazeOnDifferentDisplay,
                gazeRay.origin,
                gazeRay.direction);
        }
        else
        {
            Debug.Log($"[ClickDispatcher] condition={inputManager.CurrentCondition}, focusedDisplay={focusedDisplay.name}, normalized={Format(normalized)}, gazeOnDifferentDisplay={gazeOnDifferentDisplay}");
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

        if (referenceListTaskManager == null)
        {
            referenceListTaskManager = FindObjectOfType<ReferenceListTaskManager>();
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
        if (inputManager == null)
        {
            return;
        }

        if (focusPointingTaskManager != null)
        {
            focusPointingTaskManager.HandleClick(new FocusPointingClickEvent
            {
                Condition = inputManager.CurrentCondition,
                ClickedDisplayId = clickedDisplayId,
                ClickedNormalizedPosition = normalized,
                Timestamp = Time.time,
                HasValidDisplay = hasValidDisplay
            });
        }

        referenceListTaskManager?.HandleClick(
            clickedDisplayId,
            normalized,
            inputManager.CurrentCondition,
            Time.time);
    }

    private bool TryHandleTaskControlClick(string displayId, Vector2 normalizedPosition)
    {
        return (focusPointingTaskManager != null
                && focusPointingTaskManager.TryHandleTaskControlClick(displayId, normalizedPosition))
            || (referenceListTaskManager != null
                && referenceListTaskManager.TryHandleTaskControlClick(displayId, normalizedPosition));
    }

    private void LogClick(string displayId, Vector2 normalized, Ray ray)
    {
        if (logger != null)
        {
            logger.LogRaycastClick(inputManager.CurrentCondition, displayId, normalized, ray.origin, ray.direction);
        }
        else
        {
            Debug.Log($"[ClickDispatcher] condition={inputManager.CurrentCondition}, displayId={displayId}, normalized={Format(normalized)}");
        }
    }

    private bool TryGetWorldMenuRay(out Ray ray)
    {
        // ディスプレイ外のEnd Training等は、視線条件では視線Ray、BaselineではコントローラRayで押す。
        if (inputManager != null
            && inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus
            && gazeProvider != null)
        {
            ray = gazeProvider.GetGazeRay();
            return ray.direction != Vector3.zero;
        }

        if (raycastPointer != null)
        {
            ray = raycastPointer.CurrentRay;
            return ray.direction != Vector3.zero;
        }

        ray = default;
        return false;
    }

    private bool TryHandleDisplayMenuClick()
    {
        if (vrTaskMenuManager == null || raycastPointer == null || displayManager == null)
        {
            return false;
        }

        if (!displayManager.TryGetForemostHit(raycastPointer.CurrentRay, out DisplayHit menuHit))
        {
            return false;
        }

        if (!vrTaskMenuManager.TryHandleClick(menuHit.DisplayId, menuHit.Normalized))
        {
            return false;
        }

        LastClickResult = $"{menuHit.DisplayId} menu=True";
        return true;
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
