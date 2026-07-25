using System.Collections.Generic;
using Verse;

namespace RimPipe;

/// <summary>
/// 两个 Container 之间的一条传输边（流体或热量都走 Mapping）。
/// 默认 Equalize：每批按高低差决定方向，从高侧往低侧流。
/// 泵会把 flowDrive 设成 Forced，并按 forcedFromA 固定抽送方向。
/// 任一端是 null 就算 incomplete（边不完整，不算正常流量）。
/// leakOpen 表示开口泄漏：Accumulate 不再走这条边的正常流量，
/// Commit 时按 rate 份额把两端的量直接销毁（产品破损或 Debug 标记）。
/// </summary>
public class Mapping : IExposable
{
	public int id = -1;
	public Container? containerA;
	public Container? containerB;
	public float maxFlowRate = 10f;
	public MappingType mappingType = MappingType.Flow;
	public FlowDriveMode flowDrive = FlowDriveMode.Equalize;
	/// <summary>只在 Forced 模式下有用：true 表示固定从 A 抽到 B，false 则相反。</summary>
	public bool forcedFromA = true;
	public List<Building> attachedBuildings = new List<Building>();

	/// <summary>
	/// 阻力用：这条边经过的管道最短路径有几格（起终点都算进去）。
	/// 0 表示两设备直接相邻，或同建筑内部边——这两种都不按长度衰减流量。
	/// 拓扑重建时写好；不写进存档。
	/// </summary>
	public int pathPipeCells;

	/// <summary>
	/// 开口泄漏。为 true 时 Accumulate 跳过这条边的正常流量；
	/// Commit 时两端各自按份额扣量销毁。
	/// </summary>
	public bool leakOpen;

	/// <summary>本批 Accumulate 算流量时的临时字段，Commit 后会清。</summary>
	public Container? batchSource;
	public Container? batchTarget;
	public float batchWant;
	public bool batchIsLeak;

	/// <summary>
	/// 分网休眠用的连通网 ID。拓扑 Rebuild 时赋值，不写进存档。
	/// -1 表示还没分到任何一个网。
	/// </summary>
	public int netId = -1;

	/// <summary>
	/// 上一完整 Busy Commit 时记下的流量，给 DevMode Overlay 画边线用。
	/// 不写进存档。
	/// </summary>
	public float lastBatchWant;
	/// <summary>上一批实际流向：true = A→B，false = B→A。</summary>
	public bool lastBatchFromA = true;

	/// <summary>
	/// 异流体警告有没有打过。每条 Mapping 只警告一次，避免刷屏。
	/// 不写进存档。
	/// </summary>
	public bool loggedFluidMismatch;

	public bool IsIncomplete => containerA == null || containerB == null;

	public void ExposeData()
	{
		// SaveMig：MapComp 从不把 mappings 列表写进存档；这里只是保留 IExposable 形态，别误接过来存。
		// 泄漏能否续档，靠 Comp 上的 breached；读档后拓扑重建时再刷 leakOpen。
		// 跨建筑端点和 attachedBuildings 都不持久化。
		Scribe_Values.Look(ref id, "id", -1);
		Scribe_Values.Look(ref maxFlowRate, "maxFlowRate", 10f);
		Scribe_Values.Look(ref mappingType, "mappingType", MappingType.Flow);
		Scribe_Values.Look(ref flowDrive, "flowDrive", FlowDriveMode.Equalize);
		Scribe_Values.Look(ref forcedFromA, "forcedFromA", true);
		Scribe_Values.Look(ref leakOpen, "leakOpen", false);
	}

	public void ClearBatch()
	{
		batchSource = null;
		batchTarget = null;
		batchWant = 0f;
		batchIsLeak = false;
	}

	public override string ToString()
	{
		string a = containerA != null ? containerA.ToString() : "null";
		string b = containerB != null ? containerB.ToString() : "null";
		string type = mappingType == MappingType.Heat
			? " Heat"
			: mappingType == MappingType.Chemical ? " Chem" : "";
		string drive = flowDrive == FlowDriveMode.Forced
			? (forcedFromA ? " ForcedA→B" : " ForcedB→A")
			: "";
		string path = pathPipeCells > 0 ? $" path={pathPipeCells}" : "";
		string leak = leakOpen ? " leakOpen" : "";
		string net = netId >= 0 ? $" net={netId}" : "";
		string last = lastBatchWant > 0.0001f
			? $" lastW={lastBatchWant:0.##}{(lastBatchFromA ? " A→B" : " B→A")}"
			: "";
		string rateLabel = mappingType == MappingType.Heat ? "heatRate" : "rate";
		return $"Mapping#{id}{type}[{a} ↔ {b}] {rateLabel}={maxFlowRate}{path}{drive}{leak}{net}{last}";
	}
}
