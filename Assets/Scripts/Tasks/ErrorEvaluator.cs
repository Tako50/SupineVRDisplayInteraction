using UnityEngine;

public enum FocusPointingResultType
{
    Correct,
    DisplayError,
    TargetError,
    Miss
}

public struct FocusPointingEvaluation
{
    public FocusPointingResultType ResultType;
    public bool IsCorrect;
    public bool IsDisplayError;
    public bool IsTargetError;
}

[DisallowMultipleComponent]
public class ErrorEvaluator : MonoBehaviour
{
    public FocusPointingEvaluation EvaluateFocusPointingClick(
        string targetDisplayId,
        Vector2 targetNormalizedPosition,
        float targetSizeNormalized,
        string clickedDisplayId,
        Vector2 clickedNormalizedPosition,
        bool hasValidDisplay)
    {
        FocusPointingEvaluation evaluation = new FocusPointingEvaluation();

        if (!hasValidDisplay || string.IsNullOrEmpty(clickedDisplayId) || clickedDisplayId == "None")
        {
            evaluation.ResultType = FocusPointingResultType.Miss;
            return evaluation;
        }

        if (clickedDisplayId != targetDisplayId)
        {
            evaluation.ResultType = FocusPointingResultType.DisplayError;
            evaluation.IsDisplayError = true;
            return evaluation;
        }

        float halfSize = Mathf.Max(0f, targetSizeNormalized) * 0.5f;
        Vector2 delta = clickedNormalizedPosition - targetNormalizedPosition;
        bool insideTarget = Mathf.Abs(delta.x) <= halfSize && Mathf.Abs(delta.y) <= halfSize;
        if (!insideTarget)
        {
            evaluation.ResultType = FocusPointingResultType.TargetError;
            evaluation.IsTargetError = true;
            return evaluation;
        }

        evaluation.ResultType = FocusPointingResultType.Correct;
        evaluation.IsCorrect = true;
        return evaluation;
    }
}
