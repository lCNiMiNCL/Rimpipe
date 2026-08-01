using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimPipe;

/// <summary>
/// 换热器：建筑内部两岸 Container 之间有一条 Heat Mapping，只换热不搬量。
/// 开/关乘到换热率上（复用 maxFlowRate 字段）。关上不拆 Mapping。
/// </summary>
public class CompPipeHeatExchanger : ThingComp, IPipeInternalMappingContributor
{
	private bool isOpen = true;
	private Mapping? internalMapping;

	public CompProperties_PipeHeatExchanger Props => (CompProperties_PipeHeatExchanger)props;

	public bool IsOpen
	{
		get => isOpen;
		set
		{
			if (isOpen == value)
			{
				return;
			}
			isOpen = value;
			ApplyRateToInternalMapping();
			if (parent.Spawned)
			{
				MapComponent_PipeNetwork? net = parent.Map.GetComponent<MapComponent_PipeNetwork>();
				CompPipeNetworkMember? mem = parent.GetComp<CompPipeNetworkMember>();
				net?.WakeMember(mem, "heatExchanger");
			}
		}
	}

	public float EffectiveMaxHeatRate => isOpen ? Props.baseMaxHeatRate : 0f;

	public Mapping? InternalMapping => internalMapping;

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref isOpen, "isOpen", true);
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
	}

	/// <summary>拓扑清空重建时由 MapComponent_PipeNetwork 调用。</summary>
	public void ContributeInternalMapping(MapComponent_PipeNetwork net, HashSet<long> linkedPairs)
	{
		internalMapping = null;
		CompPipeNetworkMember? member = parent.GetComp<CompPipeNetworkMember>();
		if (!MapComponent_PipeNetwork.TryResolveContainers(member, Props.containerIndexA, Props.containerIndexB, "换热器", out Container? a, out Container? b)
			|| a == null || b == null)
		{
			return;
		}
		internalMapping = net.AddInternalMapping(
			a,
			b,
			parent as Building,
			EffectiveMaxHeatRate,
			linkedPairs,
			FlowDriveMode.Equalize,
			forcedFromA: true,
			MappingType.Heat);
	}

	public void ApplyRateToInternalMapping()
	{
		if (internalMapping != null)
		{
			internalMapping.maxFlowRate = EffectiveMaxHeatRate;
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
			defaultLabel = isOpen ? "RimPipe_Gizmo_HeatOpen".Translate() : "RimPipe_Gizmo_HeatClosed".Translate(),
			defaultDesc = "RimPipe_Gizmo_HeatDesc".Translate(),
			icon = TexCommand.DesirePower,
			isActive = () => isOpen,
			toggleAction = () => IsOpen = !isOpen
		};
	}

	public override string? CompInspectStringExtra()
	{
		string state = isOpen ? "RimPipe_State_Open".Translate() : "RimPipe_State_Closed".Translate();
		return "RimPipe_Inspect_HeatExchanger".Translate(state, EffectiveMaxHeatRate.ToString("0.#"));
	}
}
