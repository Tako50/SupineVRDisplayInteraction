using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// 操作手法とは独立して、現在のポインティング対象にあるディスプレイの表示ハイライトだけを管理する。
/// RaycastBaselineではController Ray、ExplicitDisplayFocusではgaze rayを使用する。
/// </summary>
public class GazeDisplayFocusManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private DisplayManager displayManager;
    [Header("Highlight")]
    [SerializeField, HideInInspector] private bool highlightEnabled = true;
    [SerializeField] private bool logStateChanges = true;

    [Header("Runtime State (Read Only)")]
    [Tooltip("現在、ポインティングハイライトを表示している対象。入力フォーカスとは独立した状態。")]
    [SerializeField] private DisplaySurface currentFocusedTarget;
    [SerializeField] private InteractionCondition currentInteractionMethod = InteractionCondition.RaycastBaseline;

    private bool appliedHighlightEnabled;
    private bool initialized;

    public DisplaySurface CurrentFocusedTarget => currentFocusedTarget;
    public InteractionCondition CurrentInteractionMethod => currentInteractionMethod;
    public bool HighlightEnabled => highlightEnabled;

    private void Awake()
    {
        ResolveReferences();
        highlightEnabled = true;
        currentInteractionMethod = inputManager != null
            ? inputManager.CurrentCondition
            : InteractionCondition.RaycastBaseline;
        appliedHighlightEnabled = highlightEnabled;
        initialized = true;

        RefreshHighlightTarget();
    }

    private void Update()
    {
        ResolveReferences();
        currentInteractionMethod = inputManager != null
            ? inputManager.CurrentCondition
            : currentInteractionMethod;

        // Experiment highlight is fixed ON for both methods.
        if (!initialized || !highlightEnabled || !appliedHighlightEnabled)
        {
            SetHighlightEnabled(true);
        }
    }

    private void LateUpdate()
    {
        currentInteractionMethod = inputManager != null
            ? inputManager.CurrentCondition
            : currentInteractionMethod;

        if (highlightEnabled)
        {
            RefreshHighlightTarget();
        }
    }

    private void OnDisable()
    {
        ClearAllHighlights(false);
    }

    public void SetHighlightEnabled(bool enabled)
    {
        if (highlightEnabled && initialized && appliedHighlightEnabled)
        {
            return;
        }

        highlightEnabled = true;
        ApplyHighlightEnabledChange();
    }

    private void ApplyHighlightEnabledChange()
    {
        initialized = true;
        appliedHighlightEnabled = true;
        currentInteractionMethod = inputManager != null
            ? inputManager.CurrentCondition
            : currentInteractionMethod;

        RefreshHighlightTarget();

        if (logStateChanges)
        {
            Debug.Log(
                $"[GazeDisplayFocusManager] highlightEnabled=True, interactionMethod={currentInteractionMethod}");
        }
    }

    private void RefreshHighlightTarget()
    {
        if (displayManager == null)
        {
            SetHighlightedTarget(null, true);
            return;
        }

        DisplaySurface target;
        if (currentInteractionMethod == InteractionCondition.RaycastBaseline)
        {
            // Baselineで実際に入力を受ける、Controller Rayの最初のヒットと同じ対象を使う。
            target = displayManager.HasCurrentRaycastHit
                ? displayManager.CurrentRaycastHit.Display
                : null;
        }
        else if (gazeProvider != null)
        {
            Ray gazeRay = gazeProvider.GetGazeRay();
            target = displayManager.TryGetForemostHit(gazeRay, out DisplayHit hit)
                ? hit.Display
                : null;
        }
        else
        {
            target = null;
        }

        SetHighlightedTarget(target, true);
    }

    private void SetHighlightedTarget(DisplaySurface target, bool logChange)
    {
        if (currentFocusedTarget == target)
        {
            return;
        }

        DisplaySurface previous = currentFocusedTarget;
        if (previous != null)
        {
            previous.SetFocused(false);
        }

        currentFocusedTarget = highlightEnabled ? target : null;
        if (currentFocusedTarget != null)
        {
            currentFocusedTarget.SetFocused(true);
        }

        if (logChange && logStateChanges)
        {
            string previousName = previous != null ? previous.name : "None";
            string currentName = currentFocusedTarget != null ? currentFocusedTarget.name : "None";
            Debug.Log(
                $"[GazeDisplayFocusManager] target={previousName}->{currentName}, interactionMethod={currentInteractionMethod}");
        }
    }

    private void ClearAllHighlights(bool logTargetChange)
    {
        DisplaySurface previous = currentFocusedTarget;
        DisplaySurface[] displays = FindObjectsOfType<DisplaySurface>(true);
        for (int i = 0; i < displays.Length; i++)
        {
            if (displays[i] != null)
            {
                displays[i].SetFocused(false);
            }
        }

        currentFocusedTarget = null;
        if (logTargetChange && previous != null && logStateChanges)
        {
            Debug.Log(
                $"[GazeDisplayFocusManager] target={previous.name}->None, interactionMethod={currentInteractionMethod}");
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

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }

        if (displayManager == null)
        {
            displayManager = DisplayManager.Instance != null
                ? DisplayManager.Instance
                : FindObjectOfType<DisplayManager>();
        }

    }
}
