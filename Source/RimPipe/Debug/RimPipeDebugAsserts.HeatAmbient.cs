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

		// 隔离 Amb-A：关闭换热器时容器仍会向环境散热，导致 |ΔT| 受室温影响。
		// 这里临时禁用环境散热，只验证“换热器自身是否阻断导热”。
		float originalMaxAmbientHeatRate = mem.Props.maxAmbientHeatRate;
		mem.Props.maxAmbientHeatRate = 0f;
		try
		{
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
		finally
		{
			mem.Props.maxAmbientHeatRate = originalMaxAmbientHeatRate;
		}
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
}
