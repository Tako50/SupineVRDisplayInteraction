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
    [SerializeField] private EditorDebugInputProvider debugInputProvider;

    [Header("Highlight")]
    public bool highlightEnabled = true;
    public bool debugToggleWithKeyboard = true;
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
        currentInteractionMethod = inputManager != null
            ? inputManager.CurrentCondition
            : InteractionCondition.RaycastBaseline;
        appliedHighlightEnabled = highlightEnabled;
        initialized = true;

        if (!highlightEnabled)
        {
            ClearAllHighlights(false);
        }
    }

    private void Update()
    {
        ResolveReferences();
        currentInteractionMethod = inputManager != null
            ? inputManager.CurrentCondition
            : currentInteractionMethod;

        if (debugToggleWithKeyboard
            && debugInputProvider != null
            && debugInputProvider.IsEnabled
            && debugInputProvider.HighlightTogglePressed)
        {
            SetHighlightEnabled(!highlightEnabled);
        }

        // Inspectorでpublic fieldを直接変更した場合も、そのフレームで表示へ反映する。
        if (!initialized || highlightEnabled != appliedHighlightEnabled)
        {
            ApplyHighlightEnabledChange(highlightEnabled);
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
        if (highlightEnabled == enabled && initialized && appliedHighlightEnabled == enabled)
        {
            if (!enabled)
            {
                ClearAllHighlights(false);
            }

            return;
        }

        highlightEnabled = enabled;
        ApplyHighlightEnabledChange(enabled);
    }

    private void ApplyHighlightEnabledChange(bool enabled)
    {
        initialized = true;
        appliedHighlightEnabled = enabled;
        currentInteractionMethod = inputManager != null
            ? inputManager.CurrentCondition
            : currentInteractionMethod;

        if (enabled)
        {
            RefreshHighlightTarget();
        }
        else
        {
            ClearAllHighlights(true);
        }

        if (logStateChanges)
        {
            Debug.Log(
                $"[GazeDisplayFocusManager] highlightEnabled={enabled}, interactionMethod={currentInteractionMethod}");
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

        if (debugInputProvider == null)
        {
            debugInputProvider = FindObjectOfType<EditorDebugInputProvider>();
        }
    }
}
