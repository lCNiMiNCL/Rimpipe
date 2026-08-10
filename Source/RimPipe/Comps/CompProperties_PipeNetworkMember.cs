using System.Collections.Generic;
using Verse;

namespace RimPipe;

public class PipeContainerProp
{
	public float capacity = 100f;
	public string? fluidDefName;
	public float initialAmount;
}

public class PipePortProp
{
	/// <summary>相对建筑默认朝向（North）的本地边。</summary>
	public Rot4 localRot = Rot4.North;
	public int containerIndex;

	/// <summary>接入通道：0=A（默认），1=B。与管道格方向组对应。</summary>
	public int channel;
}

public class CompProperties_PipeNetworkMember : CompProperties
{
	public List<PipeContainerProp> containers = new List<PipeContainerProp>();
	public List<PipePortProp> ports = new List<PipePortProp>();
	public float defaultMaxFlowRate = 10f;

	/// <summary>Amb-A：保温系数。0=完全散热，1=绝热。实际散出去的比例 = 1 - insulation。</summary>
	public float insulation = 0.5f;

	/// <summary>Amb-A：每一批最多能和周围空气交换多少热量。</summary>
	public float maxAmbientHeatRate = 10f;

	public CompProperties_PipeNetworkMember()
	{
		compClass = typeof(CompPipeNetworkMember);
	}

	public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
	{
		foreach (string error in base.ConfigErrors(parentDef))
		{
			yield return error;
		}
		if (containers.NullOrEmpty())
		{
			yield return "CompProperties_PipeNetworkMember 至少需要一个 container。";
		}
		if (insulation < 0f || insulation > 1f)
		{
			yield return "CompProperties_PipeNetworkMember insulation 须在 [0,1]。";
		}
		if (maxAmbientHeatRate < 0f)
		{
			yield return "CompProperties_PipeNetworkMember maxAmbientHeatRate 不能为负。";
		}
		if (ports != null)
		{
			for (int i = 0; i < ports.Count; i++)
			{
				if (ports[i].containerIndex < 0 || ports[i].containerIndex >= containers.Count)
				{
					yield return $"port[{i}] containerIndex {ports[i].containerIndex} 越界。";
				}
				if (ports[i].channel != 0 && ports[i].channel != 1)
				{
					yield return $"port[{i}] channel 只能为 0（A）或 1（B），当前 {ports[i].channel}。";
				}
			}
		}
	}
}
