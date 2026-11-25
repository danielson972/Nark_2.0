using Godot;

public enum TarefaVila { Lenhador, Verdureira, Agricultor, Pescador }
public enum ItemMissao { Machado, Tabuas, Erva, Isca }
public enum RecursoMineracao { Ouro, Prata, Cristal, Ferro, Cobre, Pedra }

public partial class ProgressoMissoes : Node
{
    public const int PerdaFamaVinar = 1;
    private const string Caminho = "user://missoes_vila.cfg";
    private const string Arte = "res://assets/sprites/itens_missoes/vila/";
    private readonly ConfigFile _dados = new();

    public static ProgressoMissoes Atual { get; private set; }
    public event System.Action Mudou;
    public int Prata => Ler("recompensas", "prata");
    public int Pocoes => Ler("recompensas", "pocoes");
    public int Fama => Ler("recompensas", "fama") - (CriseVinarAtiva ? PerdaFamaVinar : 0);
    public bool AjudaReconhecida => Ler("recompensas", "fama") > 0;
    public int EstadoLobo => Ler("lobo", "estado");
    public bool VinarFugiuDaCaverna => _dados.HasSectionKey("vinar", "vida");
    public int VidaVinarNaFloresta => Ler("vinar", "vida");
    public int CombosVinarNaFloresta => Ler("vinar", "combos");
    public bool VinarDerrotado => Ler("vinar", "derrotado") > 0
        || (VinarFugiuDaCaverna && VidaVinarNaFloresta == 0);
    public bool CriseVinarAtiva => VinarFugiuDaCaverna && !VinarDerrotado;
    public bool ConversouGarimpeiro => Ler("caverna", "garimpeiro") > 0;
    public bool TemRunaCaverna => Ler("caverna", "runa") > 0;
    public bool SeloCavernaRompido => Ler("caverna", "selo_rompido") > 0;

    public bool TemPicareta => Ler("ferramentas", "picareta") > 0;
    public int Ouro => Ler("recompensas", "ouro");

    public void ReceberPicareta()
    {
        if (TemPicareta)
            return;
        _dados.SetValue("ferramentas", "picareta", 1);
        Salvar();
    }

    public int QuantidadeMinerio(RecursoMineracao recurso) => recurso switch
    {
        RecursoMineracao.Ouro => Ouro,
        RecursoMineracao.Prata => Prata,
        _ => Ler("minerios", recurso.ToString())
    };

    public bool PedraMinerada(string id) => !string.IsNullOrWhiteSpace(id)
        && Ler("pedras_mineradas", id) > 0;

    public bool ReceberMineracao(string id, RecursoMineracao recurso, int quantidade)
    {
        if (!TemPicareta || quantidade <= 0 || PedraMinerada(id))
            return false;

        if (recurso is RecursoMineracao.Ouro or RecursoMineracao.Prata)
            _dados.SetValue("recompensas", recurso == RecursoMineracao.Ouro ? "ouro" : "prata",
                QuantidadeMinerio(recurso) + quantidade);
        else
            _dados.SetValue("minerios", recurso.ToString(), QuantidadeMinerio(recurso) + quantidade);
        if (!string.IsNullOrWhiteSpace(id))
            _dados.SetValue("pedras_mineradas", id, 1);
        Salvar();
        return true;
    }

    public void RegistrarConversaGarimpeiro()
    {
        if (ConversouGarimpeiro)
            return;
        _dados.SetValue("caverna", "garimpeiro", 1);
        Salvar();
    }

    public void ReceberRunaCaverna()
    {
        if (TemRunaCaverna || !Concluida(TarefaVila.Agricultor))
            return;
        _dados.SetValue("caverna", "runa", 1);
        Salvar();
    }

    public bool RomperSeloCaverna()
    {
        if (SeloCavernaRompido)
            return true;
        if (!TemRunaCaverna)
            return false;
        _dados.SetValue("caverna", "selo_rompido", 1);
        Salvar();
        return true;
    }

    public void RegistrarDerrotaVinar()
    {
        if (VinarFugiuDaCaverna)
            _dados.SetValue("vinar", "vida", 0);
        _dados.SetValue("vinar", "derrotado", 1);
        Salvar();
    }

    public int PrecoPocao(int precoBase) => CriseVinarAtiva ? (precoBase * 3 + 1) / 2 : precoBase;

    public bool ComprarPocao(int precoBase, bool fechaDuranteCrise)
    {
        int preco = PrecoPocao(precoBase);
        if ((fechaDuranteCrise && CriseVinarAtiva) || Prata < preco)
            return false;
        _dados.SetValue("recompensas", "prata", Prata - preco);
        _dados.SetValue("recompensas", "pocoes", Pocoes + 1);
        Salvar();
        return true;
    }

    public void ReceberSaqueSlime()
    {
        bool crise = CriseVinarAtiva;
        _dados.SetValue("recompensas", "prata", Prata + (crise ? 1 : 2));
        if (GD.Randf() < (crise ? 0.10f : 0.20f))
            _dados.SetValue("recompensas", "pocoes", Pocoes + 1);
        Salvar();
    }

    public void RegistrarVinarNaFloresta(int vida, int combos)
    {
        _dados.SetValue("vinar", "vida", vida);
        _dados.SetValue("vinar", "combos", combos);
        Salvar();
    }
    public bool LoboDerrotado => (MissaoDaVila.Estado)EstadoLobo
        is MissaoDaVila.Estado.LoboVencido or MissaoDaVila.Estado.Concluida;
    public bool RenunciouAoApoio => Ler("saida", "renunciou") > 0;
    public bool AceitouUltimoPedido => Ler("saida", "resposta") == 1;
    public bool RecusouUltimoPedido => Ler("saida", "resposta") == 2;
    public bool AvisoDoCamponesPendente => !TodasConcluidas && !RenunciouAoApoio
        && Ler("saida", "resposta") == 0;
    public int Rastros => Ler("agricultor", "rastros");
    public bool PassagemFechada => Ler("agricultor", "passagem") > 0;
    public bool TodasConcluidas => Concluida(TarefaVila.Lenhador) && Concluida(TarefaVila.Verdureira)
        && Concluida(TarefaVila.Agricultor) && Concluida(TarefaVila.Pescador);

    public override void _Ready()
    {
        Atual = this;
        var erro = _dados.Load(Caminho);
        if (erro != Error.Ok && erro != Error.FileNotFound)
            GD.PushWarning($"Não foi possível carregar as missões: {erro}.");
    }

    public bool Aceita(TarefaVila tarefa) => LoboDerrotado && Ler("tarefas", tarefa.ToString()) == 1;
    public bool Concluida(TarefaVila tarefa) => Ler("tarefas", tarefa.ToString()) == 2;
    public bool Coletado(string id) => _dados.HasSectionKey("coletados", id);

    public void Aceitar(TarefaVila tarefa)
    {
        if (!LoboDerrotado || Ler("tarefas", tarefa.ToString()) != 0)
            return;

        _dados.SetValue("tarefas", tarefa.ToString(), 1);
        Salvar();
    }

    public bool Coletar(string id, ItemMissao item)
    {
        if (!Aceita(TarefaDoItem(item)) || Coletado(id))
            return false;

        _dados.SetValue("coletados", id, (int)item);
        Salvar();
        return true;
    }

    public int NaMochila(ItemMissao item)
    {
        if (Concluida(TarefaDoItem(item)) || !_dados.HasSection("coletados"))
            return 0;

        int total = 0;
        foreach (string id in _dados.GetSectionKeys("coletados"))
        {
            if (Ler("coletados", id) == (int)item)
                total++;
        }
        return total;
    }

    public void ExaminarRastro(int indice)
    {
        if (!Aceita(TarefaVila.Agricultor) || indice != Rastros)
            return;

        _dados.SetValue("agricultor", "rastros", indice + 1);
        Salvar();
    }

    public void FecharPassagem(int quantidadeRastros)
    {
        if (!Aceita(TarefaVila.Agricultor) || PassagemFechada || Rastros < quantidadeRastros
            || !Concluida(TarefaVila.Lenhador))
            return;

        _dados.SetValue("agricultor", "passagem", 1);
        Salvar();
    }

    public bool PodeEntregar(TarefaVila tarefa) => Aceita(tarefa) && (tarefa switch
    {
        TarefaVila.Lenhador => NaMochila(ItemMissao.Machado) >= 1 && NaMochila(ItemMissao.Tabuas) >= 1,
        TarefaVila.Verdureira => NaMochila(ItemMissao.Erva) >= 1,
        TarefaVila.Agricultor => PassagemFechada,
        TarefaVila.Pescador => NaMochila(ItemMissao.Isca) >= 3,
        _ => false
    });

    public bool Entregar(TarefaVila tarefa)
    {
        if (!PodeEntregar(tarefa))
            return false;

        _dados.SetValue("tarefas", tarefa.ToString(), 2);
        if (tarefa == TarefaVila.Verdureira)
            _dados.SetValue("recompensas", "pocoes", Pocoes + 5);
        if (tarefa is TarefaVila.Agricultor or TarefaVila.Pescador)
            _dados.SetValue("recompensas", "prata", Prata + 10);
        Salvar();
        return true;
    }

    public void ReconhecerAjuda()
    {
        if (!TodasConcluidas || AjudaReconhecida || RenunciouAoApoio)
            return;

        _dados.SetValue("recompensas", "fama", 1);
        Salvar();
    }

    public void ResponderCampones(bool recusou)
    {
        if (RenunciouAoApoio || TodasConcluidas)
            return;

        _dados.SetValue("saida", "resposta", recusou ? 2 : 1);
        Salvar();
    }

    public void RegistrarSaidaDaVila()
    {
        if (RenunciouAoApoio || !RecusouUltimoPedido || TodasConcluidas)
            return;

        _dados.SetValue("saida", "renunciou", 1);
        Salvar();
    }

    public void UsarPocao(MovimentoHeroi heroi)
    {
        if (Pocoes <= 0 || !IsInstanceValid(heroi) || !heroi.Curar())
            return;

        _dados.SetValue("recompensas", "pocoes", Pocoes - 1);
        Salvar();
    }

    public void RegistrarEstadoLobo(int estado, int prata = 0)
    {
        _dados.SetValue("lobo", "estado", estado);
        if (prata > 0)
            _dados.SetValue("recompensas", "prata", Prata + prata);
        Salvar();
    }

    public static TarefaVila TarefaDoItem(ItemMissao item) => item switch
    {
        ItemMissao.Erva => TarefaVila.Verdureira,
        ItemMissao.Isca => TarefaVila.Pescador,
        _ => TarefaVila.Lenhador
    };

    public static string Nome(ItemMissao item) => item switch
    {
        ItemMissao.Machado => "Machado do pai do lenhador",
        ItemMissao.Tabuas => "Tábuas resistentes",
        ItemMissao.Erva => "Erva da floresta",
        _ => "Iscas da floresta"
    };

    public static string Icone(ItemMissao item) => Arte + (item switch
    {
        ItemMissao.Machado => "machado_missao.png",
        ItemMissao.Tabuas => "tabuas_missoes.png",
        ItemMissao.Erva => "erva_missao.png",
        _ => "iscas.png"
    });

    private int Ler(string secao, string chave) => _dados.GetValue(secao, chave, 0).AsInt32();

    private void Salvar()
    {
        var erro = _dados.Save(Caminho);
        if (erro != Error.Ok)
            GD.PushError($"Não foi possível salvar as missões: {erro}.");
        Mudou?.Invoke();
    }
}
