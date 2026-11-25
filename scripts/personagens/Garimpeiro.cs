public partial class Garimpeiro : DialogoNpc
{
	public EtapaDialogo Conversar(System.Action mostrarAviso)
	{
		var progresso = ProgressoMissoes.Atual;
		if (progresso.VinarDerrotado)
			return new("Desde que você derrotou Vinar, a floresta voltou a ficar tranquila.");
		if (progresso.CriseVinarAtiva)
			return new("Vinar está rondando a Floresta Leste. Derrote-o para que o comércio e o saque voltem ao normal.")
				{ AoEncerrar = mostrarAviso };

		bool primeiraConversa = !progresso.ConversouGarimpeiro;
		progresso.RegistrarConversaGarimpeiro();
		if (progresso.TemRunaCaverna)
			return Avisar(progresso, mostrarAviso);

		if (!progresso.Concluida(TarefaVila.Agricultor))
			return new("A Caverna Leste está selada. Ajude o agricultor a encontrar e fechar a passagem dos slimes. Depois volte aqui e eu ensino como romper o selo.");

		string fala = primeiraConversa
			? "A Caverna Leste está selada. Soube que você fechou a passagem dos slimes para o agricultor. Posso ensinar você a abrir a entrada."
			: "Você fechou a passagem dos slimes. Tenho aqui a Runa da Caverna e posso ensinar como usá-la.";
		return new(fala, new OpcaoDialogo("Receber a runa e aprender Romper Selo", () =>
		{
			progresso.ReceberRunaCaverna();
			return Avisar(progresso, mostrarAviso);
		}));
	}

	private static EtapaDialogo Avisar(ProgressoMissoes progresso, System.Action mostrarAviso)
	{
		string instrucao = progresso.SeloCavernaRompido
			? "O selo já foi rompido."
			: "Use Romper Selo com E diante da entrada.";
		return new($"{instrucao} Lá dentro vive Vinar. Se você atacá-lo e sair da caverna antes de derrotá-lo, ele vai escapar para a floresta.")
			{ AoEncerrar = mostrarAviso };
	}
}
