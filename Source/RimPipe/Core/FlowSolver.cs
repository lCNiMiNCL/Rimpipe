using Verse;

namespace RimPipe;

/// <summary>
/// 流量怎么算 want（本批想搬多少）：
///   Equalize：先看两边填充比压力 P=amount/capacity，
///     drive = |ΔP|/2 * min(两边容量)，再和 rateCap、高侧存量、低侧空位取 min。
///   Forced（泵）：方向由 forcedFromA 写死；want = min(rateCap, 源侧存量, 目标空位)。
///   rateCap = maxFlowRate / fluid.viscosity：粘度缩放流量上限（泵同样吃粘度）；
///   泄漏扣量不走这里（破口=大气孔，不吃粘度）。
/// 边不完整或 leakOpen → want=0（泄漏放到 Commit 里扣）；两边流体种类不同 → want=0 并打日志。
/// 批内欠松弛可以用「虚拟量」试算；压力必须跟着虚拟量当场重算。
/// 数值公式已下沉到 <see cref="FlowSolverCore"/>，本类只保留 Verse 类型适配与日志。
/// </summary>
public static class FlowSolver
{
	public const float AmountEpsilon = FlowSolverCore.AmountEpsilon;

	public static float ComputeWant(Mapping mapping, out Container? source, out Container? target, out bool isLeak)
	{
		if (mapping.IsIncomplete || mapping.containerA == null || mapping.containerB == null)
		{
			source = null;
			target = null;
			isLeak = false;
			return 0f;
		}
		return ComputeWant(mapping, mapping.containerA.amount, mapping.containerB.amount, out source, out target, out isLeak);
	}

	public static float ComputeWant(Mapping mapping, float amountA, float amountB, out Container? source, out Container? target, out bool isLeak)
	{
		source = null;
		target = null;
		isLeak = false;

		if (mapping.IsIncomplete || mapping.leakOpen)
		{
			return 0f;
		}

		Container a = mapping.containerA!;
		Container b = mapping.containerB!;

		if (a.fluid != b.fluid)
		{
			if (!mapping.loggedFluidMismatch)
			{
				mapping.loggedFluidMismatch = true;
				Log.Warning($"[RimPipe] Mapping#{mapping.id} 异流体：{a.fluid?.defName} vs {b.fluid?.defName}，want=0（本边仅提示一次）。");
			}
			return 0f;
		}

		if (a.fluid == null)
		{
			return 0f;
		}

		float viscosity = a.fluid.viscosity;
		if (viscosity <= 0f)
		{
			// Def 校验已拦；兜底防除零
			return 0f;
		}

		if (mapping.flowDrive == FlowDriveMode.Forced)
		{
			return ComputeForcedWant(mapping, a, b, amountA, amountB, viscosity, out source, out target);
		}

		return ComputeEqualizeWant(mapping, a, b, amountA, amountB, viscosity, out source, out target);
	}

	private static float ComputeForcedWant(
		Mapping mapping,
		Container a,
		Container b,
		float amountA,
		float amountB,
		float viscosity,
		out Container? source,
		out Container? target)
	{
		source = null;
		target = null;

		Container src = mapping.forcedFromA ? a : b;
		Container dst = mapping.forcedFromA ? b : a;
		float amountSrc = mapping.forcedFromA ? amountA : amountB;
		float amountDst = mapping.forcedFromA ? amountB : amountA;

		float want = FlowSolverCore.ComputeForcedWant(
			amountSrc,
			amountDst,
			dst.capacity,
			mapping.maxFlowRate,
			viscosity);
		if (want <= FlowSolverCore.AmountEpsilon)
		{
			return 0f;
		}

		source = src;
		target = dst;
		return want;
	}

	private static float ComputeEqualizeWant(
		Mapping mapping,
		Container a,
		Container b,
		float amountA,
		float amountB,
		float viscosity,
		out Container? source,
		out Container? target)
	{
		source = null;
		target = null;

		float pA = FlowSolverCore.PressureFromAmount(amountA, a.capacity);
		float pB = FlowSolverCore.PressureFromAmount(amountB, b.capacity);

		Container hi;
		Container lo;
		float amountHi;
		float amountLo;
		float capHi;
		float capLo;
		if (pA > pB + AmountEpsilon)
		{
			hi = a;
			lo = b;
			amountHi = amountA;
			amountLo = amountB;
			capHi = a.capacity;
			capLo = b.capacity;
		}
		else if (pB > pA + AmountEpsilon)
		{
			hi = b;
			lo = a;
			amountHi = amountB;
			amountLo = amountA;
			capHi = b.capacity;
			capLo = a.capacity;
		}
		else
		{
			return 0f;
		}

		float want = FlowSolverCore.ComputeEqualizeWant(
			amountHi,
			amountLo,
			capHi,
			capLo,
			mapping.maxFlowRate,
			viscosity);
		if (want <= FlowSolverCore.AmountEpsilon)
		{
			return 0f;
		}

		source = hi;
		target = lo;
		return want;
	}
}
