using UnityEngine;

[DisallowMultipleComponent]
public class ClickDispatcher : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private Logger logger;

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

        if (inputManager.CurrentCondition != InteractionCondition.RaycastBaseline)
        {
            return;
        }

        if (!displayManager.HasCurrentRaycastHit)
        {
            return;
        }

        DisplayHit hit = displayManager.CurrentRaycastHit;
        string targetId = string.Empty;
        bool validTarget = hit.Display != null && hit.Display.TryClickDebugTarget(hit.Normalized, out targetId);
        Ray ray = raycastPointer != null ? raycastPointer.CurrentRay : default;
        LogClick(hit.DisplayId, hit.Normalized, validTarget, targetId, ray);
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
