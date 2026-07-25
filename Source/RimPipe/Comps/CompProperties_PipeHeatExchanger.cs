using Verse;

namespace RimPipe;

public class CompProperties_PipeHeatExchanger : CompProperties
{
	public float baseMaxHeatRate = 50f;
	public int containerIndexA;
	public int containerIndexB = 1;

	public CompProperties_PipeHeatExchanger()
	{
		compClass = typeof(CompPipeHeatExchanger);
	}

	public override System.Collections.Generic.IEnumerable<string> ConfigErrors(ThingDef parentDef)
	{
		foreach (string error in base.ConfigErrors(parentDef))
		{
			yield return error;
		}
		if (containerIndexA == containerIndexB)
		{
			yield return "CompProperties_PipeHeatExchanger containerIndexA 与 containerIndexB 不能相同。";
		}
		if (baseMaxHeatRate < 0f)
		{
			yield return "CompProperties_PipeHeatExchanger baseMaxHeatRate 不能为负。";
		}
	}
}
