using Godot;

public partial class Campones : DialogoNpc
{
    [Export] public float AlcanceAbordagem { get; set; } = 100f;

    private static readonly StringName[] Direcoes =
    [
        "direita", "baixo_direita", "baixo", "baixo_esquerda",
        "esquerda", "cima_esquerda", "cima", "cima_direita"
    ];
    private AnimationNodeStateMachinePlayback _animacao;

    public override void _Ready()
    {
        base._Ready();
        var arvore = GetNode<AnimationTree>("AnimationTree");
        _animacao = arvore.Get("parameters/playback").As<AnimationNodeStateMachinePlayback>();
        arvore.Active = true;
        _animacao.Start("baixo");
    }

    public override void OlharPara(Vector2 posicao)
    {
        Vector2 direcao = posicao - GlobalPosition;
        if (direcao.IsZeroApprox())
            return;

        int indice = Mathf.PosMod(Mathf.RoundToInt(direcao.Angle() / (Mathf.Pi / 4)), Direcoes.Length);
        if (_animacao.GetCurrentNode() != Direcoes[indice])
            _animacao.Start(Direcoes[indice]);
    }

    public EtapaDialogo Conversar()
    {
        var progresso = ProgressoMissoes.Atual;
        if (progresso.TodasConcluidas)
            return new("Obrigado por ajudar a vila. Boa viagem pela estrada leste.");

        if (progresso.AceitouUltimoPedido)
            return new("Obrigado por aceitar ajudar a vila. Sua ajuda significa muito para nós.");

        if (progresso.RenunciouAoApoio)
            return new("Você ainda pode ajudar o povo daqui. Siga pela estrada se quiser, mas tenha cuidado com os slimes.");

        if (!progresso.AvisoDoCamponesPendente)
            return new("A estrada está aberta. Se você mudar de ideia, ainda pode ajudar antes de partir.",
                new OpcaoDialogo("Mudei de ideia. Vou ajudar.", () => Responder(false)),
                new OpcaoDialogo("Encerrar conversa", () => new("Cuide-se na estrada.")));

        return new("Antes de partir, escute meu último pedido. Ainda há gente precisando de ajuda na vila. Se recusar e seguir viagem, você perderá o apoio do Mestre e a chance de ganhar fama por essas missões.",
            new OpcaoDialogo("Vou ajudar a vila.", () => Responder(false)),
            new OpcaoDialogo("Não vou ajudar. Vou seguir viagem.", () => Responder(true)));
    }

    private static EtapaDialogo Responder(bool recusou)
    {
        ProgressoMissoes.Atual.ResponderCampones(recusou);
        return recusou
            ? new("Pode seguir pela estrada. Sem a espada e o escudo do Mestre, enfrentar os slimes será mais difícil.")
            : new("Obrigado por ajudar. Fale com o Mestre sobre o Lobo Fuleiro. Depois que o lobo cair, o pessoal da vila poderá contar o que precisa.");
    }
}
