using System;
using System.Collections.Generic;

namespace RimPipe;

/// <summary>
/// 纯 C# 化学配方数据镜像：只保留数值与流体名，不依赖 Verse 的 Def 对象。
/// 由 <see cref="ChemSolver"/> 从 <see cref="PipeReactionDef"/> 适配，或由单元测试直接构造。
/// </summary>
public sealed class ChemReactionSpec
{
	public string?[] InputFluids = Array.Empty<string?>();
	public float[] InputStoich = Array.Empty<float>();
	public string?[] OutputFluids = Array.Empty<string?>();
	public float[] OutputStoich = Array.Empty<float>();

	public float MaxRate = 1f;
	public float BaseMixRatio;
	public float RatioMin = 0.5f;
	public float RatioMax = 2f;
	public int MixRatioOxidizerInputIndex;
	public int MixRatioFuelInputIndex = 1;
	public int ConditionContainerIndex = -1;
	public float EfficiencyAtStoich = 1f;
	public float EfficiencyAtRatioEdge = 0.5f;
	public float MinTemperature = -273f;
	public float MinPressure;
	public float HeatPerBatch;

	public int InputCount => InputFluids.Length;
	public int OutputCount => OutputFluids.Length;

	public float ResolvedBaseMixRatio => BaseMixRatio > 0f ? BaseMixRatio : StoichMixRatio;

	public float StoichMixRatio
	{
		get
		{
			if (!TryGetMixPairAmounts(out float ox, out float fuel) || fuel <= 0f)
			{
				return 1f;
			}
			return ox / fuel;
		}
	}

	public bool TryGetMixPairAmounts(out float oxidizerStoich, out float fuelStoich)
	{
		oxidizerStoich = 0f;
		fuelStoich = 0f;
		if (MixRatioOxidizerInputIndex < 0 || MixRatioOxidizerInputIndex >= InputCount
			|| MixRatioFuelInputIndex < 0 || MixRatioFuelInputIndex >= InputCount)
		{
			return false;
		}
		float ox = InputStoich[MixRatioOxidizerInputIndex];
		float fuel = InputStoich[MixRatioFuelInputIndex];
		oxidizerStoich = ox;
		fuelStoich = fuel;
		return fuel > 0f && ox > 0f;
	}
}

/// <summary>纯 C# 容器状态快照，供 <see cref="ChemSolverCore"/> 使用。</summary>
public readonly struct ChemContainerState
{
	public readonly string? Fluid;
	public readonly float Amount;
	public readonly float Capacity;
	public readonly float Temperature;

	public ChemContainerState(string? fluid, float amount, float capacity, float temperature)
	{
		Fluid = fluid;
		Amount = amount;
		Capacity = capacity;
		Temperature = temperature;
	}

	public float FreeCapacity => Capacity - Amount;
}

/// <summary>
/// 纯 C# 化学公式核：不依赖 Verse / UnityEngine。
/// 与 <see cref="ChemSolver"/> 保持同一套公式：量转化、L1 偏比效率、T/P 条件、反应热按调用方处理。
/// </summary>
public static class ChemSolverCore
{
	public static int ResolveConditionIndex(ChemReactionSpec reaction, int inputCount)
	{
		if (inputCount <= 0)
		{
			return -1;
		}
		int idx = reaction != null ? reaction.ConditionContainerIndex : -1;
		if (idx < 0)
		{
			idx = 0;
		}
		return idx;
	}

	public static bool PassesConditions(
		ChemReactionSpec reaction,
		IReadOnlyList<ChemContainerState> inputs,
		out string? failReason)
	{
		failReason = null;
		ChemContainerState? cond = ResolveConditionContainer(reaction, inputs);
		if (cond == null)
		{
			failReason = "condSlot";
			return false;
		}
		if (cond.Value.Temperature < reaction.MinTemperature)
		{
			failReason = "cold";
			return false;
		}
		float p = FlowSolverCore.PressureFromAmount(cond.Value.Amount, cond.Value.Capacity);
		if (p < reaction.MinPressure)
		{
			failReason = "lowP";
			return false;
		}
		return true;
	}

	public static ChemContainerState? ResolveConditionContainer(
		ChemReactionSpec reaction,
		IReadOnlyList<ChemContainerState> inputs)
	{
		if (inputs == null || inputs.Count == 0)
		{
			return null;
		}
		int idx = ResolveConditionIndex(reaction, inputs.Count);
		if (idx >= inputs.Count)
		{
			return null;
		}
		return inputs[idx];
	}

	public static float ClampMixRatio(ChemReactionSpec reaction, float mixRatio)
	{
		if (mixRatio <= 0f)
		{
			mixRatio = reaction.ResolvedBaseMixRatio;
		}
		if (mixRatio < reaction.RatioMin)
		{
			return reaction.RatioMin;
		}
		if (mixRatio > reaction.RatioMax)
		{
			return reaction.RatioMax;
		}
		return mixRatio;
	}

	public static float ComputeEfficiency(ChemReactionSpec reaction, float mixRatio)
	{
		float r = ClampMixRatio(reaction, mixRatio);
		float stoich = reaction.StoichMixRatio;
		float edge = reaction.EfficiencyAtRatioEdge;
		float peak = reaction.EfficiencyAtStoich;
		if (stoich <= FlowSolverCore.AmountEpsilon)
		{
			return peak;
		}
		if (Math.Abs(r - stoich) <= 1e-4f)
		{
			return peak;
		}
		if (r < stoich)
		{
			float span = stoich - reaction.RatioMin;
			if (span <= FlowSolverCore.AmountEpsilon)
			{
				return edge;
			}
			float t = (r - reaction.RatioMin) / span;
			return Lerp(edge, peak, Clamp01(t));
		}
		else
		{
			float span = reaction.RatioMax - stoich;
			if (span <= FlowSolverCore.AmountEpsilon)
			{
				return edge;
			}
			float t = (r - stoich) / span;
			return Lerp(peak, edge, Clamp01(t));
		}
	}

	public static bool TryGetInputPerBatch(
		ChemReactionSpec reaction,
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
		int oxIdx = reaction.MixRatioOxidizerInputIndex;
		int fuelIdx = reaction.MixRatioFuelInputIndex;
		for (int i = 0; i < reaction.InputCount; i++)
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
				intoPerBatch.Add(reaction.InputStoich[i]);
			}
		}
		return true;
	}

	public static float ComputeBatchCount(
		ChemReactionSpec? reaction,
		IReadOnlyList<ChemContainerState>? inputs,
		IReadOnlyList<ChemContainerState>? outputs,
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
			|| inputs.Count != reaction.InputCount
			|| outputs.Count != reaction.OutputCount)
		{
			failReason = "slotMismatch";
			return 0f;
		}
		if (!PassesConditions(reaction, inputs, out failReason))
		{
			return 0f;
		}

		List<float> perIn = perInOut ?? new List<float>();
		if (!TryGetInputPerBatch(reaction, mixRatio, perIn, out failReason))
		{
			return 0f;
		}
		efficiency = ComputeEfficiency(reaction, mixRatio);

		float n = reaction.MaxRate;
		if (n <= FlowSolverCore.AmountEpsilon)
		{
			failReason = "maxRate";
			return 0f;
		}

		for (int i = 0; i < reaction.InputCount; i++)
		{
			ChemContainerState c = inputs[i];
			string? fluid = reaction.InputFluids[i];
			if (fluid == null || c.Fluid == null)
			{
				failReason = $"input[{i}]null";
				return 0f;
			}
			if (c.Fluid != fluid)
			{
				failReason = $"input[{i}]fluid";
				return 0f;
			}
			float need = perIn[i];
			if (need <= FlowSolverCore.AmountEpsilon)
			{
				failReason = $"input[{i}]stoich";
				return 0f;
			}
			float byAmt = c.Amount / need;
			if (byAmt < n)
			{
				n = byAmt;
			}
		}

		for (int i = 0; i < reaction.OutputCount; i++)
		{
			ChemContainerState c = outputs[i];
			string? fluid = reaction.OutputFluids[i];
			if (fluid == null || c.Fluid == null)
			{
				failReason = $"output[{i}]null";
				return 0f;
			}
			if (c.Amount > FlowSolverCore.AmountEpsilon && c.Fluid != fluid)
			{
				failReason = $"output[{i}]fluid";
				return 0f;
			}
			float produced = reaction.OutputStoich[i] * efficiency;
			if (produced <= FlowSolverCore.AmountEpsilon)
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

		if (n <= FlowSolverCore.AmountEpsilon)
		{
			failReason = "n≈0";
			return 0f;
		}
		return n;
	}

	public static void BuildAmountDeltas(
		ChemReactionSpec reaction,
		IReadOnlyList<ChemContainerState> inputs,
		IReadOnlyList<ChemContainerState> outputs,
		float n,
		float mixRatio,
		float efficiency,
		List<(int
 Index, float Delta)> into,
		List<float>? precomputedPerIn = null)
	{
		into.Clear();
		if (n <= FlowSolverCore.AmountEpsilon)
		{
			return;
		}
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
		for (int i = 0; i < reaction.InputCount; i++)
		{
			into.Add((i, -n * perIn[i]));
		}
		for (int i = 0; i < reaction.OutputCount; i++)
		{
			into.Add((reaction.InputCount + i, n * reaction.OutputStoich[i] * efficiency));
		}
	}

	private static float Lerp(float a, float b, float t)
	{
		return a + (b - a) * t;
	}

	private static float Clamp01(float value)
	{
		if (value < 0f)
		{
			return 0f;
		}
		if (value > 1f)
		{
			return 1f;
		}
		return value;
	}
}
