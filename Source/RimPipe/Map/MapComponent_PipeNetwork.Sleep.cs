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
		batchCachesDirty = true;
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
			batchCachesDirty = true;
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

	private void EnsureReevalCapacity()
	{
		if (reevalBusy.Length >= netCount && reevalAmb.Length >= netCount)
		{
			return;
		}
		reevalBusy = new bool[netCount];
		reevalAmb = new bool[netCount];
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

	/// <summary>netId 越界按 Busy 处理（对齐 <see cref="GetNetSleepState"/> 的回退）。</summary>
	private bool IsNetBusy(int netId)
	{
		if (netId < 0 || netId >= sleepStates.Length)
		{
			return true;
		}
		return sleepStates[netId] == PipeNetworkSleepState.Busy;
	}

	/// <summary>netId 越界按 Busy 处理（对齐 <see cref="GetNetSleepState"/> 的回退）。</summary>
	private bool IsNetLightCommit(int netId)
	{
		if (netId < 0 || netId >= sleepStates.Length)
		{
			return true;
		}
		PipeNetworkSleepState s = sleepStates[netId];
		return s == PipeNetworkSleepState.Busy || s == PipeNetworkSleepState.AmbientOnly;
	}
	public void ReevaluateAllNetSleepStates()
	{
		EnsureSleepStatesCapacity();
		if (netCount <= 0)
		{
			sleepStates = System.Array.Empty<PipeNetworkSleepState>();
			batchCachesDirty = true;
			return;
		}
		EnsureReevalCapacity();
		System.Array.Clear(reevalBusy, 0, netCount);
		System.Array.Clear(reevalAmb, 0, netCount);

		// 单遍聚合：一次扫完，按 netId 落 per-net 标志，替代原来的「每网各扫一遍全图」
		// （nets × 全图迭代，且 GetTemperatureForCell 每网每成员重复查温）。
		// 1) Flow/Heat want 与泄漏/不完整驱动
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			int n = m.netId;
			if (n < 0 || n >= netCount)
			{
				continue;
			}
			if (m.mappingType == MappingType.Flow)
			{
				if (m.leakOpen || m.IsIncomplete)
				{
					reevalBusy[n] = true;
					continue;
				}
				if (reevalBusy[n])
				{
					continue; // 本网已定 Busy，不再算 want（对齐原逐网早退）
				}
				float want = FlowSolver.ComputeWant(m, out _, out _, out _);
				if (want > FlowSolver.AmountEpsilon)
				{
					reevalBusy[n] = true;
				}
			}
			else if (m.mappingType == MappingType.Heat)
			{
				if (m.IsIncomplete || m.containerA == null || m.containerB == null)
				{
					continue;
				}
				if (reevalBusy[n])
				{
					continue;
				}
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
					reevalBusy[n] = true;
				}
			}
		}

		// 2) 构件：破损驱动 + 环境散热需求（GetTemperatureForCell 每个构件只查一次）
		for (int i = 0; i < members.Count; i++)
		{
			CompPipeNetworkMember mem = members[i];
			if (mem?.parent == null || !mem.parent.Spawned)
			{
				continue;
			}
			CompPipeBreachable? br = mem.parent.TryGetComp<CompPipeBreachable>();
			bool breached = br != null && br.Breached;
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
			bool ambEnabled = maxRate > FlowSolver.AmountEpsilon && leakFactor > FlowSolver.AmountEpsilon;
			float tamb = ambEnabled ? GenTemperature.GetTemperatureForCell(mem.parent.Position, map) : 0f;
			for (int c = 0; c < mem.Containers.Count; c++)
			{
				Container cont = mem.Containers[c];
				if (cont == null)
				{
					continue;
				}
				int n = cont.netId;
				if (n < 0 || n >= netCount)
				{
					continue;
				}
				if (breached && cont.amount > FlowSolver.AmountEpsilon)
				{
					reevalBusy[n] = true;
				}
				if (ambEnabled && cont.amount > FlowSolver.AmountEpsilon
					&& System.Math.Abs(cont.temperature - tamb) > HeatSolver.TempEpsilon)
				{
					reevalAmb[n] = true;
				}
			}
		}

		// 3) 破损管格：凡路径 Mapping 属某网即驱动该网 Busy
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
				Mapping m = list[j];
				if (m != null && m.netId >= 0 && m.netId < netCount)
				{
					reevalBusy[m.netId] = true;
				}
			}
		}

		// 4) 化学：绑定可转化则其所触及的所有网 Busy
		for (int i = 0; i < chemReactors.Count; i++)
		{
			ChemReactorBinding b = chemReactors[i];
			if (b == null || b.reaction == null)
			{
				continue;
			}
			float n = ChemSolver.ComputeBatchCount(
				b.reaction, b.inputs, b.outputs, b.enabled, b.mixRatio, out _, out _);
			if (n <= FlowSolver.AmountEpsilon)
			{
				continue;
			}
			for (int k = 0; k < b.inputs.Count; k++)
			{
				MarkBusyIfValidNet(b.inputs[k]);
			}
			for (int k = 0; k < b.outputs.Count; k++)
			{
				MarkBusyIfValidNet(b.outputs[k]);
			}
		}

		for (int n = 0; n < netCount; n++)
		{
			if (reevalBusy[n])
			{
				sleepStates[n] = PipeNetworkSleepState.Busy;
			}
			else if (reevalAmb[n])
			{
				sleepStates[n] = PipeNetworkSleepState.AmbientOnly;
			}
			else
			{
				sleepStates[n] = PipeNetworkSleepState.FullyQuiet;
			}
		}
		batchCachesDirty = true;
	}

	private void MarkBusyIfValidNet(Container? c)
	{
		if (c == null)
		{
			return;
		}
		int n = c.netId;
		if (n >= 0 && n < netCount)
		{
			reevalBusy[n] = true;
		}
	}
}
