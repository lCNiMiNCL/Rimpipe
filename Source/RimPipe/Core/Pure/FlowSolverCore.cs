namespace RimPipe;

/// <summary>
/// 纯 C# 流量公式核：不依赖 Verse / UnityEngine。
/// 只做数值计算，供 <see cref="FlowSolver"/> 与单元测试复用。
/// </summary>
public static class FlowSolverCore
{
	public const float AmountEpsilon = 1e-4f;

	public static float PressureFromAmount(float amount, float capacity)
	{
		if (capacity <= AmountEpsilon)
		{
			return 0f;
		}
		float p = amount / capacity;
		if (p < 0f)
		{
			return 0f;
		}
		if (p > 1f)
		{
			return 1f;
		}
		return p;
	}

	public static float ComputeRateCap(float maxFlowRate, float viscosity)
	{
		if (viscosity <= 0f)
		{
			return 0f;
		}
		return maxFlowRate / viscosity;
	}

	/// <summary>泵/强制流动：want = min(rateCap, 源存量, 目标空位)。</summary>
	public static float ComputeForcedWant(
		float amountSrc,
		float amountDst,
		float capacityDst,
		float maxFlowRate,
		float viscosity)
	{
		if (amountSrc < AmountEpsilon)
		{
			return 0f;
		}
		float freeDst = capacityDst - amountDst;
		if (freeDst < AmountEpsilon)
		{
			return 0f;
		}

		float want = ComputeRateCap(maxFlowRate, viscosity);
		if (want > amountSrc)
		{
			want = amountSrc;
		}
		if (want > freeDst)
		{
			want = freeDst;
		}
		if (want < AmountEpsilon)
		{
			return 0f;
		}
		return want;
	}

	/// <summary>压力均分：drive = |ΔP|/2 * minCap，再与 rateCap / 高侧存量 / 低侧空位取 min。</summary>
	public static float ComputeEqualizeWant(
		float amountHi,
		float amountLo,
		float capacityHi,
		float capacityLo,
		float maxFlowRate,
		float viscosity)
	{
		float pHi = PressureFromAmount(amountHi, capacityHi);
		float pLo = PressureFromAmount(amountLo, capacityLo);

		if (pHi <= pLo + AmountEpsilon)
		{
			return 0f;
		}

		float freeLo = capacityLo - amountLo;
		if (freeLo < AmountEpsilon)
		{
			return 0f;
		}

		float minCap = capacityHi < capacityLo ? capacityHi : capacityLo;
		float drive = (pHi - pLo) / 2f * minCap;

		float want = drive;
		float rateCap = ComputeRateCap(maxFlowRate, viscosity);
		if (want > rateCap)
		{
			want = rateCap;
		}
		if (want > amountHi)
		{
			want = amountHi;
		}
		if (want > freeLo)
		{
			want = freeLo;
		}
		if (want < AmountEpsilon)
		{
			return 0f;
		}
		return want;
	}
}
