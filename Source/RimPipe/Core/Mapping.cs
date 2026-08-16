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
	private int _id = -1;
	private Container? _containerA;
	private Container? _containerB;
	private float _maxFlowRate = 10f;
	private MappingType _mappingType = MappingType.Flow;
	private FlowDriveMode _flowDrive = FlowDriveMode.Equalize;
	/// <summary>只在 Forced 模式下有用：true 表示固定从 A 抽到 B，false 则相反。</summary>
	private bool _forcedFromA = true;
	private List<Building> _attachedBuildings = new List<Building>();

	/// <summary>
	/// 阻力用：这条边经过的管道最短路径有几格（起终点都算进去）。
	/// 0 表示两设备直接相邻，或同建筑内部边——这两种都不按长度衰减流量。
	/// 拓扑重建时写好；不写进存档。
	/// </summary>
	private int _pathPipeCells;

	/// <summary>
	/// 开口泄漏。为 true 时 Accumulate 跳过这条边的正常流量；
	/// Commit 时两端各自按份额扣量销毁。
	/// </summary>
	private bool _leakOpen;

	/// <summary>本批 Accumulate 算流量时的临时字段，Commit 后会清。</summary>
	private Container? _batchSource;
	private Container? _batchTarget;
	private float _batchWant;
	private bool _batchIsLeak;

	/// <summary>
	/// 分网休眠用的连通网 ID。拓扑 Rebuild 时赋值，不写进存档。
	/// -1 表示还没分到任何一个网。
	/// </summary>
	private int _netId = -1;

	/// <summary>
	/// 上一完整 Busy Commit 时记下的流量，给 DevMode Overlay 画边线用。
	/// 不写进存档。
	/// </summary>
	private float _lastBatchWant;
	/// <summary>上一批实际流向：true = A→B，false = B→A。</summary>
	private bool _lastBatchFromA = true;

	/// <summary>
	/// 异流体警告有没有打过。每条 Mapping 只警告一次，避免刷屏。
	/// 不写进存档。
	/// </summary>
	private bool _loggedFluidMismatch;

	public int id { get => _id; internal set => _id = value; }
	public Container? containerA { get => _containerA; internal set => _containerA = value; }
	public Container? containerB { get => _containerB; internal set => _containerB = value; }
	public float maxFlowRate { get => _maxFlowRate; internal set => _maxFlowRate = value; }
	public MappingType mappingType { get => _mappingType; internal set => _mappingType = value; }
	public FlowDriveMode flowDrive { get => _flowDrive; internal set => _flowDrive = value; }
	internal bool forcedFromA { get => _forcedFromA; set => _forcedFromA = value; }
	internal List<Building> attachedBuildings { get => _attachedBuildings; set => _attachedBuildings = value; }
	internal int pathPipeCells { get => _pathPipeCells; set => _pathPipeCells = value; }
	internal bool leakOpen { get => _leakOpen; set => _leakOpen = value; }
	internal Container? batchSource { get => _batchSource; set => _batchSource = value; }
	internal Container? batchTarget { get => _batchTarget; set => _batchTarget = value; }
	internal float batchWant { get => _batchWant; set => _batchWant = value; }
	internal bool batchIsLeak { get => _batchIsLeak; set => _batchIsLeak = value; }
	public int netId { get => _netId; internal set => _netId = value; }
	internal float lastBatchWant { get => _lastBatchWant; set => _lastBatchWant = value; }
	internal bool lastBatchFromA { get => _lastBatchFromA; set => _lastBatchFromA = value; }
	internal bool loggedFluidMismatch { get => _loggedFluidMismatch; set => _loggedFluidMismatch = value; }

	public bool IsIncomplete => _containerA == null || _containerB == null;

	public void ExposeData()
	{
		// SaveMig：MapComp 从不把 mappings 列表写进存档；这里只是保留 IExposable 形态，别误接过来存。
		// 泄漏能否续档，靠 Comp 上的 breached；读档后拓扑重建时再刷 leakOpen。
		// 跨建筑端点和 attachedBuildings 都不持久化。
		Scribe_Values.Look(ref _id, "id", -1);
		Scribe_Values.Look(ref _maxFlowRate, "maxFlowRate", 10f);
		Scribe_Values.Look(ref _mappingType, "mappingType", MappingType.Flow);
		Scribe_Values.Look(ref _flowDrive, "flowDrive", FlowDriveMode.Equalize);
		Scribe_Values.Look(ref _forcedFromA, "forcedFromA", true);
		Scribe_Values.Look(ref _leakOpen, "leakOpen", false);
	}

	public void ClearBatch()
	{
		_batchSource = null;
		_batchTarget = null;
		_batchWant = 0f;
		_batchIsLeak = false;
	}

	public override string ToString()
	{
		string a = _containerA != null ? _containerA.ToString() : "null";
		string b = _containerB != null ? _containerB.ToString() : "null";
		string type = _mappingType == MappingType.Heat
			? " Heat"
			: _mappingType == MappingType.Chemical ? " Chem" : "";
		string drive = _flowDrive == FlowDriveMode.Forced
			? (_forcedFromA ? " ForcedA→B" : " ForcedB→A")
			: "";
		string path = _pathPipeCells > 0 ? $" path={_pathPipeCells}" : "";
		string leak = _leakOpen ? " leakOpen" : "";
		string net = _netId >= 0 ? $" net={_netId}" : "";
		string last = _lastBatchWant > 0.0001f
			? $" lastW={_lastBatchWant:0.##}{(_lastBatchFromA ? " A→B" : " B→A")}"
			: "";
		string rateLabel = _mappingType == MappingType.Heat ? "heatRate" : "rate";
		return $"Mapping#{_id}{type}[{a} ↔ {b}] {rateLabel}={_maxFlowRate}{path}{drive}{leak}{net}{last}";
	}
}
