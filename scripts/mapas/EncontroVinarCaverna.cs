using Godot;

public partial class EncontroVinarCaverna : Marker2D
{
    [Export] public PackedScene CenaVinar { get; set; }
    [Export] public NavigationRegion2D RegiaoNavegacao { get; set; }
    [Export] public PortalDeMapa PortalDeSaida { get; set; }

    public override void _Ready()
    {
        var progresso = ProgressoMissoes.Atual;
        if (CenaVinar is null || progresso.VinarFugiuDaCaverna || progresso.VinarDerrotado)
            return;

        var vinar = CenaVinar.Instantiate<Vinar>();
        vinar.FogeParaFloresta = true;
        vinar.RegiaoNavegacao = RegiaoNavegacao;
        if (PortalDeSaida is { } saida)
        {
            saida.TravessiaConcluida += vinar.RegistrarRetiradaDoHeroi;
            vinar.TreeExiting += () => saida.TravessiaConcluida -= vinar.RegistrarRetiradaDoHeroi;
        }
        AddChild(vinar);
    }
}
