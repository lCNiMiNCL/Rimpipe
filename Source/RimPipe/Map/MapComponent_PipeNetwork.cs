using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using RimPipe.Debug;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe;

/// <summary>
/// 整张地图的管网总管。管件注册、管道拓扑、按批算流，都从这里过。
/// 每 20 tick 一批：0–18 做 Accumulate（算 want、写待提交增量），第 19 tick 做 Commit（真正改量）。
/// 分网休眠（Busy / AmbientOnly / FullyQuiet）：按连通网 netId 各自判断；
/// 没有流量需求就跳过 Accumulate，但量已经静止时仍可只跑泄漏和环境散热。
/// DevMode 下可开流量/压力 Overlay。存档带 rimPipeSchemaVersion；跨建筑 Mapping 不存，读档后重建。
/// 拓扑放在 Tick 开头处理（无 Harmony），思路对齐电力网「先理线再用电」。
/// </summary>
public partial class MapComponent_PipeNetwork : MapComponent
{
	public const int BatchSize = 20;

	/// <summary>当前存档格式版本。只有破坏性变更才往上加，并单独做迁移。</summary>
	public const int CurrentSchemaVersion = 1;

	private readonly PipeIdProvider idProvider = new PipeIdProvider();
	private readonly List<CompPipeNetworkMember> members = new List<CompPipeNetworkMember>();
	private readonly List<CompPipeCell> pipeCells = new List<CompPipeCell>();
	private readonly Dictionary<IntVec3, CompPipeCell> cellToPipe = new Dictionary<IntVec3, CompPipeCell>();
	private readonly List<Mapping> mappings = new List<Mapping>();
	private readonly Dictionary<IntVec3, List<Mapping>> cellToMapping = new Dictionary<IntVec3, List<Mapping>>();
	private readonly Dictionary<Container, float> pendingDeltas = new Dictionary<Container, float>();
	private readonly Dictionary<Container, float> pendingTempDeltas = new Dictionary<Container, float>();
	private readonly List<ChemReactorBinding> chemReactors = new List<ChemReactorBinding>();
	private readonly List<(Container c, float delta)> chemDeltaScratch = new List<(Container, float)>();
	private readonly List<float> chemPerInScratch = new List<float>();
	private readonly List<(Container c, float leak, IntVec3 cell)> leakWants = new List<(Container, float, IntVec3)>();
	private readonly Dictionary<Container, float> leakSum = new Dictionary<Container, float>();
	private readonly List<DelayedAction> delayedActions = new List<DelayedAction>();
	private bool needsFullRebuild = true;

	// —— 批级复用缓冲：AccumulateFlow / AccumulateHeat 每批都要用虚拟量和求和，
	// 这些容器作为字段复用，避免 Busy 批每批新建 List/Dict 造成 GC 压力（§7.10.7 记录的 Acc 内分配）。
	private readonly Dictionary<Container, float> flowVirtual = new Dictionary<Container, float>();
	private readonly List<Mapping> busyFlowMappings = new List<Mapping>();
	private readonly List<(Mapping map, Container src, Container tgt, float want)> flowWants = new List<(Mapping, Container, Container, float)>();
	private readonly Dictionary<Container, float> flowOutSum = new Dictionary<Container, float>();
	private readonly Dictionary<Container, float> flowInSum = new Dictionary<Container, float>();
	private readonly Dictionary<Container, float> flowStepDelta = new Dictionary<Container, float>();

	private readonly Dictionary<Container, float> heatVirtual = new Dictionary<Container, float>();
	private readonly List<Mapping> busyHeatMappings = new List<Mapping>();
	private readonly List<(Mapping map, Container hot, Container cold, float q)> heatWants = new List<(Mapping, Container, Container, float)>();
	private readonly Dictionary<Container, float> heatStepDelta = new Dictionary<Container, float>();

	// —— 批级缓存：Accumulate / 泄漏 / 散热 / 混温 只迭代「按休眠态分类」的缓存列表，
	// 不再每批全图扫描 mappings / members。批内休眠态恒定（§7.10.8 已文档化），
	// 因此仅在拓扑 / 休眠定态 / 唤醒 / 注册注销时把 batchCachesDirty 置 true，惰性重建。
	/// <summary>Busy 网 ∪ AmbientOnly 网的 Flow 边（供泄漏 / 不完整残端扣量）。</summary>
	private readonly List<Mapping> lightFlowMappings = new List<Mapping>();
	/// <summary>含任一 light（Busy∪AmbientOnly）网容器的构件（供泄漏与环境散热）。</summary>
	private readonly List<CompPipeNetworkMember> lightCommitMembers = new List<CompPipeNetworkMember>();
	/// <summary>批级缓存是否失效，置 true 后下次批入口惰性重建。</summary>
	private bool batchCachesDirty = true;

	/// <summary>这张地图存档里记下的 schema 版本号。</summary>
	private int rimPipeSchemaVersion = CurrentSchemaVersion;

	/// <summary>每个连通网各自的休眠状态；下标就是 netId。不写进存档。</summary>
	private PipeNetworkSleepState[] sleepStates = System.Array.Empty<PipeNetworkSleepState>();
	private int netCount;

	/// <summary>休眠重评估的单遍聚合标志（复用，避免每批分配）。不写进存档。</summary>
	private bool[] reevalBusy = System.Array.Empty<bool>();
	private bool[] reevalAmb = System.Array.Empty<bool>();

	/// <summary>DevMode 下是否画流量/压力 Overlay。不写进存档。</summary>
	internal bool showFlowPressureOverlay;

	/// <summary>
	/// 套件批量刷场景时为 true：跳过「已注册/注销构件」「拓扑重建」「泄漏销毁」刷屏。
	/// 断言失败与套件汇总仍正常打日志。
	/// </summary>
	internal static bool QuietDebugLogs;

	/// <summary>最近一次 <see cref="RebuildAllMappings"/> 耗时（毫秒）。不写进存档。</summary>
	public float LastTopologyRebuildMs { get; private set; }

	/// <summary>最近一次批处理各阶段的耗时（毫秒），供 Benchmark 对照 §7.10.7。不写进存档。</summary>
	public float LastAccumulateMs { get; private set; }
	public float LastCommitMs { get; private set; }
	public float LastReevaluateMs { get; private set; }

	public int SchemaVersion => rimPipeSchemaVersion;

	private enum DelayedActionType
	{
		MemberChanged,
		PipeChanged,
		FullRebuild
	}

	private struct DelayedAction
	{
		public DelayedActionType type;
		public IntVec3 cell;
		/// <summary>MemberChanged 时携带的构件引用（注销后仍有效，用于删除规则匹配容器）。</summary>
		public CompPipeNetworkMember? member;
		/// <summary>
		/// MemberChanged 时携带的「影响格」：构件 OccupiedRect 全部格 ∪ 各端口 OuterCell。
		/// 必须在 Enqueue 时刻快照（PostDeSpawn 后拿不到 Ports/Containers 的现场布局）。
		/// PipeChanged 时为 null。
		/// </summary>
		public List<IntVec3>? influenceCells;

		public DelayedAction(DelayedActionType type, IntVec3 cell, CompPipeNetworkMember? member = null)
		{
			this.type = type;
			this.cell = cell;
			this.member = member;
			influenceCells = null;
			if (type == DelayedActionType.MemberChanged && member != null && member.parent != null)
			{
				influenceCells = new List<IntVec3>();
				foreach (IntVec3 foot in member.parent.OccupiedRect())
				{
					influenceCells.Add(foot);
				}
				for (int p = 0; p < member.Ports.Count; p++)
				{
					influenceCells.Add(member.Ports[p].OuterCell);
				}
			}
		}
	}

	public IReadOnlyList<Mapping> Mappings => mappings;
	public IReadOnlyList<CompPipeNetworkMember> Members => members;
	public IReadOnlyList<CompPipeCell> PipeCells => pipeCells;
	public IReadOnlyList<ChemReactorBinding> ChemReactors => chemReactors;
	public PipeIdProvider IdProvider => idProvider;
	public int NetCount => netCount;

	public MapComponent_PipeNetwork(Map map) : base(map)
	{
	}

	public override void FinalizeInit()
	{
		base.FinalizeInit();
		SyncAllContainerPressures();
		needsFullRebuild = true;
		ProcessDelayedActions();
	}

	/// <summary>Pr-A：读档或容错时按 amount/capacity 重算 pressure。</summary>
	public void SyncAllContainerPressures()
	{
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.Containers == null)
			{
				continue;
			}
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				mem.Containers[c]?.SyncPressureFromAmount();
			}
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
		idProvider.ExposeData();
		Scribe_Values.Look(ref rimPipeSchemaVersion, "rimPipeSchemaVersion", CurrentSchemaVersion);
		if (Scribe.mode == LoadSaveMode.LoadingVars)
		{
			if (rimPipeSchemaVersion < 1)
			{
				rimPipeSchemaVersion = CurrentSchemaVersion;
			}
			if (rimPipeSchemaVersion > CurrentSchemaVersion)
			{
				Log.Warning(
					$"[RimPipe] 存档 schema={rimPipeSchemaVersion} 高于当前 {CurrentSchemaVersion}，尽力加载（SaveMig-A）。");
			}
		}
		if (Scribe.mode == LoadSaveMode.Saving)
		{
			rimPipeSchemaVersion = CurrentSchemaVersion;
		}
		if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			mappings.Clear();
			cellToMapping.Clear();
			pendingDeltas.Clear();
			pendingTempDeltas.Clear();
			chemReactors.Clear();
			delayedActions.Clear();
			needsFullRebuild = true;
		}
	}

	public override void MapComponentTick()
	{
		// 1) 先延迟拓扑  2) 仅 Busy 网 0–18 Acc  3) 19 按各网态 Commit / 轻 Commit / 仅评估
		ProcessDelayedActions();
		int ticks = Find.TickManager.TicksGame;
		// Bridge：不少建筑 tickerType=Never，修好满血后清破损要靠 MapComp 每批轮询一下
		// （官方修理工只改 HP，不会主动通知我们）。
		if (ticks % 250 == 0)
		{
			TryClearRepairedBreaches();
		}
		int phase = ticks % BatchSize;
		bool anyBusy = HasAnyNetState(PipeNetworkSleepState.Busy);
		bool anyAmb = HasAnyNetState(PipeNetworkSleepState.AmbientOnly);
		if (phase >= 0 && phase <= 18)
		{
			if (anyBusy)
			{
				Stopwatch sw = Stopwatch.StartNew();
				AccumulateFlow();
				AccumulateHeat();
				sw.Stop();
				LastAccumulateMs = (float)sw.Elapsed.TotalMilliseconds;
			}
			else
			{
				LastAccumulateMs = 0f;
			}
		}
		else if (phase == 19)
		{
			if (anyBusy)
			{
				Stopwatch sw = Stopwatch.StartNew();
				CommitDeltas();
				sw.Stop();
				LastCommitMs = (float)sw.Elapsed.TotalMilliseconds;
			}
			else if (anyAmb)
			{
				Stopwatch sw = Stopwatch.StartNew();
				CommitAmbientAndLeaksOnly();
				sw.Stop();
				LastCommitMs = (float)sw.Elapsed.TotalMilliseconds;
			}
			else
			{
				LastCommitMs = 0f;
			}
			Stopwatch swR = Stopwatch.StartNew();
			ReevaluateAllNetSleepStates();
			swR.Stop();
			LastReevaluateMs = (float)swR.Elapsed.TotalMilliseconds;
		}
	}

	/// <summary>Bridge：满血且当前没 Breakdown 故障时，清掉 breached（看 Props.clearBreachOnRepaired）。</summary>
	public void TryClearRepairedBreaches()
	{
		for (int i = 0; i < pipeCells.Count; i++)
		{
			CompPipeCell cell = pipeCells[i];
			if (cell == null || !cell.Breached || !cell.ClearBreachOnRepaired)
			{
				continue;
			}
			if (PipeBreachBridge.CanClearBreachOnRepaired(cell.parent))
			{
				cell.Breached = false;
			}
		}
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			CompPipeBreachable? br = mem?.parent?.TryGetComp<CompPipeBreachable>();
			if (br == null || !br.Breached || !br.ClearBreachOnRepaired)
			{
				continue;
			}
			if (PipeBreachBridge.CanClearBreachOnRepaired(br.parent))
			{
				br.Breached = false;
			}
		}
	}

	public override void MapComponentUpdate()
	{
		base.MapComponentUpdate();
		if (!showFlowPressureOverlay || !Prefs.DevMode)
		{
			return;
		}
		DrawFlowPressureOverlay();
	}

	public override void MapComponentOnGUI()
	{
		base.MapComponentOnGUI();
		if (!showFlowPressureOverlay || !Prefs.DevMode)
		{
			return;
		}
		DrawFlowPressureOverlayLabels();
	}
}
