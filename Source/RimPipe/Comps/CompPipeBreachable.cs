using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimPipe;

/// <summary>
/// 储罐一类「自己就能破」的开口：不依赖旁边有没有管道。
/// breached 为 true 时，每批 Commit 按 defaultMaxFlowRate 从本建筑自己的 Container 扣量销毁。
/// 伤害掉到阈值、或收到 Breakdown 故障信号，会经由 Bridge 把 breached 打开。
/// </summary>
public class CompPipeBreachable : ThingComp
{
	private bool breached;

	public CompProperties_PipeBreachable Props => (CompProperties_PipeBreachable)props;

	public bool Breached
	{
		get => breached;
		set
		{
			if (breached == value)
			{
				return;
			}
			breached = value;
			if (parent.Spawned)
			{
				MapComponent_PipeNetwork? net = parent.Map.GetComponent<MapComponent_PipeNetwork>();
				CompPipeNetworkMember? mem = parent.GetComp<CompPipeNetworkMember>();
				net?.WakeMember(mem, "tankBreach");
			}
		}
	}

	public bool ClearBreachOnRepaired => Props.clearBreachOnRepaired;

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref breached, "breached", false);
	}

	public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
	{
		base.PostPostApplyDamage(dinfo, totalDamageDealt);
		if (PipeBreachBridge.ShouldBreachFromHitPoints(parent, Props.breachBelowHitPointsPercent))
		{
			Breached = true;
		}
	}

	public override void ReceiveCompSignal(string signal)
	{
		base.ReceiveCompSignal(signal);
		if (Props.breachOnBreakdown && signal == PipeBreachBridge.BreakdownSignal)
		{
			Breached = true;
		}
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		foreach (Gizmo g in base.CompGetGizmosExtra())
		{
			yield return g;
		}
		if (parent.Faction != null && parent.Faction != Faction.OfPlayer)
		{
			yield break;
		}
		yield return new Command_Toggle
		{
			defaultLabel = breached ? "RimPipe_Gizmo_TankBreached".Translate() : "RimPipe_Gizmo_TankIntact".Translate(),
			defaultDesc = "RimPipe_Gizmo_TankBreachDesc".Translate(),
			icon = TexCommand.DesirePower,
			isActive = () => breached,
			toggleAction = () => Breached = !Breached
		};
	}

	public override string? CompInspectStringExtra()
	{
		return breached ? "RimPipe_Inspect_TankBreach".Translate() : "RimPipe_Inspect_Intact".Translate();
	}
}
