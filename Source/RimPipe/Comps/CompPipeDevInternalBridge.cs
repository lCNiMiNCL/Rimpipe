using System.Collections.Generic;
using Verse;

namespace RimPipe;

/// <summary>
/// 仅开发用的 ExtHook 自测件：建筑两岸各一个 Container，中间架一条 Equalize 内部流量边。
/// 不进建造栏；Debug 场景用来确认「扫接口 → 登记内部边」这条路径能跑通。
/// </summary>
public class CompPipeDevInternalBridge : ThingComp, IPipeInternalMappingContributor
{
	private Mapping? internalMapping;

	public CompProperties_PipeDevInternalBridge Props => (CompProperties_PipeDevInternalBridge)props;

	public Mapping? InternalMapping => internalMapping;

	public float EffectiveMaxFlowRate => Props.baseMaxFlowRate;

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
			Log.Error(
				$"[RimPipe] DevInternalBridge {parent.LabelCap} 容器索引越界 A={ia} B={ib} count={member.Containers.Count}");
			return;
		}
		Container a = member.Containers[ia];
		Container b = member.Containers[ib];
		internalMapping = net.AddInternalMapping(
			a,
			b,
			parent as Building,
			EffectiveMaxFlowRate,
			linkedPairs,
			FlowDriveMode.Equalize);
	}
}
