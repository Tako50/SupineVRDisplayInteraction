using System;
using System.Globalization;
using System.Text;

/// <summary>
/// Builds experiment CSV names that can be identified without opening them.
/// </summary>
public static class ExperimentDataFileNaming
{
    public const string CurrentSchemaVersion = "2";

    public static string BuildCsvFileName(
        string participantId,
        InteractionCondition condition,
        string taskCode,
        string dataType,
        string timestamp,
        int allocationSeed = ParticipantMethodAssignment.DefaultAllocationSeed)
    {
        string participant = Sanitize(participantId, "P000");
        string assignment = BuildAssignmentPart(participant, condition, allocationSeed);
        string task = Sanitize(taskCode, "TASK");
        string type = Sanitize(dataType, "data");
        string stamp = Sanitize(timestamp, "timestamp");
        return $"{participant}_{assignment}_{task}_{type}_{stamp}.csv";
    }

    public static string BuildCsvFileName(
        string participantId,
        InteractionCondition condition,
        string taskCode,
        string dataType,
        string timestamp,
        string phaseCode,
        string runId,
        int allocationSeed = ParticipantMethodAssignment.DefaultAllocationSeed)
    {
        string participant = Sanitize(participantId, "P000");
        string assignment = BuildAssignmentPart(participant, condition, allocationSeed);
        string task = Sanitize(taskCode, "TASK");
        string phase = Sanitize(phaseCode, "Phase");
        string run = Sanitize(runId, "Run");
        string type = Sanitize(dataType, "data");
        string stamp = Sanitize(timestamp, "timestamp");
        return $"{participant}_{assignment}_{task}_{phase}_{run}_{type}_{stamp}.csv";
    }

    public static string CreateRunId()
    {
        return DateTime.UtcNow.ToString("'R'yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
    }

    public static string BuildSessionId(
        string participantId,
        string taskCode,
        string phaseCode,
        string runId)
    {
        return string.Join("-",
            Sanitize(participantId, "P000"),
            Sanitize(taskCode, "TASK"),
            Sanitize(phaseCode, "Phase"),
            Sanitize(runId, "Run"));
    }

    public static string BuildAllocationCode(
        string participantId,
        InteractionCondition condition,
        int allocationSeed = ParticipantMethodAssignment.DefaultAllocationSeed)
    {
        if (TryParseParticipantNumber(participantId, out int participantNumber))
        {
            ParticipantMethodAssignment.Assignment assignment =
                ParticipantMethodAssignment.GetAssignment(participantNumber, allocationSeed);
            if (assignment.Condition == condition)
            {
                return assignment.AllocationCode;
            }
        }

        return ParticipantMethodAssignment.GetMethodCode(condition);
    }

    private static string BuildAssignmentPart(
        string participantId,
        InteractionCondition condition,
        int allocationSeed)
    {
        if (TryParseParticipantNumber(participantId, out int participantNumber))
        {
            ParticipantMethodAssignment.Assignment assignment =
                ParticipantMethodAssignment.GetAssignment(participantNumber, allocationSeed);
            if (assignment.Condition == condition)
            {
                return assignment.ShortAllocationCode;
            }
        }

        return ParticipantMethodAssignment.GetMethodCode(condition);
    }

    private static bool TryParseParticipantNumber(string participantId, out int participantNumber)
    {
        participantNumber = 0;
        if (string.IsNullOrWhiteSpace(participantId))
        {
            return false;
        }

        StringBuilder digits = new StringBuilder();
        for (int i = 0; i < participantId.Length; i++)
        {
            if (char.IsDigit(participantId[i]))
            {
                digits.Append(participantId[i]);
            }
        }

        return digits.Length > 0
            && int.TryParse(
                digits.ToString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out participantNumber)
            && participantNumber > 0;
    }

    private static string Sanitize(string value, string fallback)
    {
        string source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        StringBuilder sanitized = new StringBuilder(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            sanitized.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_'
                ? c
                : '_');
        }

        return sanitized.Length > 0 ? sanitized.ToString() : fallback;
    }
}
