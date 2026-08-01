using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimPipe;

/// <summary>
/// 化学反应算量：按配方从入腔扣、给出腔加。
/// 支持可调混合比 mixRatio 和效率 η；还能看入腔温度/压力够不够门槛，不够就本批不做。
/// </summary>
public static class ChemSolver
{
	/// <summary>解析条件监测腔（相对 inputs；&lt;0 → 第一入腔）。</summary>
	public static Container? ResolveConditionContainer(PipeReactionDef reaction, IList<Container> inputs)
	{
		if (inputs == null || inputs.Count == 0)
		{
			return null;
		}
		int idx = reaction.conditionContainerIndex;
		if (idx < 0)
		{
			idx = 0;
		}
		if (idx >= inputs.Count)
		{
			return null;
		}
		return inputs[idx];
	}

	/// <summary>P5a：T≥minTemperature 且 P≥minPressure；失败写出 failReason。</summary>
	public static bool PassesConditions(
		PipeReactionDef reaction,
		IList<Container> inputs,
		out string? failReason)
	{
		failReason = null;
		Container? cond = ResolveConditionContainer(reaction, inputs);
		if (cond == null)
		{
			failReason = "condSlot";
			return false;
		}
		if (cond.temperature < reaction.minTemperature)
		{
			failReason = "cold";
			return false;
		}
		float p = Container.PressureFromAmount(cond.amount, cond.capacity);
		if (p < reaction.minPressure)
		{
			failReason = "lowP";
			return false;
		}
		return true;
	}

	public static float ClampMixRatio(PipeReactionDef reaction, float mixRatio)
	{
		if (mixRatio <= 0f)
		{
			mixRatio = reaction.ResolvedBaseMixRatio;
		}
		return Mathf.Clamp(mixRatio, reaction.ratioMin, reaction.ratioMax);
	}

	/// <summary>
	/// 偏比效率：stoich→efficiencyAtStoich；ratioMin/Max→efficiencyAtRatioEdge；中间线性。
	/// </summary>
	public static float ComputeEfficiency(PipeReactionDef reaction, float mixRatio)
	{
		float r = ClampMixRatio(reaction, mixRatio);
		float stoich = reaction.StoichMixRatio;
		float edge = reaction.efficiencyAtRatioEdge;
		float peak = reaction.efficiencyAtStoich;
		if (stoich <= FlowSolver.AmountEpsilon)
		{
			return peak;
		}
		if (Mathf.Abs(r - stoich) <= 1e-4f)
		{
			return peak;
		}
		if (r < stoich)
		{
			float span = stoich - reaction.ratioMin;
			if (span <= FlowSolver.AmountEpsilon)
			{
				return edge;
			}
			float t = (r - reaction.ratioMin) / span;
			return Mathf.Lerp(edge, peak, Mathf.Clamp01(t));
		}
		else
		{
			float span = reaction.ratioMax - stoich;
			if (span <= FlowSolver.AmountEpsilon)
			{
				return edge;
			}
			float t = (r - stoich) / span;
			return Mathf.Lerp(peak, edge, Mathf.Clamp01(t));
		}
	}

	/// <summary>每 1 批单位各输入消耗量（已按 mixRatio）；失败返回 false。</summary>
	public static bool TryGetInputPerBatch(
		PipeReactionDef reaction,
		float mixRatio,
		List<float> intoPerBatch,
		out string? failReason)
	{
		intoPerBatch.Clear();
		failReason = null;
		float r = ClampMixRatio(reaction, mixRatio);
		if (!reaction.TryGetMixPairAmounts(out float oxStoich, out float fuelStoich))
		{
			failReason = "mixPair";
			return false;
		}
		int oxIdx = reaction.mixRatioOxidizerInputIndex;
		int fuelIdx = reaction.mixRatioFuelInputIndex;
		for (int i = 0; i < reaction.inputs.Count; i++)
		{
			if (i == fuelIdx)
			{
				intoPerBatch.Add(fuelStoich);
			}
			else if (i == oxIdx)
			{
				intoPerBatch.Add(fuelStoich * r);
			}
			else
			{
				intoPerBatch.Add(reaction.inputs[i].stoichAmount);
			}
		}
		return true;
	}

	public static float ComputeBatchCount(
		PipeReactionDef? reaction,
		IList<Container>? inputs,
		IList<Container>? outputs,
		bool enabled,
		float mixRatio,
		out float efficiency,
		out string? failReason,
		List<float>? perInOut = null)
	{
		efficiency = 1f;
		failReason = null;
		if (!enabled)
		{
			failReason = "disabled";
			return 0f;
		}
		if (reaction == null)
		{
			failReason = "noReaction";
			return 0f;
		}
		if (inputs == null || outputs == null
			|| inputs.Count != reaction.inputs.Count
			|| outputs.Count != reaction.outputs.Count)
		{
			failReason = "slotMismatch";
			return 0f;
		}
		if (!PassesConditions(reaction, inputs, out failReason))
		{
			return 0f;
		}

		// 传入 perInOut 则复用（批处理路径传 MapComp 的 scratch），避免每次调用分配列表
		List<float> perIn = perInOut ?? new List<float>();
		if (!TryGetInputPerBatch(reaction, mixRatio, perIn, out failReason))
		{
			return 0f;
		}
		efficiency = ComputeEfficiency(reaction, mixRatio);

		float n = reaction.maxRate;
		if (n <= FlowSolver.AmountEpsilon)
		{
			failReason = "maxRate";
			return 0f;
		}

		for (int i = 0; i < reaction.inputs.Count; i++)
		{
			Container c = inputs[i];
			PipeReactionFluidAmount row = reaction.inputs[i];
			if (row?.fluid == null || c == null)
			{
				failReason = $"input[{i}]null";
				return 0f;
			}
			if (c.fluid != row.fluid)
			{
				failReason = $"input[{i}]fluid";
				return 0f;
			}
			float need = perIn[i];
			if (need <= FlowSolver.AmountEpsilon)
			{
				failReason = $"input[{i}]stoich";
				return 0f;
			}
			float byAmt = c.amount / need;
			if (byAmt < n)
			{
				n = byAmt;
			}
		}

		for (int i = 0; i < reaction.outputs.Count; i++)
		{
			PipeReactionFluidAmount row = reaction.outputs[i];
			Container c = outputs[i];
			if (row?.fluid == null || c == null)
			{
				failReason = $"output[{i}]null";
				return 0f;
			}
			if (c.amount > FlowSolver.AmountEpsilon && c.fluid != row.fluid)
			{
				failReason = $"output[{i}]fluid";
				return 0f;
			}
			float produced = row.stoichAmount * efficiency;
			if (produced <= FlowSolver.AmountEpsilon)
			{
				failReason = $"output[{i}]stoich";
				return 0f;
			}
			float byFree = c.FreeCapacity / produced;
			if (byFree < n)
			{
				n = byFree;
			}
		}

		if (n <= FlowSolver.AmountEpsilon)
		{
			failReason = "n≈0";
			return 0f;
		}
		return n;
	}

	/// <summary>兼容：用配方默认 mix，忽略效率细节调用方。</summary>
	public static float ComputeBatchCount(
		PipeReactionDef? reaction,
		IList<Container>? inputs,
		IList<Container>? outputs,
		bool enabled,
		out string? failReason)
	{
		float mix = reaction?.ResolvedBaseMixRatio ?? 1f;
		return ComputeBatchCount(reaction, inputs, outputs, enabled, mix, out _, out failReason);
	}

	public static void BuildAmountDeltas(
		PipeReactionDef reaction,
		IList<Container> inputs,
		IList<Container> outputs,
		float n,
		float mixRatio,
		float efficiency,
		List<(Container c, float delta)> into,
		List<float>? precomputedPerIn = null)
	{
		into.Clear();
		if (n <= FlowSolver.AmountEpsilon)
		{
			return;
		}
		// 批处理路径已由 ComputeBatchCount 算好 perIn，直接复用避免同批双算
		List<float> perIn;
		if (precomputedPerIn != null)
		{
			perIn = precomputedPerIn;
		}
		else
		{
			perIn = new List<float>();
			if (!TryGetInputPerBatch(reaction, mixRatio, perIn, out _))
			{
				return;
			}
		}
		for (int i = 0; i < reaction.inputs.Count; i++)
		{
			into.Add((inputs[i], -n * perIn[i]));
		}
		for (int i = 0; i < reaction.outputs.Count; i++)
		{
			into.Add((outputs[i], n * reaction.outputs[i].stoichAmount * efficiency));
		}
	}
}
