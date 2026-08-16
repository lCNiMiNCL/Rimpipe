using Verse;

namespace RimPipe;

/// <summary>
/// 装一种流体的桶。量（amount）只能由 MapComponent 在 Commit 阶段改，别在别处直接写。
/// </summary>
public class Container : IExposable
{
	public int id = -1;
	public FluidDef? fluid;
	public float amount;
	public float capacity = 100f;

	/// <summary>
	/// 填充比压力 = amount / capacity。只是从量算出来的派生值，自己不会单独守恒。
	/// Equalize 流量按两边压力差来推。
	/// </summary>
	public float pressure;
	public float temperature = 21f;

	public CompPipeNetworkMember? owner;
	public int containerIndex;

	/// <summary>
	/// 分网休眠用的连通网 ID。拓扑 Rebuild 时赋值，不写进存档。-1 表示还没分网。
	/// </summary>
	public int netId = -1;

	public float FreeCapacity => capacity - amount;

	/// <summary>按当前（或批内虚拟）量重算填充比压力。批内 Jacobi 迭代必须走这里，别用手写 pressure。</summary>
	public static float PressureFromAmount(float amount, float capacity)
	{
		return FlowSolverCore.PressureFromAmount(amount, capacity);
	}

	public void SyncPressureFromAmount()
	{
		pressure = PressureFromAmount(amount, capacity);
	}

	public void ExposeData()
	{
		Scribe_Values.Look(ref id, "id", -1);
		Scribe_Defs.Look(ref fluid, "fluid");
		Scribe_Values.Look(ref amount, "amount", 0f);
		Scribe_Values.Look(ref capacity, "capacity", 100f);
		Scribe_Values.Look(ref pressure, "pressure", 0f);
		Scribe_Values.Look(ref temperature, "temperature", 21f);
		Scribe_Values.Look(ref containerIndex, "containerIndex", 0);
	}

	/// <summary>真正改 amount。只有 MapComponent_PipeNetwork.CommitDeltas 可以调用。</summary>
	internal void CommitAmount(float newAmount)
	{
		if (float.IsNaN(newAmount))
		{
			Log.Warning($"[RimPipe] Container {id} 提交 NaN 量，钳制为 0。");
			newAmount = 0f;
		}
		if (newAmount < -0.0001f)
		{
			Log.Warning($"[RimPipe] Container {id} 提交负量 {newAmount}，钳制为 0。");
			newAmount = 0f;
		}
		if (newAmount > capacity + 0.0001f)
		{
			Log.Warning($"[RimPipe] Container {id} 提交 {newAmount} 超过容量 {capacity}，钳制。");
			newAmount = capacity;
		}
		amount = newAmount;
		SyncPressureFromAmount();
	}

	/// <summary>真正改温度。只有 MapComponent 的温度 Commit 路径可以调用。</summary>
	internal void CommitTemperature(float newTemperature)
	{
		temperature = newTemperature;
	}

	public override string ToString()
	{
		string fluidName = fluid != null ? fluid.defName : "null";
		string net = netId >= 0 ? $" net={netId}" : "";
		return $"Container#{id}({fluidName}:{amount:0.##}/{capacity:0.##} P={pressure:0.###} T={temperature:0.#}{net})";
	}
}
