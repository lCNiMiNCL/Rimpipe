using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using RimPipe.Debug;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe;

/// <summary>
/// 整张地图的管网总管（partial 拆分版）。主文件保留状态/生命周期，职责见各 partial。
/// </summary>
public partial class MapComponent_PipeNetwork : MapComponent
{
	/// <summary>由 CompPipeCell.breached 重刷路径 Mapping.leakOpen（全量版本，供整图重建/兼容调用）。</summary>
	private void ApplyBreachLeakFlags()
	{
		for (int i = 0; i < mappings.Count; i++)
		{
			mappings[i].leakOpen = false;
		}
		for (int i = 0; i < pipeCells.Count; i++)
		{
			CompPipeCell pipe = pipeCells[i];
			if (pipe == null || !pipe.Breached || pipe.parent == null || !pipe.parent.Spawned)
			{
				continue;
			}
			IntVec3 cell = pipe.parent.Position;
			if (!cellToMapping.TryGetValue(cell, out List<Mapping> list) || list == null)
			{
				continue;
			}
			for (int j = 0; j < list.Count; j++)
			{
				list[j].leakOpen = true;
			}
		}
	}

	/// <summary>局部版本：只重算某一管道格关联的 Mapping.leakOpen，不扫描全图。</summary>
	private void ApplyBreachLeakFlagsForCell(IntVec3 cell)
	{
		if (!cellToMapping.TryGetValue(cell, out List<Mapping>? list) || list == null)
		{
			return;
		}
		for (int j = 0; j < list.Count; j++)
		{
			Mapping m = list[j];
			if (m == null)
			{
				continue;
			}
			bool leak = false;
			if (m.attachedBuildings != null)
			{
				for (int k = 0; k < m.attachedBuildings.Count; k++)
				{
					Building? b = m.attachedBuildings[k];
					if (b == null || !b.Spawned)
					{
						continue;
					}
					CompPipeCell? pc = b.TryGetComp<CompPipeCell>();
					if (pc != null && pc.Breached)
					{
						leak = true;
						break;
					}
				}
			}
			m.leakOpen = leak;
		}
	}

	/// <summary>
	/// ExtHook：扫一遍实现了 <see cref="IPipeInternalMappingContributor"/> 的 Comp
	/// （阀、泵、换热器，以及第三方设备），让它们登记内部边。
	/// </summary>
	private void BuildInternalMappings(HashSet<long> linkedPairs)
	{
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember member = members[i];
			if (member?.parent == null || !member.parent.Spawned)
			{
				continue;
			}
			BuildInternalMappingsFor(member, linkedPairs);
		}
	}

	/// <summary>对单个构件调用其内部映射贡献者（阀门/泵/换热器/第三方设备）。</summary>
	private void BuildInternalMappingsFor(CompPipeNetworkMember member, HashSet<long> linkedPairs)
	{
		ThingWithComps? parent = member?.parent;
		if (parent == null || !parent.Spawned)
		{
			return;
		}
		List<ThingComp> comps = parent.AllComps;
		if (comps == null)
		{
			return;
		}
		for (int c = 0; c < comps.Count; c++)
		{
			if (comps[c] is IPipeInternalMappingContributor contributor)
			{
				contributor.ContributeInternalMapping(this, linkedPairs);
			}
		}
	}

	/// <summary>同建筑内部 Mapping（阀 / 泵 / 换热器）。rate 与 drive / type 由调用方给定。</summary>
	public static bool TryResolveContainers(
		CompPipeNetworkMember? member,
		int indexA,
		int indexB,
		string label,
		out Container? a,
		out Container? b)
	{
		a = null;
		b = null;
		if (member == null || member.parent == null || !member.parent.Spawned)
		{
			return false;
		}
		if (indexA < 0 || indexB < 0 || indexA >= member.Containers.Count || indexB >= member.Containers.Count)
		{
			Log.Error($"[RimPipe] {label} {member.parent.LabelCap} 容器索引越界 A={indexA} B={indexB} count={member.Containers.Count}");
			return false;
		}
		a = member.Containers[indexA];
		b = member.Containers[indexB];
		return true;
	}

	/// <summary>单容器安全解析：供反应釜等按下标读取容器时统一防御越界。</summary>
	public static bool TryResolveContainer(
		CompPipeNetworkMember? member,
		int index,
		string label,
		out Container? container)
	{
		container = null;
		if (member == null || member.parent == null || !member.parent.Spawned)
		{
			return false;
		}
		if (index < 0 || index >= member.Containers.Count)
		{
			Log.Error($"[RimPipe] {label} {member.parent.LabelCap} 容器索引越界 index={index} count={member.Containers.Count}");
			return false;
		}
		container = member.Containers[index];
		return true;
	}

	/// <summary>同建筑内部 Mapping（阀 / 泵 / 换热器）。rate 与 drive / type 由调用方给定。</summary>
	public Mapping? AddInternalMapping(
		Container? ca,
		Container? cb,
		Building? owner,
		float maxFlowRate,
		HashSet<long> linkedPairs,
		FlowDriveMode flowDrive = FlowDriveMode.Equalize,
		bool forcedFromA = true,
		MappingType mappingType = MappingType.Flow)
	{
		if (ca == null || cb == null || ca == cb)
		{
			return null;
		}
		if (ca.id < 0 || cb.id < 0)
		{
			return null;
		}
		long key = ContainerPairKey(ca.id, cb.id, mappingType);
		if (!linkedPairs.Add(key))
		{
			return null;
		}
		Mapping m = new Mapping
		{
			id = idProvider.Next(),
			containerA = ca,
			containerB = cb,
			maxFlowRate = maxFlowRate,
			mappingType = mappingType,
			flowDrive = flowDrive,
			forcedFromA = forcedFromA
		};
		if (owner != null)
		{
			m.attachedBuildings.Add(owner);
		}
		mappings.Add(m);
		CacheMappingCells(m);
		return m;
	}

	public int CountMappingsFor(CompPipeNetworkMember member)
	{
		int n = 0;
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (OwnsEnd(member, m.containerA) || OwnsEnd(member, m.containerB))
			{
				n++;
			}
		}
		return n;
	}

	/// <summary>Inspect：该构件相关 Mapping 的 rate / path（R-A）。</summary>
	public void AppendMappingsInspect(CompPipeNetworkMember member, StringBuilder sb)
	{
		if (member == null || sb == null)
		{
			return;
		}
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (!OwnsEnd(member, m.containerA) && !OwnsEnd(member, m.containerB))
			{
				continue;
			}
			sb.Append("  #");
			sb.Append(m.id);
			if (m.mappingType == MappingType.Heat)
			{
				sb.Append(" Heat heatRate=");
			}
			else
			{
				sb.Append(" rate=");
			}
			sb.Append(m.maxFlowRate.ToString("0.##"));
			if (m.pathPipeCells > 0)
			{
				sb.Append(" path=");
				sb.Append(m.pathPipeCells);
			}
			sb.AppendLine();
		}
	}

	private static bool OwnsEnd(CompPipeNetworkMember member, Container? c)
	{
		return c != null && c.owner == member;
	}

	/// <summary>端口外一格是否有可直接相邻对接或管道格（检视用）。</summary>
	public bool IsPortLikelyDocked(Port port)
	{
		if (port?.owner?.parent == null || !port.owner.parent.Spawned)
		{
			return false;
		}
		IntVec3 outer = port.OuterCell;
		if (!outer.InBounds(map))
		{
			return false;
		}
		if (cellToPipe.ContainsKey(outer))
		{
			return true;
		}
		CompPipeNetworkMember? other = MemberAt(outer);
		if (other == null || other == port.owner)
		{
			return false;
		}
		Port? opp = other.FindPortFacingWorld(port.WorldRot.Opposite);
		if (opp == null)
		{
			return false;
		}
		return port.owner.parent.OccupiedRect().Contains(opp.OuterCell);
	}

	/// <summary>
	/// 管道贴着构件但未进任何端口外一格时打日志，便于发现「接了管却不进 Mapping」。
	/// 仅 DevMode 且非套件批量时执行：正常布局（管道贴双口构件侧面/背面）不该刷屏。
	/// </summary>
	private void WarnOrphanPipeTouches()
	{
		if (!Prefs.DevMode || QuietDebugLogs)
		{
			return;
		}
		HashSet<IntVec3> attachedOuters = new HashSet<IntVec3>();
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember m = members[i];
			if (m?.parent == null || !m.parent.Spawned)
			{
				continue;
			}
			for (int p = 0; p < m.Ports.Count; p++)
			{
				attachedOuters.Add(m.Ports[p].OuterCell);
			}
		}
		HashSet<int> warnedMembers = new HashSet<int>();
		foreach (KeyValuePair<IntVec3, CompPipeCell> kv in cellToPipe)
		{
			IntVec3 pipeCell = kv.Key;
			if (attachedOuters.Contains(pipeCell))
			{
				continue;
			}
			foreach (IntVec3 dir in GenAdj.CardinalDirections)
			{
				CompPipeNetworkMember? mem = MemberAt(pipeCell + dir);
				if (mem == null || !warnedMembers.Add(mem.parent.thingIDNumber))
				{
					continue;
				}
				Log.Warning($"[RimPipe] 管道 {pipeCell} 邻接 {mem.parent.LabelCap}@{mem.parent.Position}，但不是其任何端口的外一格（见检视「端口(世界向)」）。不会经该侧建 Mapping。");
			}
		}
	}

	private struct PipeAttachment
	{
		public CompPipeNetworkMember member;
		public Port port;
		public Container container;
		public IntVec3 outerCell;
		/// <summary>接入通道（CompPipeCell.GroupA/GroupB）。</summary>
		public int channel;
	}

	/// <summary>端口接入的组：channel 0→A，1→B。</summary>
	private static int PortChannelGroup(Port port)
	{
		return port != null && port.channel == 1 ? CompPipeCell.GroupB : CompPipeCell.GroupA;
	}

	/// <summary>
	/// 端口是否接入该分量：外格是管道格、管道格朝端口方向出口属端口组、且接入节点在分量中。
	/// </summary>
	private bool PortAttachedToComponent(Port port, HashSet<(IntVec3 cell, int channel)> componentCells)
	{
		if (port?.owner?.parent == null)
		{
			return false;
		}
		IntVec3 outer = port.OuterCell;
		if (!cellToPipe.TryGetValue(outer, out CompPipeCell pipe))
		{
			return false;
		}
		int g = pipe.DirGroup(port.WorldRot.Opposite);
		if (g == CompPipeCell.GroupNone || g != PortChannelGroup(port))
		{
			return false;
		}
		return componentCells.Contains((outer, g));
	}

	private void BuildPipeAdjacentMappings(HashSet<long> linkedPairs)
	{
		HashSet<(IntVec3 cell, int channel)> visitedPipe = new HashSet<(IntVec3, int)>();
		List<HashSet<(IntVec3 cell, int channel)>> components = new List<HashSet<(IntVec3, int)>>();
		HashSet<IntVec3> affected = new HashSet<IntVec3>();
		foreach (KeyValuePair<IntVec3, CompPipeCell> kv in cellToPipe)
		{
			FloodComponentFromAllChannels(kv.Key, kv.Value, visitedPipe, components, affected);
		}
		for (int i = 0; i < components.Count; i++)
		{
			BuildPipeComponent(components[i], linkedPairs);
		}
	}

	/// <summary>
	/// 对单个管道连通分量（节点 = 格子×通道）：附件收集（端口组匹配）+ 多源 BFS Voronoi 交界建 Mapping。
	/// 局部重建与整图重建共用（分量级主体）。
	/// </summary>
	private void BuildPipeComponent(HashSet<(IntVec3 cell, int channel)> componentSet, HashSet<long> linkedPairs)
	{
		List<(IntVec3 cell, int channel)> component = new List<(IntVec3, int)>(componentSet);

		// 1) 找出所有接入节点落在本分量上的端口挂接（端口组必须等于管道格该方向出口组）
		List<PipeAttachment> attachments = new List<PipeAttachment>();
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember m = members[i];
			if (m?.parent == null || !m.parent.Spawned)
			{
				continue;
			}
			for (int p = 0; p < m.Ports.Count; p++)
			{
				Port port = m.Ports[p];
				IntVec3 outer = port.OuterCell;
				if (!cellToPipe.TryGetValue(outer, out CompPipeCell pipe))
				{
					continue;
				}
				int g = pipe.DirGroup(port.WorldRot.Opposite);
				if (g == CompPipeCell.GroupNone || g != PortChannelGroup(port))
				{
					continue;
				}
				if (!componentSet.Contains((outer, g)))
				{
					continue;
				}
				Container? cont = port.Container;
				if (cont == null)
				{
					continue;
				}
				attachments.Add(new PipeAttachment
				{
					member = m,
					port = port,
					container = cont,
					outerCell = outer,
					channel = g
				});
			}
		}
		if (attachments.Count < 2)
		{
			return;
		}

		// 2) 多源 BFS（节点空间）：每个挂接节点为领地种子，交界建 Mapping
		Dictionary<(IntVec3, int), int> owner = new Dictionary<(IntVec3, int), int>();
		Queue<(IntVec3 cell, int channel)> q = new Queue<(IntVec3, int)>();
		for (int i = 0; i < attachments.Count; i++)
		{
			(IntVec3, int) node = (attachments[i].outerCell, attachments[i].channel);
			if (owner.TryGetValue(node, out int other))
			{
				// 两端口抢同一节点：直接视为邻接
				if (other != i)
				{
					AddPipePair(attachments[other], attachments[i], linkedPairs, component, componentSet);
				}
				continue;
			}
			owner[node] = i;
			q.Enqueue(node);
		}

		HashSet<long> borderPairs = new HashSet<long>();
		while (q.Count > 0)
		{
			(IntVec3 cell, int ch) = q.Dequeue();
			int id = owner[(cell, ch)];
			if (!cellToPipe.TryGetValue(cell, out CompPipeCell pipe))
			{
				continue;
			}
			for (int i = 0; i < 4; i++)
			{
				// 格内只沿本通道方向走（跨组隔离）
				if (pipe.DirGroup(new Rot4(i)) != ch)
				{
					continue;
				}
				IntVec3 n = cell + GenAdj.CardinalDirections[i];
				if (!n.InBounds(map) || !cellToPipe.TryGetValue(n, out CompPipeCell nPipe))
				{
					continue;
				}
				int nCh = nPipe.DirGroup(new Rot4(i).Opposite);
				if (nCh == CompPipeCell.GroupNone)
				{
					continue;
				}
				(IntVec3, int) node = (n, nCh);
				if (owner.TryGetValue(node, out int otherId))
				{
					if (otherId != id)
					{
						long key = PairKey(id, otherId);
						if (borderPairs.Add(key))
						{
							AddPipePair(attachments[id], attachments[otherId], linkedPairs, component, componentSet);
						}
					}
					continue;
				}
				owner[node] = id;
				q.Enqueue(node);
			}
		}
	}

	private void AddPipePair(
		PipeAttachment a,
		PipeAttachment b,
		HashSet<long> linkedPairs,
		List<(IntVec3 cell, int channel)> component,
		HashSet<(IntVec3 cell, int channel)> componentSet)
	{
		List<Building> attached = new List<Building>();
		if (a.member.parent is Building ba)
		{
			attached.Add(ba);
		}
		if (b.member.parent is Building bb && bb != a.member.parent)
		{
			attached.Add(bb);
		}
		for (int i = 0; i < component.Count; i++)
		{
			if (cellToPipe.TryGetValue(component[i].cell, out CompPipeCell pipe) && pipe.parent is Building pb && !attached.Contains(pb))
			{
				attached.Add(pb);
			}
		}
		int pathPipeCells = ShortestPipePathCellCount((a.outerCell, a.channel), (b.outerCell, b.channel), componentSet);
		TryAddPair(
			a.container,
			b.container,
			a.member.parent as Building,
			b.member.parent as Building,
			linkedPairs,
			attached,
			pathPipeCells);
	}

	/// <summary>管道分量内两挂接节点最短路径的格数（含起终；同节点=1）。失败返回 0。</summary>
	private int ShortestPipePathCellCount(
		(IntVec3 cell, int channel) start,
		(IntVec3 cell, int channel) end,
		HashSet<(IntVec3 cell, int channel)> componentSet)
	{
		if (!componentSet.Contains(start) || !componentSet.Contains(end))
		{
			return 0;
		}
		if (start == end)
		{
			return 1;
		}
		Queue<(IntVec3, int)> q = new Queue<(IntVec3, int)>();
		Dictionary<(IntVec3, int), (IntVec3, int)> prev = new Dictionary<(IntVec3, int), (IntVec3, int)>();
		q.Enqueue(start);
		prev[start] = start;
		while (q.Count > 0)
		{
			(IntVec3 c, int ch) = q.Dequeue();
			if (!cellToPipe.TryGetValue(c, out CompPipeCell pipe))
			{
				continue;
			}
			for (int i = 0; i < 4; i++)
			{
				if (pipe.DirGroup(new Rot4(i)) != ch)
				{
					continue;
				}
				IntVec3 n = c + GenAdj.CardinalDirections[i];
				if (!n.InBounds(map) || !cellToPipe.TryGetValue(n, out CompPipeCell nPipe))
				{
					continue;
				}
				int nCh = nPipe.DirGroup(new Rot4(i).Opposite);
				if (nCh == CompPipeCell.GroupNone)
				{
					continue;
				}
				(IntVec3, int) node = (n, nCh);
				if (prev.ContainsKey(node))
				{
					continue;
				}
				prev[node] = (c, ch);
				if (node == end)
				{
					int count = 1;
					(IntVec3, int) walk = node;
					while (walk != start)
					{
						count++;
						walk = prev[walk];
					}
					return count;
				}
				q.Enqueue(node);
			}
		}
		return 0;
	}

	private CompPipeNetworkMember? MemberAt(IntVec3 cell)
	{
		List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
		for (int i = 0; i < things.Count; i++)
		{
			if (things[i] is ThingWithComps twc)
			{
				CompPipeNetworkMember comp = twc.GetComp<CompPipeNetworkMember>();
				if (comp != null)
				{
					return comp;
				}
			}
		}
		return null;
	}

	private static long PairKey(int idA, int idB)
	{
		if (idA > idB)
		{
			(idA, idB) = (idB, idA);
		}
		return ((long)idA << 32) | (uint)idB;
	}

	/// <summary>H-A：同对 Container 可同时存在 Flow + Heat，key 含 mappingType。</summary>
	private static long ContainerPairKey(int idA, int idB, MappingType type)
	{
		if (idA > idB)
		{
			(idA, idB) = (idB, idA);
		}
		return ((long)(byte)type << 56) | ((long)(uint)idA << 28) | (uint)(idB & 0x0FFFFFFF);
	}

	private void TryAddPair(
		Container? ca,
		Container? cb,
		Building? buildingA,
		Building? buildingB,
		HashSet<long> linkedPairs,
		List<Building>? pathBuildings,
		int pathPipeCells = 0)
	{
		if (ca == null || cb == null || ca == cb)
		{
			return;
		}
		if (ca.id < 0 || cb.id < 0)
		{
			return;
		}
		long key = ContainerPairKey(ca.id, cb.id, MappingType.Flow);
		if (!linkedPairs.Add(key))
		{
			return;
		}
		Mapping m = new Mapping
		{
			id = idProvider.Next(),
			containerA = ca,
			containerB = cb,
			pathPipeCells = pathPipeCells,
			maxFlowRate = ResolveMaxFlowRate(ca, cb, pathPipeCells),
			mappingType = MappingType.Flow
		};
		if (pathBuildings != null)
		{
			m.attachedBuildings.AddRange(pathBuildings);
		}
		else
		{
			if (buildingA != null)
			{
				m.attachedBuildings.Add(buildingA);
			}
			if (buildingB != null && buildingB != buildingA)
			{
				m.attachedBuildings.Add(buildingB);
			}
		}
		mappings.Add(m);
		CacheMappingCells(m);
	}

	/// <summary>R-A 下限，避免极长管 rate→0。</summary>
	private const float MinFlowRateAfterResistance = 0.1f;

	/// <summary>
	/// 阻力公式：base 取两端默认 maxFlowRate 的较小值；
	/// 实际 rate = max(下限, base / max(1, 路径格数))。
	/// 路径格数为 0（直接相邻）时除数按 1 算，也就是不因长度变慢。
	/// </summary>
	private float ResolveMaxFlowRate(Container a, Container b, int pathPipeCells)
	{
		float rateA = a.owner?.Props.defaultMaxFlowRate ?? 10f;
		float rateB = b.owner?.Props.defaultMaxFlowRate ?? 10f;
		float baseRate = rateA < rateB ? rateA : rateB;
		float divisor = pathPipeCells < 1 ? 1f : pathPipeCells;
		float rate = baseRate / divisor;
		return rate < MinFlowRateAfterResistance ? MinFlowRateAfterResistance : rate;
	}

	private void CacheMappingCells(Mapping m)
	{
		for (int i = 0; i < m.attachedBuildings.Count; i++)
		{
			Building b = m.attachedBuildings[i];
			if (b == null || !b.Spawned)
			{
				continue;
			}
			foreach (IntVec3 cell in b.OccupiedRect())
			{
				if (!cellToMapping.TryGetValue(cell, out List<Mapping> list))
				{
					list = new List<Mapping>();
					cellToMapping[cell] = list;
				}
				if (!list.Contains(m))
				{
					list.Add(m);
				}
			}
		}
	}
}
