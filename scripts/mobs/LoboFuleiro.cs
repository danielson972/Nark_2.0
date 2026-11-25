using Godot;

public partial class LoboFuleiro : AnimatedSprite2D
{
    [Export] public float RaioPatrulha { get; set; } = 55f;
    [Export] public float AlcanceDeteccao { get; set; } = 180f;
    [Export] public float RaioPerseguicao { get; set; } = 245f;

    private MovimentoHeroi _heroi;
    private Vector2 _origem;
    private Vector2 _alvoPatrulha;
    private string _direcao = "direita";
    private int _vida;
    private float _tempoAtaque;
    private float _recarga;
    private bool _golpePendente;
    private Tween _piscar;

    public int Vida => _vida;
    public event System.Action GolpeAcertou;
    public event System.Action Derrotado;

    public void Vincular(MovimentoHeroi heroi)
    {
        _heroi = heroi;
        _origem = GlobalPosition;
        Desativar();
    }

    public void Ativar()
    {
        GlobalPosition = _origem;
        _alvoPatrulha = _origem + Vector2.Right * RaioPatrulha;
        _vida = 4;
        _tempoAtaque = 0;
        _recarga = 0;
        _golpePendente = false;
        Play("andando_direita");
        _piscar?.Kill();
        Modulate = Colors.White;
        Show();
        SetPhysicsProcess(true);
    }

    public void Desativar()
    {
        Stop();
        Hide();
        SetPhysicsProcess(false);
    }

    public bool ReceberGolpe(int dano)
    {
        if (!Visible || _vida <= 0 || dano <= 0 || GlobalPosition.DistanceTo(_heroi.GlobalPosition) > 59f)
            return false;

        _vida = Mathf.Max(0, _vida - dano);
        if (_vida == 0)
        {
            Desativar();
            Derrotado?.Invoke();
        }
        else
        {
            _piscar?.Kill();
            Modulate = new Color(1, 0.4f, 0.4f);
            _piscar = CreateTween();
            _piscar.TweenProperty(this, "modulate", Colors.White, 0.18);
        }
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(_heroi))
            return;

        float dt = (float)delta;
        if (_heroi.EmDialogo || _heroi.EmInventario)
        {
            Pause();
            return;
        }

        if (!IsPlaying() && (_tempoAtaque > 0 || !Animation.ToString().StartsWith("arranhao")))
            Play();

        Vector2 ateHeroi = _heroi.GlobalPosition - GlobalPosition;
        _recarga = Mathf.Max(0, _recarga - dt);
        if (_tempoAtaque > 0)
        {
            _tempoAtaque -= dt;
            if (_golpePendente && _tempoAtaque <= 0.3f)
            {
                _golpePendente = false;
                if (ateHeroi.Length() <= 49f)
                    GolpeAcertou?.Invoke();
            }
            return;
        }

        bool perseguir = ateHeroi.Length() <= AlcanceDeteccao
            && _origem.DistanceTo(_heroi.GlobalPosition) <= RaioPerseguicao;
        if (perseguir && ateHeroi.Length() <= 39f)
        {
            if (_recarga <= 0)
            {
                _tempoAtaque = 0.6f;
                _recarga = 1.25f;
                _golpePendente = true;
                _direcao = Direcao(ateHeroi);
                Stop();
                Play($"arranhao_{_direcao}");
            }
            return;
        }

        if (!perseguir && GlobalPosition.DistanceTo(_alvoPatrulha) < 2)
            _alvoPatrulha.X = _origem.X + (_alvoPatrulha.X > _origem.X ? -RaioPatrulha : RaioPatrulha);

        Vector2 alvo = perseguir ? _heroi.GlobalPosition : _alvoPatrulha;
        Vector2 deslocamento = alvo - GlobalPosition;
        if (deslocamento.LengthSquared() < 0.01f)
            return;

        _direcao = Direcao(deslocamento);
        string animacao = $"{(perseguir ? "correndo" : "andando")}_{_direcao}";
        if (Animation != animacao)
            Play(animacao);

        GlobalPosition = GlobalPosition.MoveToward(alvo, (perseguir ? 118f : 32f) * dt);
    }

    private static string Direcao(Vector2 vetor)
    {
        if (Mathf.Abs(vetor.X) > Mathf.Abs(vetor.Y))
            return vetor.X > 0 ? "direita" : "esquerda";
        return vetor.Y > 0 ? "baixo" : "cima";
    }
}
