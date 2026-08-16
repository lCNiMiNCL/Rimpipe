using Verse;

namespace RimPipe;

/// <summary>
/// 装一种流体的桶。量（amount）只能由 MapComponent 在 Commit 阶段改，别在别处直接写。
/// </summary>
public class Container : IExposable
{
	private int _id = -1;
	private FluidDef? _fluid;
	private float _amount;
	private float _capacity = 100f;

	/// <summary>
	/// 填充比压力 = amount / capacity。只是从量算出来的派生值，自己不会单独守恒。
	/// Equalize 流量按两边压力差来推。
	/// </summary>
	private float _pressure;
	private float _temperature = 21f;

	private CompPipeNetworkMember? _owner;
	private int _containerIndex;

	/// <summary>
	/// 分网休眠用的连通网 ID。拓扑 Rebuild 时赋值，不写进存档。-1 表示还没分网。
	/// </summary>
	private int _netId = -1;

	public int id { get => _id; internal set => _id = value; }
	public FluidDef? fluid { get => _fluid; internal set => _fluid = value; }
	public float amount { get => _amount; internal set => _amount = value; }
	public float capacity { get => _capacity; internal set => _capacity = value; }
	public float pressure { get => _pressure; internal set => _pressure = value; }
	public float temperature { get => _temperature; internal set => _temperature = value; }
	public CompPipeNetworkMember? owner { get => _owner; internal set => _owner = value; }
	public int containerIndex { get => _containerIndex; internal set => _containerIndex = value; }
	public int netId { get => _netId; internal set => _netId = value; }

	public float FreeCapacity => _capacity - _amount;

	/// <summary>按当前（或批内虚拟）量重算填充比压力。批内 Jacobi 迭代必须走这里，别用手写 pressure。</summary>
	public static float PressureFromAmount(float amount, float capacity)
	{
		return FlowSolverCore.PressureFromAmount(amount, capacity);
	}

	internal void SyncPressureFromAmount()
	{
		_pressure = PressureFromAmount(_amount, _capacity);
	}

	public void ExposeData()
	{
		Scribe_Values.Look(ref _id, "id", -1);
		Scribe_Defs.Look(ref _fluid, "fluid");
		Scribe_Values.Look(ref _amount, "amount", 0f);
		Scribe_Values.Look(ref _capacity, "capacity", 100f);
		Scribe_Values.Look(ref _pressure, "pressure", 0f);
		Scribe_Values.Look(ref _temperature, "temperature", 21f);
		Scribe_Values.Look(ref _containerIndex, "containerIndex", 0);
	}

	/// <summary>真正改 amount。只有 MapComponent_PipeNetwork.CommitDeltas 可以调用。</summary>
	internal void CommitAmount(float newAmount)
	{
		if (float.IsNaN(newAmount))
		{
			Log.Warning($"[RimPipe] Container {_id} 提交 NaN 量，钳制为 0。");
			newAmount = 0f;
		}
		if (newAmount < -0.0001f)
		{
			Log.Warning($"[RimPipe] Container {_id} 提交负量 {newAmount}，钳制为 0。");
			newAmount = 0f;
		}
		if (newAmount > _capacity + 0.0001f)
		{
			Log.Warning($"[RimPipe] Container {_id} 提交 {newAmount} 超过容量 {_capacity}，钳制。");
			newAmount = _capacity;
		}
		_amount = newAmount;
		SyncPressureFromAmount();
	}

	/// <summary>真正改温度。只有 MapComponent 的温度 Commit 路径可以调用。</summary>
	internal void CommitTemperature(float newTemperature)
	{
		_temperature = newTemperature;
	}

	public override string ToString()
	{
		string fluidName = _fluid != null ? _fluid.defName : "null";
		string net = _netId >= 0 ? $" net={_netId}" : "";
		return $"Container#{_id}({fluidName}:{_amount:0.##}/{_capacity:0.##} P={_pressure:0.###} T={_temperature:0.#}{net})";
	}
}
