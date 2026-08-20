using System;
using LudeonTK;
using RimWorld;
using Verse;

namespace RimPipe.Debug;

/// <summary>验收套件：5 个 ToolMap，点击原点后按偏移生成场景并跑断言。</summary>
public static class RimPipeDebugSuites
{
	private static (int passed, int total) RunFramework(IntVec3 origin)
	{
		return RunQuiet("R-框架", (map, net, origin) =>
		{
			int passed = 0;
			int total = 0;

			// 1) 干净静网：均量三通 → 休眠进入 → 唤醒（须先于 Busy 场景）
			RimPipeDebugScenes.SpawnOneTeePair(
				map, origin,
				RimPipeDefOf.RimPipe_TeeJunction, RimPipeDefOf.RimPipe_StorageTank,
				RimPipeDefOf.RimPipe_Fluid_TestWater,
				fillL: 0.5f, fillR: 0.5f, fillTee: 0.5f, tag: "静");
			net.RequestFullRebuild();
			net.DebugProcessTopology();
			for (int i = 0; i < 30; i++)
			{
				net.DebugForceOneBatch();
			}
			net.ReevaluateAllNetSleepStates();
			total++;
			if (RimPipeDebugAsserts.AssertSleepEntered())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertSleepWake())
			{
				passed++;
			}

			// 2) 产品三通对称 @ z+6
			RimPipeDebugScenes.SpawnProductTee(map, origin + new IntVec3(0, 0, 6));
			for (int i = 0; i < 20; i++)
			{
				net.DebugForceOneBatch();
			}
			total++;
			if (RimPipeDebugAsserts.AssertSymmetry())
			{
				passed++;
			}

			// 3) Bridge：对产品三通 breachable
			total++;
			if (RimPipeDebugAsserts.AssertBridgeDamageBreach())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertBridgeBreakdownBreach())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertBridgeRepairClearsBreach())
			{
				passed++;
			}

			// 4) 泵 @ z+12
			RimPipeDebugScenes.SpawnProductPumpScene(map, origin + new IntVec3(0, 0, 12));
			total++;
			if (RimPipeDebugAsserts.AssertPumpForcedBatch())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertPumpBlockedBatch())
			{
				passed++;
			}

			// 5) 泵局部重建回归 @ z+18：管—泵—管，拆/重放排出侧管道后逆压差抽送保持
			RimPipeDebugScenes.SpawnPumpPipeLocalScene(map, origin + new IntVec3(0, 0, 18));
			total++;
			if (RimPipeDebugAsserts.AssertPumpDriveAfterLocalRebuild())
			{
				passed++;
			}

			return (passed, total);
		}, origin);
	}

	private static (int passed, int total) RunPhysics(IntVec3 origin)
	{
		return RunQuiet("R-物理", (map, net, origin) =>
		{
			int passed = 0;
			int total = 0;

			RimPipeDebugScenes.SpawnPressureUnequalScene(map, origin);
			total++;
			if (RimPipeDebugAsserts.AssertPressureEqualize())
			{
				passed++;
			}

			RimPipeDebugScenes.SpawnResistanceScene(map, origin + new IntVec3(0, 0, 4));
			total++;
			if (RimPipeDebugAsserts.AssertResistanceBatch())
			{
				passed++;
			}

			RimPipeDebugScenes.SpawnPipeBreachScene(map, origin + new IntVec3(0, 0, 10));
			total++;
			if (RimPipeDebugAsserts.AssertPipeBreachLeakBatch())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertWaterLeakNoFilthBatch())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertDestroyPipeStopsLeak())
			{
				passed++;
			}

			RimPipeDebugScenes.SpawnLeakEffectFuelScene(map, origin + new IntVec3(0, 0, 14));
			total++;
			if (RimPipeDebugAsserts.AssertLeakFilthBatch())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertLeakTemperatureBatch())
			{
				passed++;
			}

			return (passed, total);
		}, origin);
	}

	private static (int passed, int total) RunHeatAmbient(IntVec3 origin)
	{
		return RunQuiet("R-热与环境", (map, net, origin) =>
		{
			int passed = 0;
			int total = 0;

			RimPipeDebugScenes.SpawnHeatExchangerScene(map, origin);
			total++;
			if (RimPipeDebugAsserts.AssertHeatEqualize())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertHeatBlocked())
			{
				passed++;
			}

			RimPipeDebugScenes.SpawnMixingScene(map, origin + new IntVec3(0, 0, 3));
			total++;
			if (RimPipeDebugAsserts.AssertMixing())
			{
				passed++;
			}

			RimPipeDebugScenes.SpawnAmbientHeatScene(map, origin + new IntVec3(0, 0, 8));
			total++;
			if (RimPipeDebugAsserts.AssertInsulationCompare())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertEmptyNoAmbient())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertAmbientHeatLoss())
			{
				passed++;
			}

			return (passed, total);
		}, origin);
	}

	private static (int passed, int total) RunChem(IntVec3 origin)
	{
		return RunQuiet("R-化学", (map, net, origin) =>
		{
			int passed = 0;
			int total = 0;

			RimPipeDebugScenes.SpawnChemP3ReactorScene(map, origin);
			total++;
			if (RimPipeDebugAsserts.AssertChemP3ReactorBatch())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertChemP4RichMix())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertChemP5aConditions())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertChemP5bHeat())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertChemP3ReactorBlocked())
			{
				passed++;
			}

			return (passed, total);
		}, origin);
	}

	private static (int passed, int total) RunExt(IntVec3 origin)
	{
		return RunQuiet("R-扩展", (map, net, origin) =>
		{
			int passed = 0;
			int total = 0;

			RimPipeDebugScenes.SpawnExtHookBridgeScene(map, origin);
			total++;
			if (RimPipeDebugAsserts.AssertExtHookInternalEdge())
			{
				passed++;
			}

			RimPipeDebugScenes.SpawnBridgeHScene(map, origin + new IntVec3(0, 0, 12));
			total++;
			if (RimPipeDebugAsserts.AssertBridgeHInjected())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertBridgeHDamageBreach())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertBridgeHBreakdownBreach())
			{
				passed++;
			}
			total++;
			if (RimPipeDebugAsserts.AssertBridgeHRepairClearsBreach())
			{
				passed++;
			}

			RimPipeDebugScenes.SpawnSplitNetSleepScene(map, origin + new IntVec3(0, 0, 6));
			total++;
			if (RimPipeDebugAsserts.AssertSplitNetSleep())
			{
				passed++;
			}

			return (passed, total);
		}, origin);
	}

	private static (int passed, int total) RunChannel(IntVec3 origin)
	{
		return RunQuiet("R-通道", (map, net, origin) =>
		{
			int passed = 0;
			int total = 0;

			// 1) 双通道十字隔离：A={E,W} B={N,S}，东西/南北各自均压、跨线不相连
			RimPipeDebugScenes.SpawnChannelCrossScene(map, origin);
			total++;
			if (RimPipeDebugAsserts.AssertChannelCross())
			{
				passed++;
			}
			// 2) 方向断开/恢复：把东方向关掉再开回
			total++;
			if (RimPipeDebugAsserts.AssertChannelBreakRestore())
			{
				passed++;
			}

			// 3) 粘度：水(v=1) 比稠液(v=2) 流得快
			RimPipeDebugScenes.SpawnViscosityScene(map, origin + new IntVec3(0, 0, 4));
			total++;
			if (RimPipeDebugAsserts.AssertViscosity())
			{
				passed++;
			}

			// 4) 比热：c=1 腔温变 ≈ 2× c=2 腔
			RimPipeDebugScenes.SpawnSpecificHeatScene(map, origin + new IntVec3(0, 0, 8));
			total++;
			if (RimPipeDebugAsserts.AssertSpecificHeat())
			{
				passed++;
			}

			return (passed, total);
		}, origin);
	}


	[DebugAction("RimPipe", "R-框架", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteFramework()
	{
		RunFramework(UI.MouseCell());
	}

	[DebugAction("RimPipe", "R-物理", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuitePhysics()
	{
		RunPhysics(UI.MouseCell());
	}

	[DebugAction("RimPipe", "R-热与环境", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteHeatAmbient()
	{
		RunHeatAmbient(UI.MouseCell());
	}

	[DebugAction("RimPipe", "R-化学", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteChem()
	{
		RunChem(UI.MouseCell());
	}

	[DebugAction("RimPipe", "R-扩展", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteExt()
	{
		RunExt(UI.MouseCell());
	}

	[DebugAction("RimPipe", "R-通道", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteChannel()
	{
		RunChannel(UI.MouseCell());
	}

	[DebugAction("RimPipe", "R-全部", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteAll()
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (map == null || net == null)
		{
			return;
		}
		IntVec3 origin = UI.MouseCell();
		bool prevQuiet = MapComponent_PipeNetwork.QuietDebugLogs;
		MapComponent_PipeNetwork.QuietDebugLogs = true;
		try
		{
			int passed = 0;
			int total = 0;

			(int p, int t) r;
			r = RunFramework(origin); passed += r.p; total += r.t;
			r = RunPhysics(origin + new IntVec3(0, 0, 30)); passed += r.p; total += r.t;
			r = RunHeatAmbient(origin + new IntVec3(0, 0, 60)); passed += r.p; total += r.t;
			r = RunChem(origin + new IntVec3(0, 0, 90)); passed += r.p; total += r.t;
			r = RunExt(origin + new IntVec3(0, 0, 120)); passed += r.p; total += r.t;
			r = RunChannel(origin + new IntVec3(0, 0, 150)); passed += r.p; total += r.t;

			RimPipeDebugUtil.ReportSuite("R-全部", passed, total);
			Messages.Message($"[RimPipe] R-全部：通过 {passed}/{total}", MessageTypeDefOf.TaskCompletion, historical: false);
		}
		finally
		{
			MapComponent_PipeNetwork.QuietDebugLogs = prevQuiet;
		}
	}

	private static (int passed, int total) RunQuiet(
		string name,
		Func<Map, MapComponent_PipeNetwork, IntVec3, (int passed, int total)> body,
		IntVec3 origin)
	{
		Map map = Find.CurrentMap;
		MapComponent_PipeNetwork? net = map?.GetComponent<MapComponent_PipeNetwork>();
		if (map == null || net == null)
		{
			return (0, 0);
		}
		bool prevQuiet = MapComponent_PipeNetwork.QuietDebugLogs;
		MapComponent_PipeNetwork.QuietDebugLogs = true;
		try
		{
			(int passed, int total) = body(map, net, origin);
			RimPipeDebugUtil.ReportSuite(name, passed, total);
			Messages.Message($"[RimPipe] {name}：通过 {passed}/{total}", MessageTypeDefOf.TaskCompletion, historical: false);
			return (passed, total);
		}
		finally
		{
			MapComponent_PipeNetwork.QuietDebugLogs = prevQuiet;
		}
	}
}
