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
    [SerializeField] private EditorDebugInputProvider debugInputProvider;

    private readonly List<XRInputDevice> rightHandDevices = new List<XRInputDevice>();
    private bool previousPrimaryButton;
    private bool previousSecondaryButton;
    private bool previousGripButton;
    private bool previousTriggerButton;

    public InteractionCondition CurrentCondition => currentCondition;
    public Vector2 Stick { get; private set; }
    public bool SubmitPressed { get; private set; }
    public bool GripPressed { get; private set; }
    public bool GripHeld { get; private set; }
    public bool TriggerPressed { get; private set; }
    public bool TriggerHeld { get; private set; }
    public bool TriggerReleased { get; private set; }
    public bool ConditionTogglePressed { get; private set; }
    public bool RayVisualizationTogglePressed { get; private set; }
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
        bool xrTriggerButton = false;
        float xrTriggerValue = 0f;

        XRInputDevice rightHand = GetRightHandDevice();
        if (rightHand.isValid)
        {
            rightHand.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out xrStick);
            rightHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out xrPrimary);
            rightHand.TryGetFeatureValue(XRCommonUsages.secondaryButton, out xrSecondary);
            rightHand.TryGetFeatureValue(XRCommonUsages.gripButton, out xrGrip);
            rightHand.TryGetFeatureValue(XRCommonUsages.triggerButton, out xrTriggerButton);
            rightHand.TryGetFeatureValue(XRCommonUsages.trigger, out xrTriggerValue);
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

        bool debugSubmit = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.SubmitPressed;
        bool debugGrip = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.GripPressed;
        bool debugGripHeld = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.GripHeld;
        bool debugTriggerHeld = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.TriggerHeld;
        bool debugTriggerPressed = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.TriggerPressed;
        bool debugTriggerReleased = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.TriggerReleased;
        bool debugConditionToggle = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.ToggleConditionPressed;
        bool xrTriggerHeld = xrTriggerButton || xrTriggerValue > 0.5f;

        // Pressed/Released はこのフレームだけtrueになるイベントとして扱う。
        SubmitPressed = debugSubmit || (xrPrimary && !previousPrimaryButton);
        GripPressed = debugGrip || (xrGrip && !previousGripButton);
        GripHeld = debugGripHeld || xrGrip;
        TriggerHeld = debugTriggerHeld || xrTriggerHeld;
        TriggerPressed = debugTriggerPressed || (xrTriggerHeld && !previousTriggerButton);
        TriggerReleased = debugTriggerReleased || (!xrTriggerHeld && previousTriggerButton);
        ConditionTogglePressed = debugConditionToggle || (xrSecondary && !previousSecondaryButton);
        RayVisualizationTogglePressed = false;

        previousPrimaryButton = xrPrimary;
        previousSecondaryButton = xrSecondary;
        previousGripButton = xrGrip;
        previousTriggerButton = xrTriggerHeld;
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

}
