using System.Collections.Generic;
using Godot;

public partial class InteracaoMissoes : CanvasLayer
{
    public const string Vila = "res://scenes/mapas/Vila.scn";
    public const string Floresta = "res://scenes/mapas/floresta_leste.scn";
    private const float Alcance = 74f;
    private const double TempoColeta = 1.4;

    private readonly record struct Coletavel(Node2D No, string Id, ItemMissao Tipo);
    private readonly List<Coletavel> _itens = [];
    private readonly List<Node2D> _rastros = [];
    private ProgressoMissoes _progresso;
    private MovimentoHeroi _heroi;
    private CaixaDialogo _dialogo;
    private Label _aviso;
    private ProgressBar _barra;
    private Node2D _grupoRastros;
    private Sprite2D _barreira;
    private Vector2 _portao;
    private string _destino;
    private Coletavel? _coletando;
    private double _tempo;
    private int _vidaInicial;
    private bool _tentouSair;
    private bool _avisouNaSaida;

    public void Configurar(Fase fase, MovimentoHeroi heroi)
    {
        _heroi = heroi;
        _dialogo = fase.InterfaceDialogo;
        _progresso = ProgressoMissoes.Atual;
        bool naVila = fase.SceneFilePath == Vila;
        var saidaLeste = fase.FindChild("SaidaLeste", recursive: true, owned: true) as Marker2D;
        _portao = naVila ? saidaLeste?.GlobalPosition ?? new Vector2(2790, 180)
            : fase.PontoInicialHeroi.GlobalPosition + new Vector2(-150, 0);
        _destino = naVila ? Floresta : Vila;

        if (naVila)
            PrepararRastros(fase);
        else if (fase.GetNodeOrNull<Node2D>("Itens_missao") is { } itens)
            RegistrarItens(fase, itens);

        var placa = new Label { Position = _portao + new Vector2(-95, -35), Size = new Vector2(190, 28),
            HorizontalAlignment = HorizontalAlignment.Center, Text = naVila ? "Floresta Leste →" : "← Vila",
            MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 5 };
        placa.AddThemeConstantOverride("outline_size", 6);
        fase.AddChild(placa);
    }

    public override void _Ready()
    {
        Layer = 5;
        var tela = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(tela);
        tela.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var caixa = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        tela.AddChild(caixa);
        caixa.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        caixa.OffsetLeft = -225;
        caixa.OffsetRight = 225;
        caixa.OffsetTop = -100;
        caixa.OffsetBottom = -54;
        _aviso = new Label { HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        _aviso.AddThemeConstantOverride("outline_size", 6);
        caixa.AddChild(_aviso);
        _barra = new ProgressBar { MaxValue = TempoColeta, ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 12), Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        caixa.AddChild(_barra);
        _progresso.Mudou += AtualizarMapa;
        AtualizarMapa();
    }

    public override void _ExitTree()
    {
        _progresso.Mudou -= AtualizarMapa;
    }

    public override void _Process(double delta)
    {
        if (_coletando is { } item)
        {
            if (Bloqueado() || _heroi.Vida != _vidaInicial || !Perto(item.No.GlobalPosition))
            {
                EncerrarColeta();
                return;
            }

            _tempo += delta;
            _barra.Value = _tempo;
            if (_tempo >= TempoColeta)
            {
                _progresso.Coletar(item.Id, item.Tipo);
                EncerrarColeta();
            }
            return;
        }

        if (_destino == Floresta)
        {
            bool naSaida = _heroi.GlobalPosition.X >= _portao.X - Alcance
                && Mathf.Abs(_heroi.GlobalPosition.Y - _portao.Y) <= 115f;
            if (!naSaida)
            {
                _tentouSair = false;
                _avisouNaSaida = false;
            }
            else if (!Bloqueado() && !_tentouSair)
            {
                if (_progresso.AvisoDoCamponesPendente)
                {
                    if (_avisouNaSaida)
                        return;
                    if (_dialogo?.AbrirUltimoPedido() == true)
                    {
                        _avisouNaSaida = true;
                        return;
                    }
                }

                _tentouSair = true;
                TrocarDeMapa();
                return;
            }
        }

        _aviso.Text = Bloqueado() ? "" : TextoInteracao();
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (_coletando.HasValue && evento.IsActionPressed("ui_cancel"))
        {
            EncerrarColeta();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Bloqueado() || !evento.IsActionPressed("interagir"))
            return;

        if (_coletando.HasValue)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (ItemProximo() is { } item)
        {
            _coletando = item;
            _tempo = 0;
            _vidaInicial = _heroi.Vida;
            _heroi.Coletando = true;
            _aviso.Text = $"Coletando {ProgressoMissoes.Nome(item.Tipo)}…";
            _barra.Value = 0;
            _barra.Show();
        }
        else if (RastroProximo())
        {
            if (_progresso.Rastros < _rastros.Count)
                _progresso.ExaminarRastro(_progresso.Rastros);
            else
                _progresso.FecharPassagem(_rastros.Count);
        }
        else if (_destino == Vila && Perto(_portao))
        {
            GetViewport().SetInputAsHandled();
            TrocarDeMapa();
            return;
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void TrocarDeMapa()
    {
        var erro = GetTree().ChangeSceneToFile(_destino);
        if (erro != Error.Ok)
            GD.PushError($"Não foi possível abrir {_destino}: {erro}.");
        else if (_destino == Floresta)
            _progresso.RegistrarSaidaDaVila();
    }

    private void RegistrarItens(Fase fase, Node no)
    {
        foreach (var filho in no.GetChildren())
        {
            if (filho is not Node2D visual)
                continue;

            ItemMissao? tipo = visual.SceneFilePath == "res://scenes/bichos/larva_isca.tscn" ? ItemMissao.Isca
                : (visual as Sprite2D)?.Texture?.ResourcePath.GetFile() switch
                {
                    "machado_missao.png" => ItemMissao.Machado,
                    "tabuas_missoes.png" => ItemMissao.Tabuas,
                    "erva_missao.png" => ItemMissao.Erva,
                    _ => null
                };
            if (tipo is { } item)
                _itens.Add(new(visual, fase.GetPathTo(visual).ToString(), item));
            else
                RegistrarItens(fase, visual);
        }
    }

    private void PrepararRastros(Fase fase)
    {
        _grupoRastros = fase.GetNodeOrNull<Node2D>("Cenario/Rastro")
            ?? fase.FindChild("Pegada_slime", recursive: true, owned: true) as Node2D;
        if (_grupoRastros is null)
        {
            _grupoRastros = new Node2D { Name = "Pegada_slime" };
            fase.AddChild(_grupoRastros);
        }
        foreach (var no in _grupoRastros.GetChildren())
        {
            if (no is Node2D rastro)
                _rastros.Add(rastro);
        }

        if (_rastros.Count == 0 && _grupoRastros is Sprite2D { Texture: not null })
            _rastros.Add(_grupoRastros);

        if (_rastros.Count == 0)
        {
            var textura = GD.Load<Texture2D>("res://assets/sprites/itens_missoes/vila/pegadas_slime.png");
            foreach (var posicao in new Vector2[] { new(1115, 925), new(1295, 1010), new(1500, 1100) })
            {
                var rastro = new Sprite2D { Texture = textura, Scale = Vector2.One * (84f / textura.GetWidth()), ZIndex = 1 };
                _grupoRastros.AddChild(rastro);
                rastro.GlobalPosition = posicao;
                _rastros.Add(rastro);
            }
        }

        var madeira = GD.Load<Texture2D>(ProgressoMissoes.Icone(ItemMissao.Tabuas));
        _barreira = new Sprite2D { Texture = madeira, Scale = Vector2.One * (115f / madeira.GetWidth()), ZIndex = 1 };
        fase.AddChild(_barreira);
        _barreira.GlobalPosition = _rastros[^1].GlobalPosition;
        var corpo = new StaticBody2D();
        _barreira.AddChild(corpo);
        corpo.AddChild(new CollisionShape2D { Shape = new RectangleShape2D {
            Size = new Vector2(madeira.GetWidth(), madeira.GetWidth() * 0.2f) }, Disabled = true });
    }

    private void AtualizarMapa()
    {
        foreach (var item in _itens)
        {
            bool disponivel = ItemDisponivel(item);
            item.No.Visible = disponivel;
            item.No.ProcessMode = disponivel ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        }

        if (_grupoRastros is null)
            return;

        _grupoRastros.Visible = _progresso.Aceita(TarefaVila.Agricultor) && !_progresso.PassagemFechada;
        _barreira.Visible = _progresso.PassagemFechada;
        _barreira.GetChild(0).GetChild<CollisionShape2D>(0)
            .SetDeferred(CollisionShape2D.PropertyName.Disabled, !_progresso.PassagemFechada);
    }

    private bool ItemDisponivel(Coletavel item) =>
        _progresso.Aceita(ProgressoMissoes.TarefaDoItem(item.Tipo)) && !_progresso.Coletado(item.Id);

    private Coletavel? ItemProximo()
    {
        Coletavel? proximo = null;
        float distanciaMinima = Alcance * Alcance;
        foreach (var item in _itens)
        {
            if (!item.No.IsVisibleInTree() || !ItemDisponivel(item))
                continue;
            float distancia = _heroi.GlobalPosition.DistanceSquaredTo(item.No.GlobalPosition);
            if (distancia < distanciaMinima)
            {
                proximo = item;
                distanciaMinima = distancia;
            }
        }
        return proximo;
    }

    private bool RastroProximo() => _grupoRastros?.Visible == true
        && Perto(_rastros[System.Math.Min(_progresso.Rastros, _rastros.Count - 1)].GlobalPosition);

    private string TextoInteracao()
    {
        if (ItemProximo() is { } item)
            return $"[E] Coletar {ProgressoMissoes.Nome(item.Tipo)}";
        if (RastroProximo())
        {
            if (_progresso.Rastros < _rastros.Count)
                return $"[E] Examinar rastro {_progresso.Rastros + 1}/{_rastros.Count}";
            return _progresso.Concluida(TarefaVila.Lenhador) ? "[E] Fechar a passagem"
                : "Ajude o lenhador a recuperar as tábuas para fechar a passagem.";
        }
        return _destino == Vila && Perto(_portao) ? "[E] Voltar à Vila" : "";
    }

    private void EncerrarColeta()
    {
        _coletando = null;
        _heroi.Coletando = false;
        _barra.Hide();
        _aviso.Text = "";
    }

    private bool Perto(Vector2 posicao) => _heroi.GlobalPosition.DistanceSquaredTo(posicao) <= Alcance * Alcance;
    private bool Bloqueado() => _heroi.Vida <= 0 || _heroi.EmDialogo || _heroi.EmInventario;
}
