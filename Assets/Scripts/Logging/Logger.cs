using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// プロトタイプ全体のイベントCSVロガー。
/// フレームサンプル、クリック、スクロール、フォーカス、T1結果を同じ形式で追記できるようにする。
/// </summary>
public class Logger : MonoBehaviour
{
    [SerializeField] private bool writeCsv = true;
    [SerializeField] private string filePrefix = "prototype";
    [SerializeField] private float sampleIntervalSeconds = 0.25f;

    private StreamWriter writer;
    private StreamWriter gazeRayWriter;
    private float nextSampleTime;

    public bool IsOpen => writer != null;

    private void OnEnable()
    {
        Open();
    }

    private void OnDisable()
    {
        Close();
    }

    public bool ShouldSample()
    {
        if (Time.time < nextSampleTime)
        {
            return false;
        }

        nextSampleTime = Time.time + sampleIntervalSeconds;
        return true;
    }

    public void LogFrame(
        InteractionCondition condition,
        DisplayLayoutPreset layoutPreset,
        DisplayHit raycastHit,
        bool hasRaycastHit,
        DisplayHit gazeHit,
        bool hasGazeHit,
        DisplaySurface focusedDisplay,
        string cursorDisplayId,
        Vector2 cursorNormalized,
        bool hasCursor,
        Vector3 controllerRayOrigin,
        Vector3 controllerRayDirection)
    {
        if (!writeCsv || writer == null)
        {
            return;
        }

        writer.WriteLine(string.Join(",",
            Time.time.ToString("0.000", CultureInfo.InvariantCulture),
            "Frame",
            condition,
            layoutPreset,
            string.Empty,
            FormatDisplay(hasRaycastHit, raycastHit),
            FormatVector(hasRaycastHit ? raycastHit.Normalized : Vector2.zero),
            FormatDisplay(hasGazeHit, gazeHit),
            FormatVector(hasGazeHit ? gazeHit.Normalized : Vector2.zero),
            focusedDisplay != null ? focusedDisplay.name : "None",
            hasCursor ? cursorDisplayId : "None",
            hasCursor ? FormatVector(cursorNormalized) : string.Empty,
            "0.000",
            FormatVector3(controllerRayOrigin),
            FormatVector3(controllerRayDirection),
            string.Empty,
            string.Empty,
            "False"));
    }

    public void LogEvent(string eventName, InteractionCondition condition, string displayId, Vector2 normalized)
    {
        if (writeCsv && writer != null)
        {
            writer.WriteLine(string.Join(",",
                Time.time.ToString("0.000", CultureInfo.InvariantCulture),
                "Event",
                condition,
                eventName,
                string.Empty,
                displayId,
                FormatVector(normalized),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "0.000",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "False"));
        }

        Debug.Log($"[Logger] event={eventName}, condition={condition}, displayId={displayId}, normalized={FormatVector(normalized)}");
    }

    public void LogSyncMarker(string markerName, InteractionCondition condition, string details, float timestamp)
    {
        string safeMarkerName = string.IsNullOrWhiteSpace(markerName) ? "UNKNOWN" : markerName.Trim();
        if (writeCsv && writer != null)
        {
            writer.WriteLine(string.Join(",",
                timestamp.ToString("0.000", CultureInfo.InvariantCulture),
                "SyncMarker",
                condition,
                safeMarkerName,
                Escape(details),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "0.000",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "False"));
            writer.Flush();
        }

        Debug.Log($"SyncMarker, {safeMarkerName}, {timestamp.ToString("0.000", CultureInfo.InvariantCulture)}");
    }

    public void LogRaycastHit(InteractionCondition condition, string displayId, Vector2 normalized, Vector3 rayOrigin, Vector3 rayDirection)
    {
        WriteBaselineEvent("RaycastHit", condition, displayId, normalized, string.Empty, 0f, rayOrigin, rayDirection);
    }

    public void LogRaycastClick(InteractionCondition condition, string displayId, Vector2 normalized, Vector3 rayOrigin, Vector3 rayDirection)
    {
        WriteBaselineEvent(condition == InteractionCondition.GazeRay ? "click" : "Click", condition, displayId, normalized, string.Empty, 0f, rayOrigin, rayDirection);
    }

    public void LogRaycastScroll(InteractionCondition condition, string displayId, Vector2 normalized, float scrollAmount, Vector3 rayOrigin, Vector3 rayDirection)
    {
        WriteBaselineEvent("Scroll", condition, displayId, normalized, string.Empty, scrollAmount, rayOrigin, rayDirection);
    }

    public void LogExplicitCandidateUpdate(
        InteractionCondition condition,
        GazeSource gazeSource,
        FocusState focusState,
        string candidateDisplayIds,
        string focusedDisplayId,
        bool gazeOnDifferentDisplay,
        Vector3 gazeRayOrigin,
        Vector3 gazeRayDirection)
    {
        WriteExplicitEvent("GazeCandidates", condition, gazeSource, candidateDisplayIds, focusedDisplayId, Vector2.zero, focusedDisplayId, string.Empty, 0f, gazeOnDifferentDisplay, focusState, gazeRayOrigin, gazeRayDirection);
    }

    public void LogExplicitFocus(
        InteractionCondition condition,
        GazeSource gazeSource,
        string candidateDisplayIds,
        string focusedDisplayId,
        Vector2 normalized,
        bool gazeOnDifferentDisplay,
        Vector3 gazeRayOrigin,
        Vector3 gazeRayDirection)
    {
        WriteExplicitEvent("FocusConfirmed", condition, gazeSource, candidateDisplayIds, focusedDisplayId, normalized, focusedDisplayId, string.Empty, 0f, gazeOnDifferentDisplay, FocusState.FocusedLocked, gazeRayOrigin, gazeRayDirection);
    }

    public void LogExplicitCursorWarp(
        InteractionCondition condition,
        GazeSource gazeSource,
        string candidateDisplayIds,
        string focusedDisplayId,
        Vector2 normalized,
        bool gazeOnDifferentDisplay,
        Vector3 gazeRayOrigin,
        Vector3 gazeRayDirection)
    {
        WriteExplicitEvent("CursorWarp", condition, gazeSource, candidateDisplayIds, focusedDisplayId, normalized, focusedDisplayId, string.Empty, 0f, gazeOnDifferentDisplay, FocusState.FocusedLocked, gazeRayOrigin, gazeRayDirection);
    }

    public void LogExplicitClick(
        InteractionCondition condition,
        GazeSource gazeSource,
        string candidateDisplayIds,
        string focusedDisplayId,
        Vector2 normalized,
        bool gazeOnDifferentDisplay,
        Vector3 gazeRayOrigin,
        Vector3 gazeRayDirection)
    {
        WriteExplicitEvent("Click", condition, gazeSource, candidateDisplayIds, focusedDisplayId, normalized, focusedDisplayId, string.Empty, 0f, gazeOnDifferentDisplay, FocusState.FocusedLocked, gazeRayOrigin, gazeRayDirection);
    }

    public void LogExplicitScroll(
        InteractionCondition condition,
        GazeSource gazeSource,
        string candidateDisplayIds,
        string focusedDisplayId,
        Vector2 normalized,
        float scrollAmount,
        bool gazeOnDifferentDisplay,
        Vector3 gazeRayOrigin,
        Vector3 gazeRayDirection)
    {
        WriteExplicitEvent("Scroll", condition, gazeSource, candidateDisplayIds, focusedDisplayId, normalized, focusedDisplayId, string.Empty, scrollAmount, gazeOnDifferentDisplay, FocusState.FocusedLocked, gazeRayOrigin, gazeRayDirection);
    }

    public void LogFocusPointingTrial(FocusPointingTrialResult result)
    {
        if (writeCsv && writer != null)
        {
            writer.WriteLine(string.Join(",",
                result.ClickTime.ToString("0.000", CultureInfo.InvariantCulture),
                "TrialResult",
                result.Condition,
                result.LayoutPreset,
                string.Empty,
                result.ClickedDisplayId,
                FormatVector(result.ClickedNormalizedPosition),
                string.Empty,
                string.Empty,
                string.Empty,
                result.ClickedDisplayId,
                FormatVector(result.ClickedNormalizedPosition),
                "0.000",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "False",
                result.ParticipantId,
                result.SessionId,
                result.TrialIndex,
                result.TrialSetId,
                result.TrialIndexInSet,
                result.InputOccluded ? "inputOccluded" : "none",
                result.TargetDisplayId,
                FormatVector(result.TargetNormalizedPosition),
                result.TargetSizeNormalized.ToString("0.000", CultureInfo.InvariantCulture),
                result.ClickedDisplayId,
                FormatVector(result.ClickedNormalizedPosition),
                result.ResultType,
                result.IsCorrect,
                result.IsDisplayError,
                result.IsTargetError,
                result.TrialStartTime.ToString("0.000", CultureInfo.InvariantCulture),
                result.ClickTime.ToString("0.000", CultureInfo.InvariantCulture),
                result.CompletionTime.ToString("0.000", CultureInfo.InvariantCulture)));
        }

        Debug.Log($"[Logger] trialResult participant={result.ParticipantId}, session={result.SessionId}, trial={result.TrialIndex}, trialSetId={result.TrialSetId}, task={result.Task}, taskOrder={result.TaskOrder}, methodOrder={result.MethodOrder}, condition={result.Condition}, layout={result.LayoutPreset}, occlusion={(result.InputOccluded ? "inputOccluded" : "none")}, targetDisplay={result.TargetDisplayId}, target={FormatVector(result.TargetNormalizedPosition)}, size={result.TargetSizeNormalized:0.000}, clickedDisplay={result.ClickedDisplayId}, clicked={FormatVector(result.ClickedNormalizedPosition)}, result={result.ResultType}, correct={result.IsCorrect}, displayError={result.IsDisplayError}, targetError={result.IsTargetError}, completion={result.CompletionTime:0.000}, controllerMovementMeters={result.ControllerMovementMeters:0.000000}, controllerRotationDegrees={result.ControllerRotationDegrees:0.000}");
    }

    public void LogGazeRayFrame(
        bool gazeValid,
        string gazeHitDisplayId,
        string gazeSelectedDisplayId,
        DisplayHit controllerHit,
        bool pointerValid,
        Vector3 controllerRayOrigin,
        Vector3 controllerRayDirection,
        string penetratedDisplayIds,
        int displaySwitchCount,
        int triggerPressCount,
        string scrollTargetDisplayId,
        GazeInvalidPolicy invalidPolicy,
        float switchDwellSeconds)
    {
        if (!writeCsv || gazeRayWriter == null)
        {
            return;
        }

        Vector3 intersection = pointerValid ? controllerHit.WorldPosition : Vector3.zero;
        Vector2 normalized = pointerValid ? controllerHit.Normalized : Vector2.zero;
        int penetratedCount = string.IsNullOrEmpty(penetratedDisplayIds) || penetratedDisplayIds == "None"
            ? 0
            : penetratedDisplayIds.Split('|').Length;
        gazeRayWriter.WriteLine(string.Join(",",
            Time.time.ToString("0.000", CultureInfo.InvariantCulture),
            "GazeRay",
            gazeValid,
            Escape(gazeHitDisplayId),
            Escape(gazeSelectedDisplayId),
            FormatVector3(controllerRayOrigin),
            FormatVector3(controllerRayDirection),
            pointerValid ? Escape(controllerHit.DisplayId) : "None",
            pointerValid ? FormatVector3(intersection) : string.Empty,
            pointerValid ? normalized.x.ToString("0.000", CultureInfo.InvariantCulture) : string.Empty,
            pointerValid ? normalized.y.ToString("0.000", CultureInfo.InvariantCulture) : string.Empty,
            pointerValid,
            displaySwitchCount,
            displaySwitchCount,
            triggerPressCount,
            Escape(scrollTargetDisplayId),
            Escape(penetratedDisplayIds),
            penetratedCount,
            invalidPolicy,
            switchDwellSeconds.ToString("0.000", CultureInfo.InvariantCulture)));
    }

    private void Open()
    {
        if (!writeCsv || writer != null)
        {
            return;
        }

        string directory = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(directory);

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string path = Path.Combine(directory, $"{filePrefix}_{timestamp}.csv");
        writer = new StreamWriter(path);
        writer.WriteLine("time,rowType,condition,layoutPresetOrEvent,details,displayId,normalized,gazeDisplay,gazeNormalized,focusedDisplay,cursorDisplay,cursorNormalized,scrollAmount,rayOrigin,rayDirection,candidateDisplayIds,focusState,gazeOnDifferentDisplay,participantId,sessionId,trialIndex,trialSetId,trialIndexInSet,occlusion_type,targetDisplayId,targetNormalized,targetSize,clickedDisplayId,clickedNormalized,resultType,isCorrect,displayError,targetError,trialStartTime,clickTime,completionTime");
        Debug.Log($"[Logger] CSV logging to {path}");

        string gazeRayPath = Path.Combine(directory, $"gazeray_{timestamp}.csv");
        gazeRayWriter = new StreamWriter(gazeRayPath);
        gazeRayWriter.WriteLine("timestamp,method,gazeValid,gazeHitDisplayId,gazeSelectedDisplayId,controllerRayOrigin,controllerRayDirection,controllerRayTargetDisplayId,controllerRayIntersectionPosition,controllerRayIntersectionNormalizedX,controllerRayIntersectionNormalizedY,pointerValid,displaySwitchCount,gazeDisplaySwitchCount,triggerPressCount,scrollTargetDisplayId,penetratedDisplayId,penetratedDisplayCount,gazeInvalidPolicy,gazeDisplaySwitchDwellSeconds");
    }

    private void Close()
    {
        if (writer == null)
        {
            return;
        }

        writer.Flush();
        writer.Dispose();
        writer = null;
        gazeRayWriter?.Flush();
        gazeRayWriter?.Dispose();
        gazeRayWriter = null;
    }

    private static string FormatDisplay(bool hasHit, DisplayHit hit)
    {
        return hasHit ? hit.DisplayId : "None";
    }

    private static string FormatVector(Vector2 value)
    {
        return $"{value.x.ToString("0.000", CultureInfo.InvariantCulture)}:{value.y.ToString("0.000", CultureInfo.InvariantCulture)}";
    }

    private void WriteBaselineEvent(string eventName, InteractionCondition condition, string displayId, Vector2 normalized, string details, float scrollAmount, Vector3 rayOrigin, Vector3 rayDirection)
    {
        if (writeCsv && writer != null)
        {
            writer.WriteLine(string.Join(",",
                Time.time.ToString("0.000", CultureInfo.InvariantCulture),
                "Event",
                condition,
                eventName,
                Escape(details),
                displayId,
                FormatVector(normalized),
                string.Empty,
                string.Empty,
                string.Empty,
                displayId,
                FormatVector(normalized),
                scrollAmount.ToString("0.000", CultureInfo.InvariantCulture),
                FormatVector3(rayOrigin),
                FormatVector3(rayDirection),
                string.Empty,
                string.Empty,
                "False"));
        }

        Debug.Log($"[Logger] event={eventName}, condition={condition}, displayId={displayId}, normalized={FormatVector(normalized)}, scrollAmount={scrollAmount:0.000}, rayOrigin={FormatVector3(rayOrigin)}, rayDirection={FormatVector3(rayDirection)}, details={details}");
    }

    private void WriteExplicitEvent(
        string eventName,
        InteractionCondition condition,
        GazeSource gazeSource,
        string candidateDisplayIds,
        string displayId,
        Vector2 normalized,
        string focusedDisplayId,
        string details,
        float scrollAmount,
        bool gazeOnDifferentDisplay,
        FocusState focusState,
        Vector3 gazeRayOrigin,
        Vector3 gazeRayDirection)
    {
        if (writeCsv && writer != null)
        {
            writer.WriteLine(string.Join(",",
                Time.time.ToString("0.000", CultureInfo.InvariantCulture),
                "Event",
                condition,
                eventName,
                Escape(details),
                displayId,
                FormatVector(normalized),
                string.Empty,
                string.Empty,
                focusedDisplayId,
                eventName == "GazeCandidates" ? "None" : displayId,
                eventName == "GazeCandidates" ? string.Empty : FormatVector(normalized),
                scrollAmount.ToString("0.000", CultureInfo.InvariantCulture),
                FormatVector3(gazeRayOrigin),
                FormatVector3(gazeRayDirection),
                Escape(candidateDisplayIds),
                focusState,
                gazeOnDifferentDisplay));
        }

        Debug.Log($"[Logger] event={eventName}, condition={condition}, gazeSource={gazeSource}, candidates={candidateDisplayIds}, focusedDisplay={focusedDisplayId}, displayId={displayId}, normalized={FormatVector(normalized)}, scrollAmount={scrollAmount:0.000}, gazeOnDifferentDisplay={gazeOnDifferentDisplay}, gazeRayOrigin={FormatVector3(gazeRayOrigin)}, gazeRayDirection={FormatVector3(gazeRayDirection)}, details={details}");
    }

    private static string FormatVector3(Vector3 value)
    {
        return $"{value.x.ToString("0.000", CultureInfo.InvariantCulture)}:{value.y.ToString("0.000", CultureInfo.InvariantCulture)}:{value.z.ToString("0.000", CultureInfo.InvariantCulture)}";
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace(",", ";");
    }
}
