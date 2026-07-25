using LudeonTK;
using RimWorld;
using Verse;

namespace RimPipe.Debug;

/// <summary>DevMode 调试菜单（RimPipe）日常工具（含 Benchmark / Stress）。</summary>
public static class RimPipeDebugTools
{
	[DebugAction("RimPipe", "探测（Ping）", false, false, false, false, false, 0, false,
		actionType = DebugActionType.Action,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void Ping()
	{
		Log.Message("[RimPipe] DevMode 探测成功。");
		Messages.Message("[RimPipe] DevMode 探测成功。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	[DebugAction("RimPipe", "转储管网状态", false, false, false, false, false, 0, false,
		actionType = DebugActionType.Action,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void DumpNetwork()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			Log.Warning("[RimPipe] 当前地图没有 MapComponent_PipeNetwork。");
			return;
		}
		Log.Message(net.Dump());
	}

	[DebugAction("RimPipe", "强制执行一批（Accumulate+Commit）", false, false, false, false, false, 0, false,
		actionType = DebugActionType.Action,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void ForceOneBatch()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return;
		}
		net.DebugForceOneBatch();
		Log.Message("[RimPipe] 已强制执行一批 Accumulate+Commit。\n" + net.Dump());
		// 有三通则顺带对称断言；跳过/失败不额外打断
		RimPipeDebugAsserts.AssertSymmetry(net);
	}

	[DebugAction("RimPipe", "选中容器灌至 80%", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void FillSelected()
	{
		foreach (Thing t in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()))
		{
			if (t is not ThingWithComps twc)
			{
				continue;
			}
			CompPipeNetworkMember? comp = twc.GetComp<CompPipeNetworkMember>();
			if (comp == null || comp.Containers.Count == 0)
			{
				continue;
			}
			MapComponent_PipeNetwork net = Find.CurrentMap.GetComponent<MapComponent_PipeNetwork>();
			if (comp.Containers[0].fluid == null)
			{
				comp.Containers[0].fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
			}
			net.DebugFillContainer(comp.Containers[0], 0.8f);
			Log.Message($"[RimPipe] 已灌满 {t.LabelCap} → {comp.Containers[0]}");
			return;
		}
	}

	[DebugAction("RimPipe", "选中容器抽空", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void EmptySelected()
	{
		foreach (Thing t in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()))
		{
			if (t is not ThingWithComps twc)
			{
				continue;
			}
			CompPipeNetworkMember? comp = twc.GetComp<CompPipeNetworkMember>();
			if (comp == null || comp.Containers.Count == 0)
			{
				continue;
			}
			Find.CurrentMap.GetComponent<MapComponent_PipeNetwork>().DebugFillContainer(comp.Containers[0], 0f);
			Log.Message($"[RimPipe] 已抽空 {t.LabelCap}");
			return;
		}
	}

	[DebugAction("RimPipe", "切换流量/压力 Overlay", false, false, false, false, false, 0, false,
		actionType = DebugActionType.Action,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void ToggleFlowPressureOverlay()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return;
		}
		net.showFlowPressureOverlay = !net.showFlowPressureOverlay;
		string on = net.showFlowPressureOverlay ? "开" : "关";
		Log.Message($"[RimPipe] 流量/压力 Overlay={on}（仅 DevMode；看上批流量，最多滞后约一批）");
		Messages.Message($"[RimPipe] Overlay {on}", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	[DebugAction("RimPipe", "标记开口泄漏（两端泄量）", false, false, false, false, false, 0, false,
		actionType = DebugActionType.Action,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void MarkLeakOpen()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		net?.DebugMarkAllLeakOpen();
		Log.Message("[RimPipe] 已标记开口泄漏：全部 Mapping.leakOpen（两端各按 rate/2 销毁，下次 Commit）。");
	}

	[DebugAction("RimPipe", "计时整图重建", false, false, false, false, false, 0, false,
		actionType = DebugActionType.Action,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void TimedFullRebuild()
	{
		MapComponent_PipeNetwork? net = Find.CurrentMap?.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return;
		}
		net.DebugTimedFullRebuild();
		string msg =
			$"[RimPipe] 计时整图重建：构件={net.Members.Count} 管={net.PipeCells.Count} " +
			$"Mapping={net.Mappings.Count} nets={net.NetCount} 耗时={net.LastTopologyRebuildMs:F3}ms";
		Log.Message(msg);
		Messages.Message(msg, MessageTypeDefOf.TaskCompletion, historical: false);
	}

	[DebugAction("RimPipe", "生成 Stress 管网（~100罐/~500管）", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SpawnStress()
	{
		Map map = Find.CurrentMap;
		if (map == null)
		{
			return;
		}
		RimPipeDebugScenes.SpawnStressGrid(map, UI.MouseCell());
	}
}
