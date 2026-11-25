using Godot;

public partial class PicaretaColetavel : Node2D
{
    [Export] public float AlcanceColeta { get; set; } = 75f;
    private Label _aviso;

    public override void _Ready()
    {
        _aviso = GetNode<Label>("Aviso");
        if (ProgressoMissoes.Atual.TemPicareta)
        {
            Hide();
            QueueFree();
        }
    }

    public override void _Process(double delta)
    {
        _aviso.Visible = HeroiProximo() is not null;
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (!evento.IsActionPressed("interagir") || evento.IsEcho() || HeroiProximo() is null)
            return;
        GetViewport().SetInputAsHandled();
        ProgressoMissoes.Atual.ReceberPicareta();
        Hide();
        QueueFree();
    }

    private MovimentoHeroi HeroiProximo()
    {
        foreach (Node no in GetTree().GetNodesInGroup("herois"))
        {
            if (no is not MovimentoHeroi heroi || heroi.Vida <= 0 || heroi.EmDialogo
                || heroi.EmInventario || heroi.Coletando
                || GlobalPosition.DistanceSquaredTo(heroi.GlobalPosition) > AlcanceColeta * AlcanceColeta)
                continue;
            var raio = PhysicsRayQueryParameters2D.Create(heroi.GlobalPosition, GlobalPosition,
                1, [heroi.GetRid()]);
            if (GetWorld2D().DirectSpaceState.IntersectRay(raio).Count == 0)
                return heroi;
        }
        return null;
    }
}
