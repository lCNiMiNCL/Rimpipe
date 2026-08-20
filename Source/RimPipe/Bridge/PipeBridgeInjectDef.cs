using System.Collections.Generic;
using Verse;

namespace RimPipe;

/// <summary>
/// Bridge-H 注入配置：在加载阶段向目标 ThingDef 追加 CompProperties。
/// 典型用途：给“已有 CompPipeNetworkMember 但没有 CompPipeBreachable”的第三方建筑注入破损能力。
/// </summary>
public class PipeBridgeInjectDef : Def
{
	public string targetThingDef = "";

	public List<CompProperties> injectComps = new List<CompProperties>();

	public override IEnumerable<string> ConfigErrors()
	{
		foreach (string e in base.ConfigErrors())
		{
			yield return e;
		}

		if (targetThingDef.NullOrEmpty())
		{
			yield return "PipeBridgeInjectDef.targetThingDef 不能为空。";
		}

		if (injectComps == null || injectComps.Count == 0)
		{
			yield return "PipeBridgeInjectDef.injectComps 至少需要一项 CompProperties。";
		}
		else
		{
			for (int i = 0; i < injectComps.Count; i++)
			{
				for (int j = i + 1; j < injectComps.Count; j++)
				{
					if (injectComps[i]?.compClass != null
						&& injectComps[i].compClass == injectComps[j]?.compClass)
					{
						yield return
							$"PipeBridgeInjectDef.injectComps 中 {injectComps[i].compClass.Name} 重复。";
					}
				}
			}
		}
	}
}
