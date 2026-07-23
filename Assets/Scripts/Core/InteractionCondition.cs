/// <summary>
/// 実験で比較する入力条件。
/// RaycastBaseline は通常のコントローラ Ray、ExplicitDisplayFocus は視線で候補を選び明示的に表示へフォーカスする条件。
/// </summary>
public enum InteractionCondition
{
    RaycastBaseline,
    ExplicitDisplayFocus,
    GazeRay
}
