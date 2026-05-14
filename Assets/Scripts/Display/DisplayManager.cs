using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DisplayManager : MonoBehaviour
{
    public static DisplayManager Instance { get; private set; }

    [SerializeField] private DisplaySurface[] displays;
    [SerializeField] private DisplaySurface focusedDisplay;
    [SerializeField] private LayerMask raycastLayers = ~0;
    [SerializeField] private float maxRayDistance = 10f;

    private readonly List<DisplayHit> reusableHits = new List<DisplayHit>();

    public DisplaySurface[] Displays => displays;
    public DisplaySurface FocusedDisplay => focusedDisplay;
    public DisplayHit CurrentRaycastHit { get; private set; }
    public bool HasCurrentRaycastHit { get; private set; }

    private void Awake()
    {
        Instance = this;
        RefreshDisplays();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            RefreshDisplays();
        }
    }

    public void RefreshDisplays()
    {
        displays = FindObjectsOfType<DisplaySurface>(true);
    }

    public bool TryGetForemostHit(Ray ray, out DisplayHit displayHit)
    {
        DisplayHit[] hits = GetDisplayHitsAll(ray);
        if (hits.Length > 0)
        {
            displayHit = hits[0];
            return true;
        }

        displayHit = default;
        return false;
    }

    public bool TryGetFirstDisplayHit(Ray ray, out DisplayHit displayHit)
    {
        if (!Physics.Raycast(ray, out RaycastHit physicsHit, maxRayDistance, raycastLayers, QueryTriggerInteraction.Collide))
        {
            displayHit = default;
            return false;
        }

        DisplaySurface display = physicsHit.collider.GetComponentInParent<DisplaySurface>();
        if (display == null || display.TransparentHitPlane == null || physicsHit.collider != display.TransparentHitPlane)
        {
            displayHit = default;
            return false;
        }

        Vector2 normalized = GetNormalizedFromWorldPoint(display, physicsHit.point);
        displayHit = new DisplayHit(display, physicsHit, normalized);
        return true;
    }

    public DisplayHit[] GetDisplayHitsAll(Ray ray)
    {
        reusableHits.Clear();

        RaycastHit[] physicsHits = Physics.RaycastAll(
            ray,
            maxRayDistance,
            raycastLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < physicsHits.Length; i++)
        {
            DisplaySurface display = physicsHits[i].collider.GetComponentInParent<DisplaySurface>();
            if (display == null || display.TransparentHitPlane == null)
            {
                continue;
            }

            if (physicsHits[i].collider != display.TransparentHitPlane)
            {
                continue;
            }

            Vector2 normalized = GetNormalizedFromWorldPoint(display, physicsHits[i].point);
            reusableHits.Add(new DisplayHit(display, physicsHits[i], normalized));
        }

        reusableHits.Sort((left, right) => left.Distance.CompareTo(right.Distance));
        return reusableHits.ToArray();
    }

    public void SetCurrentRaycastHit(DisplayHit hit)
    {
        CurrentRaycastHit = hit;
        HasCurrentRaycastHit = hit.Display != null;
    }

    public void ClearCurrentRaycastHit()
    {
        CurrentRaycastHit = default;
        HasCurrentRaycastHit = false;
    }

    public void SetFocusedDisplay(DisplaySurface display)
    {
        focusedDisplay = display;
    }

    public Vector2 GetNormalizedFromWorldPoint(DisplaySurface display, Vector3 worldPoint)
    {
        if (display == null)
        {
            return new Vector2(0.5f, 0.5f);
        }

        return display.WorldToNormalized(worldPoint);
    }

    public void SetCursorNormalized(DisplaySurface display, Vector2 normalized, bool visible)
    {
        if (display == null || display.Cursor == null)
        {
            return;
        }

        display.Cursor.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        RectTransform canvasRect = display.WorldSpaceCanvas != null
            ? display.WorldSpaceCanvas.GetComponent<RectTransform>()
            : null;

        Vector2 canvasSize = canvasRect != null ? canvasRect.sizeDelta : new Vector2(800f, 450f);
        Vector2 clamped = new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        display.Cursor.anchoredPosition = new Vector2(
            (clamped.x - 0.5f) * canvasSize.x,
            (clamped.y - 0.5f) * canvasSize.y);
    }

    public void SetOnlyCursorsVisible(DisplaySurface primary, DisplaySurface secondary)
    {
        if (displays == null)
        {
            return;
        }

        for (int i = 0; i < displays.Length; i++)
        {
            DisplaySurface display = displays[i];
            if (display != null && display.Cursor != null)
            {
                bool visible = display == primary || display == secondary;
                display.Cursor.gameObject.SetActive(visible);
            }
        }
    }
}

public struct DisplayHit
{
    public readonly DisplaySurface Display;
    public readonly RaycastHit Hit;
    public readonly Vector2 Normalized;

    public float Distance => Hit.distance;
    public string DisplayId => Display != null ? Display.name : "None";

    public DisplayHit(DisplaySurface display, RaycastHit hit, Vector2 normalized)
    {
        Display = display;
        Hit = hit;
        Normalized = normalized;
    }
}
