using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimPipe;

/// <summary>
/// 泵（P-A）。建筑内部有两岸小桶，中间一条 Forced 内部 Mapping（默认 A→B）。
/// 开启且有电时，还会把吸入口/排出口外面直接相邻的那几条边也改成 Forced
/// （邻格→入口、出口→邻格），这样流体才能抽进已经更满的目标罐；
/// 若外面仍是 Equalize，压力差会把液往回推，泵就抽不动。
/// 有效流量：开且有电用 baseRate，否则为 0。关泵或断电不拆 Mapping，只是把外面边恢复成 Equalize。
/// </summary>
public class CompPipePump : ThingComp, IPipeInternalMappingContributor
{
	private bool isOpen = true;
	private Mapping? internalMapping;
	private CompPowerTrader? powerComp;
	private readonly List<ExternalMappingSnapshot> externalSnapshots = new List<ExternalMappingSnapshot>();
	/// <summary>与泵两岸直接相邻的外部边（拓扑重建时收集一次）；开关/断电时只扫这份，不再 O(全图)。</summary>
	private readonly List<Mapping> adjacentExternalMappings = new List<Mapping>();

	/// <summary>只给 Debug 场景用：假装有电，方便验收。不写进存档。</summary>
	public bool debugForcePowered;

	private struct ExternalMappingSnapshot
	{
		public Mapping mapping;
		public FlowDriveMode drive;
		public bool forcedFromA;
		public float rate;
	}

	public CompProperties_PipePump Props => (CompProperties_PipePump)props;

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
				net?.WakeMember(mem, "pump");
			}
		}
	}

	public bool HasPower => debugForcePowered || powerComp == null || powerComp.PowerOn;

	public float EffectiveMaxFlowRate => isOpen && HasPower ? Props.baseMaxFlowRate : 0f;

	public Mapping? InternalMapping => internalMapping;

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref isOpen, "isOpen", true);
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		powerComp = parent.TryGetComp<CompPowerTrader>();
	}

	public override void ReceiveCompSignal(string signal)
	{
		base.ReceiveCompSignal(signal);
		if (signal == CompPowerTrader.PowerTurnedOnSignal || signal == CompPowerTrader.PowerTurnedOffSignal)
		{
			ApplyRateToInternalMapping();
			if (parent.Spawned)
			{
				MapComponent_PipeNetwork? net = parent.Map.GetComponent<MapComponent_PipeNetwork>();
				CompPipeNetworkMember? mem = parent.GetComp<CompPipeNetworkMember>();
				net?.WakeMember(mem, "pumpPower");
			}
		}
	}

	/// <summary>拓扑清空重建时由 MapComponent_PipeNetwork 调用（此时外部 Mapping 已建好）。</summary>
	public void ContributeInternalMapping(MapComponent_PipeNetwork net, HashSet<long> linkedPairs)
	{
		externalSnapshots.Clear();
		internalMapping = null;
		powerComp ??= parent.TryGetComp<CompPowerTrader>();
		CompPipeNetworkMember? member = parent.GetComp<CompPipeNetworkMember>();
		if (!MapComponent_PipeNetwork.TryResolveContainers(member, Props.containerIndexA, Props.containerIndexB, "泵", out Container? a, out Container? b)
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
			FlowDriveMode.Forced,
			Props.forcedFromA);
		// 外部边在此前已全部建好：一次收集与泵两岸相接的边，供后续开关泵复用
		adjacentExternalMappings.Clear();
		IReadOnlyList<Mapping> all = net.Mappings;
		for (int i = 0; i < all.Count; i++)
		{
			Mapping m = all[i];
			if (m == internalMapping || m.IsIncomplete || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			if (Touches(m, a) || Touches(m, b))
			{
				adjacentExternalMappings.Add(m);
			}
		}
		ApplyDriveToConnectedMappings(net);
	}

	public void ApplyRateToInternalMapping()
	{
		if (internalMapping != null)
		{
			internalMapping.maxFlowRate = EffectiveMaxFlowRate;
			internalMapping.flowDrive = FlowDriveMode.Forced;
			internalMapping.forcedFromA = Props.forcedFromA;
		}
		if (parent.Spawned)
		{
			MapComponent_PipeNetwork? net = parent.Map.GetComponent<MapComponent_PipeNetwork>();
			if (net != null)
			{
				ApplyDriveToConnectedMappings(net);
			}
		}
	}

	/// <summary>
	/// 泵开着时：把直接相邻的外部边改成 Forced（吸入侧从邻格抽进来，排出侧往邻格送出去）。
	/// 泵关着时：把这些边恢复成原来的 Equalize 和 rate。
	/// </summary>
	private void ApplyDriveToConnectedMappings(MapComponent_PipeNetwork net)
	{
		RestoreExternalSnapshots();
		if (EffectiveMaxFlowRate <= 0f || internalMapping == null)
		{
			return;
		}
		CompPipeNetworkMember? member = parent.GetComp<CompPipeNetworkMember>();
		if (member == null)
		{
			return;
		}
		Container inlet = member.Containers[Props.containerIndexA];
		Container outlet = member.Containers[Props.containerIndexB];
		if (!Props.forcedFromA)
		{
			(inlet, outlet) = (outlet, inlet);
		}

		// 只扫拓扑重建时收集的邻接边，避免每次开/关泵遍历全图
		for (int i = 0; i < adjacentExternalMappings.Count; i++)
		{
			Mapping m = adjacentExternalMappings[i];
			if (m == internalMapping || m.IsIncomplete || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			if (Touches(m, inlet) && !Touches(m, outlet))
			{
				Container other = m.containerA == inlet ? m.containerB! : m.containerA!;
				SnapshotExternal(m);
				m.flowDrive = FlowDriveMode.Forced;
				m.forcedFromA = m.containerA == other;
				m.maxFlowRate = EffectiveMaxFlowRate;
			}
			else if (Touches(m, outlet) && !Touches(m, inlet))
			{
				Container other = m.containerA == outlet ? m.containerB! : m.containerA!;
				SnapshotExternal(m);
				m.flowDrive = FlowDriveMode.Forced;
				m.forcedFromA = m.containerA == outlet;
				m.maxFlowRate = EffectiveMaxFlowRate;
			}
		}
	}

	/// <summary>
	/// DirtyTopo 局部重建后调用：泵旁边的管道边可能被删了又新建（退回 Equalize），
	/// 重收邻接缓存并重新施加 Forced 驱动，避免旧引用残留导致泵失去逆压差抽送。
	/// 注意：不清 internalMapping——非脏泵的内部边仍存活（脏泵已由 ContributeInternalMapping 处理，重复调用幂等）。
	/// </summary>
	public void RefreshAdjacentMappingsAfterLocalRebuild(MapComponent_PipeNetwork net)
	{
		if (!parent.Spawned || net == null)
		{
			return;
		}
		CompPipeNetworkMember? member = parent.GetComp<CompPipeNetworkMember>();
		if (member == null || member.Containers.Count <= Props.containerIndexA
			|| member.Containers.Count <= Props.containerIndexB)
		{
			return;
		}
		Container inlet = member.Containers[Props.containerIndexA];
		Container outlet = member.Containers[Props.containerIndexB];

		// 重收邻接边：丢弃已删的陈旧 Mapping，纳入本次局部重建新建的边
		adjacentExternalMappings.Clear();
		IReadOnlyList<Mapping> all = net.Mappings;
		for (int i = 0; i < all.Count; i++)
		{
			Mapping m = all[i];
			if (m == internalMapping || m.IsIncomplete || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			if (Touches(m, inlet) || Touches(m, outlet))
			{
				adjacentExternalMappings.Add(m);
			}
		}
		// 先恢复旧快照（若还有残留）→ 再对新鲜边快照并施加 Forced（幂等）
		ApplyDriveToConnectedMappings(net);
	}

	private void SnapshotExternal(Mapping m)
	{
		for (int i = 0; i < externalSnapshots.Count; i++)
		{
			if (externalSnapshots[i].mapping == m)
			{
				return;
			}
		}
		externalSnapshots.Add(new ExternalMappingSnapshot
		{
			mapping = m,
			drive = m.flowDrive,
			forcedFromA = m.forcedFromA,
			rate = m.maxFlowRate
		});
	}

	private void RestoreExternalSnapshots()
	{
		for (int i = 0; i < externalSnapshots.Count; i++)
		{
			ExternalMappingSnapshot s = externalSnapshots[i];
			if (s.mapping == null)
			{
				continue;
			}
			s.mapping.flowDrive = s.drive;
			s.mapping.forcedFromA = s.forcedFromA;
			s.mapping.maxFlowRate = s.rate;
		}
		externalSnapshots.Clear();
	}

	private static bool Touches(Mapping m, Container c)
	{
		return m.containerA == c || m.containerB == c;
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
			defaultLabel = isOpen ? "RimPipe_Gizmo_PumpOpen".Translate() : "RimPipe_Gizmo_PumpClosed".Translate(),
			defaultDesc = "RimPipe_Gizmo_PumpDesc".Translate(),
			icon = TexCommand.DesirePower,
			isActive = () => isOpen,
			toggleAction = () => IsOpen = !isOpen
		};
		yield return cmd;
	}

	public override string? CompInspectStringExtra()
	{
		string power = HasPower ? "RimPipe_State_Powered".Translate() : "RimPipe_State_Unpowered".Translate();
		string state = isOpen ? "RimPipe_State_Open".Translate() : "RimPipe_State_Closed".Translate();
		string forced = Props.forcedFromA ? "A→B" : "B→A";
		return "RimPipe_Inspect_Pump".Translate(state, power, EffectiveMaxFlowRate.ToString("0.#"), forced);
	}
}
