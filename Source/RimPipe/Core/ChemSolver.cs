using System.Collections.Generic;

namespace RimPipe;

/// <summary>
/// 化学反应算量：按配方从入腔扣、给出腔加。
/// 支持可调混合比 mixRatio 和效率 η；还能看入腔温度/压力够不够门槛，不够就本批不做。
/// 数值公式已下沉到 <see cref="ChemSolverCore"/>，本类只做 Verse 类型适配。
/// </summary>
public static class ChemSolver
{
	private static ChemContainerState ToState(Container? c)
	{
		return new ChemContainerState(
			c?.fluid?.defName,
			c?.amount ?? 0f,
			c?.capacity ?? 0f,
			c?.temperature ?? 0f);
	}

	private static List<ChemContainerState> ToStates(IList<Container>? list)
	{
		var states = new List<ChemContainerState>(list?.Count ?? 0);
		if (list == null)
		{
			return states;
		}
		for (int i = 0; i < list.Count; i++)
		{
			states.Add(ToState(list[i]));
		}
		return states;
	}

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
		return ChemSolverCore.PassesConditions(reaction.GetChemSpec(), ToStates(inputs), out failReason);
	}

	public static float ClampMixRatio(PipeReactionDef reaction, float mixRatio)
	{
		return ChemSolverCore.ClampMixRatio(reaction.GetChemSpec(), mixRatio);
	}

	/// <summary>
	/// 偏比效率：stoich→efficiencyAtStoich；ratioMin/Max→efficiencyAtRatioEdge；中间线性。
	/// </summary>
	public static float ComputeEfficiency(PipeReactionDef reaction, float mixRatio)
	{
		return ChemSolverCore.ComputeEfficiency(reaction.GetChemSpec(), mixRatio);
	}

	/// <summary>每 1 批单位各输入消耗量（已按 mixRatio）；失败返回 false。</summary>
	public static bool TryGetInputPerBatch(
		PipeReactionDef reaction,
		float mixRatio,
		List<float> intoPerBatch,
		out string? failReason)
	{
		return ChemSolverCore.TryGetInputPerBatch(reaction.GetChemSpec(), mixRatio, intoPerBatch, out failReason);
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
		if (reaction == null)
		{
			efficiency = 1f;
			failReason = "noReaction";
			return 0f;
		}
		return ChemSolverCore.ComputeBatchCount(
			reaction.GetChemSpec(),
			ToStates(inputs),
			ToStates(outputs),
			enabled,
			mixRatio,
			out efficiency,
			out failReason,
			perInOut);
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
		if (n <= FlowSolverCore.AmountEpsilon)
		{
			return;
		}

		var pureDeltas = new List<(int Index, float Delta)>();
		ChemSolverCore.BuildAmountDeltas(
			reaction.GetChemSpec(),
			ToStates(inputs),
			ToStates(outputs),
			n,
			mixRatio,
			efficiency,
			pureDeltas,
			precomputedPerIn);

		for (int i = 0; i < pureDeltas.Count; i++)
		{
			int index = pureDeltas[i].Index;
			float delta = pureDeltas[i].Delta;
			if (index < inputs.Count)
			{
				into.Add((inputs[index], delta));
			}
			else
			{
				int outIndex = index - inputs.Count;
				into.Add((outputs[outIndex], delta));
			}
		}
	}
}
