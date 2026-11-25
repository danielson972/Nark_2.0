using Godot;

public partial class Naroab : DialogoNpc
{
    private static readonly StringName[] AnimacoesParado =
    [
        "idle_baixo", "idle_baixo_direita", "idle_direita", "idle_cima_direita",
        "idle_cima", "idle_cima_esquerda", "idle_esquerda", "idle_baixo_esquerda"
    ];
    private AnimationPlayer _animacao;

    public override void _Ready()
    {
        base._Ready();
        _animacao = GetNode<AnimationPlayer>("Sprite2D/AnimationPlayer");
    }

    public override void OlharPara(Vector2 posicao)
    {
        Vector2 direcao = ToLocal(posicao);
        if (direcao.IsZeroApprox())
            return;

        int linha = Mathf.PosMod(2 - Mathf.RoundToInt(direcao.Angle() / (Mathf.Pi / 4)), 8);
        if (_animacao.CurrentAnimation == AnimacoesParado[linha])
            return;

        _animacao.Play(AnimacoesParado[linha]);
        _animacao.Advance(0);
    }
}
