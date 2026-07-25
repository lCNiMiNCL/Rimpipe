using System.Collections.Generic;
using Verse;

namespace RimPipe;

public class CompProperties_PipeCell : CompProperties
{
	/// <summary>HitPoints/MaxHitPoints 低于此值时自动 breached（Bridge-A 默认 0.5）。</summary>
	public float breachBelowHitPointsPercent = 0.5f;

	/// <summary>满血且未 Breakdown 时自动清 breached。</summary>
	public bool clearBreachOnRepaired = true;

	/// <summary>收到 CompBreakdownable「Breakdown」信号时设 breached。</summary>
	public bool breachOnBreakdown = true;

	public CompProperties_PipeCell()
	{
		compClass = typeof(CompPipeCell);
	}

	public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
	{
		foreach (string e in base.ConfigErrors(parentDef))
		{
			yield return e;
		}
		if (breachBelowHitPointsPercent < 0f || breachBelowHitPointsPercent > 1f)
		{
			yield return "CompProperties_PipeCell.breachBelowHitPointsPercent 须在 [0,1]。";
		}
	}
}
