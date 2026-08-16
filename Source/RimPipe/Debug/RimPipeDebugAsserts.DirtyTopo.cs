using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe.Debug;

/// <summary>
/// 验收断言（partial 拆分版）：按回归套件分文件。
/// 返回 true=通过，false=失败或缺场景（skip-as-fail）。
/// 套件调用时勿依赖 Messages；日志关键字保持不变。
/// </summary>
internal static partial class RimPipeDebugAsserts
{
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
}
