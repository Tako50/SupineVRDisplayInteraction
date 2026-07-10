using UnityEngine;

public enum RayVisualLengthLevel
{
    Short,
    Medium,
    Long
}

[DisallowMultipleComponent]
/// <summary>
/// RaycastBaseline用の右コントローラRay。
/// Physics.Raycast 1回で最前面のDisplaySurfaceだけを操作対象にし、遮蔽を再現する。
/// </summary>
public class RaycastPointer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private Transform rightControllerTransform;
    [SerializeField] private LineRenderer rayLine;

    [Header("Ray Visual")]
    [Tooltip("Current visible controller Ray state. ExperimentManager can override this per condition.")]
    [SerializeField] private bool showRayLine = false;
    [Tooltip("Current visible Ray length preset. ExperimentManager can override this per condition.")]
    [SerializeField] private RayVisualLengthLevel rayLengthLevel = RayVisualLengthLevel.Medium;
    [Min(0.01f)]
    [SerializeField] private float shortRayLength = 1.5f;
    [Min(0.01f)]
    [SerializeField] private float mediumRayLength = 2.5f;
    [Min(0.01f)]
    [SerializeField] private float longRayLength = 4f;

    [Header("Ray Appearance")]
    [Min(0.0001f)]
    [SerializeField] private float rayStartWidth = 0.0035f;
    [Min(0.0001f)]
    [SerializeField] private float rayEndWidth = 0.001f;
    [SerializeField] private Color rayColor = new Color(0.4f, 0.8f, 1f, 0.55f);
    [Tooltip("Draw the visible Ray over display surfaces so screenshots do not make it look hidden by the hit plane.")]
    [SerializeField] private bool drawRayVisualOnTop = true;
    [Tooltip("Keep the Ray endpoint slightly in front of the first hit surface to avoid z-fighting in screenshots.")]
    [Min(0f)]
    [SerializeField] private float rayHitSurfaceOffsetMeters = 0.01f;
    [SerializeField] private bool logRayVisualSettingChanges = true;

    public Ray CurrentRay { get; private set; }
    public Transform RightControllerTransform => rightControllerTransform;
    public bool RayVisualEnabled => showRayLine;
    public RayVisualLengthLevel RayLengthLevel => rayLengthLevel;
    public float VisibleRayLengthMeters => ResolveVisibleRayLengthMeters(rayLengthLevel);

    private void Awake()
    {
        ResolveReferences();
        EnsureLineRenderer();
    }

    private void OnValidate()
    {
        ClampInspectorValues();
        ApplyLineRendererAppearanceIfAvailable();
        if (!showRayLine)
        {
            SetRayVisible(false);
        }
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
        float visibleLength = ResolveVisibleRayLengthMeters(rayLengthLevel);

        if (inputManager.CurrentCondition != InteractionCondition.RaycastBaseline)
        {
            // ExplicitDisplayFocus中はBaselineのRayヒット状態を消す。Ray線は条件設定に応じて表示だけ行える。
            displayManager.ClearCurrentRaycastHit();
            UpdateRayLine(pointerRay.origin, pointerRay.origin + pointerRay.direction * visibleLength);
            return;
        }

        Vector3 lineEnd = pointerRay.origin + pointerRay.direction * visibleLength;

        if (displayManager.TryGetFirstDisplayHit(pointerRay, out DisplayHit hit))
        {
            displayManager.SetCurrentRaycastHit(hit);
            displayManager.SetOnlyCursorsVisible(hit.Display, null);
            displayManager.SetCursorNormalized(hit.Display, hit.Normalized, true);

            float hitDistance = Vector3.Distance(pointerRay.origin, hit.Hit.point);
            if (hitDistance <= visibleLength)
            {
                lineEnd = GetVisibleHitLineEnd(pointerRay, hit.Hit.point);
            }

            if (inputManager.SubmitReleased)
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
        ClampInspectorValues();

        if (rayLine == null)
        {
            rayLine = GetComponent<LineRenderer>();
        }

        if (rayLine == null)
        {
            rayLine = gameObject.AddComponent<LineRenderer>();
        }

        ApplyLineRendererAppearanceIfAvailable();
        SetRayVisible(false);
    }

    private void ApplyLineRendererAppearanceIfAvailable()
    {
        if (rayLine == null)
        {
            return;
        }

        rayLine.useWorldSpace = true;
        rayLine.positionCount = 2;
        rayLine.startWidth = rayStartWidth;
        rayLine.endWidth = rayEndWidth;
        if (rayLine.sharedMaterial == null)
        {
            rayLine.material = CreateRayLineMaterial();
        }

        rayLine.sortingOrder = drawRayVisualOnTop ? 100 : 0;
        rayLine.startColor = rayColor;
        rayLine.endColor = rayColor;
    }

    private Material CreateRayLineMaterial()
    {
        Shader shader = drawRayVisualOnTop
            ? Shader.Find("UI/NoZTest")
            : null;

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.name = drawRayVisualOnTop
            ? "RaycastPointer_Visual_Overlay"
            : "RaycastPointer_Visual";
        material.color = Color.white;
        if (drawRayVisualOnTop)
        {
            material.renderQueue = 5000;
        }

        return material;
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
        SetRayVisualEnabled(visible);
    }

    public void SetRayVisualEnabled(bool enabled)
    {
        if (showRayLine == enabled)
        {
            if (!enabled)
            {
                SetRayVisible(false);
            }

            return;
        }

        showRayLine = enabled;
        if (!enabled)
        {
            SetRayVisible(false);
        }

        if (logRayVisualSettingChanges)
        {
            Debug.Log($"[RaycastPointer] rayVisualEnabled={showRayLine}");
        }
    }

    public void SetRayLengthLevel(RayVisualLengthLevel level)
    {
        if (rayLengthLevel == level)
        {
            return;
        }

        rayLengthLevel = level;
        if (logRayVisualSettingChanges)
        {
            Debug.Log($"[RaycastPointer] rayLengthLevel={rayLengthLevel}, visibleLengthMeters={ResolveVisibleRayLengthMeters(rayLengthLevel):0.00}");
        }
    }

    public void SetRayVisualSettings(bool enabled, RayVisualLengthLevel level)
    {
        bool changed = showRayLine != enabled || rayLengthLevel != level;
        showRayLine = enabled;
        rayLengthLevel = level;
        if (!enabled)
        {
            SetRayVisible(false);
        }

        if (changed && logRayVisualSettingChanges)
        {
            Debug.Log(
                $"[RaycastPointer] rayVisualEnabled={showRayLine}, "
                + $"rayLengthLevel={rayLengthLevel}, visibleLengthMeters={ResolveVisibleRayLengthMeters(rayLengthLevel):0.00}");
        }
    }

    private float ResolveVisibleRayLengthMeters(RayVisualLengthLevel level)
    {
        switch (level)
        {
            case RayVisualLengthLevel.Short:
                return Mathf.Max(0.01f, shortRayLength);
            case RayVisualLengthLevel.Long:
                return Mathf.Max(0.01f, longRayLength);
            default:
                return Mathf.Max(0.01f, mediumRayLength);
        }
    }

    private void ClampInspectorValues()
    {
        shortRayLength = Mathf.Max(0.01f, shortRayLength);
        mediumRayLength = Mathf.Max(0.01f, mediumRayLength);
        longRayLength = Mathf.Max(0.01f, longRayLength);
        rayStartWidth = Mathf.Max(0.0001f, rayStartWidth);
        rayEndWidth = Mathf.Max(0.0001f, rayEndWidth);
        rayHitSurfaceOffsetMeters = Mathf.Max(0f, rayHitSurfaceOffsetMeters);
    }

    private Vector3 GetVisibleHitLineEnd(Ray pointerRay, Vector3 hitPoint)
    {
        if (rayHitSurfaceOffsetMeters <= 0f)
        {
            return hitPoint;
        }

        float hitDistance = Vector3.Distance(pointerRay.origin, hitPoint);
        float visibleDistance = Mathf.Max(0f, hitDistance - rayHitSurfaceOffsetMeters);
        return pointerRay.origin + pointerRay.direction * visibleDistance;
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
