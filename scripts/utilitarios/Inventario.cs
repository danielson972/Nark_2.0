using Godot;

public partial class Inventario : CanvasLayer
{
    [Export] public int Ouro { get; set; }
    [Export] public int Prata { get; set; }
    [Export] public MovimentoHeroi HeroiDaCena { get; set; }

    private Control _janela;
    private Control _painel;
    private Label _valorOuro;
    private Label _valorPrata;
    private Label _valorFama;
    private GridContainer _slots;
    private ProgressoMissoes _progresso;
    private string _aba = "Todos";
    private readonly System.Collections.Generic.List<Control> _conteudoDaMissao = [];
    private MovimentoHeroi _heroi;

    public override void _Ready()
    {
        _janela = GetNode<Control>("Janela");
        _painel = _janela.GetNode<Control>("Painel");
        _valorOuro = GetNode<Label>("%ValorOuro");
        _valorPrata = GetNode<Label>("%ValorPrata");
        _slots = _painel.GetNode<GridContainer>("Mochila/Slots");
        AddToGroup("interface_inventario");
        _heroi = HeroiDaCena;
        _progresso = ProgressoMissoes.Atual;
        _progresso.Mudou += AtualizarMochila;

        _valorFama = new Label { Position = new Vector2(665, 21), Size = new Vector2(110, 24) };
        _painel.AddChild(_valorFama);

        var abas = new ButtonGroup();
        foreach (var no in _painel.GetNode("Abas").GetChildren())
        {
            var botao = (Button)no;
            string aba = botao.Name;
            botao.ToggleMode = true;
            botao.ButtonGroup = abas;
            botao.SetPressedNoSignal(aba == _aba);
            botao.Pressed += () =>
            {
                _aba = aba;
                AtualizarMochila();
            };
        }
        GetNode<Button>("%Fechar").Pressed += () => DefinirAberto(false);
        _janela.Resized += AjustarPainel;
    }

    public override void _ExitTree()
    {
        _progresso.Mudou -= AtualizarMochila;
    }

    public void VincularHeroi(Node2D heroi)
    {
        _heroi = heroi as MovimentoHeroi;
    }

    public void EquiparKitDaVila()
    {
        GetNode<Label>("%EspadaVelha").Show();
        GetNode<Label>("%EscudoVelho").Show();
    }

    public override void _Input(InputEvent evento)
    {
        if (evento.IsActionPressed("inventario"))
        {
            if (IsInstanceValid(_heroi) && (_heroi.EmDialogo || (_heroi.EmInventario && !_janela.Visible)))
                return;

            DefinirAberto(!_janela.Visible);
        }
        else if (_janela.Visible && evento.IsActionPressed("ui_cancel"))
        {
            DefinirAberto(false);
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    public void DefinirAberto(bool aberto)
    {
        _janela.Visible = aberto;
        if (aberto)
        {
            _valorOuro.Text = (Ouro + _progresso.Ouro).ToString();
            AtualizarMochila();
            if (IsInstanceValid(_heroi))
                _painel.GetNode<Label>("Equipamento/Atributos/Valor1").Text = _heroi.Vida.ToString();
            if (IsInstanceValid(_heroi) && _heroi.Ataque > 0)
            {
                GetNode<Label>("%Valor3").Text = _heroi.Ataque.ToString();
                GetNode<Label>("%Valor4").Text = _heroi.Defesa.ToString();
            }
            AjustarPainel();
        }

        if (IsInstanceValid(_heroi))
            _heroi.EmInventario = aberto;
    }

    private void AtualizarMochila()
    {
        _valorOuro.Text = (Ouro + _progresso.Ouro).ToString();
        _valorPrata.Text = (Prata + _progresso.Prata).ToString();
        _valorFama.Text = $"Fama {_progresso.Fama}";
        foreach (var conteudo in _conteudoDaMissao)
        {
            conteudo.GetParent().RemoveChild(conteudo);
            conteudo.QueueFree();
        }
        _conteudoDaMissao.Clear();

        int indice = 0;
        if (_progresso.TemPicareta && _aba is "Todos" or "Arsenal")
            AdicionarItemTexto(ref indice, "Picareta", "Ferramenta necessária para minerar pedras.");
        if (_aba is "Todos" or "Materiais")
        {
            foreach (RecursoMineracao recurso in System.Enum.GetValues<RecursoMineracao>())
            {
                if (recurso is RecursoMineracao.Ouro or RecursoMineracao.Prata)
                    continue; // Exibidos nos contadores de moedas.
                int quantidade = _progresso.QuantidadeMinerio(recurso);
                if (quantidade > 0)
                    AdicionarItemTexto(ref indice, $"{recurso}\n×{quantidade}", recurso.ToString());
            }
        }
        if (_aba is "Todos" or "Missoes")
        {
            foreach (ItemMissao item in System.Enum.GetValues<ItemMissao>())
            {
                int quantidade = _progresso.NaMochila(item);
                if (quantidade == 0)
                    continue;

                var slot = _slots.GetChild<Control>(indice++);
                var icone = new TextureRect
                {
                    Texture = GD.Load<Texture2D>(ProgressoMissoes.Icone(item)),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    Position = new Vector2(5, 4),
                    Size = new Vector2(51, 44),
                    TooltipText = ProgressoMissoes.Nome(item)
                };
                slot.AddChild(icone);
                _conteudoDaMissao.Add(icone);
                var texto = new Label
                {
                    Position = new Vector2(0, 46), Size = new Vector2(61, 20),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Text = item switch
                    {
                        ItemMissao.Machado => "Machado",
                        ItemMissao.Tabuas => "Tábuas",
                        ItemMissao.Erva => $"Erva ×{quantidade}",
                        _ => $"Isca ×{quantidade}"
                    }
                };
                texto.AddThemeFontSizeOverride("font_size", 12);
                texto.AddThemeColorOverride("font_color", new Color(0.26f, 0.13f, 0.08f));
                slot.AddChild(texto);
                _conteudoDaMissao.Add(texto);
            }
            if (_progresso.TemRunaCaverna)
            {
                var runa = new Label
                {
                    Text = "Runa",
                    TooltipText = "Runa da Caverna · Romper Selo",
                    Size = new Vector2(61, 68),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                runa.AddThemeColorOverride("font_color", new Color(0.26f, 0.13f, 0.08f));
                _slots.GetChild<Control>(indice++).AddChild(runa);
                _conteudoDaMissao.Add(runa);
            }
        }

        if (_progresso.Pocoes > 0 && _aba is "Todos" or "Materiais")
        {
            var pocao = new Button {
                TooltipText = "Clique para usar uma poção de cura.", Size = new Vector2(61, 68) };
            pocao.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://assets/sprites/itens/pocao_cura.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Position = new Vector2(5, 4),
                Size = new Vector2(51, 44),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            var quantidade = new Label
            {
                Text = $"×{_progresso.Pocoes}",
                Position = new Vector2(0, 46),
                Size = new Vector2(61, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            quantidade.AddThemeFontSizeOverride("font_size", 12);
            pocao.AddChild(quantidade);
            pocao.Pressed += () =>
            {
                _progresso.UsarPocao(_heroi);
                if (IsInstanceValid(_heroi))
                    _painel.GetNode<Label>("Equipamento/Atributos/Valor1").Text = _heroi.Vida.ToString();
            };
            _slots.GetChild<Control>(indice).AddChild(pocao);
            _conteudoDaMissao.Add(pocao);
        }
    }

    private void AdicionarItemTexto(ref int indice, string nome, string descricao)
    {
        if (indice >= _slots.GetChildCount())
            return;
        var texto = new Label
        {
            Text = nome, TooltipText = descricao,
            Size = new Vector2(61, 68),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        texto.AddThemeFontSizeOverride("font_size", 12);
        texto.AddThemeColorOverride("font_color", new Color(0.26f, 0.13f, 0.08f));
        _slots.GetChild<Control>(indice++).AddChild(texto);
        _conteudoDaMissao.Add(texto);
    }

    private void AjustarPainel()
    {
        var area = _janela.Size - new Vector2(36, 36);
        float escala = Mathf.Max(0, Mathf.Min(area.X / _painel.Size.X, area.Y / _painel.Size.Y));
        _painel.Scale = Vector2.One * escala;
        _painel.Position = (_janela.Size - _painel.Size * escala) / 2;
    }
}
