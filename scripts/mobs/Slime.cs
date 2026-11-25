using Godot;

public partial class Slime : CharacterBody2D
{
    [Export(PropertyHint.Range, "0,500,1,or_greater")]
    public float Velocidade { get; set; } = 110f;

    [Export(PropertyHint.Range, "0,512,1,or_greater")]
    public float AlcanceDeteccao { get; set; } = 180f;

    [Export(PropertyHint.Range, "0,512,1,or_greater")]
    public float RaioPerseguicao { get; set; } = 260f;

    [Export(PropertyHint.Range, "0,256,1,or_greater")]
    public float AlcanceAtaque { get; set; } = 64f;

    [Export(PropertyHint.Range, "0,5,0.05,or_greater")]
    public float IntervaloAtaque { get; set; } = 1.25f;

    [Export(PropertyHint.Range, "1,20,1,or_greater")]
    public int VidaMaxima { get; set; } = 4;

    [Export(PropertyHint.Range, "1,10,1,or_greater")]
    public int DanoAtaque { get; set; } = 2;

    private const float AlcanceGolpeHeroi = 74f;
    private const double TempoRespawn = 60;
    private const double IntervaloRegeneracao = 2;
    private int _vida;
    private double _tempoSemAlvo;
    private Label _indicadorVida;
    private Sprite2D _sprite;
    private CollisionShape2D _colisao;
    private AnimationTree _arvore;
    private Timer _temporizadorRespawn;
    private Tween _piscar;
    private double _recargaGolpeHeroi;
    private double _tempoAteImpacto;
    private MovimentoHeroi _alvoDoGolpe;

    private AnimationNodeStateMachinePlayback _animacao = null!;
    private string _direcao = "baixo";
    private double _recargaAtaque;
    private Vector2 _origem;
    private MovimentoHeroi _alvo;

    public override void _Ready()
    {
        _arvore = GetNode<AnimationTree>("AnimationTree");
        _animacao = _arvore.Get("parameters/playback").As<AnimationNodeStateMachinePlayback>();
        _origem = GlobalPosition;
        _arvore.Active = true;
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _colisao = GetNode<CollisionShape2D>("CollisionShape2D");
        _temporizadorRespawn = new Timer { OneShot = true, WaitTime = TempoRespawn };
        _temporizadorRespawn.Timeout += Renascer;
        AddChild(_temporizadorRespawn);
        _vida = VidaMaxima;
        _indicadorVida = new Label
        {
            Position = new Vector2(-40, -64),
            Size = new Vector2(80, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 10
        };
        _indicadorVida.AddThemeFontSizeOverride("font_size", 14);
        _indicadorVida.AddThemeConstantOverride("outline_size", 4);
        AddChild(_indicadorVida);
        AtualizarIndicadorVida();
    }

    public override void _PhysicsProcess(double delta)
    {
        _recargaAtaque = System.Math.Max(0, _recargaAtaque - delta);
        _recargaGolpeHeroi = System.Math.Max(0, _recargaGolpeHeroi - delta);
        Velocity = Vector2.Zero;
        AplicarImpactoPendente(delta);
        if (EstaAtacando())
            return;

        AtualizarAlvo();
        if (_alvo is null)
        {
            RegenerarVida(delta);
            MoverEmDirecao(_origem - GlobalPosition, delta);
            return;
        }

        _tempoSemAlvo = 0;
        Vector2 direcao = _alvo.GlobalPosition - GlobalPosition;
        if (direcao.LengthSquared() <= AlcanceAtaque * AlcanceAtaque)
        {
            AtualizarDirecao(direcao);
            Atacar();
            return;
        }

        MoverEmDirecao(direcao, delta);
    }

    private void AtualizarAlvo()
    {
        float raioQuadrado = RaioPerseguicao * RaioPerseguicao;
        if (GlobalPosition.DistanceSquaredTo(_origem) > raioQuadrado)
        {
            _alvo = null;
            return;
        }

        if (IsInstanceValid(_alvo) && _alvo.IsInsideTree() && !_alvo.IsQueuedForDeletion()
            && _alvo.GlobalPosition.DistanceSquaredTo(_origem) <= raioQuadrado)
            return;

        MovimentoHeroi alvo = null;
        float distanciaMaxima = AlcanceDeteccao * AlcanceDeteccao;
        foreach (var node in GetTree().GetNodesInGroup("herois"))
        {
            if (node is not MovimentoHeroi heroi || heroi.IsQueuedForDeletion()
                || heroi.GlobalPosition.DistanceSquaredTo(_origem) > raioQuadrado)
                continue;

            float distancia = GlobalPosition.DistanceSquaredTo(heroi.GlobalPosition);
            if (distancia <= distanciaMaxima)
            {
                alvo = heroi;
                distanciaMaxima = distancia;
            }
        }

        _alvo = alvo;
    }

    private void MoverEmDirecao(Vector2 deslocamento, double delta)
    {
        if (deslocamento.LengthSquared() <= 1f)
            return;

        Andar(deslocamento);
        Velocity = (deslocamento / (float)delta).LimitLength(Velocidade);
        MoveAndSlide();
    }

    public void Andar(Vector2 direcao)
    {
        if (_vida <= 0 || direcao.IsZeroApprox() || EstaAtacando())
            return;

        AtualizarDirecao(direcao);

        var estado = new StringName($"andar_{_direcao}");
        if (_animacao.GetCurrentNode() != estado)
            _animacao.Start(estado);
    }

    public void Atacar()
    {
        if (_vida <= 0 || _recargaAtaque > 0 || EstaAtacando())
            return;

        _animacao.Start(new StringName($"atacar_{_direcao}"));
        _recargaAtaque = IntervaloAtaque;
        _alvoDoGolpe = _alvo;
        _tempoAteImpacto = 0.36;
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (_vida <= 0 || _recargaGolpeHeroi > 0 || !evento.IsActionPressed("atacar"))
            return;

        foreach (var node in GetTree().GetNodesInGroup("herois"))
        {
            if (node is not MovimentoHeroi heroi || heroi.Vida <= 0 || heroi.EmDialogo || heroi.EmInventario || heroi.Coletando
                || GlobalPosition.DistanceTo(heroi.GlobalPosition) > AlcanceGolpeHeroi)
                continue;

            ReceberGolpe(1 + heroi.Ataque);
            _recargaGolpeHeroi = 0.44;
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    private void AplicarImpactoPendente(double delta)
    {
        if (_tempoAteImpacto <= 0)
            return;

        _tempoAteImpacto -= delta;
        if (_tempoAteImpacto > 0)
            return;

        if (IsInstanceValid(_alvoDoGolpe) && _alvoDoGolpe.IsInsideTree()
            && GlobalPosition.DistanceTo(_alvoDoGolpe.GlobalPosition) <= AlcanceAtaque + 10f)
            _alvoDoGolpe.ReceberGolpe(DanoAtaque);

        _alvoDoGolpe = null;
    }

    private void ReceberGolpe(int dano)
    {
        _tempoSemAlvo = 0;
        _vida = Mathf.Max(0, _vida - dano);
        AtualizarIndicadorVida();
        if (_vida == 0)
        {
            ProgressoMissoes.Atual?.ReceberSaqueSlime();
            AguardarRespawn();
            return;
        }

        _piscar?.Kill();
        _sprite.Modulate = new Color(1, 0.4f, 0.4f);
        _piscar = CreateTween();
        _piscar.TweenProperty(_sprite, "modulate", Colors.White, 0.18);
    }

    private void RegenerarVida(double delta)
    {
        if (_vida <= 0 || _vida >= VidaMaxima)
            return;

        _tempoSemAlvo += delta;
        if (_tempoSemAlvo < IntervaloRegeneracao)
            return;

        _tempoSemAlvo -= IntervaloRegeneracao;
        _vida++;
        AtualizarIndicadorVida();
    }

    private void AguardarRespawn()
    {
        SetPhysicsProcess(false);
        Velocity = Vector2.Zero;
        _alvo = null;
        _alvoDoGolpe = null;
        _tempoAteImpacto = 0;
        _piscar?.Kill();
        _arvore.Active = false;
        _colisao.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        _sprite.Hide();
        _indicadorVida.Hide();
        _temporizadorRespawn.Start();
    }

    private void Renascer()
    {
        GlobalPosition = _origem;
        ResetPhysicsInterpolation();
        _vida = VidaMaxima;
        _tempoSemAlvo = 0;
        _direcao = "baixo";
        _recargaGolpeHeroi = 0;
        _recargaAtaque = 0;
        _sprite.Modulate = Colors.White;
        AtualizarIndicadorVida();
        _arvore.Active = true;
        _animacao.Start("andar_baixo");
        _colisao.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
        _sprite.Show();
        _indicadorVida.Show();
        SetPhysicsProcess(true);
    }

    private void AtualizarIndicadorVida()
    {
        _indicadorVida.Text = $"HP {_vida}/{VidaMaxima}";
    }

    private void AtualizarDirecao(Vector2 direcao)
    {
        if (direcao.IsZeroApprox())
            return;

        if (Mathf.Abs(direcao.X) > Mathf.Abs(direcao.Y))
            _direcao = direcao.X > 0 ? "direita" : "esquerda";
        else
            _direcao = direcao.Y > 0 ? "baixo" : "cima";
    }

    private bool EstaAtacando()
    {
        return _animacao.GetCurrentNode().ToString().StartsWith("atacar_", System.StringComparison.Ordinal);
    }
}
