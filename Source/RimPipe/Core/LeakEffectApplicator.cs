using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe;

/// <summary>
/// 泄漏扣完量之后，往环境泼效果（脏污、升温等）。走官方 FilthMaker / GenTemperature。
/// 效果失败也不会把已经扣掉的量加回去。
/// </summary>
public static class LeakEffectApplicator
{
	private static readonly HashSet<string> WarnedMissingFilth = new HashSet<string>();

	public static void Apply(Container? container, float leakedAmount, IntVec3 cell, Map? map)
	{
		if (container?.fluid == null || map == null || leakedAmount <= FlowSolver.AmountEpsilon)
		{
			return;
		}
		if (!cell.IsValid || !cell.InBounds(map))
		{
			return;
		}

		FluidDef fluid = container.fluid;
		List<LeakEffect>? effects = fluid.leakEffects;
		if (effects == null || effects.Count == 0)
		{
			return;
		}

		bool didFilth = false;
		bool didHeat = false;
		for (int i = 0; i < effects.Count; i++)
		{
			LeakEffect e = effects[i];
			if (e == LeakEffect.None)
			{
				continue;
			}
			if (e == LeakEffect.Filth && !didFilth)
			{
				didFilth = true;
				ApplyFilth(fluid, leakedAmount, cell, map);
			}
			else if (e == LeakEffect.Temperature && !didHeat)
			{
				didHeat = true;
				ApplyTemperature(fluid, leakedAmount, cell, map);
			}
		}
	}

	private static void ApplyFilth(FluidDef fluid, float leakedAmount, IntVec3 cell, Map map)
	{
		ThingDef? filthDef = fluid.leakFilthDef;
		if (filthDef == null)
		{
			string key = fluid.defName ?? "?";
			if (WarnedMissingFilth.Add(key))
			{
				Log.Warning($"[RimPipe] FluidDef {key} 含 leakEffects=Filth 但未设 leakFilthDef，跳过 Filth。");
			}
			return;
		}

		float units = fluid.leakFilthUnitsPerFilth;
		if (units < 0.01f)
		{
			units = 0.01f;
		}
		int count = Mathf.Max(1, Mathf.FloorToInt(leakedAmount / units));
		FilthMaker.TryMakeFilth(cell, map, filthDef, count);
	}

	private static void ApplyTemperature(FluidDef fluid, float leakedAmount, IntVec3 cell, Map map)
	{
		float energy = leakedAmount * fluid.leakHeatEnergyPerUnit;
		if (Mathf.Abs(energy) <= FlowSolver.AmountEpsilon)
		{
			return;
		}
		GenTemperature.PushHeat(cell, map, energy);
	}

	public static bool WantsFilth(FluidDef? fluid)
	{
		return HasEffect(fluid, LeakEffect.Filth);
	}

	public static bool WantsTemperature(FluidDef? fluid)
	{
		return HasEffect(fluid, LeakEffect.Temperature);
	}

	private static bool HasEffect(FluidDef? fluid, LeakEffect target)
	{
		if (fluid?.leakEffects == null)
		{
			return false;
		}
		for (int i = 0; i < fluid.leakEffects.Count; i++)
		{
			if (fluid.leakEffects[i] == target)
			{
				return true;
			}
		}
		return false;
	}
}
