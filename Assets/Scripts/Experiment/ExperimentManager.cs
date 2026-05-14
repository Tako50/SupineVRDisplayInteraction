using UnityEngine;

[DisallowMultipleComponent]
public class ExperimentManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private DisplayLayoutManager layoutManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private FocusManager focusManager;
    [SerializeField] private EditorDebugInputProvider debugInputProvider;
    [SerializeField] private Logger logger;

    [Header("Experiment State")]
    [SerializeField] private InteractionCondition startingCondition = InteractionCondition.RaycastBaseline;
    [SerializeField] private DisplayLayoutPreset startingLayout = DisplayLayoutPreset.StrongOcclusion;
    [SerializeField] private bool lockDisplaysAfterStart = true;
    [SerializeField] private float debugLogIntervalSeconds = 0.5f;

    private DisplayLayoutPreset currentLayout;
    private float nextDebugLogTime;

    public InteractionCondition CurrentCondition => inputManager != null ? inputManager.CurrentCondition : startingCondition;
    public DisplayLayoutPreset CurrentLayout => currentLayout;

    private void Awake()
    {
        ResolveReferences();
        currentLayout = startingLayout;

        if (inputManager != null)
        {
            inputManager.SetCondition(startingCondition);
        }
    }

    private void Start()
    {
        ApplyLayout(startingLayout);
    }

    private void Update()
    {
        ResolveReferences();
        HandleKeyboardShortcuts();
        SampleDebugState();
    }

    public void ApplyLayout(DisplayLayoutPreset preset)
    {
        currentLayout = preset;
        if (layoutManager != null)
        {
            layoutManager.ApplyLayout(preset);
            layoutManager.enabled = !lockDisplaysAfterStart;
        }

        Debug.Log($"[ExperimentManager] layout={preset}");
    }

    private void HandleKeyboardShortcuts()
    {
        if (debugInputProvider == null || !debugInputProvider.IsEnabled)
        {
            return;
        }

        if (debugInputProvider.NoOcclusionPressed)
        {
            ApplyLayout(DisplayLayoutPreset.NoOcclusion);
        }
        else if (debugInputProvider.PartialOcclusionPressed)
        {
            ApplyLayout(DisplayLayoutPreset.PartialOcclusion);
        }
        else if (debugInputProvider.StrongOcclusionPressed)
        {
            ApplyLayout(DisplayLayoutPreset.StrongOcclusion);
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
