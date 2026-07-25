using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimPipe;

/// <summary>管道格：仅拓扑；breached=通向大气的孔（路径 Mapping.leakOpen）。Bridge-A 含伤害/Breakdown。</summary>
public class CompPipeCell : ThingComp
{
	private bool breached;

	public CompProperties_PipeCell Props => (CompProperties_PipeCell)props;

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
				parent.Map.GetComponent<MapComponent_PipeNetwork>()?.NotifyBreachChanged();
			}
		}
	}

	public bool ClearBreachOnRepaired => Props.clearBreachOnRepaired;

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref breached, "breached", false);
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		parent.Map.GetComponent<MapComponent_PipeNetwork>()?.RegisterPipeCell(this);
	}

	public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
	{
		map.GetComponent<MapComponent_PipeNetwork>()?.DeregisterPipeCell(this, mode);
		base.PostDeSpawn(map, mode);
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
			defaultLabel = breached ? "RimPipe_Gizmo_PipeBreached".Translate() : "RimPipe_Gizmo_PipeIntact".Translate(),
			defaultDesc = "RimPipe_Gizmo_PipeBreachDesc".Translate(),
			icon = TexCommand.DesirePower,
			isActive = () => breached,
			toggleAction = () => Breached = !Breached
		};
	}

	public override string? CompInspectStringExtra()
	{
		string state = breached ? "RimPipe_Inspect_TankBreach".Translate() : "RimPipe_Inspect_Intact".Translate();
		return "RimPipe_Inspect_PipeCell".Translate(state);
	}
}
