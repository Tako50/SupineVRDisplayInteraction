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
        Transform leftControllerRaySource = ResolveLeftControllerRaySource(hmdCamera.transform);

        GameObject prototypeRoot = GameObject.Find("Prototype_Root") ?? new GameObject("Prototype_Root");
        Transform displaysRoot = EnsureChild(prototypeRoot.transform, "Displays");

        Color sharedDisplayColor = new Color(0.125f, 0.125f, 0.125f, 1f);
        DisplaySurface displayA = CreateOrUpdateDisplay(displaysRoot, "Display_A_Front", sharedDisplayColor, string.Empty);
        DisplaySurface displayB = CreateOrUpdateDisplay(displaysRoot, "Display_B_Back", sharedDisplayColor, string.Empty);
        TLabWebViewDisplayBridge tLabWebViewBridgeA = GetOrAdd<TLabWebViewDisplayBridge>(displayA.gameObject);
        TLabWebViewDisplayBridge tLabWebViewBridgeB = GetOrAdd<TLabWebViewDisplayBridge>(displayB.gameObject);

        GameObject managers = GameObject.Find("Prototype_Managers") ?? new GameObject("Prototype_Managers");
        Component controlPanel = GetOrAddByTypeName(managers, "PrototypeControlPanel");
        ExperimentManager experimentManager = GetOrAdd<ExperimentManager>(managers);
        EditorDebugInputProvider debugInputProvider = GetOrAdd<EditorDebugInputProvider>(managers);
        PrototypeInputManager inputManager = GetOrAdd<PrototypeInputManager>(managers);
        GazeProvider gazeProvider = GetOrAdd<GazeProvider>(managers);
        EyeTrackingRayAdapter eyeTrackingRayAdapter = GetOrAdd<EyeTrackingRayAdapter>(managers);
        DisplayManager displayManager = GetOrAdd<DisplayManager>(managers);
        DisplayLayoutManager layoutManager = GetOrAdd<DisplayLayoutManager>(managers);
        FocusManager focusManager = GetOrAdd<FocusManager>(managers);
        GazeDisplayFocusManager gazeDisplayFocusManager = GetOrAdd<GazeDisplayFocusManager>(managers);
        RaycastPointer raycastPointer = GetOrAdd<RaycastPointer>(managers);
        VirtualCursorController cursorController = GetOrAdd<VirtualCursorController>(managers);
        ScrollController scrollController = GetOrAdd<ScrollController>(managers);
        ClickDispatcher clickDispatcher = GetOrAdd<ClickDispatcher>(managers);
        FocusPointingTaskManager focusPointingTaskManager = GetOrAdd<FocusPointingTaskManager>(managers);
        WebViewSessionManager webViewSessionManager = GetOrAdd<WebViewSessionManager>(managers);
        T1RayOcclusionLayoutProbe rayOcclusionLayoutProbe = GetOrAdd<T1RayOcclusionLayoutProbe>(managers);
        ErrorEvaluator errorEvaluator = GetOrAdd<ErrorEvaluator>(managers);
        VRTaskMenuManager vrTaskMenuManager = GetOrAdd<VRTaskMenuManager>(managers);
        ExitPanelController exitPanelController = GetOrAdd<ExitPanelController>(managers);
        Logger logger = GetOrAdd<Logger>(managers);
        TrajectoryLogger trajectoryLogger = GetOrAdd<TrajectoryLogger>(managers);
        PrototypeDebugVisualizer debugVisualizer = GetOrAdd<PrototypeDebugVisualizer>(managers);

        SetObject(layoutManager, "hmdCamera", hmdCamera);
        SetObject(layoutManager, "displaysRoot", displaysRoot);
        SetObject(layoutManager, "displayAFront", displayA);
        SetObject(layoutManager, "displayBBack", displayB);
        SetEnum(layoutManager, "initialPreset", DisplayLayoutPreset.UpDownDepth);
        SetBool(layoutManager, "useFixedSceneLayout", false);
        SetBool(layoutManager, "applyOnStart", true);
        SetFloat(layoutManager, "upDownDepth.displayA.distanceMeters", 0.75f);
        SetFloat(layoutManager, "upDownDepth.displayA.horizontalAngleDegrees", 0f);
        SetFloat(layoutManager, "upDownDepth.displayA.verticalAngleDegrees", -13.2f);
        SetFloat(layoutManager, "upDownDepth.displayA.apparentWidthDegreesOverride", 37.5f);
        SetFloat(layoutManager, "upDownDepth.displayA.apparentHeightDegreesOverride", 21.09375f);
        SetFloat(layoutManager, "upDownDepth.displayB.distanceMeters", 2.25f);
        SetFloat(layoutManager, "upDownDepth.displayB.horizontalAngleDegrees", 0f);
        SetFloat(layoutManager, "upDownDepth.displayB.verticalAngleDegrees", 11.8f);
        SetFloat(layoutManager, "upDownDepth.displayB.apparentWidthDegreesOverride", 42.5f);
        SetFloat(layoutManager, "upDownDepth.displayB.apparentHeightDegreesOverride", 23.90625f);

        SetBool(tLabWebViewBridgeA, "enableOnStart", false);
        SetString(tLabWebViewBridgeA, "initialUrl", "about:blank");
        SetVector2Int(tLabWebViewBridgeA, "viewSize", new Vector2Int(960, 540));
        SetVector2Int(tLabWebViewBridgeA, "textureSize", new Vector2Int(1920, 1080));
        SetBool(tLabWebViewBridgeA, "matchDisplayAspect", true);
        SetInt(tLabWebViewBridgeA, "fps", 24);
        SetBool(tLabWebViewBridgeA, "limitTextureUpdatesToFps", true);
        SetEnum(tLabWebViewBridgeA, "captureMode", TLabCaptureModePreference.HardwareBuffer);
        SetBool(tLabWebViewBridgeA, "useBuiltInTLabKeyboard", false);
        SetBool(tLabWebViewBridgeA, "showDisplayKeyboard", true);
        SetBool(tLabWebViewBridgeA, "showKeyboardOnlyForTextInput", true);
        SetFloat(tLabWebViewBridgeA, "displayKeyboardHeightNormalized", 0.34f);
        SetInt(tLabWebViewBridgeA, "keyboardSortingOrder", 30);
        SetInt(tLabWebViewBridgeA, "cursorSortingOrderAboveKeyboard", 31);
        SetBool(tLabWebViewBridgeA, "loadBlankPageOnDisable", true);
        SetBool(tLabWebViewBridgeA, "destroyPrefabOnDisable", true);
        SetBool(tLabWebViewBridgeB, "enableOnStart", false);
        SetString(tLabWebViewBridgeB, "initialUrl", "https://www.youtube.com");
        SetVector2Int(tLabWebViewBridgeB, "viewSize", new Vector2Int(960, 540));
        SetVector2Int(tLabWebViewBridgeB, "textureSize", new Vector2Int(1920, 1080));
        SetBool(tLabWebViewBridgeB, "matchDisplayAspect", true);
        SetInt(tLabWebViewBridgeB, "fps", 24);
        SetBool(tLabWebViewBridgeB, "limitTextureUpdatesToFps", true);
        SetEnum(tLabWebViewBridgeB, "captureMode", TLabCaptureModePreference.HardwareBuffer);
        SetBool(tLabWebViewBridgeB, "useBuiltInTLabKeyboard", false);
        SetBool(tLabWebViewBridgeB, "showDisplayKeyboard", true);
        SetBool(tLabWebViewBridgeB, "showKeyboardOnlyForTextInput", true);
        SetFloat(tLabWebViewBridgeB, "displayKeyboardHeightNormalized", 0.34f);
        SetInt(tLabWebViewBridgeB, "keyboardSortingOrder", 30);
        SetInt(tLabWebViewBridgeB, "cursorSortingOrderAboveKeyboard", 31);
        SetBool(tLabWebViewBridgeB, "loadBlankPageOnDisable", true);
        SetBool(tLabWebViewBridgeB, "destroyPrefabOnDisable", true);

        SetObject(gazeProvider, "hmdCamera", hmdCamera);
        Transform eyeTrackingRaySource = EnsureChild(managers.transform, "EyeTracking_RaySource");
        SetObject(eyeTrackingRayAdapter, "hmdCamera", hmdCamera);
        SetObject(eyeTrackingRayAdapter, "raySource", eyeTrackingRaySource);
        SetObject(gazeProvider, "eyeTrackingAdapter", eyeTrackingRayAdapter);
        SetObject(gazeProvider, "eyeTrackingRaySource", eyeTrackingRaySource);
        SetEnum(gazeProvider, "gazeSource", GazeSource.EyeTracking);
        SetObject(inputManager, "debugInputProvider", debugInputProvider);
        SetBool(inputManager, "allowConditionToggle", true);

        SetObject(trajectoryLogger, "hmdCamera", hmdCamera);
        SetObject(trajectoryLogger, "rightControllerTransform", controllerRaySource);
        SetObject(trajectoryLogger, "inputManager", inputManager);
        SetObject(trajectoryLogger, "experimentManager", experimentManager);
        SetObject(trajectoryLogger, "gazeProvider", gazeProvider);
        SetObject(trajectoryLogger, "displayManager", displayManager);
        SetObject(trajectoryLogger, "virtualCursorController", cursorController);
        SetObject(trajectoryLogger, "focusPointingTaskManager", focusPointingTaskManager);
        SetObject(trajectoryLogger, "webViewSessionManager", webViewSessionManager);

        SetObject(experimentManager, "inputManager", inputManager);
        SetObject(experimentManager, "gazeProvider", gazeProvider);
        SetObject(experimentManager, "displayManager", displayManager);
        SetObject(experimentManager, "layoutManager", layoutManager);
        SetObject(experimentManager, "raycastPointer", raycastPointer);
        SetObject(experimentManager, "focusManager", focusManager);
        SetObject(experimentManager, "virtualCursorController", cursorController);
        SetObject(experimentManager, "gazeDisplayFocusManager", gazeDisplayFocusManager);
        SetObject(experimentManager, "debugInputProvider", debugInputProvider);
        SetObject(experimentManager, "logger", logger);
        SetEnum(experimentManager, "startingCondition", InteractionCondition.RaycastBaseline);
        SetBool(experimentManager, "applyStartingLayoutOnStart", true);
        SetBool(experimentManager, "allowLayoutSwitching", true);

        SetObject(focusManager, "inputManager", inputManager);
        SetObject(focusManager, "gazeProvider", gazeProvider);
        SetObject(focusManager, "displayManager", displayManager);
        SetObject(focusManager, "virtualCursorController", cursorController);
        SetObject(focusManager, "logger", logger);
        SetFloat(focusManager, "offDisplaySnapMaxAngleDegrees", 3f);
        SetFloat(focusManager, "offDisplaySwitchHysteresisDegrees", 0.5f);

        SetObject(gazeDisplayFocusManager, "inputManager", inputManager);
        SetObject(gazeDisplayFocusManager, "gazeProvider", gazeProvider);
        SetObject(gazeDisplayFocusManager, "displayManager", displayManager);
        SetObject(gazeDisplayFocusManager, "focusManager", focusManager);
        SetBool(gazeDisplayFocusManager, "highlightEnabled", true);
        SetBool(gazeDisplayFocusManager, "logStateChanges", true);

        SetObject(raycastPointer, "inputManager", inputManager);
        SetObject(raycastPointer, "displayManager", displayManager);
        SetObject(raycastPointer, "rightControllerTransform", controllerRaySource);
        SetBool(raycastPointer, "showRayLine", true);
        SetEnum(raycastPointer, "rayLengthLevel", RayVisualLengthLevel.Long);
        SetFloat(raycastPointer, "shortRayLength", 1.5f);
        SetFloat(raycastPointer, "mediumRayLength", 2.5f);
        SetFloat(raycastPointer, "longRayLength", 4f);
        SetColor(raycastPointer, "rayColor", new Color(0.4f, 0.8f, 1f, 1f));

        SetObject(cursorController, "inputManager", inputManager);
        SetObject(cursorController, "displayManager", displayManager);
        SetObject(cursorController, "webViewSessionManager", webViewSessionManager);
        SetFloat(cursorController, "cursorSpeedPixelsPerSecond", 220f);
        SetFloat(cursorController, "accelerationPixelsPerSecond", 0f);
        SetBool(cursorController, "allowStickCursorMovement", true);

        SetObject(scrollController, "inputManager", inputManager);
        SetObject(scrollController, "displayManager", displayManager);
        SetObject(scrollController, "raycastPointer", raycastPointer);
        SetObject(scrollController, "focusManager", focusManager);
        SetObject(scrollController, "virtualCursorController", cursorController);
        SetObject(scrollController, "clickDispatcher", clickDispatcher);
        SetObject(scrollController, "gazeProvider", gazeProvider);
        SetObject(scrollController, "logger", logger);
        SetObject(scrollController, "webViewSessionManager", webViewSessionManager);

        SetObject(clickDispatcher, "inputManager", inputManager);
        SetObject(clickDispatcher, "displayManager", displayManager);
        SetObject(clickDispatcher, "raycastPointer", raycastPointer);
        SetObject(clickDispatcher, "focusManager", focusManager);
        SetObject(clickDispatcher, "virtualCursorController", cursorController);
        SetObject(clickDispatcher, "gazeProvider", gazeProvider);
        SetObject(clickDispatcher, "focusPointingTaskManager", focusPointingTaskManager);
        SetObject(clickDispatcher, "webViewSessionManager", webViewSessionManager);
        SetObject(clickDispatcher, "vrTaskMenuManager", vrTaskMenuManager);
        SetObject(clickDispatcher, "logger", logger);
        SetBool(clickDispatcher, "enableWebViewPointerDrag", true);
        SetFloat(clickDispatcher, "webViewStickGestureDeadzone", 0.12f);
        SetFloat(clickDispatcher, "webViewStickGestureSpeedPixelsPerSecond", 260f);
        SetFloat(clickDispatcher, "webViewRayNeutralReleaseDelay", 0.12f);
        SetFloat(clickDispatcher, "webViewVerticalGestureEdgeMargin", 0.08f);
        SetFloat(clickDispatcher, "webViewVerticalGestureReentry", 0.28f);

        SetBool(vrTaskMenuManager, "assignMethodFromParticipantNumber", true);
        SetInt(vrTaskMenuManager, "participantAllocationSeed", 20260725);

        SetObject(focusPointingTaskManager, "inputManager", inputManager);
        SetObject(focusPointingTaskManager, "experimentManager", experimentManager);
        SetObject(focusPointingTaskManager, "displayManager", displayManager);
        SetObject(focusPointingTaskManager, "errorEvaluator", errorEvaluator);
        SetObject(focusPointingTaskManager, "logger", logger);
        SetObject(focusPointingTaskManager, "virtualCursorController", cursorController);
        SetObject(focusPointingTaskManager, "displayA", displayA.transform);
        SetObject(focusPointingTaskManager, "displayB", displayB.transform);
        SetEnum(focusPointingTaskManager, "selectedTask", T1PointingTask.TaskA_LeftRight);
        SetEnum(focusPointingTaskManager, "selectedLayout", DisplayLayoutPreset.LeftRight);
        SetEnum(focusPointingTaskManager, "taskOrder", T1TaskOrder.ABC);
        SetEnum(focusPointingTaskManager, "methodOrder", T1MethodOrder.RayFirst);
        SetBool(focusPointingTaskManager, "runFixedMainTaskSequence", true);
        SetFloat(focusPointingTaskManager, "mainBlockStartLockSeconds", 30f);
        SetBool(focusPointingTaskManager, "useLatest48TrialDesign", true);
        SetFloat(focusPointingTaskManager, "smallTargetSizeDegrees", 1.5f);
        SetFloat(focusPointingTaskManager, "largeTargetSizeDegrees", 3f);
        SetFloat(focusPointingTaskManager, "feedbackVolume", 0.8f);
        SetString(focusPointingTaskManager, "targetOrderResourcePath", "T1/target_orders_ABCDEFG");
        SetBool(focusPointingTaskManager, "rebuildTrialsOnStart", true);
        SetBool(focusPointingTaskManager, "randomizeTrialsWithinCondition", false);

        SetObject(webViewSessionManager, "inputManager", inputManager);
        SetObject(webViewSessionManager, "experimentManager", experimentManager);
        SetObject(webViewSessionManager, "displayManager", displayManager);
        SetObject(webViewSessionManager, "logger", logger);
        SetObject(webViewSessionManager, "virtualCursorController", cursorController);
        SetObject(webViewSessionManager, "raycastPointer", raycastPointer);
        SetObject(webViewSessionManager, "displayA", displayA);
        SetObject(webViewSessionManager, "displayB", displayB);
        SetObject(webViewSessionManager, "hmdCamera", hmdCamera);
        SetEnum(webViewSessionManager, "inputMode", WebViewInputMode.DirectScrollAndSeek);
        SetObject(webViewSessionManager, "targetTLabWebViewBridge", tLabWebViewBridgeB);
        SetBool(webViewSessionManager, "autoAddTLabBridgeToTargetDisplay", true);
        SetString(webViewSessionManager, "targetDisplayId", "Display_B_Back");
        SetString(webViewSessionManager, "initialUrl", "https://www.youtube.com");
        SetBool(webViewSessionManager, "enableSecondaryWebView", true);
        SetString(webViewSessionManager, "secondaryDisplayId", "Display_A_Front");
        SetString(webViewSessionManager, "secondaryInitialUrl", "http://133.87.151.83:5173/");
        SetEnum(webViewSessionManager, "selectedContentSet", T2ContentSet.ACampGear2024);
        SetString(webViewSessionManager, "youtubeUrlSetA", "https://www.youtube.com/watch?v=oCKnZl-XT1Y");
        SetString(webViewSessionManager, "practiceYoutubeUrl", "https://www.youtube.com/watch?v=HQRzNpPDk0k");
        SetString(webViewSessionManager, "practiceComparisonInitialUrl", "http://133.87.151.83:5173/?mode=practice");
        SetString(webViewSessionManager, "youtubeUrlSetB", "https://www.youtube.com/watch?v=wIHPxl6OPOc");
        SetBool(webViewSessionManager, "forceYoutubeUnmuted", true);
        SetFloat(webViewSessionManager, "youtubeForcedVolume", 1f);
        SetVector2(webViewSessionManager, "d2vPauseRangeSetASeconds", new Vector2(688f, 743f));
        SetVector2(webViewSessionManager, "d2vPauseRangeSetBSeconds", new Vector2(948f, 1011f));
        SetString(webViewSessionManager, "comparisonPageResourceSetA", "T2/t2_content_2024");
        SetString(webViewSessionManager, "comparisonPageResourceSetB", "");
        SetInt(webViewSessionManager, "minimumPracticeRounds", 1);
        SetFloat(webViewSessionManager, "totalTaskDurationSeconds", 720f);
        SetFloat(webViewSessionManager, "candidateConfirmationUnlockSeconds", 690f);
        SetFloat(webViewSessionManager, "practiceStageTimeoutSeconds", 0f);
        SetFloat(webViewSessionManager, "v2dStageTimeoutSeconds", 0f);
        SetFloat(webViewSessionManager, "d2vStageTimeoutSeconds", 0f);
        SetFloat(webViewSessionManager, "semiFreeStageTimeoutSeconds", 0f);
        SetFloat(webViewSessionManager, "finalizeStageTimeoutSeconds", 0f);
        SetFloat(webViewSessionManager, "practiceScrollThresholdPixels", 80f);
        SetBool(webViewSessionManager, "autoAddTLabBridgeToSecondaryDisplay", true);
        SetBool(webViewSessionManager, "disableOtherWebViewsOnStart", true);
        SetString(webViewSessionManager, "startGateDisplayId", "Display_B_Back");
        SetBool(webViewSessionManager, "requireStartButtonBeforeSession", true);
        SetFloat(webViewSessionManager, "startCountdownSeconds", 3f);
        SetBool(webViewSessionManager, "showInstructionPanelAboveBackDisplay", true);
        SetVector2(webViewSessionManager, "instructionPanelSizePixels", new Vector2(1600f, 180f));
        SetFloat(webViewSessionManager, "instructionPanelScale", 0.0009f);
        SetInt(webViewSessionManager, "instructionPanelPhaseFontSize", 30);
        SetInt(webViewSessionManager, "instructionPanelMessageFontSize", 42);
        SetFloat(webViewSessionManager, "instructionPanelVerticalGapMeters", 0.01f);

        SetObject(rayOcclusionLayoutProbe, "hmdCamera", hmdCamera);
        SetObject(rayOcclusionLayoutProbe, "d1BackDisplay", displayB);
        SetObject(rayOcclusionLayoutProbe, "d2FrontDisplay", displayA);
        SetFloat(rayOcclusionLayoutProbe, "h1", 1.90f);
        SetFloat(rayOcclusionLayoutProbe, "h2", 1.15f);
        SetFloat(rayOcclusionLayoutProbe, "eta2Degrees", 25f);
        SetFloat(rayOcclusionLayoutProbe, "handVerticalSign", -1f);
        SetFloat(rayOcclusionLayoutProbe, "h1Min", 1.60f);
        SetFloat(rayOcclusionLayoutProbe, "h1Max", 2.20f);
        SetFloat(rayOcclusionLayoutProbe, "h1Step", 0.10f);
        SetFloat(rayOcclusionLayoutProbe, "h2Min", 0.90f);
        SetFloat(rayOcclusionLayoutProbe, "h2Max", 1.80f);
        SetFloat(rayOcclusionLayoutProbe, "h2Step", 0.05f);
        SetFloat(rayOcclusionLayoutProbe, "eta2MinDegrees", 22.5f);
        SetFloat(rayOcclusionLayoutProbe, "eta2MaxDegrees", 45f);
        SetFloat(rayOcclusionLayoutProbe, "eta2StepDegrees", 2.5f);
        SetBool(rayOcclusionLayoutProbe, "applyCurrentLayoutOnStart", false);
        SetBool(rayOcclusionLayoutProbe, "logOnStart", false);
        SetBool(rayOcclusionLayoutProbe, "logHandSamples", true);
        SetBool(rayOcclusionLayoutProbe, "requireMinimumVisualVerticalGap", true);
        SetFloat(rayOcclusionLayoutProbe, "minVisualVerticalGapMeters", 0.08f);
        SetFloat(rayOcclusionLayoutProbe, "visualEyeSampleOffsetMeters", 0.04f);
        SetBool(rayOcclusionLayoutProbe, "showDebugOcclusionOverlay", true);
        SetBool(rayOcclusionLayoutProbe, "showVisualOcclusionOverlay", false);
        SetBool(rayOcclusionLayoutProbe, "showRayOcclusionOverlay", true);
        SetBool(rayOcclusionLayoutProbe, "showAmbiguousOverlay", true);
        SetInt(rayOcclusionLayoutProbe, "debugOverlaySamplesPerAxis", 31);
        SetBool(rayOcclusionLayoutProbe, "applyDisplaySize", true);
        SetBool(rayOcclusionLayoutProbe, "applyDisplayPose", true);
        SetBool(rayOcclusionLayoutProbe, "applyOnlyWhenNearTargetRatio", true);
        SetBool(rayOcclusionLayoutProbe, "requireD2BelowD1", true);

        SetObject(vrTaskMenuManager, "inputManager", inputManager);
        SetObject(vrTaskMenuManager, "experimentManager", experimentManager);
        SetObject(vrTaskMenuManager, "displayManager", displayManager);
        SetObject(vrTaskMenuManager, "focusPointingTaskManager", focusPointingTaskManager);
        SetObject(vrTaskMenuManager, "webViewSessionManager", webViewSessionManager);
        SetObject(vrTaskMenuManager, "raycastPointer", raycastPointer);
        SetObject(vrTaskMenuManager, "gazeProvider", gazeProvider);
        SetObject(vrTaskMenuManager, "gazeDisplayFocusManager", gazeDisplayFocusManager);
        SetObject(vrTaskMenuManager, "logger", logger);
        SetBool(logger, "requireParticipantContextBeforeOpen", true);
        SetBool(vrTaskMenuManager, "showLayoutButtons", true);
        SetBool(vrTaskMenuManager, "applySelectedLayout", true);

        SetObject(exitPanelController, "hmdCamera", hmdCamera);
        SetObject(exitPanelController, "leftControllerTransform", leftControllerRaySource);
        SetObject(exitPanelController, "inputManager", inputManager);
        SetObject(exitPanelController, "taskMenuManager", vrTaskMenuManager);
        SetObject(exitPanelController, "logger", logger);
        SetFloat(exitPanelController, "panelDistanceMeters", 1.2f);
        SetFloat(exitPanelController, "holdToExitSeconds", 0.6f);

        SetObject(debugVisualizer, "inputManager", inputManager);
        SetObject(debugVisualizer, "gazeProvider", gazeProvider);
        SetObject(debugVisualizer, "displayManager", displayManager);
        SetObject(debugVisualizer, "focusManager", focusManager);
        SetObject(debugVisualizer, "virtualCursorController", cursorController);
        SetObject(debugVisualizer, "clickDispatcher", clickDispatcher);
        SetObject(debugVisualizer, "scrollController", scrollController);
        SetObject(debugVisualizer, "experimentManager", experimentManager);
        SetObject(debugVisualizer, "debugInputProvider", debugInputProvider);
        SetObject(debugVisualizer, "focusPointingTaskManager", focusPointingTaskManager);
        SetObject(debugVisualizer, "raycastPointer", raycastPointer);
        SetObject(debugVisualizer, "controllerRaySource", controllerRaySource);
        SetEnum(debugVisualizer, "rayVisualizationMode", RayVisualizationMode.Hidden);
        SetFloat(debugVisualizer, "hitPointRadius", 0.01f);
        SetBool(debugVisualizer, "showWorldConditionLabel", true);
        SetString(debugVisualizer, "worldConditionLabelAnchorDisplayId", "Display_B_Back");
        SetVector2(debugVisualizer, "worldConditionLabelNormalizedAnchor", new Vector2(0.5f, 1.60f));
        SetBool(debugVisualizer, "showGazeRayOnlyWhileGripHeld", true);
        SetFloat(debugVisualizer, "gazeRayStartOffset", 0.35f);
        SetFloat(debugVisualizer, "gazeRayStartWidth", 0.0035f);
        SetFloat(debugVisualizer, "gazeRayEndWidth", 0.001f);

        SetObject(controlPanel, "inputManager", inputManager);
        SetObject(controlPanel, "experimentManager", experimentManager);
        SetObject(controlPanel, "gazeProvider", gazeProvider);
        SetObject(controlPanel, "debugVisualizer", debugVisualizer);
        SetObject(controlPanel, "virtualCursorController", cursorController);
        SetObject(controlPanel, "focusPointingTaskManager", focusPointingTaskManager);
        SetEnum(controlPanel, "condition", InteractionCondition.RaycastBaseline);
        SetEnum(controlPanel, "layout", DisplayLayoutPreset.UpDownDepth);
        SetBool(controlPanel, "applyLayout", false);
        SetEnum(controlPanel, "gazeSource", GazeSource.EyeTracking);
        SetEnum(controlPanel, "rayMode", RayVisualizationMode.Hidden);
        SetBool(controlPanel, "showDebugOverlay", true);
        SetBool(controlPanel, "showWorldConditionLabel", true);
        SetBool(controlPanel, "showGazeRayOnlyWhileGripHeld", true);
        SetBool(controlPanel, "allowStickCursorMovement", true);

        SetBool(focusPointingTaskManager, "autoStartOnPlay", false);
        SetBool(focusPointingTaskManager, "loopTrainingTrials", true);
        SetBool(focusPointingTaskManager, "returnToConditionSelectionAfterMainTask", true);

        layoutManager.ApplyLayout(DisplayLayoutPreset.UpDownDepth);
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

    private static Transform ResolveLeftControllerRaySource(Transform fallbackParent)
    {
        GameObject leftController = GameObject.Find("Left Controller");
        if (leftController != null)
        {
            return leftController.transform;
        }

        Transform debugRay = EnsureChild(fallbackParent, "Left_Controller_DebugRay");
        debugRay.localPosition = new Vector3(-0.25f, -0.15f, 0.05f);
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
            DisableComponentIfPresent(gameObject, "XRPokeInteractor");
            DisableComponentIfPresent(gameObject, "XRDirectInteractor");

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
        canvasTransform.gameObject.SetActive(true);
        Canvas canvas = GetOrAdd<Canvas>(canvasTransform.gameObject);
        canvasTransform = canvas.transform;
        GetOrAdd<CanvasScaler>(canvasTransform.gameObject);
        GetOrAdd<GraphicRaycaster>(canvasTransform.gameObject);

        RectTransform panel = EnsureRectChild(canvasTransform, "VisiblePanel");
        panel.gameObject.SetActive(true);
        Image panelImage = GetOrAdd<Image>(panel.gameObject);
        panelImage.color = panelColor;
        GetOrAdd<RectMask2D>(panel.gameObject);

        RectTransform title = EnsureRectChild(panel, "Label");
        Text titleText = GetOrAdd<Text>(title.gameObject);
        bool showLabel = !string.IsNullOrWhiteSpace(label);
        title.gameObject.SetActive(showLabel);
        titleText.text = showLabel ? label : string.Empty;
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.color = Color.white;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 42;
        titleText.raycastTarget = false;
        title.anchorMin = new Vector2(0f, 1f);
        title.anchorMax = new Vector2(1f, 1f);
        title.pivot = new Vector2(0.5f, 1f);
        title.offsetMin = new Vector2(24f, -76f);
        title.offsetMax = new Vector2(-24f, -16f);

        RectTransform content = EnsureRectChild(panel, "Pseudo2DContent");
        content.anchorMin = new Vector2(0.08f, 0.04f);
        content.anchorMax = new Vector2(0.92f, 0.94f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(0f, -260f);
        content.offsetMax = new Vector2(0f, 260f);

        Text contentText = GetOrAdd<Text>(content.gameObject);
        contentText.text =
            "Scroll task content preview\n\n" +
            "Use this panel only when validating scroll behavior.\n" +
            "Pointing trials hide this text and show only the target.\n\n" +
            "Line 01  Scrollable reading content\n" +
            "Line 02  The active input target receives scroll events\n" +
            "Line 03  In ExplicitDisplayFocus, focus decides the scroll target\n" +
            "Line 04  In RaycastBaseline, the ray hit decides the scroll target\n" +
            "Line 05  More content below the visible panel\n" +
            "Line 06  More content below the visible panel\n" +
            "Line 07  More content below the visible panel";
        contentText.alignment = TextAnchor.UpperCenter;
        contentText.color = new Color(1f, 1f, 1f, 0.86f);
        contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        contentText.fontSize = 28;

        RectTransform cursor = EnsureRectChild(canvasTransform, "Cursor");
        Image cursorImage = GetOrAdd<Image>(cursor.gameObject);
        cursorImage.color = Color.yellow;
        cursorImage.raycastTarget = false;

        Transform hitPlaneTransform = EnsureChild(displayObject.transform, "TransparentHitPlane");
        hitPlaneTransform.gameObject.SetActive(true);
        BoxCollider hitPlane = GetOrAdd<BoxCollider>(hitPlaneTransform.gameObject);

        surface.AssignParts(canvas, panel, hitPlane, cursor);
        surface.AssignScrollContent(content);
        surface.SetContentMode(DisplayContentMode.ConditionSelection);
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

    private static Component GetOrAddByTypeName(GameObject gameObject, string typeName)
    {
        Component existing = gameObject.GetComponent(typeName);
        if (existing != null)
        {
            return existing;
        }

        System.Type type = System.Type.GetType(typeName);
        if (type == null)
        {
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    break;
                }
            }
        }

        return type != null && typeof(Component).IsAssignableFrom(type)
            ? gameObject.AddComponent(type)
            : null;
    }

    private static void SetObject(Object target, string propertyName, Object value)
    {
        if (target == null)
        {
            return;
        }

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
        if (target == null)
        {
            return;
        }

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
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetFloat(Object target, string propertyName, float value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetInt(Object target, string propertyName, int value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetColor(Object target, string propertyName, Color value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.colorValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetVector2Int(Object target, string propertyName, Vector2Int value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2IntValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetVector2(Object target, string propertyName, Vector2 value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetString(Object target, string propertyName, string value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
