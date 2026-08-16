using System.Collections.Generic;

namespace RimPipe;

/// <summary>
/// 运行时的「这个反应釜绑了哪套配方、哪些入/出腔」。只活在内存里，不写进存档。
/// 正式釜 Comp 会在 Spawn 时注册；Debug / API 也可以直接注册一条来测。
/// </summary>
public class ChemReactorBinding
{
	private PipeReactionDef? _reaction;
	private List<Container> _inputs = new List<Container>();
	private List<Container> _outputs = new List<Container>();

	/// <summary>关则 n=0。P3 接开关/电力。</summary>
	private bool _enabled = true;

	/// <summary>当前 mixRatio（氧化剂/燃料）。P2 用配方 ResolvedBaseMixRatio；P4 可调。</summary>
	private float _mixRatio;

	/// <summary>注册者键（Comp 或 Debug）；用于注销。</summary>
	private object? _owner;

	private float _lastBatchN;

	/// <summary>P4：上一批效率 η。</summary>
	private float _lastEfficiency = 1f;

	public PipeReactionDef? reaction { get => _reaction; internal set => _reaction = value; }
	internal List<Container> inputs { get => _inputs; set => _inputs = value; }
	internal List<Container> outputs { get => _outputs; set => _outputs = value; }
	public bool enabled { get => _enabled; internal set => _enabled = value; }
	public float mixRatio { get => _mixRatio; internal set => _mixRatio = value; }
	public object? owner { get => _owner; internal set => _owner = value; }
	public float lastBatchN { get => _lastBatchN; internal set => _lastBatchN = value; }
	public float lastEfficiency { get => _lastEfficiency; internal set => _lastEfficiency = value; }

	public IReadOnlyList<Container> Inputs => _inputs;
	public IReadOnlyList<Container> Outputs => _outputs;
}
