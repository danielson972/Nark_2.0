using Godot;

public partial class PedraMineravel : Node2D
{
    [Export(PropertyHint.Range, "1,100,1,or_greater")]
    public int GolpesParaQuebrar { get; set; } = 3;
    [Export] public RecursoMineracao RecursoFornecido { get; set; } = RecursoMineracao.Ouro;
    [Export(PropertyHint.Range, "1,100,1,or_greater")]
    public int QuantidadeFornecida { get; set; } = 1;
    [Export(PropertyHint.Range, "1,300,1,or_greater")]
    public float AlcanceMineracao { get; set; } = 85f;
    // Use um identificador único por pedra para manter sua destruição entre partidas.
    [Export] public string IdPersistente { get; set; } = "";

    public int GolpesDados { get; private set; }
    public bool Quebrada { get; private set; }

    private MovimentoHeroi _minerador;
    private int _vidaInicial;
    private double _tempoGolpe;
    private bool _impactoAplicado;
    private Sprite2D _sprite;
    private Sprite2D _lascas;
    private AnimationPlayer _animacaoLascas;
    private CollisionPolygon2D _colisao;
    private Label _aviso;

    public override void _Ready()
    {
        AddToGroup("pedras_mineraveis");
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _colisao = GetNode<CollisionPolygon2D>("Sprite2D/StaticBody2D/CollisionPolygon2D");
        _lascas = GetNode<Sprite2D>("Lascas");
        _animacaoLascas = _lascas.GetNode<AnimationPlayer>("AnimationPlayer");
        _animacaoLascas.AnimationFinished += _ => EncerrarGolpe();
        _aviso = GetNode<Label>("AvisoMineracao");
        if (ProgressoMissoes.Atual.PedraMinerada(IdPersistente))
        {
            Quebrada = true;
            Hide();
            _colisao.SetDeferred(CollisionPolygon2D.PropertyName.Disabled, true);
            QueueFree();
        }
    }

    public override void _Process(double delta)
    {
        if (Quebrada)
            return;
        var heroi = HeroiDisponivel();
        bool perto = heroi is not null && PedraMaisProxima(heroi) == this;
        _aviso.Visible = perto || IsInstanceValid(_minerador);
        _aviso.Text = IsInstanceValid(_minerador) ? "Minerando…"
            : !ProgressoMissoes.Atual.TemPicareta ? "Precisa de uma picareta"
            : $"Pressione E para minerar ({GolpesDados}/{Mathf.Max(1, GolpesParaQuebrar)})";
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (!evento.IsActionPressed("interagir") || evento.IsEcho() || Quebrada)
            return;
        var heroi = HeroiDisponivel();
        if (heroi is null || PedraMaisProxima(heroi) != this)
            return;
        // Consome a interação mesmo sem a ferramenta: E não ativa outro objeto ao mesmo tempo.
        GetViewport().SetInputAsHandled();
        TentarMinerar(heroi);
    }

    public bool TentarMinerar(MovimentoHeroi heroi)
    {
        if (Quebrada || IsInstanceValid(_minerador) || !PodeInteragir(heroi)
            || !ProgressoMissoes.Atual.TemPicareta || !EstaAoAlcance(heroi))
            return false;
        _minerador = heroi;
        _vidaInicial = heroi.Vida;
        _tempoGolpe = 0;
        _impactoAplicado = false;
        heroi.Coletando = true;
        heroi.Velocity = Vector2.Zero;
        heroi.VidaEsgotada += CancelarGolpe;
        _lascas.Show();
        _animacaoLascas.Play("minerando");
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(_minerador))
            return;
        if (!Quebrada && (_minerador.Vida != _vidaInicial || _minerador.EmDialogo
            || _minerador.EmInventario || !EstaAoAlcance(_minerador)))
        {
            CancelarGolpe();
            return;
        }
        _tempoGolpe += delta;
        if (_impactoAplicado || _tempoGolpe < 0.16)
            return;
        _impactoAplicado = true;
        GolpesDados++;
        if (GolpesDados < Mathf.Max(1, GolpesParaQuebrar))
            return;
        if (!ProgressoMissoes.Atual.ReceberMineracao(IdPersistente, RecursoFornecido,
            Mathf.Max(1, QuantidadeFornecida)))
        {
            CancelarGolpe();
            return;
        }
        Quebrada = true;
        _sprite.Hide();
        _aviso.Hide();
        _colisao.SetDeferred(CollisionPolygon2D.PropertyName.Disabled, true);
    }

    private bool EstaAoAlcance(MovimentoHeroi heroi)
    {
        if (GlobalPosition.DistanceSquaredTo(heroi.GlobalPosition) > AlcanceMineracao * AlcanceMineracao)
            return false;
        var corpo = GetNode<StaticBody2D>("Sprite2D/StaticBody2D");
        var raio = PhysicsRayQueryParameters2D.Create(heroi.GlobalPosition, GlobalPosition,
            1, [heroi.GetRid(), corpo.GetRid()]);
        return GetWorld2D().DirectSpaceState.IntersectRay(raio).Count == 0;
    }

    private static bool PodeInteragir(MovimentoHeroi heroi) => IsInstanceValid(heroi)
        && heroi.IsInsideTree() && !heroi.IsQueuedForDeletion() && heroi.Vida > 0
        && !heroi.EmDialogo && !heroi.EmInventario && !heroi.Coletando;

    private MovimentoHeroi HeroiDisponivel()
    {
        foreach (Node no in GetTree().GetNodesInGroup("herois"))
            if (no is MovimentoHeroi heroi && PodeInteragir(heroi) && EstaAoAlcance(heroi))
                return heroi;
        return null;
    }

    private PedraMineravel PedraMaisProxima(MovimentoHeroi heroi)
    {
        PedraMineravel proxima = null;
        float menor = float.PositiveInfinity;
        foreach (Node no in GetTree().GetNodesInGroup("pedras_mineraveis"))
        {
            if (no is not PedraMineravel pedra || pedra.Quebrada || pedra.IsQueuedForDeletion()
                || !pedra.EstaAoAlcance(heroi))
                continue;
            float distancia = pedra.GlobalPosition.DistanceSquaredTo(heroi.GlobalPosition);
            if (distancia < menor || (Mathf.IsEqualApprox(distancia, menor)
                && pedra.GetInstanceId() < proxima.GetInstanceId()))
            {
                menor = distancia;
                proxima = pedra;
            }
        }
        return proxima;
    }

    private void CancelarGolpe()
    {
        _animacaoLascas.Stop();
        EncerrarGolpe();
    }

    private void EncerrarGolpe()
    {
        LiberarHeroi();
        _lascas.Hide();
        if (Quebrada)
            QueueFree();
    }

    private void LiberarHeroi()
    {
        if (IsInstanceValid(_minerador))
        {
            _minerador.VidaEsgotada -= CancelarGolpe;
            _minerador.Coletando = false;
        }
        _minerador = null;
    }

    public override void _ExitTree() => LiberarHeroi();
}
