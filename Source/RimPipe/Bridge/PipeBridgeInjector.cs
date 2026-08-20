using System;
using System.Collections.Generic;
using Verse;

namespace RimPipe;

/// <summary>
/// Bridge-H 应用器：在所有 Def 加载完成后，扫描 <see cref="PipeBridgeInjectDef"/> 并向目标 ThingDef 注入 Comp。
/// 只注入 CompPipeBreachable 这类明确声明的 Comp；目标缺失 / 无 NetworkMember 时跳过并警告。
/// </summary>
[StaticConstructorOnStartup]
public static class PipeBridgeInjector
{
	static PipeBridgeInjector()
	{
		ApplyAll();
	}

	public static void ApplyAll()
	{
		foreach (PipeBridgeInjectDef injectDef in DefDatabase<PipeBridgeInjectDef>.AllDefs)
		{
			Apply(injectDef);
		}
	}

	private static void Apply(PipeBridgeInjectDef injectDef)
	{
		if (injectDef == null)
		{
			return;
		}

		if (injectDef.targetThingDef.NullOrEmpty())
		{
			Log.Warning($"[RimPipe] Bridge-H: 注入 Def {injectDef.defName} 缺少 targetThingDef，跳过。");
			return;
		}

		ThingDef? target = DefDatabase<ThingDef>.GetNamedSilentFail(injectDef.targetThingDef);
		if (target == null)
		{
			Log.Warning($"[RimPipe] Bridge-H: 目标 ThingDef '{injectDef.targetThingDef}' 不存在，跳过 {injectDef.defName}。");
			return;
		}

		if (!HasComp(target, typeof(CompPipeNetworkMember)))
		{
			Log.Warning($"[RimPipe] Bridge-H: 目标 {target.defName} 没有 CompPipeNetworkMember，跳过注入 Breachable。");
			return;
		}

		if (injectDef.injectComps == null || injectDef.injectComps.Count == 0)
		{
			Log.Warning($"[RimPipe] Bridge-H: 注入 Def {injectDef.defName} 的 injectComps 为空，跳过。");
			return;
		}

		for (int i = 0; i < injectDef.injectComps.Count; i++)
		{
			CompProperties? cp = injectDef.injectComps[i];
			if (cp?.compClass == null)
			{
				continue;
			}

			if (HasComp(target, cp.compClass))
			{
				Log.Message($"[RimPipe] Bridge-H: {target.defName} 已存在 {cp.compClass.Name}，跳过重复注入。");
				continue;
			}

			target.comps ??= new List<CompProperties>();
			target.comps.Add(cp);
			Log.Message($"[RimPipe] Bridge-H: 已向 {target.defName} 注入 {cp.compClass.Name}。");
		}
	}

	private static bool HasComp(ThingDef def, Type compClass)
	{
		if (def.comps == null)
		{
			return false;
		}

		for (int i = 0; i < def.comps.Count; i++)
		{
			CompProperties? cp = def.comps[i];
			if (cp?.compClass != null && compClass.IsAssignableFrom(cp.compClass))
			{
				return true;
			}
		}

		return false;
	}
}
