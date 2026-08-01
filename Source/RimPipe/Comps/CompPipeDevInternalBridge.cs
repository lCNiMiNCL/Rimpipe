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
		if (!MapComponent_PipeNetwork.TryResolveContainers(member, Props.containerIndexA, Props.containerIndexB, "DevInternalBridge", out Container? a, out Container? b)
			|| a == null || b == null)
		{
			return;
		}
		internalMapping = net.AddInternalMapping(
			a,
			b,
			parent as Building,
			EffectiveMaxFlowRate,
			linkedPairs,
			FlowDriveMode.Equalize);
	}
}
