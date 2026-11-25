using Godot;

public partial class Morcego : CharacterBody2D
{
    [Export(PropertyHint.Range, "0.1,120,0.1,or_greater")]
    public float IntervaloPasseio { get; set; } = 10f;

    [Export(PropertyHint.Range, "1,500,1,or_greater")]
    public float DistanciaPasseio { get; set; } = 96f;

    [Export(PropertyHint.Range, "1,500,1,or_greater")]
    public float Velocidade { get; set; } = 60f;

    [Export] public Vector2 DirecaoPasseio { get; set; } = Vector2.Right;

    [Export(PropertyHint.Range, "1,500,1,or_greater")]
    public float AlcanceDeteccao { get; set; } = 180f;

    [Export(PropertyHint.Range, "0.8,10,0.1,or_greater")]
    public float IntervaloAtaque { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "1,1000,1,or_greater")]
    public float VelocidadeBroca { get; set; } = 600f;

    [Export(PropertyHint.Range, "1,20,1,or_greater")]
    public int DanoAtaque { get; set; } = 1;

    private MovimentoHeroi _alvoAtaque;
    private Vector2 _direcaoAtaque;
    private double _recargaAtaque;
    private bool _impactoAplicado;
    private bool _investidaBloqueada;
    private bool _movendo;
    private bool _atacando;
    private int _proximoPonto;
    private Vector2 _origem;
    private Vector2 _destino;
    private double _tempoDesdeMudanca;
    private Sprite2D _sprite;
    private AnimationPlayer _animacao;

    public override void _Ready()
    {
        _origem = GlobalPosition;
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _animacao = _sprite.GetNode<AnimationPlayer>("AnimationPlayer");
        _animacao.AnimationFinished += AoTerminarAnimacao;
        Parar();
    }

    public override void _PhysicsProcess(double delta)
    {
        Velocity = Vector2.Zero;
        // Conta também o tempo de voo: com o percurso padrão, parte aos 10, 20 e 30 segundos.
        _tempoDesdeMudanca += delta;
        _recargaAtaque = System.Math.Max(0, _recargaAtaque - delta);
        if (_atacando)
        {
            AtualizarBroca(delta);
            return;
        }

        if (HeroiProximo() is { } heroi)
        {
            if (_recargaAtaque <= 0)
            {
                _alvoAtaque = heroi;
                Atacar(heroi.GlobalPosition - GlobalPosition);
            }
            else
            {
                _sprite.FlipH = false;
                _animacao.Play("parado");
            }
            return;
        }

        if (!_movendo)
        {
            if (_tempoDesdeMudanca < Mathf.Max(0.1f, IntervaloPasseio))
                return;

            Vector2 direcao = DirecaoPasseio.IsZeroApprox() ? Vector2.Right : DirecaoPasseio.Normalized();
            float distancia = Mathf.Max(1f, DistanciaPasseio);
            _destino = _proximoPonto switch
            {
                0 => _origem + direcao * distancia,
                1 => _origem + direcao * distancia * 2f,
                _ => _origem
            };
            _proximoPonto = (_proximoPonto + 1) % 3;
            _tempoDesdeMudanca = 0;
            _movendo = true;
        }

        Vector2 deslocamento = _destino - GlobalPosition;
        if (deslocamento.LengthSquared() <= 0.01f)
        {
            GlobalPosition = _destino;
            Parar();
            return;
        }

        AnimarMovimento(deslocamento);
        Velocity = deslocamento.Normalized() * Mathf.Max(1f, Velocidade);
        var colisao = MoveAndCollide(deslocamento.LimitLength(Mathf.Max(1f, Velocidade) * (float)delta));
        // Ao encontrar um obstáculo, espera a próxima mudança sem atravessar a parede.
        if (colisao is not null)
            Parar();
    }

    public bool Atacar(Vector2 direcao)
    {
        if (_atacando || _animacao is null)
            return false;

        _atacando = true;
        _recargaAtaque = Mathf.Max(0.8f, IntervaloAtaque);
        _direcaoAtaque = direcao.IsZeroApprox() ? Vector2.Down : direcao.Normalized();
        _impactoAplicado = false;
        _investidaBloqueada = false;
        Velocity = Vector2.Zero;
        _sprite.FlipH = direcao.X < 0;
        string animacao = "broca_de_sangue";
        if (!direcao.IsZeroApprox())
        {
            if (Mathf.Abs(direcao.X) > Mathf.Abs(direcao.Y) * 2f)
                animacao = "broca_de_sangue_lado";
            else if (direcao.Y < 0)
                animacao = "broca_de_sangue_cima";
            else if (Mathf.Abs(direcao.X) > Mathf.Abs(direcao.Y) * 0.5f)
                animacao = "broca_de_sangue_diagonal";
        }
        _animacao.Play(animacao);
        return true;
    }

    private MovimentoHeroi HeroiProximo()
    {
        MovimentoHeroi proximo = null;
        float menorDistancia = AlcanceDeteccao * AlcanceDeteccao;
        foreach (Node no in GetTree().GetNodesInGroup("herois"))
        {
            if (no is not MovimentoHeroi heroi || !PodeAtacar(heroi))
                continue;

            float distancia = GlobalPosition.DistanceSquaredTo(heroi.GlobalPosition);
            if (distancia > menorDistancia)
                continue;

            var raio = PhysicsRayQueryParameters2D.Create(GlobalPosition, heroi.GlobalPosition,
                CollisionMask, [GetRid(), heroi.GetRid()]);
            if (GetWorld2D().DirectSpaceState.IntersectRay(raio).Count > 0)
                continue;

            proximo = heroi;
            menorDistancia = distancia;
        }
        return proximo;
    }

    private static bool PodeAtacar(MovimentoHeroi heroi) => IsInstanceValid(heroi)
        && heroi.IsInsideTree() && !heroi.IsQueuedForDeletion()
        && heroi.Vida > 0 && !heroi.EmDialogo && !heroi.EmInventario;

    private void AtualizarBroca(double delta)
    {
        // A investida ocorre durante a transformação em broca (quadros 3 e 4).
        double tempo = _animacao.CurrentAnimationPosition;
        if (tempo < 0.36 || tempo >= 0.68 || _investidaBloqueada)
            return;
        if (!PodeAtacar(_alvoAtaque))
            return;

        Velocity = _direcaoAtaque * Mathf.Max(1f, VelocidadeBroca);
        var colisao = MoveAndCollide(Velocity * (float)delta);
        if (colisao is null)
            return;

        _investidaBloqueada = true;
        Velocity = Vector2.Zero;
        if (!_impactoAplicado && colisao.GetCollider() is MovimentoHeroi atingido && PodeAtacar(atingido))
        {
            _impactoAplicado = true;
            atingido.ReceberGolpe(DanoAtaque);
        }
    }

    private void AoTerminarAnimacao(StringName nome)
    {
        if (!nome.ToString().StartsWith("broca_de_sangue", System.StringComparison.Ordinal))
            return;

        _atacando = false;
        _alvoAtaque = null;
        if (_movendo)
            AnimarMovimento(_destino - GlobalPosition);
        else
            Parar();
    }

    private void Parar()
    {
        _movendo = false;
        Velocity = Vector2.Zero;
        _sprite.FlipH = false;
        _animacao.Play("parado");
    }

    private void AnimarMovimento(Vector2 direcao)
    {
        bool horizontal = Mathf.Abs(direcao.X) >= Mathf.Abs(direcao.Y);
        _sprite.FlipH = horizontal && direcao.X < 0;
        _animacao.Play(horizontal ? "andando" : direcao.Y < 0 ? "andando_cima" : "andando_baixo");
    }
}
