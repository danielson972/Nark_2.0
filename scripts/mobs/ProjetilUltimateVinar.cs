using Godot;

public partial class ProjetilUltimateVinar : Sprite2D
{
	private const float Velocidade = 720f;
	private const float Alcance = 600f;
	private const float DuracaoImpacto = 0.56f;

	private ShapeCast2D _colisao;
	private float _distanciaRestante;
	private int _dano;

	public override void _Ready()
	{
		SetPhysicsProcess(false);
	}

	public void Lancar(Vinar origem, Vector2 direcao, int dano)
	{
		_dano = dano;
		_distanciaRestante = Alcance;
		GlobalPosition = origem.GlobalPosition;
		GlobalRotation = direcao.Angle();
		_colisao = GetNode<ShapeCast2D>("ColisaoProjetil");
		_colisao.CollisionMask = origem.CollisionMask;
		_colisao.AddException(origem);
		var animacao = GetNode<AnimationPlayer>("AnimationPlayer");
		animacao.AnimationFinished += _ => QueueFree();
		animacao.Play("projetil");
		SetPhysicsProcess(true);
	}

	public override void _PhysicsProcess(double delta)
	{
		float passo = Mathf.Min(_distanciaRestante, Velocidade * (float)delta);
		_colisao.TargetPosition = Vector2.Right * passo;
		_colisao.ForceShapecastUpdate();

		if (_colisao.IsColliding())
		{
			GlobalPosition += GlobalTransform.X * (passo * _colisao.GetClosestCollisionSafeFraction());
			MovimentoHeroi heroi = null;
			for (int i = 0; i < _colisao.GetCollisionCount(); i++)
			{
				if (_colisao.GetCollider(i) is not MovimentoHeroi atingido)
				{
					heroi = null;
					break;
				}
				heroi = atingido;
			}
			heroi?.ReceberGolpe(_dano);
			EncerrarVoo();
			return;
		}

		GlobalPosition += GlobalTransform.X * passo;
		_distanciaRestante -= passo;
		if (_distanciaRestante <= 0)
			EncerrarVoo();
	}

	private void EncerrarVoo()
	{
		SetPhysicsProcess(false);
		var animacao = GetNode<AnimationPlayer>("AnimationPlayer");
		float velocidadeImpacto = (float)(animacao.GetAnimation("impacto").Length / DuracaoImpacto);
		animacao.Play("impacto", customSpeed: velocidadeImpacto);
	}
}
