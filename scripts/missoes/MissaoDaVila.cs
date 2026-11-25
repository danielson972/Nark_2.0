using Godot;

public sealed record OpcaoDialogo(string Texto, System.Func<EtapaDialogo> Escolher);
public sealed record EtapaDialogo(string Fala, params OpcaoDialogo[] Opcoes)
{
    public System.Action AoEncerrar { get; init; }
}

public partial class MissaoDaVila : CanvasLayer
{
    [Export] public int RecompensaPrata { get; set; } = 25;

    internal enum Estado { Inicial, Aceita, Derrota, LoboVencido, Concluida }

    private Estado _estado;
    private MovimentoHeroi _heroi;
    private Marker2D _inicio;
    private LoboFuleiro _lobo;
    private Inventario _inventario;
    private Label _status;
    private int _vidaHeroi = 5;
    private float _recargaHeroi;

    private bool EmCombate => _estado is Estado.Aceita or Estado.Derrota;

    public override void _Ready()
    {
        _status = GetNode<Label>("Status");
    }

    public void Vincular(MovimentoHeroi heroi, Marker2D inicio, LoboFuleiro lobo, Inventario inventario)
    {
        _heroi = heroi;
        _inicio = inicio;
        _lobo = lobo;
        _inventario = inventario;
        _lobo.Vincular(heroi);
        _lobo.GolpeAcertou += SofrerGolpe;
        _lobo.Derrotado += VencerLobo;
        _estado = (Estado)ProgressoMissoes.Atual.EstadoLobo;
        if (_estado != Estado.Inicial && !ProgressoMissoes.Atual.RenunciouAoApoio)
        {
            _heroi.EquiparKitDaVila();
            _inventario?.EquiparKitDaVila();
        }
        if (EmCombate)
            _lobo.Ativar();
        AtualizarStatus();
    }

    public EtapaDialogo Conversar(DialogoNpc npc)
    {
        if (!IsInstanceValid(_lobo))
            return null;

        if (npc.Nome == "Mestre")
            return _estado switch
            {
                Estado.Inicial => new EtapaDialogo(
                    "Um lobo banhado em forças ocultas ronda a vila. As pessoas têm medo de sair. Você nos ajudaria?",
                    new OpcaoDialogo("Quero ajudar, mas não tenho equipamento.", OferecerEquipamento),
                    new OpcaoDialogo("Agora não.", () => new EtapaDialogo("Entendo. Estarei aqui se mudar de ideia."))),
                Estado.Aceita => new EtapaDialogo("Procure o lobo ao nordeste da vila. Espere o arranhão passar antes de se aproximar para golpear."),
                Estado.Derrota => new EtapaDialogo("Você voltou. Isso é o que importa. Recupere o fôlego e tente de novo quando estiver pronto."),
                Estado.LoboVencido => EntregarRecompensa(),
                Estado.Concluida => new EtapaDialogo("A vila está em paz graças a você. Siga pela estrada ao leste quando estiver pronto para continuar."),
                _ => null
            };

        return npc.Nome switch
        {
            "Verdureira" => FalaDaVila(
                "Desde que o lobo apareceu, ninguém se arrisca a vir buscar verduras.",
                "Sente-se um pouco. Coma alguma coisa e recupere as forças. O lobo pode esperar.",
                "As pessoas voltaram à feira! Obrigada por devolver a tranquilidade à vila."),
            "Agricultor" => FalaDaVila(
                "O lobo ronda as plantações. Tive de deixar metade da colheita no campo.",
                "Uma colheita ruim não acaba com o plantio. Descanse e tente de novo quando puder.",
                "Agora posso terminar a colheita em paz. Você tem minha gratidão."),
            "Lenhador" => FalaDaVila(
                "Deixei a lenha empilhada na trilha. Aquele lobo tem algo estranho nos olhos.",
                "Já derrubei muito machado antes de acertar o corte. Na próxima, espere o bote passar.",
                "A trilha da serraria está livre de novo. Obrigado, viajante."),
            "Pescador" => FalaDaVila(
                "Até a lagoa anda quieta desde que o lobo chegou. Ninguém quer ficar na margem depois do entardecer.",
                "Na pesca, às vezes é preciso recolher a linha e tentar de novo. Tome seu tempo.",
                "Já posso ficar na margem até escurecer. Se passar por aqui, a primeira pesca é por minha conta."),
            _ => null
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        _recargaHeroi = Mathf.Max(0, _recargaHeroi - (float)delta);
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (!EmCombate || !evento.IsActionPressed("atacar") || _heroi.EmDialogo || _heroi.EmInventario)
            return;

        if (_recargaHeroi <= 0 && _lobo.ReceberGolpe(Mathf.Max(1, _heroi.Ataque)))
        {
            _recargaHeroi = 0.44f;
            AtualizarStatus();
        }
        GetViewport().SetInputAsHandled();
    }

    private EtapaDialogo OferecerEquipamento()
    {
        return new EtapaDialogo(
            ProgressoMissoes.Atual.RenunciouAoApoio
                ? "Você ainda pode enfrentar o Lobo Fuleiro. Vai precisar lutar com o que tem."
                : "Tenho uma espada e um escudo velhos. Posso entregá-los a você se aceitar enfrentar o lobo.",
            new OpcaoDialogo("Aceito ajudar.", Aceitar),
            new OpcaoDialogo("Preciso pensar.", () => new EtapaDialogo("Pense com calma. Volte quando decidir.")));
    }

    private EtapaDialogo Aceitar()
    {
        _estado = Estado.Aceita;
        ProgressoMissoes.Atual.RegistrarEstadoLobo((int)_estado);
        bool recebeEquipamento = !ProgressoMissoes.Atual.RenunciouAoApoio;
        if (recebeEquipamento)
        {
            _heroi.EquiparKitDaVila();
            _inventario?.EquiparKitDaVila();
        }
        _lobo.Ativar();
        AtualizarStatus();
        return new EtapaDialogo(recebeEquipamento
            ? "A espada velha e o escudo já estão equipados. Vá ao nordeste da vila. Use Espaço para atacar quando estiver perto do lobo."
            : "O lobo está ao nordeste da vila. Use Espaço para atacar de perto e recue quando ele preparar o bote.");
    }

    private EtapaDialogo EntregarRecompensa()
    {
        _estado = Estado.Concluida;
        ProgressoMissoes.Atual.RegistrarEstadoLobo((int)_estado, RecompensaPrata);
        AtualizarStatus();
        return new EtapaDialogo($"Obrigado por livrar a vila do lobo. Tome {RecompensaPrata} moedas de prata. Quando quiser seguir viagem, pegue a estrada ao leste e pergunte pelos viajantes na próxima parada.");
    }

    private EtapaDialogo FalaDaVila(string antes, string apoio, string agradecimento)
    {
        return new EtapaDialogo(_estado switch
        {
            Estado.Derrota => apoio,
            Estado.LoboVencido or Estado.Concluida => agradecimento,
            _ => antes
        });
    }

    private void SofrerGolpe()
    {
        _vidaHeroi = Mathf.Max(0, _vidaHeroi - Mathf.Max(1, 2 - _heroi.Defesa));
        if (_vidaHeroi == 0)
        {
            _estado = Estado.Derrota;
            ProgressoMissoes.Atual.RegistrarEstadoLobo((int)_estado);
            _vidaHeroi = 5;
            _heroi.Velocity = Vector2.Zero;
            _heroi.GlobalPosition = _inicio.GlobalPosition;
            GetViewport().GetCamera2D()?.ResetSmoothing();
            _lobo.Ativar();
        }
        AtualizarStatus();
    }

    private void VencerLobo()
    {
        _estado = Estado.LoboVencido;
        ProgressoMissoes.Atual.RegistrarEstadoLobo((int)_estado);
        AtualizarStatus();
    }

    private void AtualizarStatus()
    {
        _status.Visible = _estado is not Estado.Inicial and not Estado.Concluida;
        _status.Text = _estado switch
        {
            Estado.Aceita => $"Lobo da vila   |   Vida: {_vidaHeroi}/5   |   Lobo: {_lobo.Vida}/4   |   Espaço: atacar",
            Estado.Derrota => $"Tente novamente   |   Vida: {_vidaHeroi}/5   |   Lobo: {_lobo.Vida}/4   |   Espaço: atacar",
            Estado.LoboVencido => "Lobo derrotado. Volte para falar com o Mestre.",
            _ => ""
        };
    }
}
