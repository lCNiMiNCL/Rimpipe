using Verse;

namespace RimPipe;

/// <summary>
/// 建筑表面的对接口。Def 里写本地朝向（相对建筑默认朝北）和连到哪个 Container；
/// 运行时再转到世界方向，去看外面那一格有没有邻居或管道。
/// Port 自己不算流量，流量在 Container 之间的 Mapping 上算。
/// </summary>
public class Port : IExposable
{
	public Rot4 localRot = Rot4.North;
	public int containerIndex;
	public CompPipeNetworkMember? owner;

	public Container? Container
	{
		get
		{
			if (owner == null || containerIndex < 0 || containerIndex >= owner.Containers.Count)
			{
				return null;
			}
			return owner.Containers[containerIndex];
		}
	}

	public Rot4 WorldRot
	{
		get
		{
			if (owner == null)
			{
				return localRot;
			}
			return PipeRotUtility.ToWorld(localRot, owner.parent.Rotation);
		}
	}

	public IntVec3 OuterCell
	{
		get
		{
			if (owner == null)
			{
				return IntVec3.Invalid;
			}
			return PipeRotUtility.OuterCell(owner.parent.Position, WorldRot);
		}
	}

	public void ExposeData()
	{
		Scribe_Values.Look(ref localRot, "localRot", Rot4.North);
		Scribe_Values.Look(ref containerIndex, "containerIndex", 0);
	}
}
