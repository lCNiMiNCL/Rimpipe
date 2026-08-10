using System.Collections.Generic;
using Verse;

namespace RimPipe;

public class FluidDef : Def
{
	/// <summary>
	/// 扣量后的效果（可多选）。None / 空列表 = 仅销毁量。
	/// 例：水→None；TestFuel→Filth+Temperature。
	/// </summary>
	public List<LeakEffect> leakEffects = new List<LeakEffect>();

	/// <summary>Filth 效果用官方 ThingDef（如 Filth_Fuel）。缺省则跳过 Filth。</summary>
	public ThingDef? leakFilthDef;

	/// <summary>每多少流体量至少产 1 层 Filth（默认 10）。</summary>
	public float leakFilthUnitsPerFilth = 10f;

	/// <summary>每单位销毁量推入的热量（正加热，负降温；默认 5）。</summary>
	public float leakHeatEnergyPerUnit = 5f;

	/// <summary>
	/// 粘度：流动阻力系数。批内按 rateCap = maxFlowRate / viscosity 缩流量，
	/// 越大流得越慢（水基准 1）。只作用于正常流动；泄漏扣量不吃粘度。
	/// </summary>
	public float viscosity = 1f;

	/// <summary>
	/// 比热：同样热量下温度变化率。ΔT = Q / (m·specificHeat)，越大升温越慢。
	/// 作用于导热 / 环境散热 / 反应热三处温变；混温（同流体）与之无关。
	/// </summary>
	public float specificHeat = 1f;

	public override IEnumerable<string> ConfigErrors()
	{
		foreach (string e in base.ConfigErrors())
		{
			yield return e;
		}
		if (viscosity <= 0f)
		{
			yield return "FluidDef viscosity 必须 > 0。";
		}
		if (specificHeat <= 0f)
		{
			yield return "FluidDef specificHeat 必须 > 0。";
		}
	}
}
