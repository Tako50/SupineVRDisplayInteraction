using UnityEngine;

[DisallowMultipleComponent]
public class PrototypeDebugVisualizer : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private Transform controllerRaySource;
    [SerializeField] private float rayLength = 4f;
    [SerializeField] private float hitPointRadius = 0.025f;
    [SerializeField] private bool showOverlay = true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
    }

    private void OnDrawGizmos()
    {
        ResolveReferences();

        if (displayManager == null)
        {
            return;
        }

        Ray controllerRay = GetControllerRay();
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(controllerRay.origin, controllerRay.origin + controllerRay.direction * rayLength);
        if (displayManager.TryGetForemostHit(controllerRay, out DisplayHit controllerHit))
        {
            Gizmos.DrawSphere(controllerHit.Hit.point, hitPointRadius);
        }

        if (gazeProvider != null)
        {
            Ray gazeRay = gazeProvider.GetRay();
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(gazeRay.origin, gazeRay.origin + gazeRay.direction * rayLength);
            if (displayManager.TryGetForemostHit(gazeRay, out DisplayHit gazeHit))
            {
                Gizmos.DrawWireSphere(gazeHit.Hit.point, hitPointRadius * 1.4f);
            }
        }

        DisplaySurface focused = displayManager.FocusedDisplay;
        if (focused != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(focused.transform.position, new Vector3(focused.PhysicalSizeMeters.x, focused.PhysicalSizeMeters.y, 0.02f));
        }
    }

    private void OnGUI()
    {
        if (!showOverlay || displayManager == null)
        {
            return;
        }

        Ray controllerRay = GetControllerRay();
        bool hasRayHit = displayManager.TryGetForemostHit(controllerRay, out DisplayHit rayHit);
        DisplayHit gazeHit = default;
        bool hasGazeHit = gazeProvider != null && displayManager.TryGetForemostHit(gazeProvider.GetRay(), out gazeHit);

        GUI.Box(new Rect(12f, 12f, 420f, 118f), "Explicit Display Focus Debug");
        GUI.Label(new Rect(24f, 40f, 390f, 20f), $"Condition: {(inputManager != null ? inputManager.CurrentCondition.ToString() : "Unknown")}");
        GUI.Label(new Rect(24f, 62f, 390f, 20f), $"Controller hit: {(hasRayHit ? rayHit.DisplayId + " " + Format(rayHit.Normalized) : "None")}");
        GUI.Label(new Rect(24f, 84f, 390f, 20f), $"Gaze hit: {(hasGazeHit ? gazeHit.DisplayId + " " + Format(gazeHit.Normalized) : "None")}");
        GUI.Label(new Rect(24f, 106f, 390f, 20f), $"Focused display: {(displayManager.FocusedDisplay != null ? displayManager.FocusedDisplay.name : "None")}");
    }

    private void ResolveReferences()
    {
        if (inputManager == null)
        {
            inputManager = PrototypeInputManager.Instance != null ? PrototypeInputManager.Instance : FindObjectOfType<PrototypeInputManager>();
        }

        if (displayManager == null)
        {
            displayManager = DisplayManager.Instance != null ? DisplayManager.Instance : FindObjectOfType<DisplayManager>();
        }

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }
    }

    private Ray GetControllerRay()
    {
        Transform source = controllerRaySource;
        if (source == null && Camera.main != null)
        {
            source = Camera.main.transform;
        }

        return source != null
            ? new Ray(source.position, source.forward)
            : new Ray(transform.position, transform.forward);
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
