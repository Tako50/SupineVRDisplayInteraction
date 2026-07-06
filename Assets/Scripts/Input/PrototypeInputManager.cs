using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

[DisallowMultipleComponent]
/// <summary>
/// VR実機入力とEditorデバッグ入力を1つの入力状態にまとめる。
/// 実験中は条件をロックし、タスク途中で条件が変わらないようにする。
/// </summary>
public class PrototypeInputManager : MonoBehaviour
{
    public static PrototypeInputManager Instance { get; private set; }

    [SerializeField] private InteractionCondition currentCondition = InteractionCondition.RaycastBaseline;
    [SerializeField] private bool allowConditionToggle = true;
    [SerializeField] private float stickDeadzone = 0.08f;
    [Header("XR Trigger")]
    [Range(0f, 1f)]
    [Tooltip("Analog value at which a released XR trigger becomes held.")]
    [SerializeField] private float triggerPressThreshold = 0.45f;
    [Range(0f, 1f)]
    [Tooltip("Lower analog value required to release a held XR trigger. This hysteresis prevents edge flicker.")]
    [SerializeField] private float triggerReleaseThreshold = 0.35f;

    [Header("References")]
    [SerializeField] private EditorDebugInputProvider debugInputProvider;

    private readonly List<XRInputDevice> rightHandDevices = new List<XRInputDevice>();
    private readonly List<XRInputDevice> leftHandDevices = new List<XRInputDevice>();
    private bool previousPrimaryButton;
    private bool previousSecondaryButton;
    private bool previousGripButton;
    private bool previousTriggerButton;
    private bool previousLeftTriggerButton;
    private bool previousStickClickButton;

    public InteractionCondition CurrentCondition => currentCondition;
    public Vector2 Stick { get; private set; }
    public bool SubmitPressed { get; private set; }
    public bool SubmitHeld { get; private set; }
    public bool SubmitReleased { get; private set; }
    public bool GripPressed { get; private set; }
    public bool GripHeld { get; private set; }
    public bool TriggerPressed { get; private set; }
    public bool TriggerHeld { get; private set; }
    public bool TriggerReleased { get; private set; }
    public float TriggerValue { get; private set; }
    public bool StickClickPressed { get; private set; }
    public bool StickClickHeld { get; private set; }
    public bool StickClickReleased { get; private set; }
    public bool LeftTriggerPressed { get; private set; }
    public bool LeftTriggerHeld { get; private set; }
    public bool LeftTriggerReleased { get; private set; }
    public float LeftTriggerValue { get; private set; }
    public bool SecondaryButtonPressed { get; private set; }
    public bool ConditionTogglePressed { get; private set; }
    public bool ResetFocusPressed => debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.ResetFocusPressed;
    public bool DebugInputEnabled => debugInputProvider != null && debugInputProvider.IsEnabled;
    public bool IsConditionLocked { get; private set; }

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        ReadInputs();

        if (allowConditionToggle
            && ConditionTogglePressed)
        {
            ToggleCondition();
        }
    }

    public void SetCondition(InteractionCondition condition)
    {
        SetCondition(condition, false);
    }

    public void SetCondition(InteractionCondition condition, bool force)
    {
        if (currentCondition == condition)
        {
            return;
        }

        if (IsConditionLocked && !force)
        {
            Debug.Log($"[PrototypeInput] condition locked. ignored={condition}, current={currentCondition}");
            return;
        }

        currentCondition = condition;
        Debug.Log($"[PrototypeInput] condition={currentCondition}");
    }

    public void LockCondition(InteractionCondition condition)
    {
        currentCondition = condition;
        IsConditionLocked = true;
        Debug.Log($"[PrototypeInput] condition locked={currentCondition}");
    }

    public void UnlockCondition()
    {
        if (!IsConditionLocked)
        {
            return;
        }

        IsConditionLocked = false;
        Debug.Log("[PrototypeInput] condition unlocked");
    }

    private void ToggleCondition()
    {
        SetCondition(currentCondition == InteractionCondition.RaycastBaseline
            ? InteractionCondition.ExplicitDisplayFocus
            : InteractionCondition.RaycastBaseline);
    }

    private void ReadInputs()
    {
        // まずXRの右手コントローラ入力を読む。取得できないEditor実行時は既定値のまま進む。
        Vector2 xrStick = Vector2.zero;
        bool xrPrimary = false;
        bool xrSecondary = false;
        bool xrGrip = false;
        bool xrStickClick = false;
        bool xrTriggerButton = false;
        float xrTriggerValue = 0f;
        bool xrLeftTriggerButton = false;
        float xrLeftTriggerValue = 0f;

        XRInputDevice rightHand = GetRightHandDevice();
        if (rightHand.isValid)
        {
            rightHand.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out xrStick);
            rightHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out xrPrimary);
            rightHand.TryGetFeatureValue(XRCommonUsages.secondaryButton, out xrSecondary);
            rightHand.TryGetFeatureValue(XRCommonUsages.gripButton, out xrGrip);
            rightHand.TryGetFeatureValue(XRCommonUsages.primary2DAxisClick, out xrStickClick);
            rightHand.TryGetFeatureValue(XRCommonUsages.triggerButton, out xrTriggerButton);
            rightHand.TryGetFeatureValue(XRCommonUsages.trigger, out xrTriggerValue);
        }

        XRInputDevice leftHand = GetLeftHandDevice();
        if (leftHand.isValid)
        {
            leftHand.TryGetFeatureValue(XRCommonUsages.triggerButton, out xrLeftTriggerButton);
            leftHand.TryGetFeatureValue(XRCommonUsages.trigger, out xrLeftTriggerValue);
        }

        Vector2 debugStick = debugInputProvider != null && debugInputProvider.IsEnabled
            ? debugInputProvider.Stick
            : Vector2.zero;

        // Editor検証ではキーボード入力を優先できるよう、入力の大きい方を採用する。
        Stick = debugStick.sqrMagnitude > xrStick.sqrMagnitude ? debugStick : xrStick;
        if (Stick.magnitude < stickDeadzone)
        {
            Stick = Vector2.zero;
        }

        bool debugSubmitPressed = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.SubmitPressed;
        bool debugSubmitHeld = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.SubmitHeld;
        bool debugSubmitReleased = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.SubmitReleased;
        bool debugGrip = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.GripPressed;
        bool debugGripHeld = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.GripHeld;
        bool debugTriggerHeld = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.TriggerHeld;
        bool debugTriggerPressed = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.TriggerPressed;
        bool debugTriggerReleased = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.TriggerReleased;
        bool debugStickClickPressed = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.StickClickPressed;
        bool debugStickClickHeld = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.StickClickHeld;
        bool debugStickClickReleased = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.StickClickReleased;
        bool debugLeftTriggerHeld = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.ExitHeld;
        bool debugLeftTriggerPressed = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.ExitPressed;
        bool debugLeftTriggerReleased = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.ExitReleased;
        bool debugConditionToggle = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.ToggleConditionPressed;
        bool xrTriggerHeld = ResolveTriggerHeld(xrTriggerButton, xrTriggerValue, previousTriggerButton);
        bool xrLeftTriggerHeld = ResolveTriggerHeld(xrLeftTriggerButton, xrLeftTriggerValue, previousLeftTriggerButton);

        TriggerValue = Mathf.Max(debugTriggerHeld ? 1f : 0f, xrTriggerButton ? 1f : Mathf.Clamp01(xrTriggerValue));
        LeftTriggerValue = Mathf.Max(debugLeftTriggerHeld ? 1f : 0f, xrLeftTriggerButton ? 1f : Mathf.Clamp01(xrLeftTriggerValue));

        // Pressed/Released はこのフレームだけtrueになるイベントとして扱う。
        SubmitPressed = debugSubmitPressed || (xrPrimary && !previousPrimaryButton);
        SubmitHeld = debugSubmitHeld || xrPrimary;
        SubmitReleased = debugSubmitReleased || (!xrPrimary && previousPrimaryButton);
        GripPressed = debugGrip || (xrGrip && !previousGripButton);
        GripHeld = debugGripHeld || xrGrip;
        TriggerHeld = debugTriggerHeld || xrTriggerHeld;
        TriggerPressed = debugTriggerPressed || (xrTriggerHeld && !previousTriggerButton);
        TriggerReleased = debugTriggerReleased || (!xrTriggerHeld && previousTriggerButton);
        StickClickPressed = debugStickClickPressed || (xrStickClick && !previousStickClickButton);
        StickClickHeld = debugStickClickHeld || xrStickClick;
        StickClickReleased = debugStickClickReleased || (!xrStickClick && previousStickClickButton);
        LeftTriggerHeld = debugLeftTriggerHeld || xrLeftTriggerHeld;
        LeftTriggerPressed = debugLeftTriggerPressed || (xrLeftTriggerHeld && !previousLeftTriggerButton);
        LeftTriggerReleased = debugLeftTriggerReleased || (!xrLeftTriggerHeld && previousLeftTriggerButton);
        SecondaryButtonPressed = debugConditionToggle || (xrSecondary && !previousSecondaryButton);
        ConditionTogglePressed = SecondaryButtonPressed;

        previousPrimaryButton = xrPrimary;
        previousSecondaryButton = xrSecondary;
        previousGripButton = xrGrip;
        previousTriggerButton = xrTriggerHeld;
        previousLeftTriggerButton = xrLeftTriggerHeld;
        previousStickClickButton = xrStickClick;
    }

    private bool ResolveTriggerHeld(bool triggerButton, float triggerValue, bool wasHeld)
    {
        float pressThreshold = Mathf.Clamp01(triggerPressThreshold);
        float releaseThreshold = Mathf.Min(pressThreshold, Mathf.Clamp01(triggerReleaseThreshold));
        float threshold = wasHeld ? releaseThreshold : pressThreshold;
        return triggerButton || triggerValue >= threshold;
    }

    public void SendLeftHapticImpulse(float amplitude, float duration)
    {
        XRInputDevice leftHand = GetLeftHandDevice();
        if (leftHand.isValid)
        {
            leftHand.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0f, duration));
        }
    }

    private void ResolveReferences()
    {
        if (debugInputProvider == null)
        {
            debugInputProvider = FindObjectOfType<EditorDebugInputProvider>();
        }
    }

    private XRInputDevice GetRightHandDevice()
    {
        rightHandDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            rightHandDevices);

        return rightHandDevices.Count > 0 ? rightHandDevices[0] : default;
    }

    private XRInputDevice GetLeftHandDevice()
    {
        leftHandDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
            leftHandDevices);

        return leftHandDevices.Count > 0 ? leftHandDevices[0] : default;
    }

}
