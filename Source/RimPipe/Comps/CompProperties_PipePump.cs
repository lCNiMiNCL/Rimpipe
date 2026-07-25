using Verse;

namespace RimPipe;

public class CompProperties_PipePump : CompProperties
{
	public float baseMaxFlowRate = 10f;
	public int containerIndexA;
	public int containerIndexB = 1;
	/// <summary>Forced 时 true = A→B（吸入→排出）。</summary>
	public bool forcedFromA = true;

	public CompProperties_PipePump()
	{
		compClass = typeof(CompPipePump);
	}

	public override System.Collections.Generic.IEnumerable<string> ConfigErrors(ThingDef parentDef)
	{
		foreach (string error in base.ConfigErrors(parentDef))
		{
			yield return error;
		}
		if (containerIndexA == containerIndexB)
		{
			yield return "CompProperties_PipePump containerIndexA 与 containerIndexB 不能相同。";
		}
		if (baseMaxFlowRate < 0f)
		{
			yield return "CompProperties_PipePump baseMaxFlowRate 不能为负。";
		}
	}
}
