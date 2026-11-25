using Godot;

public partial class Vinar : CharacterBody2D
{
	private static readonly StringName Idle = "idle";
	private static readonly StringName Run = "run";
	private static readonly StringName Attack = "attack";
	private static readonly StringName Magic = "magic";
	private static readonly StringName ImpactoMagia = "impacto";
	private static readonly StringName Ultimate = "ultimate";
	private static readonly StringName Death = "death";
	private const float DistanciaParaRecalcularRota = 24f;
	private const float IntervaloGolpeHeroi = 0.44f;
	private const float EspacamentoMagiaEmRaios = 2.5f;
	private static readonly int[] LimitesCombo = [30, 20, 10];
	private static readonly Vector2[] FormacaoMagia =
		[Vector2.Zero, Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left];

	[Export(PropertyHint.Range, "1,1000,1,or_greater")]
	public int VidaMaxima { get; set; } = 12;
	[Export] public float AlcanceGolpeHeroi { get; set; } = 74f;
	[Export] public float AlcanceAtaque { get; set; } = 105f;
	[Export] public float Velocidade { get; set; } = 300f;
	[Export(PropertyHint.Range, "0,120,1,or_greater")] public float PausaPatrulha { get; set; } = 35f;
	[Export(PropertyHint.Range, "1,60,1,or_greater")] public float IntervaloRegeneracao { get; set; } = 12f;
	[Export] public float IntervaloEntreAtaques { get; set; } = 0.65f;
	[Export] public int DanoAtaque { get; set; } = 1;
	[Export] public float AlcanceMagia { get; set; } = 340f;
	[Export] public float RaioMagia { get; set; } = 82f;
	[Export] public float RecargaMagia { get; set; } = 4.5f;
	[Export] public int DanoMagia { get; set; } = 1;
	[Export(PropertyHint.Range, "0,1,0.05")] public float FatorVelocidadeMagia { get; set; } = 0.6f;
	[Export(PropertyHint.Range, "0,10,0.1,or_greater")] public float DuracaoLentidaoMagia { get; set; } = 2.5f;
	[Export] public PackedScene CenaEfeitoMagia { get; set; }
	[Export] public float AlcanceParaLancarUltimate { get; set; } = 240f;
	[Export] public int DanoUltimate { get; set; } = 2;
	[Export] public NavigationRegion2D RegiaoNavegacao { get; set; }

	public bool FogeParaFloresta { get; set; }
	public bool NaFlorestaAposFuga { get; set; }

	private AnimatedSprite2D _sprite;
	private NavigationAgent2D _navegacao;
	private ProgressBar _barraVida;
	private Label _textoVida;
	private MovimentoHeroi _heroi;
	private int _vida;
	private int _vidaMaxima;
	private float _tempoAteProximoAtaque;
	private float _recargaGolpeHeroi;
	private float _tempoAteProximaMagia;
	private float _pausaPatrulha;
	private float _tempoForaDeCombate;
	private bool _rotaDefinida;
	private bool _foiAtacado;
	private int _combosConcluidos;
	private bool _ultimatePendente;
	private bool _impactoAplicado;
	private readonly Vector2[] _centrosMagia = new Vector2[FormacaoMagia.Length];
	private Vector2 _direcaoUltimate;
	private readonly AnimationPlayer[] _efeitosMagia = new AnimationPlayer[FormacaoMagia.Length];

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_navegacao = GetNode<NavigationAgent2D>("NavigationAgent2D");
		_barraVida = GetNode<ProgressBar>("BarraVida");
		_textoVida = _barraVida.GetNode<Label>("Texto");
		_vidaMaxima = Mathf.Max(1, VidaMaxima);
		_vida = _vidaMaxima;
		if (NaFlorestaAposFuga)
		{
			var progresso = ProgressoMissoes.Atual;
			_vida = Mathf.Clamp(progresso.VidaVinarNaFloresta, 1, _vidaMaxima);
			_combosConcluidos = Mathf.Clamp(progresso.CombosVinarNaFloresta, 0, LimitesCombo.Length);
		}
		_barraVida.MaxValue = _vidaMaxima;
		AtualizarBarraVida();
		_sprite.FrameChanged += AplicarImpacto;
		_sprite.AnimationFinished += EncerrarAtaque;
		if (RegiaoNavegacao != null)
			RegiaoNavegacao.CallDeferred(NavigationRegion2D.MethodName.BakeNavigationPolygon, NaFlorestaAposFuga);
	}

	public override void _ExitTree()
	{
		DesvincularHeroi();
	}

	public override void _UnhandledInput(InputEvent evento)
	{
		if (_vida <= 0 || _recargaGolpeHeroi > 0f || !evento.IsActionPressed("atacar"))
			return;

		foreach (Node node in GetTree().GetNodesInGroup("herois"))
		{
			if (node is not MovimentoHeroi heroi || heroi.Vida <= 0 || heroi.EmDialogo
				|| heroi.EmInventario || heroi.Coletando || !PodeAtingir(heroi, AlcanceGolpeHeroi))
				continue;

			GetViewport().SetInputAsHandled();
			ReceberGolpe(heroi);
			return;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_vida <= 0 || IsQueuedForDeletion())
			return;

		_recargaGolpeHeroi = Mathf.Max(0f, _recargaGolpeHeroi - (float)delta);
		_tempoAteProximoAtaque = Mathf.Max(0f, _tempoAteProximoAtaque - (float)delta);
		_tempoAteProximaMagia = Mathf.Max(0f, _tempoAteProximaMagia - (float)delta);
		if (_sprite.Animation == Attack || _sprite.Animation == Magic || _sprite.Animation == Ultimate)
			return;

		if (!TemAlvo())
		{
			RegenerarVida((float)delta);
			if (NaFlorestaAposFuga && RegiaoNavegacao != null)
				Patrulhar((float)delta);
			else
				Parar();
			return;
		}

		if (_ultimatePendente)
		{
			_ultimatePendente = false;
			_direcaoUltimate = GlobalPosition.DirectionTo(_heroi.GlobalPosition);
			if (_direcaoUltimate.IsZeroApprox())
				_direcaoUltimate = _sprite.FlipH ? Vector2.Left : Vector2.Right;
			IniciarAtaque(Ultimate);
			return;
		}

		if (_tempoAteProximoAtaque <= 0f && AtingiuLimiteDoCombo()
			&& (_combosConcluidos > 0 || _tempoAteProximaMagia <= 0f)
			&& PodeAtingir(_heroi, AlcanceMagia))
		{
			_ultimatePendente = true;
			IniciarMagia();
			return;
		}

		if (PodeAtingir(_heroi, AlcanceAtaque))
		{
			if (_tempoAteProximoAtaque <= 0f)
				IniciarAtaque(Attack);
			else
				Parar();
			return;
		}

		if (_tempoAteProximoAtaque <= 0f && _tempoAteProximaMagia <= 0f && PodeAtingir(_heroi, AlcanceMagia))
		{
			IniciarMagia();
			return;
		}

		Perseguir((float)delta);
	}

	private bool AtingiuLimiteDoCombo()
	{
		if (_combosConcluidos >= LimitesCombo.Length)
			return false;

		long limite = System.Math.Max(1L, _vidaMaxima * (long)LimitesCombo[_combosConcluidos] / 100L);
		return _vida <= limite;
	}

	private void ReceberGolpe(MovimentoHeroi heroi)
	{
		if (_heroi != heroi)
		{
			DesvincularHeroi();
			_heroi = heroi;
			_heroi.VidaEsgotada += AoHeroiSerDerrotado;
			_rotaDefinida = false;
		}

		_foiAtacado = true;
		_tempoForaDeCombate = 0f;
		_recargaGolpeHeroi = IntervaloGolpeHeroi;
		int vidaMinima = _combosConcluidos < LimitesCombo.Length ? 1 : 0;
		_vida = Mathf.Max(vidaMinima, _vida - (1 + heroi.Ataque));
		AtualizarBarraVida();
		if (_vida > 0)
		{
			SalvarNaFloresta();
			return;
		}

		if (FogeParaFloresta || NaFlorestaAposFuga)
			ProgressoMissoes.Atual.RegistrarDerrotaVinar();
		DesvincularHeroi();
		Velocity = Vector2.Zero;
		if (_sprite.Animation == Magic && !_impactoAplicado)
		{
			foreach (AnimationPlayer efeito in _efeitosMagia)
			{
				if (IsInstanceValid(efeito))
					efeito.GetParent().QueueFree();
			}
		}
		GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_sprite.Play(Death);
		QueueRedraw();
	}

	public void RegistrarRetiradaDoHeroi()
	{
		if (FogeParaFloresta && _foiAtacado && _vida > 0)
			ProgressoMissoes.Atual.RegistrarVinarNaFloresta(_vida, _combosConcluidos);
	}

	private void AoHeroiSerDerrotado()
	{
		DesvincularHeroi();
		_rotaDefinida = false;
		_pausaPatrulha = PausaPatrulha;
		_ultimatePendente = false;
		Parar();
		QueueRedraw();

		foreach (Node filho in GetChildren())
		{
			if (filho is ProjetilUltimateVinar)
				filho.QueueFree();
		}
	}

	private void DesvincularHeroi()
	{
		if (IsInstanceValid(_heroi))
			_heroi.VidaEsgotada -= AoHeroiSerDerrotado;
		_heroi = null;
	}

	private void SalvarNaFloresta()
	{
		if (NaFlorestaAposFuga)
			ProgressoMissoes.Atual.RegistrarVinarNaFloresta(_vida, _combosConcluidos);
	}

	private void RegenerarVida(float delta)
	{
		if (_vida >= _vidaMaxima)
			return;

		_tempoForaDeCombate += delta;
		if (_tempoForaDeCombate < IntervaloRegeneracao)
			return;

		_tempoForaDeCombate = 0f;
		_vida++;
		AtualizarBarraVida();
		SalvarNaFloresta();
	}

	private void AtualizarBarraVida()
	{
		_barraVida.Value = _vida;
		_textoVida.Text = $"Vinar  {_vida}/{_barraVida.MaxValue:0} HP";
	}

	private void Perseguir(float delta)
	{
		Vector2 destino = _heroi.GlobalPosition;
		Vector2 proximoPonto = destino;
		if (RegiaoNavegacao != null)
		{
			if (!NavegacaoDisponivel())
			{
				Parar();
				return;
			}

			if (!_rotaDefinida || _navegacao.TargetPosition.DistanceTo(destino) > DistanciaParaRecalcularRota)
			{
				_navegacao.TargetPosition = destino;
				_rotaDefinida = true;
			}

			proximoPonto = _navegacao.GetNextPathPosition();
		}

		MoverAte(proximoPonto, delta, Velocidade);
	}

	private bool NavegacaoDisponivel()
	{
		return RegiaoNavegacao.NavigationPolygon.GetPolygonCount() > 0
			&& NavigationServer2D.MapGetIterationId(_navegacao.GetNavigationMap()) != 0;
	}

	private void Patrulhar(float delta)
	{
		if (!NavegacaoDisponivel())
		{
			Parar();
			return;
		}

		if (!_rotaDefinida)
		{
			_pausaPatrulha -= delta;
			if (_pausaPatrulha > 0f)
				return;

			_navegacao.TargetPosition = NavigationServer2D.MapGetRandomPoint(
				_navegacao.GetNavigationMap(), _navegacao.NavigationLayers, true);
			_rotaDefinida = true;
		}

		Vector2 proximoPonto = _navegacao.GetNextPathPosition();
		if (!_navegacao.IsNavigationFinished() && MoverAte(proximoPonto, delta, Velocidade * 0.4f)
			&& GetPositionDelta().LengthSquared() > 0.01f)
			return;

		_rotaDefinida = false;
		_pausaPatrulha = PausaPatrulha;
		Parar();
	}

	private bool MoverAte(Vector2 ponto, float delta, float velocidade)
	{
		Vector2 deslocamento = ponto - GlobalPosition;
		if (deslocamento.LengthSquared() < 1f)
		{
			Parar();
			return false;
		}

		VirarPara(deslocamento);
		_sprite.Play(Run);
		Velocity = (deslocamento / delta).LimitLength(velocidade);
		MoveAndSlide();
		return true;
	}

	private void VirarPara(Vector2 direcao)
	{
		if (!Mathf.IsZeroApprox(direcao.X))
			_sprite.FlipH = direcao.X < 0f;
	}

	private bool PodeAtingir(MovimentoHeroi heroi, float alcance)
	{
		if (GlobalPosition.DistanceSquaredTo(heroi.GlobalPosition) > alcance * alcance)
			return false;

		var consulta = PhysicsRayQueryParameters2D.Create(
			GlobalPosition, heroi.GlobalPosition, CollisionMask, [GetRid(), heroi.GetRid()]);
		return GetWorld2D().DirectSpaceState.IntersectRay(consulta).Count == 0;
	}

	private bool TemAlvo()
	{
		return IsInstanceValid(_heroi) && !_heroi.IsQueuedForDeletion() && _heroi.Vida > 0;
	}

	private void Parar()
	{
		Velocity = Vector2.Zero;
		_sprite.Play(Idle);
	}

	private void IniciarAtaque(StringName animacao)
	{
		Velocity = Vector2.Zero;
		VirarPara(_heroi.GlobalPosition - GlobalPosition);
		_impactoAplicado = false;
		_sprite.Play(animacao);
		QueueRedraw();
	}

	private void IniciarMagia()
	{
		Vector2 alvo = _heroi.GlobalPosition;
		for (int i = 0; i < FormacaoMagia.Length; i++)
		{
			_centrosMagia[i] = alvo + FormacaoMagia[i] * (RaioMagia * EspacamentoMagiaEmRaios);
			_efeitosMagia[i] = ConjurarEfeitoMagia(_centrosMagia[i]);
		}
		_tempoAteProximaMagia = RecargaMagia;
		IniciarAtaque(Magic);
	}

	private AnimationPlayer ConjurarEfeitoMagia(Vector2 centro)
	{
		if (CenaEfeitoMagia is null)
			return null;

		var efeito = CenaEfeitoMagia.Instantiate<Sprite2D>();
		var animacao = efeito.GetNode<AnimationPlayer>("AnimationPlayer");
		animacao.AnimationFinished += nome =>
		{
			if (nome == ImpactoMagia)
				efeito.QueueFree();
		};

		AddChild(efeito);
		efeito.GlobalPosition = centro;
		float larguraQuadro = (float)efeito.Texture.GetWidth() / efeito.Hframes;
		efeito.GlobalScale = Vector2.One * (2f * RaioMagia / larguraQuadro);
		animacao.Play("conjurar");
		return animacao;
	}

	private void AplicarImpacto()
	{
		StringName animacao = _sprite.Animation;
		bool magia = animacao == Magic;
		bool ultimate = animacao == Ultimate;
		if (_vida <= 0 || _impactoAplicado || (!magia && !ultimate && animacao != Attack)
			|| _sprite.Frame != (magia ? 2 : 3))
			return;

		_impactoAplicado = true;
		QueueRedraw();
		if (ultimate)
		{
			_combosConcluidos++;
			SalvarNaFloresta();
			if (CenaEfeitoMagia is not null)
			{
				var projetil = CenaEfeitoMagia.Instantiate<ProjetilUltimateVinar>();
				AddChild(projetil);
				projetil.Lancar(this, _direcaoUltimate, DanoUltimate);
			}
			return;
		}

		if (magia)
		{
			bool acertou = AplicarBombardeio();
			if (_combosConcluidos == 0 && !acertou)
				_ultimatePendente = false;
		}
		else if (TemAlvo() && PodeAtingir(_heroi, AlcanceAtaque))
			_heroi.ReceberGolpe(DanoAtaque);
	}

	private bool AplicarBombardeio()
	{
		foreach (AnimationPlayer efeito in _efeitosMagia)
		{
			if (IsInstanceValid(efeito))
				efeito.Play(ImpactoMagia);
		}

		if (!TemAlvo() || !PodeAtingir(_heroi, AlcanceMagia + RaioMagia * (EspacamentoMagiaEmRaios + 1f)))
			return false;

		foreach (Vector2 centro in _centrosMagia)
		{
			if (_heroi.GlobalPosition.DistanceSquaredTo(centro) <= RaioMagia * RaioMagia)
				return _heroi.ReceberGolpe(DanoMagia, FatorVelocidadeMagia, DuracaoLentidaoMagia);
		}
		return false;
	}

	public override void _Draw()
	{
		if (_sprite is null || (_sprite.Animation != Magic && _sprite.Animation != Ultimate))
			return;

		if (_sprite.Animation == Ultimate)
		{
			if (!_impactoAplicado)
			{
				Vector2 fim = ToLocal(GlobalPosition + _direcaoUltimate * AlcanceParaLancarUltimate);
				DrawLine(Vector2.Zero, fim, new Color(0.74f, 0.39f, 1f, 0.65f), 4f, true);
			}
			return;
		}

		if (_impactoAplicado && CenaEfeitoMagia is not null)
			return;

		float raio = RaioMagia;
		foreach (Vector2 centro in _centrosMagia)
		{
			Vector2 local = ToLocal(centro);
			DrawCircle(local, raio, new Color(0.62f, 0.19f, 0.95f, _impactoAplicado ? 0.28f : 0.14f));
			DrawArc(local, raio, 0f, Mathf.Tau, 64, new Color(0.74f, 0.39f, 1f, 0.85f), 3f, true);
		}
	}

	private void EncerrarAtaque()
	{
		if (_vida <= 0)
		{
			_barraVida.Hide();
			return;
		}

		_tempoAteProximoAtaque = IntervaloEntreAtaques;
		Parar();
		QueueRedraw();
	}
}
