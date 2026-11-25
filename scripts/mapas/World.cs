using Godot;

public partial class World : Camera2D
{
	[Export(PropertyHint.Range, "0.1,3,0.01")]
	public float ZoomDaVila { get; set; } = 0.65f;

	[Export] public TileMapLayer ChaoDaVila { get; set; }

	private Vector2 _tamanhoDoMapa;

	public override void _EnterTree()
	{
		GetViewport().SizeChanged += AtualizarZoom;
	}

	public override void _ExitTree()
	{
		GetViewport().SizeChanged -= AtualizarZoom;
	}

	public override void _Ready()
	{
		if (ChaoDaVila is not null && ChaoDaVila.TileSet is not null)
		{
			var celulas = ChaoDaVila.GetUsedRect();
			if (celulas.HasArea())
			{
				var meioTile = (Vector2)ChaoDaVila.TileSet.TileSize / 2f;
				var inicio = ChaoDaVila.ToGlobal(ChaoDaVila.MapToLocal(celulas.Position) - meioTile);
				var fim = ChaoDaVila.ToGlobal(ChaoDaVila.MapToLocal(celulas.End - Vector2I.One) + meioTile);

				LimitLeft = Mathf.CeilToInt(inicio.X);
				LimitTop = Mathf.CeilToInt(inicio.Y);
				LimitRight = Mathf.FloorToInt(fim.X);
				LimitBottom = Mathf.FloorToInt(fim.Y);
				_tamanhoDoMapa = new Vector2(LimitRight - LimitLeft, LimitBottom - LimitTop);
			}
		}

		AtualizarZoom();
	}

	private void AtualizarZoom()
	{
		float zoom = ZoomDaVila;
		if (_tamanhoDoMapa.X > 0 && _tamanhoDoMapa.Y > 0)
		{
			var proporcao = GetViewportRect().Size / _tamanhoDoMapa;
			zoom = Mathf.Max(zoom, Mathf.Max(proporcao.X, proporcao.Y));
		}

		Zoom = new Vector2(zoom, zoom);
	}
}
