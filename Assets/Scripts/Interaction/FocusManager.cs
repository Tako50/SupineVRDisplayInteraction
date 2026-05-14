using UnityEngine;

[DisallowMultipleComponent]
public class FocusManager : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private VirtualCursorController virtualCursorController;

    private DisplayHit currentCandidate;
    private bool hasCandidate;

    public DisplaySurface CandidateDisplay => hasCandidate ? currentCandidate.Display : null;

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
            hasCandidate = false;
            return;
        }

        UpdateCandidate();

        if (hasCandidate && inputManager.GripPressed)
        {
            displayManager.SetFocusedDisplay(currentCandidate.Display);

            if (virtualCursorController != null)
            {
                virtualCursorController.WarpTo(currentCandidate.Display, currentCandidate.Normalized);
            }

            Debug.Log($"[FocusManager] condition={inputManager.CurrentCondition}, focusedDisplay={currentCandidate.DisplayId}, normalized={Format(currentCandidate.Normalized)}");
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
    }

    private void UpdateCandidate()
    {
        DisplayHit[] candidates = displayManager.GetDisplayHitsAll(gazeProvider.GetRay());
        hasCandidate = candidates.Length > 0;
        if (!hasCandidate)
        {
            displayManager.SetOnlyCursorsVisible(displayManager.FocusedDisplay, null);
            return;
        }

        currentCandidate = candidates[0];

        if (displayManager.FocusedDisplay != currentCandidate.Display)
        {
            displayManager.SetCursorNormalized(currentCandidate.Display, currentCandidate.Normalized, true);
        }

        displayManager.SetOnlyCursorsVisible(displayManager.FocusedDisplay, currentCandidate.Display);
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
