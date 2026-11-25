using System.Collections.Generic;

public static class DiarioDeMissoes
{
    public static IEnumerable<(string Titulo, string Objetivo)> Ativas(ProgressoMissoes progresso)
    {
        var estadoLobo = (MissaoDaVila.Estado)progresso.EstadoLobo;
        if (estadoLobo is MissaoDaVila.Estado.Aceita or MissaoDaVila.Estado.Derrota)
            yield return ("O lobo da vila", "Derrote o lobo e fale com o Mestre.");
        else if (estadoLobo == MissaoDaVila.Estado.LoboVencido)
            yield return ("O lobo da vila", "Volte ao Mestre para receber a recompensa.");

        if (progresso.Aceita(TarefaVila.Lenhador))
            yield return ("O machado do lenhador", progresso.PodeEntregar(TarefaVila.Lenhador)
                ? "Entregue o machado e as tábuas ao lenhador."
                : $"Procure no oeste da floresta. Machado: {progresso.NaMochila(ItemMissao.Machado)}/1 · Tábuas: {progresso.NaMochila(ItemMissao.Tabuas)}/1.");
        if (progresso.Aceita(TarefaVila.Verdureira))
            yield return ("A erva da floresta", progresso.PodeEntregar(TarefaVila.Verdureira)
                ? "Entregue a erva à verdureira." : "Encontre uma erva no nordeste da Floresta Leste.");
        if (progresso.Aceita(TarefaVila.Agricultor))
            yield return ("A passagem dos slimes", progresso.PassagemFechada
                ? "Avise o agricultor de que a passagem foi fechada."
                : progresso.Concluida(TarefaVila.Lenhador)
                    ? "Siga os rastros a partir do espantalho e feche a passagem."
                    : "Siga os rastros do espantalho e ajude o lenhador a recuperar as tábuas.");
        if (progresso.Aceita(TarefaVila.Pescador))
            yield return ("Iscas para o pescador", progresso.PodeEntregar(TarefaVila.Pescador)
                ? "Entregue as três iscas ao pescador."
                : $"Colete larvas ao norte da floresta. Iscas: {progresso.NaMochila(ItemMissao.Isca)}/3.");

        if (progresso.CriseVinarAtiva)
            yield return ("Vinar à solta", "Derrote Vinar na Floresta Leste para encerrar as penalidades da fuga.");
        else if (progresso.ConversouGarimpeiro && !progresso.SeloCavernaRompido)
            yield return ("O selo da Caverna Leste", progresso.TemRunaCaverna
                ? "Use a Runa da Caverna com E na entrada para romper o selo."
                : progresso.Concluida(TarefaVila.Agricultor)
                    ? "Volte ao garimpeiro para receber a Runa da Caverna."
                    : "Conclua a missão da passagem dos slimes para ganhar a runa com o garimpeiro.");
    }
}
