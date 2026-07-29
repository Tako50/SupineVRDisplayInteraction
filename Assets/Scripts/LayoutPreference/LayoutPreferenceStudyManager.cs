using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class LayoutPreferenceStudyManager : MonoBehaviour
{
    [Header("Participant")]
    [SerializeField] private string participantId = "P01";
    [SerializeField] private string sessionId = "S01";
    [SerializeField] private bool randomizeOrder;
    [SerializeField] private bool useParticipantIdAsSeed = true;

    [Header("References")]
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private Transform displaysRoot;
    [SerializeField] private DisplaySurface displayA;
    [SerializeField] private DisplaySurface displayB;
    [SerializeField] private LayoutPreferenceLogger logger;

    [Header("Display Size")]
    [SerializeField] private float apparentWidthDegrees = DisplayGeometry.DefaultApparentWidthDegrees;
    [SerializeField] private float apparentHeightDegrees = DisplayGeometry.DefaultApparentHeightDegrees;
    [SerializeField] private bool preserveDisplayAspectRatio = true;
    [SerializeField] private Vector2 displayAspectRatio = DisplayGeometry.DefaultAspectRatio;
    [SerializeField] private Vector2 canvasPixelSize = DisplayGeometry.DefaultCanvasPixelSize;

    [Header("Placement")]
    [SerializeField] private Vector3 layoutCenterOffsetMeters = Vector3.zero;

    [Header("Layouts")]
    [SerializeField] private List<LayoutPreferenceLayout> layouts = new List<LayoutPreferenceLayout>();

    [Header("World UI")]
    [SerializeField] private Transform instructionCanvasRoot;
    [SerializeField] private Vector3 instructionLocalOffset = new Vector3(0f, 0.62f, 0.85f);
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text conditionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text statusText;

    [Header("Controls")]
    [SerializeField] private bool useXrInput = true;
    [SerializeField] private bool useInputSystemActions = true;
    [SerializeField] private bool useOvrInputFallback;
    [SerializeField] private bool allowKeyboardInput = true;
    [SerializeField] private bool menuHoldRecenters;
    [SerializeField] private float menuHoldSeconds = 0.75f;

    private readonly List<XRInputDevice> controllerDevices = new List<XRInputDevice>();
    private readonly List<int> conditionOrder = new List<int>();

    private int currentPresentedIndex;
    private bool previousPrimaryButton;
    private bool previousSecondaryButton;
    private bool previousRecenterButton;
    private float menuHoldTimer;
    private bool menuHoldTriggered;
    private float conditionEnterTime;
    private bool currentConditionEntered;
    private DisplayAnchorFrame lastAnchor;
    private OvrInputReader ovrInputReader;

#if ENABLE_INPUT_SYSTEM
    private InputAction nextAction;
    private InputAction previousAction;
    private InputAction recenterAction;
#endif

    private void Reset()
    {
        EnsureDefaultLayouts();
    }

    private void OnValidate()
    {
        EnsureDefaultLayouts();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureDefaultLayouts();
        BuildConditionOrder();
        ovrInputReader = new OvrInputReader();
        CreateInputSystemActions();
    }

    private void OnEnable()
    {
        EnableInputSystemActions();
    }

    private void OnDisable()
    {
        DisableInputSystemActions();
    }

    private void Start()
    {
        ShowCondition(0, true);
    }

    private void Update()
    {
        ReadButtons(out bool nextPressed, out bool previousPressed, out bool recenterPressed);

        if (recenterPressed)
        {
            RecenterCurrentCondition();
        }

        if (nextPressed)
        {
            GoNext();
        }
        else if (previousPressed)
        {
            GoPrevious();
        }
    }

    public void RecenterCurrentCondition()
    {
        if (conditionOrder.Count == 0)
        {
            return;
        }

        ApplyCurrentLayout();
        LogCurrentCondition("Recenter", Time.time, 0f);
        Debug.Log("[LayoutPreferenceStudyManager] Recentered current layout from the current HMD pose.");
    }

    private void GoNext()
    {
        float exitTime = Time.time;
        LogCurrentCondition("Exit", exitTime, exitTime - conditionEnterTime);

        ShowCondition((currentPresentedIndex + 1) % conditionOrder.Count, true);
    }

    private void GoPrevious()
    {
        if (currentPresentedIndex <= 0)
        {
            RecenterCurrentCondition();
            return;
        }

        float exitTime = Time.time;
        LogCurrentCondition("Exit", exitTime, exitTime - conditionEnterTime);
        ShowCondition(currentPresentedIndex - 1, true);
    }

    private void ShowCondition(int presentedIndex, bool logEnter)
    {
        if (conditionOrder.Count == 0)
        {
            return;
        }

        currentPresentedIndex = Mathf.Clamp(presentedIndex, 0, conditionOrder.Count - 1);
        conditionEnterTime = Time.time;
        currentConditionEntered = true;

        LayoutPreferenceLayout layout = GetCurrentLayout();
        SetDisplaysVisible(true);
        ApplyCurrentLayout();
        if (layout != null)
        {
            SetDisplayLabel(displayA, layout.DisplayALabel);
            SetDisplayLabel(displayB, layout.DisplayBLabel);
        }
        UpdateWorldUi();

        if (logEnter)
        {
            LogCurrentCondition("Enter", conditionEnterTime, 0f);
        }
    }

    private void ApplyCurrentLayout()
    {
        LayoutPreferenceLayout layout = GetCurrentLayout();
        if (layout == null)
        {
            return;
        }

        lastAnchor = CaptureHmdAnchor();
        Vector3 conditionCenterOffset = layoutCenterOffsetMeters + layout.ConditionCenterOffsetMeters;
        ApplyDisplay(displayA, layout.DisplayA, lastAnchor, conditionCenterOffset);
        ApplyDisplay(displayB, layout.DisplayB, lastAnchor, conditionCenterOffset);
        PositionInstructionCanvas(lastAnchor);
    }

    private void ApplyDisplay(
        DisplaySurface surface,
        LayoutPreferenceDisplayPlacement placement,
        DisplayAnchorFrame anchor,
        Vector3 conditionCenterOffset)
    {
        if (surface == null || placement == null)
        {
            return;
        }

        DisplayGeometry.ComputeDisplayPose(
            anchor,
            placement.DistanceMeters,
            placement.HorizontalAngleDegrees,
            placement.VerticalAngleDegrees,
            placement.RotationOffsetDegrees,
            conditionCenterOffset,
            out Vector3 position,
            out Quaternion rotation);

        surface.transform.SetParent(displaysRoot, true);
        surface.transform.SetPositionAndRotation(position, rotation);
        surface.transform.localScale = Vector3.one;

        Vector2 sizeMeters = DisplayGeometry.ComputePhysicalSize(
            placement.DistanceMeters,
            apparentWidthDegrees,
            apparentHeightDegrees,
            preserveDisplayAspectRatio,
            displayAspectRatio);
        surface.SetSize(sizeMeters, DisplayGeometry.ValidPixelSize(canvasPixelSize));
        surface.SetContentMode(DisplayContentMode.ConditionSelection);
        if (surface.Cursor != null)
        {
            surface.Cursor.gameObject.SetActive(false);
        }

        if (surface.TransparentHitPlane != null)
        {
            surface.TransparentHitPlane.enabled = false;
        }
    }

    private void PositionInstructionCanvas(DisplayAnchorFrame anchor)
    {
        if (instructionCanvasRoot == null)
        {
            return;
        }

        Vector3 position = anchor.TransformOffset(instructionLocalOffset);

        instructionCanvasRoot.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(position - anchor.Position, anchor.Up));
    }

    private void UpdateWorldUi()
    {
        LayoutPreferenceLayout layout = GetCurrentLayout();
        if (layout == null)
        {
            return;
        }

        SetText(titleText, string.Empty);
        SetText(conditionText, layout.ConditionName);
        SetText(progressText, string.Empty);
        SetText(instructionText, string.Empty);
        SetText(statusText, string.Empty);
    }

    private void SetDisplaysVisible(bool visible)
    {
        if (displayA != null)
        {
            displayA.gameObject.SetActive(visible);
        }

        if (displayB != null)
        {
            displayB.gameObject.SetActive(visible);
        }
    }

    private static void SetDisplayLabel(DisplaySurface surface, string label)
    {
        if (surface == null)
        {
            return;
        }

        TMP_Text[] texts = surface.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == "DisplayLabel")
            {
                texts[i].text = label;
                return;
            }
        }
    }

    private void LogCurrentCondition(string eventType, float eventTime, float duration)
    {
        if (!currentConditionEntered || logger == null || conditionOrder.Count == 0)
        {
            return;
        }

        LayoutPreferenceLayout layout = GetCurrentLayout();
        if (layout == null)
        {
            return;
        }

        logger.LogEvent(new LayoutPreferenceLogRow
        {
            Timestamp = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
            ParticipantId = participantId,
            SessionId = sessionId,
            EventType = eventType,
            PresentedIndex = currentPresentedIndex + 1,
            ConditionName = layout.ConditionName,
            ConditionOrder = BuildConditionOrderString(),
            TimeSinceSceneStart = Time.time,
            ConditionEnterTime = conditionEnterTime,
            ConditionExitTime = eventType == "Exit" ? eventTime : 0f,
            Duration = duration,
            DisplayAPosition = displayA != null ? displayA.transform.position : Vector3.zero,
            DisplayBPosition = displayB != null ? displayB.transform.position : Vector3.zero,
            DisplayARotation = displayA != null ? displayA.transform.rotation : Quaternion.identity,
            DisplayBRotation = displayB != null ? displayB.transform.rotation : Quaternion.identity,
            DisplayASizeMeters = displayA != null ? displayA.PhysicalSizeMeters : Vector2.zero,
            DisplayBSizeMeters = displayB != null ? displayB.PhysicalSizeMeters : Vector2.zero,
            HmdAnchorPosition = lastAnchor.Position,
            HmdAnchorForward = lastAnchor.Forward,
            HmdAnchorUp = lastAnchor.Up
        });
    }

    private DisplayAnchorFrame CaptureHmdAnchor()
    {
        ResolveReferences();
        Transform anchorTransform = hmdCamera != null ? hmdCamera.transform : transform;
        return DisplayAnchorFrame.FromTransform(anchorTransform);
    }

    private void ReadButtons(out bool nextPressed, out bool previousPressed, out bool recenterPressed)
    {
        bool primary = false;
        bool primaryReleased = false;
        bool secondary = false;
        bool menu = false;
        bool recenterButton = false;

        if (useXrInput)
        {
            controllerDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, controllerDevices);
            for (int i = 0; i < controllerDevices.Count; i++)
            {
                if (!controllerDevices[i].isValid)
                {
                    continue;
                }

                primary |= controllerDevices[i].TryGetFeatureValue(XRCommonUsages.primaryButton, out bool anyPrimary)
                    && anyPrimary;
                secondary |= controllerDevices[i].TryGetFeatureValue(XRCommonUsages.secondaryButton, out bool anySecondary)
                    && anySecondary;
                menu |= controllerDevices[i].TryGetFeatureValue(XRCommonUsages.menuButton, out bool anyMenu)
                    && anyMenu;
                recenterButton |= controllerDevices[i].TryGetFeatureValue(XRCommonUsages.primary2DAxisClick, out bool anyAxisClick)
                    && anyAxisClick;
            }
        }

        if (useOvrInputFallback)
        {
            if (ovrInputReader == null)
            {
                ovrInputReader = new OvrInputReader();
            }

            primaryReleased |= ovrInputReader.GetUp("One");
            secondary |= ovrInputReader.GetDown("Two");
            recenterButton |= ovrInputReader.GetDown("PrimaryThumbstick");
        }

        nextPressed = primaryReleased || (!primary && previousPrimaryButton);
        previousPressed = secondary && !previousSecondaryButton;
        recenterPressed = recenterButton && !previousRecenterButton;

        if (useInputSystemActions)
        {
#if ENABLE_INPUT_SYSTEM
            nextPressed |= WasInputActionReleased(nextAction);
            previousPressed |= WasInputActionPressed(previousAction);
            recenterPressed |= WasInputActionPressed(recenterAction);
#endif
        }

        if (menuHoldRecenters)
        {
            if (menu)
            {
                menuHoldTimer += Time.deltaTime;
                if (!menuHoldTriggered && menuHoldTimer >= menuHoldSeconds)
                {
                    recenterPressed = true;
                    menuHoldTriggered = true;
                }
            }
            else
            {
                menuHoldTimer = 0f;
                menuHoldTriggered = false;
            }
        }

        if (allowKeyboardInput)
        {
            nextPressed |= GetKeyUp(KeyCode.Space)
                || GetKeyUp(KeyCode.RightArrow)
                || GetKeyUp(KeyCode.JoystickButton0);
            previousPressed |= GetKeyDown(KeyCode.Backspace)
                || GetKeyDown(KeyCode.LeftArrow)
                || GetKeyDown(KeyCode.JoystickButton1);
            recenterPressed |= GetKeyDown(KeyCode.R)
                || GetKeyDown(KeyCode.JoystickButton7);
        }

        previousPrimaryButton = primary;
        previousSecondaryButton = secondary;
        previousRecenterButton = recenterButton;
    }

    private void ResolveReferences()
    {
        if (hmdCamera == null)
        {
            hmdCamera = Camera.main;
        }

        if (displaysRoot == null && displayA != null)
        {
            displaysRoot = displayA.transform.parent;
        }

        if (logger == null)
        {
            logger = FindObjectOfType<LayoutPreferenceLogger>();
        }
    }

    private void CreateInputSystemActions()
    {
#if ENABLE_INPUT_SYSTEM
        if (nextAction != null)
        {
            return;
        }

        nextAction = new InputAction("LayoutPreferenceNext", InputActionType.Button);
        nextAction.AddBinding("<XRController>{RightHand}/primaryButton");
        nextAction.AddBinding("<XRController>/primaryButton");
        nextAction.AddBinding("<OculusTouchController>{RightHand}/primaryButton");
        nextAction.AddBinding("<Keyboard>/space");
        nextAction.AddBinding("<Keyboard>/rightArrow");

        previousAction = new InputAction("LayoutPreferencePrevious", InputActionType.Button);
        previousAction.AddBinding("<XRController>{RightHand}/secondaryButton");
        previousAction.AddBinding("<XRController>/secondaryButton");
        previousAction.AddBinding("<OculusTouchController>{RightHand}/secondaryButton");
        previousAction.AddBinding("<Keyboard>/backspace");
        previousAction.AddBinding("<Keyboard>/leftArrow");

        recenterAction = new InputAction("LayoutPreferenceRecenter", InputActionType.Button);
        recenterAction.AddBinding("<XRController>{RightHand}/primary2DAxisClick");
        recenterAction.AddBinding("<XRController>{RightHand}/{Primary2DAxisClick}");
        recenterAction.AddBinding("<XRController>/primary2DAxisClick");
        recenterAction.AddBinding("<XRController>/{Primary2DAxisClick}");
        recenterAction.AddBinding("<OculusTouchController>{RightHand}/primary2DAxisClick");
        recenterAction.AddBinding("<Keyboard>/r");
#endif
    }

    private void EnableInputSystemActions()
    {
#if ENABLE_INPUT_SYSTEM
        CreateInputSystemActions();
        nextAction?.Enable();
        previousAction?.Enable();
        recenterAction?.Enable();
#endif
    }

    private void DisableInputSystemActions()
    {
#if ENABLE_INPUT_SYSTEM
        nextAction?.Disable();
        previousAction?.Disable();
        recenterAction?.Disable();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool WasInputActionPressed(InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }

    private static bool WasInputActionReleased(InputAction action)
    {
        return action != null && action.WasReleasedThisFrame();
    }

    private static bool IsInputActionPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }
#else
    private static bool WasInputActionPressed(object action)
    {
        return false;
    }

    private static bool WasInputActionReleased(object action)
    {
        return false;
    }

    private static bool IsInputActionPressed(object action)
    {
        return false;
    }
#endif

    private static bool GetKeyDown(KeyCode keyCode)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        switch (keyCode)
        {
            case KeyCode.Space:
                return keyboard.spaceKey.wasPressedThisFrame;
            case KeyCode.RightArrow:
                return keyboard.rightArrowKey.wasPressedThisFrame;
            case KeyCode.Backspace:
                return keyboard.backspaceKey.wasPressedThisFrame;
            case KeyCode.LeftArrow:
                return keyboard.leftArrowKey.wasPressedThisFrame;
            case KeyCode.R:
                return keyboard.rKey.wasPressedThisFrame;
            default:
                return false;
        }
#else
        return UnityEngine.Input.GetKeyDown(keyCode);
#endif
    }

    private static bool GetKeyUp(KeyCode keyCode)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        switch (keyCode)
        {
            case KeyCode.Space:
                return keyboard.spaceKey.wasReleasedThisFrame;
            case KeyCode.RightArrow:
                return keyboard.rightArrowKey.wasReleasedThisFrame;
            default:
                return false;
        }
#else
        return UnityEngine.Input.GetKeyUp(keyCode);
#endif
    }

    private void EnsureDefaultLayouts()
    {
        if (layouts != null && layouts.Count > 0)
        {
            for (int i = 0; i < layouts.Count; i++)
            {
                layouts[i]?.MigrateLegacyMetersToAnglesIfNeeded();
            }

            return;
        }

        layouts = new List<LayoutPreferenceLayout>(LayoutPreferenceLayout.CreateDefaultLayouts());
    }

    private void BuildConditionOrder()
    {
        conditionOrder.Clear();
        for (int i = 0; i < layouts.Count; i++)
        {
            conditionOrder.Add(i);
        }

        if (!randomizeOrder)
        {
            return;
        }

        int seed = useParticipantIdAsSeed ? BuildStableSeed(participantId) : Environment.TickCount;
        System.Random random = new System.Random(seed);
        for (int i = conditionOrder.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int tmp = conditionOrder[i];
            conditionOrder[i] = conditionOrder[j];
            conditionOrder[j] = tmp;
        }
    }

    private string BuildConditionOrderString()
    {
        List<string> names = new List<string>();
        for (int i = 0; i < conditionOrder.Count; i++)
        {
            int layoutIndex = conditionOrder[i];
            if (layoutIndex >= 0 && layoutIndex < layouts.Count)
            {
                names.Add(layouts[layoutIndex].ConditionName);
            }
        }

        return string.Join("|", names);
    }

    private LayoutPreferenceLayout GetCurrentLayout()
    {
        if (conditionOrder.Count == 0 || layouts.Count == 0)
        {
            return null;
        }

        int layoutIndex = conditionOrder[Mathf.Clamp(currentPresentedIndex, 0, conditionOrder.Count - 1)];
        return layoutIndex >= 0 && layoutIndex < layouts.Count ? layouts[layoutIndex] : null;
    }

    private static int BuildStableSeed(string seedText)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < seedText.Length; i++)
            {
                hash = hash * 31 + seedText[i];
            }

            return hash;
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private sealed class OvrInputReader
    {
        private readonly Type ovrInputType;
        private readonly Type buttonType;
        private readonly Type controllerType;
        private readonly MethodInfo getDownMethod;
        private readonly MethodInfo getUpMethod;
        private readonly MethodInfo getMethod;
        private readonly object controller;

        public OvrInputReader()
        {
            ovrInputType = FindType("OVRInput");
            if (ovrInputType == null)
            {
                return;
            }

            buttonType = ovrInputType.GetNestedType("Button");
            controllerType = ovrInputType.GetNestedType("Controller");
            if (buttonType == null || controllerType == null)
            {
                return;
            }

            controller = ParseEnum(controllerType, "RTouch") ?? ParseEnum(controllerType, "Touch");
            getDownMethod = FindButtonMethod("GetDown");
            getUpMethod = FindButtonMethod("GetUp");
            getMethod = FindButtonMethod("Get");
        }

        public bool GetDown(string buttonName)
        {
            return InvokeButtonMethod(getDownMethod, buttonName);
        }

        public bool GetUp(string buttonName)
        {
            return InvokeButtonMethod(getUpMethod, buttonName);
        }

        public bool Get(string buttonName)
        {
            return InvokeButtonMethod(getMethod, buttonName);
        }

        private bool InvokeButtonMethod(MethodInfo method, string buttonName)
        {
            if (method == null || controller == null)
            {
                return false;
            }

            object button = ParseEnum(buttonType, buttonName);
            if (button == null)
            {
                return false;
            }

            try
            {
                return (bool)method.Invoke(null, new[] { button, controller });
            }
            catch
            {
                return false;
            }
        }

        private MethodInfo FindButtonMethod(string methodName)
        {
            MethodInfo[] methods = ovrInputType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != methodName)
                {
                    continue;
                }

                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length == 2
                    && parameters[0].ParameterType == buttonType
                    && parameters[1].ParameterType == controllerType)
                {
                    return methods[i];
                }
            }

            return null;
        }

        private static object ParseEnum(Type enumType, string name)
        {
            try
            {
                return Enum.Parse(enumType, name);
            }
            catch
            {
                return null;
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
    }
}
