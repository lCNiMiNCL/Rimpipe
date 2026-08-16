namespace RimPipe;

/// <summary>
/// 纯 C# 导热公式核：不依赖 Verse / UnityEngine。
/// Qwant = min(maxHeatRate, |ΔT|/2 * min(两侧热容))，热容 = 质量 × 比热。
/// </summary>
public static class HeatSolverCore
{
	public const float TempEpsilon = 0.01f;

	public static float ComputeHeatWant(
		float amountA,
		float amountB,
		float tempA,
		float tempB,
		float specificHeatA,
		float specificHeatB,
		float maxHeatRate)
	{
		if (float.IsNaN(amountA) || float.IsNaN(amountB)
			|| float.IsNaN(tempA) || float.IsNaN(tempB)
			|| float.IsNaN(specificHeatA) || float.IsNaN(specificHeatB)
			|| float.IsNaN(maxHeatRate))
		{
			return 0f;
		}
		if (amountA <= FlowSolverCore.AmountEpsilon || amountB <= FlowSolverCore.AmountEpsilon)
		{
			return 0f;
		}

		float dT = tempA - tempB;
		if (dT > TempEpsilon)
		{
			float mHot = amountA;
			float mCold = amountB;
			float capHot = mHot * specificHeatA;
			float capCold = mCold * specificHeatB;
			return ComputeCappedHeatWant(dT, capHot, capCold, maxHeatRate);
		}
		if (dT < -TempEpsilon)
		{
			float mHot = amountB;
			float mCold = amountA;
			float capHot = mHot * specificHeatB;
			float capCold = mCold * specificHeatA;
			return ComputeCappedHeatWant(-dT, capHot, capCold, maxHeatRate);
		}
		return 0f;
	}

	private static float ComputeCappedHeatWant(float dT, float capHot, float capCold, float maxHeatRate)
	{
		float mEff = capHot < capCold ? capHot : capCold;
		float qWant = dT / 2f * mEff;
		if (qWant > maxHeatRate)
		{
			qWant = maxHeatRate;
		}
		if (qWant < FlowSolverCore.AmountEpsilon)
		{
			return 0f;
		}
		return qWant;
	}
}
