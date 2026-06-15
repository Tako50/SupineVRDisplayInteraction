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
/// <summary>
/// T1クリック結果をCorrect / DisplayError / TargetError / Missへ分類する。
/// 入力方式に依存せず、クリックされた表示IDと正規化座標だけで判定する。
/// </summary>
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
        return EvaluateFocusPointingClick(
            targetDisplayId,
            targetNormalizedPosition,
            Vector2.one * targetSizeNormalized,
            clickedDisplayId,
            clickedNormalizedPosition,
            hasValidDisplay);
    }

    public FocusPointingEvaluation EvaluateFocusPointingClick(
        string targetDisplayId,
        Vector2 targetNormalizedPosition,
        Vector2 targetNormalizedSize,
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

        Vector2 halfSize = new Vector2(
            Mathf.Max(0f, targetNormalizedSize.x) * 0.5f,
            Mathf.Max(0f, targetNormalizedSize.y) * 0.5f);
        Vector2 delta = clickedNormalizedPosition - targetNormalizedPosition;
        bool insideTarget = Mathf.Abs(delta.x) <= halfSize.x && Mathf.Abs(delta.y) <= halfSize.y;
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
