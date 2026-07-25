using RimWorld;
using Verse;

namespace RimPipe;

/// <summary>
/// 破损桥接（Bridge-A，无 Harmony）：把游戏里的「挨打 / 故障」接到管道的 breached 状态上。
/// HP 掉到阈值以下，或收到 Breakdown 信号 → 记为破损开口；
/// 修到满血且当前没有故障 → 可以清掉破损。
/// 官方挂点：<see cref="ThingComp.PostPostApplyDamage"/>、<c>CompBreakdownable.BreakdownSignal</c>。
/// </summary>
public static class PipeBreachBridge
{
	public const string BreakdownSignal = CompBreakdownable.BreakdownSignal;

	public static bool ShouldBreachFromHitPoints(Thing thing, float breachBelowHitPointsPercent)
	{
		if (thing == null || thing.MaxHitPoints <= 0)
		{
			return false;
		}
		float pct = (float)thing.HitPoints / thing.MaxHitPoints;
		return pct < breachBelowHitPointsPercent;
	}

	public static bool CanClearBreachOnRepaired(Thing thing)
	{
		if (thing == null || thing.MaxHitPoints <= 0)
		{
			return false;
		}
		if (thing.HitPoints < thing.MaxHitPoints)
		{
			return false;
		}
		CompBreakdownable? bd = thing.TryGetComp<CompBreakdownable>();
		if (bd != null && bd.BrokenDown)
		{
			return false;
		}
		return true;
	}
}
