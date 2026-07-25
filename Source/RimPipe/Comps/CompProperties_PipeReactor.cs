using System.Collections.Generic;
using Verse;

namespace RimPipe;

public class CompProperties_PipeReactor : CompProperties
{
	public string? reactionDefName;

	/// <summary>入腔 Container 下标；空则按 0..inputs.Count-1。</summary>
	public List<int> inputContainerIndices = new List<int>();

	/// <summary>出腔 Container 下标；空则紧接输入之后。</summary>
	public List<int> outputContainerIndices = new List<int>();

	public CompProperties_PipeReactor()
	{
		compClass = typeof(CompPipeReactor);
	}

	public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
	{
		foreach (string error in base.ConfigErrors(parentDef))
		{
			yield return error;
		}
		if (reactionDefName.NullOrEmpty())
		{
			yield return "CompProperties_PipeReactor reactionDefName 为空";
		}
	}
}
