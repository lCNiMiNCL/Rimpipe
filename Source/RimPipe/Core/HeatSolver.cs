namespace RimPipe;

/// <summary>
/// 导热：想换的热量 Qwant = min(maxHeatRate, |ΔT|/2 * min(两边热容))；
/// 热容 = 质量 × 流体比热（fluid.specificHeat，null → 1）。
/// 热的一侧降温、凉的一侧升温。空罐（几乎没液）不参与。两边流体种类不同也可以换热。
/// 数值公式已下沉到 <see cref="HeatSolverCore"/>，本类只保留 Mapping/Container 适配。
/// </summary>
public static class HeatSolver
{
	public const float TempEpsilon = HeatSolverCore.TempEpsilon;

	/// <summary>取容器流体比热；无流体或空引用按 1。</summary>
	public static float SpecificHeatOf(Container? c)
	{
		return c?.fluid != null ? c.fluid.specificHeat : 1f;
	}

	public static float ComputeHeatWant(
		Mapping mapping,
		float amountA,
		float amountB,
		float tempA,
		float tempB,
		out Container? hot,
		out Container? cold)
	{
		hot = null;
		cold = null;

		if (mapping.IsIncomplete || mapping.leakOpen || mapping.mappingType != MappingType.Heat)
		{
			return 0f;
		}

		Container a = mapping.containerA!;
		Container b = mapping.containerB!;

		float want = HeatSolverCore.ComputeHeatWant(
			amountA,
			amountB,
			tempA,
			tempB,
			SpecificHeatOf(a),
			SpecificHeatOf(b),
			mapping.maxFlowRate);
		if (want <= FlowSolverCore.AmountEpsilon)
		{
			return 0f;
		}

		if (tempA > tempB)
		{
			hot = a;
			cold = b;
		}
		else
		{
			hot = b;
			cold = a;
		}
		return want;
	}
}
