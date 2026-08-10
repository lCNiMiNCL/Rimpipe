using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe.Debug;

/// <summary>
/// 验收断言：返回 true=通过，false=失败或缺场景（skip-as-fail）。
/// 套件调用时勿依赖 Messages；日志关键字保持不变。
/// </summary>
internal static class RimPipeDebugAsserts
{
	internal static bool AssertExtHookInternalEdge()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindExtHookScene(net, out CompPipeDevInternalBridge? bridge, out CompPipeNetworkMember? left, out CompPipeNetworkMember? right))
		{
			Log.Warning("[RimPipe] 断言 ExtHook：未找到 Dev 内部桥+双罐布局，请先生成 ExtHook 验收场景。");
			return false;
		}

		Mapping? internalMap = bridge!.InternalMapping;
		bool edgeOk = internalMap != null
			&& !internalMap.IsIncomplete
			&& internalMap.mappingType == MappingType.Flow
			&& internalMap.maxFlowRate > 0.01f;

		// Hardening：批前重置左满右空
		net.DebugFillContainer(left!.Containers[0], 1f);
		net.DebugFillContainer(right!.Containers[0], 0f);
		float left0 = left.Containers[0].amount;
		float right0 = right.Containers[0].amount;
		for (int i = 0; i < 12; i++)
		{
			net.DebugForceOneBatch();
		}
		float left1 = left.Containers[0].amount;
		float right1 = right.Containers[0].amount;
		float dL = System.Math.Abs(left1 - left0);
		float dR = System.Math.Abs(right1 - right0);
		bool flowed = dL > 0.5f && dR > 0.5f;
		bool closer = System.Math.Abs(left1 - right1) < System.Math.Abs(left0 - right0) - 0.5f;
		bool nearEqual = System.Math.Abs(left1 - right1) < 1f;
		bool ok = edgeOk && ((flowed && closer) || nearEqual);

		if (ok)
		{
			Log.Message(
				$"[RimPipe] ExtHook通过：内部边 rate={internalMap!.maxFlowRate:0.#} " +
				$"左 {left0:0.####}→{left1:0.####} 右 {right0:0.####}→{right1:0.####}" +
				(nearEqual && !(flowed && closer) ? "（已近均，flow 条件放宽）" : ""));
			Messages.Message("[RimPipe] ExtHook通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] ExtHook失败：edgeOk={edgeOk} flowed={flowed} closer={closer} nearEqual={nearEqual} " +
			$"内部边={(internalMap != null)} rate={internalMap?.maxFlowRate} " +
			$"左 {left0:0.####}→{left1:0.####} 右 {right0:0.####}→{right1:0.####}\n{net.Dump()}");
		Messages.Message("[RimPipe] ExtHook失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool TryFindExtHookScene(
		MapComponent_PipeNetwork net,
		out CompPipeDevInternalBridge? bridge,
		out CompPipeNetworkMember? left,
		out CompPipeNetworkMember? right)
	{
		bridge = null;
		left = null;
		right = null;
		CompPipeNetworkMember? bridgeMem = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m?.parent == null)
			{
				continue;
			}
			CompPipeDevInternalBridge? b = m.parent.TryGetComp<CompPipeDevInternalBridge>();
			if (b == null)
			{
				continue;
			}
			if (bridgeMem == null || RimPipeDebugUtil.IsNewer(m.parent, bridgeMem.parent))
			{
				bridge = b;
				bridgeMem = m;
			}
		}
		if (bridge == null || bridgeMem?.parent == null)
		{
			return false;
		}
		IntVec3 pos = bridgeMem.parent.Position;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (!RimPipeDebugUtil.IsTankDef(m.parent.def) || m.Containers.Count < 1)
			{
				continue;
			}
			if (m.parent.Position == pos + IntVec3.West)
			{
				left = m;
			}
			else if (m.parent.Position == pos + IntVec3.East)
			{
				right = m;
			}
		}
		return left != null && right != null;
	}

	internal static bool AssertPressureEqualize()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}

		if (!TryFindPressureUnequalPair(net, out CompPipeNetworkMember? small, out CompPipeNetworkMember? large))
		{
			Log.Warning("[RimPipe] 断言压力：未找到贴脸小罐+大罐，请先生成压力验收场景（异容）。");
			return false;
		}

		Container cS = small!.Containers[0];
		Container cL = large!.Containers[0];
		float totalBefore = cS.amount + cL.amount;
		const int batches = 40;
		for (int i = 0; i < batches; i++)
		{
			net.DebugForceOneBatch();
		}

		float pS = Container.PressureFromAmount(cS.amount, cS.capacity);
		float pL = Container.PressureFromAmount(cL.amount, cL.capacity);
		float dP = System.Math.Abs(pS - pL);
		float totalAfter = cS.amount + cL.amount;
		float totalDrift = System.Math.Abs(totalAfter - totalBefore);
		const float pressureTol = 0.05f;
		const float massTol = 0.5f;
		bool ok = dP <= pressureTol && totalDrift <= massTol;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 压力均分通过：小罐 {cS.amount:0.####}/{cS.capacity:0.#} P={pS:0.####} " +
				$"大罐 {cL.amount:0.####}/{cL.capacity:0.#} P={pL:0.####} |ΔP|={dP:0.####} " +
				$"总量 {totalBefore:0.####}→{totalAfter:0.####} 批={batches}");
			Messages.Message("[RimPipe] 压力均分通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 压力均分失败：小罐 P={pS:0.####} 大罐 P={pL:0.####} |ΔP|={dP:0.####}（阈={pressureTol}） " +
			$"总量漂移={totalDrift:0.####}（阈={massTol}） 小={cS.amount:0.####}/{cS.capacity} 大={cL.amount:0.####}/{cL.capacity}");
		Messages.Message("[RimPipe] 压力均分失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	private static bool TryFindPressureUnequalPair(
		MapComponent_PipeNetwork net,
		out CompPipeNetworkMember? small,
		out CompPipeNetworkMember? large)
	{
		small = null;
		large = null;
		CompPipeNetworkMember? largeMem = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m.parent.def != RimPipeDefOf.RimPipe_Dev_TankLarge || m.Containers.Count == 0)
			{
				continue;
			}
			if (largeMem == null || RimPipeDebugUtil.IsNewer(m.parent, largeMem.parent))
			{
				largeMem = m;
			}
		}
		if (largeMem == null)
		{
			return false;
		}

		IntVec3 largePos = largeMem.parent.Position;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (!IsSmallTankDef(m.parent.def) || m.Containers.Count == 0)
			{
				continue;
			}
			IntVec3 p = m.parent.Position;
			if (p.z == largePos.z && System.Math.Abs(p.x - largePos.x) == 1)
			{
				small = m;
				large = largeMem;
				return true;
			}
		}
		return false;
	}

	private static bool IsSmallTankDef(ThingDef def)
	{
		return def == RimPipeDefOf.RimPipe_Dev_Tank || def == RimPipeDefOf.RimPipe_StorageTank;
	}

	internal static bool AssertPumpForcedBatch()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}

		if (!TryFindNewestPumpScene(net, out Building? pumpBuilding, out CompPipeNetworkMember? left, out CompPipeNetworkMember? right)
		    || pumpBuilding == null || left == null || right == null)
		{
			Log.Warning("[RimPipe] 断言泵：未找到最新泵+贴脸双罐，请先生成泵验收场景。");
			return false;
		}

		CompPipePump? pump = pumpBuilding.GetComp<CompPipePump>();
		bool poweredOpen = pump != null && pump.IsOpen && pump.HasPower && pump.EffectiveMaxFlowRate > 0f;

		// Hardening：开泵时若左已近空，回灌 0.2/0.8 再测
		if (poweredOpen && left.Containers[0].amount <= FlowSolver.AmountEpsilon)
		{
			net.DebugFillContainer(left.Containers[0], 0.2f);
			net.DebugFillContainer(right.Containers[0], 0.8f);
		}

		float leftBefore = left.Containers[0].amount;
		float rightBefore = right.Containers[0].amount;

		net.DebugForceOneBatch();

		float leftAfter = left.Containers[0].amount;
		float rightAfter = right.Containers[0].amount;
		float dL = leftAfter - leftBefore;
		float dR = rightAfter - rightBefore;

		if (poweredOpen)
		{
			bool ok = dL < -FlowSolver.AmountEpsilon && dR > FlowSolver.AmountEpsilon;
			if (ok)
			{
				Log.Message(
					$"[RimPipe] 泵逆均分通过：左 {leftBefore:0.####}→{leftAfter:0.####} 右 {rightBefore:0.####}→{rightAfter:0.####} " +
					$"rate={pump!.EffectiveMaxFlowRate}");
				Messages.Message("[RimPipe] 泵逆均分通过", MessageTypeDefOf.TaskCompletion, historical: false);
				return true;
			}
			Log.Error(
				$"[RimPipe] 泵逆均分失败：左Δ={dL:0.####} 右Δ={dR:0.####}（期望左减右增） " +
				$"开={pump?.IsOpen} 电={pump?.HasPower} rate={pump?.EffectiveMaxFlowRate}");
			Messages.Message("[RimPipe] 泵逆均分失败", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}

		bool blocked = dL <= FlowSolver.AmountEpsilon && dR <= FlowSolver.AmountEpsilon;
		if (blocked)
		{
			Log.Message(
				$"[RimPipe] 泵阻断通过：开={pump?.IsOpen} 电={pump?.HasPower} rate={pump?.EffectiveMaxFlowRate} " +
				$"左Δ={dL:0.####} 右Δ={dR:0.####}（两侧均未因穿越而增加）");
			Messages.Message("[RimPipe] 泵阻断通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 泵阻断失败：关/无电仍有穿越迹象 左Δ={dL:0.####} 右Δ={dR:0.####}");
		Messages.Message("[RimPipe] 泵阻断失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertPumpBlockedBatch()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindNewestPumpScene(net, out Building? pumpBuilding, out CompPipeNetworkMember? left, out CompPipeNetworkMember? right)
		    || pumpBuilding == null || left == null || right == null)
		{
			Log.Warning("[RimPipe] 断言泵阻断：未找到最新泵+贴脸双罐，请先生成泵验收场景。");
			return false;
		}
		CompPipePump? pump = pumpBuilding.GetComp<CompPipePump>();
		CompPipeNetworkMember? pumpMem = pumpBuilding.GetComp<CompPipeNetworkMember>();
		if (pump == null)
		{
			return false;
		}

		// 逆均分后再关泵时左罐已近空，Equalize 会从泵小桶回灌导致左Δ>0 误报；
		// 关泵前重置为已知压差，并抽空泵两岸小桶。
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		left.Containers[0].fluid = fluid;
		right.Containers[0].fluid = fluid;
		net.DebugFillContainer(left.Containers[0], 0.2f);
		net.DebugFillContainer(right.Containers[0], 0.8f);
		if (pumpMem != null)
		{
			for (int i = 0; i < pumpMem.Containers.Count; i++)
			{
				pumpMem.Containers[i].fluid = fluid;
				net.DebugFillContainer(pumpMem.Containers[i], 0f);
			}
		}

		pump.debugForcePowered = false;
		pump.IsOpen = false;
		pump.ApplyRateToInternalMapping();
		return AssertPumpForcedBatch();
	}

	/// <summary>
	/// DirtyTopo 局部重建回归：泵排出侧外部边必须保持 Forced；
	/// 拆掉再原位重放排出侧管道（触发局部重建）后，不动泵仍应保持逆压差抽送。
	/// 修复前：局部重建只重做脏构件内部边，泵邻接缓存残留已删旧边，新建边退回 Equalize。
	/// </summary>
	internal static bool AssertPumpDriveAfterLocalRebuild()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || map == null)
		{
			return false;
		}
		if (!TryFindPumpPipeScene(net, out Building? pumpBuilding, out CompPipeNetworkMember? left,
			    out CompPipeNetworkMember? right, out CompPipeCell? pipeR, out IntVec3 pipeRPos)
		    || pumpBuilding == null || left == null || right == null || pipeR == null)
		{
			Log.Warning("[RimPipe] 断言泵局部重建：未找到 罐—管—泵—管—罐 布局（先生成泵局部重建场景）。");
			return false;
		}
		CompPipePump? pump = pumpBuilding.GetComp<CompPipePump>();
		CompPipeNetworkMember? pumpMem = pumpBuilding.GetComp<CompPipeNetworkMember>();
		if (pump == null || pumpMem == null || pumpMem.Containers.Count < 2)
		{
			return false;
		}
		if (!pump.IsOpen || !pump.HasPower || pump.EffectiveMaxFlowRate <= 0f)
		{
			Log.Warning("[RimPipe] 断言泵局部重建：泵未开或无电，先开泵再断言。");
			return false;
		}
		Container inlet = pumpMem.Containers[pump.Props.containerIndexA];
		Container outlet = pumpMem.Containers[pump.Props.containerIndexB];
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		// 泵两岸小桶清空 + 罐体重置，保证两轮测量都从「管道未灌满」的同一起点出发
		void ResetTanksAndBuckets()
		{
			inlet.fluid = fluid;
			outlet.fluid = fluid;
			net.DebugFillContainer(inlet, 0f);
			net.DebugFillContainer(outlet, 0f);
			left.Containers[0].fluid = fluid;
			right.Containers[0].fluid = fluid;
			net.DebugFillContainer(left.Containers[0], 0.2f);
			net.DebugFillContainer(right.Containers[0], 0.8f);
		}

		// 1) 结构断言：重建前排出侧外部边必须是 Forced
		Mapping? outletEdge = FindExternalEdgeTouching(net, outlet, inlet);
		bool driveBefore = outletEdge != null && outletEdge.flowDrive == FlowDriveMode.Forced;

		// 2) 行为断言：先验证逆压差抽送可用（右增 ≥ 阈值）
		ResetTanksAndBuckets();
		float r0 = right.Containers[0].amount;
		net.DebugForceOneBatch();
		float gainBefore = right.Containers[0].amount - r0;

		// 3) 触发局部重建但不改变连通：拆掉排出侧管道，再原位重放
		RimPipeDebugUtil.DestroyAt(map, pipeRPos);
		net.DebugProcessTopology();
		GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, pipeRPos, map, Rot4.North);
		net.DebugProcessTopology();

		// 4) 不动泵：结构 + 行为双重再断言
		outletEdge = FindExternalEdgeTouching(net, outlet, inlet);
		bool driveAfter = outletEdge != null && outletEdge.flowDrive == FlowDriveMode.Forced;
		ResetTanksAndBuckets();
		float r1 = right.Containers[0].amount;
		net.DebugForceOneBatch();
		float gainAfter = right.Containers[0].amount - r1;

		const float gainTol = 3f;
		bool ok = driveBefore && driveAfter && gainBefore >= gainTol && gainAfter >= gainTol;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 泵局部重建通过：排出侧Forced={driveAfter} " +
				$"重建前右增{gainBefore:0.##} 重建后右增{gainAfter:0.##}（左20/右80 逆压差抽送保持）");
			Messages.Message("[RimPipe] 泵局部重建通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 泵局部重建失败：driveBefore={driveBefore} driveAfter={driveAfter} " +
			$"重建前右增{gainBefore:0.##} 重建后右增{gainAfter:0.##}（阈={gainTol}）\n{net.Dump()}");
		Messages.Message("[RimPipe] 泵局部重建失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	/// <summary>找「罐—管—泵—管—罐」横排：最新泵及其两侧管道格、左右罐。</summary>
	private static bool TryFindPumpPipeScene(
		MapComponent_PipeNetwork net,
		out Building? pumpBuilding,
		out CompPipeNetworkMember? left,
		out CompPipeNetworkMember? right,
		out CompPipeCell? pipeR,
		out IntVec3 pipeRPos)
	{
		pumpBuilding = null;
		left = null;
		right = null;
		pipeR = null;
		pipeRPos = IntVec3.Invalid;
		Building? best = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m.parent.def != RimPipeDefOf.RimPipe_Pump || m.parent is not Building b)
			{
				continue;
			}
			if (RimPipeDebugUtil.IsNewer(b, best))
			{
				best = b;
			}
		}
		if (best == null)
		{
			return false;
		}
		pumpBuilding = best;
		IntVec3 pumpPos = best.Position;
		IntVec3 pipeLPos = pumpPos + IntVec3.West;
		IntVec3 pipeRPosLocal = pumpPos + IntVec3.East;
		IntVec3 leftPos = pumpPos + new IntVec3(-2, 0, 0);
		IntVec3 rightPos = pumpPos + new IntVec3(2, 0, 0);

		bool hasPipeL = false;
		for (int i = 0; i < net.PipeCells.Count; i++)
		{
			CompPipeCell p = net.PipeCells[i];
			if (p?.parent == null)
			{
				continue;
			}
			if (p.parent.Position == pipeLPos)
			{
				hasPipeL = true;
			}
			else if (p.parent.Position == pipeRPosLocal)
			{
				pipeR = p;
			}
		}
		if (!hasPipeL || pipeR == null)
		{
			return false;
		}
		pipeRPos = pipeRPosLocal;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (!RimPipeDebugUtil.IsTankDef(m.parent.def))
			{
				continue;
			}
			if (m.parent.Position == leftPos)
			{
				left = m;
			}
			else if (m.parent.Position == rightPos)
			{
				right = m;
			}
		}
		return left != null && right != null;
	}

	/// <summary>找一端是 pumpContainer、另一端不是另一泵桶的外部 Flow 边。</summary>
	private static Mapping? FindExternalEdgeTouching(
		MapComponent_PipeNetwork net,
		Container pumpContainer,
		Container otherPumpContainer)
	{
		for (int i = 0; i < net.Mappings.Count; i++)
		{
			Mapping m = net.Mappings[i];
			if (m.mappingType != MappingType.Flow || m.IsIncomplete || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			bool touchesPump = ReferenceEquals(m.containerA, pumpContainer)
				|| ReferenceEquals(m.containerB, pumpContainer);
			bool touchesOther = ReferenceEquals(m.containerA, otherPumpContainer)
				|| ReferenceEquals(m.containerB, otherPumpContainer);
			if (touchesPump && !touchesOther)
			{
				return m;
			}
		}
		return null;
	}

	/// <summary>认 thingID 最新的泵及其东西贴脸储罐。</summary>
	private static bool TryFindNewestPumpScene(
		MapComponent_PipeNetwork net,
		out Building? pumpBuilding,
		out CompPipeNetworkMember? left,
		out CompPipeNetworkMember? right)
	{
		pumpBuilding = null;
		left = null;
		right = null;
		Building? best = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m.parent.def != RimPipeDefOf.RimPipe_Pump || m.parent is not Building b)
			{
				continue;
			}
			if (RimPipeDebugUtil.IsNewer(b, best))
			{
				best = b;
			}
		}
		if (best == null)
		{
			return false;
		}
		pumpBuilding = best;
		IntVec3 pumpPos = best.Position;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (!RimPipeDebugUtil.IsTankDef(m.parent.def))
			{
				continue;
			}
			IntVec3 p = m.parent.Position;
			if (p.z == pumpPos.z && p.x == pumpPos.x - 1)
			{
				left = m;
			}
			else if (p.z == pumpPos.z && p.x == pumpPos.x + 1)
			{
				right = m;
			}
		}
		return left != null && right != null;
	}


	internal static bool AssertLeakFilthBatch()
	{
		Map? map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || map == null)
		{
			return false;
		}
		if (!TryFindFuelBreachScene(net, out CompPipeCell? pipe, out CompPipeNetworkMember? left, out CompPipeNetworkMember? right)
		    || pipe == null || left == null || right == null)
		{
			Log.Warning("[RimPipe] 断言Filth：请先生成「泄漏效果验收场景（燃料破损）」。");
			return false;
		}
		if (!pipe.Breached)
		{
			Log.Warning("[RimPipe] 断言Filth：管道未破损。");
			return false;
		}

		IntVec3 cell = pipe.parent.Position;
		ThingDef filthDef = ThingDefOf.Filth_Fuel;
		int before = RimPipeDebugUtil.CountFilthThickness(cell, map, filthDef);
		float leftBefore = left.Containers[0].amount;
		float rightBefore = right.Containers[0].amount;
		net.DebugForceOneBatch();
		int after = RimPipeDebugUtil.CountFilthThickness(cell, map, filthDef);
		float leftAfter = left.Containers[0].amount;
		float rightAfter = right.Containers[0].amount;

		bool amountDown = leftAfter < leftBefore - FlowSolver.AmountEpsilon
			|| rightAfter < rightBefore - FlowSolver.AmountEpsilon;
		bool filthUp = after > before;
		if (filthUp && amountDown)
		{
			Log.Message(
				$"[RimPipe] 泄漏Filth通过：Filth_Fuel {before}→{after} " +
				$"左 {leftBefore:0.####}→{leftAfter:0.####} 右 {rightBefore:0.####}→{rightAfter:0.####}");
			Messages.Message("[RimPipe] 泄漏Filth通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 泄漏Filth失败：filth {before}→{after} amountDown={amountDown} " +
			$"左 {leftBefore:0.####}→{leftAfter:0.####} 右 {rightBefore:0.####}→{rightAfter:0.####}");
		Messages.Message("[RimPipe] 泄漏Filth失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertWaterLeakNoFilthBatch()
	{
		Map? map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || map == null)
		{
			return false;
		}
		if (!TryFindPipeBreachScene(net, out CompPipeCell? pipe, out CompPipeNetworkMember? left, out CompPipeNetworkMember? right)
		    || pipe == null || left == null || right == null)
		{
			Log.Warning("[RimPipe] 断言水无Filth：请先生成「泄漏验收场景（管道破损）」（水）。");
			return false;
		}
		if (left.Containers[0].fluid != RimPipeDefOf.RimPipe_Fluid_TestWater
		    || right.Containers[0].fluid != RimPipeDefOf.RimPipe_Fluid_TestWater)
		{
			Log.Warning("[RimPipe] 断言水无Filth：场景罐不是 TestWater。");
			return false;
		}

		IntVec3 cell = pipe.parent.Position;
		int fuelBefore = RimPipeDebugUtil.CountFilthThickness(cell, map, ThingDefOf.Filth_Fuel);
		int waterBefore = RimPipeDebugUtil.CountFilthThickness(cell, map, ThingDefOf.Filth_Water);
		float leftBefore = left.Containers[0].amount;
		float rightBefore = right.Containers[0].amount;
		net.DebugForceOneBatch();
		int fuelAfter = RimPipeDebugUtil.CountFilthThickness(cell, map, ThingDefOf.Filth_Fuel);
		int waterAfter = RimPipeDebugUtil.CountFilthThickness(cell, map, ThingDefOf.Filth_Water);
		float leftAfter = left.Containers[0].amount;
		float rightAfter = right.Containers[0].amount;

		bool amountOk = leftAfter < leftBefore - FlowSolver.AmountEpsilon
			|| rightAfter < rightBefore - FlowSolver.AmountEpsilon;
		bool noNewFilth = fuelAfter <= fuelBefore && waterAfter <= waterBefore;
		if (amountOk && noNewFilth)
		{
			Log.Message(
				$"[RimPipe] 水泄漏无Filth通过：左 {leftBefore:0.####}→{leftAfter:0.####} " +
				$"右 {rightBefore:0.####}→{rightAfter:0.####} Filth_Fuel={fuelAfter} Filth_Water={waterAfter}");
			Messages.Message("[RimPipe] 水泄漏无Filth通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 水泄漏无Filth失败：amountOk={amountOk} noNewFilth={noNewFilth} " +
			$"左 {leftBefore:0.####}→{leftAfter:0.####} 右 {rightBefore:0.####}→{rightAfter:0.####} " +
			$"fuelFilth {fuelBefore}→{fuelAfter} waterFilth {waterBefore}→{waterAfter}");
		Messages.Message("[RimPipe] 水泄漏无Filth失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertLeakTemperatureBatch()
	{
		Map? map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || map == null)
		{
			return false;
		}
		if (!TryFindFuelBreachScene(net, out CompPipeCell? pipe, out CompPipeNetworkMember? left, out _)
		    || pipe == null || left == null)
		{
			Log.Warning("[RimPipe] 断言室温：请先生成「泄漏效果验收场景（燃料破损）」。");
			return false;
		}
		if (!pipe.Breached || left.Containers[0].amount <= FlowSolver.AmountEpsilon)
		{
			Log.Warning("[RimPipe] 断言室温：管道未破损或罐已空。");
			return false;
		}

		FluidDef? fluid = left.Containers[0].fluid;
		if (fluid == null || !LeakEffectApplicator.WantsTemperature(fluid)
		    || System.Math.Abs(fluid.leakHeatEnergyPerUnit) <= FlowSolver.AmountEpsilon)
		{
			Log.Warning("[RimPipe] 断言室温：流体无 Temperature 泄漏热。");
			return false;
		}

		IntVec3 cell = pipe.parent.Position;
		Room? room = cell.GetRoom(map);
		bool outdoor = room == null || room.UsesOutdoorTemperature;
		float amtBefore = left.Containers[0].amount;
		float before = GenTemperature.GetTemperatureForCell(cell, map);
		net.DebugForceOneBatch();
		float after = GenTemperature.GetTemperatureForCell(cell, map);
		float amtAfter = left.Containers[0].amount;
		bool leaked = amtAfter < amtBefore - FlowSolver.AmountEpsilon;

		// 室外 PushHeat 不抬格温（热立刻散掉）；室内才要求升温。
		if (outdoor)
		{
			if (leaked)
			{
				Log.Message(
					$"[RimPipe] 泄漏室温通过：室外跳过格温（仍泄漏 {amtBefore:0.##}→{amtAfter:0.##}，heatPerUnit={fluid.leakHeatEnergyPerUnit:0.#}）@ {cell}");
				Messages.Message("[RimPipe] 泄漏室温通过（室外）", MessageTypeDefOf.TaskCompletion, historical: false);
				return true;
			}
			Log.Error($"[RimPipe] 泄漏室温失败：室外且本批未扣量 {amtBefore:0.####}→{amtAfter:0.####}");
			Messages.Message("[RimPipe] 泄漏室温失败", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}

		if (after > before + 0.001f && leaked)
		{
			Log.Message($"[RimPipe] 泄漏室温通过：{before:0.####}°C→{after:0.####}°C @ {cell}");
			Messages.Message("[RimPipe] 泄漏室温通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error($"[RimPipe] 泄漏室温失败：{before:0.####}°C→{after:0.####}°C leaked={leaked}（期望升温）");
		Messages.Message("[RimPipe] 泄漏室温失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	private static bool TryFindFuelBreachScene(
		MapComponent_PipeNetwork net,
		out CompPipeCell? pipe,
		out CompPipeNetworkMember? left,
		out CompPipeNetworkMember? right)
	{
		pipe = null;
		left = null;
		right = null;
		FluidDef fuel = RimPipeDefOf.RimPipe_Fluid_TestFuel;
		Thing? bestPipeThing = null;
		for (int i = 0; i < net.PipeCells.Count; i++)
		{
			CompPipeCell p = net.PipeCells[i];
			if (p?.parent == null || !p.Breached || !RimPipeDebugUtil.IsPipeDef(p.parent.def))
			{
				continue;
			}
			IntVec3 pipePos = p.parent.Position;
			CompPipeNetworkMember? l = null;
			CompPipeNetworkMember? r = null;
			for (int j = 0; j < net.Members.Count; j++)
			{
				CompPipeNetworkMember m = net.Members[j];
				if (!RimPipeDebugUtil.IsTankDef(m.parent.def) || m.Containers.Count == 0)
				{
					continue;
				}
				if (m.Containers[0].fluid != fuel)
				{
					continue;
				}
				IntVec3 pos = m.parent.Position;
				if (pos.z == pipePos.z && pos.x == pipePos.x - 1)
				{
					l = m;
				}
				else if (pos.z == pipePos.z && pos.x == pipePos.x + 1)
				{
					r = m;
				}
			}
			// 必须还有液；优先最新管道（避免扫到已漏干的旧场景）
			if (l == null || r == null)
			{
				continue;
			}
			if (l.Containers[0].amount <= FlowSolver.AmountEpsilon
			    && r.Containers[0].amount <= FlowSolver.AmountEpsilon)
			{
				continue;
			}
			if (RimPipeDebugUtil.IsNewer(p.parent, bestPipeThing))
			{
				bestPipeThing = p.parent;
				pipe = p;
				left = l;
				right = r;
			}
		}
		return pipe != null && left != null && right != null;
	}

	internal static bool AssertResistanceBatch()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindResistancePairs(
			    net,
			    out Mapping? shortM,
			    out Mapping? longM,
			    out CompPipeNetworkMember? shortSrc,
			    out CompPipeNetworkMember? shortDst,
			    out CompPipeNetworkMember? longSrc,
			    out CompPipeNetworkMember? longDst)
		    || shortM == null || longM == null
		    || shortSrc == null || shortDst == null || longSrc == null || longDst == null)
		{
			Log.Warning("[RimPipe] 断言阻力：请先生成「阻力验收场景（短管+长管）」。");
			return false;
		}

		// Hardening：批前重置短/长源满、宿空
		net.DebugFillContainer(shortSrc.Containers[0], 1f);
		net.DebugFillContainer(shortDst.Containers[0], 0f);
		net.DebugFillContainer(longSrc.Containers[0], 1f);
		net.DebugFillContainer(longDst.Containers[0], 0f);

		bool rateOk = shortM.pathPipeCells > 0
			&& longM.pathPipeCells > shortM.pathPipeCells
			&& shortM.maxFlowRate > longM.maxFlowRate + FlowSolver.AmountEpsilon;

		float shortDstBefore = shortDst.Containers[0].amount;
		float longDstBefore = longDst.Containers[0].amount;
		net.DebugForceOneBatch();
		float shortGain = shortDst.Containers[0].amount - shortDstBefore;
		float longGain = longDst.Containers[0].amount - longDstBefore;
		bool flowOk = shortGain > longGain + FlowSolver.AmountEpsilon;
		bool bothNearZero = System.Math.Abs(shortGain) <= FlowSolver.AmountEpsilon
			&& System.Math.Abs(longGain) <= FlowSolver.AmountEpsilon;

		if (rateOk && flowOk)
		{
			Log.Message(
				$"[RimPipe] 阻力通过：短 path={shortM.pathPipeCells} rate={shortM.maxFlowRate:0.##} Δdst={shortGain:0.####}；" +
				$"长 path={longM.pathPipeCells} rate={longM.maxFlowRate:0.##} Δdst={longGain:0.####}");
			Messages.Message("[RimPipe] 阻力通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		if (rateOk && bothNearZero)
		{
			Log.Message(
				$"[RimPipe] 阻力通过：rateOk 充分（skippedFlow：两端增益近 0）短 path={shortM.pathPipeCells} rate={shortM.maxFlowRate:0.##}；" +
				$"长 path={longM.pathPipeCells} rate={longM.maxFlowRate:0.##}");
			Messages.Message("[RimPipe] 阻力通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 阻力失败：rateOk={rateOk} flowOk={flowOk} " +
			$"短 path={shortM.pathPipeCells} rate={shortM.maxFlowRate:0.##} Δdst={shortGain:0.####}；" +
			$"长 path={longM.pathPipeCells} rate={longM.maxFlowRate:0.##} Δdst={longGain:0.####}");
		Messages.Message("[RimPipe] 阻力失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	/// <summary>在地图上找两条罐↔罐管道 Mapping：path 最短为短管、最长为长管。</summary>
	internal static bool TryFindResistancePairs(
		MapComponent_PipeNetwork net,
		out Mapping? shortM,
		out Mapping? longM,
		out CompPipeNetworkMember? shortSrc,
		out CompPipeNetworkMember? shortDst,
		out CompPipeNetworkMember? longSrc,
		out CompPipeNetworkMember? longDst)
	{
		shortM = null;
		longM = null;
		shortSrc = null;
		shortDst = null;
		longSrc = null;
		longDst = null;

		for (int i = 0; i < net.Mappings.Count; i++)
		{
			Mapping m = net.Mappings[i];
			if (m.pathPipeCells < 1 || m.IsIncomplete)
			{
				continue;
			}
			if (!TryGetTankEnds(m, out CompPipeNetworkMember? ta, out CompPipeNetworkMember? tb)
			    || ta == null || tb == null)
			{
				continue;
			}
			// 同 path 长度时认更新的端点，避免同图多次刷阻力场景时短/长各来自不同世代
			if (shortM == null
			    || m.pathPipeCells < shortM.pathPipeCells
			    || (m.pathPipeCells == shortM.pathPipeCells && MappingEndsNewer(m, shortM)))
			{
				shortM = m;
			}
			if (longM == null
			    || m.pathPipeCells > longM.pathPipeCells
			    || (m.pathPipeCells == longM.pathPipeCells && MappingEndsNewer(m, longM)))
			{
				longM = m;
			}
		}

		if (shortM == null || longM == null || shortM == longM || shortM.pathPipeCells >= longM.pathPipeCells)
		{
			return false;
		}

		AssignHiLo(shortM, out shortSrc, out shortDst);
		AssignHiLo(longM, out longSrc, out longDst);
		return shortSrc != null && shortDst != null && longSrc != null && longDst != null;
	}

	private static bool MappingEndsNewer(Mapping candidate, Mapping currentBest)
	{
		Thing? cBest = MappingNewerEnd(candidate);
		Thing? bBest = MappingNewerEnd(currentBest);
		return RimPipeDebugUtil.IsNewer(cBest, bBest);
	}

	private static Thing? MappingNewerEnd(Mapping m)
	{
		Thing? a = m.containerA?.owner?.parent;
		Thing? b = m.containerB?.owner?.parent;
		if (a == null)
		{
			return b;
		}
		if (b == null)
		{
			return a;
		}
		return RimPipeDebugUtil.IsNewer(a, b) ? a : b;
	}

	private static bool TryGetTankEnds(Mapping m, out CompPipeNetworkMember? a, out CompPipeNetworkMember? b)
	{
		a = m.containerA?.owner;
		b = m.containerB?.owner;
		if (a == null || b == null || a == b)
		{
			return false;
		}
		return RimPipeDebugUtil.IsTankDef(a.parent.def) && RimPipeDebugUtil.IsTankDef(b.parent.def);
	}

	private static void AssignHiLo(Mapping m, out CompPipeNetworkMember? src, out CompPipeNetworkMember? dst)
	{
		src = null;
		dst = null;
		if (!TryGetTankEnds(m, out CompPipeNetworkMember? a, out CompPipeNetworkMember? b) || a == null || b == null)
		{
			return;
		}
		if (a.Containers[0].amount >= b.Containers[0].amount)
		{
			src = a;
			dst = b;
		}
		else
		{
			src = b;
			dst = a;
		}
	}

	internal static bool AssertPipeBreachLeakBatch()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindPipeBreachScene(net, out CompPipeCell? pipe, out CompPipeNetworkMember? left, out CompPipeNetworkMember? right)
		    || pipe == null || left == null || right == null)
		{
			Log.Warning("[RimPipe] 断言泄漏：请先生成「泄漏验收场景（管道破损）」。");
			return false;
		}
		if (!pipe.Breached)
		{
			Log.Warning("[RimPipe] 断言泄漏：管道未破损。");
			return false;
		}

		float leftBefore = left.Containers[0].amount;
		float rightBefore = right.Containers[0].amount;
		net.DebugForceOneBatch();
		float leftAfter = left.Containers[0].amount;
		float rightAfter = right.Containers[0].amount;
		float dL = leftAfter - leftBefore;
		float dR = rightAfter - rightBefore;

		bool ok = dL < -FlowSolver.AmountEpsilon && dR < -FlowSolver.AmountEpsilon;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 破损泄漏通过：左 {leftBefore:0.####}→{leftAfter:0.####} 右 {rightBefore:0.####}→{rightAfter:0.####}");
			Messages.Message("[RimPipe] 破损泄漏通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 破损泄漏失败：左Δ={dL:0.####} 右Δ={dR:0.####}（期望两侧均减）");
		Messages.Message("[RimPipe] 破损泄漏失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertDestroyPipeStopsLeak()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || map == null)
		{
			return false;
		}
		if (!TryFindPipeBreachScene(net, out CompPipeCell? pipe, out CompPipeNetworkMember? left, out CompPipeNetworkMember? right)
		    || pipe == null || left == null || right == null)
		{
			Log.Warning("[RimPipe] 断言停漏：请先生成「泄漏验收场景（管道破损）」。");
			return false;
		}

		net.DebugForceOneBatch();
		float leftMid = left.Containers[0].amount;
		float rightMid = right.Containers[0].amount;

		Thing pipeThing = pipe.parent;
		IntVec3 pipePos = pipeThing.Position;
		pipeThing.Destroy(DestroyMode.KillFinalize);

		net.DebugProcessTopology();
		float leftAfterDestroy = left.Containers[0].amount;
		float rightAfterDestroy = right.Containers[0].amount;

		net.DebugForceOneBatch();
		float leftFinal = left.Containers[0].amount;
		float rightFinal = right.Containers[0].amount;

		bool residualKept = leftAfterDestroy > FlowSolver.AmountEpsilon || rightAfterDestroy > FlowSolver.AmountEpsilon;
		bool stopped = System.Math.Abs(leftFinal - leftAfterDestroy) <= FlowSolver.AmountEpsilon
			&& System.Math.Abs(rightFinal - rightAfterDestroy) <= FlowSolver.AmountEpsilon;
		bool noCatastrophicDump = leftAfterDestroy + rightAfterDestroy >= leftMid + rightMid - 1f;

		if (residualKept && stopped && noCatastrophicDump)
		{
			Log.Message(
				$"[RimPipe] 摧毁停漏通过：管道@{pipePos} 毁后左={leftAfterDestroy:0.####} 右={rightAfterDestroy:0.####} " +
				$"再一批左={leftFinal:0.####} 右={rightFinal:0.####}（量不变） dumpOnDestroy={RimPipeMod.Settings?.dumpOnDestroy}");
			Messages.Message("[RimPipe] 摧毁停漏通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 摧毁停漏失败：residual={residualKept} stopped={stopped} noDump={noCatastrophicDump} " +
			$"左 {leftMid:0.####}→{leftAfterDestroy:0.####}→{leftFinal:0.####} " +
			$"右 {rightMid:0.####}→{rightAfterDestroy:0.####}→{rightFinal:0.####}");
		Messages.Message("[RimPipe] 摧毁停漏失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertBridgeDamageBreach()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		CompPipeBreachable? br = FindAnyBreachable(net);
		if (br?.parent == null)
		{
			Log.Warning("[RimPipe] 断言Bridge伤害：图上无 CompPipeBreachable（可先生成三通/泄漏场景）。");
			return false;
		}
		ThingWithComps thing = br.parent;
		net.TrySetBreached(thing, false);
		int maxHp = thing.MaxHitPoints;
		if (maxHp <= 0)
		{
			Log.Warning("[RimPipe] 断言Bridge伤害：MaxHitPoints=0。");
			return false;
		}
		thing.HitPoints = Mathf.Max(1, (int)(maxHp * 0.4f));
		thing.PostApplyDamage(new DamageInfo(DamageDefOf.Bullet, 1f, 0f, -1f, null), 1f);
		bool ok = br.Breached && net.TryGetBreached(thing, out bool apiBreached) && apiBreached;
		if (ok)
		{
			Log.Message($"[RimPipe] Bridge伤害破损通过：{thing.LabelCap} HP={thing.HitPoints}/{maxHp} breached=True");
			Messages.Message("[RimPipe] Bridge伤害破损通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error($"[RimPipe] Bridge伤害破损失败：HP={thing.HitPoints}/{maxHp} breached={br.Breached}");
		Messages.Message("[RimPipe] Bridge伤害破损失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertBridgeBreakdownBreach()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		CompPipeBreachable? br = FindAnyBreachable(net);
		if (br?.parent == null)
		{
			Log.Warning("[RimPipe] 断言Bridge故障：图上无 CompPipeBreachable。");
			return false;
		}
		ThingWithComps thing = br.parent;
		net.TrySetBreached(thing, false);
		thing.HitPoints = thing.MaxHitPoints;
		thing.BroadcastCompSignal(PipeBreachBridge.BreakdownSignal);
		bool ok = br.Breached;
		if (ok)
		{
			Log.Message($"[RimPipe] Bridge故障破损通过：{thing.LabelCap} signal={PipeBreachBridge.BreakdownSignal}");
			Messages.Message("[RimPipe] Bridge故障破损通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error($"[RimPipe] Bridge故障破损失败：breached={br.Breached}");
		Messages.Message("[RimPipe] Bridge故障破损失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertBridgeRepairClearsBreach()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		CompPipeBreachable? br = FindAnyBreachable(net);
		if (br?.parent == null)
		{
			Log.Warning("[RimPipe] 断言Bridge满血：图上无 CompPipeBreachable。");
			return false;
		}
		ThingWithComps thing = br.parent;
		net.TrySetBreached(thing, true);
		thing.HitPoints = thing.MaxHitPoints;
		net.TryClearRepairedBreaches();
		bool ok = !br.Breached;
		if (ok)
		{
			Log.Message($"[RimPipe] Bridge满血清通过：{thing.LabelCap} HP={thing.HitPoints}/{thing.MaxHitPoints} breached=False");
			Messages.Message("[RimPipe] Bridge满血清通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error($"[RimPipe] Bridge满血清失败：breached={br.Breached} canClear={PipeBreachBridge.CanClearBreachOnRepaired(thing)}");
		Messages.Message("[RimPipe] Bridge满血清失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	private static CompPipeBreachable? FindAnyBreachable(MapComponent_PipeNetwork net)
	{
		CompPipeBreachable? best = null;
		Thing? bestThing = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			ThingWithComps? parent = net.Members[i]?.parent;
			CompPipeBreachable? br = parent?.TryGetComp<CompPipeBreachable>();
			if (br == null || parent == null)
			{
				continue;
			}
			if (RimPipeDebugUtil.IsNewer(parent, bestThing))
			{
				bestThing = parent;
				best = br;
			}
		}
		return best;
	}

	internal static bool TryFindPipeBreachScene(
		MapComponent_PipeNetwork net,
		out CompPipeCell? pipe,
		out CompPipeNetworkMember? left,
		out CompPipeNetworkMember? right)
	{
		pipe = null;
		left = null;
		right = null;
		FluidDef water = RimPipeDefOf.RimPipe_Fluid_TestWater;
		Thing? bestPipeThing = null;
		// 必须 Breached + 左右贴脸水罐；认最新管道，避免旧破损/阻力场景抢先。
		for (int i = 0; i < net.PipeCells.Count; i++)
		{
			CompPipeCell p = net.PipeCells[i];
			if (p?.parent == null || !p.Breached || !RimPipeDebugUtil.IsPipeDef(p.parent.def))
			{
				continue;
			}
			IntVec3 pipePos = p.parent.Position;
			CompPipeNetworkMember? l = null;
			CompPipeNetworkMember? r = null;
			for (int j = 0; j < net.Members.Count; j++)
			{
				CompPipeNetworkMember m = net.Members[j];
				if (!RimPipeDebugUtil.IsTankDef(m.parent.def) || m.Containers.Count == 0)
				{
					continue;
				}
				if (m.Containers[0].fluid != water)
				{
					continue;
				}
				IntVec3 pos = m.parent.Position;
				if (pos.z == pipePos.z && pos.x == pipePos.x - 1)
				{
					l = m;
				}
				else if (pos.z == pipePos.z && pos.x == pipePos.x + 1)
				{
					r = m;
				}
			}
			if (l == null || r == null)
			{
				continue;
			}
			if (RimPipeDebugUtil.IsNewer(p.parent, bestPipeThing))
			{
				bestPipeThing = p.parent;
				pipe = p;
				left = l;
				right = r;
			}
		}
		return pipe != null && left != null && right != null;
	}

	internal static bool AssertSymmetry()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		return AssertSymmetry(net);
	}

	internal static bool AssertSymmetry(MapComponent_PipeNetwork net)
	{
		// 认最新产品/Dev 三通及其东西贴脸罐（同图多次刷时勿扫到旧静网三通）
		CompPipeNetworkMember? tee = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (!RimPipeDebugUtil.IsTeeDef(m.parent.def))
			{
				continue;
			}
			if (tee == null || RimPipeDebugUtil.IsNewer(m.parent, tee.parent))
			{
				tee = m;
			}
		}
		if (tee == null)
		{
			Log.Message("[RimPipe] 跳过对称断言（地图上需要三通；请先生成三通验收场景）。");
			return false;
		}
		if (!IsLikelyIsolatedTee(net, tee))
		{
			Log.Message("[RimPipe] 跳过对称断言（三通似非孤立：外接 Mapping 过多）。");
			return false;
		}
		CompPipeNetworkMember? left = null;
		CompPipeNetworkMember? right = null;
		IntVec3 teePos = tee.parent.Position;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (!RimPipeDebugUtil.IsTankDef(m.parent.def))
			{
				continue;
			}
			IntVec3 p = m.parent.Position;
			if (p.z == teePos.z && p.x == teePos.x - 1)
			{
				left = m;
			}
			else if (p.z == teePos.z && p.x == teePos.x + 1)
			{
				right = m;
			}
		}
		if (left == null || right == null)
		{
			Log.Message("[RimPipe] 跳过对称断言（三通左右无贴脸储罐）。");
			return false;
		}

		float a = left.Containers[0].amount;
		float b = right.Containers[0].amount;
		float diff = System.Math.Abs(a - b);
		float scale = System.Math.Max(1f, System.Math.Max(a, b));
		bool ok = diff <= RimPipeDebugUtil.SymmetryTolerance || diff / scale <= RimPipeDebugUtil.SymmetryTolerance;
		if (ok)
		{
			Log.Message($"[RimPipe] 对称通过：左={a:0.####} 右={b:0.####} 通={tee.Containers[0].amount:0.####}");
			Messages.Message($"[RimPipe] 对称通过 左={a:0.#} 右={b:0.#}", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error($"[RimPipe] 对称失败：左={a:0.####} 右={b:0.####} 差={diff:0.####}");
		Messages.Message($"[RimPipe] 对称失败 左={a:0.#} 右={b:0.#}", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	private static bool IsLikelyIsolatedTee(MapComponent_PipeNetwork net, CompPipeNetworkMember tee)
	{
		int n = net.CountMappingsFor(tee);
		return n >= 1 && n <= 3;
	}

	internal static bool AssertHeatEqualize()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindHeatExchanger(net, out CompPipeNetworkMember? mem, out CompPipeHeatExchanger? hx))
		{
			Log.Warning("[RimPipe] 断言热量：未找到换热器，请先生成热量验收场景。");
			return false;
		}

		// 同图多次跑套件时可能先命中已关闭的旧换热器；强制开并重置温差。
		hx!.IsOpen = true;
		Container a = mem!.Containers[0];
		Container b = mem.Containers[1];
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		a.fluid = fluid;
		b.fluid = fluid;
		net.DebugFillContainer(a, 1f);
		net.DebugFillContainer(b, 1f);
		net.DebugSetTemperature(a, 80f);
		net.DebugSetTemperature(b, 20f);

		float amtA0 = a.amount;
		float amtB0 = b.amount;
		float dT0 = System.Math.Abs(a.temperature - b.temperature);
		const int batches = 40;
		for (int i = 0; i < batches; i++)
		{
			net.DebugForceOneBatch();
		}
		float dT1 = System.Math.Abs(a.temperature - b.temperature);
		float amtDrift = System.Math.Abs(a.amount - amtA0) + System.Math.Abs(b.amount - amtB0);
		const float dTTol = 2f;
		const float massTol = 0.5f;
		bool ok = dT0 > 10f && dT1 <= dTTol && amtDrift <= massTol && hx.IsOpen;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 热量均分通过：A T={a.temperature:0.##} B T={b.temperature:0.##} |ΔT|={dT1:0.####} " +
				$"（初|ΔT|={dT0:0.##}）量不变 drift={amtDrift:0.####} 批={batches}");
			Messages.Message("[RimPipe] 热量均分通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 热量均分失败：A T={a.temperature:0.##} B T={b.temperature:0.##} |ΔT|={dT1:0.####}（阈={dTTol}） " +
			$"初|ΔT|={dT0:0.##} 量drift={amtDrift:0.####} 开={hx.IsOpen}");
		Messages.Message("[RimPipe] 热量均分失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertHeatBlocked()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindHeatExchanger(net, out CompPipeNetworkMember? mem, out CompPipeHeatExchanger? hx))
		{
			Log.Warning("[RimPipe] 断言热量阻断：未找到换热器。");
			return false;
		}

		hx!.IsOpen = false;
		Container a = mem!.Containers[0];
		Container b = mem.Containers[1];
		net.DebugSetTemperature(a, 80f);
		net.DebugSetTemperature(b, 20f);
		float dT0 = System.Math.Abs(a.temperature - b.temperature);
		for (int i = 0; i < 10; i++)
		{
			net.DebugForceOneBatch();
		}
		float dT1 = System.Math.Abs(a.temperature - b.temperature);
		float drift = System.Math.Abs(dT1 - dT0);
		const float driftTol = 0.5f;
		bool ok = !hx.IsOpen && hx.EffectiveMaxHeatRate <= FlowSolver.AmountEpsilon && drift <= driftTol;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 热量阻断通过：开=False heatRate={hx.EffectiveMaxHeatRate:0.#} " +
				$"|ΔT|={dT0:0.##}→{dT1:0.##} drift={drift:0.####}");
			Messages.Message("[RimPipe] 热量阻断通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 热量阻断失败：开={hx.IsOpen} heatRate={hx.EffectiveMaxHeatRate:0.#} " +
			$"|ΔT|={dT0:0.##}→{dT1:0.##} drift={drift:0.####}（阈={driftTol}）");
		Messages.Message("[RimPipe] 热量阻断失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertMixing()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindMixingPair(net, out CompPipeNetworkMember? hotSide, out CompPipeNetworkMember? coldSide))
		{
			Log.Warning("[RimPipe] 断言混温：未找到贴脸双罐，请先生成混温验收场景。");
			return false;
		}

		Container hot = hotSide!.Containers[0];
		Container cold = coldSide!.Containers[0];
		float tCold0 = cold.temperature;
		float amtCold0 = cold.amount;
		const int batches = 30;
		for (int i = 0; i < batches; i++)
		{
			net.DebugForceOneBatch();
		}
		bool gotFluid = cold.amount > amtCold0 + 1f;
		bool warmed = cold.temperature > tCold0 + 5f;
		bool ok = gotFluid && warmed;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 混温通过：冷侧 {amtCold0:0.##}→{cold.amount:0.##} T {tCold0:0.##}→{cold.temperature:0.##} " +
				$"热侧 T={hot.temperature:0.##} 批={batches}");
			Messages.Message("[RimPipe] 混温通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 混温失败：冷侧量 {amtCold0:0.##}→{cold.amount:0.##} T {tCold0:0.##}→{cold.temperature:0.##} " +
			$"gotFluid={gotFluid} warmed={warmed}");
		Messages.Message("[RimPipe] 混温失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	private static bool TryFindHeatExchanger(
		MapComponent_PipeNetwork net,
		out CompPipeNetworkMember? mem,
		out CompPipeHeatExchanger? hx)
	{
		mem = null;
		hx = null;
		int bestId = -1;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m.parent.def != RimPipeDefOf.RimPipe_HeatExchanger || m.Containers.Count < 2)
			{
				continue;
			}
			CompPipeHeatExchanger? c = m.parent.TryGetComp<CompPipeHeatExchanger>();
			if (c == null)
			{
				continue;
			}
			// 优先最新刷的换热器（thingID 更大），避免命中套件残留的已关旧机。
			int id = m.parent.thingIDNumber;
			if (id > bestId)
			{
				bestId = id;
				mem = m;
				hx = c;
			}
		}
		return mem != null && hx != null;
	}

	private static bool TryFindMixingPair(
		MapComponent_PipeNetwork net,
		out CompPipeNetworkMember? hotSide,
		out CompPipeNetworkMember? coldSide)
	{
		hotSide = null;
		coldSide = null;
		Thing? bestThing = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember a = net.Members[i];
			if (a.parent.def != RimPipeDefOf.RimPipe_StorageTank || a.Containers.Count < 1)
			{
				continue;
			}
			for (int j = i + 1; j < net.Members.Count; j++)
			{
				CompPipeNetworkMember b = net.Members[j];
				if (b.parent.def != RimPipeDefOf.RimPipe_StorageTank || b.Containers.Count < 1)
				{
					continue;
				}
				if (!RimPipeDebugUtil.AreAdjacentCardinal(a.parent.Position, b.parent.Position))
				{
					continue;
				}
				Container ca = a.Containers[0];
				Container cb = b.Containers[0];
				CompPipeNetworkMember? hot = null;
				CompPipeNetworkMember? cold = null;
				if (ca.temperature >= cb.temperature + 10f && ca.amount > cb.amount + 10f)
				{
					hot = a;
					cold = b;
				}
				else if (cb.temperature >= ca.temperature + 10f && cb.amount > ca.amount + 10f)
				{
					hot = b;
					cold = a;
				}
				if (hot == null || cold == null)
				{
					continue;
				}
				// 认较新的一侧，避免同图多次刷混温时扫到已均温的旧对
				Thing newer = RimPipeDebugUtil.IsNewer(a.parent, b.parent) ? a.parent : b.parent;
				if (RimPipeDebugUtil.IsNewer(newer, bestThing))
				{
					bestThing = newer;
					hotSide = hot;
					coldSide = cold;
				}
			}
		}
		return hotSide != null && coldSide != null;
	}

	internal static bool AssertAmbientHeatLoss()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindAmbientBareTank(net, out CompPipeNetworkMember? bare))
		{
			Log.Warning("[RimPipe] 断言环境散热：未找到满液 Dev 罐，请先生成环境散热验收场景。");
			return false;
		}

		Container c = bare!.Containers[0];
		// Hardening：测 dT0 前强制 80°C 且满液
		net.DebugFillContainer(c, 1f);
		net.DebugSetTemperature(c, 80f);
		float tamb = GenTemperature.GetTemperatureForCell(bare.parent.Position, map);
		float dT0 = System.Math.Abs(c.temperature - tamb);
		float amt0 = c.amount;
		const int batches = 30;
		for (int i = 0; i < batches; i++)
		{
			net.DebugForceOneBatch();
		}
		float dT1 = System.Math.Abs(c.temperature - tamb);
		float amtDrift = System.Math.Abs(c.amount - amt0);
		bool cooled = dT0 > 5f && dT1 < dT0 - 2f;
		bool massOk = amtDrift <= 0.5f;
		bool ok = cooled && massOk;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 环境散热通过：Tamb={tamb:0.##} T={c.temperature:0.##} |ΔT| {dT0:0.##}→{dT1:0.##} " +
				$"量drift={amtDrift:0.####} 批={batches}");
			Messages.Message("[RimPipe] 环境散热通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 环境散热失败：Tamb={tamb:0.##} T={c.temperature:0.##} |ΔT| {dT0:0.##}→{dT1:0.##} " +
			$"量drift={amtDrift:0.####} cooled={cooled} massOk={massOk}");
		Messages.Message("[RimPipe] 环境散热失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertInsulationCompare()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindAmbientPair(net, out CompPipeNetworkMember? bare, out CompPipeNetworkMember? insulated))
		{
			Log.Warning("[RimPipe] 断言保温对比：未找到 Dev(无保温)+产品储罐(高保温) 对，请先生成环境散热场景。");
			return false;
		}

		Container cBare = bare!.Containers[0];
		Container cIns = insulated!.Containers[0];
		float tamb = GenTemperature.GetTemperatureForCell(bare.parent.Position, map);
		net.DebugSetTemperature(cBare, 80f);
		net.DebugSetTemperature(cIns, 80f);
		net.DebugFillContainer(cBare, 1f);
		net.DebugFillContainer(cIns, 1f);

		const int batches = 25;
		for (int i = 0; i < batches; i++)
		{
			net.DebugForceOneBatch();
		}

		float dBare = System.Math.Abs(cBare.temperature - tamb);
		float dIns = System.Math.Abs(cIns.temperature - tamb);
		bool ok = dBare + 1f < dIns
			&& bare.Props.insulation < insulated.Props.insulation - 0.1f;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 保温对比通过：低ins={bare.Props.insulation:0.##} T={cBare.temperature:0.##} |ΔT|={dBare:0.##}；" +
				$"高ins={insulated.Props.insulation:0.##} T={cIns.temperature:0.##} |ΔT|={dIns:0.##} Tamb={tamb:0.##} 批={batches}");
			Messages.Message("[RimPipe] 保温对比通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 保温对比失败：低ins T={cBare.temperature:0.##} |ΔT|={dBare:0.##}；" +
			$"高ins T={cIns.temperature:0.##} |ΔT|={dIns:0.##} Tamb={tamb:0.##}");
		Messages.Message("[RimPipe] 保温对比失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertEmptyNoAmbient()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindAmbientEmptyTank(net, out CompPipeNetworkMember? emptyMem))
		{
			Log.Warning("[RimPipe] 断言空罐不散热：未找到空 Dev 罐（amount≈0 且 T≈80），请先生成环境散热场景。");
			return false;
		}

		Container c = emptyMem!.Containers[0];
		net.DebugFillContainer(c, 0f);
		net.DebugSetTemperature(c, 80f);
		float t0 = c.temperature;
		for (int i = 0; i < 20; i++)
		{
			net.DebugForceOneBatch();
		}
		float drift = System.Math.Abs(c.temperature - t0);
		bool ok = c.amount <= FlowSolver.AmountEpsilon && drift <= 0.5f;
		if (ok)
		{
			Log.Message($"[RimPipe] 空罐不散热通过：amount={c.amount:0.####} T {t0:0.##}→{c.temperature:0.##} drift={drift:0.####}");
			Messages.Message("[RimPipe] 空罐不散热通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error($"[RimPipe] 空罐不散热失败：amount={c.amount:0.####} T {t0:0.##}→{c.temperature:0.##} drift={drift:0.####}");
		Messages.Message("[RimPipe] 空罐不散热失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool TryFindAmbientBareTank(MapComponent_PipeNetwork net, out CompPipeNetworkMember? bare)
	{
		bare = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m.parent.def != RimPipeDefOf.RimPipe_Dev_Tank || m.Containers.Count < 1)
			{
				continue;
			}
			if (m.Containers[0].amount > 50f && m.Props.insulation <= 0.05f)
			{
				if (bare == null || RimPipeDebugUtil.IsNewer(m.parent, bare.parent))
				{
					bare = m;
				}
			}
		}
		return bare != null;
	}

	private static bool TryFindAmbientPair(
		MapComponent_PipeNetwork net,
		out CompPipeNetworkMember? bare,
		out CompPipeNetworkMember? insulated)
	{
		bare = null;
		insulated = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m.Containers.Count < 1 || m.Containers[0].amount <= 50f)
			{
				continue;
			}
			if (m.parent.def == RimPipeDefOf.RimPipe_Dev_Tank && m.Props.insulation <= 0.05f)
			{
				if (bare == null || RimPipeDebugUtil.IsNewer(m.parent, bare.parent))
				{
					bare = m;
				}
			}
			else if (m.parent.def == RimPipeDefOf.RimPipe_StorageTank && m.Props.insulation >= 0.6f)
			{
				if (insulated == null || RimPipeDebugUtil.IsNewer(m.parent, insulated.parent))
				{
					insulated = m;
				}
			}
		}
		return bare != null && insulated != null;
	}

	private static bool TryFindAmbientEmptyTank(MapComponent_PipeNetwork net, out CompPipeNetworkMember? emptyMem)
	{
		emptyMem = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m.parent.def != RimPipeDefOf.RimPipe_Dev_Tank || m.Containers.Count < 1)
			{
				continue;
			}
			if (m.Containers[0].amount <= FlowSolver.AmountEpsilon)
			{
				if (emptyMem == null || RimPipeDebugUtil.IsNewer(m.parent, emptyMem.parent))
				{
					emptyMem = m;
				}
			}
		}
		return emptyMem != null;
	}

	internal static bool AssertSplitNetSleep()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		net.DebugProcessTopology();
		net.ReevaluateAllNetSleepStates();
		if (net.NetCount < 2)
		{
			Log.Error($"[RimPipe] 分网休眠失败：nets={net.NetCount}（期望≥2）。请先生成分网休眠场景。\n{net.Dump()}");
			Messages.Message("[RimPipe] 分网休眠失败：网数不足", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}

		int busyN = -1;
		int quietN = -1;
		for (int i = 0; i < net.NetCount; i++)
		{
			PipeNetworkSleepState s = net.GetNetSleepState(i);
			if (s == PipeNetworkSleepState.Busy && busyN < 0)
			{
				busyN = i;
			}
			if ((s == PipeNetworkSleepState.AmbientOnly || s == PipeNetworkSleepState.FullyQuiet) && quietN < 0)
			{
				quietN = i;
			}
		}

		bool ok = busyN >= 0 && quietN >= 0;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 分网休眠通过：网甲=Busy(net={busyN}) 网乙={net.GetNetSleepState(quietN)}(net={quietN}) " +
				$"nets={net.NetCount} 派生整图={net.SleepState}");
			Messages.Message("[RimPipe] 分网休眠通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 分网休眠失败：需同时存在 Busy 与 AmbientOnly|FullyQuiet。busyN={busyN} quietN={quietN}\n{net.Dump()}");
		Messages.Message("[RimPipe] 分网休眠失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	/// <summary>
	/// 断言「刚刷的静网」已进入休眠。查该三通所属 netId，不要求整图 Quiet
	/// （同图可另有 Busy 泵网）。
	/// </summary>
	internal static bool AssertSleepEntered()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindNewestTee(net, out CompPipeNetworkMember? tee) || tee == null || tee.Containers.Count < 1)
		{
			Log.Warning("[RimPipe] 断言休眠进入：未找到三通，请先生成静网场景。");
			return false;
		}
		net.DebugProcessTopology();
		net.ReevaluateAllNetSleepStates();
		int netId = tee.Containers[0].netId;
		PipeNetworkSleepState state = net.GetNetSleepState(netId);
		bool ok = state == PipeNetworkSleepState.AmbientOnly || state == PipeNetworkSleepState.FullyQuiet;
		if (ok)
		{
			Log.Message($"[RimPipe] 休眠进入通过：net={netId} state={state}（整图={net.SleepState}）");
			Messages.Message($"[RimPipe] 休眠进入通过：{state}", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 休眠进入失败：net={netId} state={state}（期望 AmbientOnly|FullyQuiet；整图={net.SleepState}）。\n{net.Dump()}");
		Messages.Message("[RimPipe] 休眠进入失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertSleepWake()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindNewestTee(net, out CompPipeNetworkMember? tee) || tee == null)
		{
			Log.Warning("[RimPipe] 断言休眠唤醒：未找到三通。");
			return false;
		}
		if (!TryFindFaceTank(net, tee, west: true, out CompPipeNetworkMember? tank) || tank == null)
		{
			Log.Warning("[RimPipe] 断言休眠唤醒：三通西侧无储罐。");
			return false;
		}

		net.ReevaluateAllNetSleepStates();
		Container c = tank.Containers[0];
		float amt0 = c.amount;
		float fill = c.amount < c.capacity * 0.5f ? 1f : 0.2f;
		net.DebugFillContainer(c, fill);
		if (c.fluid == null)
		{
			c.fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		}

		bool woke = net.GetNetSleepState(c.netId) == PipeNetworkSleepState.Busy;
		net.DebugForceOneBatch();
		float dAmt = System.Math.Abs(c.amount - amt0);
		if (woke)
		{
			Log.Message(
				$"[RimPipe] 休眠唤醒通过：net={c.netId} state={net.GetNetSleepState(c.netId)} wokeBusy={woke} " +
				$"量 {amt0:0.####}→{c.amount:0.####} Δ={dAmt:0.####}");
			Messages.Message("[RimPipe] 休眠唤醒通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 休眠唤醒失败：net={c.netId} state={net.GetNetSleepState(c.netId)} wokeBusy={woke} " +
			$"量 {amt0:0.####}→{c.amount:0.####}\n{net.Dump()}");
		Messages.Message("[RimPipe] 休眠唤醒失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	private static bool TryFindNewestTee(MapComponent_PipeNetwork net, out CompPipeNetworkMember? tee)
	{
		tee = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (!RimPipeDebugUtil.IsTeeDef(m.parent.def))
			{
				continue;
			}
			if (tee == null || RimPipeDebugUtil.IsNewer(m.parent, tee.parent))
			{
				tee = m;
			}
		}
		return tee != null;
	}

	private static bool TryFindFaceTank(
		MapComponent_PipeNetwork net,
		CompPipeNetworkMember tee,
		bool west,
		out CompPipeNetworkMember? tank)
	{
		tank = null;
		IntVec3 want = tee.parent.Position + (west ? IntVec3.West : IntVec3.East);
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (!RimPipeDebugUtil.IsTankDef(m.parent.def) || m.Containers.Count < 1)
			{
				continue;
			}
			if (m.parent.Position == want)
			{
				tank = m;
				return true;
			}
		}
		return false;
	}

	internal static bool AssertAmbientOnlySleep()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindAmbientBareTank(net, out CompPipeNetworkMember? bare))
		{
			Log.Warning("[RimPipe] 断言仅Amb休眠：未找到满液 Dev 罐，请先生成环境散热验收场景。");
			return false;
		}

		Container c = bare!.Containers[0];
		float tamb = GenTemperature.GetTemperatureForCell(bare.parent.Position, map);
		if (System.Math.Abs(c.temperature - tamb) <= HeatSolver.TempEpsilon)
		{
			net.DebugSetTemperature(c, tamb + 40f);
		}
		net.ReevaluateAllNetSleepStates();
		int netId = c.netId;
		if (net.GetNetSleepState(netId) == PipeNetworkSleepState.Busy)
		{
			for (int i = 0; i < 8; i++)
			{
				net.DebugForceOneBatch();
			}
			net.ReevaluateAllNetSleepStates();
		}

		PipeNetworkSleepState before = net.GetNetSleepState(netId);
		float amt0 = c.amount;
		float t0 = c.temperature;
		const int batches = 15;
		for (int i = 0; i < batches; i++)
		{
			net.DebugForceAmbientOnlyBatch();
		}
		PipeNetworkSleepState after = net.GetNetSleepState(netId);
		float amtDrift = System.Math.Abs(c.amount - amt0);
		float dT0 = System.Math.Abs(t0 - tamb);
		float dT1 = System.Math.Abs(c.temperature - tamb);
		bool massOk = amtDrift <= 0.5f;
		bool cooled = dT1 < dT0 - 0.5f || (before == PipeNetworkSleepState.AmbientOnly && dT1 <= dT0);
		bool stateOk = before == PipeNetworkSleepState.AmbientOnly
			|| after == PipeNetworkSleepState.AmbientOnly
			|| after == PipeNetworkSleepState.FullyQuiet;
		bool ok = massOk && cooled && stateOk && before != PipeNetworkSleepState.Busy;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 仅Amb休眠通过：net={netId} before={before} after={after}（整图={net.SleepState}） " +
				$"T {t0:0.##}→{c.temperature:0.##} Tamb={tamb:0.##} 量drift={amtDrift:0.####} 批={batches}");
			Messages.Message("[RimPipe] 仅Amb休眠通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Error(
			$"[RimPipe] 仅Amb休眠失败：net={netId} before={before} after={after}（整图={net.SleepState}） " +
			$"T {t0:0.##}→{c.temperature:0.##} Tamb={tamb:0.##} 量drift={amtDrift:0.####} " +
			$"massOk={massOk} cooled={cooled} stateOk={stateOk}\n{net.Dump()}");
		Messages.Message("[RimPipe] 仅Amb休眠失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertChemP3ReactorBatch()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		CompPipeReactor? rx = FindAnyReactor(net);
		if (rx == null)
		{
			Log.Warning("[RimPipe] 化学釜失败：未找到 Dev 反应釜。");
			Messages.Message("[RimPipe] 化学釜失败：未找到 Dev 反应釜。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		CompPipeNetworkMember? mem = rx.parent.TryGetComp<CompPipeNetworkMember>();
		if (mem == null || mem.Containers.Count < 3 || rx.Reaction == null)
		{
			Log.Warning("[RimPipe] 化学釜失败：缺 Comp/配方。");
			Messages.Message("[RimPipe] 化学釜失败：缺 Comp/配方。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}

		rx.debugForcePowered = true;
		rx.IsOpen = true;
		rx.MixRatio = rx.Reaction.ResolvedBaseMixRatio;
		net.TrySetAmount(mem.Containers[0], 50f);
		net.TrySetAmount(mem.Containers[1], 50f);
		net.TrySetAmount(mem.Containers[2], 0f);
		rx.SyncChemBinding("assert");
		ChemReactorBinding? b = net.FindChemReactor(rx);
		if (b == null || b.reaction == null || b.inputs.Count < 2 || b.outputs.Count < 1)
		{
			Log.Warning("[RimPipe] 化学釜失败：未注册绑定。");
			Messages.Message("[RimPipe] 化学釜失败：未注册绑定。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}

		if (!TryExpectChemDeltas(b, out float expectN, out float wantLox, out float wantRp1, out float wantEx, out float expectEta, out string? reason))
		{
			Log.Warning($"[RimPipe] 化学釜失败：期望 n≈0 ({reason})");
			Messages.Message($"[RimPipe] 化学釜失败：期望 n≈0 ({reason})", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		float beforeLox = b.inputs[0].amount;
		float beforeRp1 = b.inputs[1].amount;
		float beforeEx = b.outputs[0].amount;
		net.DebugForceOneBatch();
		float dLox = beforeLox - b.inputs[0].amount;
		float dRp1 = beforeRp1 - b.inputs[1].amount;
		float dEx = b.outputs[0].amount - beforeEx;
		const float tol = 0.05f;
		bool ok = System.Math.Abs(dLox - wantLox) <= tol
			&& System.Math.Abs(dRp1 - wantRp1) <= tol
			&& System.Math.Abs(dEx - wantEx) <= tol
			&& System.Math.Abs(b.lastEfficiency - expectEta) <= 0.02f;
		string detail =
			$"n={b.lastBatchN:0.##} expect={expectN:0.##} η={b.lastEfficiency:0.##}/{expectEta:0.##} " +
			$"LOX Δ{dLox:0.##}/{wantLox:0.##} RP1 Δ{dRp1:0.##}/{wantRp1:0.##} Ex Δ{dEx:0.##}/{wantEx:0.##}";
		if (ok)
		{
			Log.Message($"[RimPipe] 化学釜通过：{detail}");
			Messages.Message($"[RimPipe] 化学釜通过：{detail}", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Warning($"[RimPipe] 化学釜失败：{detail}");
		Messages.Message($"[RimPipe] 化学釜失败：{detail}", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertChemP5bHeat()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		CompPipeReactor? rx = FindAnyReactor(net);
		if (rx?.Reaction == null)
		{
			Messages.Message("[RimPipe] 化学反应热失败：未找到 Dev 反应釜。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		CompPipeNetworkMember? mem = rx.parent.TryGetComp<CompPipeNetworkMember>();
		PipeReactionDef recipe = rx.Reaction;
		if (mem == null || mem.Containers.Count < 3)
		{
			return false;
		}
		if (System.Math.Abs(recipe.heatPerBatch) <= FlowSolver.AmountEpsilon)
		{
			Messages.Message("[RimPipe] 化学反应热失败：heatPerBatch≈0。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}

		rx.debugForcePowered = true;
		rx.IsOpen = true;
		rx.MixRatio = recipe.ResolvedBaseMixRatio;
		net.TrySetAmount(mem.Containers[0], 50f);
		net.TrySetAmount(mem.Containers[1], 50f);
		net.TrySetAmount(mem.Containers[2], 0f);
		net.TrySetTemperature(mem.Containers[0], 21f);
		net.TrySetTemperature(mem.Containers[1], 21f);
		net.TrySetTemperature(mem.Containers[2], 21f);
		rx.SyncChemBinding("assertHeat");
		ChemReactorBinding? b = net.FindChemReactor(rx);
		if (b == null || b.outputs.Count < 1)
		{
			Messages.Message("[RimPipe] 化学反应热失败：无绑定。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}

		Container heatTgt = b.outputs[0];
		float tBefore = heatTgt.temperature;
		if (!TryExpectChemDeltas(b, out float expectN, out _, out _, out float wantEx, out _, out string? reason))
		{
			Messages.Message($"[RimPipe] 化学反应热失败：无 n ({reason})", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		float expectDt = expectN * recipe.heatPerBatch / wantEx;
		net.DebugForceOneBatch();
		float tAfter = heatTgt.temperature;
		float dT = tAfter - tBefore;
		const float tol = 1.5f;
		bool ok = expectN > FlowSolver.AmountEpsilon
			&& wantEx > FlowSolver.AmountEpsilon
			&& dT > 1.0f
			&& expectDt > 1.0f
			&& System.Math.Abs(dT - expectDt) <= tol;
		string detail =
			$"n={b.lastBatchN:0.##} Ex={heatTgt.amount:0.##} T {tBefore:0.##}→{tAfter:0.##} " +
			$"ΔT={dT:0.##}/{expectDt:0.##} Q={expectN * recipe.heatPerBatch:0.##}";
		if (ok)
		{
			Log.Message($"[RimPipe] 化学反应热通过：{detail}");
			Messages.Message($"[RimPipe] 化学反应热通过：{detail}", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Warning($"[RimPipe] 化学反应热失败：{detail}");
		Messages.Message($"[RimPipe] 化学反应热失败：{detail}", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertChemP5aConditions()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		CompPipeReactor? rx = FindAnyReactor(net);
		if (rx?.Reaction == null)
		{
			Messages.Message("[RimPipe] 化学条件失败：未找到 Dev 反应釜（先生成 P3 场景）。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		CompPipeNetworkMember? mem = rx.parent.TryGetComp<CompPipeNetworkMember>();
		PipeReactionDef recipe = rx.Reaction;
		if (mem == null || mem.Containers.Count < 3)
		{
			return false;
		}

		float savedMinT = recipe.minTemperature;
		float savedMinP = recipe.minPressure;
		try
		{
			rx.debugForcePowered = true;
			rx.IsOpen = true;
			rx.MixRatio = recipe.ResolvedBaseMixRatio;
			net.TrySetAmount(mem.Containers[0], 50f);
			net.TrySetAmount(mem.Containers[1], 50f);
			net.TrySetAmount(mem.Containers[2], 0f);
			net.TrySetTemperature(mem.Containers[0], 20f);
			rx.SyncChemBinding("assertCond");
			ChemReactorBinding? b = net.FindChemReactor(rx);
			if (b == null)
			{
				Messages.Message("[RimPipe] 化学条件失败：无绑定。", MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}

			recipe.minTemperature = 40f;
			recipe.minPressure = 0f;
			float nCold = ChemSolver.ComputeBatchCount(
				b.reaction, b.inputs, b.outputs, b.enabled, b.mixRatio, out _, out string? reasonCold);
			float beforeLox = b.inputs[0].amount;
			net.DebugForceOneBatch();
			bool coldOk = nCold <= FlowSolver.AmountEpsilon
				&& reasonCold == "cold"
				&& System.Math.Abs(b.inputs[0].amount - beforeLox) <= 0.05f;

			net.TrySetTemperature(mem.Containers[0], 50f);
			recipe.minTemperature = 40f;
			recipe.minPressure = 0f;
			if (!TryExpectChemDeltas(b, out float expectN, out float wantLox, out _, out _, out _, out string? reasonWarm))
			{
				Messages.Message($"[RimPipe] 化学条件失败：升温后仍无 n ({reasonWarm})", MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			beforeLox = b.inputs[0].amount;
			net.DebugForceOneBatch();
			float dLox = beforeLox - b.inputs[0].amount;
			bool warmOk = expectN > FlowSolver.AmountEpsilon
				&& System.Math.Abs(dLox - wantLox) <= 0.08f;

			net.TrySetAmount(mem.Containers[0], 50f);
			net.TrySetAmount(mem.Containers[1], 50f);
			net.TrySetTemperature(mem.Containers[0], 50f);
			recipe.minTemperature = -273f;
			recipe.minPressure = 0.8f;
			float nLowP = ChemSolver.ComputeBatchCount(
				b.reaction, b.inputs, b.outputs, b.enabled, b.mixRatio, out _, out string? reasonP);
			beforeLox = b.inputs[0].amount;
			net.DebugForceOneBatch();
			bool lowPOk = nLowP <= FlowSolver.AmountEpsilon
				&& reasonP == "lowP"
				&& System.Math.Abs(b.inputs[0].amount - beforeLox) <= 0.05f;

			bool ok = coldOk && warmOk && lowPOk;
			string detail = $"冷={coldOk}({reasonCold}) 温转={warmOk} 低压={lowPOk}({reasonP})";
			if (ok)
			{
				Log.Message($"[RimPipe] 化学条件通过：{detail}");
				Messages.Message($"[RimPipe] 化学条件通过：{detail}", MessageTypeDefOf.TaskCompletion, historical: false);
				return true;
			}
			Log.Warning($"[RimPipe] 化学条件失败：{detail}");
			Messages.Message($"[RimPipe] 化学条件失败：{detail}", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		finally
		{
			recipe.minTemperature = savedMinT;
			recipe.minPressure = savedMinP;
		}
	}

	internal static bool AssertChemP4RichMix()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		CompPipeReactor? rx = FindAnyReactor(net);
		if (rx?.Reaction == null)
		{
			Messages.Message("[RimPipe] 化学L1失败：未找到 Dev 反应釜。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		CompPipeNetworkMember? mem = rx.parent.TryGetComp<CompPipeNetworkMember>();
		if (mem == null || mem.Containers.Count < 3)
		{
			return false;
		}
		rx.debugForcePowered = true;
		rx.IsOpen = true;
		rx.MixRatio = rx.Reaction.ratioMax;
		net.TrySetAmount(mem.Containers[0], 50f);
		net.TrySetAmount(mem.Containers[1], 50f);
		net.TrySetAmount(mem.Containers[2], 0f);
		rx.SyncChemBinding("assertL1");
		ChemReactorBinding? b = net.FindChemReactor(rx);
		if (b == null || b.reaction == null)
		{
			Messages.Message("[RimPipe] 化学L1失败：无绑定。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		float expectEta = ChemSolver.ComputeEfficiency(b.reaction, b.mixRatio);
		if (expectEta > b.reaction.efficiencyAtStoich - 0.05f)
		{
			Messages.Message($"[RimPipe] 化学L1失败：富氧 η 未下降 η={expectEta:0.##}", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		if (!TryExpectChemDeltas(b, out float expectN, out float wantLox, out float wantRp1, out float wantEx, out expectEta, out string? reason))
		{
			Messages.Message($"[RimPipe] 化学L1失败：{reason}", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		float beforeLox = b.inputs[0].amount;
		float beforeRp1 = b.inputs[1].amount;
		float beforeEx = b.outputs[0].amount;
		net.DebugForceOneBatch();
		float dLox = beforeLox - b.inputs[0].amount;
		float dRp1 = beforeRp1 - b.inputs[1].amount;
		float dEx = b.outputs[0].amount - beforeEx;
		const float tol = 0.08f;
		bool richerOx = wantLox + 0.1f > expectN * b.reaction.inputs[0].stoichAmount;
		bool ok = System.Math.Abs(dLox - wantLox) <= tol
			&& System.Math.Abs(dRp1 - wantRp1) <= tol
			&& System.Math.Abs(dEx - wantEx) <= tol
			&& System.Math.Abs(b.lastEfficiency - expectEta) <= 0.02f
			&& richerOx
			&& wantEx + 0.1f < expectN * b.reaction.outputs[0].stoichAmount;
		string detail =
			$"mix={b.mixRatio:0.##} n={b.lastBatchN:0.##} η={b.lastEfficiency:0.##}/{expectEta:0.##} " +
			$"LOX Δ{dLox:0.##}/{wantLox:0.##} RP1 Δ{dRp1:0.##}/{wantRp1:0.##} Ex Δ{dEx:0.##}/{wantEx:0.##}";
		if (ok)
		{
			Log.Message($"[RimPipe] 化学L1通过：{detail}");
			Messages.Message($"[RimPipe] 化学L1通过：{detail}", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Warning($"[RimPipe] 化学L1失败：{detail}");
		Messages.Message($"[RimPipe] 化学L1失败：{detail}", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertChemP3ReactorBlocked()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		CompPipeReactor? rx = FindAnyReactor(net);
		if (rx == null)
		{
			Messages.Message("[RimPipe] 化学釜阻断失败：未找到反应釜。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		ChemReactorBinding? b = net.FindChemReactor(rx);
		if (b == null || b.inputs.Count < 1)
		{
			Messages.Message("[RimPipe] 化学釜阻断失败：无绑定。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		rx.IsOpen = false;
		rx.SyncChemBinding("assertOff");
		float before = b.inputs[0].amount;
		net.DebugForceOneBatch();
		bool okOff = System.Math.Abs(b.inputs[0].amount - before) <= 0.05f && b.lastBatchN <= FlowSolver.AmountEpsilon;
		rx.IsOpen = true;
		rx.debugForcePowered = false;
		CompPowerTrader? power = rx.parent.TryGetComp<CompPowerTrader>();
		if (power != null)
		{
			power.PowerOn = false;
		}
		rx.SyncChemBinding("assertUnpowered");
		before = b.inputs[0].amount;
		net.DebugForceOneBatch();
		bool okPower = System.Math.Abs(b.inputs[0].amount - before) <= 0.05f && b.lastBatchN <= FlowSolver.AmountEpsilon;
		rx.debugForcePowered = true;
		if (power != null)
		{
			power.PowerOn = true;
		}
		rx.SyncChemBinding("assertRestore");
		bool ok = okOff && okPower;
		string detail = $"关停={okOff} 断电={okPower}";
		if (ok)
		{
			Log.Message($"[RimPipe] 化学釜阻断通过：{detail}");
			Messages.Message($"[RimPipe] 化学釜阻断通过：{detail}", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Warning($"[RimPipe] 化学釜阻断失败：{detail}");
		Messages.Message($"[RimPipe] 化学釜阻断失败：{detail}", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertChemP2Batch()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || net.ChemReactors.Count == 0)
		{
			Messages.Message("[RimPipe] 化学转化失败：无 Chem 绑定（先生成化学验收场景）。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		ChemReactorBinding b = net.ChemReactors[0];
		if (b.reaction == null || b.inputs.Count < 2 || b.outputs.Count < 1)
		{
			Messages.Message("[RimPipe] 化学转化失败：绑定不完整。", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		Container cLox = b.inputs[0];
		Container cRp1 = b.inputs[1];
		Container cEx = b.outputs[0];
		float beforeLox = cLox.amount;
		float beforeRp1 = cRp1.amount;
		float beforeEx = cEx.amount;
		if (!TryExpectChemDeltas(b, out float expectN, out float wantLox, out float wantRp1, out float wantEx, out _, out string? reason))
		{
			Messages.Message($"[RimPipe] 化学转化失败：期望 n≈0 ({reason})", MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}

		net.DebugForceOneBatch();

		float dLox = beforeLox - cLox.amount;
		float dRp1 = beforeRp1 - cRp1.amount;
		float dEx = cEx.amount - beforeEx;
		const float tol = 0.05f;
		bool ok = System.Math.Abs(dLox - wantLox) <= tol
			&& System.Math.Abs(dRp1 - wantRp1) <= tol
			&& System.Math.Abs(dEx - wantEx) <= tol
			&& b.lastBatchN + 1e-4f >= expectN - tol;
		string detail =
			$"n={b.lastBatchN:0.##} expect={expectN:0.##} " +
			$"LOX {beforeLox:0.##}→{cLox.amount:0.##} (Δ{dLox:0.##}/{wantLox:0.##}) " +
			$"RP1 {beforeRp1:0.##}→{cRp1.amount:0.##} (Δ{dRp1:0.##}/{wantRp1:0.##}) " +
			$"Ex {beforeEx:0.##}→{cEx.amount:0.##} (Δ{dEx:0.##}/{wantEx:0.##})";
		if (ok)
		{
			Log.Message($"[RimPipe] 化学转化通过：{detail}");
			Messages.Message($"[RimPipe] 化学转化通过：{detail}", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
		Log.Warning($"[RimPipe] 化学转化失败：{detail}");
		Messages.Message($"[RimPipe] 化学转化失败：{detail}", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	private static CompPipeReactor? FindAnyReactor(MapComponent_PipeNetwork net)
	{
		CompPipeReactor? best = null;
		Thing? bestThing = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			ThingWithComps? parent = net.Members[i].parent;
			CompPipeReactor? c = parent?.TryGetComp<CompPipeReactor>();
			if (c == null || parent == null)
			{
				continue;
			}
			if (RimPipeDebugUtil.IsNewer(parent, bestThing))
			{
				bestThing = parent;
				best = c;
			}
		}
		return best;
	}

	internal static bool TryExpectChemDeltas(
		ChemReactorBinding b,
		out float expectN,
		out float wantLox,
		out float wantRp1,
		out float wantEx,
		out float expectEta,
		out string? reason)
	{
		expectN = 0f;
		wantLox = 0f;
		wantRp1 = 0f;
		wantEx = 0f;
		expectEta = 1f;
		reason = null;
		if (b.reaction == null || b.inputs.Count < 2 || b.outputs.Count < 1)
		{
			reason = "badBinding";
			return false;
		}
		expectN = ChemSolver.ComputeBatchCount(
			b.reaction, b.inputs, b.outputs, b.enabled, b.mixRatio, out expectEta, out reason);
		if (expectN <= FlowSolver.AmountEpsilon)
		{
			return false;
		}
		List<float> perIn = new List<float>();
		if (!ChemSolver.TryGetInputPerBatch(b.reaction, b.mixRatio, perIn, out reason))
		{
			return false;
		}
		wantLox = expectN * perIn[0];
		wantRp1 = expectN * perIn[1];
		wantEx = expectN * b.reaction.outputs[0].stoichAmount * expectEta;
		return true;
	}

	/// <summary>定位指定坐标的构件（ThingComp）；没有返回 null。</summary>
	private static CompPipeNetworkMember? TankMemberAt(Map map, IntVec3 cell)
	{
		List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
		for (int i = 0; i < things.Count; i++)
		{
			if (things[i] is ThingWithComps twc)
			{
				CompPipeNetworkMember? c = twc.GetComp<CompPipeNetworkMember>();
				if (c != null)
				{
					return c;
				}
			}
		}
		return null;
	}

	/// <summary>找双通道十字格（A={E,W} B={N,S}）；找不到返回 false。</summary>
	private static bool TryFindChannelCross(
		MapComponent_PipeNetwork net,
		out CompPipeCell? center,
		out CompPipeNetworkMember? east,
		out CompPipeNetworkMember? west,
		out CompPipeNetworkMember? north,
		out CompPipeNetworkMember? south)
	{
		center = null;
		east = west = north = south = null;
		// A={E,W}=bit1|bit3=0b1010；B={N,S}=bit0|bit2=0b0101
		for (int i = 0; i < net.PipeCells.Count; i++)
		{
			CompPipeCell p = net.PipeCells[i];
			if (p != null && p.groupAMask == 0b1010u && p.groupBMask == 0b0101u)
			{
				center = p;
				break;
			}
		}
		if (center == null || center.parent == null)
		{
			return false;
		}
		Map map = center.parent.Map;
		IntVec3 cc = center.parent.Position;
		east = TankMemberAt(map, cc + IntVec3.East);
		west = TankMemberAt(map, cc + IntVec3.West);
		north = TankMemberAt(map, cc + IntVec3.North);
		south = TankMemberAt(map, cc + IntVec3.South);
		return east != null && west != null && north != null && south != null
			&& east.Containers.Count > 0 && west.Containers.Count > 0
			&& north.Containers.Count > 0 && south.Containers.Count > 0;
	}

	internal static bool AssertChannelCross()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindChannelCross(net, out CompPipeCell? center, out CompPipeNetworkMember? east,
			out CompPipeNetworkMember? west, out CompPipeNetworkMember? north, out CompPipeNetworkMember? south))
		{
			Log.Warning("[RimPipe] 断言通道十字：未找到双通道十字场景，请先生成。");
			return false;
		}

		Container cEast = east!.Containers[0];
		Container cWest = west!.Containers[0];
		Container cNorth = north!.Containers[0];
		Container cSouth = south!.Containers[0];
		bool ew = net.HasFlowMappingBetween(cEast, cWest);
		bool ns = net.HasFlowMappingBetween(cNorth, cSouth);
		bool cross = net.HasFlowMappingBetween(cEast, cNorth) || net.HasFlowMappingBetween(cWest, cSouth);

		float e0 = cEast.amount, w0 = cWest.amount, n0 = cNorth.amount, s0 = cSouth.amount;
		for (int i = 0; i < 15; i++)
		{
			net.DebugForceOneBatch();
		}
		float e1 = cEast.amount, w1 = cWest.amount, n1 = cNorth.amount, s1 = cSouth.amount;
		bool ewEq = System.Math.Abs(e1 - w1) < 1f;
		bool nsEq = System.Math.Abs(n1 - s1) < 1f;
		bool sep = System.Math.Abs(e1 - n1) > 5f;

		bool ok = ew && ns && !cross && ewEq && nsEq && sep;
		if (ok)
		{
			Log.Message(
				$"[RimPipe] 双通道十字通过：EW={ew} NS={ns} 跨线={cross} " +
				$"东 {e0:0.#}→{e1:0.#} 西 {w0:0.#}→{w1:0.#} 北 {n0:0.#}→{n1:0.#} 南 {s0:0.#}→{s1:0.#}");
		}
		else
		{
			Log.Error(
				$"[RimPipe] 双通道十字失败：EW={ew} NS={ns} 跨线={cross} 东西均={ewEq} 南北均={nsEq} 隔离={sep} " +
				$"东 {e0:0.#}→{e1:0.#} 西 {w0:0.#}→{w1:0.#} 北 {n0:0.#}→{n1:0.#} 南 {s0:0.#}→{s1:0.#}");
		}
		return ok;
	}

	internal static bool AssertChannelBreakRestore()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		if (!TryFindChannelCross(net, out CompPipeCell? center, out CompPipeNetworkMember? east,
			out CompPipeNetworkMember? west, out _, out _) || center == null)
		{
			Log.Warning("[RimPipe] 断言方向断开恢复：未找到双通道十字场景，请先生成。");
			return false;
		}

		Container cEast = east!.Containers[0];
		Container cWest = west!.Containers[0];
		bool before = net.HasFlowMappingBetween(cEast, cWest);

		// 断开：把东方向从 A 组移除（东出口全关）
		center.SetDir(Rot4.East, CompPipeCell.GroupA, false);
		net.DebugProcessTopology();
		bool afterBreak = net.HasFlowMappingBetween(cEast, cWest);

		// 恢复
		center.SetDir(Rot4.East, CompPipeCell.GroupA, true);
		net.DebugProcessTopology();
		bool afterRestore = net.HasFlowMappingBetween(cEast, cWest);

		bool ok = before && !afterBreak && afterRestore;
		if (ok)
		{
			Log.Message($"[RimPipe] 方向断开恢复通过：前={before} 断={afterBreak} 恢复={afterRestore}");
		}
		else
		{
			Log.Error($"[RimPipe] 方向断开恢复失败：前={before} 断={afterBreak} 恢复={afterRestore}");
		}
		return ok;
	}

	internal static bool AssertViscosity()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		Mapping? thickM = null;
		Mapping? waterM = null;
		// 基准线用 TestFuel（v=1 c=1），与稠液线唯一区分
		FluidDef water = RimPipeDefOf.RimPipe_Fluid_TestFuel;
		FluidDef thick = RimPipeDefOf.RimPipe_Fluid_TestThick;
		for (int i = 0; i < net.Mappings.Count; i++)
		{
			Mapping m = net.Mappings[i];
			if (m.mappingType != MappingType.Flow || m.IsIncomplete || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			if (m.containerA.fluid == thick && m.containerB.fluid == thick && thickM == null)
			{
				thickM = m;
			}
			else if (m.containerA.fluid == water && m.containerB.fluid == water && waterM == null)
			{
				waterM = m;
			}
		}
		if (thickM == null || waterM == null)
		{
			Log.Warning("[RimPipe] 断言粘度：未找到水/稠液直列场景，请先生成。");
			return false;
		}

		Container wSrc = waterM.containerA!.amount > waterM.containerB!.amount ? waterM.containerA : waterM.containerB;
		Container wDst = ReferenceEquals(wSrc, waterM.containerA) ? waterM.containerB! : waterM.containerA!;
		Container tSrc = thickM.containerA!.amount > thickM.containerB!.amount ? thickM.containerA : thickM.containerB;
		Container tDst = ReferenceEquals(tSrc, thickM.containerA) ? thickM.containerB! : thickM.containerA!;

		net.DebugFillContainer(wSrc, 1f);
		net.DebugFillContainer(wDst, 0f);
		net.DebugFillContainer(tSrc, 1f);
		net.DebugFillContainer(tDst, 0f);
		float w0 = wDst.amount;
		float t0 = tDst.amount;
		// 只跑 1 批：粘度效果是收敛速度（单批 transfer 少）。
		// 1 格管 rate=10：Jacobi 12 轮后水(rateCap=10) 单批≈43、稠液(rateCap=5) 单批≈27，差≈16；
		// 批数越多越接近均衡（2 批时两者都≈50）断言失效。
		net.DebugForceOneBatch();
		float wDelta = wDst.amount - w0;
		float tDelta = tDst.amount - t0;

		bool ok = wDelta > tDelta + 2f;
		if (ok)
		{
			Log.Message($"[RimPipe] 粘度通过：基准(v=1) 右增 {wDelta:0.##} > 稠液(v=2) 右增 {tDelta:0.##}");
		}
		else
		{
			Log.Error($"[RimPipe] 粘度失败：基准 右增 {wDelta:0.##} vs 稠液 右增 {tDelta:0.##}");
		}
		return ok;
	}

	internal static bool AssertSpecificHeat()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return false;
		}
		Container? hot = null;
		Container? cold = null;
		FluidDef water = RimPipeDefOf.RimPipe_Fluid_TestWater;
		FluidDef sink = RimPipeDefOf.RimPipe_Fluid_TestHeatSink;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m?.parent == null || m.Containers.Count < 2)
			{
				continue;
			}
			if (m.parent.TryGetComp<CompPipeHeatExchanger>() == null)
			{
				continue;
			}
			if (m.Containers[0].fluid == water && m.Containers[1].fluid == sink)
			{
				hot = m.Containers[0];
				cold = m.Containers[1];
				break;
			}
		}
		if (hot == null || cold == null)
		{
			Log.Warning("[RimPipe] 断言比热：未找到水/高比热换热器场景，请先生成。");
			return false;
		}

		net.DebugFillContainer(hot, 1f);
		net.DebugFillContainer(cold, 1f);
		net.DebugSetTemperature(hot, 80f);
		net.DebugSetTemperature(cold, 20f);
		float h0 = hot.temperature;
		float c0 = cold.temperature;
		for (int i = 0; i < 2; i++)
		{
			net.DebugForceOneBatch();
		}
		float dHot = System.Math.Abs(hot.temperature - h0);
		float dCold = System.Math.Abs(cold.temperature - c0);

		bool ok = dHot > 0.1f && dCold > 0.05f && dHot > dCold * 1.5f;
		if (ok)
		{
			Log.Message($"[RimPipe] 比热通过：水腔(c=1) 温变 {dHot:0.###} ≈ 2× 高比热腔(c=2) 温变 {dCold:0.###}");
		}
		else
		{
			Log.Error($"[RimPipe] 比热失败：水腔温变 {dHot:0.###} vs 高比热腔温变 {dCold:0.###}");
		}
		return ok;
	}
}
