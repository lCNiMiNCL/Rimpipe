using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe;

/// <summary>
/// 阀门：建筑内部两岸 Container 之间有一条 Mapping。
/// 开着时有效流量 = baseRate；关上时有效流量为 0。
/// 关阀只改 rate，不拆掉 Mapping，免得整网拓扑跟着抖。
/// </summary>
public class CompPipeValve : ThingComp, IPipeInternalMappingContributor
{
	private bool isOpen = true;
	private Mapping? internalMapping;

	public CompProperties_PipeValve Props => (CompProperties_PipeValve)props;

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
				net?.WakeMember(mem, "valve");
			}
		}
	}

	public float EffectiveMaxFlowRate => isOpen ? Props.baseMaxFlowRate : 0f;

	public Mapping? InternalMapping => internalMapping;

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref isOpen, "isOpen", true);
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		// 拓扑重建后由 MapComp 调用 ContributeInternalMapping
	}

	/// <summary>拓扑清空重建时由 MapComponent_PipeNetwork 调用。</summary>
	public void ContributeInternalMapping(MapComponent_PipeNetwork net, HashSet<long> linkedPairs)
	{
		internalMapping = null;
		CompPipeNetworkMember? member = parent.GetComp<CompPipeNetworkMember>();
		if (member == null || !parent.Spawned)
		{
			return;
		}
		int ia = Props.containerIndexA;
		int ib = Props.containerIndexB;
		if (ia < 0 || ib < 0 || ia >= member.Containers.Count || ib >= member.Containers.Count)
		{
			Log.Error($"[RimPipe] 阀门 {parent.LabelCap} 容器索引越界 A={ia} B={ib} count={member.Containers.Count}");
			return;
		}
		Container a = member.Containers[ia];
		Container b = member.Containers[ib];
		internalMapping = net.AddInternalMapping(a, b, parent as Building, EffectiveMaxFlowRate, linkedPairs);
	}

	public void ApplyRateToInternalMapping()
	{
		if (internalMapping != null)
		{
			internalMapping.maxFlowRate = EffectiveMaxFlowRate;
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
		Command_Toggle cmd = new Command_Toggle
		{
			defaultLabel = isOpen ? "RimPipe_Gizmo_ValveOpen".Translate() : "RimPipe_Gizmo_ValveClosed".Translate(),
			defaultDesc = "RimPipe_Gizmo_ValveDesc".Translate(),
			icon = TexCommand.DesirePower,
			isActive = () => isOpen,
			toggleAction = () => IsOpen = !isOpen
		};
		yield return cmd;
	}

	public override string? CompInspectStringExtra()
	{
		string state = isOpen ? "RimPipe_State_Open".Translate() : "RimPipe_State_Closed".Translate();
		return "RimPipe_Inspect_Valve".Translate(state, EffectiveMaxFlowRate.ToString("0.#"));
	}
}
