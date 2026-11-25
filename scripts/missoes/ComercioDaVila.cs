public static class ComercioDaVila
{
	public static EtapaDialogo Conversar(string nome, EtapaDialogo missao)
	{
		bool pescador = nome == "Pescador";
		if (!pescador && nome is not ("Verdureira" or "Verdudeira"))
			return missao;

		var progresso = ProgressoMissoes.Atual;
		if (!progresso.LoboDerrotado)
			return missao;

		int precoBase = pescador ? 10 : 8;
		EtapaDialogo Loja()
		{
			if (pescador && progresso.CriseVinarAtiva)
				return new("Com Vinar à solta, vou guardar meus suprimentos para a vila. Volte quando ele for derrotado.");

			int preco = progresso.PrecoPocao(precoBase);
			string fala = progresso.CriseVinarAtiva
				? "Desde que Vinar fugiu, está mais difícil trazer ervas da floresta. As poções ficaram mais caras."
				: "Tenho poções de cura para a sua viagem.";
			return new(fala,
				new OpcaoDialogo($"Comprar poção por {preco} pratas", () => new(
					progresso.ComprarPocao(precoBase, pescador) ? "Aqui está sua poção de cura." : "Você não tem prata suficiente.")),
				new OpcaoDialogo("Voltar", () => Conversar(nome, missao)));
		}

		var comprar = new OpcaoDialogo("Ver poções à venda", Loja);
		return missao is null
			? new("O que você procura?", comprar)
			: new("Como posso ajudar?", new OpcaoDialogo("Conversar sobre a missão", () => missao), comprar);
	}
}
