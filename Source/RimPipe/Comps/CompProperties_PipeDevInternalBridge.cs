using Verse;

namespace RimPipe;

/// <summary>Dev 自测桥的 Props：只给 ExtHook 验收用。</summary>
public class CompProperties_PipeDevInternalBridge : CompProperties
{
	public float baseMaxFlowRate = 10f;
	public int containerIndexA;
	public int containerIndexB = 1;

	public CompProperties_PipeDevInternalBridge()
	{
		compClass = typeof(CompPipeDevInternalBridge);
	}

	public override System.Collections.Generic.IEnumerable<string> ConfigErrors(ThingDef parentDef)
	{
		foreach (string error in base.ConfigErrors(parentDef))
		{
			yield return error;
		}
		if (containerIndexA == containerIndexB)
		{
			yield return "CompProperties_PipeDevInternalBridge containerIndexA 与 containerIndexB 不能相同。";
		}
		if (baseMaxFlowRate < 0f)
		{
			yield return "CompProperties_PipeDevInternalBridge baseMaxFlowRate 不能为负。";
		}
	}
}
