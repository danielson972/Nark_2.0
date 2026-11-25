using Godot;

public partial class CaixaDialogo : CanvasLayer
{
	[Export] public float Alcance { get; set; } = 135f;
	public MissaoDaVila Missao { get; set; }

	private Control _painel;
	private Label _aviso;
	private Label _nome;
	private Label _fala;
	private VBoxContainer _opcoes;
	private MovimentoHeroi _heroi;
	private DialogoNpc _proximo;
	private DialogoNpc _ativo;
	private Campones _camponesQueAbordou;
	private System.Action _aoEncerrar;
	private AcceptDialog _avisoVinar;

	public override void _Ready()
	{
		_painel = GetNode<Control>("%Painel");
		_aviso = GetNode<Label>("%Aviso");
		_nome = GetNode<Label>("%Nome");
		_fala = GetNode<Label>("%Fala");
		_opcoes = GetNode<VBoxContainer>("%Opcoes");
	}

	public void VincularHeroi(Node2D heroi)
	{
		_heroi = heroi as MovimentoHeroi;
	}

	public override void _Process(double delta)
	{
		if (!IsInstanceValid(_heroi) || _ativo is not null || _avisoVinar?.Visible == true)
			return;

		if (_heroi.EmInventario)
		{
			_aviso.Hide();
			return;
		}

		_proximo = null;
		Campones camponesProximo = null;
		float distanciaMinima = Alcance * Alcance;
		foreach (var no in GetTree().GetNodesInGroup("dialogo_npc"))
		{
			if (no is not DialogoNpc npc || !npc.IsVisibleInTree())
				continue;

			float distancia = _heroi.GlobalPosition.DistanceSquaredTo(npc.GlobalPosition);
			if (npc is Campones campones && distancia <= campones.AlcanceAbordagem * campones.AlcanceAbordagem)
				camponesProximo = campones;
			if (distancia < distanciaMinima)
			{
				distanciaMinima = distancia;
				_proximo = npc;
			}
		}
		if (camponesProximo != _camponesQueAbordou)
		{
			_camponesQueAbordou = camponesProximo;
			if (camponesProximo is not null && ProgressoMissoes.Atual.AvisoDoCamponesPendente)
			{
				Abrir(camponesProximo);
				return;
			}
		}

		_aviso.Visible = _proximo is not null;
		if (_proximo is not null)
			_aviso.Text = $"[E]  Falar com {_proximo.Nome}";
	}

	public override void _UnhandledInput(InputEvent evento)
	{
		if (_avisoVinar?.Visible == true || (IsInstanceValid(_heroi) && _heroi.EmInventario))
			return;

		if (_ativo is not null && evento.IsActionPressed("ui_cancel"))
		{
			Fechar();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!evento.IsActionPressed("interagir"))
			return;

		if (_ativo is not null && GetViewport().GuiGetFocusOwner() is Button botao && botao.GetParent() == _opcoes)
			botao.EmitSignal(BaseButton.SignalName.Pressed);
		else if (_ativo is null && IsInstanceValid(_proximo) && IsInstanceValid(_heroi))
			Abrir(_proximo);
		else
			return;

		GetViewport().SetInputAsHandled();
	}

	private void Abrir(DialogoNpc npc)
	{
		_ativo = npc;
		_heroi.EmDialogo = true;
		_aviso.Hide();
		_painel.Show();
		_nome.Text = npc.Nome;
		if (npc is Campones campones)
			_camponesQueAbordou = campones;
		npc.OlharPara(_heroi.GlobalPosition);
		var etapa = (npc as Garimpeiro)?.Conversar(MostrarAvisoVinar) ?? (npc as Campones)?.Conversar()
			?? DialogosDasMissoes.Conversar(npc.Nome) ?? Missao?.Conversar(npc);
		etapa = ComercioDaVila.Conversar(npc.Nome, etapa);
		if (etapa is not null)
		{
			MostrarEtapa(etapa);
			return;
		}

		_fala.Text = npc.Fala;
		LimparOpcoes();

		for (int i = 0; i < npc.Escolhas.Length; i++)
		{
			string resposta = i < npc.Respostas.Length ? npc.Respostas[i] : "";
			AdicionarOpcao(npc.Escolhas[i], () => MostrarResposta(resposta));
		}

		if (_opcoes.GetChildCount() == 0)
			AdicionarOpcao("Continuar", Fechar);

		_opcoes.GetChild<Button>(0).GrabFocus();
	}

	public bool AbrirUltimoPedido()
	{
		if (_ativo is not null || !IsInstanceValid(_heroi))
			return false;

		foreach (var no in GetTree().GetNodesInGroup("dialogo_npc"))
		{
			if (no is Campones campones && campones.IsVisibleInTree())
			{
				Abrir(campones);
				return true;
			}
		}
		return false;
	}

	private void MostrarEtapa(EtapaDialogo etapa)
	{
		_aoEncerrar = etapa.AoEncerrar;
		_fala.Text = etapa.Fala;
		LimparOpcoes();
		foreach (var opcao in etapa.Opcoes)
			AdicionarOpcao(opcao.Texto, () => MostrarEtapa(opcao.Escolher()));

		if (_opcoes.GetChildCount() == 0)
			AdicionarOpcao("Encerrar conversa", Fechar);

		_opcoes.GetChild<Button>(0).GrabFocus();
	}

	private void MostrarResposta(string resposta)
	{
		if (string.IsNullOrWhiteSpace(resposta))
		{
			Fechar();
			return;
		}

		_fala.Text = resposta;
		LimparOpcoes();
		AdicionarOpcao("Encerrar conversa", Fechar).GrabFocus();
	}

	private Button AdicionarOpcao(string texto, System.Action acao)
	{
		var botao = new Button
		{
			Text = texto,
			Alignment = HorizontalAlignment.Left,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		botao.Pressed += acao;
		_opcoes.AddChild(botao);
		return botao;
	}

	private void LimparOpcoes()
	{
		foreach (var opcao in _opcoes.GetChildren())
		{
			_opcoes.RemoveChild(opcao);
			opcao.QueueFree();
		}
	}

	private void Fechar()
	{
		var aoEncerrar = _aoEncerrar;
		_aoEncerrar = null;
		_painel.Hide();
		_ativo = null;
		if (IsInstanceValid(_heroi))
			_heroi.EmDialogo = false;
		aoEncerrar?.Invoke();
	}

	private void MostrarAvisoVinar()
	{
		if (_avisoVinar is null)
		{
			_avisoVinar = new AcceptDialog
			{
				Title = "Se Vinar escapar",
				Transient = true,
				Exclusive = true,
				OkButtonText = "Entendi",
				DialogText = $"• Você perde {ProgressoMissoes.PerdaFamaVinar} ponto de fama.\n"
					+ "• Algumas lojas cobram 50% a mais. Outras suspendem as vendas.\n"
					+ "• Os slimes rendem metade da prata e têm metade da chance de deixar itens.\n\n"
					+ "Derrote Vinar na Floresta Leste para tranquilizar os moradores e encerrar as penalidades."
			};
			_avisoVinar.VisibilityChanged += () =>
			{
				if (IsInstanceValid(_heroi))
					_heroi.EmDialogo = _avisoVinar.Visible;
			};
			AddChild(_avisoVinar);
		}

		_avisoVinar.PopupCentered(new Vector2I(590, 220));
	}
}
