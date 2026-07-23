using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// Records the HMD, right-controller, eye-gaze, and active cursor trajectory at 60 Hz
/// while either T1 or T2 is running. The resulting samples are intended for offline
/// movement and fixation analysis rather than runtime behavior.
/// </summary>
public class TrajectoryLogger : MonoBehaviour
{
    private const double SampleIntervalSeconds = 1.0 / 60.0;
    private const double FlushIntervalSeconds = 1.0;

    [SerializeField] private Camera hmdCamera;
    [SerializeField] private Transform rightControllerTransform;
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private FocusPointingTaskManager focusPointingTaskManager;
    [SerializeField] private WebViewSessionManager webViewSessionManager;

    private StreamWriter writer;
    private readonly StringBuilder lineBuilder = new StringBuilder(1024);
    private double nextSampleTime;
    private double nextFlushTime;
    private long sampleIndex;
    private bool wasExperimentTaskRunning;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        bool taskRunning = IsExperimentTaskRunning();
        if (!taskRunning)
        {
            if (wasExperimentTaskRunning && writer != null)
            {
                writer.Flush();
            }

            wasExperimentTaskRunning = false;
            return;
        }

        wasExperimentTaskRunning = true;

        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextSampleTime)
        {
            return;
        }

        if (nextSampleTime <= 0.0 || now - nextSampleTime > SampleIntervalSeconds * 4.0)
        {
            nextSampleTime = now;
        }

        nextSampleTime += SampleIntervalSeconds;
        EnsureWriter();
        WriteSample(now);

        if (now >= nextFlushTime)
        {
            writer.Flush();
            nextFlushTime = now + FlushIntervalSeconds;
        }
    }

    private void OnDisable()
    {
        CloseWriter();
    }

    private void OnDestroy()
    {
        CloseWriter();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && writer != null)
        {
            writer.Flush();
        }
    }

    private bool IsExperimentTaskRunning()
    {
        bool t1Running = focusPointingTaskManager != null
            && focusPointingTaskManager.IsTaskRunning;
        bool t2Running = webViewSessionManager != null
            && webViewSessionManager.IsTaskActive;
        return t1Running || t2Running;
    }

    private void EnsureWriter()
    {
        if (writer != null)
        {
            return;
        }

        string directory = Path.Combine(Application.persistentDataPath, "Logs", "Trajectory");
        Directory.CreateDirectory(directory);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string path = Path.Combine(directory, $"trajectory_{stamp}.csv");
        writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        writer.WriteLine(
            "timestamp,sampleIndex,participantId,sessionId,taskType,taskPhase,condition,layout,t1Task,trialIndex,globalTrialIndex,trialSetId,trialIndexInSet," +
            "hmdPosX,hmdPosY,hmdPosZ,hmdRotX,hmdRotY,hmdRotZ,hmdRotW," +
            "controllerPosX,controllerPosY,controllerPosZ,controllerRotX,controllerRotY,controllerRotZ,controllerRotW," +
            "eyeTrackingValid,gazeOriginX,gazeOriginY,gazeOriginZ,gazeDirectionX,gazeDirectionY,gazeDirectionZ," +
            "gazeDisplayId,gazeNormalizedX,gazeNormalizedY,gazeWorldX,gazeWorldY,gazeWorldZ," +
            "cursorDisplayId,cursorNormalizedX,cursorNormalizedY,triggerHeld,triggerValue,gripHeld,stickX,stickY");
        nextFlushTime = Time.realtimeSinceStartupAsDouble + FlushIntervalSeconds;
        Debug.Log($"[TrajectoryLogger] 60 Hz trajectory log started: {path}");
    }

    private void WriteSample(double timestamp)
    {
        Transform hmd = hmdCamera != null ? hmdCamera.transform : null;
        Vector3 hmdPosition = hmd != null ? hmd.position : Vector3.zero;
        Quaternion hmdRotation = hmd != null ? hmd.rotation : Quaternion.identity;
        Vector3 controllerPosition = rightControllerTransform != null
            ? rightControllerTransform.position
            : Vector3.zero;
        Quaternion controllerRotation = rightControllerTransform != null
            ? rightControllerTransform.rotation
            : Quaternion.identity;

        Ray gazeRay = gazeProvider != null ? gazeProvider.GetGazeRay() : default;
        bool eyeTrackingValid = gazeProvider != null
            && gazeProvider.CurrentGazeSource == GazeSource.EyeTracking;
        DisplayHit gazeHit = default;
        bool hasGazeHit = eyeTrackingValid
            && displayManager != null
            && displayManager.TryGetForemostHit(gazeRay, out gazeHit);

        string cursorDisplayId = string.Empty;
        Vector2 cursorNormalized = Vector2.zero;
        bool hasCursor = TryGetCursor(out cursorDisplayId, out cursorNormalized);

        bool isT1 = focusPointingTaskManager != null
            && focusPointingTaskManager.IsTaskRunning;
        string participantId = isT1
            ? focusPointingTaskManager.ParticipantId
            : webViewSessionManager != null ? webViewSessionManager.ParticipantId : string.Empty;
        string sessionId = isT1
            ? focusPointingTaskManager.SessionId
            : webViewSessionManager != null ? webViewSessionManager.SessionId : string.Empty;
        string taskType = isT1 ? "T1" : "T2";
        string taskPhase = isT1
            ? focusPointingTaskManager.CurrentPhase.ToString()
            : webViewSessionManager != null ? webViewSessionManager.CurrentPhase.ToString() : string.Empty;

        lineBuilder.Clear();
        Append(timestamp.ToString("0.000000", CultureInfo.InvariantCulture));
        Append(sampleIndex++.ToString(CultureInfo.InvariantCulture));
        AppendCsv(participantId);
        AppendCsv(sessionId);
        Append(taskType);
        Append(taskPhase);
        Append(inputManager != null ? inputManager.CurrentCondition.ToString() : string.Empty);
        Append(experimentManager != null ? experimentManager.CurrentLayout.ToString() : string.Empty);
        Append(isT1 ? focusPointingTaskManager.SelectedTask.ToString() : string.Empty);
        Append(isT1 ? focusPointingTaskManager.CurrentTrialIndex.ToString(CultureInfo.InvariantCulture) : string.Empty);
        Append(isT1 ? focusPointingTaskManager.CurrentGlobalTrialIndex.ToString(CultureInfo.InvariantCulture) : string.Empty);
        Append(isT1 ? focusPointingTaskManager.CurrentTrialSetId.ToString(CultureInfo.InvariantCulture) : string.Empty);
        Append(isT1 ? focusPointingTaskManager.CurrentTrialIndexInSet.ToString(CultureInfo.InvariantCulture) : string.Empty);
        AppendVector3(hmdPosition);
        AppendQuaternion(hmdRotation);
        AppendVector3(controllerPosition);
        AppendQuaternion(controllerRotation);
        Append(eyeTrackingValid ? "True" : "False");
        AppendVector3OrEmpty(gazeRay.origin, eyeTrackingValid);
        AppendVector3OrEmpty(gazeRay.direction, eyeTrackingValid);
        Append(hasGazeHit ? gazeHit.DisplayId : string.Empty);
        AppendVector2OrEmpty(hasGazeHit ? gazeHit.Normalized : Vector2.zero, hasGazeHit);
        AppendVector3OrEmpty(hasGazeHit ? gazeHit.WorldPosition : Vector3.zero, hasGazeHit);
        Append(hasCursor ? cursorDisplayId : string.Empty);
        AppendVector2OrEmpty(cursorNormalized, hasCursor);
        Append(inputManager != null && inputManager.TriggerHeld ? "True" : "False");
        Append(inputManager != null
            ? inputManager.TriggerValue.ToString("0.000000", CultureInfo.InvariantCulture)
            : string.Empty);
        Append(inputManager != null && inputManager.GripHeld ? "True" : "False");
        Append(inputManager != null
            ? inputManager.Stick.x.ToString("0.000000", CultureInfo.InvariantCulture)
            : string.Empty);
        AppendLast(inputManager != null
            ? inputManager.Stick.y.ToString("0.000000", CultureInfo.InvariantCulture)
            : string.Empty);
        writer.WriteLine(lineBuilder.ToString());
    }

    private bool TryGetCursor(out string displayId, out Vector2 normalized)
    {
        displayId = string.Empty;
        normalized = Vector2.zero;
        if (inputManager == null || displayManager == null)
        {
            return false;
        }

        if ((inputManager.CurrentCondition == InteractionCondition.RaycastBaseline
                || inputManager.CurrentCondition == InteractionCondition.GazeRay)
            && displayManager.HasCurrentRaycastHit)
        {
            DisplayHit hit = displayManager.CurrentRaycastHit;
            displayId = hit.DisplayId;
            normalized = hit.Normalized;
            return true;
        }

        if (inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus
            && displayManager.FocusedDisplay != null
            && virtualCursorController != null)
        {
            displayId = displayManager.FocusedDisplay.name;
            normalized = virtualCursorController.NormalizedPosition;
            return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (hmdCamera == null) hmdCamera = Camera.main;
        if (inputManager == null) inputManager = PrototypeInputManager.Instance != null
            ? PrototypeInputManager.Instance : FindObjectOfType<PrototypeInputManager>();
        if (experimentManager == null) experimentManager = FindObjectOfType<ExperimentManager>();
        if (gazeProvider == null) gazeProvider = FindObjectOfType<GazeProvider>();
        if (displayManager == null) displayManager = DisplayManager.Instance != null
            ? DisplayManager.Instance : FindObjectOfType<DisplayManager>();
        if (virtualCursorController == null) virtualCursorController = FindObjectOfType<VirtualCursorController>();
        if (focusPointingTaskManager == null) focusPointingTaskManager = FindObjectOfType<FocusPointingTaskManager>();
        if (webViewSessionManager == null) webViewSessionManager = FindObjectOfType<WebViewSessionManager>();
    }

    private void CloseWriter()
    {
        if (writer == null)
        {
            return;
        }

        writer.Flush();
        writer.Dispose();
        writer = null;
    }

    private void Append(string value)
    {
        lineBuilder.Append(value).Append(',');
    }

    private void AppendLast(string value)
    {
        lineBuilder.Append(value);
    }

    private void AppendCsv(string value)
    {
        string safe = value ?? string.Empty;
        lineBuilder.Append('"').Append(safe.Replace("\"", "\"\"")).Append("\",");
    }

    private void AppendVector2OrEmpty(Vector2 value, bool available)
    {
        if (!available)
        {
            Append(string.Empty);
            Append(string.Empty);
            return;
        }

        Append(value.x.ToString("0.000000", CultureInfo.InvariantCulture));
        Append(value.y.ToString("0.000000", CultureInfo.InvariantCulture));
    }

    private void AppendVector3(Vector3 value)
    {
        Append(value.x.ToString("0.000000", CultureInfo.InvariantCulture));
        Append(value.y.ToString("0.000000", CultureInfo.InvariantCulture));
        Append(value.z.ToString("0.000000", CultureInfo.InvariantCulture));
    }

    private void AppendVector3OrEmpty(Vector3 value, bool available)
    {
        if (!available)
        {
            Append(string.Empty);
            Append(string.Empty);
            Append(string.Empty);
            return;
        }

        AppendVector3(value);
    }

    private void AppendQuaternion(Quaternion value)
    {
        Append(value.x.ToString("0.000000", CultureInfo.InvariantCulture));
        Append(value.y.ToString("0.000000", CultureInfo.InvariantCulture));
        Append(value.z.ToString("0.000000", CultureInfo.InvariantCulture));
        Append(value.w.ToString("0.000000", CultureInfo.InvariantCulture));
    }
}
