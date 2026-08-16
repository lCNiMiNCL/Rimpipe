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
	// —— 4.5 拓扑重建分配优化：局部重建高频临时容器复用 ——
	private readonly List<DelayedAction> scratchMemberActions = new List<DelayedAction>();
	private readonly HashSet<IntVec3> scratchPipeDirtyCells = new HashSet<IntVec3>();
	private readonly HashSet<IntVec3> scratchAffectedPipeCells = new HashSet<IntVec3>();
	private readonly List<HashSet<(IntVec3 cell, int channel)>> scratchComponents = new List<HashSet<(IntVec3 cell, int channel)>>();
	private readonly HashSet<(IntVec3 cell, int channel)> scratchVisited = new HashSet<(IntVec3 cell, int channel)>();
	private readonly HashSet<CompPipeNetworkMember> scratchDirtyMembers = new HashSet<CompPipeNetworkMember>();
	private readonly HashSet<CompPipeNetworkMember> scratchAffectedMembers = new HashSet<CompPipeNetworkMember>();
	private readonly List<(Container a, Container b)> scratchDeletedPipePairs = new List<(Container, Container)>();
	private readonly HashSet<Container> scratchAffectedContainers = new HashSet<Container>();
	private readonly HashSet<long> scratchLinkedPairs = new HashSet<long>();
	private readonly List<HashSet<(IntVec3 cell, int channel)>> scratchReconnectComponents = new List<HashSet<(IntVec3 cell, int channel)>>();
	private readonly HashSet<(IntVec3 cell, int channel)> scratchReconnectSeen = new HashSet<(IntVec3 cell, int channel)>();
	private readonly HashSet<(Container, Container)> scratchRecovered = new HashSet<(Container, Container)>();
	private readonly HashSet<Container> scratchSeedContainers = new HashSet<Container>();

	public void RegisterMember(CompPipeNetworkMember comp, bool respawningAfterLoad)
	{
		if (!members.Contains(comp))
		{
			members.Add(comp);
		}
		batchCachesDirty = true;
		AssignContainerIds(comp);
		if (!respawningAfterLoad)
		{
			Enqueue(DelayedActionType.MemberChanged, comp.parent.Position, comp);
		}
		else
		{
			needsFullRebuild = true;
		}
		if (!QuietDebugLogs)
		{
			Log.Message($"[RimPipe] 已注册构件 {comp.parent.LabelCap} id={comp.parent.thingIDNumber} 容器={comp.Containers.Count} 端口={comp.Ports.Count}");
		}
	}

	public void DeregisterMember(CompPipeNetworkMember comp, DestroyMode mode = DestroyMode.Vanish)
	{
		if (ShouldDumpOnDestroy(mode))
		{
			TryDumpMemberOnce(comp);
		}
		UnregisterChemReactorsTouching(comp);
		members.Remove(comp);
		batchCachesDirty = true;
		Enqueue(DelayedActionType.MemberChanged, comp.parent.Position, comp);
		if (!QuietDebugLogs)
		{
			Log.Message($"[RimPipe] 已注销构件 {comp.parent.LabelCap}");
		}
	}

	public void RegisterPipeCell(CompPipeCell comp)
	{
		if (!pipeCells.Contains(comp))
		{
			pipeCells.Add(comp);
		}
		cellToPipe[comp.parent.Position] = comp;
		batchCachesDirty = true;
		Enqueue(DelayedActionType.PipeChanged, comp.parent.Position);
	}

	public void DeregisterPipeCell(CompPipeCell comp, DestroyMode mode = DestroyMode.Vanish)
	{
		if (ShouldDumpOnDestroy(mode))
		{
			TryDumpPipeOnce(comp);
		}
		pipeCells.Remove(comp);
		if (cellToPipe.TryGetValue(comp.parent.Position, out CompPipeCell existing) && existing == comp)
		{
			cellToPipe.Remove(comp.parent.Position);
		}
		batchCachesDirty = true;
		Enqueue(DelayedActionType.PipeChanged, comp.parent.Position);
	}

	/// <summary>
	/// 管道格方向分组刚切换过：必须走拓扑重建（方向影响连通性），
	/// 复用 PipeChanged 局部重建路径（自带 netId 增量与缓存失效）。
	/// </summary>
	public void NotifyPipeConnectionChanged(CompPipeCell comp)
	{
		if (comp?.parent == null)
		{
			return;
		}
		batchCachesDirty = true;
		Enqueue(DelayedActionType.PipeChanged, comp.parent.Position);
	}

	/// <summary>
	/// 管道或储罐的 breached 刚切换过：只刷新相关 Mapping.leakOpen（不必整网重建），并唤醒受影响的网。
	/// </summary>
	public void NotifyBreachChanged()
	{
		ApplyBreachLeakFlags();
		HashSet<int> woke = new HashSet<int>();
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.leakOpen && m.netId >= 0 && woke.Add(m.netId))
			{
				WakeNet(m.netId, "breach");
			}
		}
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			CompPipeBreachable? br = mem?.parent?.TryGetComp<CompPipeBreachable>();
			if (br == null || !br.Breached || mem?.Containers == null)
			{
				continue;
			}
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				WakeContainer(mem.Containers[c], "breach");
			}
		}
		if (woke.Count == 0)
		{
			WakeNetwork("breach");
		}
	}

	/// <summary>
	/// 局部版本：由 Comp 在切换破损时传入具体 Thing，只刷新该管道格关联 Mapping 并唤醒相关网；
	/// 若是储罐破损则只唤醒该建筑所属网。
	/// </summary>
	public void NotifyBreachChanged(Thing thing)
	{
		if (thing == null || !thing.Spawned)
		{
			NotifyBreachChanged();
			return;
		}

		bool handled = false;

		CompPipeCell? cell = thing.TryGetComp<CompPipeCell>();
		if (cell != null)
		{
			ApplyBreachLeakFlagsForCell(thing.Position);
			HashSet<int> woke = new HashSet<int>();
			if (cellToMapping.TryGetValue(thing.Position, out List<Mapping>? list) && list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					Mapping m = list[i];
					if (m != null && m.netId >= 0 && woke.Add(m.netId))
					{
						WakeNet(m.netId, "breach");
					}
				}
			}
			// 即使该格当前没有 leakOpen，也唤醒相关网，让休眠重评重新判定。
			handled = true;
		}

		CompPipeBreachable? br = thing.TryGetComp<CompPipeBreachable>();
		if (br != null)
		{
			CompPipeNetworkMember? mem = (thing as ThingWithComps)?.GetComp<CompPipeNetworkMember>();
			WakeMember(mem, "breach");
			handled = true;
		}

		if (!handled)
		{
			NotifyBreachChanged();
		}
	}

	private static bool ShouldDumpOnDestroy(DestroyMode mode)
	{
		if (RimPipeMod.Settings == null || !RimPipeMod.Settings.dumpOnDestroy)
		{
			return false;
		}
		return mode == DestroyMode.KillFinalize || mode == DestroyMode.Deconstruct;
	}

	/// <summary>
	/// 摧毁瞬间倾泻：最多按 1 批 rate 立刻扣一点量；环境效果和正常泄漏走同一套入口。
	/// </summary>
	private void TryDumpMemberOnce(CompPipeNetworkMember comp)
	{
		if (comp?.Containers == null || comp.parent == null)
		{
			return;
		}
		IntVec3 cell = comp.parent.Position;
		float rate = comp.Props.defaultMaxFlowRate;
		for (int i = 0; i < comp.Containers.Count; i++)
		{
			DumpContainerOnce(comp.Containers[i], rate, cell);
		}
	}

	private void TryDumpPipeOnce(CompPipeCell pipe)
	{
		if (pipe?.parent == null)
		{
			return;
		}
		IntVec3 cell = pipe.parent.Position;
		if (!cellToMapping.TryGetValue(cell, out List<Mapping> list) || list == null)
		{
			return;
		}
		HashSet<int> seen = new HashSet<int>();
		for (int i = 0; i < list.Count; i++)
		{
			Mapping m = list[i];
			if (m == null || !seen.Add(m.id))
			{
				continue;
			}
			float half = m.maxFlowRate * 0.5f;
			DumpContainerOnce(m.containerA, half, cell);
			DumpContainerOnce(m.containerB, half, cell);
		}
	}

	private void DumpContainerOnce(Container? c, float want, IntVec3 cell)
	{
		if (c == null || want <= FlowSolver.AmountEpsilon)
		{
			return;
		}
		float dump = want;
		if (dump > c.amount)
		{
			dump = c.amount;
		}
		if (dump <= FlowSolver.AmountEpsilon)
		{
			return;
		}
		Log.Message($"[RimPipe] dumpOnDestroy 倾泻 {dump:0.##} 自 {c}");
		c.CommitAmount(c.amount - dump);
		LeakEffectApplicator.Apply(c, dump, cell, map);
	}

	private void AssignContainerIds(CompPipeNetworkMember comp)
	{
		for (int i = 0; i < comp.Containers.Count; i++)
		{
			Container c = comp.Containers[i];
			if (c.id < 0)
			{
				c.id = idProvider.Next();
			}
		}
	}

	private void Enqueue(DelayedActionType type, IntVec3 cell, CompPipeNetworkMember? member = null)
	{
		delayedActions.Add(new DelayedAction(type, cell, member));
	}

	public void RequestFullRebuild()
	{
		needsFullRebuild = true;
		Enqueue(DelayedActionType.FullRebuild, IntVec3.Invalid);
	}

	private void ProcessDelayedActions()
	{
		if (delayedActions.Count == 0 && !needsFullRebuild)
		{
			return;
		}
		if (needsFullRebuild)
		{
			delayedActions.Clear();
			RebuildAllMappings();
			needsFullRebuild = false;
			return;
		}
		// 局部重建：先值拷贝出动作列表再 Clear，避免遍历已清列表
		List<DelayedAction> actions = new List<DelayedAction>(delayedActions);
		delayedActions.Clear();
		RebuildDirtyLocal(actions);
		needsFullRebuild = false;
	}

	private void RebuildAllMappings()
	{
		Stopwatch sw = Stopwatch.StartNew();
		batchCachesDirty = true;
		mappings.Clear();
		cellToMapping.Clear();

		HashSet<long> linkedPairs = new HashSet<long>();

		// Face-to-face
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember a = members[i];
			if (a?.parent == null || !a.parent.Spawned)
			{
				continue;
			}
			for (int p = 0; p < a.Ports.Count; p++)
			{
				Port portA = a.Ports[p];
				IntVec3 outer = portA.OuterCell;
				if (!outer.InBounds(map))
				{
					continue;
				}
				CompPipeNetworkMember? b = MemberAt(outer);
				if (b == null || b == a)
				{
					continue;
				}
				Rot4 need = portA.WorldRot.Opposite;
				Port? portB = b.FindPortFacingWorld(need);
				if (portB == null)
				{
					continue;
				}
				if (!a.parent.OccupiedRect().Contains(portB.OuterCell))
				{
					continue;
				}
				TryAddPair(portA.Container, portB.Container, a.parent as Building, b.parent as Building, linkedPairs, null);
			}
		}

		// 管道：每个连通管道分量上，对挂接端口做图上多源 BFS（Voronoi），
		// 仅邻接领地交界处建 Mapping，避免完全图导致批间振荡。
		BuildPipeAdjacentMappings(linkedPairs);

		// V-A 等：同建筑内部 Mapping（阀门开关只改 rate，不参与外部拓扑）
		BuildInternalMappings(linkedPairs);

		ApplyBreachLeakFlags();
		AssignNetworkIds();
		WakeNetwork("topology");

		sw.Stop();
		LastTopologyRebuildMs = (float)sw.Elapsed.TotalMilliseconds;

		if (!QuietDebugLogs)
		{
			Log.Message(
				$"[RimPipe] 拓扑重建：构件={members.Count} 管道格={pipeCells.Count} Mapping={mappings.Count} nets={netCount} 耗时={LastTopologyRebuildMs:F3}ms");
		}
		WarnOrphanPipeTouches();
	}

	/// <summary>局部脏区重建退化为整图重建的阈值：受影响管道格数占比。</summary>
	private const float DirtyTopoThreshold = 0.30f;

	/// <summary>
	/// DirtyTopo 局部脏区拓扑重建：只重建受影响构件 / 受影响管道分量周边，
	/// 语义与整图重建等价（同一容器对最多一条 Flow mapping、连通分量划分一致、环路拆一段仍连通）。
	/// 超出阈值（>30% 管道格）时退化整图重建。
	/// </summary>
	private void RebuildDirtyLocal(List<DelayedAction> actions)
	{
		Stopwatch sw = Stopwatch.StartNew();
		batchCachesDirty = true;

		// —— 1) 收集脏动作：MemberChanged 与 PipeChanged 分流 ——
		List<DelayedAction> memberActions = scratchMemberActions;
		scratchMemberActions.Clear();
		HashSet<IntVec3> pipeDirtyCells = scratchPipeDirtyCells;
		scratchPipeDirtyCells.Clear();
		for (int i = 0; i < actions.Count; i++)
		{
			DelayedAction a = actions[i];
			if (a.type == DelayedActionType.MemberChanged)
			{
				memberActions.Add(a);
			}
			else if (a.type == DelayedActionType.PipeChanged)
			{
				pipeDirtyCells.Add(a.cell);
			}
			// FullRebuild 不会走到这里：needsFullRebuild=true 时 ProcessDelayedActions 已走整图分支
		}

		// —— 2)+4) 受影响管道分量洪水收集（规格步骤 4 的主体，先于阈值粗算） ——
		HashSet<IntVec3> affectedPipeCells = scratchAffectedPipeCells;
		scratchAffectedPipeCells.Clear();
		List<HashSet<(IntVec3 cell, int channel)>> components = scratchComponents;
		scratchComponents.Clear();
		HashSet<(IntVec3 cell, int channel)> visited = scratchVisited;
		scratchVisited.Clear();
		// 放管（格还在）：从格各通道洪水；拆管（格已移除）：从 4 邻各自洪水（分裂）
		foreach (IntVec3 c in pipeDirtyCells)
		{
			if (cellToPipe.TryGetValue(c, out CompPipeCell pipe))
			{
				FloodComponentFromAllChannels(c, pipe, visited, components, affectedPipeCells);
			}
			else
			{
				foreach (IntVec3 dir in GenAdj.CardinalDirections)
				{
					IntVec3 n = c + dir;
					if (!n.InBounds(map) || !cellToPipe.TryGetValue(n, out CompPipeCell nPipe))
					{
						continue;
					}
					FloodComponentFromAllChannels(n, nPipe, visited, components, affectedPipeCells);
				}
			}
		}
		// 构件影响格落在管道上的 → 洪水（放/拆构件都会带动周边分量）
		for (int i = 0; i < memberActions.Count; i++)
		{
			List<IntVec3>? cells = memberActions[i].influenceCells;
			if (cells == null)
			{
				continue;
			}
			for (int j = 0; j < cells.Count; j++)
			{
				IntVec3 cell = cells[j];
				if (cell.IsValid && cellToPipe.TryGetValue(cell, out CompPipeCell memPipe))
				{
					FloodComponentFromAllChannels(cell, memPipe, visited, components, affectedPipeCells);
				}
			}
		}

		// —— 2) 阈值保护：受影响管道格超 30% 退化整图 ——
		if (affectedPipeCells.Count > pipeCells.Count * DirtyTopoThreshold)
		{
			RebuildAllMappings();
			return;
		}

		// —— 3) 受影响构件集 ——
		// dirtyMembers：memberActions 的 member（含已注销，供删除规则匹配容器）
		// affectedMembers：脏构件（parent != null）∪ 被指/对向构件 ∪ 受影响分量附件构件
		HashSet<CompPipeNetworkMember> dirtyMembers = scratchDirtyMembers;
		scratchDirtyMembers.Clear();
		HashSet<CompPipeNetworkMember> affectedMembers = scratchAffectedMembers;
		scratchAffectedMembers.Clear();
		for (int i = 0; i < memberActions.Count; i++)
		{
			CompPipeNetworkMember? mem = memberActions[i].member;
			if (mem == null)
			{
				continue;
			}
			dirtyMembers.Add(mem);
			if (mem.parent != null)
			{
				affectedMembers.Add(mem);
			}
		}
		// 被指/对向构件：全扫 members×ports，port.OuterCell 落在任一影响格内
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember m = members[i];
			if (m?.parent == null || !m.parent.Spawned)
			{
				continue;
			}
			for (int p = 0; p < m.Ports.Count; p++)
			{
				IntVec3 outer = m.Ports[p].OuterCell;
				for (int j = 0; j < memberActions.Count; j++)
				{
					List<IntVec3>? cells = memberActions[j].influenceCells;
					if (cells != null && cells.Contains(outer))
					{
						affectedMembers.Add(m);
						break;
					}
				}
			}
		}
		// 受影响分量附件构件
		for (int i = 0; i < components.Count; i++)
		{
			CollectAttachedMembers(components[i], affectedMembers);
		}

		// —— 6) 删除受影响区域的旧 mapping（倒序遍历） ——
		List<(Container a, Container b)> deletedPipePairs = scratchDeletedPipePairs;
		scratchDeletedPipePairs.Clear();
		HashSet<Container> affectedContainers = scratchAffectedContainers;
		scratchAffectedContainers.Clear();
		for (int i = mappings.Count - 1; i >= 0; i--)
		{
			Mapping m = mappings[i];
			// 判断映射类型：attachedBuildings 中任一建筑带 CompPipeCell → 管道映射
			bool isPipeMapping = false;
			for (int j = 0; j < m.attachedBuildings.Count; j++)
			{
				Building? b = m.attachedBuildings[j];
				if (b != null && b.GetComp<CompPipeCell>() != null)
				{
					isPipeMapping = true;
					break;
				}
			}
			bool delete = false;
			// 规则 b（最高优先）：任一端容器属于脏构件（含已注销）→ 删。
			// 适用于管道 / 直接相邻 / 内部映射：被注销构件的映射一律作废，不依赖路径判断。
			if ((m.containerA?.owner != null && dirtyMembers.Contains(m.containerA.owner))
				|| (m.containerB?.owner != null && dirtyMembers.Contains(m.containerB.owner)))
			{
				delete = true;
			}
			else if (isPipeMapping)
			{
				// 规则 a：任一管道建筑已拆除（!Spawned）或 Position 失效，或 Position 落在受影响管道格内 → 删
				// （并记录 pair 供重连检查）。
				// !Spawned 覆盖「整段分量随拆除消失、受影响区域洪水为空」的漏删场景（摧毁停漏/等价断言复现）：
				// 已拆管道不在 cellToMapping/洪水中，仅靠 Position 命中会漏删陈旧映射。
				for (int j = 0; j < m.attachedBuildings.Count; j++)
				{
					Building? b = m.attachedBuildings[j];
					if (b != null && b.GetComp<CompPipeCell>() != null
						&& (!b.Spawned || !b.Position.IsValid || affectedPipeCells.Contains(b.Position)))
					{
						delete = true;
						break;
					}
				}
				if (delete && m.containerA != null && m.containerB != null)
				{
					deletedPipePairs.Add((m.containerA, m.containerB));
				}
			}
			else
			{
				// 规则 c：直接相邻映射（>=2 建筑）任一端在受影响构件集 → 删
				if (m.attachedBuildings.Count >= 2
					&& ((m.containerA?.owner != null && affectedMembers.Contains(m.containerA.owner))
						|| (m.containerB?.owner != null && affectedMembers.Contains(m.containerB.owner))))
				{
					delete = true;
				}
			}
			if (delete)
			{
				if (m.containerA != null)
				{
					affectedContainers.Add(m.containerA);
				}
				if (m.containerB != null)
				{
					affectedContainers.Add(m.containerB);
				}
				RemoveMappingCells(m);
				mappings.RemoveAt(i);
			}
		}

		// —— 7) linkedPairs 初始化：剩余 mapping 全部预置去重 key ——
		HashSet<long> linkedPairs = scratchLinkedPairs;
		scratchLinkedPairs.Clear();
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.containerA == null || m.containerB == null || m.containerA.id < 0 || m.containerB.id < 0)
			{
				continue;
			}
			linkedPairs.Add(ContainerPairKey(m.containerA.id, m.containerB.id, m.mappingType));
		}

		// —— 8) 重建（顺序与整图一致：直接相邻 → 管道分量 Voronoi → 内部映射） ——
		// a. Face-to-face 直接相邻（仅受影响构件的端口）
		foreach (CompPipeNetworkMember a in affectedMembers)
		{
			if (a?.parent == null || !a.parent.Spawned)
			{
				continue;
			}
			for (int p = 0; p < a.Ports.Count; p++)
			{
				Port portA = a.Ports[p];
				IntVec3 outer = portA.OuterCell;
				if (!outer.InBounds(map))
				{
					continue;
				}
				CompPipeNetworkMember? b = MemberAt(outer);
				if (b == null || b == a)
				{
					continue;
				}
				Rot4 need = portA.WorldRot.Opposite;
				Port? portB = b.FindPortFacingWorld(need);
				if (portB == null)
				{
					continue;
				}
				if (!a.parent.OccupiedRect().Contains(portB.OuterCell))
				{
					continue;
				}
				TryAddPair(portA.Container, portB.Container, a.parent as Building, b.parent as Building, linkedPairs, null);
			}
		}
		// b. 管道分量 Voronoi（受影响分量逐个重建）
		for (int i = 0; i < components.Count; i++)
		{
			BuildPipeComponent(components[i], linkedPairs);
		}
		// c. 内部映射（脏构件中仍在 members 且 Spawned 者）
		foreach (CompPipeNetworkMember mem in dirtyMembers)
		{
			if (mem?.parent == null || !mem.parent.Spawned || !members.Contains(mem))
			{
				continue;
			}
			BuildInternalMappingsFor(mem, linkedPairs);
		}

		// —— 9) 环路重连检查：对「重建后仍未恢复」的被删管道 pair 找替代连通分量 ——
		List<HashSet<(IntVec3 cell, int channel)>> reconnectComponents = scratchReconnectComponents;
		scratchReconnectComponents.Clear();
		// 分量去重 key：同一分量可能被多个 pair / 附件格洪水到，用分量最小节点作规范 key
		//（List.Contains 对 HashSet 是引用相等，不能用于去重）
		HashSet<(IntVec3 cell, int channel)> reconnectSeen = scratchReconnectSeen;
		scratchReconnectSeen.Clear();
		if (deletedPipePairs.Count > 0)
		{
			// 恢复判定：当前 mappings 里存在同 key（Flow）的 pair
			HashSet<(Container, Container)> recovered = scratchRecovered;
			scratchRecovered.Clear();
			for (int i = 0; i < mappings.Count; i++)
			{
				Mapping m = mappings[i];
				if (m.mappingType != MappingType.Flow || m.containerA == null || m.containerB == null)
				{
					continue;
				}
				long key = ContainerPairKey(m.containerA.id, m.containerB.id, MappingType.Flow);
				for (int j = 0; j < deletedPipePairs.Count; j++)
				{
					(Container a, Container b) dp = deletedPipePairs[j];
					if (dp.a.id >= 0 && dp.b.id >= 0
						&& ContainerPairKey(dp.a.id, dp.b.id, MappingType.Flow) == key)
					{
						recovered.Add(dp);
					}
				}
			}
			foreach ((Container a, Container b) dp in deletedPipePairs)
			{
				if (recovered.Contains(dp))
				{
					continue;
				}
				// 已注销构件无需重连
				if (dp.a.owner == null || dp.b.owner == null
					|| !members.Contains(dp.a.owner) || !members.Contains(dp.b.owner))
				{
					continue;
				}
				// 收集两端容器的管道附件接入节点（外格 + 通道）
				List<(IntVec3 cell, int channel)> attachA = new List<(IntVec3, int)>();
				List<(IntVec3 cell, int channel)> attachB = new List<(IntVec3, int)>();
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
						if (ReferenceEquals(port.Container, dp.a))
						{
							attachA.Add((outer, g));
						}
						else if (ReferenceEquals(port.Container, dp.b))
						{
							attachB.Add((outer, g));
						}
					}
				}
				if (attachA.Count == 0 || attachB.Count == 0)
				{
					continue;
				}
				bool found = false;
				foreach ((IntVec3 ca, int chA) in attachA)
				{
					if (affectedPipeCells.Contains(ca))
					{
						continue; // 已重建过的小残段，无对端附件
					}
					HashSet<(IntVec3, int)> compCells = FloodComponentSet((ca, chA));
					if (compCells.Count > pipeCells.Count * DirtyTopoThreshold)
					{
						RebuildAllMappings();
						return;
					}
					foreach ((IntVec3 cb, int chB) in attachB)
					{
						if (compCells.Contains((cb, chB)))
						{
							// 同一分量可能被多个 pair / 附件格洪水到，取分量最小节点作规范 key 去重
							(IntVec3, int) minNode = (IntVec3.Invalid, int.MaxValue);
							foreach ((IntVec3 cc, int cch) in compCells)
							{
								if (!minNode.Item1.IsValid || cc.x < minNode.Item1.x
									|| (cc.x == minNode.Item1.x && (cc.z < minNode.Item1.z
										|| (cc.z == minNode.Item1.z && cch < minNode.Item2))))
								{
									minNode = (cc, cch);
								}
							}
							if (reconnectSeen.Add(minNode))
							{
								reconnectComponents.Add(compCells);
							}
							found = true;
							break;
						}
					}
					if (found)
					{
						break;
					}
				}
			}
			// 重连分量并入受影响管道格并重建
			if (reconnectComponents.Count > 0)
			{
				for (int i = 0; i < reconnectComponents.Count; i++)
				{
					HashSet<(IntVec3 cell, int channel)> comp = reconnectComponents[i];
					foreach ((IntVec3 cc, int _) in comp)
					{
						affectedPipeCells.Add(cc);
					}
				}
				for (int i = 0; i < reconnectComponents.Count; i++)
				{
					BuildPipeComponent(reconnectComponents[i], linkedPairs);
					CollectAttachmentContainers(reconnectComponents[i], affectedContainers);
				}
			}
		}

		// —— 9d) 泵邻接外部边刷新：局部重建后泵旁的管道边可能被删了又新建（退回 Equalize），
		// 需重收邻接缓存并重新施加 Forced（全图重建由 ContributeInternalMapping 覆盖，此处只补局部路径）。
		// 必须放在环路重连之后，确保所有新建外部边都已入列；对脏泵重复调用幂等。
		foreach (CompPipeNetworkMember mem in affectedMembers)
		{
			if (mem?.parent == null || !mem.parent.Spawned)
			{
				continue;
			}
			List<ThingComp> comps = mem.parent.AllComps;
			if (comps == null)
			{
				continue;
			}
			for (int c = 0; c < comps.Count; c++)
			{
				if (comps[c] is CompPipePump pump)
				{
					pump.RefreshAdjacentMappingsAfterLocalRebuild(this);
				}
			}
		}

		// —— 10) breach 局部化：只刷受影响区域内 Breached 管道格的新 mapping；区域外保持原值 ——
		foreach (IntVec3 cell in affectedPipeCells)
		{
			if (!cellToPipe.TryGetValue(cell, out CompPipeCell? pipe) || pipe == null || !pipe.Breached)
			{
				continue;
			}
			if (!cellToMapping.TryGetValue(cell, out List<Mapping>? list) || list == null)
			{
				continue;
			}
			for (int j = 0; j < list.Count; j++)
			{
				if (list[j] != null)
				{
					list[j].leakOpen = true;
				}
			}
		}

		// —— 11) netId 增量：受影响域重打 id，未受影响域完全不动 ——
		HashSet<Container> seedContainers = scratchSeedContainers;
		scratchSeedContainers.Clear();
		foreach (CompPipeNetworkMember mem in dirtyMembers)
		{
			if (mem?.Containers == null)
			{
				continue;
			}
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				if (mem.Containers[c] != null)
				{
					seedContainers.Add(mem.Containers[c]);
				}
			}
		}
		for (int i = 0; i < components.Count; i++)
		{
			CollectAttachmentContainers(components[i], seedContainers);
		}
		foreach (Container c in affectedContainers)
		{
			if (c != null)
			{
				seedContainers.Add(c);
			}
		}
		for (int i = 0; i < reconnectComponents.Count; i++)
		{
			CollectAttachmentContainers(reconnectComponents[i], seedContainers);
		}
		if (seedContainers.Count > 0)
		{
			ReassignNetworkIdsIncremental(seedContainers);
		}

		// —— 13) 批级缓存失效（WakeNet 已置；兜底） ——
		batchCachesDirty = true;

		// —— 14) 计时与日志 ——
		sw.Stop();
		LastTopologyRebuildMs = (float)sw.Elapsed.TotalMilliseconds;
		if (!QuietDebugLogs)
		{
			Log.Message(
				$"[RimPipe] 局部拓扑重建：dirty={dirtyMembers.Count + pipeDirtyCells.Count} 构件={affectedMembers.Count} " +
				$"管道格={affectedPipeCells.Count} 分量={components.Count} Mapping={mappings.Count} nets={netCount} " +
				$"耗时={LastTopologyRebuildMs:F3}ms");
		}
	}

	/// <summary>
	/// 从 (seed, channel) 节点洪水收集一个管道连通分量（节点 = 格子×通道）。
	/// 格内只沿本通道方向走（跨组隔离）；邻居只要对向出口开就连通（A 可连 B）。
	/// </summary>
	private void FloodPipeComponent(
		(IntVec3 cell, int channel) seed,
		HashSet<(IntVec3 cell, int channel)> visited,
		List<HashSet<(IntVec3 cell, int channel)>> components,
		HashSet<IntVec3> affectedPipeCells)
	{
		HashSet<(IntVec3, int)> comp = new HashSet<(IntVec3, int)>();
		Queue<(IntVec3 cell, int channel)> flood = scratchFloodQueue;
		scratchFloodQueue.Clear();
		visited.Add(seed);
		comp.Add(seed);
		flood.Enqueue(seed);
		while (flood.Count > 0)
		{
			(IntVec3 c, int ch) = flood.Dequeue();
			if (!cellToPipe.TryGetValue(c, out CompPipeCell pipe))
			{
				continue;
			}
			for (int i = 0; i < 4; i++)
			{
				// 格内只沿本通道的方向走（跨组隔离）
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
				if (visited.Add(node))
				{
					comp.Add(node);
					flood.Enqueue(node);
				}
			}
		}
		components.Add(comp);
		foreach ((IntVec3 cc, int _) in comp)
		{
			affectedPipeCells.Add(cc);
		}
	}

	/// <summary>从格子所有打开的通道分别洪水（A、B 各一个可能的分量种子）。</summary>
	private void FloodComponentFromAllChannels(
		IntVec3 cell,
		CompPipeCell pipe,
		HashSet<(IntVec3 cell, int channel)> visited,
		List<HashSet<(IntVec3 cell, int channel)>> components,
		HashSet<IntVec3> affectedPipeCells)
	{
		if (pipe.groupAMask != 0 && !visited.Contains((cell, CompPipeCell.GroupA)))
		{
			FloodPipeComponent((cell, CompPipeCell.GroupA), visited, components, affectedPipeCells);
		}
		if (pipe.groupBMask != 0 && !visited.Contains((cell, CompPipeCell.GroupB)))
		{
			FloodPipeComponent((cell, CompPipeCell.GroupB), visited, components, affectedPipeCells);
		}
	}

	/// <summary>从 (seed, channel) 洪水收集一个连通分量（不改 visited；供环路重连检查用）。</summary>
	private HashSet<(IntVec3 cell, int channel)> FloodComponentSet((IntVec3 cell, int channel) seed)
	{
		HashSet<(IntVec3, int)> comp = new HashSet<(IntVec3, int)>();
		Queue<(IntVec3, int)> flood = scratchFloodQueue;
		scratchFloodQueue.Clear();
		comp.Add(seed);
		flood.Enqueue(seed);
		while (flood.Count > 0)
		{
			(IntVec3 c, int ch) = flood.Dequeue();
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
				if (comp.Add(node))
				{
					flood.Enqueue(node);
				}
			}
		}
		return comp;
	}

	/// <summary>把端口接入节点落在分量内的构件收进集合（分量附件构件）。</summary>
	private void CollectAttachedMembers(HashSet<(IntVec3 cell, int channel)> componentCells, HashSet<CompPipeNetworkMember> into)
	{
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember m = members[i];
			if (m?.parent == null || !m.parent.Spawned)
			{
				continue;
			}
			for (int p = 0; p < m.Ports.Count; p++)
			{
				if (PortAttachedToComponent(m.Ports[p], componentCells))
				{
					into.Add(m);
					break;
				}
			}
		}
	}

	/// <summary>把端口接入节点落在分量内的容器收进集合（分量附件容器，供 netId 种子）。</summary>
	private void CollectAttachmentContainers(HashSet<(IntVec3 cell, int channel)> componentCells, HashSet<Container> into)
	{
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
				if (!PortAttachedToComponent(port, componentCells))
				{
					continue;
				}
				Container? cont = port.Container;
				if (cont != null)
				{
					into.Add(cont);
				}
			}
		}
	}

	/// <summary>
	/// 从 cellToMapping 索引里摘除 Mapping m。
	/// 与 CacheMappingCells 对称；已拆建筑（Spawned=false）也清理——
	/// 其格索引是 Spawned 时写入的，若不摘除会残留旧 mapping 引用（导致日后该格 dump 误判）。
	/// </summary>
	private void RemoveMappingCells(Mapping m)
	{
		for (int i = 0; i < m.attachedBuildings.Count; i++)
		{
			Building? b = m.attachedBuildings[i];
			if (b == null)
			{
				continue;
			}
			foreach (IntVec3 cell in b.OccupiedRect())
			{
				if (!cellToMapping.TryGetValue(cell, out List<Mapping>? list) || list == null)
				{
					continue;
				}
				list.Remove(m);
				if (list.Count == 0)
				{
					cellToMapping.Remove(cell);
				}
			}
		}
	}

	/// <summary>
	/// netId 增量：只对受影响 seed 容器所在的连通域重打 id（允许空洞，不压缩），
	/// 未受影响容器的 netId / sleepStates 完全不动；受影响域一律 Wake。
	/// </summary>
	private void ReassignNetworkIdsIncremental(HashSet<Container> seedContainers)
	{
		// 1) 从 seed 沿当前全局 mappings 做 BFS 收集连通域
		List<HashSet<Container>> domains = new List<HashSet<Container>>();
		HashSet<Container> visited = new HashSet<Container>();
		foreach (Container seed in seedContainers)
		{
			if (seed == null || visited.Contains(seed))
			{
				continue;
			}
			HashSet<Container> domain = new HashSet<Container>();
			Queue<Container> q = new Queue<Container>();
			q.Enqueue(seed);
			visited.Add(seed);
			domain.Add(seed);
			while (q.Count > 0)
			{
				Container c = q.Dequeue();
				for (int i = 0; i < mappings.Count; i++)
				{
					Mapping m = mappings[i];
					if (m.IsIncomplete || m.containerA == null || m.containerB == null)
					{
						continue;
					}
					Container? other = null;
					if (ReferenceEquals(m.containerA, c))
					{
						other = m.containerB;
					}
					else if (ReferenceEquals(m.containerB, c))
					{
						other = m.containerA;
					}
					if (other == null || visited.Contains(other))
					{
						continue;
					}
					visited.Add(other);
					q.Enqueue(other);
					domain.Add(other);
				}
			}
			domains.Add(domain);
		}
		if (domains.Count == 0)
		{
			return;
		}

		// 2) 分配 id：域优先复用「域内容器原 netId 中最小有效值且未被本次其他域占用」；否则新 id=netCount++
		HashSet<int> usedThisPass = new HashSet<int>();
		for (int d = 0; d < domains.Count; d++)
		{
			HashSet<Container> domain = domains[d];
			int bestId = -1;
			foreach (Container c in domain)
			{
				if (c.netId >= 0 && c.netId < netCount && !usedThisPass.Contains(c.netId))
				{
					if (bestId < 0 || c.netId < bestId)
					{
						bestId = c.netId;
					}
				}
			}
			if (bestId < 0)
			{
				bestId = netCount++;
				EnsureSleepStatesCapacity();
			}
			usedThisPass.Add(bestId);
			foreach (Container c in domain)
			{
				c.netId = bestId;
			}
			WakeNet(bestId, "topology");
		}

		// 3) 同步 Mapping.netId：两端任一属于受影响域 → 用该容器 netId
		HashSet<Container> allAffected = new HashSet<Container>();
		for (int d = 0; d < domains.Count; d++)
		{
			foreach (Container c in domains[d])
			{
				allAffected.Add(c);
			}
		}
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.containerA != null && allAffected.Contains(m.containerA))
			{
				m.netId = m.containerA.netId;
			}
			else if (m.containerB != null && allAffected.Contains(m.containerB))
			{
				m.netId = m.containerB.netId;
			}
		}
	}

	/// <summary>
	/// 按完整 Mapping 连通关系给每个 Container/Mapping 打 netId，并重建各网的 sleepStates。
	/// </summary>
	private void AssignNetworkIds()
	{
		List<Container> all = new List<Container>();
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.Containers == null)
			{
				continue;
			}
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				Container cont = mem.Containers[c];
				if (cont == null)
				{
					continue;
				}
				cont.netId = -1;
				all.Add(cont);
			}
		}

		Dictionary<Container, int> indexOf = new Dictionary<Container, int>();
		for (int i = 0; i < all.Count; i++)
		{
			indexOf[all[i]] = i;
		}

		int[] parent = new int[all.Count];
		for (int i = 0; i < parent.Length; i++)
		{
			parent[i] = i;
		}

		int Find(int x)
		{
			while (parent[x] != x)
			{
				parent[x] = parent[parent[x]];
				x = parent[x];
			}
			return x;
		}

		void Union(int a, int b)
		{
			int ra = Find(a);
			int rb = Find(b);
			if (ra != rb)
			{
				parent[rb] = ra;
			}
		}

		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			m.netId = -1;
			if (m.IsIncomplete || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			if (!indexOf.TryGetValue(m.containerA, out int ia) || !indexOf.TryGetValue(m.containerB, out int ib))
			{
				continue;
			}
			Union(ia, ib);
		}

		Dictionary<int, int> rootToNet = new Dictionary<int, int>();
		netCount = 0;
		for (int i = 0; i < all.Count; i++)
		{
			int root = Find(i);
			if (!rootToNet.TryGetValue(root, out int nid))
			{
				nid = netCount++;
				rootToNet[root] = nid;
			}
			all[i].netId = nid;
		}

		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			Container? refC = m.containerA ?? m.containerB;
			m.netId = refC != null ? refC.netId : -1;
		}

		sleepStates = new PipeNetworkSleepState[netCount];
		for (int i = 0; i < netCount; i++)
		{
			sleepStates[i] = PipeNetworkSleepState.Busy;
		}
	}

}
