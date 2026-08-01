using System.Collections.Generic;

namespace RimPipe;

/// <summary>
/// 运行时的「这个反应釜绑了哪套配方、哪些入/出腔」。只活在内存里，不写进存档。
/// 正式釜 Comp 会在 Spawn 时注册；Debug / API 也可以直接注册一条来测。
/// </summary>
public class ChemReactorBinding
{
	public PipeReactionDef? reaction;
	public List<Container> inputs = new List<Container>();
	public List<Container> outputs = new List<Container>();

	/// <summary>关则 n=0。P3 接开关/电力。</summary>
	public bool enabled = true;

	/// <summary>当前 mixRatio（氧化剂/燃料）。P2 用配方 ResolvedBaseMixRatio；P4 可调。</summary>
	public float mixRatio;

	/// <summary>注册者键（Comp 或 Debug）；用于注销。</summary>
	public object? owner;

	public float lastBatchN;

	/// <summary>P4：上一批效率 η。</summary>
	public float lastEfficiency = 1f;
}
