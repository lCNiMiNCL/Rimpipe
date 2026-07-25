namespace RimPipe;

/// <summary>
/// Mapping 上流体怎么决定流向。
/// <list type="bullet">
/// <item><b>Equalize</b>（默认）：按压力/量差往低的一侧流，两边会逐渐拉平。</item>
/// <item><b>Forced</b>：泵用的模式。方向由 forcedFromA 写死，尽量按 maxFlowRate 单向抽送，
/// 即使目标侧已经更满也会继续抽（所以能做出「逆压差」抽送）。</item>
/// </list>
/// </summary>
public enum FlowDriveMode : byte
{
	Equalize = 0,
	Forced = 1
}
