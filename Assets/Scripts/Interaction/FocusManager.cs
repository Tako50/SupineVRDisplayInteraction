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
    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private Logger logger;

    [Header("Off-Display Gaze Assistance")]
    [Tooltip("Maximum angular distance outside a display edge that can snap to the nearest display. A valid gaze farther away does not update focus or cursor.")]
    [Min(0f)]
    [SerializeField] private float offDisplaySnapMaxAngleDegrees = 3f;
    [Tooltip("During off-display snapping, keep the current focused/candidate display until another display is closer by this angular amount.")]
    [Min(0f)]
    [SerializeField] private float offDisplaySwitchHysteresisDegrees = 0.5f;

    private DisplayHit[] currentCandidates = new DisplayHit[0];
    private DisplaySurface currentCandidateDisplay;
    private Vector2 currentCandidateNormalized = new Vector2(0.5f, 0.5f);
    private bool currentCandidateUsesOffDisplaySnap;
    private float currentCandidateAngularDistanceDegrees;
    private Ray currentValidGazeRay;
    private bool hasValidGazeThisFrame;
    private bool hasObservedGazeValidity;
    private bool previousGazeValid;
    private bool hasLoggedOffDisplaySnapState;
    private bool lastLoggedOffDisplaySnapState;
    private string lastCandidateSignature = "None|Direct";

    public FocusState CurrentState { get; private set; } = FocusState.NoCandidate;
    public DisplaySurface CandidateDisplay => currentCandidateDisplay;
    public DisplayHit PrimaryCandidate => currentCandidates.Length > 0 ? currentCandidates[0] : default;
    public DisplayHit[] CurrentCandidates => currentCandidates;
    public string CurrentCandidateIds => currentCandidates.Length > 0
        ? DisplayManager.FormatDisplayIds(currentCandidates)
        : currentCandidateDisplay != null ? currentCandidateDisplay.name : "None";
    public bool HasCandidate => currentCandidateDisplay != null;
    public bool HasValidGazeThisFrame => hasValidGazeThisFrame;
    public bool CurrentCandidateUsesOffDisplaySnap => currentCandidateUsesOffDisplaySnap;
    public float CurrentCandidateAngularDistanceDegrees => currentCandidateAngularDistanceDegrees;
    public float OffDisplaySnapMaxAngleDegrees => offDisplaySnapMaxAngleDegrees;
    public float OffDisplaySwitchHysteresisDegrees => offDisplaySwitchHysteresisDegrees;

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
            ResetCandidateState();
            displayManager.ResetDisplayVisuals();
            return;
        }

        if (inputManager.ResetFocusPressed)
        {
            ClearFocus();
        }

        hasValidGazeThisFrame = gazeProvider.TryGetValidGazeRay(out Ray gazeRay);
        LogGazeValidityChange(hasValidGazeThisFrame);
        if (!hasValidGazeThisFrame)
        {
            // Eye Trackingの瞬断をHMD forwardへ置き換えない。
            // 最後の有効なfocus、candidate、cursorをそのまま保持する。
            return;
        }

        currentValidGazeRay = gazeRay;
        UpdateCandidates(gazeRay);

        if (inputManager.GripHeld)
        {
            if (inputManager.GripPressed)
            {
                Debug.Log($"[FocusManager] grip received candidates={CurrentCandidateIds}");
            }

            UpdateFocusFromCurrentGaze(gazeRay, inputManager.GripPressed);
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

    private void UpdateCandidates(Ray gazeRay)
    {
        // 視線Rayでは重なり候補を全部拾う。フォーカス済みなら候補が変わっても入力対象は変えない。
        DisplaySurface preferredDisplay = displayManager.FocusedDisplay != null
            ? displayManager.FocusedDisplay
            : currentCandidateDisplay;
        currentCandidates = displayManager.GetDisplayHitsAll(gazeRay);

        if (currentCandidates.Length > 0)
        {
            currentCandidateDisplay = currentCandidates[0].Display;
            currentCandidateNormalized = currentCandidates[0].Normalized;
            currentCandidateUsesOffDisplaySnap = false;
            currentCandidateAngularDistanceDegrees = 0f;
        }
        else if (displayManager.TryGetNearestDisplayCandidate(
            gazeRay,
            preferredDisplay,
            offDisplaySnapMaxAngleDegrees,
            offDisplaySwitchHysteresisDegrees,
            out DisplaySurface nearestDisplay,
            out Vector2 nearestNormalized,
            out float angularDistanceDegrees))
        {
            currentCandidateDisplay = nearestDisplay;
            currentCandidateNormalized = nearestNormalized;
            currentCandidateUsesOffDisplaySnap = true;
            currentCandidateAngularDistanceDegrees = angularDistanceDegrees;
        }
        else
        {
            currentCandidateDisplay = null;
            currentCandidateNormalized = new Vector2(0.5f, 0.5f);
            currentCandidateUsesOffDisplaySnap = false;
            currentCandidateAngularDistanceDegrees = 0f;
        }

        if (!HasCandidate)
        {
            CurrentState = displayManager.FocusedDisplay != null ? FocusState.FocusedLocked : FocusState.NoCandidate;
        }
        else if (displayManager.FocusedDisplay != null)
        {
            CurrentState = FocusState.FocusedLocked;
        }
        else
        {
            CurrentState = currentCandidates.Length > 1
                ? FocusState.GazeOnMultipleDisplays
                : FocusState.GazeOnSingleDisplay;
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

    private void UpdateFocusFromCurrentGaze(Ray gazeRay, bool logFocusEvent)
    {
        if (!HasCandidate)
        {
            return;
        }

        DisplaySurface selectedDisplay = currentCandidateDisplay;
        Vector2 selectedNormalized = currentCandidateNormalized;
        displayManager.SetFocusedDisplay(selectedDisplay);
        displayManager.SetOnlyCursorsVisible(selectedDisplay, null);
        displayManager.ApplyFocusVisuals(null);
        CurrentState = FocusState.FocusedLocked;

        if (virtualCursorController != null)
        {
            virtualCursorController.WarpTo(selectedDisplay, selectedNormalized);
        }

        bool gazeOnDifferentDisplay = IsGazeOnDifferentDisplay(selectedDisplay);
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
                selectedDisplay.name,
                selectedNormalized,
                gazeOnDifferentDisplay,
                gazeRay.origin,
                gazeRay.direction);
            logger.LogExplicitCursorWarp(
                inputManager.CurrentCondition,
                gazeProvider.CurrentGazeSource,
                CurrentCandidateIds,
                selectedDisplay.name,
                selectedNormalized,
                gazeOnDifferentDisplay,
                gazeRay.origin,
                gazeRay.direction);
        }
        else
        {
            Debug.Log(
                $"[FocusManager] condition={inputManager.CurrentCondition}, focusedDisplay={selectedDisplay.name}, normalized={Format(selectedNormalized)}, candidates={CurrentCandidateIds}, acquisition={GetCandidateAcquisitionLabel()}");
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
        CurrentState = !HasCandidate
            ? FocusState.NoCandidate
            : currentCandidates.Length > 1
                ? FocusState.GazeOnMultipleDisplays
                : FocusState.GazeOnSingleDisplay;

        Debug.Log("[FocusManager] focus cleared");
    }

    public void ForceRefocusFromCurrentGazeCandidate()
    {
        if (hasValidGazeThisFrame)
        {
            UpdateFocusFromCurrentGaze(currentValidGazeRay, true);
        }
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

    public bool IsGazeOnDifferentDisplay(DisplaySurface focusedDisplay)
    {
        if (focusedDisplay == null || !HasCandidate)
        {
            return false;
        }

        return currentCandidateDisplay != focusedDisplay;
    }

    private void LogCandidateChange(Ray gazeRay)
    {
        string candidateIds = CurrentCandidateIds;
        string acquisitionMode = currentCandidateUsesOffDisplaySnap ? "OffDisplaySnap" : "Direct";
        string candidateSignature = $"{candidateIds}|{acquisitionMode}";
        if (candidateSignature == lastCandidateSignature)
        {
            return;
        }

        lastCandidateSignature = candidateSignature;
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
            Debug.Log(
                $"[FocusManager] state={CurrentState}, candidates={candidateIds}, acquisition={GetCandidateAcquisitionLabel()}");
        }

        if (!hasLoggedOffDisplaySnapState
            || lastLoggedOffDisplaySnapState != currentCandidateUsesOffDisplaySnap)
        {
            bool shouldLogTransition = hasLoggedOffDisplaySnapState || currentCandidateUsesOffDisplaySnap;
            hasLoggedOffDisplaySnapState = true;
            lastLoggedOffDisplaySnapState = currentCandidateUsesOffDisplaySnap;
            if (shouldLogTransition && logger != null)
            {
                logger.LogEvent(
                    currentCandidateUsesOffDisplaySnap
                        ? "OffDisplayGazeSnapStarted"
                        : "OffDisplayGazeSnapEnded",
                    inputManager.CurrentCondition,
                    currentCandidateDisplay != null ? currentCandidateDisplay.name : "None",
                    currentCandidateNormalized);
            }
        }
    }

    private void LogGazeValidityChange(bool gazeValid)
    {
        if (hasObservedGazeValidity && previousGazeValid == gazeValid)
        {
            return;
        }

        hasObservedGazeValidity = true;
        previousGazeValid = gazeValid;
        if (logger != null)
        {
            logger.LogEvent(
                gazeValid ? "ExplicitGazeValid" : "ExplicitGazeInvalidHold",
                inputManager.CurrentCondition,
                currentCandidateDisplay != null ? currentCandidateDisplay.name : "None",
                currentCandidateNormalized);
        }
    }

    private void ResetCandidateState()
    {
        currentCandidates = new DisplayHit[0];
        currentCandidateDisplay = null;
        currentCandidateNormalized = new Vector2(0.5f, 0.5f);
        currentCandidateUsesOffDisplaySnap = false;
        currentCandidateAngularDistanceDegrees = 0f;
        hasValidGazeThisFrame = false;
        hasObservedGazeValidity = false;
        hasLoggedOffDisplaySnapState = false;
        lastCandidateSignature = "None|Direct";
        CurrentState = FocusState.NoCandidate;
    }

    private string GetCandidateAcquisitionLabel()
    {
        return currentCandidateUsesOffDisplaySnap
            ? $"OffDisplaySnap {currentCandidateAngularDistanceDegrees:0.00}deg"
            : "Direct";
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
