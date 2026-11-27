using Godot;

public partial class Javali : CharacterBody2D
{
	[Export(PropertyHint.Range, "0.1,120,0.1,or_greater")]
	public float IntervaloPasseio { get; set; } = 5f;
	[Export] public float DistanciaPasseio { get; set; } = 96f;
	[Export] public float Velocidade { get; set; } = 60f;
	[Export] public Vector2 DirecaoPasseio { get; set; } = Vector2.Right;
	[Export] public float AlcanceDeteccao { get; set; } = 260f;
	[Export] public float DistanciaDesistencia { get; set; } = 480f;
	[Export] public float RaioPerseguicao { get; set; } = 3000;
	[Export] public float VelocidadePerseguicao { get; set; } = 180;
	[Export] public float AlcanceInvestida { get; set; } = 220f;
	[Export] public float VelocidadeInvestida { get; set; } = 650f;
	[Export] public float IntervaloAtaque { get; set; } = 1.8f;
	[Export] public int DanoAtaque { get; set; } = 1;
	[Export] public float DistanciaEmpurrao { get; set; } = 200f;

	private static readonly string[] SufixosDirecao =
	[
		"_direita", "_frente_direita", "", "_frente_esquerda",
		"_esquerda", "_costa_esquerda", "_costa", "_costa_direita"
	];
	private readonly RandomNumberGenerator _sorteio = new();
	private readonly Vector2[] _pontosPasseio = new Vector2[8];
	private AnimationPlayer _animacao;
	private Vector2 _origem;
	private Vector2 _destino;
	private Vector2 _direcaoInvestida;
	private MovimentoHeroi _alvo;
	private double _tempoDesdeMudanca;
	private double _recargaAtaque;
	private double _tempoInvestida;
	private bool _movendo;
	private bool _atacando;
	private bool _investidaBloqueada;
	private bool _retornando;
	private int _proximoPonto;
	private string _direcao = "";

	public override void _Ready()
	{
		_origem = GlobalPosition;
		_animacao = GetNode<AnimationPlayer>("Sprite2D/AnimationPlayer");
		_sorteio.Randomize();
		SortearPercurso();
		Parar();
	}

	public override void _PhysicsProcess(double delta)
	{
		Velocity = Vector2.Zero;
		_recargaAtaque = System.Math.Max(0, _recargaAtaque - delta);
		if (_alvo is not null && !PodePerseguir(_alvo))
			AbandonarPerseguicao();

		if (_atacando)
		{
			AtualizarInvestida(delta);
			return;
		}
		// Reavalia o jogador mesmo durante o retorno: combate tem prioridade sobre passeio.
		_alvo ??= EncontrarHeroi();
		if (_alvo is not null)
		{
			_retornando = false;
			_movendo = false;
			Vector2 deslocamento = _alvo.GlobalPosition - GlobalPosition;
			if (_recargaAtaque <= 0 && deslocamento.Length() <= Mathf.Max(1, AlcanceInvestida)
				&& CaminhoLivre(_alvo))
			{
				IniciarInvestida(deslocamento);
				return;
			}
			MoverAte(_alvo.GlobalPosition, VelocidadePerseguicao, delta);
			return;
		}
		if (_retornando)
		{
			if (MoverAte(_origem, Velocidade, delta))
			{
				_retornando = false;
				_tempoDesdeMudanca = 0;
				SortearPercurso();
				Parar();
			}
			return;
		}

		_tempoDesdeMudanca += delta;
		if (!_movendo)
		{
			if (_tempoDesdeMudanca < Mathf.Max(0.1f, IntervaloPasseio)) return;
			_destino = _pontosPasseio[_proximoPonto];
			_tempoDesdeMudanca = 0;
			_movendo = true;
		}
		if (MoverAte(_destino, Velocidade, delta))
		{
			_proximoPonto++;
			if (_proximoPonto == _pontosPasseio.Length) SortearPercurso();
			Parar();
		}
		else if (Velocity.IsZeroApprox()) Parar();
	}

	private static bool AlvoValido(MovimentoHeroi heroi) => IsInstanceValid(heroi)
		&& heroi.IsInsideTree() && !heroi.IsQueuedForDeletion() && heroi.Vida > 0
		&& !heroi.EmDialogo && !heroi.EmInventario;

	private bool PodePerseguir(MovimentoHeroi heroi) => AlvoValido(heroi)
		&& GlobalPosition.DistanceTo(heroi.GlobalPosition) <= Mathf.Max(1, DistanciaDesistencia)
		&& _origem.DistanceTo(heroi.GlobalPosition) <= Mathf.Max(1, RaioPerseguicao)
		&& _origem.DistanceTo(GlobalPosition) <= Mathf.Max(1, RaioPerseguicao);

	private MovimentoHeroi EncontrarHeroi()
	{
		MovimentoHeroi alvo = null;
		float distancia = Mathf.Max(1, AlcanceDeteccao);
		foreach (Node no in GetTree().GetNodesInGroup("herois"))
		{
			if (no is not MovimentoHeroi heroi || !PodePerseguir(heroi)) continue;
			float ateHeroi = GlobalPosition.DistanceTo(heroi.GlobalPosition);
			if (ateHeroi <= distancia && CaminhoLivre(heroi))
			{
				alvo = heroi;
				distancia = ateHeroi;
			}
		}
		return alvo;
	}

	private bool CaminhoLivre(MovimentoHeroi heroi)
	{
		var raio = PhysicsRayQueryParameters2D.Create(GlobalPosition, heroi.GlobalPosition,
			CollisionMask, [GetRid(), heroi.GetRid()]);
		return GetWorld2D().DirectSpaceState.IntersectRay(raio).Count == 0;
	}

	private void AbandonarPerseguicao()
	{
		_alvo = null;
		_atacando = false;
		_retornando = true;
		Parar();
	}

	private void IniciarInvestida(Vector2 direcao)
	{
		_direcaoInvestida = direcao.IsZeroApprox() ? Vector2.Down : direcao.Normalized();
		AtualizarDirecao(_direcaoInvestida);
		_atacando = true;
		_investidaBloqueada = false;
		_tempoInvestida = 0;
		_recargaAtaque = Mathf.Max(0.8f, IntervaloAtaque);
		_animacao.Play($"atack_carreira{_direcao}");
		_animacao.Advance(0);
	}

	private void AtualizarInvestida(double delta)
	{
		double anterior = _tempoInvestida;
		_tempoInvestida += delta;
		// Quadros 0–1 preparam, 2–5 arrancam e 6–7 recuperam.
		double tempoMovimento = System.Math.Max(0,
			System.Math.Min(_tempoInvestida, 0.6) - System.Math.Max(anterior, 0.2));
		if (tempoMovimento > 0 && !_investidaBloqueada)
		{
			Vector2 passo = _direcaoInvestida * Mathf.Max(1, VelocidadeInvestida) * (float)tempoMovimento;
			// A investida também respeita o limite territorial.
			Vector2 proximo = GlobalPosition + passo;
			if (_origem.DistanceTo(proximo) > Mathf.Max(1, RaioPerseguicao))
			{
				AbandonarPerseguicao();
				return;
			}
			Velocity = _direcaoInvestida * Mathf.Max(1, VelocidadeInvestida);
			var colisao = MoveAndCollide(passo);
			if (colisao is not null)
			{
				_investidaBloqueada = true;
				Velocity = Vector2.Zero;
				if (colisao.GetCollider() is MovimentoHeroi atingido && AlvoValido(atingido))
					atingido.ReceberInvestida(DanoAtaque, _direcaoInvestida, DistanciaEmpurrao);
			}
		}
		if (_tempoInvestida >= 0.8)
		{
			_atacando = false;
			Parar();
		}
	}

	private bool MoverAte(Vector2 destino, float velocidade, double delta)
	{
		Vector2 deslocamento = destino - GlobalPosition;
		if (deslocamento.LengthSquared() <= 0.01f)
		{
			GlobalPosition = destino;
			Velocity = Vector2.Zero;
			return true;
		}
		AtualizarDirecao(deslocamento);
		_animacao.Play($"andando{_direcao}");
		Velocity = deslocamento.Normalized() * Mathf.Max(1, velocidade);
		var colisao = MoveAndCollide(deslocamento.LimitLength(Mathf.Max(1, velocidade) * (float)delta));
		if (colisao is not null)
		{
			Velocity = Vector2.Zero;
			_animacao.Play($"parado{_direcao}");
		}
		return GlobalPosition.DistanceSquaredTo(destino) <= 0.01f;
	}

	private void AtualizarDirecao(Vector2 direcao)
	{
		int setor = Mathf.PosMod(Mathf.RoundToInt(direcao.Angle() / (Mathf.Pi / 4f)), 8);
		_direcao = SufixosDirecao[setor];
	}

	private void SortearPercurso()
	{
		int[] direcoes = [0, 1, 2, 3, 4, 5, 6, 7];
		for (int i = direcoes.Length - 1; i > 0; i--)
		{
			int outro = _sorteio.RandiRange(0, i);
			(direcoes[i], direcoes[outro]) = (direcoes[outro], direcoes[i]);
		}
		int inicio = DirecaoPasseio.IsZeroApprox() ? 0
			: Mathf.RoundToInt(DirecaoPasseio.Angle() / (Mathf.Pi / 4f));
		Vector2 ponto = _origem;
		for (int i = 0; i < direcoes.Length; i++)
		{
			int setor = Mathf.PosMod(direcoes[i] + inicio, 8);
			ponto += Vector2.FromAngle(setor * Mathf.Pi / 4f) * Mathf.Max(1f, DistanciaPasseio);
			_pontosPasseio[i] = ponto;
		}
		_pontosPasseio[^1] = _origem;
		_proximoPonto = 0;
	}

	private void Parar()
	{
		_movendo = false;
		Velocity = Vector2.Zero;
		_animacao.Play($"parado{_direcao}");
		_animacao.Advance(0);
	}
}
