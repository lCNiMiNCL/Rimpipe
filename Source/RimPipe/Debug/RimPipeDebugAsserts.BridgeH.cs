using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe.Debug;

/// <summary>Bridge-H 注入自测断言：验证 PipeBridgeInjectDef 已给目标 Def 注入 Breachable，且破损链路可用。</summary>
internal static partial class RimPipeDebugAsserts
{
	internal static bool AssertBridgeHInjected()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || !TryFindBridgeHScene(net, out CompPipeNetworkMember? target, out _, out _)
			|| target?.parent == null)
		{
			Log.Warning("[RimPipe] 断言 Bridge-H 注入：未找到 Bridge-H 注入验收场景，请先生成场景。");
			return false;
		}

		ThingWithComps thing = target.parent;
		bool defHasBreachable = false;
		if (thing.def.comps != null)
		{
			for (int i = 0; i < thing.def.comps.Count; i++)
			{
				if (thing.def.comps[i] is CompProperties_PipeBreachable)
				{
					defHasBreachable = true;
					break;
				}
			}
		}

		CompPipeBreachable? br = thing.TryGetComp<CompPipeBreachable>();
		bool ok = defHasBreachable && br != null;
		if (ok)
		{
			Log.Message($"[RimPipe] BridgeHInjected通过：{thing.LabelCap} def.comps 含 CompProperties_PipeBreachable，运行时 Comp 存在。");
			Messages.Message("[RimPipe] BridgeHInjected通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}

		Log.Error($"[RimPipe] BridgeHInjected失败：defHasBreachable={defHasBreachable} runtimeComp={(br != null)}");
		Messages.Message("[RimPipe] BridgeHInjected失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertBridgeHDamageBreach()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || !TryFindBridgeHScene(net, out CompPipeNetworkMember? target, out _, out _)
			|| target?.parent == null)
		{
			Log.Warning("[RimPipe] 断言 Bridge-H 伤害：未找到 Bridge-H 注入验收场景。");
			return false;
		}

		ThingWithComps thing = target.parent;
		CompPipeBreachable? br = thing.TryGetComp<CompPipeBreachable>();
		if (br == null)
		{
			Log.Warning("[RimPipe] 断言 Bridge-H 伤害：目标没有 CompPipeBreachable。");
			return false;
		}

		net.TrySetBreached(thing, false);
		int maxHp = thing.MaxHitPoints;
		if (maxHp <= 0)
		{
			Log.Warning("[RimPipe] 断言 Bridge-H 伤害：MaxHitPoints=0。");
			return false;
		}

		thing.HitPoints = Mathf.Max(1, (int)(maxHp * 0.4f));
		thing.PostApplyDamage(new DamageInfo(DamageDefOf.Bullet, 1f, 0f, -1f, null), 1f);
		bool ok = br.Breached && net.TryGetBreached(thing, out bool apiBreached) && apiBreached;
		if (ok)
		{
			Log.Message($"[RimPipe] BridgeH伤害破损通过：{thing.LabelCap} HP={thing.HitPoints}/{maxHp} breached=True");
			Messages.Message("[RimPipe] BridgeH伤害破损通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}

		Log.Error($"[RimPipe] BridgeH伤害破损失败：HP={thing.HitPoints}/{maxHp} breached={br.Breached}");
		Messages.Message("[RimPipe] BridgeH伤害破损失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertBridgeHBreakdownBreach()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || !TryFindBridgeHScene(net, out CompPipeNetworkMember? target, out _, out _)
			|| target?.parent == null)
		{
			Log.Warning("[RimPipe] 断言 Bridge-H 故障：未找到 Bridge-H 注入验收场景。");
			return false;
		}

		ThingWithComps thing = target.parent;
		CompPipeBreachable? br = thing.TryGetComp<CompPipeBreachable>();
		if (br == null)
		{
			Log.Warning("[RimPipe] 断言 Bridge-H 故障：目标没有 CompPipeBreachable。");
			return false;
		}

		net.TrySetBreached(thing, false);
		thing.HitPoints = thing.MaxHitPoints;
		thing.BroadcastCompSignal(PipeBreachBridge.BreakdownSignal);
		bool ok = br.Breached;
		if (ok)
		{
			Log.Message($"[RimPipe] BridgeH故障破损通过：{thing.LabelCap} signal={PipeBreachBridge.BreakdownSignal}");
			Messages.Message("[RimPipe] BridgeH故障破损通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}

		Log.Error($"[RimPipe] BridgeH故障破损失败：breached={br.Breached}");
		Messages.Message("[RimPipe] BridgeH故障破损失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	internal static bool AssertBridgeHRepairClearsBreach()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null || !TryFindBridgeHScene(net, out CompPipeNetworkMember? target, out _, out _)
			|| target?.parent == null)
		{
			Log.Warning("[RimPipe] 断言 Bridge-H 满血：未找到 Bridge-H 注入验收场景。");
			return false;
		}

		ThingWithComps thing = target.parent;
		CompPipeBreachable? br = thing.TryGetComp<CompPipeBreachable>();
		if (br == null)
		{
			Log.Warning("[RimPipe] 断言 Bridge-H 满血：目标没有 CompPipeBreachable。");
			return false;
		}

		net.TrySetBreached(thing, true);
		thing.HitPoints = thing.MaxHitPoints;
		net.TryClearRepairedBreaches();
		bool ok = !br.Breached;
		if (ok)
		{
			Log.Message($"[RimPipe] BridgeH满血清通过：{thing.LabelCap} HP={thing.HitPoints}/{thing.MaxHitPoints} breached=False");
			Messages.Message("[RimPipe] BridgeH满血清通过", MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}

		Log.Error($"[RimPipe] BridgeH满血清失败：breached={br.Breached} canClear={PipeBreachBridge.CanClearBreachOnRepaired(thing)}");
		Messages.Message("[RimPipe] BridgeH满血清失败", MessageTypeDefOf.RejectInput, historical: false);
		return false;
	}

	private static bool TryFindBridgeHScene(
		MapComponent_PipeNetwork net,
		out CompPipeNetworkMember? target,
		out CompPipeNetworkMember? left,
		out CompPipeNetworkMember? right)
	{
		target = null;
		left = null;
		right = null;

		CompPipeNetworkMember? bestTarget = null;
		for (int i = 0; i < net.Members.Count; i++)
		{
			CompPipeNetworkMember m = net.Members[i];
			if (m?.parent == null || !RimPipeDebugUtil.IsBridgeHTargetDef(m.parent.def))
			{
				continue;
			}
			if (bestTarget == null || RimPipeDebugUtil.IsNewer(m.parent, bestTarget.parent))
			{
				bestTarget = m;
			}
		}

		if (bestTarget?.parent == null)
		{
			return false;
		}

		target = bestTarget;
		IntVec3 pos = bestTarget.parent.Position;
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
}
