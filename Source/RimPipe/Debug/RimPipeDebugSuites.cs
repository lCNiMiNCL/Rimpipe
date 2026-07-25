using System;
using LudeonTK;
using RimWorld;
using Verse;

namespace RimPipe.Debug;

/// <summary>验收套件：5 个 ToolMap，点击原点后按偏移生成场景并跑断言。</summary>
public static class RimPipeDebugSuites
{
	[DebugAction("RimPipe", "R-框架", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteFramework()
	{
		RunQuiet("R-框架", (map, net, origin) =>
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

			return (passed, total);
		});
	}

	[DebugAction("RimPipe", "R-物理", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuitePhysics()
	{
		RunQuiet("R-物理", (map, net, origin) =>
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
		});
	}

	[DebugAction("RimPipe", "R-热与环境", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteHeatAmbient()
	{
		RunQuiet("R-热与环境", (map, net, origin) =>
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
		});
	}

	[DebugAction("RimPipe", "R-化学", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteChem()
	{
		RunQuiet("R-化学", (map, net, origin) =>
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
		});
	}

	[DebugAction("RimPipe", "R-扩展", false, false, false, false, false, 0, false,
		actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static void SuiteExt()
	{
		RunQuiet("R-扩展", (map, net, origin) =>
		{
			int passed = 0;
			int total = 0;

			RimPipeDebugScenes.SpawnExtHookBridgeScene(map, origin);
			total++;
			if (RimPipeDebugAsserts.AssertExtHookInternalEdge())
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
		});
	}

	private static void RunQuiet(string name, Func<Map, MapComponent_PipeNetwork, IntVec3, (int passed, int total)> body)
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
			(int passed, int total) = body(map, net, origin);
			RimPipeDebugUtil.ReportSuite(name, passed, total);
			Messages.Message($"[RimPipe] {name}：通过 {passed}/{total}", MessageTypeDefOf.TaskCompletion, historical: false);
		}
		finally
		{
			MapComponent_PipeNetwork.QuietDebugLogs = prevQuiet;
		}
	}
}
