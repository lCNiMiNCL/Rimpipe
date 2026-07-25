using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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
public class MapComponent_PipeNetwork : MapComponent
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
	private readonly List<DelayedAction> delayedActions = new List<DelayedAction>();
	private bool needsFullRebuild = true;

	/// <summary>这张地图存档里记下的 schema 版本号。</summary>
	private int rimPipeSchemaVersion = CurrentSchemaVersion;

	/// <summary>每个连通网各自的休眠状态；下标就是 netId。不写进存档。</summary>
	private PipeNetworkSleepState[] sleepStates = System.Array.Empty<PipeNetworkSleepState>();
	private int netCount;

	/// <summary>DevMode 下是否画流量/压力 Overlay。不写进存档。</summary>
	public bool showFlowPressureOverlay;

	/// <summary>
	/// 套件批量刷场景时为 true：跳过「已注册/注销构件」「拓扑重建」「泄漏销毁」刷屏。
	/// 断言失败与套件汇总仍正常打日志。
	/// </summary>
	public static bool QuietDebugLogs;

	/// <summary>最近一次 <see cref="RebuildAllMappings"/> 耗时（毫秒）。不写进存档。</summary>
	public float LastTopologyRebuildMs { get; private set; }

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

		public DelayedAction(DelayedActionType type, IntVec3 cell)
		{
			this.type = type;
			this.cell = cell;
		}
	}

	public IReadOnlyList<Mapping> Mappings => mappings;
	public IReadOnlyList<CompPipeNetworkMember> Members => members;
	public IReadOnlyList<CompPipeCell> PipeCells => pipeCells;
	public IReadOnlyList<ChemReactorBinding> ChemReactors => chemReactors;
	public PipeIdProvider IdProvider => idProvider;
	public int NetCount => netCount;

	/// <summary>
	/// 从各网状态汇总出的「整图观感」：只要有一个网 Busy 整图就算 Busy；
	/// 否则有 AmbientOnly 就算 AmbientOnly；全都安静才是 FullyQuiet。
	/// </summary>
	public PipeNetworkSleepState SleepState
	{
		get
		{
			bool anyAmb = false;
			for (int i = 0; i < sleepStates.Length; i++)
			{
				if (sleepStates[i] == PipeNetworkSleepState.Busy)
				{
					return PipeNetworkSleepState.Busy;
				}
				if (sleepStates[i] == PipeNetworkSleepState.AmbientOnly)
				{
					anyAmb = true;
				}
			}
			return anyAmb ? PipeNetworkSleepState.AmbientOnly : PipeNetworkSleepState.FullyQuiet;
		}
	}

	public PipeNetworkSleepState GetNetSleepState(int netId)
	{
		if (netId < 0 || netId >= sleepStates.Length)
		{
			return PipeNetworkSleepState.Busy;
		}
		return sleepStates[netId];
	}

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
				AccumulateFlow();
				AccumulateHeat();
				AccumulateChem();
			}
		}
		else if (phase == 19)
		{
			if (anyBusy)
			{
				CommitDeltas();
			}
			else if (anyAmb)
			{
				CommitAmbientAndLeaksOnly();
			}
			ReevaluateAllNetSleepStates();
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

	/// <summary>
	/// 外部有人改了量/温/开关等 → 把对应网强制打成 Busy。
	/// 如果说不清是哪个网，就唤醒整张图上的所有网。
	/// </summary>
	public void WakeNetwork(string? reason = null)
	{
		EnsureSleepStatesCapacity();
		for (int i = 0; i < sleepStates.Length; i++)
		{
			sleepStates[i] = PipeNetworkSleepState.Busy;
		}
	}

	public void WakeNet(int netId, string? reason = null)
	{
		if (netId < 0)
		{
			WakeNetwork(reason);
			return;
		}
		EnsureSleepStatesCapacity();
		if (netId < sleepStates.Length)
		{
			sleepStates[netId] = PipeNetworkSleepState.Busy;
		}
	}

	public void WakeContainer(Container? c, string? reason = null)
	{
		if (c == null || c.netId < 0)
		{
			WakeNetwork(reason);
			return;
		}
		WakeNet(c.netId, reason);
	}

	public void WakeMember(CompPipeNetworkMember? mem, string? reason = null)
	{
		if (mem?.Containers == null || mem.Containers.Count == 0)
		{
			WakeNetwork(reason);
			return;
		}
		for (int i = 0; i < mem.Containers.Count; i++)
		{
			WakeContainer(mem.Containers[i], reason);
		}
	}

	public void RegisterMember(CompPipeNetworkMember comp, bool respawningAfterLoad)
	{
		if (!members.Contains(comp))
		{
			members.Add(comp);
		}
		AssignContainerIds(comp);
		if (!respawningAfterLoad)
		{
			Enqueue(DelayedActionType.MemberChanged, comp.parent.Position);
		}
		else
		{
			needsFullRebuild = true;
		}
		if (!QuietDebugLogs)
		{
			Log.Message($"[RimPipe] 已注册构件 {comp.parent.LabelCap} id={comp.parent.thingIDNumber} 容器={comp.Containers.Count} 端口={comp.Ports.Count}");
		}
	}

	public void DeregisterMember(CompPipeNetworkMember comp, DestroyMode mode = DestroyMode.Vanish)
	{
		if (ShouldDumpOnDestroy(mode))
		{
			TryDumpMemberOnce(comp);
		}
		UnregisterChemReactorsTouching(comp);
		members.Remove(comp);
		Enqueue(DelayedActionType.MemberChanged, comp.parent.Position);
		if (!QuietDebugLogs)
		{
			Log.Message($"[RimPipe] 已注销构件 {comp.parent.LabelCap}");
		}
	}

	/// <summary>
	/// P2：注册化学绑定（运行时，不写入存档）。inputs/outputs 数量须与配方一致。
	/// P3 起由 CompPipeReactor 调用；同 owner 重复注册会先撤旧。
	/// </summary>
	public bool TryRegisterChemReactor(
		PipeReactionDef? reaction,
		IList<Container>? inputs,
		IList<Container>? outputs,
		object? owner = null,
		float mixRatio = -1f,
		bool enabled = true)
	{
		if (reaction == null || inputs == null || outputs == null)
		{
			return false;
		}
		if (inputs.Count != reaction.inputs.Count || outputs.Count != reaction.outputs.Count)
		{
			Log.Warning($"[RimPipe] Chem 注册失败：槽位数与配方不符 ({reaction.defName})。");
			return false;
		}
		for (int i = 0; i < inputs.Count; i++)
		{
			if (inputs[i] == null)
			{
				return false;
			}
		}
		for (int i = 0; i < outputs.Count; i++)
		{
			if (outputs[i] == null)
			{
				return false;
			}
		}
		if (owner != null)
		{
			UnregisterChemReactor(owner);
		}
		ChemReactorBinding b = new ChemReactorBinding
		{
			reaction = reaction,
			enabled = enabled,
			mixRatio = mixRatio > 0f ? mixRatio : reaction.ResolvedBaseMixRatio,
			owner = owner
		};
		b.inputs.AddRange(inputs);
		b.outputs.AddRange(outputs);
		chemReactors.Add(b);
		for (int i = 0; i < b.inputs.Count; i++)
		{
			WakeContainer(b.inputs[i], "chemRegister");
		}
		for (int i = 0; i < b.outputs.Count; i++)
		{
			WakeContainer(b.outputs[i], "chemRegister");
		}
		return true;
	}

	/// <summary>按 owner 更新已注册绑定的开关/mix（找不到则 false）。</summary>
	public bool TryUpdateChemReactor(object? owner, bool enabled, float mixRatio = -1f)
	{
		if (owner == null)
		{
			return false;
		}
		for (int i = 0; i < chemReactors.Count; i++)
		{
			ChemReactorBinding b = chemReactors[i];
			if (!ReferenceEquals(b.owner, owner))
			{
				continue;
			}
			b.enabled = enabled;
			if (mixRatio > 0f)
			{
				b.mixRatio = mixRatio;
			}
			for (int c = 0; c < b.inputs.Count; c++)
			{
				WakeContainer(b.inputs[c], "chemUpdate");
			}
			return true;
		}
		return false;
	}

	public ChemReactorBinding? FindChemReactor(object? owner)
	{
		if (owner == null)
		{
			return null;
		}
		for (int i = 0; i < chemReactors.Count; i++)
		{
			if (ReferenceEquals(chemReactors[i].owner, owner))
			{
				return chemReactors[i];
			}
		}
		return null;
	}

	public void UnregisterChemReactor(object? owner)
	{
		if (owner == null)
		{
			return;
		}
		for (int i = chemReactors.Count - 1; i >= 0; i--)
		{
			if (ReferenceEquals(chemReactors[i].owner, owner))
			{
				chemReactors.RemoveAt(i);
			}
		}
	}

	public void ClearChemReactors()
	{
		chemReactors.Clear();
	}

	private void UnregisterChemReactorsTouching(CompPipeNetworkMember? mem)
	{
		if (mem?.Containers == null)
		{
			return;
		}
		for (int i = chemReactors.Count - 1; i >= 0; i--)
		{
			ChemReactorBinding b = chemReactors[i];
			if (BindingTouchesMember(b, mem))
			{
				chemReactors.RemoveAt(i);
			}
		}
	}

	private static bool BindingTouchesMember(ChemReactorBinding b, CompPipeNetworkMember mem)
	{
		for (int i = 0; i < b.inputs.Count; i++)
		{
			if (b.inputs[i]?.owner == mem)
			{
				return true;
			}
		}
		for (int i = 0; i < b.outputs.Count; i++)
		{
			if (b.outputs[i]?.owner == mem)
			{
				return true;
			}
		}
		return false;
	}

	public void RegisterPipeCell(CompPipeCell comp)
	{
		if (!pipeCells.Contains(comp))
		{
			pipeCells.Add(comp);
		}
		cellToPipe[comp.parent.Position] = comp;
		Enqueue(DelayedActionType.PipeChanged, comp.parent.Position);
	}

	public void DeregisterPipeCell(CompPipeCell comp, DestroyMode mode = DestroyMode.Vanish)
	{
		if (ShouldDumpOnDestroy(mode))
		{
			TryDumpPipeOnce(comp);
		}
		pipeCells.Remove(comp);
		if (cellToPipe.TryGetValue(comp.parent.Position, out CompPipeCell existing) && existing == comp)
		{
			cellToPipe.Remove(comp.parent.Position);
		}
		Enqueue(DelayedActionType.PipeChanged, comp.parent.Position);
	}

	/// <summary>
	/// 管道或储罐的 breached 刚切换过：只刷新相关 Mapping.leakOpen（不必整网重建），并唤醒受影响的网。
	/// </summary>
	public void NotifyBreachChanged()
	{
		ApplyBreachLeakFlags();
		HashSet<int> woke = new HashSet<int>();
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.leakOpen && m.netId >= 0 && woke.Add(m.netId))
			{
				WakeNet(m.netId, "breach");
			}
		}
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			CompPipeBreachable? br = mem?.parent?.TryGetComp<CompPipeBreachable>();
			if (br == null || !br.Breached || mem?.Containers == null)
			{
				continue;
			}
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				WakeContainer(mem.Containers[c], "breach");
			}
		}
		if (woke.Count == 0)
		{
			WakeNetwork("breach");
		}
	}

	private static bool ShouldDumpOnDestroy(DestroyMode mode)
	{
		if (RimPipeMod.Settings == null || !RimPipeMod.Settings.dumpOnDestroy)
		{
			return false;
		}
		return mode == DestroyMode.KillFinalize || mode == DestroyMode.Deconstruct;
	}

	/// <summary>
	/// 摧毁瞬间倾泻：最多按 1 批 rate 立刻扣一点量；环境效果和正常泄漏走同一套入口。
	/// </summary>
	private void TryDumpMemberOnce(CompPipeNetworkMember comp)
	{
		if (comp?.Containers == null || comp.parent == null)
		{
			return;
		}
		IntVec3 cell = comp.parent.Position;
		float rate = comp.Props.defaultMaxFlowRate;
		for (int i = 0; i < comp.Containers.Count; i++)
		{
			DumpContainerOnce(comp.Containers[i], rate, cell);
		}
	}

	private void TryDumpPipeOnce(CompPipeCell pipe)
	{
		if (pipe?.parent == null)
		{
			return;
		}
		IntVec3 cell = pipe.parent.Position;
		if (!cellToMapping.TryGetValue(cell, out List<Mapping> list) || list == null)
		{
			return;
		}
		HashSet<int> seen = new HashSet<int>();
		for (int i = 0; i < list.Count; i++)
		{
			Mapping m = list[i];
			if (m == null || !seen.Add(m.id))
			{
				continue;
			}
			float half = m.maxFlowRate * 0.5f;
			DumpContainerOnce(m.containerA, half, cell);
			DumpContainerOnce(m.containerB, half, cell);
		}
	}

	private void DumpContainerOnce(Container? c, float want, IntVec3 cell)
	{
		if (c == null || want <= FlowSolver.AmountEpsilon)
		{
			return;
		}
		float dump = want;
		if (dump > c.amount)
		{
			dump = c.amount;
		}
		if (dump <= FlowSolver.AmountEpsilon)
		{
			return;
		}
		Log.Message($"[RimPipe] dumpOnDestroy 倾泻 {dump:0.##} 自 {c}");
		c.CommitAmount(c.amount - dump);
		LeakEffectApplicator.Apply(c, dump, cell, map);
	}

	private void AssignContainerIds(CompPipeNetworkMember comp)
	{
		for (int i = 0; i < comp.Containers.Count; i++)
		{
			Container c = comp.Containers[i];
			if (c.id < 0)
			{
				c.id = idProvider.Next();
			}
		}
	}

	private void Enqueue(DelayedActionType type, IntVec3 cell)
	{
		delayedActions.Add(new DelayedAction(type, cell));
	}

	public void RequestFullRebuild()
	{
		needsFullRebuild = true;
		Enqueue(DelayedActionType.FullRebuild, IntVec3.Invalid);
	}

	private void ProcessDelayedActions()
	{
		if (delayedActions.Count == 0 && !needsFullRebuild)
		{
			return;
		}
		delayedActions.Clear();
		RebuildAllMappings();
		needsFullRebuild = false;
	}

	private void RebuildAllMappings()
	{
		Stopwatch sw = Stopwatch.StartNew();
		mappings.Clear();
		cellToMapping.Clear();

		HashSet<long> linkedPairs = new HashSet<long>();

		// Face-to-face
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember a = members[i];
			if (a?.parent == null || !a.parent.Spawned)
			{
				continue;
			}
			for (int p = 0; p < a.Ports.Count; p++)
			{
				Port portA = a.Ports[p];
				IntVec3 outer = portA.OuterCell;
				if (!outer.InBounds(map))
				{
					continue;
				}
				CompPipeNetworkMember? b = MemberAt(outer);
				if (b == null || b == a)
				{
					continue;
				}
				Rot4 need = portA.WorldRot.Opposite;
				Port? portB = b.FindPortFacingWorld(need);
				if (portB == null)
				{
					continue;
				}
				if (!a.parent.OccupiedRect().Contains(portB.OuterCell))
				{
					continue;
				}
				TryAddPair(portA.Container, portB.Container, a.parent as Building, b.parent as Building, linkedPairs, null);
			}
		}

		// 管道：每个连通管道分量上，对挂接端口做图上多源 BFS（Voronoi），
		// 仅邻接领地交界处建 Mapping，避免完全图导致批间振荡。
		BuildPipeAdjacentMappings(linkedPairs);

		// V-A 等：同建筑内部 Mapping（阀门开关只改 rate，不参与外部拓扑）
		BuildInternalMappings(linkedPairs);

		ApplyBreachLeakFlags();
		AssignNetworkIds();
		WakeNetwork("topology");

		sw.Stop();
		LastTopologyRebuildMs = (float)sw.Elapsed.TotalMilliseconds;

		if (!QuietDebugLogs)
		{
			Log.Message(
				$"[RimPipe] 拓扑重建：构件={members.Count} 管道格={pipeCells.Count} Mapping={mappings.Count} nets={netCount} 耗时={LastTopologyRebuildMs:F3}ms");
		}
		WarnOrphanPipeTouches();
	}

	/// <summary>
	/// 按完整 Mapping 连通关系给每个 Container/Mapping 打 netId，并重建各网的 sleepStates。
	/// </summary>
	private void AssignNetworkIds()
	{
		List<Container> all = new List<Container>();
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.Containers == null)
			{
				continue;
			}
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				Container cont = mem.Containers[c];
				if (cont == null)
				{
					continue;
				}
				cont.netId = -1;
				all.Add(cont);
			}
		}

		Dictionary<Container, int> indexOf = new Dictionary<Container, int>();
		for (int i = 0; i < all.Count; i++)
		{
			indexOf[all[i]] = i;
		}

		int[] parent = new int[all.Count];
		for (int i = 0; i < parent.Length; i++)
		{
			parent[i] = i;
		}

		int Find(int x)
		{
			while (parent[x] != x)
			{
				parent[x] = parent[parent[x]];
				x = parent[x];
			}
			return x;
		}

		void Union(int a, int b)
		{
			int ra = Find(a);
			int rb = Find(b);
			if (ra != rb)
			{
				parent[rb] = ra;
			}
		}

		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			m.netId = -1;
			if (m.IsIncomplete || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			if (!indexOf.TryGetValue(m.containerA, out int ia) || !indexOf.TryGetValue(m.containerB, out int ib))
			{
				continue;
			}
			Union(ia, ib);
		}

		Dictionary<int, int> rootToNet = new Dictionary<int, int>();
		netCount = 0;
		for (int i = 0; i < all.Count; i++)
		{
			int root = Find(i);
			if (!rootToNet.TryGetValue(root, out int nid))
			{
				nid = netCount++;
				rootToNet[root] = nid;
			}
			all[i].netId = nid;
		}

		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			Container? refC = m.containerA ?? m.containerB;
			m.netId = refC != null ? refC.netId : -1;
		}

		sleepStates = new PipeNetworkSleepState[netCount];
		for (int i = 0; i < netCount; i++)
		{
			sleepStates[i] = PipeNetworkSleepState.Busy;
		}
	}

	private void EnsureSleepStatesCapacity()
	{
		if (sleepStates.Length == netCount && (netCount > 0 || sleepStates.Length == 0))
		{
			return;
		}
		if (netCount <= 0)
		{
			sleepStates = System.Array.Empty<PipeNetworkSleepState>();
			return;
		}
		PipeNetworkSleepState[] next = new PipeNetworkSleepState[netCount];
		for (int i = 0; i < netCount; i++)
		{
			next[i] = i < sleepStates.Length ? sleepStates[i] : PipeNetworkSleepState.Busy;
		}
		sleepStates = next;
	}

	private bool HasAnyNetState(PipeNetworkSleepState state)
	{
		for (int i = 0; i < sleepStates.Length; i++)
		{
			if (sleepStates[i] == state)
			{
				return true;
			}
		}
		return false;
	}

	private bool NetAllowsAccumulate(int netId)
	{
		return GetNetSleepState(netId) == PipeNetworkSleepState.Busy;
	}

	private bool NetAllowsLightCommit(int netId)
	{
		PipeNetworkSleepState s = GetNetSleepState(netId);
		return s == PipeNetworkSleepState.Busy || s == PipeNetworkSleepState.AmbientOnly;
	}

	/// <summary>由 CompPipeCell.breached 重刷路径 Mapping.leakOpen。</summary>
	private void ApplyBreachLeakFlags()
	{
		for (int i = 0; i < mappings.Count; i++)
		{
			mappings[i].leakOpen = false;
		}
		for (int i = 0; i < pipeCells.Count; i++)
		{
			CompPipeCell pipe = pipeCells[i];
			if (pipe == null || !pipe.Breached || pipe.parent == null || !pipe.parent.Spawned)
			{
				continue;
			}
			IntVec3 cell = pipe.parent.Position;
			if (!cellToMapping.TryGetValue(cell, out List<Mapping> list) || list == null)
			{
				continue;
			}
			for (int j = 0; j < list.Count; j++)
			{
				list[j].leakOpen = true;
			}
		}
	}

	/// <summary>
	/// ExtHook：扫一遍实现了 <see cref="IPipeInternalMappingContributor"/> 的 Comp
	/// （阀、泵、换热器，以及第三方设备），让它们登记内部边。
	/// </summary>
	private void BuildInternalMappings(HashSet<long> linkedPairs)
	{
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember member = members[i];
			ThingWithComps? parent = member?.parent;
			if (parent == null || !parent.Spawned)
			{
				continue;
			}
			List<ThingComp> comps = parent.AllComps;
			if (comps == null)
			{
				continue;
			}
			for (int c = 0; c < comps.Count; c++)
			{
				if (comps[c] is IPipeInternalMappingContributor contributor)
				{
					contributor.ContributeInternalMapping(this, linkedPairs);
				}
			}
		}
	}

	/// <summary>同建筑内部 Mapping（阀 / 泵 / 换热器）。rate 与 drive / type 由调用方给定。</summary>
	public Mapping? AddInternalMapping(
		Container? ca,
		Container? cb,
		Building? owner,
		float maxFlowRate,
		HashSet<long> linkedPairs,
		FlowDriveMode flowDrive = FlowDriveMode.Equalize,
		bool forcedFromA = true,
		MappingType mappingType = MappingType.Flow)
	{
		if (ca == null || cb == null || ca == cb)
		{
			return null;
		}
		if (ca.id < 0 || cb.id < 0)
		{
			return null;
		}
		long key = ContainerPairKey(ca.id, cb.id, mappingType);
		if (!linkedPairs.Add(key))
		{
			return null;
		}
		Mapping m = new Mapping
		{
			id = idProvider.Next(),
			containerA = ca,
			containerB = cb,
			maxFlowRate = maxFlowRate,
			mappingType = mappingType,
			flowDrive = flowDrive,
			forcedFromA = forcedFromA
		};
		if (owner != null)
		{
			m.attachedBuildings.Add(owner);
		}
		mappings.Add(m);
		CacheMappingCells(m);
		return m;
	}

	public int CountMappingsFor(CompPipeNetworkMember member)
	{
		int n = 0;
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (OwnsEnd(member, m.containerA) || OwnsEnd(member, m.containerB))
			{
				n++;
			}
		}
		return n;
	}

	/// <summary>Inspect：该构件相关 Mapping 的 rate / path（R-A）。</summary>
	public void AppendMappingsInspect(CompPipeNetworkMember member, StringBuilder sb)
	{
		if (member == null || sb == null)
		{
			return;
		}
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (!OwnsEnd(member, m.containerA) && !OwnsEnd(member, m.containerB))
			{
				continue;
			}
			sb.Append("  #");
			sb.Append(m.id);
			if (m.mappingType == MappingType.Heat)
			{
				sb.Append(" Heat heatRate=");
			}
			else
			{
				sb.Append(" rate=");
			}
			sb.Append(m.maxFlowRate.ToString("0.##"));
			if (m.pathPipeCells > 0)
			{
				sb.Append(" path=");
				sb.Append(m.pathPipeCells);
			}
			sb.AppendLine();
		}
	}

	private static bool OwnsEnd(CompPipeNetworkMember member, Container? c)
	{
		return c != null && c.owner == member;
	}

	/// <summary>端口外一格是否有可直接相邻对接或管道格（检视用）。</summary>
	public bool IsPortLikelyDocked(Port port)
	{
		if (port?.owner?.parent == null || !port.owner.parent.Spawned)
		{
			return false;
		}
		IntVec3 outer = port.OuterCell;
		if (!outer.InBounds(map))
		{
			return false;
		}
		if (cellToPipe.ContainsKey(outer))
		{
			return true;
		}
		CompPipeNetworkMember? other = MemberAt(outer);
		if (other == null || other == port.owner)
		{
			return false;
		}
		Port? opp = other.FindPortFacingWorld(port.WorldRot.Opposite);
		if (opp == null)
		{
			return false;
		}
		return port.owner.parent.OccupiedRect().Contains(opp.OuterCell);
	}

	/// <summary>
	/// 管道贴着构件但未进任何端口外一格时打日志，便于发现「接了管却不进 Mapping」。
	/// </summary>
	private void WarnOrphanPipeTouches()
	{
		HashSet<IntVec3> attachedOuters = new HashSet<IntVec3>();
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember m = members[i];
			if (m?.parent == null || !m.parent.Spawned)
			{
				continue;
			}
			for (int p = 0; p < m.Ports.Count; p++)
			{
				attachedOuters.Add(m.Ports[p].OuterCell);
			}
		}
		HashSet<int> warnedMembers = new HashSet<int>();
		foreach (KeyValuePair<IntVec3, CompPipeCell> kv in cellToPipe)
		{
			IntVec3 pipeCell = kv.Key;
			if (attachedOuters.Contains(pipeCell))
			{
				continue;
			}
			foreach (IntVec3 dir in GenAdj.CardinalDirections)
			{
				CompPipeNetworkMember? mem = MemberAt(pipeCell + dir);
				if (mem == null || !warnedMembers.Add(mem.parent.thingIDNumber))
				{
					continue;
				}
				Log.Warning($"[RimPipe] 管道 {pipeCell} 邻接 {mem.parent.LabelCap}@{mem.parent.Position}，但不是其任何端口的外一格（见检视「端口(世界向)」）。不会经该侧建 Mapping。");
			}
		}
	}

	private struct PipeAttachment
	{
		public CompPipeNetworkMember member;
		public Port port;
		public Container container;
		public IntVec3 outerCell;
	}

	private void BuildPipeAdjacentMappings(HashSet<long> linkedPairs)
	{
		HashSet<IntVec3> visitedPipe = new HashSet<IntVec3>();
		foreach (KeyValuePair<IntVec3, CompPipeCell> kv in cellToPipe)
		{
			IntVec3 seed = kv.Key;
			if (visitedPipe.Contains(seed))
			{
				continue;
			}

			// 1) 收集本管道连通分量
			List<IntVec3> component = new List<IntVec3>();
			Queue<IntVec3> flood = new Queue<IntVec3>();
			flood.Enqueue(seed);
			visitedPipe.Add(seed);
			while (flood.Count > 0)
			{
				IntVec3 c = flood.Dequeue();
				component.Add(c);
				foreach (IntVec3 dir in GenAdj.CardinalDirections)
				{
					IntVec3 n = c + dir;
					if (!n.InBounds(map) || visitedPipe.Contains(n) || !cellToPipe.ContainsKey(n))
					{
						continue;
					}
					visitedPipe.Add(n);
					flood.Enqueue(n);
				}
			}

			HashSet<IntVec3> componentSet = new HashSet<IntVec3>(component);

			// 2) 找出所有外一格落在本分量上的端口挂接
			List<PipeAttachment> attachments = new List<PipeAttachment>();
			for (int i = 0; i < members.Count; i++)
			{
				CompPipeNetworkMember m = members[i];
				if (m?.parent == null || !m.parent.Spawned)
				{
					continue;
				}
				for (int p = 0; p < m.Ports.Count; p++)
				{
					Port port = m.Ports[p];
					IntVec3 outer = port.OuterCell;
					if (!componentSet.Contains(outer))
					{
						continue;
					}
					Container? cont = port.Container;
					if (cont == null)
					{
						continue;
					}
					attachments.Add(new PipeAttachment
					{
						member = m,
						port = port,
						container = cont,
						outerCell = outer
					});
				}
			}
			if (attachments.Count < 2)
			{
				continue;
			}

			// 3) 多源 BFS：每个挂接点 outerCell 为领地种子，交界建 Mapping
			Dictionary<IntVec3, int> owner = new Dictionary<IntVec3, int>();
			Queue<IntVec3> q = new Queue<IntVec3>();
			for (int i = 0; i < attachments.Count; i++)
			{
				IntVec3 cell = attachments[i].outerCell;
				if (owner.ContainsKey(cell))
				{
					// 两端口抢同一格：直接视为邻接
					int other = owner[cell];
					if (other != i)
					{
						AddPipePair(attachments[other], attachments[i], linkedPairs, component, componentSet);
					}
					continue;
				}
				owner[cell] = i;
				q.Enqueue(cell);
			}

			HashSet<long> borderPairs = new HashSet<long>();
			while (q.Count > 0)
			{
				IntVec3 cell = q.Dequeue();
				int id = owner[cell];
				foreach (IntVec3 dir in GenAdj.CardinalDirections)
				{
					IntVec3 n = cell + dir;
					if (!componentSet.Contains(n))
					{
						continue;
					}
					if (owner.TryGetValue(n, out int otherId))
					{
						if (otherId != id)
						{
							long key = PairKey(id, otherId);
							if (borderPairs.Add(key))
							{
								AddPipePair(attachments[id], attachments[otherId], linkedPairs, component, componentSet);
							}
						}
						continue;
					}
					owner[n] = id;
					q.Enqueue(n);
				}
			}
		}
	}

	private void AddPipePair(
		PipeAttachment a,
		PipeAttachment b,
		HashSet<long> linkedPairs,
		List<IntVec3> component,
		HashSet<IntVec3> componentSet)
	{
		List<Building> attached = new List<Building>();
		if (a.member.parent is Building ba)
		{
			attached.Add(ba);
		}
		if (b.member.parent is Building bb && bb != a.member.parent)
		{
			attached.Add(bb);
		}
		for (int i = 0; i < component.Count; i++)
		{
			if (cellToPipe.TryGetValue(component[i], out CompPipeCell pipe) && pipe.parent is Building pb && !attached.Contains(pb))
			{
				attached.Add(pb);
			}
		}
		int pathPipeCells = ShortestPipePathCellCount(a.outerCell, b.outerCell, componentSet);
		TryAddPair(
			a.container,
			b.container,
			a.member.parent as Building,
			b.member.parent as Building,
			linkedPairs,
			attached,
			pathPipeCells);
	}

	/// <summary>管道分量内两挂接格最短路径的格数（含起终；同格=1）。失败返回 0。</summary>
	private static int ShortestPipePathCellCount(IntVec3 start, IntVec3 end, HashSet<IntVec3> componentSet)
	{
		if (!componentSet.Contains(start) || !componentSet.Contains(end))
		{
			return 0;
		}
		if (start == end)
		{
			return 1;
		}
		Queue<IntVec3> q = new Queue<IntVec3>();
		Dictionary<IntVec3, IntVec3> prev = new Dictionary<IntVec3, IntVec3>();
		q.Enqueue(start);
		prev[start] = start;
		while (q.Count > 0)
		{
			IntVec3 c = q.Dequeue();
			foreach (IntVec3 dir in GenAdj.CardinalDirections)
			{
				IntVec3 n = c + dir;
				if (!componentSet.Contains(n) || prev.ContainsKey(n))
				{
					continue;
				}
				prev[n] = c;
				if (n == end)
				{
					int count = 1;
					IntVec3 walk = n;
					while (walk != start)
					{
						count++;
						walk = prev[walk];
					}
					return count;
				}
				q.Enqueue(n);
			}
		}
		return 0;
	}

	private CompPipeNetworkMember? MemberAt(IntVec3 cell)
	{
		List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
		for (int i = 0; i < things.Count; i++)
		{
			if (things[i] is ThingWithComps twc)
			{
				CompPipeNetworkMember comp = twc.GetComp<CompPipeNetworkMember>();
				if (comp != null)
				{
					return comp;
				}
			}
		}
		return null;
	}

	private static long PairKey(int idA, int idB)
	{
		if (idA > idB)
		{
			(idA, idB) = (idB, idA);
		}
		return ((long)idA << 32) | (uint)idB;
	}

	/// <summary>H-A：同对 Container 可同时存在 Flow + Heat，key 含 mappingType。</summary>
	private static long ContainerPairKey(int idA, int idB, MappingType type)
	{
		if (idA > idB)
		{
			(idA, idB) = (idB, idA);
		}
		return ((long)(byte)type << 56) | ((long)(uint)idA << 28) | (uint)(idB & 0x0FFFFFFF);
	}

	private void TryAddPair(
		Container? ca,
		Container? cb,
		Building? buildingA,
		Building? buildingB,
		HashSet<long> linkedPairs,
		List<Building>? pathBuildings,
		int pathPipeCells = 0)
	{
		if (ca == null || cb == null || ca == cb)
		{
			return;
		}
		if (ca.id < 0 || cb.id < 0)
		{
			return;
		}
		long key = ContainerPairKey(ca.id, cb.id, MappingType.Flow);
		if (!linkedPairs.Add(key))
		{
			return;
		}
		Mapping m = new Mapping
		{
			id = idProvider.Next(),
			containerA = ca,
			containerB = cb,
			pathPipeCells = pathPipeCells,
			maxFlowRate = ResolveMaxFlowRate(ca, cb, pathPipeCells),
			mappingType = MappingType.Flow
		};
		if (pathBuildings != null)
		{
			m.attachedBuildings.AddRange(pathBuildings);
		}
		else
		{
			if (buildingA != null)
			{
				m.attachedBuildings.Add(buildingA);
			}
			if (buildingB != null && buildingB != buildingA)
			{
				m.attachedBuildings.Add(buildingB);
			}
		}
		mappings.Add(m);
		CacheMappingCells(m);
	}

	/// <summary>R-A 下限，避免极长管 rate→0。</summary>
	private const float MinFlowRateAfterResistance = 0.1f;

	/// <summary>
	/// 阻力公式：base 取两端默认 maxFlowRate 的较小值；
	/// 实际 rate = max(下限, base / max(1, 路径格数))。
	/// 路径格数为 0（直接相邻）时除数按 1 算，也就是不因长度变慢。
	/// </summary>
	private float ResolveMaxFlowRate(Container a, Container b, int pathPipeCells)
	{
		float rateA = a.owner?.Props.defaultMaxFlowRate ?? 10f;
		float rateB = b.owner?.Props.defaultMaxFlowRate ?? 10f;
		float baseRate = rateA < rateB ? rateA : rateB;
		float divisor = pathPipeCells < 1 ? 1f : pathPipeCells;
		float rate = baseRate / divisor;
		return rate < MinFlowRateAfterResistance ? MinFlowRateAfterResistance : rate;
	}

	private void CacheMappingCells(Mapping m)
	{
		for (int i = 0; i < m.attachedBuildings.Count; i++)
		{
			Building b = m.attachedBuildings[i];
			if (b == null || !b.Spawned)
			{
				continue;
			}
			foreach (IntVec3 cell in b.OccupiedRect())
			{
				if (!cellToMapping.TryGetValue(cell, out List<Mapping> list))
				{
					list = new List<Mapping>();
					cellToMapping[cell] = list;
				}
				if (!list.Contains(m))
				{
					list.Add(m);
				}
			}
		}
	}

	/// <summary>批内欠松弛迭代次数（多罐网防振荡）。</summary>
	private const int FlowRelaxIterations = 12;
	/// <summary>每轮迭代应用 want 的比例。</summary>
	private const float FlowRelaxFactor = 0.45f;
	/// <summary>H-A 导热欠松弛轮数。</summary>
	private const int HeatRelaxIterations = 4;
	private const float HeatRelaxFactor = 0.45f;

	private void AccumulateFlow()
	{
		// maxFlowRate 是整批上限。虚拟量多轮欠松弛；每轮必须用 Jacobi（基于本轮初值同步提交），
		// 禁止边算边写，否则入流竞争时 Mapping 列表顺序会导致左右不对称（见 Player.log 对称失败）。
		pendingDeltas.Clear();
		for (int i = 0; i < mappings.Count; i++)
		{
			if (mappings[i].mappingType == MappingType.Flow)
			{
				mappings[i].ClearBatch();
			}
		}

		Dictionary<Container, float> virtualAmount = new Dictionary<Container, float>();
		for (int i = 0; i < members.Count; i++)
		{
			for (int c = 0; c < members[i].Containers.Count; c++)
			{
				Container cont = members[i].Containers[c];
				virtualAmount[cont] = cont.amount;
			}
		}

		for (int iter = 0; iter < FlowRelaxIterations; iter++)
		{
			List<(Mapping map, Container src, Container tgt, float want)> wants = new List<(Mapping, Container, Container, float)>();
			for (int i = 0; i < mappings.Count; i++)
			{
				Mapping m = mappings[i];
				if (m.mappingType != MappingType.Flow || m.IsIncomplete || m.leakOpen || m.containerA == null || m.containerB == null)
				{
					continue;
				}
				if (!NetAllowsAccumulate(m.netId))
				{
					continue;
				}
				float amountA = virtualAmount[m.containerA];
				float amountB = virtualAmount[m.containerB];
				float want = FlowSolver.ComputeWant(m, amountA, amountB, out Container? src, out Container? tgt, out _);
				if (want > FlowSolver.AmountEpsilon && src != null && tgt != null)
				{
					wants.Add((m, src, tgt, want));
				}
			}

			Dictionary<Container, float> outSum = new Dictionary<Container, float>();
			Dictionary<Container, float> inSum = new Dictionary<Container, float>();
			for (int i = 0; i < wants.Count; i++)
			{
				outSum.TryGetValue(wants[i].src, out float o);
				outSum[wants[i].src] = o + wants[i].want;
				inSum.TryGetValue(wants[i].tgt, out float inn);
				inSum[wants[i].tgt] = inn + wants[i].want;
			}

			// 本轮净增量（相对本轮初值），全部算完再写回
			Dictionary<Container, float> stepDelta = new Dictionary<Container, float>();
			for (int i = 0; i < wants.Count; i++)
			{
				Mapping m = wants[i].map;
				Container src = wants[i].src;
				Container tgt = wants[i].tgt;
				float w = wants[i].want;
				float srcAmt = virtualAmount[src];
				float tgtAmt = virtualAmount[tgt];
				if (outSum.TryGetValue(src, out float os) && os > srcAmt + FlowSolver.AmountEpsilon)
				{
					w *= srcAmt / os;
				}
				float free = tgt.capacity - tgtAmt;
				if (inSum.TryGetValue(tgt, out float ins) && ins > free + FlowSolver.AmountEpsilon)
				{
					w *= free / ins;
				}
				w *= FlowRelaxFactor;
				if (w <= FlowSolver.AmountEpsilon)
				{
					continue;
				}
				stepDelta.TryGetValue(src, out float ds);
				stepDelta[src] = ds - w;
				stepDelta.TryGetValue(tgt, out float dt);
				stepDelta[tgt] = dt + w;
				m.batchSource = src;
				m.batchTarget = tgt;
				m.batchWant += w;
			}

			foreach (KeyValuePair<Container, float> kv in stepDelta)
			{
				float next = virtualAmount[kv.Key] + kv.Value;
				if (next < 0f)
				{
					next = 0f;
				}
				if (next > kv.Key.capacity)
				{
					next = kv.Key.capacity;
				}
				virtualAmount[kv.Key] = next;
			}
		}

		foreach (KeyValuePair<Container, float> kv in virtualAmount)
		{
			float delta = kv.Value - kv.Key.amount;
			if (System.Math.Abs(delta) > FlowSolver.AmountEpsilon)
			{
				pendingDeltas[kv.Key] = delta;
			}
		}
	}

	/// <summary>H-A：仅 MappingType.Heat；写 pendingTempDeltas（ΔT），不改 amount。</summary>
	private void AccumulateHeat()
	{
		pendingTempDeltas.Clear();
		for (int i = 0; i < mappings.Count; i++)
		{
			if (mappings[i].mappingType == MappingType.Heat)
			{
				mappings[i].ClearBatch();
			}
		}

		Dictionary<Container, float> virtualTemp = new Dictionary<Container, float>();
		for (int i = 0; i < members.Count; i++)
		{
			for (int c = 0; c < members[i].Containers.Count; c++)
			{
				Container cont = members[i].Containers[c];
				virtualTemp[cont] = cont.temperature;
			}
		}

		for (int iter = 0; iter < HeatRelaxIterations; iter++)
		{
			List<(Mapping map, Container hot, Container cold, float q)> wants = new List<(Mapping, Container, Container, float)>();
			for (int i = 0; i < mappings.Count; i++)
			{
				Mapping m = mappings[i];
				if (m.mappingType != MappingType.Heat || m.IsIncomplete || m.leakOpen
					|| m.containerA == null || m.containerB == null)
				{
					continue;
				}
				if (!NetAllowsAccumulate(m.netId))
				{
					continue;
				}
				float q = HeatSolver.ComputeHeatWant(
					m,
					m.containerA.amount,
					m.containerB.amount,
					virtualTemp[m.containerA],
					virtualTemp[m.containerB],
					out Container? hot,
					out Container? cold);
				if (q > FlowSolver.AmountEpsilon && hot != null && cold != null)
				{
					wants.Add((m, hot, cold, q));
				}
			}

			Dictionary<Container, float> stepDelta = new Dictionary<Container, float>();
			for (int i = 0; i < wants.Count; i++)
			{
				Mapping m = wants[i].map;
				Container hot = wants[i].hot;
				Container cold = wants[i].cold;
				float q = wants[i].q * HeatRelaxFactor;
				float mHot = hot.amount;
				float mCold = cold.amount;
				if (mHot <= FlowSolver.AmountEpsilon || mCold <= FlowSolver.AmountEpsilon)
				{
					continue;
				}
				float dHot = -q / mHot;
				float dCold = q / mCold;
				stepDelta.TryGetValue(hot, out float dh);
				stepDelta[hot] = dh + dHot;
				stepDelta.TryGetValue(cold, out float dc);
				stepDelta[cold] = dc + dCold;
				m.batchSource = hot;
				m.batchTarget = cold;
				m.batchWant += q;
			}

			foreach (KeyValuePair<Container, float> kv in stepDelta)
			{
				virtualTemp[kv.Key] = virtualTemp[kv.Key] + kv.Value;
			}
		}

		foreach (KeyValuePair<Container, float> kv in virtualTemp)
		{
			float delta = kv.Value - kv.Key.temperature;
			if (System.Math.Abs(delta) > 1e-6f)
			{
				pendingTempDeltas[kv.Key] = delta;
			}
		}
	}

	private void AddDelta(Container c, float delta)
	{
		pendingDeltas.TryGetValue(c, out float cur);
		pendingDeltas[c] = cur + delta;
	}

	private void CommitDeltas()
	{
		ProcessLeakDeltas();
		ApplyPendingAmountDeltas();
		ApplyFlowMixing();
		CommitChem();
		CommitTempDeltas();
		ApplyAmbientHeatExchange();
		SnapshotFlowBatchWants();

		for (int i = 0; i < mappings.Count; i++)
		{
			mappings[i].ClearBatch();
		}
	}

	/// <summary>预览各绑定 batchN（休眠/Debug）；不写量。</summary>
	private void AccumulateChem()
	{
		for (int i = 0; i < chemReactors.Count; i++)
		{
			ChemReactorBinding b = chemReactors[i];
			b.batchN = ChemSolver.ComputeBatchCount(
				b.reaction, b.inputs, b.outputs, b.enabled, b.mixRatio, out _, out _);
		}
	}

	/// <summary>
	/// 按当前量重算 n，按 mixRatio/η 扣入加出；P5b：反应热写入 pendingTempDeltas（ΔT=Q/m）。
	/// </summary>
	private void CommitChem()
	{
		for (int i = 0; i < chemReactors.Count; i++)
		{
			ChemReactorBinding b = chemReactors[i];
			float n = ChemSolver.ComputeBatchCount(
				b.reaction, b.inputs, b.outputs, b.enabled, b.mixRatio,
				out float efficiency, out _);
			b.batchN = n;
			b.lastBatchN = n;
			b.lastEfficiency = efficiency;
			if (n <= FlowSolver.AmountEpsilon || b.reaction == null)
			{
				continue;
			}
			ChemSolver.BuildAmountDeltas(
				b.reaction, b.inputs, b.outputs, n, b.mixRatio, efficiency, chemDeltaScratch);
			for (int d = 0; d < chemDeltaScratch.Count; d++)
			{
				Container c = chemDeltaScratch[d].c;
				float delta = chemDeltaScratch[d].delta;
				if (delta > FlowSolver.AmountEpsilon)
				{
					int outIdx = IndexOfContainer(b.outputs, c);
					if (outIdx >= 0 && c.amount <= FlowSolver.AmountEpsilon)
					{
						c.fluid = b.reaction.outputs[outIdx].fluid;
					}
				}
				c.CommitAmount(c.amount + delta);
				WakeContainer(c, "chem");
			}
			ApplyChemReactionHeat(b, n);
		}
	}

	/// <summary>P5b：Q = n×heatPerBatch；ΔT = Q/m（量已 Commit）；写入 pendingTempDeltas。</summary>
	private void ApplyChemReactionHeat(ChemReactorBinding b, float n)
	{
		PipeReactionDef? rx = b.reaction;
		if (rx == null || System.Math.Abs(rx.heatPerBatch) <= FlowSolver.AmountEpsilon)
		{
			return;
		}
		Container? tgt = ResolveChemHeatTarget(b, rx);
		if (tgt == null || tgt.amount <= FlowSolver.AmountEpsilon)
		{
			return;
		}
		float q = n * rx.heatPerBatch;
		float dT = q / tgt.amount;
		pendingTempDeltas.TryGetValue(tgt, out float cur);
		pendingTempDeltas[tgt] = cur + dT;
		WakeContainer(tgt, "chemHeat");
	}

	private static Container? ResolveChemHeatTarget(ChemReactorBinding b, PipeReactionDef rx)
	{
		if (rx.heatTargetContainerIndex < 0)
		{
			return b.outputs.Count > 0 ? b.outputs[0] : null;
		}
		CompPipeNetworkMember? mem = null;
		if (b.owner is CompPipeReactor reactor)
		{
			mem = reactor.parent.TryGetComp<CompPipeNetworkMember>();
		}
		else if (b.inputs.Count > 0)
		{
			mem = b.inputs[0]?.owner;
		}
		if (mem == null || rx.heatTargetContainerIndex >= mem.Containers.Count)
		{
			return b.outputs.Count > 0 ? b.outputs[0] : null;
		}
		return mem.Containers[rx.heatTargetContainerIndex];
	}

	private static int IndexOfContainer(List<Container> list, Container c)
	{
		for (int i = 0; i < list.Count; i++)
		{
			if (ReferenceEquals(list[i], c))
			{
				return i;
			}
		}
		return -1;
	}

	/// <summary>
	/// AmbientOnly 网：跳过 Accumulate 之后，Commit 仍要跑泄漏和环境散热；这时 Flow/Heat 的 pending 应该是空的。
	/// </summary>
	private void CommitAmbientAndLeaksOnly()
	{
		ProcessLeakDeltas();
		ApplyPendingAmountDeltas();
		pendingTempDeltas.Clear();
		ApplyAmbientHeatExchange();
		for (int i = 0; i < mappings.Count; i++)
		{
			mappings[i].ClearBatch();
		}
	}

	/// <summary>
	/// Overlay 用：完整 Commit 在清掉本批临时字段之前，先把流量快照到 lastBatchWant。
	/// </summary>
	private void SnapshotFlowBatchWants()
	{
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.mappingType != MappingType.Flow)
			{
				continue;
			}
			if (!NetAllowsAccumulate(m.netId))
			{
				m.lastBatchWant = 0f;
				continue;
			}
			m.lastBatchWant = m.batchWant;
			if (m.batchWant > FlowSolver.AmountEpsilon && m.batchSource != null && m.containerA != null)
			{
				m.lastBatchFromA = ReferenceEquals(m.batchSource, m.containerA);
			}
		}
	}

	private void ApplyPendingAmountDeltas()
	{
		foreach (KeyValuePair<Container, float> kv in pendingDeltas)
		{
			Container c = kv.Key;
			float d = kv.Value;
			if (System.Math.Abs(d) <= FlowSolver.AmountEpsilon)
			{
				continue;
			}
			c.CommitAmount(c.amount + d);
		}
		pendingDeltas.Clear();
	}

	/// <summary>
	/// 泄漏：不完整 → 残存端；leakOpen → 两端各开口；储罐 breached → 本罐开口。
	/// 写入 pendingDeltas；E-A 在实际销毁量 >0 后施加。由调用方再 ApplyPendingAmountDeltas。
	/// </summary>
	private void ProcessLeakDeltas()
	{
		List<(Container c, float leak, IntVec3 cell)> leakWants = new List<(Container, float, IntVec3)>();
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.mappingType != MappingType.Flow)
			{
				continue;
			}
			if (!NetAllowsLightCommit(m.netId))
			{
				continue;
			}
			if (m.leakOpen)
			{
				float half = m.maxFlowRate * 0.5f;
				IntVec3 cell = ResolveLeakCell(m, m.containerA ?? m.containerB);
				if (m.containerA != null)
				{
					leakWants.Add((m.containerA, half, cell));
				}
				if (m.containerB != null)
				{
					leakWants.Add((m.containerB, half, cell));
				}
				continue;
			}
			if (!m.IsIncomplete)
			{
				continue;
			}
			Container? survivor = m.containerA ?? m.containerB;
			if (survivor != null)
			{
				leakWants.Add((survivor, m.maxFlowRate, ResolveLeakCell(m, survivor)));
			}
		}

		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.parent == null || !mem.parent.Spawned)
			{
				continue;
			}
			CompPipeBreachable? br = mem.parent.TryGetComp<CompPipeBreachable>();
			if (br == null || !br.Breached)
			{
				continue;
			}
			IntVec3 cell = mem.parent.Position;
			float rate = mem.Props.defaultMaxFlowRate;
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				Container cont = mem.Containers[c];
				if (!NetAllowsLightCommit(cont.netId))
				{
					continue;
				}
				leakWants.Add((cont, rate, cell));
			}
		}

		Dictionary<Container, float> leakSum = new Dictionary<Container, float>();
		for (int i = 0; i < leakWants.Count; i++)
		{
			leakSum.TryGetValue(leakWants[i].c, out float s);
			leakSum[leakWants[i].c] = s + leakWants[i].leak;
		}
		for (int i = 0; i < leakWants.Count; i++)
		{
			Container c = leakWants[i].c;
			float leak = leakWants[i].leak;
			IntVec3 cell = leakWants[i].cell;
			if (leakSum.TryGetValue(c, out float sum) && sum > c.amount + FlowSolver.AmountEpsilon)
			{
				leak *= c.amount / sum;
			}
			if (leak <= FlowSolver.AmountEpsilon)
			{
				continue;
			}
			if (!QuietDebugLogs)
			{
				Log.Warning($"[RimPipe] 泄漏销毁 {leak:0.##} 自 {c}");
			}
			AddDelta(c, -leak);
			LeakEffectApplicator.Apply(c, leak, cell, map);
		}
	}

	/// <summary>
	/// H-A：量已 Commit 后，按本批 Flow batchWant 混温。
	/// tgt：T' = ((m'-w)*T + w*T_src) / m'；src 失液均温不变。
	/// </summary>
	private void ApplyFlowMixing()
	{
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.mappingType != MappingType.Flow || m.batchWant <= FlowSolver.AmountEpsilon
				|| m.batchSource == null || m.batchTarget == null)
			{
				continue;
			}
			Container src = m.batchSource;
			Container tgt = m.batchTarget;
			float w = m.batchWant;
			float tSrc = src.temperature;
			float tTgt = tgt.temperature;
			float mTgtBefore = tgt.amount - w;
			if (mTgtBefore < 0f)
			{
				mTgtBefore = 0f;
			}
			if (tgt.amount > FlowSolver.AmountEpsilon)
			{
				float tNew = (mTgtBefore * tTgt + w * tSrc) / tgt.amount;
				tgt.CommitTemperature(tNew);
			}
			else if (w > FlowSolver.AmountEpsilon)
			{
				tgt.CommitTemperature(tSrc);
			}
			// src 侧均匀流体失液：温度不变
		}
	}

	private void CommitTempDeltas()
	{
		foreach (KeyValuePair<Container, float> kv in pendingTempDeltas)
		{
			Container c = kv.Key;
			float d = kv.Value;
			if (System.Math.Abs(d) <= 1e-6f)
			{
				continue;
			}
			c.CommitTemperature(c.temperature + d);
		}
		pendingTempDeltas.Clear();
	}

	/// <summary>
	/// Amb-A：有液 Container 向格温靠拢。
	/// Q = min(maxAmbientHeatRate, |ΔT|/2*m) * (1-insulation) —— 先截断再乘保温，避免两种保温同顶满 rate。
	/// PushHeat 仅室内（!UsesOutdoorTemperature）；室外只改罐温、不推热（对齐官方房间热能）。
	/// </summary>
	private void ApplyAmbientHeatExchange()
	{
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.parent == null || !mem.parent.Spawned)
			{
				continue;
			}
			IntVec3 cell = mem.parent.Position;
			float tamb = GenTemperature.GetTemperatureForCell(cell, map);
			float insulation = mem.Props.insulation;
			if (insulation < 0f)
			{
				insulation = 0f;
			}
			if (insulation > 1f)
			{
				insulation = 1f;
			}
			float leakFactor = 1f - insulation;
			float maxRate = mem.Props.maxAmbientHeatRate;
			if (maxRate <= FlowSolver.AmountEpsilon || leakFactor <= FlowSolver.AmountEpsilon)
			{
				continue;
			}

			bool canPushRoomHeat = false;
			Room? room = cell.GetRoom(map);
			if (room != null && !room.UsesOutdoorTemperature)
			{
				canPushRoomHeat = true;
			}

			for (int c = 0; c < mem.Containers.Count; c++)
			{
				Container cont = mem.Containers[c];
				if (!NetAllowsLightCommit(cont.netId))
				{
					continue;
				}
				float m = cont.amount;
				if (m <= FlowSolver.AmountEpsilon)
				{
					continue;
				}
				float dT = cont.temperature - tamb;
				if (System.Math.Abs(dT) <= HeatSolver.TempEpsilon)
				{
					continue;
				}
				// 先按 |ΔT|/2*m 与 maxRate 取基值，再乘 (1-insulation)，保证保温一定拉开 Q
				float qBase = System.Math.Abs(dT) / 2f * m;
				if (qBase > maxRate)
				{
					qBase = maxRate;
				}
				float qWant = qBase * leakFactor;
				if (qWant <= FlowSolver.AmountEpsilon)
				{
					continue;
				}
				float tNew = cont.temperature - (dT > 0f ? 1f : -1f) * qWant / m;
				cont.CommitTemperature(tNew);
				// 流体变凉 → 房间得热(+Q)；流体变热 → 房间失热(-Q)；失败不回滚 T
				if (canPushRoomHeat)
				{
					float push = dT > 0f ? qWant : -qWant;
					GenTemperature.PushHeat(cell, map, push);
				}
			}
		}
	}

	/// <summary>E-A：破损管道格优先，否则任一管道格，再否则容器建筑格。</summary>
	private IntVec3 ResolveLeakCell(Mapping m, Container? prefer)
	{
		IntVec3 fallback = IntVec3.Invalid;
		if (prefer?.owner?.parent != null && prefer.owner.parent.Spawned)
		{
			fallback = prefer.owner.parent.Position;
		}
		if (m?.attachedBuildings == null)
		{
			return fallback;
		}
		IntVec3 anyPipe = IntVec3.Invalid;
		for (int i = 0; i < m.attachedBuildings.Count; i++)
		{
			Building b = m.attachedBuildings[i];
			if (b == null || !b.Spawned)
			{
				continue;
			}
			CompPipeCell? pipe = b.TryGetComp<CompPipeCell>();
			if (pipe == null)
			{
				continue;
			}
			if (pipe.Breached)
			{
				return b.Position;
			}
			if (!anyPipe.IsValid)
			{
				anyPipe = b.Position;
			}
		}
		return anyPipe.IsValid ? anyPipe : fallback;
	}

	/// <summary>
	/// 低开销地按网重算休眠状态（不算 Jacobi）。名字沿用旧入口，方便 Debug 调用。
	/// </summary>
	public void ReevaluateSleepState()
	{
		ReevaluateAllNetSleepStates();
	}

	public void ReevaluateAllNetSleepStates()
	{
		EnsureSleepStatesCapacity();
		if (netCount <= 0)
		{
			sleepStates = System.Array.Empty<PipeNetworkSleepState>();
			return;
		}
		for (int n = 0; n < netCount; n++)
		{
			if (HasLeakOrBreachDriver(n) || HasFlowOrHeatWant(n) || HasChemWant(n))
			{
				sleepStates[n] = PipeNetworkSleepState.Busy;
			}
			else if (HasAmbientNeed(n))
			{
				sleepStates[n] = PipeNetworkSleepState.AmbientOnly;
			}
			else
			{
				sleepStates[n] = PipeNetworkSleepState.FullyQuiet;
			}
		}
	}

	private bool HasLeakOrBreachDriver(int netId)
	{
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.netId != netId || m.mappingType != MappingType.Flow)
			{
				continue;
			}
			if (m.leakOpen || m.IsIncomplete)
			{
				return true;
			}
		}
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.parent == null || !mem.parent.Spawned)
			{
				continue;
			}
			CompPipeBreachable? br = mem.parent.TryGetComp<CompPipeBreachable>();
			if (br == null || !br.Breached)
			{
				continue;
			}
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				Container cont = mem.Containers[c];
				if (cont.netId != netId)
				{
					continue;
				}
				if (cont.amount > FlowSolver.AmountEpsilon)
				{
					return true;
				}
			}
		}
		// 破损管格：凡路径 Mapping 属本网即驱动 Busy
		for (int i = 0; i < pipeCells.Count; i++)
		{
			CompPipeCell pipe = pipeCells[i];
			if (pipe == null || !pipe.Breached || pipe.parent == null)
			{
				continue;
			}
			if (!cellToMapping.TryGetValue(pipe.parent.Position, out List<Mapping>? list) || list == null)
			{
				continue;
			}
			for (int j = 0; j < list.Count; j++)
			{
				if (list[j].netId == netId)
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool HasFlowOrHeatWant(int netId)
	{
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.netId != netId || m.IsIncomplete || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			if (m.mappingType == MappingType.Flow)
			{
				if (m.leakOpen)
				{
					continue;
				}
				float want = FlowSolver.ComputeWant(m, out _, out _, out _);
				if (want > FlowSolver.AmountEpsilon)
				{
					return true;
				}
			}
			else if (m.mappingType == MappingType.Heat)
			{
				float q = HeatSolver.ComputeHeatWant(
					m,
					m.containerA.amount,
					m.containerB.amount,
					m.containerA.temperature,
					m.containerB.temperature,
					out _,
					out _);
				if (q > FlowSolver.AmountEpsilon)
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>P2：绑定涉及本网容器且可转化 → Busy。</summary>
	private bool HasChemWant(int netId)
	{
		for (int i = 0; i < chemReactors.Count; i++)
		{
			ChemReactorBinding b = chemReactors[i];
			if (!BindingTouchesNet(b, netId))
			{
				continue;
			}
			float n = ChemSolver.ComputeBatchCount(
				b.reaction, b.inputs, b.outputs, b.enabled, b.mixRatio, out _, out _);
			if (n > FlowSolver.AmountEpsilon)
			{
				return true;
			}
		}
		return false;
	}

	private static bool BindingTouchesNet(ChemReactorBinding b, int netId)
	{
		if (netId < 0)
		{
			return true;
		}
		for (int i = 0; i < b.inputs.Count; i++)
		{
			if (b.inputs[i] != null && b.inputs[i].netId == netId)
			{
				return true;
			}
		}
		for (int i = 0; i < b.outputs.Count; i++)
		{
			if (b.outputs[i] != null && b.outputs[i].netId == netId)
			{
				return true;
			}
		}
		return false;
	}

	private bool HasAmbientNeed(int netId)
	{
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.parent == null || !mem.parent.Spawned)
			{
				continue;
			}
			float insulation = mem.Props.insulation;
			if (insulation < 0f)
			{
				insulation = 0f;
			}
			if (insulation > 1f)
			{
				insulation = 1f;
			}
			float leakFactor = 1f - insulation;
			float maxRate = mem.Props.maxAmbientHeatRate;
			if (maxRate <= FlowSolver.AmountEpsilon || leakFactor <= FlowSolver.AmountEpsilon)
			{
				continue;
			}
			float tamb = GenTemperature.GetTemperatureForCell(mem.parent.Position, map);
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				Container cont = mem.Containers[c];
				if (cont.netId != netId || cont.amount <= FlowSolver.AmountEpsilon)
				{
					continue;
				}
				if (System.Math.Abs(cont.temperature - tamb) > HeatSolver.TempEpsilon)
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>调试：只冲刷延迟拓扑，不算流。</summary>
	public void DebugProcessTopology()
	{
		ProcessDelayedActions();
	}

	/// <summary>调试：强制整图拓扑重建并打耗时日志（临时关闭 Quiet）。</summary>
	public void DebugTimedFullRebuild()
	{
		bool prevQuiet = QuietDebugLogs;
		QuietDebugLogs = false;
		try
		{
			RequestFullRebuild();
			ProcessDelayedActions();
		}
		finally
		{
			QuietDebugLogs = prevQuiet;
		}
	}

	/// <summary>调试：无视相位与休眠，立即跑一轮 Accumulate+完整 Commit，再评估休眠。</summary>
	public void DebugForceOneBatch()
	{
		ProcessDelayedActions();
		WakeNetwork("debugForce");
		AccumulateFlow();
		AccumulateHeat();
		AccumulateChem();
		CommitDeltas();
		ReevaluateAllNetSleepStates();
	}

	/// <summary>调试：仅跑 AmbientOnly 路径（泄漏+Amb），用于仅Amb断言。</summary>
	public void DebugForceAmbientOnlyBatch()
	{
		ProcessDelayedActions();
		CommitAmbientAndLeaksOnly();
		ReevaluateAllNetSleepStates();
	}

	/// <summary>
	/// 稳定公开 API：设置管道格或可破损构件的 breached；内部走既有 setter（会 Wake、刷 leakOpen）。
	/// </summary>
	public bool TrySetBreached(Thing? thing, bool breached)
	{
		if (thing == null)
		{
			return false;
		}
		CompPipeCell? cell = thing.TryGetComp<CompPipeCell>();
		if (cell != null)
		{
			cell.Breached = breached;
			return true;
		}
		CompPipeBreachable? br = thing.TryGetComp<CompPipeBreachable>();
		if (br != null)
		{
			br.Breached = breached;
			return true;
		}
		return false;
	}

	/// <summary>Bridge-A：读取 breached；无对应 Comp 返回 false。</summary>
	public bool TryGetBreached(Thing? thing, out bool breached)
	{
		breached = false;
		if (thing == null)
		{
			return false;
		}
		CompPipeCell? cell = thing.TryGetComp<CompPipeCell>();
		if (cell != null)
		{
			breached = cell.Breached;
			return true;
		}
		CompPipeBreachable? br = thing.TryGetComp<CompPipeBreachable>();
		if (br != null)
		{
			breached = br.Breached;
			return true;
		}
		return false;
	}

	/// <summary>
	/// 稳定公开 API：设置容器量（钳到 0～capacity），同步压力，并唤醒它所在的网。
	/// 第三方模组不要直接调 Container.CommitAmount。
	/// </summary>
	public bool TrySetAmount(Container? c, float amount)
	{
		if (c == null)
		{
			return false;
		}
		c.CommitAmount(Mathf.Clamp(amount, 0f, c.capacity));
		WakeContainer(c, "apiSetAmount");
		return true;
	}

	/// <summary>相对增减量；内部还是走 TrySetAmount。</summary>
	public bool TryAddAmount(Container? c, float delta)
	{
		if (c == null)
		{
			return false;
		}
		return TrySetAmount(c, c.amount + delta);
	}

	/// <summary>设置容器温度，并唤醒它所在的网。</summary>
	public bool TrySetTemperature(Container? c, float temperature)
	{
		if (c == null)
		{
			return false;
		}
		c.CommitTemperature(temperature);
		WakeContainer(c, "apiSetTemp");
		return true;
	}

	/// <summary>调试：设置容器温度（H-A）；薄包装 → TrySetTemperature。</summary>
	public void DebugSetTemperature(Container c, float temperature)
	{
		TrySetTemperature(c, temperature);
	}

	/// <summary>调试泄漏：标记全部 Mapping 开口泄漏（两端均泄）。</summary>
	public void DebugMarkAllLeakOpen()
	{
		for (int i = 0; i < mappings.Count; i++)
		{
			mappings[i].leakOpen = true;
		}
		WakeNetwork("debugLeakOpen");
	}

	public string Dump()
	{
		StringBuilder sb = new StringBuilder();
		int busy = 0, amb = 0, quiet = 0;
		for (int i = 0; i < sleepStates.Length; i++)
		{
			if (sleepStates[i] == PipeNetworkSleepState.Busy)
			{
				busy++;
			}
			else if (sleepStates[i] == PipeNetworkSleepState.AmbientOnly)
			{
				amb++;
			}
			else
			{
				quiet++;
			}
		}
		sb.AppendLine(
			$"[RimPipe] map schema={rimPipeSchemaVersion} members={members.Count} pipes={pipeCells.Count} mappings={mappings.Count} " +
			$"chem={chemReactors.Count} pending={pendingDeltas.Count} nets={netCount} sleepState={SleepState} " +
			$"(busy={busy} amb={amb} quiet={quiet}) overlay={showFlowPressureOverlay} lastTopoMs={LastTopologyRebuildMs:F3}");
		for (int n = 0; n < sleepStates.Length; n++)
		{
			sb.AppendLine($"  Net#{n} state={sleepStates[n]}");
		}
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember m = members[i];
			CompPipeBreachable? br = m.parent.TryGetComp<CompPipeBreachable>();
			string breach = br != null && br.Breached ? " breached" : "";
			sb.AppendLine($"  Member {m.parent.LabelCap}@{m.parent.Position}{breach}");
			for (int c = 0; c < m.Containers.Count; c++)
			{
				sb.AppendLine($"    {m.Containers[c]}");
			}
		}
		for (int i = 0; i < pipeCells.Count; i++)
		{
			CompPipeCell p = pipeCells[i];
			sb.AppendLine($"  Pipe@{p.parent.Position} breached={p.Breached}");
		}
		for (int i = 0; i < mappings.Count; i++)
		{
			sb.AppendLine($"  {mappings[i]} incomplete={mappings[i].IsIncomplete}");
		}
		for (int i = 0; i < chemReactors.Count; i++)
		{
			ChemReactorBinding b = chemReactors[i];
			string rx = b.reaction != null ? b.reaction.defName : "null";
			sb.AppendLine(
				$"  Chem#{i} {rx} on={b.enabled} mix={b.mixRatio:0.###} η={b.lastEfficiency:0.###} " +
				$"lastN={b.lastBatchN:0.##} in={b.inputs.Count} out={b.outputs.Count}");
		}
		return sb.ToString();
	}

	/// <summary>调试：按容量比例填液；薄包装 → TrySetAmount。</summary>
	public void DebugFillContainer(Container c, float fraction)
	{
		if (c == null)
		{
			return;
		}
		TrySetAmount(c, c.capacity * fraction);
	}

	/// <summary>某构件所属主网 ID（取首个 Container）；无则 -1。</summary>
	public int GetMemberNetId(CompPipeNetworkMember mem)
	{
		if (mem?.Containers == null || mem.Containers.Count == 0)
		{
			return -1;
		}
		return mem.Containers[0].netId;
	}

	private void DrawFlowPressureOverlay()
	{
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.parent == null || !mem.parent.Spawned || mem.Containers.Count == 0)
			{
				continue;
			}
			float p = ClampPressure01(mem.Containers[0].pressure);
			// colorPct：0=绿 … 0.5=蓝 … 1≈红（与 DebugCellDrawer / CellRenderer 约定一致）
			// 不用 FlashCell：它会往 debugDrawer 列表里 Add，Update 频率高于 Tick 时列表膨胀。
			CellRenderer.RenderCell(mem.parent.Position, p);
		}

		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.mappingType != MappingType.Flow || m.IsIncomplete
				|| m.containerA?.owner?.parent == null || m.containerB?.owner?.parent == null)
			{
				continue;
			}
			if (m.lastBatchWant <= FlowSolver.AmountEpsilon)
			{
				continue;
			}
			IntVec3 a = m.containerA.owner.parent.Position;
			IntVec3 b = m.containerB.owner.parent.Position;
			Vector3 from = (m.lastBatchFromA ? a : b).ToVector3Shifted();
			Vector3 to = (m.lastBatchFromA ? b : a).ToVector3Shifted();
			float width = 0.05f + System.Math.Min(m.lastBatchWant / 20f, 1f) * 0.25f;
			GenDraw.DrawLineBetween(from, to, SimpleColor.Cyan, width);
		}
	}

	/// <summary>
	/// 近距才画 P= 标签（对齐官方 DebugDrawerOnGUI）。不经 FlashCell，避免列表膨胀。
	/// </summary>
	private void DrawFlowPressureOverlayLabels()
	{
		if (Find.CameraDriver.CurrentZoom != CameraZoomRange.Closest)
		{
			return;
		}
		Text.Font = GameFont.Tiny;
		Text.Anchor = TextAnchor.MiddleCenter;
		GUI.color = new Color(1f, 1f, 1f, 0.5f);
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.parent == null || !mem.parent.Spawned || mem.Containers.Count == 0)
			{
				continue;
			}
			float p = ClampPressure01(mem.Containers[0].pressure);
			Vector2 ui = mem.parent.Position.ToUIPosition();
			Rect rect = new Rect(ui.x - 20f, ui.y - 20f, 40f, 40f);
			if (new Rect(0f, 0f, UI.screenWidth, UI.screenHeight).Overlaps(rect))
			{
				Widgets.Label(rect, $"P={p:0.##}");
			}
		}
		GUI.color = Color.white;
		Text.Anchor = TextAnchor.UpperLeft;
	}

	private static float ClampPressure01(float p)
	{
		if (p < 0f)
		{
			return 0f;
		}
		if (p > 1f)
		{
			return 1f;
		}
		return p;
	}
}
