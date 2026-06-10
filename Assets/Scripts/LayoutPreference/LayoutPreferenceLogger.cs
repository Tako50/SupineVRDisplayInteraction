using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class LayoutPreferenceLogger : MonoBehaviour
{
    [SerializeField] private bool writeCsv = true;
    [SerializeField] private string filePrefix = "layout_preference";

    private StreamWriter writer;

    public bool IsOpen => writer != null;

    private void OnEnable()
    {
        Open();
    }

    private void OnDisable()
    {
        Close();
    }

    public void LogEvent(LayoutPreferenceLogRow row)
    {
        if (!writeCsv || writer == null)
        {
            return;
        }

        writer.WriteLine(string.Join(",",
            Escape(row.Timestamp),
            Escape(row.ParticipantId),
            Escape(row.SessionId),
            Escape(row.EventType),
            FormatInt(row.PresentedIndex),
            Escape(row.ConditionName),
            Escape(row.ConditionOrder),
            FormatFloat(row.TimeSinceSceneStart),
            FormatFloat(row.ConditionEnterTime),
            FormatFloat(row.ConditionExitTime),
            FormatFloat(row.Duration),
            Escape(FormatVector3(row.DisplayAPosition)),
            Escape(FormatVector3(row.DisplayBPosition)),
            Escape(FormatQuaternion(row.DisplayARotation)),
            Escape(FormatQuaternion(row.DisplayBRotation)),
            Escape(FormatVector2(row.DisplayASizeMeters)),
            Escape(FormatVector2(row.DisplayBSizeMeters)),
            Escape(FormatVector3(row.HmdAnchorPosition)),
            Escape(FormatVector3(row.HmdAnchorForward)),
            Escape(FormatVector3(row.HmdAnchorUp))));
        writer.Flush();
    }

    private void Open()
    {
        if (!writeCsv || writer != null)
        {
            return;
        }

        string directory = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(directory);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string path = Path.Combine(directory, $"{filePrefix}_{timestamp}.csv");
        writer = new StreamWriter(path);
        writer.WriteLine("timestamp,participantId,sessionId,eventType,presentedIndex,conditionName,conditionOrder,timeSinceSceneStart,conditionEnterTime,conditionExitTime,duration,displayA_position,displayB_position,displayA_rotation,displayB_rotation,displayA_sizeMeters,displayB_sizeMeters,hmdAnchorPosition,hmdAnchorForward,hmdAnchorUp");
        Debug.Log($"[LayoutPreferenceLogger] CSV logging to {path}");
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
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string FormatInt(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static string FormatVector2(Vector2 value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.000};{1:0.000}",
            value.x,
            value.y);
    }

    private static string FormatVector3(Vector3 value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.000};{1:0.000};{2:0.000}",
            value.x,
            value.y,
            value.z);
    }

    private static string FormatQuaternion(Quaternion value)
    {
        Vector3 euler = value.eulerAngles;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.000};{1:0.000};{2:0.000}",
            euler.x,
            euler.y,
            euler.z);
    }
}

public struct LayoutPreferenceLogRow
{
    public string Timestamp;
    public string ParticipantId;
    public string SessionId;
    public string EventType;
    public int PresentedIndex;
    public string ConditionName;
    public string ConditionOrder;
    public float TimeSinceSceneStart;
    public float ConditionEnterTime;
    public float ConditionExitTime;
    public float Duration;
    public Vector3 DisplayAPosition;
    public Vector3 DisplayBPosition;
    public Quaternion DisplayARotation;
    public Quaternion DisplayBRotation;
    public Vector2 DisplayASizeMeters;
    public Vector2 DisplayBSizeMeters;
    public Vector3 HmdAnchorPosition;
    public Vector3 HmdAnchorForward;
    public Vector3 HmdAnchorUp;
}
