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
}
