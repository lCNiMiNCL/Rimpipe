using Verse;

namespace RimPipe;

/// <summary>
/// 建筑表面的对接口。Def 里写本地朝向（相对建筑默认朝北）和连到哪个 Container；
/// 运行时再转到世界方向，去看外面那一格有没有邻居或管道。
/// Port 自己不算流量，流量在 Container 之间的 Mapping 上算。
/// </summary>
public class Port : IExposable
{
	private Rot4 _localRot = Rot4.North;
	private int _containerIndex;

	/// <summary>
	/// 接入通道：0=A（默认），1=B。设备通过端口声明接入哪套连接组的管网；
	/// 只与管道格「同组」的方向出口连通（管道格该方向出口属另一组则端口悬空）。
	/// </summary>
	private int _channel;

	private CompPipeNetworkMember? _owner;

	public Rot4 localRot { get => _localRot; internal set => _localRot = value; }
	public int containerIndex { get => _containerIndex; internal set => _containerIndex = value; }
	public int channel { get => _channel; internal set => _channel = value; }
	public CompPipeNetworkMember? owner { get => _owner; internal set => _owner = value; }

	public Container? Container
	{
		get
		{
			if (_owner == null || _containerIndex < 0 || _containerIndex >= _owner.Containers.Count)
			{
				return null;
			}
			return _owner.Containers[_containerIndex];
		}
	}

	public Rot4 WorldRot
	{
		get
		{
			if (_owner == null)
			{
				return _localRot;
			}
			return PipeRotUtility.ToWorld(_localRot, _owner.parent.Rotation);
		}
	}

	public IntVec3 OuterCell
	{
		get
		{
			if (_owner == null)
			{
				return IntVec3.Invalid;
			}
			return PipeRotUtility.OuterCell(_owner.parent.Position, WorldRot);
		}
	}

	public void ExposeData()
	{
		Scribe_Values.Look(ref _localRot, "localRot", Rot4.North);
		Scribe_Values.Look(ref _containerIndex, "containerIndex", 0);
		Scribe_Values.Look(ref _channel, "channel", 0);
	}
}
