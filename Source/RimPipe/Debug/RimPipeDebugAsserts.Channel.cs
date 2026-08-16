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
		int bestThickId = -1;
		int bestWaterId = -1;
		// 基准线用 TestFuel（v=1 c=1），与稠液线唯一区分。
		// 必须取最新且非 leakOpen 的边：旧泄漏场景的 TestFuel 边会残留，若被命中会因 leakOpen 不流动导致误报。
		FluidDef water = RimPipeDefOf.RimPipe_Fluid_TestFuel;
		FluidDef thick = RimPipeDefOf.RimPipe_Fluid_TestThick;
		for (int i = 0; i < net.Mappings.Count; i++)
		{
			Mapping m = net.Mappings[i];
			if (m.mappingType != MappingType.Flow || m.IsIncomplete || m.leakOpen || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			if (m.containerA.fluid == thick && m.containerB.fluid == thick && m.id > bestThickId)
			{
				thickM = m;
				bestThickId = m.id;
			}
			else if (m.containerA.fluid == water && m.containerB.fluid == water && m.id > bestWaterId)
			{
				waterM = m;
				bestWaterId = m.id;
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
