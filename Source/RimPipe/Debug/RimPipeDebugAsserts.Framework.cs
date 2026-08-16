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
}
