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
}
