using Godot;

public partial class PortalDeMapa : Area2D
{
	public const string CavernaLeste = "res://caverna_leste.tscn";

	[Export(PropertyHint.File, "*.tscn,*.scn")]
	public string Destino { get; set; } = "";
	[Export] public string NomePontoDeChegada { get; set; } = "";
	[Export] public bool ExigeRunaDaCaverna { get; set; }
	public event System.Action TravessiaConcluida;

	private Label _aviso;

	private static void PosicionarAposTroca(SceneTree arvore, string nomePonto)
	{
		void AoTrocarCena()
		{
			arvore.SceneChanged -= AoTrocarCena;
			Node cena = arvore.CurrentScene;
			Node ponto = cena.FindChild(nomePonto, recursive: true, owned: false);
			Marker2D marcador = ponto is PontoDeChegada configuracao ? configuracao.Marcador : ponto as Marker2D;
			if (marcador is null)
			{
				GD.PushWarning($"Ponto de chegada '{nomePonto}' não encontrado ou sem Marker2D em {cena.SceneFilePath}.");
				return;
			}

			if (arvore.GetFirstNodeInGroup("herois") is not Node2D heroi)
			{
				GD.PushWarning($"Nenhum herói encontrado para o ponto '{nomePonto}'.");
				return;
			}

			if (heroi is MovimentoHeroi jogador)
				jogador.PosicionarNaChegada(marcador.GlobalPosition);
			else
				heroi.GlobalPosition = marcador.GlobalPosition;

			arvore.Root.GetCamera2D()?.ResetSmoothing();
		}

		arvore.SceneChanged += AoTrocarCena;
	}

	public override void _Ready()
	{
		_aviso = new Label
		{
			Name = "AvisoInteracao",
			Text = "Pressione E",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Position = new Vector2(-100, -126),
			Size = new Vector2(200, 30),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ZIndex = 10,
			Visible = false
		};
		_aviso.AddThemeFontSizeOverride("font_size", 20);
		_aviso.AddThemeColorOverride("font_outline_color", Colors.Black);
		_aviso.AddThemeConstantOverride("outline_size", 5);
		AddChild(_aviso);

		BodyEntered += AoEntrar;
		BodyExited += AoSair;
		if (ExigeRunaDaCaverna)
		{
			ProgressoMissoes.Atual.Mudou += AtualizarAviso;
			AtualizarAviso();
		}
	}

	public override void _ExitTree()
	{
		if (ExigeRunaDaCaverna && ProgressoMissoes.Atual is { } progresso)
			progresso.Mudou -= AtualizarAviso;
	}

	private void AtualizarAviso()
	{
		var progresso = ProgressoMissoes.Atual;
		_aviso.Text = progresso.SeloCavernaRompido ? "Pressione E"
			: progresso.TemRunaCaverna ? "[E] Romper selo" : "Caverna selada";
	}

	private void AoEntrar(Node2D corpo)
	{
		if (corpo is MovimentoHeroi)
			_aviso.Show();
	}

	private void AoSair(Node2D corpo)
	{
		if (corpo is not MovimentoHeroi)
			return;

		foreach (Node2D dentro in GetOverlappingBodies())
		{
			if (dentro != corpo && dentro is MovimentoHeroi)
				return;
		}

		_aviso.Hide();
	}

	public override void _UnhandledInput(InputEvent evento)
	{
		if (!evento.IsActionPressed("interagir") || string.IsNullOrEmpty(Destino))
			return;

		foreach (Node2D corpo in GetOverlappingBodies())
		{
			if (corpo is not MovimentoHeroi heroi || heroi.Vida <= 0
				|| heroi.EmDialogo || heroi.EmInventario || heroi.Coletando)
				continue;

			if (ExigeRunaDaCaverna && !ProgressoMissoes.Atual.RomperSeloCaverna())
				return;
			GetViewport().SetInputAsHandled();
			string destino = Destino;
			string nomePonto = NomePontoDeChegada.Trim();
			SceneTree arvore = GetTree();
			var aoAtravessar = TravessiaConcluida;
			Error erro = arvore.ChangeSceneToFile(destino);
			if (erro != Error.Ok)
				GD.PushError($"Não foi possível abrir {destino}: {erro}.");
			else
			{
				if (nomePonto.Length > 0)
					PosicionarAposTroca(arvore, nomePonto);
				aoAtravessar?.Invoke();
			}
			return;
		}
	}
}
