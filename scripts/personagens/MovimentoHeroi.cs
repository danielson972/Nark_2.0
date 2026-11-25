using Godot;

public partial class MovimentoHeroi : CharacterBody2D
{
	private const int VidaMaxima = 5;

	[Export(PropertyHint.Range, "0,500,1,or_greater")]
	public float Velocidade { get; set; } = 800f;

	public bool EmDialogo { get; set; }
	public bool EmInventario { get; set; }
	public bool Coletando { get; set; }
	public int Ataque { get; private set; }
	public int Defesa { get; private set; }
	public int Vida { get; private set; } = VidaMaxima;
	public event System.Action Derrotado;
	public event System.Action VidaEsgotada;

	private Vector2 _pontoInicial;
	private ProgressBar _barraVida;
	private Label _textoVida;
	private float _tempoDeLentidao;
	private float _fatorDeLentidao = 1f;

	public override void _Ready()
	{
		_pontoInicial = GlobalPosition;
		_barraVida = GetNodeOrNull<ProgressBar>("BarraVida");
		if (_barraVida is not null)
		{
			_barraVida.MaxValue = VidaMaxima;
			_textoVida = _barraVida.GetNode<Label>("Texto");
			AtualizarVida(Vida);
		}
	}

	public bool ReceberGolpe(int dano, float fatorVelocidade = 1f, float duracaoLentidao = 0f)
	{
		if (dano <= 0 || Vida <= 0 || EmDialogo || EmInventario)
			return false;

		AtualizarVida(Mathf.Max(0, Vida - Mathf.Max(1, dano - Defesa)));
		if (Vida > 0)
		{
			if (duracaoLentidao > 0f && fatorVelocidade < 1f)
			{
				_tempoDeLentidao = duracaoLentidao;
				_fatorDeLentidao = Mathf.Clamp(fatorVelocidade, 0f, 1f);
			}
			return true;
		}

		Velocity = Vector2.Zero;
		VidaEsgotada?.Invoke();
		if (Derrotado is not null)
		{
			Derrotado.Invoke();
			return true;
		}

		RenascerNoInicio();
		return true;
	}

	public void RenascerNoInicio()
	{
		AtualizarVida(VidaMaxima);
		_tempoDeLentidao = 0f;
		Velocity = Vector2.Zero;
		GlobalPosition = _pontoInicial;
		GetViewport().GetCamera2D()?.ResetSmoothing();
	}

	public void PosicionarNaChegada(Vector2 posicao)
	{
		_pontoInicial = posicao;
		GlobalPosition = posicao;
		Velocity = Vector2.Zero;
	}

	public void EquiparKitDaVila()
	{
		Ataque = 1;
		Defesa = 1;
	}

	public bool Curar()
	{
		if (Vida >= VidaMaxima)
			return false;

		AtualizarVida(VidaMaxima);
		return true;
	}

	private void AtualizarVida(int vida)
	{
		Vida = vida;
		if (_barraVida is null)
			return;

		_barraVida.Value = Vida;
		_textoVida.Text = $"{Vida}/{VidaMaxima} HP";
	}

	public override void _PhysicsProcess(double delta)
	{
		_tempoDeLentidao = Mathf.Max(0f, _tempoDeLentidao - (float)delta);
		if (Vida <= 0 || EmDialogo || EmInventario || Coletando)
		{
			Velocity = Vector2.Zero;
			return;
		}

		bool esquerda = Input.IsActionPressed("ui_left") || Input.IsPhysicalKeyPressed(Key.A);
		bool direita = Input.IsActionPressed("ui_right") || Input.IsPhysicalKeyPressed(Key.D);
		bool cima = Input.IsActionPressed("ui_up") || Input.IsPhysicalKeyPressed(Key.W);
		bool baixo = Input.IsActionPressed("ui_down") || Input.IsPhysicalKeyPressed(Key.S);

		var direcao = new Vector2(
			System.Convert.ToInt32(direita) - System.Convert.ToInt32(esquerda),
			System.Convert.ToInt32(baixo) - System.Convert.ToInt32(cima));

		float fator = _tempoDeLentidao > 0f ? _fatorDeLentidao : 1f;
		Velocity = direcao.Normalized() * (Velocidade * fator);
		MoveAndSlide();
	}
}
