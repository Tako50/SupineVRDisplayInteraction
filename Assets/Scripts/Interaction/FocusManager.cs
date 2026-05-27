using UnityEngine;

public enum FocusState
{
    NoCandidate,
    GazeOnSingleDisplay,
    GazeOnMultipleDisplays,
    FocusPreview,
    FocusedLocked
}

[DisallowMultipleComponent]
/// <summary>
/// ExplicitDisplayFocusの中心ロジック。
/// 視線で候補ディスプレイを集め、グリップ入力があった時だけ候補をフォーカスとして確定する。
/// </summary>
public class FocusManager : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private Logger logger;

    private DisplayHit[] currentCandidates = new DisplayHit[0];
    private string lastCandidateIds = "None";

    public FocusState CurrentState { get; private set; } = FocusState.NoCandidate;
    public DisplaySurface CandidateDisplay => currentCandidates.Length > 0 ? currentCandidates[0].Display : null;
    public DisplayHit PrimaryCandidate => currentCandidates.Length > 0 ? currentCandidates[0] : default;
    public DisplayHit[] CurrentCandidates => currentCandidates;
    public string CurrentCandidateIds => DisplayManager.FormatDisplayIds(currentCandidates);
    public bool HasCandidate => currentCandidates.Length > 0;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (inputManager == null || gazeProvider == null || displayManager == null)
        {
            return;
        }

        if (inputManager.CurrentCondition != InteractionCondition.ExplicitDisplayFocus)
        {
            currentCandidates = new DisplayHit[0];
            CurrentState = FocusState.NoCandidate;
            displayManager.ResetDisplayVisuals();
            return;
        }

        if (inputManager.ResetFocusPressed)
        {
            ClearFocus();
        }

        UpdateCandidates();

        if (inputManager.GripHeld)
        {
            if (inputManager.GripPressed)
            {
                Debug.Log($"[FocusManager] grip received candidates={CurrentCandidateIds}");
            }

            UpdateFocusFromCurrentGaze(inputManager.GripPressed);
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

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }

        if (virtualCursorController == null)
        {
            virtualCursorController = FindObjectOfType<VirtualCursorController>();
        }

        if (logger == null)
        {
            logger = FindObjectOfType<Logger>();
        }
    }

    private void UpdateCandidates()
    {
        // 視線Rayでは重なり候補を全部拾う。フォーカス済みなら候補が変わっても入力対象は変えない。
        Ray gazeRay = gazeProvider.GetGazeRay();
        currentCandidates = displayManager.GetDisplayHitsAll(gazeRay);

        if (currentCandidates.Length == 0)
        {
            CurrentState = displayManager.FocusedDisplay != null ? FocusState.FocusedLocked : FocusState.NoCandidate;
        }
        else if (displayManager.FocusedDisplay != null)
        {
            CurrentState = FocusState.FocusedLocked;
        }
        else
        {
            CurrentState = currentCandidates.Length == 1
                ? FocusState.GazeOnSingleDisplay
                : FocusState.GazeOnMultipleDisplays;
        }

        displayManager.ApplyFocusVisuals(currentCandidates, displayManager.FocusedDisplay == null);

        if (displayManager.FocusedDisplay != null)
        {
            displayManager.SetOnlyCursorsVisible(displayManager.FocusedDisplay, null);
        }
        else
        {
            displayManager.HideAllCursors();
        }

        LogCandidateChange(gazeRay);
    }

    private void UpdateFocusFromCurrentGaze(bool logFocusEvent)
    {
        // Phase 3 MVPでは複数候補のうち最も近い候補を選ぶ。将来ここを候補選択UIへ差し替える。
        if (currentCandidates.Length == 0)
        {
            return;
        }

        DisplayHit selected = SelectCandidateForFocus(currentCandidates);
        displayManager.SetFocusedDisplay(selected.Display);
        displayManager.SetOnlyCursorsVisible(selected.Display, null);
        displayManager.ApplyFocusVisuals(null);
        CurrentState = FocusState.FocusedLocked;

        if (virtualCursorController != null)
        {
            virtualCursorController.WarpTo(selected.Display, selected.Normalized);
        }

        Ray gazeRay = gazeProvider.GetGazeRay();
        bool gazeOnDifferentDisplay = IsGazeOnDifferentDisplay(selected.Display);
        if (!logFocusEvent)
        {
            return;
        }

        if (logger != null)
        {
            logger.LogExplicitFocus(
                inputManager.CurrentCondition,
                gazeProvider.CurrentGazeSource,
                CurrentCandidateIds,
                selected.DisplayId,
                selected.Normalized,
                gazeOnDifferentDisplay,
                gazeRay.origin,
                gazeRay.direction);
            logger.LogExplicitCursorWarp(
                inputManager.CurrentCondition,
                gazeProvider.CurrentGazeSource,
                CurrentCandidateIds,
                selected.DisplayId,
                selected.Normalized,
                gazeOnDifferentDisplay,
                gazeRay.origin,
                gazeRay.direction);
        }
        else
        {
            Debug.Log($"[FocusManager] condition={inputManager.CurrentCondition}, focusedDisplay={selected.DisplayId}, normalized={Format(selected.Normalized)}, candidates={CurrentCandidateIds}");
        }
    }

    public void ClearFocus()
    {
        if (displayManager == null)
        {
            return;
        }

        displayManager.SetFocusedDisplay(null);
        displayManager.HideAllCursors();
        displayManager.ApplyFocusVisuals(currentCandidates);
        CurrentState = currentCandidates.Length == 0
            ? FocusState.NoCandidate
            : currentCandidates.Length == 1
                ? FocusState.GazeOnSingleDisplay
                : FocusState.GazeOnMultipleDisplays;

        Debug.Log("[FocusManager] focus cleared");
    }

    public void ForceRefocusFromCurrentGazeCandidate()
    {
        UpdateFocusFromCurrentGaze(true);
    }

    public string GetFocusedDisplayId()
    {
        DisplaySurface focused = displayManager != null ? displayManager.FocusedDisplay : null;
        return focused != null ? focused.name : "None";
    }

    public string GetCurrentCandidateIds()
    {
        return CurrentCandidateIds;
    }

    public FocusState GetFocusState()
    {
        return CurrentState;
    }

    private static DisplayHit SelectCandidateForFocus(DisplayHit[] candidates)
    {
        return candidates[0];
    }

    public bool IsGazeOnDifferentDisplay(DisplaySurface focusedDisplay)
    {
        if (focusedDisplay == null || currentCandidates.Length == 0)
        {
            return false;
        }

        return currentCandidates[0].Display != null && currentCandidates[0].Display != focusedDisplay;
    }

    private void LogCandidateChange(Ray gazeRay)
    {
        string candidateIds = CurrentCandidateIds;
        if (candidateIds == lastCandidateIds)
        {
            return;
        }

        lastCandidateIds = candidateIds;
        if (logger != null)
        {
            logger.LogExplicitCandidateUpdate(
                inputManager.CurrentCondition,
                gazeProvider.CurrentGazeSource,
                CurrentState,
                candidateIds,
                displayManager.FocusedDisplay != null ? displayManager.FocusedDisplay.name : "None",
                displayManager.FocusedDisplay != null && IsGazeOnDifferentDisplay(displayManager.FocusedDisplay),
                gazeRay.origin,
                gazeRay.direction);
        }
        else
        {
            Debug.Log($"[FocusManager] state={CurrentState}, candidates={candidateIds}");
        }
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
