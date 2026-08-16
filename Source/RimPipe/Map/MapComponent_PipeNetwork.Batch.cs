using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using RimPipe.Debug;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe;

/// <summary>
/// 整张地图的管网总管（partial 拆分版）。主文件保留状态/生命周期，职责见各 partial。
/// </summary>
public partial class MapComponent_PipeNetwork : MapComponent
{
	private readonly List<(int Index, float Delta)> chemPureDeltaScratch = new List<(int Index, float Delta)>();

	/// <summary>
	/// 批级缓存惰性重建：Busy Flow / Busy Heat 边、light Flow 边、light 构件。
	/// 批内休眠态恒定，因此只在失效点（拓扑重建 / 休眠定态 / 唤醒 / 注册注销）置脏后重建，
	/// 各批入口先确保新鲜，省掉每批对全图 mappings / members 的多次扫描。
	/// </summary>
	private void EnsureBatchCachesFresh()
	{
		if (!batchCachesDirty)
		{
			return;
		}
		batchCachesDirty = false;
		busyFlowMappings.Clear();
		busyHeatMappings.Clear();
		lightFlowMappings.Clear();
		lightCommitMembers.Clear();
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.mappingType == MappingType.Flow)
			{
				if (IsNetBusy(m.netId))
				{
					busyFlowMappings.Add(m);
				}
				if (IsNetLightCommit(m.netId))
				{
					lightFlowMappings.Add(m);
				}
			}
			else if (m.mappingType == MappingType.Heat && IsNetBusy(m.netId))
			{
				busyHeatMappings.Add(m);
			}
		}
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.Containers == null || mem.Containers.Count == 0)
			{
				continue;
			}
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				Container? cont = mem.Containers[c];
				if (cont != null && IsNetLightCommit(cont.netId))
				{
					lightCommitMembers.Add(mem);
					break;
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
		EnsureBatchCachesFresh();
		// 批进入时本批字段必然已清（上一批 Commit 已全量 ClearBatch），只需清缓存内 Busy 边。
		for (int i = 0; i < busyFlowMappings.Count; i++)
		{
			busyFlowMappings[i].ClearBatch();
		}

		// 虚拟量只种子 Busy 边两端；非 Busy 容器本批不会动，无需进表（等价旧实现的全量种子）。
		flowVirtual.Clear();
		for (int i = 0; i < busyFlowMappings.Count; i++)
		{
			Mapping m = busyFlowMappings[i];
			if (m.containerA != null)
			{
				flowVirtual[m.containerA] = m.containerA.amount;
			}
			if (m.containerB != null)
			{
				flowVirtual[m.containerB] = m.containerB.amount;
			}
		}

		for (int iter = 0; iter < FlowRelaxIterations; iter++)
		{
			flowWants.Clear();
			for (int i = 0; i < busyFlowMappings.Count; i++)
			{
				Mapping m = busyFlowMappings[i];
				if (m.IsIncomplete || m.leakOpen || m.containerA == null || m.containerB == null)
				{
					continue;
				}
				float amountA = flowVirtual[m.containerA];
				float amountB = flowVirtual[m.containerB];
				float want = FlowSolver.ComputeWant(m, amountA, amountB, out Container? src, out Container? tgt, out _);
				if (want > FlowSolver.AmountEpsilon && src != null && tgt != null)
				{
					flowWants.Add((m, src, tgt, want));
				}
			}

			flowOutSum.Clear();
			flowInSum.Clear();
			for (int i = 0; i < flowWants.Count; i++)
			{
				flowOutSum.TryGetValue(flowWants[i].src, out float o);
				flowOutSum[flowWants[i].src] = o + flowWants[i].want;
				flowInSum.TryGetValue(flowWants[i].tgt, out float inn);
				flowInSum[flowWants[i].tgt] = inn + flowWants[i].want;
			}

			// 本轮净增量（相对本轮初值），全部算完再写回
			flowStepDelta.Clear();
			for (int i = 0; i < flowWants.Count; i++)
			{
				Mapping m = flowWants[i].map;
				Container src = flowWants[i].src;
				Container tgt = flowWants[i].tgt;
				float w = flowWants[i].want;
				float srcAmt = flowVirtual[src];
				float tgtAmt = flowVirtual[tgt];
				if (flowOutSum.TryGetValue(src, out float os) && os > srcAmt + FlowSolver.AmountEpsilon)
				{
					w *= srcAmt / os;
				}
				float free = tgt.capacity - tgtAmt;
				if (flowInSum.TryGetValue(tgt, out float ins) && ins > free + FlowSolver.AmountEpsilon)
				{
					w *= free / ins;
				}
				w *= FlowRelaxFactor;
				if (w <= FlowSolver.AmountEpsilon)
				{
					continue;
				}
				flowStepDelta.TryGetValue(src, out float ds);
				flowStepDelta[src] = ds - w;
				flowStepDelta.TryGetValue(tgt, out float dt);
				flowStepDelta[tgt] = dt + w;
				m.batchSource = src;
				m.batchTarget = tgt;
				m.batchWant += w;
			}

			foreach (KeyValuePair<Container, float> kv in flowStepDelta)
			{
				float next = flowVirtual[kv.Key] + kv.Value;
				if (next < 0f)
				{
					next = 0f;
				}
				if (next > kv.Key.capacity)
				{
					next = kv.Key.capacity;
				}
				flowVirtual[kv.Key] = next;
			}
		}

		foreach (KeyValuePair<Container, float> kv in flowVirtual)
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
		EnsureBatchCachesFresh();
		for (int i = 0; i < busyHeatMappings.Count; i++)
		{
			busyHeatMappings[i].ClearBatch();
		}

		heatVirtual.Clear();
		for (int i = 0; i < busyHeatMappings.Count; i++)
		{
			Mapping m = busyHeatMappings[i];
			if (m.containerA != null)
			{
				heatVirtual[m.containerA] = m.containerA.temperature;
			}
			if (m.containerB != null)
			{
				heatVirtual[m.containerB] = m.containerB.temperature;
			}
		}

		for (int iter = 0; iter < HeatRelaxIterations; iter++)
		{
			heatWants.Clear();
			for (int i = 0; i < busyHeatMappings.Count; i++)
			{
				Mapping m = busyHeatMappings[i];
				if (m.IsIncomplete || m.leakOpen || m.containerA == null || m.containerB == null)
				{
					continue;
				}
				float q = HeatSolver.ComputeHeatWant(
					m,
					m.containerA.amount,
					m.containerB.amount,
					heatVirtual[m.containerA],
					heatVirtual[m.containerB],
					out Container? hot,
					out Container? cold);
				if (q > FlowSolver.AmountEpsilon && hot != null && cold != null)
				{
					heatWants.Add((m, hot, cold, q));
				}
			}

			heatStepDelta.Clear();
			for (int i = 0; i < heatWants.Count; i++)
			{
				Mapping m = heatWants[i].map;
				Container hot = heatWants[i].hot;
				Container cold = heatWants[i].cold;
				float q = heatWants[i].q * HeatRelaxFactor;
				float mHot = hot.amount;
				float mCold = cold.amount;
				if (mHot <= FlowSolver.AmountEpsilon || mCold <= FlowSolver.AmountEpsilon)
				{
					continue;
				}
				float cHot = HeatSolver.SpecificHeatOf(hot);
				float cCold = HeatSolver.SpecificHeatOf(cold);
				float dHot = -q / (mHot * cHot);
				float dCold = q / (mCold * cCold);
				heatStepDelta.TryGetValue(hot, out float dh);
				heatStepDelta[hot] = dh + dHot;
				heatStepDelta.TryGetValue(cold, out float dc);
				heatStepDelta[cold] = dc + dCold;
				m.batchSource = hot;
				m.batchTarget = cold;
				m.batchWant += q;
			}

			foreach (KeyValuePair<Container, float> kv in heatStepDelta)
			{
				heatVirtual[kv.Key] = heatVirtual[kv.Key] + kv.Value;
			}
		}

		foreach (KeyValuePair<Container, float> kv in heatVirtual)
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

	/// <summary>
	/// 按当前量重算 n，按 mixRatio/η 扣入加出；P5b：反应热写入 pendingTempDeltas（ΔT=Q/m）。
	/// perIn 用 chemPerInScratch 复算一次并透传给 BuildAmountDeltas，避免同批双算/双分配。
	/// </summary>
	private void CommitChem()
	{
		for (int i = 0; i < chemReactors.Count; i++)
		{
			ChemReactorBinding b = chemReactors[i];
			if (b.reaction == null)
			{
				continue;
			}
			// 每个反应釜每批只构建一次纯 spec 与容器状态快照，避免 ComputeBatchCount/BuildAmountDeltas 重复分配。
			ChemReactionSpec spec = b.reaction.GetChemSpec();
			List<ChemContainerState> inputStates = ChemSolver.ToStates(b.inputs);
			List<ChemContainerState> outputStates = ChemSolver.ToStates(b.outputs);
			float n = ChemSolver.ComputeBatchCount(
				spec, inputStates, outputStates, b.enabled, b.mixRatio,
				out float efficiency, out _, chemPerInScratch);
			b.lastBatchN = n;
			b.lastEfficiency = efficiency;
			if (n <= FlowSolver.AmountEpsilon)
			{
				continue;
			}
			ChemSolver.BuildAmountDeltas(
				spec, b.inputs, b.outputs, n, b.mixRatio, efficiency, chemDeltaScratch, chemPerInScratch, chemPureDeltaScratch);
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
		float dT = q / (tgt.amount * HeatSolver.SpecificHeatOf(tgt));
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
		EnsureBatchCachesFresh();
		leakWants.Clear();
		// 只迭代 light（Busy∪AmbientOnly）网的 Flow 边；leakOpen / IsIncomplete 仍逐边读，语义不变。
		for (int i = 0; i < lightFlowMappings.Count; i++)
		{
			Mapping m = lightFlowMappings[i];
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

		// 只迭代含 light 网容器的构件；逐容器仍保留网态检查（同构件容器可能分属不同网）。
		for (int i = 0; i < lightCommitMembers.Count; i++)
		{
			CompPipeNetworkMember mem = lightCommitMembers[i];
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

		leakSum.Clear();
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
		EnsureBatchCachesFresh();
		// 只有 Busy 网会产生 batchWant；缓存内全为 Flow 边，等价原「全扫 + 类型/网态过滤」。
		for (int i = 0; i < busyFlowMappings.Count; i++)
		{
			Mapping m = busyFlowMappings[i];
			if (m.batchWant <= FlowSolver.AmountEpsilon
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
		EnsureBatchCachesFresh();
		// 只迭代含 light 网容器的构件（FullyQuiet 网成员不进场）；逐容器仍保留网态检查。
		for (int i = 0; i < lightCommitMembers.Count; i++)
		{
			CompPipeNetworkMember mem = lightCommitMembers[i];
			if (mem?.parent == null || !mem.parent.Spawned)
			{
				continue;
			}
			IntVec3 cell = mem.parent.Position;
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
			// 查格温偏贵，放到「保温/速率可用」过滤之后再查（空罐/FullyQuiet 网不再白查）
			float tamb = GenTemperature.GetTemperatureForCell(cell, map);

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
				// 驱动上限也换热容（|ΔT|/2·m·c），保证收敛速度与比热无关；再乘 (1-insulation) 拉开保温差距
				float spHeat = HeatSolver.SpecificHeatOf(cont);
				float qBase = System.Math.Abs(dT) / 2f * m * spHeat;
				if (qBase > maxRate)
				{
					qBase = maxRate;
				}
				float qWant = qBase * leakFactor;
				if (qWant <= FlowSolver.AmountEpsilon)
				{
					continue;
				}
				float tNew = cont.temperature - (dT > 0f ? 1f : -1f) * qWant / (m * spHeat);
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

}
