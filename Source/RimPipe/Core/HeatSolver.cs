namespace RimPipe;

/// <summary>
/// 导热：想换的热量 Qwant = min(maxHeatRate, |ΔT|/2 * min(两边热容))；
/// 热容 = 质量 × 流体比热（fluid.specificHeat，null → 1）。
/// 热的一侧降温、凉的一侧升温。空罐（几乎没液）不参与。两边流体种类不同也可以换热。
/// </summary>
public static class HeatSolver
{
	public const float TempEpsilon = 0.01f;

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

		if (amountA <= FlowSolver.AmountEpsilon || amountB <= FlowSolver.AmountEpsilon)
		{
			return 0f;
		}

		float dT = tempA - tempB;
		if (dT > TempEpsilon)
		{
			hot = a;
			cold = b;
		}
		else if (dT < -TempEpsilon)
		{
			hot = b;
			cold = a;
			dT = -dT;
		}
		else
		{
			return 0f;
		}

		float mHot = hot == a ? amountA : amountB;
		float mCold = cold == a ? amountA : amountB;
		float capHot = mHot * SpecificHeatOf(hot);
		float capCold = mCold * SpecificHeatOf(cold);
		float mEff = capHot < capCold ? capHot : capCold;
		float qWant = dT / 2f * mEff;
		if (qWant > mapping.maxFlowRate)
		{
			qWant = mapping.maxFlowRate;
		}
		if (qWant < FlowSolver.AmountEpsilon)
		{
			return 0f;
		}
		return qWant;
	}
}
