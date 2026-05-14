using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DevPrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Dev_Prototype.unity";

    [MenuItem("Prototype/Build Dev Prototype Scene")]
    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureCamera();
        EnsureLight();

        Camera hmdCamera = Camera.main != null ? Camera.main : EnsureCamera();
        DisableUnusedXriLocomotionAndLines();

        GameObject staleControllerSource = GameObject.Find("Right_Controller_DebugRay");
        if (staleControllerSource != null && staleControllerSource.transform.parent != hmdCamera.transform)
        {
            Object.DestroyImmediate(staleControllerSource);
        }

        Transform controllerRaySource = ResolveRightControllerRaySource(hmdCamera.transform);

        GameObject prototypeRoot = GameObject.Find("Prototype_Root") ?? new GameObject("Prototype_Root");
        Transform displaysRoot = EnsureChild(prototypeRoot.transform, "Displays");

        DisplaySurface displayA = CreateOrUpdateDisplay(displaysRoot, "Display_A_Front", new Color(0.12f, 0.40f, 0.90f, 0.86f), "A Front");
        DisplaySurface displayB = CreateOrUpdateDisplay(displaysRoot, "Display_B_Back", new Color(0.90f, 0.38f, 0.16f, 0.82f), "B Back");

        GameObject managers = GameObject.Find("Prototype_Managers") ?? new GameObject("Prototype_Managers");
        ExperimentManager experimentManager = GetOrAdd<ExperimentManager>(managers);
        EditorDebugInputProvider debugInputProvider = GetOrAdd<EditorDebugInputProvider>(managers);
        PrototypeInputManager inputManager = GetOrAdd<PrototypeInputManager>(managers);
        GazeProvider gazeProvider = GetOrAdd<GazeProvider>(managers);
        EyeTrackingRayAdapter eyeTrackingRayAdapter = GetOrAdd<EyeTrackingRayAdapter>(managers);
        DisplayManager displayManager = GetOrAdd<DisplayManager>(managers);
        DisplayLayoutManager layoutManager = GetOrAdd<DisplayLayoutManager>(managers);
        FocusManager focusManager = GetOrAdd<FocusManager>(managers);
        RaycastPointer raycastPointer = GetOrAdd<RaycastPointer>(managers);
        VirtualCursorController cursorController = GetOrAdd<VirtualCursorController>(managers);
        ScrollController scrollController = GetOrAdd<ScrollController>(managers);
        ClickDispatcher clickDispatcher = GetOrAdd<ClickDispatcher>(managers);
        Logger logger = GetOrAdd<Logger>(managers);
        PrototypeDebugVisualizer debugVisualizer = GetOrAdd<PrototypeDebugVisualizer>(managers);

        SetObject(layoutManager, "hmdCamera", hmdCamera);
        SetObject(layoutManager, "displaysRoot", displaysRoot);
        SetObject(layoutManager, "displayAFront", displayA);
        SetObject(layoutManager, "displayBBack", displayB);
        SetEnum(layoutManager, "initialPreset", DisplayLayoutPreset.StrongOcclusion);

        SetObject(gazeProvider, "hmdCamera", hmdCamera);
        Transform eyeTrackingRaySource = EnsureChild(managers.transform, "EyeTracking_RaySource");
        SetObject(eyeTrackingRayAdapter, "hmdCamera", hmdCamera);
        SetObject(eyeTrackingRayAdapter, "raySource", eyeTrackingRaySource);
        SetObject(gazeProvider, "eyeTrackingAdapter", eyeTrackingRayAdapter);
        SetObject(gazeProvider, "eyeTrackingRaySource", eyeTrackingRaySource);
        SetEnum(gazeProvider, "gazeSource", GazeSource.DebugCameraForward);
        SetObject(inputManager, "debugInputProvider", debugInputProvider);
        SetBool(inputManager, "allowConditionToggle", true);

        SetObject(experimentManager, "inputManager", inputManager);
        SetObject(experimentManager, "gazeProvider", gazeProvider);
        SetObject(experimentManager, "displayManager", displayManager);
        SetObject(experimentManager, "layoutManager", layoutManager);
        SetObject(experimentManager, "raycastPointer", raycastPointer);
        SetObject(experimentManager, "focusManager", focusManager);
        SetObject(experimentManager, "debugInputProvider", debugInputProvider);
        SetObject(experimentManager, "logger", logger);
        SetEnum(experimentManager, "startingCondition", InteractionCondition.RaycastBaseline);

        SetObject(focusManager, "inputManager", inputManager);
        SetObject(focusManager, "gazeProvider", gazeProvider);
        SetObject(focusManager, "displayManager", displayManager);
        SetObject(focusManager, "virtualCursorController", cursorController);
        SetObject(focusManager, "logger", logger);

        SetObject(raycastPointer, "inputManager", inputManager);
        SetObject(raycastPointer, "displayManager", displayManager);
        SetObject(raycastPointer, "rightControllerTransform", controllerRaySource);

        SetObject(cursorController, "inputManager", inputManager);
        SetObject(cursorController, "displayManager", displayManager);

        SetObject(scrollController, "inputManager", inputManager);
        SetObject(scrollController, "displayManager", displayManager);
        SetObject(scrollController, "raycastPointer", raycastPointer);
        SetObject(scrollController, "focusManager", focusManager);
        SetObject(scrollController, "virtualCursorController", cursorController);
        SetObject(scrollController, "gazeProvider", gazeProvider);
        SetObject(scrollController, "logger", logger);

        SetObject(clickDispatcher, "inputManager", inputManager);
        SetObject(clickDispatcher, "displayManager", displayManager);
        SetObject(clickDispatcher, "raycastPointer", raycastPointer);
        SetObject(clickDispatcher, "focusManager", focusManager);
        SetObject(clickDispatcher, "virtualCursorController", cursorController);
        SetObject(clickDispatcher, "gazeProvider", gazeProvider);
        SetObject(clickDispatcher, "logger", logger);

        SetObject(debugVisualizer, "inputManager", inputManager);
        SetObject(debugVisualizer, "gazeProvider", gazeProvider);
        SetObject(debugVisualizer, "displayManager", displayManager);
        SetObject(debugVisualizer, "focusManager", focusManager);
        SetObject(debugVisualizer, "virtualCursorController", cursorController);
        SetObject(debugVisualizer, "clickDispatcher", clickDispatcher);
        SetObject(debugVisualizer, "scrollController", scrollController);
        SetObject(debugVisualizer, "experimentManager", experimentManager);
        SetObject(debugVisualizer, "debugInputProvider", debugInputProvider);
        SetObject(debugVisualizer, "controllerRaySource", controllerRaySource);

        layoutManager.ApplyLayout(DisplayLayoutPreset.StrongOcclusion);
        displayManager.RefreshDisplays();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DevPrototypeSceneBuilder] Built Dev_Prototype scene with two display surfaces, hit planes, managers, and debug visuals.");
    }

    private static Camera EnsureCamera()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            return camera;
        }

        GameObject cameraObject = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 1.2f, -0.2f);
        cameraObject.transform.rotation = Quaternion.identity;
        camera = GetOrAdd<Camera>(cameraObject);
        camera.nearClipPlane = 0.01f;
        GetOrAdd<AudioListener>(cameraObject);
        return camera;
    }

    private static Transform ResolveRightControllerRaySource(Transform fallbackParent)
    {
        GameObject rightController = GameObject.Find("Right Controller");
        if (rightController != null)
        {
            return rightController.transform;
        }

        Transform debugRay = EnsureChild(fallbackParent, "Right_Controller_DebugRay");
        debugRay.localPosition = new Vector3(0.25f, -0.15f, 0.05f);
        debugRay.localRotation = Quaternion.identity;
        return debugRay;
    }

    private static void EnsureLight()
    {
        if (Object.FindObjectOfType<Light>() != null)
        {
            return;
        }

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void DisableUnusedXriLocomotionAndLines()
    {
        SetNamedObjectActive("XR Interaction Setup/XR Origin (XR Rig)/Locomotion System/Move", false);
        SetNamedObjectActive("XR Interaction Setup/XR Origin (XR Rig)/Locomotion System/Turn", false);
        SetNamedObjectActive("XR Interaction Setup/XR Origin (XR Rig)/Locomotion System/Teleportation", false);
        SetNamedObjectActive("XR Interaction Setup/XR Origin (XR Rig)/Locomotion System/Climb", false);
        SetNamedObjectActive("XR Interaction Setup/XR Origin (XR Rig)/Locomotion System/Grab Move", false);

        foreach (GameObject gameObject in Object.FindObjectsOfType<GameObject>(true))
        {
            if (gameObject.name.Contains("Teleport Interactor"))
            {
                gameObject.SetActive(false);
            }

            DisableComponentIfPresent(gameObject, "ActionBasedControllerManager");
            DisableComponentIfPresent(gameObject, "XRInteractorLineVisual");

            LineRenderer lineRenderer = gameObject.GetComponent<LineRenderer>();
            if (lineRenderer != null && gameObject.transform.root.name == "XR Interaction Setup")
            {
                lineRenderer.enabled = false;
            }
        }
    }

    private static void SetNamedObjectActive(string path, bool active)
    {
        GameObject gameObject = GameObject.Find(path);
        if (gameObject != null)
        {
            gameObject.SetActive(active);
        }
    }

    private static void DisableComponentIfPresent(GameObject gameObject, string typeName)
    {
        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component.GetType().Name != typeName)
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty enabledProperty = serializedObject.FindProperty("m_Enabled");
            if (enabledProperty != null)
            {
                enabledProperty.boolValue = false;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static DisplaySurface CreateOrUpdateDisplay(Transform parent, string name, Color panelColor, string label)
    {
        Transform displayTransform = parent.Find(name);
        GameObject displayObject = displayTransform != null ? displayTransform.gameObject : new GameObject(name);
        displayObject.transform.SetParent(parent, false);

        DisplaySurface surface = GetOrAdd<DisplaySurface>(displayObject);

        Transform canvasTransform = EnsureChild(displayObject.transform, "WorldSpaceCanvas");
        Canvas canvas = GetOrAdd<Canvas>(canvasTransform.gameObject);
        canvasTransform = canvas.transform;
        GetOrAdd<CanvasScaler>(canvasTransform.gameObject);
        GetOrAdd<GraphicRaycaster>(canvasTransform.gameObject);

        RectTransform panel = EnsureRectChild(canvasTransform, "VisiblePanel");
        Image panelImage = GetOrAdd<Image>(panel.gameObject);
        panelImage.color = panelColor;
        GetOrAdd<RectMask2D>(panel.gameObject);

        RectTransform title = EnsureRectChild(panel, "Label");
        Text titleText = GetOrAdd<Text>(title.gameObject);
        titleText.text = label;
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.color = Color.white;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 42;
        title.anchorMin = new Vector2(0f, 1f);
        title.anchorMax = new Vector2(1f, 1f);
        title.pivot = new Vector2(0.5f, 1f);
        title.offsetMin = new Vector2(24f, -76f);
        title.offsetMax = new Vector2(-24f, -16f);

        RectTransform content = EnsureRectChild(panel, "Pseudo2DContent");
        content.anchorMin = new Vector2(0.08f, 0.04f);
        content.anchorMax = new Vector2(0.92f, 0.86f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(0f, -260f);
        content.offsetMax = new Vector2(0f, 260f);

        Text contentText = GetOrAdd<Text>(content.gameObject);
        contentText.text =
            "RaycastBaseline debug page\n\n" +
            "Use the right controller ray to move the cursor.\n" +
            "Press A / Space on the target button.\n" +
            "Use right stick vertical / W-S to scroll this content.\n\n" +
            "Line 01  Baseline scroll content\n" +
            "Line 02  Front-most ray hit receives input\n" +
            "Line 03  Rear displays do not receive input through the front display\n" +
            "Line 04  CSV and Debug.Log events are emitted\n" +
            "Line 05  More content below the visible panel\n" +
            "Line 06  More content below the visible panel\n" +
            "Line 07  More content below the visible panel";
        contentText.alignment = TextAnchor.UpperCenter;
        contentText.color = new Color(1f, 1f, 1f, 0.86f);
        contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        contentText.fontSize = 28;

        RectTransform clickTarget = EnsureRectChild(content, "DebugClickTarget");
        Image targetImage = GetOrAdd<Image>(clickTarget.gameObject);
        targetImage.color = new Color(0.05f, 0.95f, 0.42f, 0.95f);
        clickTarget.anchorMin = new Vector2(0.5f, 0.5f);
        clickTarget.anchorMax = new Vector2(0.5f, 0.5f);
        clickTarget.pivot = new Vector2(0.5f, 0.5f);
        clickTarget.anchoredPosition = new Vector2(0f, -95f);
        clickTarget.sizeDelta = new Vector2(260f, 76f);

        RectTransform targetLabel = EnsureRectChild(clickTarget, "Label");
        Text targetText = GetOrAdd<Text>(targetLabel.gameObject);
        targetText.text = "DEBUG TARGET";
        targetText.alignment = TextAnchor.MiddleCenter;
        targetText.color = Color.black;
        targetText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        targetText.fontSize = 30;
        targetLabel.anchorMin = Vector2.zero;
        targetLabel.anchorMax = Vector2.one;
        targetLabel.offsetMin = Vector2.zero;
        targetLabel.offsetMax = Vector2.zero;

        RectTransform cursor = EnsureRectChild(canvasTransform, "Cursor");
        Image cursorImage = GetOrAdd<Image>(cursor.gameObject);
        cursorImage.color = Color.yellow;

        Transform hitPlaneTransform = EnsureChild(displayObject.transform, "TransparentHitPlane");
        BoxCollider hitPlane = GetOrAdd<BoxCollider>(hitPlaneTransform.gameObject);

        surface.AssignParts(canvas, panel, hitPlane, cursor);
        surface.AssignDebugContent(content, clickTarget);
        cursor.gameObject.SetActive(false);
        return surface;
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

    private static RectTransform EnsureRectChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        GameObject childObject = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject.GetComponent<RectTransform>();
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetEnum(Object target, string propertyName, System.Enum value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = System.Convert.ToInt32(value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetBool(Object target, string propertyName, bool value)
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
