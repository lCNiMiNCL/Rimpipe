namespace RimPipe;

/// <summary>
/// 管网休眠状态。用来省 tick：没活干就少算一点。
/// 这个值不写进存档；读档后会按当前管网情况重新判断。
/// 4.1 是整张地图共用一个状态；4.2 起每个连通网（netId）各自一份。
/// <list type="bullet">
/// <item><b>Busy</b>：有流动/换热/泄漏等活要干，正常跑 Accumulate，Commit 也跑完整路径。</item>
/// <item><b>AmbientOnly</b>：暂时没有流量需求，跳过 Accumulate；Commit 仍会处理泄漏和环境散热（Amb）。</item>
/// <item><b>FullyQuiet</b>：连泄漏和散热都不需要时，连 Commit 主体也跳过，只在批次结束时做一次很轻的状态检查。</item>
/// </list>
/// </summary>
public enum PipeNetworkSleepState : byte
{
	Busy = 0,
	AmbientOnly = 1,
	FullyQuiet = 2
}
