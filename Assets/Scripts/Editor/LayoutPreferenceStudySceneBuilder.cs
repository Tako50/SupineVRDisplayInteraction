using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LayoutPreferenceStudySceneBuilder
{
    private const string ScenePath = "Assets/Scenes/LayoutPreferenceStudy.unity";

    [MenuItem("Prototype/Build Layout Preference Study Scene")]
    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Camera hmdCamera = EnsureCamera();
        EnsureLight();
        EnsureEnvironmentLighting();

        GameObject root = new GameObject("LayoutPreferenceStudy_Root");
        Transform displaysRoot = EnsureChild(root.transform, "Displays");
        Transform managersRoot = EnsureChild(root.transform, "Study_Managers");

        DisplaySurface displayA = CreateDisplay(displaysRoot, "Display_A", "Display A", new Color(0.62f, 0.82f, 1f, 1f));
        DisplaySurface displayB = CreateDisplay(displaysRoot, "Display_B", "Display B", new Color(0.82f, 0.84f, 0.86f, 1f));

        GameObject loggerObject = new GameObject("LayoutPreferenceLogger");
        loggerObject.transform.SetParent(managersRoot, false);
        LayoutPreferenceLogger logger = loggerObject.AddComponent<LayoutPreferenceLogger>();

        GameObject managerObject = new GameObject("LayoutPreferenceStudyManager");
        managerObject.transform.SetParent(managersRoot, false);
        LayoutPreferenceStudyManager manager = managerObject.AddComponent<LayoutPreferenceStudyManager>();

        Transform instructionCanvas = CreateInstructionCanvas(root.transform);
        TMP_Text titleText = instructionCanvas.Find("Panel/Title").GetComponent<TMP_Text>();
        TMP_Text conditionText = instructionCanvas.Find("Panel/Condition").GetComponent<TMP_Text>();
        TMP_Text progressText = instructionCanvas.Find("Panel/Progress").GetComponent<TMP_Text>();
        TMP_Text instructionText = instructionCanvas.Find("Panel/Instructions").GetComponent<TMP_Text>();
        TMP_Text statusText = instructionCanvas.Find("Panel/Status").GetComponent<TMP_Text>();

        SetObject(manager, "hmdCamera", hmdCamera);
        SetObject(manager, "displaysRoot", displaysRoot);
        SetObject(manager, "displayA", displayA);
        SetObject(manager, "displayB", displayB);
        SetObject(manager, "logger", logger);
        SetObject(manager, "instructionCanvasRoot", instructionCanvas);
        SetObject(manager, "titleText", titleText);
        SetObject(manager, "conditionText", conditionText);
        SetObject(manager, "progressText", progressText);
        SetObject(manager, "instructionText", instructionText);
        SetObject(manager, "statusText", statusText);
        SetString(manager, "participantId", "P01");
        SetString(manager, "sessionId", "S01");
        SetBool(manager, "randomizeOrder", false);
        SetBool(manager, "useParticipantIdAsSeed", true);
        SetBool(manager, "menuHoldRecenters", false);
        SetBool(manager, "useInputSystemActions", true);
        SetBool(manager, "useOvrInputFallback", false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LayoutPreferenceStudySceneBuilder] Built {ScenePath} as an independent Study 1 scene.");
    }

    private static Camera EnsureCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = Vector3.zero;
        cameraObject.transform.rotation = Quaternion.identity;

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.01f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.36f, 0.38f, 0.42f, 1f);
        cameraObject.AddComponent<AudioListener>();
        AddComponentByTypeName(cameraObject, "UnityEngine.InputSystem.XR.TrackedPoseDriver");
        AddComponentByTypeName(cameraObject, "UnityEngine.SpatialTracking.TrackedPoseDriver");
        return camera;
    }

    private static void EnsureLight()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
    }

    private static void EnsureEnvironmentLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.68f, 1f);
        RenderSettings.fog = false;
    }

    private static DisplaySurface CreateDisplay(Transform parent, string name, string label, Color panelColor)
    {
        GameObject displayObject = new GameObject(name);
        displayObject.transform.SetParent(parent, false);
        DisplaySurface surface = displayObject.AddComponent<DisplaySurface>();

        RectTransform canvasTransform = CreateRectChild(displayObject.transform, "WorldSpaceCanvas");
        Canvas canvas = canvasTransform.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasTransform.gameObject.AddComponent<CanvasScaler>();
        canvasTransform.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform panel = CreateRectChild(canvasTransform, "VisiblePanel");
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = panelColor;
        panel.gameObject.AddComponent<RectMask2D>();

        RectTransform labelRect = CreateRectChild(panel, "DisplayLabel");
        TextMeshProUGUI labelText = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.alignment = TextAlignmentOptions.TopLeft;
        labelText.color = Color.black;
        labelText.fontSize = 44f;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.offsetMin = new Vector2(28f, -82f);
        labelRect.offsetMax = new Vector2(-28f, -18f);

        CreatePseudoContent(panel, panelColor);

        RectTransform cursor = CreateRectChild(canvasTransform, "Cursor");
        Image cursorImage = cursor.gameObject.AddComponent<Image>();
        cursorImage.color = Color.clear;
        cursor.gameObject.SetActive(false);

        Transform hitPlaneTransform = EnsureChild(displayObject.transform, "TransparentHitPlane");
        BoxCollider hitPlane = hitPlaneTransform.gameObject.AddComponent<BoxCollider>();
        hitPlane.enabled = false;
        hitPlane.isTrigger = true;

        surface.AssignParts(canvas, panel, hitPlane, cursor);
        surface.SetContentMode(DisplayContentMode.ConditionSelection);
        surface.SetSize(new Vector2(0.8f, 0.45f), new Vector2(800f, 450f));
        return surface;
    }

    private static void CreatePseudoContent(RectTransform panel, Color baseColor)
    {
        RectTransform content = CreateRectChild(panel, "StudyContent");
        content.anchorMin = new Vector2(0.06f, 0.08f);
        content.anchorMax = new Vector2(0.94f, 0.80f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        for (int i = 0; i < 4; i++)
        {
            RectTransform row = CreateRectChild(content, $"ContentBlock_{i + 1}");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -18f - i * 72f);
            row.sizeDelta = new Vector2(0f, 42f);
            Image rowImage = row.gameObject.AddComponent<Image>();
            float alpha = i % 2 == 0 ? 0.22f : 0.14f;
            rowImage.color = new Color(0f, 0f, 0f, alpha);
        }

        RectTransform textRect = CreateRectChild(content, "PseudoText");
        textRect.anchorMin = new Vector2(0.02f, 0.02f);
        textRect.anchorMax = new Vector2(0.98f, 0.98f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = "Visual preview\nText block\nWindow content";
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0f, 0f, 0f, 0.72f);
        text.fontSize = 36f;

        RectTransform accent = CreateRectChild(panel, "Accent");
        accent.anchorMin = new Vector2(0.06f, 0.05f);
        accent.anchorMax = new Vector2(0.34f, 0.10f);
        accent.offsetMin = Vector2.zero;
        accent.offsetMax = Vector2.zero;
        Image accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.color = new Color(
            Mathf.Clamp01(baseColor.r - 0.30f),
            Mathf.Clamp01(baseColor.g - 0.30f),
            Mathf.Clamp01(baseColor.b - 0.30f),
            0.75f);
    }

    private static Transform CreateInstructionCanvas(Transform parent)
    {
        RectTransform canvasTransform = CreateRectChild(parent, "WorldInstructionCanvas");
        Canvas canvas = canvasTransform.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasTransform.gameObject.AddComponent<CanvasScaler>();
        canvasTransform.gameObject.AddComponent<GraphicRaycaster>();
        canvasTransform.sizeDelta = new Vector2(760f, 96f);
        canvasTransform.localScale = Vector3.one * 0.001f;

        RectTransform panel = CreateRectChild(canvasTransform, "Panel");
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;

        CreateUiText(panel, "Title", string.Empty, 1f, new Vector2(0f, 0f), new Vector2(1f, 1f));
        CreateUiText(panel, "Condition", "LeftRight", 38f, new Vector2(0f, 0f), new Vector2(1f, 1f));
        CreateUiText(panel, "Progress", string.Empty, 1f, new Vector2(0f, 0f), new Vector2(1f, 1f));
        CreateUiText(panel, "Instructions", string.Empty, 1f, new Vector2(0f, 0f), new Vector2(1f, 1f));
        CreateUiText(panel, "Status", string.Empty, 1f, new Vector2(0f, 0f), new Vector2(1f, 1f));
        return canvasTransform;
    }

    private static TextMeshProUGUI CreateUiText(
        RectTransform parent,
        string name,
        string text,
        float fontSize,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        RectTransform rectTransform = CreateRectChild(parent, name);
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = new Vector2(16f, 0f);
        rectTransform.offsetMax = new Vector2(-16f, 0f);

        TextMeshProUGUI tmp = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontSize = fontSize;
        return tmp;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static RectTransform CreateRectChild(Transform parent, string name)
    {
        GameObject childObject = new GameObject(name, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject.GetComponent<RectTransform>();
    }

    private static void AddComponentByTypeName(GameObject target, string typeName)
    {
        Type type = FindType(typeName);
        if (type != null && typeof(Component).IsAssignableFrom(type) && target.GetComponent(type) == null)
        {
            target.AddComponent(type);
        }
    }

    private static Type FindType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetString(UnityEngine.Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetBool(UnityEngine.Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
