using Verse;

namespace RimPipe;

public class CompProperties_PipeValve : CompProperties
{
	public float baseMaxFlowRate = 10f;
	public int containerIndexA;
	public int containerIndexB = 1;

	public CompProperties_PipeValve()
	{
		compClass = typeof(CompPipeValve);
	}

	public override System.Collections.Generic.IEnumerable<string> ConfigErrors(ThingDef parentDef)
	{
		foreach (string error in base.ConfigErrors(parentDef))
		{
			yield return error;
		}
		if (containerIndexA == containerIndexB)
		{
			yield return "CompProperties_PipeValve containerIndexA 与 containerIndexB 不能相同。";
		}
		if (baseMaxFlowRate < 0f)
		{
			yield return "CompProperties_PipeValve baseMaxFlowRate 不能为负。";
		}
	}
}
