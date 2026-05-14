using UnityEngine;

[DisallowMultipleComponent]
public class ScrollController : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private Logger logger;
    [SerializeField] private float scrollLogInterval = 0.12f;
    [SerializeField] private float stickScrollDeadzone = 0.05f;

    private float lastScrollLogTime;

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

        if (inputManager.CurrentCondition != InteractionCondition.RaycastBaseline)
        {
            return;
        }

        if (!displayManager.HasCurrentRaycastHit)
        {
            return;
        }

        float stickVertical = inputManager.Stick.y;
        if (Mathf.Abs(stickVertical) < stickScrollDeadzone)
        {
            return;
        }

        DisplayHit hit = displayManager.CurrentRaycastHit;
        if (hit.Display == null)
        {
            return;
        }

        float appliedScroll = hit.Display.Scroll(stickVertical, Time.deltaTime);
        if (Mathf.Approximately(appliedScroll, 0f))
        {
            return;
        }

        if (Time.time - lastScrollLogTime >= scrollLogInterval)
        {
            lastScrollLogTime = Time.time;
            Ray ray = raycastPointer != null ? raycastPointer.CurrentRay : default;
            if (logger != null)
            {
                logger.LogRaycastScroll(inputManager.CurrentCondition, hit.DisplayId, hit.Normalized, appliedScroll, ray.origin, ray.direction);
            }
            else
            {
                Debug.Log($"[ScrollController] condition={inputManager.CurrentCondition}, displayId={hit.DisplayId}, normalized={Format(hit.Normalized)}, scrollAmount={appliedScroll:0.000}");
            }
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
