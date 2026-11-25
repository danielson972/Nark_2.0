using Godot;

public partial class DialogoNpc : Node2D
{
	[Export] public string Nome { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Fala { get; set; } = "";
	[Export] public string[] Escolhas { get; set; } = [];
	[Export] public string[] Respostas { get; set; } = [];

	public override void _Ready()
	{
		AddToGroup("dialogo_npc");
	}

	public virtual void OlharPara(Vector2 posicao) { }
}
