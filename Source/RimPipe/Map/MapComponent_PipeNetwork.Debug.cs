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
	internal string DebugVerifyLocalEqualsFull()
	{
		// 先冲刷尚未处理的延迟动作，避免“环路拆段回归”清理场景后残留的 Enqueue 导致 before 快照过期。
		ProcessDelayedActions();
		string before = SnapshotTopologyString();
		RequestFullRebuild();
		ProcessDelayedActions();
		string after = SnapshotTopologyString();
		if (before == after)
		{
			return "局部≈整图等价通过";
		}
		return $"局部≈整图等价失败\n——局部状态：{before}\n——整图状态：{after}";
	}

	/// <summary>
	/// 调试：环路拆段回归断言。先在空地生成 A、B 两储罐 + 两条互不 4 邻的独立管道路径，
	/// 拆掉其中一条路径的中间一段后，A、B 间应仍存在 Flow mapping（另一路径经重连恢复）。
	/// </summary>
	internal string DebugVerifyLoopReconnect()
	{
		if (map == null)
		{
			return "环路拆段回归失败：当前地图没有 MapComponent（map 为空）";
		}
		ThingDef tankDef = RimPipeDefOf.RimPipe_StorageTank;
		ThingDef pipeDef = RimPipeDefOf.RimPipe_Pipe;
		if (tankDef == null || pipeDef == null)
		{
			return "环路拆段回归失败：缺少 ThingDef（RimPipe_StorageTank / RimPipe_Pipe）";
		}

		// 布局：南线（z=0）A 东口 → (1,0)..(5,0) → B 西口；北 U 线（z=1..2）A 北口 → 上折 → B 北口。
		// 两条管线作为管道分量互不 4 邻接触，各自连通 A、B。
		IntVec3 aPos = new IntVec3(0, 0, 0);
		IntVec3 bPos = new IntVec3(6, 0, 0);
		List<IntVec3> line1 = new List<IntVec3>();
		for (int x = 1; x <= 5; x++)
		{
			line1.Add(new IntVec3(x, 0, 0));
		}
		List<IntVec3> line2 = new List<IntVec3>
		{
			new IntVec3(0, 0, 1),
			new IntVec3(0, 0, 2)
		};
		for (int x = 1; x <= 6; x++)
		{
			line2.Add(new IntVec3(x, 0, 2));
		}
		line2.Add(new IntVec3(6, 0, 1));

		if (!aPos.InBounds(map) || !bPos.InBounds(map))
		{
			return "环路拆段回归失败：原点越界";
		}
		for (int i = 0; i < line1.Count; i++)
		{
			if (!line1[i].InBounds(map))
			{
				return "环路拆段回归失败：南线越界";
			}
		}
		for (int i = 0; i < line2.Count; i++)
		{
			if (!line2[i].InBounds(map))
			{
				return "环路拆段回归失败：北线越界";
			}
		}

		// 清场后生成（清场为重复运行自愈；本测试结束 finally 再清一次，避免遗留场景）
		RimPipeDebugUtil.DestroyAt(map, aPos);
		RimPipeDebugUtil.DestroyAt(map, bPos);
		for (int i = 0; i < line1.Count; i++)
		{
			RimPipeDebugUtil.DestroyAt(map, line1[i]);
		}
		for (int i = 0; i < line2.Count; i++)
		{
			RimPipeDebugUtil.DestroyAt(map, line2[i]);
		}
		try
		{
			Building tankA = (Building)GenSpawn.Spawn(tankDef, aPos, map, Rot4.North);
			Building tankB = (Building)GenSpawn.Spawn(tankDef, bPos, map, Rot4.North);
			for (int i = 0; i < line1.Count; i++)
			{
				GenSpawn.Spawn(pipeDef, line1[i], map, Rot4.North);
			}
			for (int i = 0; i < line2.Count; i++)
			{
				GenSpawn.Spawn(pipeDef, line2[i], map, Rot4.North);
			}
			ProcessDelayedActions();

			CompPipeNetworkMember? memA = tankA.GetComp<CompPipeNetworkMember>();
			CompPipeNetworkMember? memB = tankB.GetComp<CompPipeNetworkMember>();
			if (memA == null || memB == null || memA.Containers.Count == 0 || memB.Containers.Count == 0)
			{
				return "环路拆段回归失败：储罐缺少 CompPipeNetworkMember";
			}
			Container ca = memA.Containers[0];
			Container cb = memB.Containers[0];
			bool hasBefore = HasFlowMappingBetween(ca, cb);

			// 拆除南线中间一段管道（(3,0)），触发 DeregisterPipeCell → Enqueue(PipeChanged)
			IntVec3 cutCell = new IntVec3(3, 0, 0);
			bool cut = false;
			List<Thing> things = cutCell.GetThingList(map);
			for (int i = things.Count - 1; i >= 0; i--)
			{
				if (things[i].def == pipeDef)
				{
					things[i].Destroy(DestroyMode.KillFinalize);
					cut = true;
					break;
				}
			}
			if (!cut)
			{
				return "环路拆段回归失败：未找到南线中间段管道";
			}
			ProcessDelayedActions();

			bool hasAfter = HasFlowMappingBetween(ca, cb);
			return hasBefore && hasAfter
				? "环路拆段回归通过"
				: $"环路拆段回归失败（拆前有={hasBefore} 拆后仍有={hasAfter}）";
		}
		finally
		{
			// 清理测试场景：2 罐 + 13 管（拆掉的 1 段已 Destroy），避免遗留图面并汇入管网
			RimPipeDebugUtil.DestroyAt(map, aPos);
			RimPipeDebugUtil.DestroyAt(map, bPos);
			for (int i = 0; i < line1.Count; i++)
			{
				RimPipeDebugUtil.DestroyAt(map, line1[i]);
			}
			for (int i = 0; i < line2.Count; i++)
			{
				RimPipeDebugUtil.DestroyAt(map, line2[i]);
			}
			// 冲刷清理产生的延迟动作，避免后续“局部≈整图等价断言”拿到过期局部快照。
			ProcessDelayedActions();
		}
	}

	/// <summary>拓扑快照（容器对 key 集合 + 连通域划分），用于局部/整图等价比对。</summary>
	private string SnapshotTopologyString()
	{
		StringBuilder sb = new StringBuilder();
		// 1) 容器对 key 集合
		List<long> keys = new List<long>();
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.IsIncomplete || m.containerA == null || m.containerB == null
				|| m.containerA.id < 0 || m.containerB.id < 0)
			{
				continue;
			}
			keys.Add(ContainerPairKey(m.containerA.id, m.containerB.id, m.mappingType));
		}
		keys.Sort();
		sb.Append("keys=");
		for (int i = 0; i < keys.Count; i++)
		{
			if (i > 0)
			{
				sb.Append(',');
			}
			sb.Append(keys[i]);
		}

		// 2) 连通域划分：按 mappings 连通关系（不依赖 netId 编号，只比连通构成）
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
				if (mem.Containers[c] != null)
				{
					all.Add(mem.Containers[c]);
				}
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
		Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
		for (int i = 0; i < all.Count; i++)
		{
			int root = Find(i);
			if (!groups.TryGetValue(root, out List<int>? ids))
			{
				ids = new List<int>();
				groups[root] = ids;
			}
			ids.Add(all[i].id);
		}
		List<List<int>> domains = new List<List<int>>(groups.Values);
		for (int i = 0; i < domains.Count; i++)
		{
			domains[i].Sort();
		}
		domains.Sort((x, y) => x[0].CompareTo(y[0]));
		sb.Append(" domains=");
		for (int d = 0; d < domains.Count; d++)
		{
			if (d > 0)
			{
				sb.Append(';');
			}
			sb.Append('[');
			for (int i = 0; i < domains[d].Count; i++)
			{
				if (i > 0)
				{
					sb.Append(',');
				}
				sb.Append(domains[d][i]);
			}
			sb.Append(']');
		}
		return sb.ToString();
	}

	/// <summary>调试：只冲刷延迟拓扑，不算流。</summary>
	internal void DebugProcessTopology()
	{
		ProcessDelayedActions();
	}

	/// <summary>调试：强制整图拓扑重建并打耗时日志（临时关闭 Quiet）。</summary>
	internal void DebugTimedFullRebuild()
	{
		bool prevQuiet = QuietDebugLogs;
		QuietDebugLogs = false;
		try
		{
			RequestFullRebuild();
			ProcessDelayedActions();
		}
		finally
		{
			QuietDebugLogs = prevQuiet;
		}
	}

	/// <summary>调试：无视相位与休眠，立即跑一轮 Accumulate+完整 Commit，再评估休眠。</summary>
	internal void DebugForceOneBatch()
	{
		ProcessDelayedActions();
		WakeNetwork("debugForce");
		Stopwatch swA = Stopwatch.StartNew();
		AccumulateFlow();
		AccumulateHeat();
		swA.Stop();
		LastAccumulateMs = (float)swA.Elapsed.TotalMilliseconds;
		Stopwatch swC = Stopwatch.StartNew();
		CommitDeltas();
		swC.Stop();
		LastCommitMs = (float)swC.Elapsed.TotalMilliseconds;
		Stopwatch swR = Stopwatch.StartNew();
		ReevaluateAllNetSleepStates();
		swR.Stop();
		LastReevaluateMs = (float)swR.Elapsed.TotalMilliseconds;
	}

	/// <summary>调试：仅跑 AmbientOnly 路径（泄漏+Amb），用于仅Amb断言。</summary>
	internal void DebugForceAmbientOnlyBatch()
	{
		ProcessDelayedActions();
		CommitAmbientAndLeaksOnly();
		ReevaluateAllNetSleepStates();
	}

	/// <summary>
	/// 稳定公开 API：设置管道格或可破损构件的 breached；内部走既有 setter（会 Wake、刷 leakOpen）。

	/// <summary>调试：设置容器温度（H-A）；薄包装 → TrySetTemperature。</summary>
	internal void DebugSetTemperature(Container c, float temperature)
	{
		TrySetTemperature(c, temperature);
	}

	/// <summary>调试泄漏：标记全部 Mapping 开口泄漏（两端均泄）。</summary>
	internal void DebugMarkAllLeakOpen()
	{
		for (int i = 0; i < mappings.Count; i++)
		{
			mappings[i].leakOpen = true;
		}
		WakeNetwork("debugLeakOpen");
	}

	internal string Dump()
	{
		StringBuilder sb = new StringBuilder();
		int busy = 0, amb = 0, quiet = 0;
		for (int i = 0; i < sleepStates.Length; i++)
		{
			if (sleepStates[i] == PipeNetworkSleepState.Busy)
			{
				busy++;
			}
			else if (sleepStates[i] == PipeNetworkSleepState.AmbientOnly)
			{
				amb++;
			}
			else
			{
				quiet++;
			}
		}
		sb.AppendLine(
			$"[RimPipe] map schema={rimPipeSchemaVersion} members={members.Count} pipes={pipeCells.Count} mappings={mappings.Count} " +
			$"chem={chemReactors.Count} pending={pendingDeltas.Count} nets={netCount} sleepState={SleepState} " +
			$"(busy={busy} amb={amb} quiet={quiet}) overlay={showFlowPressureOverlay} " +
			$"lastTopoMs={LastTopologyRebuildMs:F3} lastAccMs={LastAccumulateMs:F3} lastCommitMs={LastCommitMs:F3} lastReevalMs={LastReevaluateMs:F3}");
		for (int n = 0; n < sleepStates.Length; n++)
		{
			sb.AppendLine($"  Net#{n} state={sleepStates[n]}");
		}
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember m = members[i];
			CompPipeBreachable? br = m.parent.TryGetComp<CompPipeBreachable>();
			string breach = br != null && br.Breached ? " breached" : "";
			sb.AppendLine($"  Member {m.parent.LabelCap}@{m.parent.Position}{breach}");
			for (int c = 0; c < m.Containers.Count; c++)
			{
				sb.AppendLine($"    {m.Containers[c]}");
			}
		}
		for (int i = 0; i < pipeCells.Count; i++)
		{
			CompPipeCell p = pipeCells[i];
			sb.AppendLine($"  Pipe@{p.parent.Position} breached={p.Breached}");
		}
		for (int i = 0; i < mappings.Count; i++)
		{
			sb.AppendLine($"  {mappings[i]} incomplete={mappings[i].IsIncomplete}");
		}
		for (int i = 0; i < chemReactors.Count; i++)
		{
			ChemReactorBinding b = chemReactors[i];
			string rx = b.reaction != null ? b.reaction.defName : "null";
			sb.AppendLine(
				$"  Chem#{i} {rx} on={b.enabled} mix={b.mixRatio:0.###} η={b.lastEfficiency:0.###} " +
				$"lastN={b.lastBatchN:0.##} in={b.inputs.Count} out={b.outputs.Count}");
		}
		return sb.ToString();
	}

	/// <summary>调试：按容量比例填液；薄包装 → TrySetAmount。</summary>
	internal void DebugFillContainer(Container c, float fraction)
	{
		if (c == null)
		{
			return;
		}
		TrySetAmount(c, c.capacity * fraction);
	}

	private void DrawFlowPressureOverlay()
	{
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.parent == null || !mem.parent.Spawned || mem.Containers.Count == 0)
			{
				continue;
			}
			float p = ClampPressure01(mem.Containers[0].pressure);
			// colorPct：0=绿 … 0.5=蓝 … 1≈红（与 DebugCellDrawer / CellRenderer 约定一致）
			// 不用 FlashCell：它会往 debugDrawer 列表里 Add，Update 频率高于 Tick 时列表膨胀。
			CellRenderer.RenderCell(mem.parent.Position, p);
		}

		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.mappingType != MappingType.Flow || m.IsIncomplete
				|| m.containerA?.owner?.parent == null || m.containerB?.owner?.parent == null)
			{
				continue;
			}
			if (m.lastBatchWant <= FlowSolver.AmountEpsilon)
			{
				continue;
			}
			IntVec3 a = m.containerA.owner.parent.Position;
			IntVec3 b = m.containerB.owner.parent.Position;
			Vector3 from = (m.lastBatchFromA ? a : b).ToVector3Shifted();
			Vector3 to = (m.lastBatchFromA ? b : a).ToVector3Shifted();
			float width = 0.05f + System.Math.Min(m.lastBatchWant / 20f, 1f) * 0.25f;
			GenDraw.DrawLineBetween(from, to, SimpleColor.Cyan, width);
		}
	}

	/// <summary>
	/// 近距才画 P= 标签（对齐官方 DebugDrawerOnGUI）。不经 FlashCell，避免列表膨胀。
	/// </summary>
	private void DrawFlowPressureOverlayLabels()
	{
		if (Find.CameraDriver.CurrentZoom != CameraZoomRange.Closest)
		{
			return;
		}
		Text.Font = GameFont.Tiny;
		Text.Anchor = TextAnchor.MiddleCenter;
		GUI.color = new Color(1f, 1f, 1f, 0.5f);
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.parent == null || !mem.parent.Spawned || mem.Containers.Count == 0)
			{
				continue;
			}
			float p = ClampPressure01(mem.Containers[0].pressure);
			Vector2 ui = mem.parent.Position.ToUIPosition();
			Rect rect = new Rect(ui.x - 20f, ui.y - 20f, 40f, 40f);
			if (new Rect(0f, 0f, UI.screenWidth, UI.screenHeight).Overlaps(rect))
			{
				Widgets.Label(rect, $"P={p:0.##}");
			}
		}
		GUI.color = Color.white;
		Text.Anchor = TextAnchor.UpperLeft;
	}

	private static float ClampPressure01(float p)
	{
		if (p < 0f)
		{
			return 0f;
		}
		if (p > 1f)
		{
			return 1f;
		}
		return p;
	}
}
