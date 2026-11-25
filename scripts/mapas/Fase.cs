using Godot;

public partial class Fase : Node2D
{
	[Export] public PackedScene CenaHeroi { get; set; }
	[Export] public Marker2D PontoInicialHeroi { get; set; }
	[Export] public Node2D GrupoPersonagens { get; set; }
	[Export] public Camera2D CameraDaFase { get; set; }
	[Export] public CaixaDialogo InterfaceDialogo { get; set; }

	public override void _Ready()
	{
		if (CenaHeroi is null || PontoInicialHeroi is null)
		{
			GD.PushError("Configure a cena e o ponto inicial do herói no Inspector.");
			return;
		}

		var heroi = CenaHeroi.Instantiate<Node2D>();
		heroi.Name = "Heroi";
		var grupo = GrupoPersonagens ?? this;
		heroi.Position = grupo.ToLocal(PontoInicialHeroi.GlobalPosition);
		grupo.AddChild(heroi);

		if (CameraDaFase is not null)
		{
			CameraDaFase.Reparent(heroi, keepGlobalTransform: false);
			CameraDaFase.Position = Vector2.Zero;
			CameraDaFase.MakeCurrent();
		}

		if (InterfaceDialogo is null && SceneFilePath == InteracaoMissoes.Floresta)
		{
			InterfaceDialogo = GD.Load<PackedScene>("res://scenes/utilitarios/CaixaDialogo.tscn").Instantiate<CaixaDialogo>();
			AddChild(InterfaceDialogo);
		}
		InterfaceDialogo?.VincularHeroi(heroi);
		var inventario = GetNodeOrNull<Inventario>("Inventario");
		if (inventario is null && SceneFilePath == InteracaoMissoes.Floresta)
		{
			inventario = GD.Load<PackedScene>("res://scenes/utilitarios/Inventario.tscn").Instantiate<Inventario>();
			AddChild(inventario);
		}
		inventario?.VincularHeroi(heroi);
		if (SceneFilePath == InteracaoMissoes.Floresta && heroi is MovimentoHeroi aventureiro)
		{
			AdicionarEntradaDaCaverna(aventureiro);
			aventureiro.Derrotado += () => Callable.From(() => VoltarAVila(aventureiro)).CallDeferred();
			if (ProgressoMissoes.Atual.EstadoLobo > 0 && !ProgressoMissoes.Atual.RenunciouAoApoio)
			{
				aventureiro.EquiparKitDaVila();
				inventario?.EquiparKitDaVila();
			}
		}

		if ((SceneFilePath == InteracaoMissoes.Vila || SceneFilePath == InteracaoMissoes.Floresta)
			&& heroi is MovimentoHeroi coletor)
		{
			var interacao = new InteracaoMissoes();
			interacao.Configurar(this, coletor);
			AddChild(interacao);
		}

		if (GetNodeOrNull<MissaoDaVila>("MissaoDaVila") is { } missao
			&& grupo.GetNodeOrNull<LoboFuleiro>("Mobs/Lobo") is { } lobo
			&& heroi is MovimentoHeroi jogador)
		{
			missao.Vincular(jogador, PontoInicialHeroi, lobo, inventario);
			if (InterfaceDialogo is not null)
				InterfaceDialogo.Missao = missao;
		}

	}

	private void AdicionarEntradaDaCaverna(MovimentoHeroi heroi)
	{
		const string texturaEntrada = "res://assets/sprites/ambiente/entrada_caverna_leste.png";
		foreach (Node no in FindChildren("*", "Sprite2D", recursive: true, owned: false))
		{
			if (no is not Sprite2D sprite || sprite.Texture?.ResourcePath != texturaEntrada)
				continue;

			int camadaEntrada = sprite.ZIndex;
			for (CanvasItem item = sprite; item.ZAsRelative && item.GetParent() is CanvasItem pai; item = pai)
				camadaEntrada += pai.ZIndex;
			heroi.ZAsRelative = false;
			heroi.ZIndex = Mathf.Max(1, camadaEntrada + 1);

			Rect2 imagem = sprite.GetRect();
			var portal = new PortalDeMapa
			{
				Name = "EntradaDaCaverna",
				Destino = PortalDeMapa.CavernaLeste,
				ExigeRunaDaCaverna = true,
				CollisionLayer = 0,
				CollisionMask = 1
			};
			portal.AddChild(new CollisionShape2D
			{
				Shape = new RectangleShape2D { Size = new Vector2(200, 120) }
			});
			AddChild(portal);
			portal.GlobalPosition = sprite.ToGlobal(imagem.Position + imagem.Size * new Vector2(0.49f, 0.66f));
			AdicionarVinarFugitivo(portal.GlobalPosition, heroi.ZIndex);
			return;
		}
	}

	private void AdicionarVinarFugitivo(Vector2 entrada, int camada)
	{
		var progresso = ProgressoMissoes.Atual;
		if (!progresso.VinarFugiuDaCaverna || progresso.VidaVinarNaFloresta <= 0)
			return;

		var cena = GD.Load<PackedScene>("res://scenes/mobs/vinar/Vinar.tscn");
		var vinar = cena.Instantiate<Vinar>();
		Vector2 posicaoPadrao = entrada + entrada.DirectionTo(PontoInicialHeroi.GlobalPosition) * 260f;
		Vector2 posicao = FindChild("VinarNaFloresta", owned: false) is Marker2D marcador
			? marcador.GlobalPosition : posicaoPadrao;
		vinar.Position = ToLocal(posicao);
		vinar.NaFlorestaAposFuga = true;
		vinar.RegiaoNavegacao = CriarNavegacaoDoVinar();
		vinar.ZAsRelative = false;
		vinar.ZIndex = camada;
		AddChild(vinar);
	}

	private NavigationRegion2D CriarNavegacaoDoVinar()
	{
		if (CameraDaFase is null)
			return null;

		const string grupoGeometria = "geometria_floresta_vinar";
		AddToGroup(grupoGeometria);
		var malha = new NavigationPolygon
		{
			AgentRadius = 34f,
			ParsedGeometryType = NavigationPolygon.ParsedGeometryTypeEnum.StaticColliders,
			SourceGeometryMode = NavigationPolygon.SourceGeometryModeEnum.GroupsWithChildren,
			SourceGeometryGroupName = grupoGeometria,
			SamplePartitionType = NavigationPolygon.SamplePartitionTypeEnum.Triangulate
		};
		malha.AddOutline(new Vector2[]
		{
			ToLocal(new Vector2(CameraDaFase.LimitLeft, CameraDaFase.LimitTop)),
			ToLocal(new Vector2(CameraDaFase.LimitRight, CameraDaFase.LimitTop)),
			ToLocal(new Vector2(CameraDaFase.LimitRight, CameraDaFase.LimitBottom)),
			ToLocal(new Vector2(CameraDaFase.LimitLeft, CameraDaFase.LimitBottom))
		});
		var regiao = new NavigationRegion2D { Name = "NavegacaoVinar", NavigationPolygon = malha };
		AddChild(regiao);
		return regiao;
	}

	private void VoltarAVila(MovimentoHeroi heroi)
	{
		if (!IsInsideTree())
			return;

		var erro = GetTree().ChangeSceneToFile(InteracaoMissoes.Vila);
		if (erro != Error.Ok)
		{
			GD.PushError($"Não foi possível retornar à Vila: {erro}.");
			heroi.RenascerNoInicio();
		}
	}
}
