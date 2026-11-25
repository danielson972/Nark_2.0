public static class DialogosDasMissoes
{
    public static EtapaDialogo Conversar(string nome)
    {
        var progresso = ProgressoMissoes.Atual;
        if (!progresso.LoboDerrotado)
            return null;

        if (nome == "Mestre" && progresso.TodasConcluidas)
        {
            if (progresso.AjudaReconhecida)
                return new("A vila ainda se lembra da ajuda que você nos deu. Obrigado por tudo.");

            if (progresso.RenunciouAoApoio)
                return new("Agradeço por ter voltado para ajudar nossa gente. A vila está melhor desde que você concluiu as tarefas.");

            return new("O lenhador, a verdureira, o agricultor e o pescador me contaram o que você fez. Sua ajuda sempre será lembrada nesta vila.",
                new OpcaoDialogo("Obrigado, Mestre.", () =>
                {
                    progresso.ReconhecerAjuda();
                    return new("Você ganhou 1 ponto de fama.");
                }));
        }

        return nome switch
        {
            "Lenhador" => Montar(TarefaVila.Lenhador,
                "Meu pai me deu um machado que deixei na Floresta Leste quando os monstros apareceram. Lá também ficaram umas tábuas mais resistentes que as daqui. Pode trazer os dois?",
                "O machado e as tábuas ficaram juntos na parte oeste da floresta.",
                "Meu pai ficaria contente em ver este machado de volta. Vou guardar as tábuas para o agricultor.",
                "O machado voltou para casa. As tábuas ficaram reservadas para fechar a passagem na plantação."),
            "Verdureira" or "Verdudeira" => Montar(TarefaVila.Verdureira,
                "Preciso de uma erva que cresce no nordeste da Floresta Leste. Se trouxer uma, preparo cinco poções de cura para você.",
                "Procure a erva no nordeste da floresta. Uma já é suficiente.",
                "É a erva certa. Aqui estão suas 5 poções de cura.",
                "Obrigada pela erva. As poções devem ajudar na próxima viagem."),
            "Agricultor" => Montar(TarefaVila.Agricultor,
                "Os slimes estão entrando na plantação. Siga as pegadas a partir do espantalho e descubra a passagem. As tábuas resistentes que o lenhador recuperar servem para fechá-la.",
                progresso.Concluida(TarefaVila.Lenhador)
                    ? "Siga as pegadas do espantalho até o fim e use as tábuas para fechar a passagem."
                    : "Você já pode examinar as pegadas. Para fechar a passagem, ajude o lenhador a recuperar as tábuas primeiro.",
                "A passagem está fechada e a plantação está segura. Tome 10 moedas de prata.",
                "Desde que você fechou a passagem, os slimes não voltaram à plantação."),
            "Pescador" => Montar(TarefaVila.Pescador,
                "As larvas da Floresta Leste são as melhores iscas para esta lagoa. Pode me trazer três? Pago dez moedas de prata.",
                $"Procure as larvas perto da árvore ao norte da floresta. Você já trouxe {progresso.NaMochila(ItemMissao.Isca)} de 3 para a mochila.",
                "Essas iscas vão render uma boa pescaria. Aqui estão suas 10 moedas de prata.",
                "As iscas da floresta funcionaram muito bem. Obrigado!"),
            _ => null
        };
    }

    private static EtapaDialogo Montar(TarefaVila tarefa, string pedido, string lembrete, string entrega, string agradecimento)
    {
        var progresso = ProgressoMissoes.Atual;
        if (progresso.Concluida(tarefa))
            return new(agradecimento);

        if (progresso.PodeEntregar(tarefa))
            return new("Você conseguiu? Posso receber agora?",
                new OpcaoDialogo("Concluir missão", () => new(progresso.Entregar(tarefa) ? entrega : lembrete)));

        if (progresso.Aceita(tarefa))
            return new(lembrete);

        return new(pedido,
            new OpcaoDialogo("Aceitar missão", () =>
            {
                progresso.Aceitar(tarefa);
                return new(lembrete);
            }),
            new OpcaoDialogo("Agora não", () => new("Tudo bem. Podemos conversar quando você voltar.")));
    }
}
