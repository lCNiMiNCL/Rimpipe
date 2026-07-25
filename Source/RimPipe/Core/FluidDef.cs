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
}
