using Godot;

public partial class PainelDeMissoes : CanvasLayer
{
    private Control _janela;
    private VBoxContainer _objetivos;
    private ProgressoMissoes _progresso;
    private MovimentoHeroi _heroi;

    public override void _Ready()
    {
        _janela = GetNode<Control>("%Janela");
        _objetivos = GetNode<VBoxContainer>("%Objetivos");
        GetNode<Button>("%Fechar").Pressed += Fechar;
        _progresso = ProgressoMissoes.Atual;
        _progresso.Mudou += Atualizar;
        GetTree().SceneChanged += Fechar;
    }

    public override void _ExitTree()
    {
        _progresso.Mudou -= Atualizar;
        GetTree().SceneChanged -= Fechar;
    }

    public override void _Input(InputEvent evento)
    {
        if (evento.IsActionPressed("diario"))
        {
            if (_janela.Visible)
                Fechar();
            else if (!Abrir())
                return;
        }
        else if (_janela.Visible && evento.IsActionPressed("inventario"))
        {
            Fechar();
            (GetTree().GetFirstNodeInGroup("interface_inventario") as Inventario)?.DefinirAberto(true);
        }
        else if (_janela.Visible && evento.IsActionPressed("ui_cancel"))
            Fechar();
        else
            return;

        GetViewport().SetInputAsHandled();
    }

    private bool Abrir()
    {
        if (GetTree().GetFirstNodeInGroup("herois") is not MovimentoHeroi heroi
            || heroi.Vida <= 0 || heroi.EmDialogo || heroi.Coletando)
            return false;

        if (heroi.EmInventario)
            (GetTree().GetFirstNodeInGroup("interface_inventario") as Inventario)?.DefinirAberto(false);
        if (heroi.EmInventario)
            return false;

        _heroi = heroi;
        _heroi.EmInventario = true;
        _janela.Show();
        Atualizar();
        return true;
    }

    private void Fechar()
    {
        _janela.Hide();
        if (IsInstanceValid(_heroi))
            _heroi.EmInventario = false;
        _heroi = null;
    }

    private void Atualizar()
    {
        if (!_janela.Visible)
            return;

        foreach (Node item in _objetivos.GetChildren())
        {
            _objetivos.RemoveChild(item);
            item.QueueFree();
        }

        foreach (var (titulo, objetivo) in DiarioDeMissoes.Ativas(_progresso))
        {
            var missao = new VBoxContainer();
            missao.AddChild(CriarTexto(titulo, 19));
            missao.AddChild(CriarTexto(objetivo, 16));
            _objetivos.AddChild(missao);
        }

        if (_objetivos.GetChildCount() == 0)
            _objetivos.AddChild(CriarTexto("Nenhuma missão ativa.", 17));
    }

    private static Label CriarTexto(string texto, int tamanho)
    {
        var linha = new Label { Text = texto, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        linha.AddThemeColorOverride("font_color", new Color(0.24f, 0.15f, 0.09f));
        linha.AddThemeFontSizeOverride("font_size", tamanho);
        return linha;
    }
}
