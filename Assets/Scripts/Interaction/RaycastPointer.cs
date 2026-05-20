using UnityEngine;

[DisallowMultipleComponent]
public class RaycastPointer : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private Transform rightControllerTransform;
    [SerializeField] private LineRenderer rayLine;
    [SerializeField] private float visibleRayLength = 4f;
    [SerializeField] private float rayStartWidth = 0.0035f;
    [SerializeField] private float rayEndWidth = 0.001f;
    [SerializeField] private Color rayColor = Color.cyan;
    [SerializeField] private bool showRayLine = true;

    public Ray CurrentRay { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        EnsureLineRenderer();
    }

    private void Update()
    {
        ResolveReferences();

        if (inputManager == null || displayManager == null)
        {
            CurrentRay = GetPointerRay();
            SetRayVisible(false);
            return;
        }

        Ray pointerRay = GetPointerRay();
        CurrentRay = pointerRay;

        if (inputManager.CurrentCondition != InteractionCondition.RaycastBaseline)
        {
            displayManager.ClearCurrentRaycastHit();
            SetRayVisible(false);
            return;
        }

        Vector3 lineEnd = pointerRay.origin + pointerRay.direction * visibleRayLength;

        if (displayManager.TryGetFirstDisplayHit(pointerRay, out DisplayHit hit))
        {
            displayManager.SetCurrentRaycastHit(hit);
            displayManager.SetOnlyCursorsVisible(hit.Display, null);
            displayManager.SetCursorNormalized(hit.Display, hit.Normalized, true);
            lineEnd = hit.Hit.point;

            if (inputManager.SubmitPressed)
            {
                Debug.Log($"[RaycastPointer] condition={inputManager.CurrentCondition}, displayId={hit.DisplayId}, normalized={Format(hit.Normalized)}");
            }
        }
        else
        {
            displayManager.ClearCurrentRaycastHit();
            displayManager.SetOnlyCursorsVisible(null, null);
        }

        UpdateRayLine(pointerRay.origin, lineEnd);
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
    }

    private Ray GetPointerRay()
    {
        Transform source = rightControllerTransform;
        if (source == null && Camera.main != null)
        {
            source = Camera.main.transform;
        }

        return source != null
            ? new Ray(source.position, source.forward)
            : new Ray(transform.position, transform.forward);
    }

    private void EnsureLineRenderer()
    {
        if (rayLine == null)
        {
            rayLine = GetComponent<LineRenderer>();
        }

        if (rayLine == null)
        {
            rayLine = gameObject.AddComponent<LineRenderer>();
        }

        rayLine.useWorldSpace = true;
        rayLine.positionCount = 2;
        rayLine.startWidth = rayStartWidth;
        rayLine.endWidth = rayEndWidth;
        rayLine.material = new Material(Shader.Find("Sprites/Default"));
        rayLine.startColor = rayColor;
        rayLine.endColor = rayColor;
        SetRayVisible(false);
    }

    private void UpdateRayLine(Vector3 start, Vector3 end)
    {
        EnsureLineRenderer();
        rayLine.SetPosition(0, start);
        rayLine.SetPosition(1, end);
        SetRayVisible(showRayLine);
    }

    public void SetRayLineVisible(bool visible)
    {
        showRayLine = visible;
        SetRayVisible(visible);
    }

    private void SetRayVisible(bool visible)
    {
        if (rayLine != null)
        {
            rayLine.enabled = visible;
        }
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
