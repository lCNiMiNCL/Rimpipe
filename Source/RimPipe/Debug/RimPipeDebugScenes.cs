using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimPipe.Debug;

/// <summary>验收场景生成（无 DebugAction；由 Tools/Suites 传入 origin）。</summary>
internal static class RimPipeDebugScenes
{
	internal static void SpawnProductTee(Map map, IntVec3 origin)
	{
		SpawnTeeScene(map, origin, RimPipeDefOf.RimPipe_TeeJunction, RimPipeDefOf.RimPipe_StorageTank, "产品");
	}

	internal static void SpawnTeeScene(Map map, IntVec3 origin, ThingDef teeDef, ThingDef tankDef, string tag)
	{
		IntVec3 teePos = origin;
		IntVec3 tankL = origin + IntVec3.West;
		IntVec3 tankR = origin + IntVec3.East;

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, teePos);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building tee = (Building)GenSpawn.Spawn(teeDef, teePos, map, Rot4.North);
		Building left = (Building)GenSpawn.Spawn(tankDef, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(tankDef, tankR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember ct = tee.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		if (cl.Containers.Count > 0)
		{
			cl.Containers[0].fluid = fluid;
			net.DebugFillContainer(cl.Containers[0], 0.8f);
		}
		if (cr.Containers.Count > 0)
		{
			cr.Containers[0].fluid = fluid;
			net.DebugFillContainer(cr.Containers[0], 0.8f);
		}
		if (ct.Containers.Count > 0)
		{
			ct.Containers[0].fluid = fluid;
			net.DebugFillContainer(ct.Containers[0], 0f);
		}

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message($"[RimPipe] 已生成{tag}三通场景 @ {origin}。左={cl.Containers[0].amount} 通={ct.Containers[0].amount} 右={cr.Containers[0].amount}");
		Messages.Message($"[RimPipe] {tag}三通验收场景已生成。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnProductValveScene(Map map, IntVec3 origin)
	{
		IntVec3 valvePos = origin;
		IntVec3 tankL = origin + IntVec3.West;
		IntVec3 tankR = origin + IntVec3.East;

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, valvePos);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building valve = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Valve, valvePos, map, Rot4.North);
		Building left = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		cl.Containers[0].fluid = fluid;
		cr.Containers[0].fluid = fluid;
		net.DebugFillContainer(cl.Containers[0], 1f);
		net.DebugFillContainer(cr.Containers[0], 0f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		CompPipeValve? v = valve.GetComp<CompPipeValve>();
		Log.Message($"[RimPipe] 阀门场景 @ {origin} 开={v?.IsOpen} rate={v?.EffectiveMaxFlowRate} 左={cl.Containers[0].amount} 右={cr.Containers[0].amount}");
		Messages.Message("[RimPipe] 阀门验收场景已生成（默认开）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnExtHookBridgeScene(Map map, IntVec3 origin)
	{
		IntVec3 bridgePos = origin;
		IntVec3 tankL = origin + IntVec3.West;
		IntVec3 tankR = origin + IntVec3.East;

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, bridgePos);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building bridge = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Dev_InternalBridge, bridgePos, map, Rot4.North);
		Building left = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		cl.Containers[0].fluid = fluid;
		cr.Containers[0].fluid = fluid;
		net.DebugFillContainer(cl.Containers[0], 1f);
		net.DebugFillContainer(cr.Containers[0], 0f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		CompPipeDevInternalBridge? bridgeComp = bridge.GetComp<CompPipeDevInternalBridge>();
		Log.Message(
			$"[RimPipe] ExtHook 场景 @ {origin} 内部边={(bridgeComp?.InternalMapping != null)} " +
			$"rate={bridgeComp?.InternalMapping?.maxFlowRate} 左={cl.Containers[0].amount} 右={cr.Containers[0].amount}");
		Messages.Message("[RimPipe] ExtHook 验收场景已生成（Dev 内部桥）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnProductPumpScene(Map map, IntVec3 origin)
	{
		// 左罐 | 泵 | 右罐；Forced A→B = 西→东 = 左→右
		IntVec3 pumpPos = origin;
		IntVec3 tankL = origin + IntVec3.West;
		IntVec3 tankR = origin + IntVec3.East;

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, pumpPos);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building pump = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pump, pumpPos, map, Rot4.North);
		Building left = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankR, map, Rot4.North);

		CompPipePump? p = pump.GetComp<CompPipePump>();
		if (p != null)
		{
			p.debugForcePowered = true;
			p.IsOpen = true;
		}

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		cl.Containers[0].fluid = fluid;
		cr.Containers[0].fluid = fluid;
		net.DebugFillContainer(cl.Containers[0], 0.2f);
		net.DebugFillContainer(cr.Containers[0], 0.8f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		p?.ApplyRateToInternalMapping();
		Log.Message(
			$"[RimPipe] 泵场景 @ {origin} 开={p?.IsOpen} 电={p?.HasPower} rate={p?.EffectiveMaxFlowRate} " +
			$"左={cl.Containers[0].amount} 右={cr.Containers[0].amount}");
		Messages.Message("[RimPipe] 泵逆均分场景已生成（左20/右80，Forced 左→右）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	/// <summary>
	/// 泵局部重建回归场景：左罐—管—泵—管—右罐（5 格横排），左 20% / 右 80%，泵开+强电。
	/// 用于验证「局部拓扑重建后泵邻接外部边仍被强制」（DirtyTopo 回归）。
	/// </summary>
	internal static void SpawnPumpPipeLocalScene(Map map, IntVec3 origin)
	{
		IntVec3 tankL = origin + new IntVec3(-2, 0, 0);
		IntVec3 pipeL = origin + new IntVec3(-1, 0, 0);
		IntVec3 pumpPos = origin;
		IntVec3 pipeR = origin + new IntVec3(1, 0, 0);
		IntVec3 tankR = origin + new IntVec3(2, 0, 0);

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, pipeL);
		RimPipeDebugUtil.DestroyAt(map, pumpPos);
		RimPipeDebugUtil.DestroyAt(map, pipeR);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building pump = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pump, pumpPos, map, Rot4.North);
		GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, pipeL, map, Rot4.North);
		GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, pipeR, map, Rot4.North);
		Building left = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankR, map, Rot4.North);

		CompPipePump? p = pump.GetComp<CompPipePump>();
		if (p != null)
		{
			p.debugForcePowered = true;
			p.IsOpen = true;
		}

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		cl.Containers[0].fluid = fluid;
		cr.Containers[0].fluid = fluid;
		net.DebugFillContainer(cl.Containers[0], 0.2f);
		net.DebugFillContainer(cr.Containers[0], 0.8f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		p?.ApplyRateToInternalMapping();
		Log.Message(
			$"[RimPipe] 泵局部重建场景 @ {origin} 开={p?.IsOpen} 电={p?.HasPower} rate={p?.EffectiveMaxFlowRate} " +
			$"左={cl.Containers[0].amount} 右={cr.Containers[0].amount}");
		Messages.Message("[RimPipe] 泵局部重建回归场景已生成（左20/右80，管—泵—管）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnPressureUnequalScene(Map map, IntVec3 origin)
	{
		IntVec3 tankL = origin + IntVec3.West;
		IntVec3 tankR = origin;

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building left = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Dev_TankLarge, tankR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		cl.Containers[0].fluid = fluid;
		cr.Containers[0].fluid = fluid;
		net.DebugFillContainer(cl.Containers[0], 1f);
		net.DebugFillContainer(cr.Containers[0], 0f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message(
			$"[RimPipe] 压力异容场景 @ {origin} 左={cl.Containers[0]} 右={cr.Containers[0]}");
		Messages.Message("[RimPipe] 压力异容场景已生成（左100/100 P=1，右0/500 P=0）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnLeakEffectFuelScene(Map map, IntVec3 origin)
	{
		IntVec3 pipePos = origin;
		IntVec3 tankL = origin + IntVec3.West;
		IntVec3 tankR = origin + IntVec3.East;

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, pipePos);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building pipe = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, pipePos, map, Rot4.North);
		Building left = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		FluidDef fuel = RimPipeDefOf.RimPipe_Fluid_TestFuel;
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		cl.Containers[0].fluid = fuel;
		cr.Containers[0].fluid = fuel;
		net.DebugFillContainer(cl.Containers[0], 1f);
		net.DebugFillContainer(cr.Containers[0], 1f);

		CompPipeCell? cell = pipe.GetComp<CompPipeCell>();
		if (cell != null)
		{
			cell.Breached = true;
		}

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message(
			$"[RimPipe] 泄漏效果场景 @ {origin} 流体={fuel.defName} 破损={cell?.Breached} " +
			$"左={cl.Containers[0].amount} 右={cr.Containers[0].amount}");
		Messages.Message("[RimPipe] 泄漏效果场景已生成（燃料+管道破损）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnResistanceScene(Map map, IntVec3 origin)
	{
		const int longPipeCount = 5;

		IntVec3 shortL = origin;
		IntVec3 shortPipe = origin + IntVec3.East;
		IntVec3 shortR = origin + new IntVec3(2, 0, 0);

		IntVec3 longL = origin + new IntVec3(0, 0, 2);
		IntVec3 longR = longL + new IntVec3(longPipeCount + 1, 0, 0);

		RimPipeDebugUtil.DestroyAt(map, shortL);
		RimPipeDebugUtil.DestroyAt(map, shortPipe);
		RimPipeDebugUtil.DestroyAt(map, shortR);
		RimPipeDebugUtil.DestroyAt(map, longL);
		RimPipeDebugUtil.DestroyAt(map, longR);
		for (int i = 1; i <= longPipeCount; i++)
		{
			RimPipeDebugUtil.DestroyAt(map, longL + new IntVec3(i, 0, 0));
		}

		Building tankShortL = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, shortL, map, Rot4.North);
		GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, shortPipe, map, Rot4.North);
		Building tankShortR = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, shortR, map, Rot4.North);

		Building tankLongL = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, longL, map, Rot4.North);
		for (int i = 1; i <= longPipeCount; i++)
		{
			GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, longL + new IntVec3(i, 0, 0), map, Rot4.North);
		}
		Building tankLongR = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, longR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		CompPipeNetworkMember cShortL = tankShortL.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cShortR = tankShortR.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cLongL = tankLongL.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cLongR = tankLongR.GetComp<CompPipeNetworkMember>();
		cShortL.Containers[0].fluid = fluid;
		cShortR.Containers[0].fluid = fluid;
		cLongL.Containers[0].fluid = fluid;
		cLongR.Containers[0].fluid = fluid;
		net.DebugFillContainer(cShortL.Containers[0], 1f);
		net.DebugFillContainer(cShortR.Containers[0], 0f);
		net.DebugFillContainer(cLongL.Containers[0], 1f);
		net.DebugFillContainer(cLongR.Containers[0], 0f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();

		RimPipeDebugAsserts.TryFindResistancePairs(net, out Mapping? shortM, out Mapping? longM, out _, out _, out _, out _);
		Log.Message(
			$"[RimPipe] 阻力场景 @ {origin} 短管 path={shortM?.pathPipeCells} rate={shortM?.maxFlowRate:0.##} " +
			$"长管 path={longM?.pathPipeCells} rate={longM?.maxFlowRate:0.##}");
		Messages.Message("[RimPipe] 阻力验收场景已生成（短1管 / 长5管）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnPipeBreachScene(Map map, IntVec3 origin)
	{
		IntVec3 pipePos = origin;
		IntVec3 tankL = origin + IntVec3.West;
		IntVec3 tankR = origin + IntVec3.East;

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, pipePos);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building pipe = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, pipePos, map, Rot4.North);
		Building left = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		cl.Containers[0].fluid = fluid;
		cr.Containers[0].fluid = fluid;
		net.DebugFillContainer(cl.Containers[0], 1f);
		net.DebugFillContainer(cr.Containers[0], 1f);

		CompPipeCell? cell = pipe.GetComp<CompPipeCell>();
		if (cell != null)
		{
			cell.Breached = true;
		}

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message(
			$"[RimPipe] 泄漏场景 @ {origin} 管道破损={cell?.Breached} 左={cl.Containers[0].amount} 右={cr.Containers[0].amount}");
		Messages.Message("[RimPipe] 泄漏验收场景已生成（管道已破损）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnHeatExchangerScene(Map map, IntVec3 origin)
	{
		RimPipeDebugUtil.DestroyAt(map, origin);

		Building hx = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_HeatExchanger, origin, map, Rot4.North);
		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember mem = hx.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		Container hot = mem.Containers[0];
		Container cold = mem.Containers[1];
		hot.fluid = fluid;
		cold.fluid = fluid;
		net.DebugFillContainer(hot, 1f);
		net.DebugFillContainer(cold, 1f);
		net.DebugSetTemperature(hot, 80f);
		net.DebugSetTemperature(cold, 20f);

		CompPipeHeatExchanger? hxComp = hx.GetComp<CompPipeHeatExchanger>();
		if (hxComp != null)
		{
			hxComp.IsOpen = true;
		}

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message(
			$"[RimPipe] 热量换热器场景 @ {origin} 热侧={hot} 冷侧={cold} 开={hxComp?.IsOpen}");
		Messages.Message("[RimPipe] 热量换热器场景已生成（A=80°C B=20°C，满液）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnMixingScene(Map map, IntVec3 origin)
	{
		IntVec3 tankL = origin + IntVec3.West;
		IntVec3 tankR = origin;

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building left = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		cl.Containers[0].fluid = fluid;
		cr.Containers[0].fluid = fluid;
		net.DebugFillContainer(cl.Containers[0], 1f);
		net.DebugFillContainer(cr.Containers[0], 0f);
		net.DebugSetTemperature(cl.Containers[0], 80f);
		net.DebugSetTemperature(cr.Containers[0], 20f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message(
			$"[RimPipe] 混温场景 @ {origin} 左={cl.Containers[0]} 右={cr.Containers[0]}");
		Messages.Message("[RimPipe] 混温场景已生成（左满80°C，右空20°C，贴脸）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnAmbientHeatScene(Map map, IntVec3 origin)
	{
		IntVec3 lowIns = origin + new IntVec3(-2, 0, 0);
		IntVec3 highIns = origin;
		IntVec3 emptyPos = origin + new IntVec3(2, 0, 0);

		RimPipeDebugUtil.DestroyAt(map, lowIns);
		RimPipeDebugUtil.DestroyAt(map, highIns);
		RimPipeDebugUtil.DestroyAt(map, emptyPos);
		RimPipeDebugUtil.DestroyAt(map, origin + IntVec3.West);
		RimPipeDebugUtil.DestroyAt(map, origin + IntVec3.East);

		Building bare = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Dev_Tank, lowIns, map, Rot4.North);
		Building insulated = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, highIns, map, Rot4.North);
		Building empty = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Dev_Tank, emptyPos, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;

		CompPipeNetworkMember cBare = bare.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cIns = insulated.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cEmpty = empty.GetComp<CompPipeNetworkMember>();

		cBare.Containers[0].fluid = fluid;
		cIns.Containers[0].fluid = fluid;
		cEmpty.Containers[0].fluid = fluid;

		net.DebugFillContainer(cBare.Containers[0], 1f);
		net.DebugFillContainer(cIns.Containers[0], 1f);
		net.DebugFillContainer(cEmpty.Containers[0], 0f);
		net.DebugSetTemperature(cBare.Containers[0], 80f);
		net.DebugSetTemperature(cIns.Containers[0], 80f);
		net.DebugSetTemperature(cEmpty.Containers[0], 80f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();

		float tamb = GenTemperature.GetTemperatureForCell(origin, map);
		Log.Message(
			$"[RimPipe] 环境散热场景 @ {origin} Tamb≈{tamb:0.#} " +
			$"低保温Dev(ins={cBare.Props.insulation:0.##})={cBare.Containers[0]} " +
			$"高保温产品(ins={cIns.Props.insulation:0.##})={cIns.Containers[0]} " +
			$"空罐T={cEmpty.Containers[0].temperature:0.#}");
		Messages.Message(
			"[RimPipe] 环境散热场景已生成（左Dev无保温满80°C / 中产品保温0.7满80°C / 右空80°C）。",
			MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnSplitNetSleepScene(Map map, IntVec3 origin)
	{
		ThingDef teeDef = RimPipeDefOf.RimPipe_TeeJunction;
		ThingDef tankDef = RimPipeDefOf.RimPipe_StorageTank;
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;

		SpawnOneTeePair(map, origin, teeDef, tankDef, fluid, fillL: 1f, fillR: 0f, fillTee: 0f, tag: "甲");
		SpawnOneTeePair(map, origin + new IntVec3(0, 0, 6), teeDef, tankDef, fluid, fillL: 0.5f, fillR: 0.5f, fillTee: 0.5f, tag: "乙");

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		net.RequestFullRebuild();
		net.DebugProcessTopology();
		net.ReevaluateAllNetSleepStates();
		Log.Message($"[RimPipe] 已生成分网休眠场景 @ {origin}（甲压差 / 乙均量）。nets={net.NetCount}");
		Messages.Message($"[RimPipe] 分网休眠场景已生成 nets={net.NetCount}", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	internal static void SpawnOneTeePair(
		Map map,
		IntVec3 center,
		ThingDef teeDef,
		ThingDef tankDef,
		FluidDef fluid,
		float fillL,
		float fillR,
		float fillTee,
		string tag)
	{
		IntVec3 teePos = center;
		IntVec3 tankL = center + IntVec3.West;
		IntVec3 tankR = center + IntVec3.East;
		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, teePos);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building tee = (Building)GenSpawn.Spawn(teeDef, teePos, map, Rot4.North);
		Building left = (Building)GenSpawn.Spawn(tankDef, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(tankDef, tankR, map, Rot4.North);
		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember ct = tee.GetComp<CompPipeNetworkMember>();
		if (cl.Containers.Count > 0)
		{
			cl.Containers[0].fluid = fluid;
			net.DebugFillContainer(cl.Containers[0], fillL);
		}
		if (cr.Containers.Count > 0)
		{
			cr.Containers[0].fluid = fluid;
			net.DebugFillContainer(cr.Containers[0], fillR);
		}
		if (ct.Containers.Count > 0)
		{
			ct.Containers[0].fluid = fluid;
			net.DebugFillContainer(ct.Containers[0], fillTee);
		}
		Log.Message(
			$"[RimPipe] 分网{tag} @ {center} 左={cl.Containers[0].amount:0.#} 通={ct.Containers[0].amount:0.#} 右={cr.Containers[0].amount:0.#}");
	}

	internal static void SpawnChemP3ReactorScene(Map map, IntVec3 origin)
	{
		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		RimPipeDebugUtil.DestroyAt(map, origin);
		net.ClearChemReactors();

		Building reactor = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Dev_Reactor, origin, map, Rot4.North);
		net.DebugProcessTopology();
		CompPipeReactor? rx = reactor.TryGetComp<CompPipeReactor>();
		CompPipeNetworkMember? mem = reactor.TryGetComp<CompPipeNetworkMember>();
		if (rx == null || mem == null || mem.Containers.Count < 3)
		{
			Messages.Message("[RimPipe] 化学 P3 场景失败：缺 Comp。", MessageTypeDefOf.RejectInput, historical: false);
			return;
		}
		rx.debugForcePowered = true;
		rx.IsOpen = true;
		net.TrySetAmount(mem.Containers[0], 50f);
		net.TrySetAmount(mem.Containers[1], 50f);
		net.TrySetAmount(mem.Containers[2], 0f);
		rx.RegisterChemBinding();
		if (!MapComponent_PipeNetwork.QuietDebugLogs) { Log.Message(net.Dump()); }
		Messages.Message(
			"[RimPipe] 化学 P3 Dev 反应釜场景已生成（开+强行有电，LOX50/RP150/Ex0）。",
			MessageTypeDefOf.TaskCompletion,
			historical: false);
	}

	internal static void SpawnChemP2Scene(Map map, IntVec3 origin)
	{
		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		IntVec3 posLox = origin;
		IntVec3 posRp1 = origin + IntVec3.East * 2;
		IntVec3 posEx = origin + IntVec3.East * 4;
		RimPipeDebugUtil.DestroyAt(map, posLox);
		RimPipeDebugUtil.DestroyAt(map, posRp1);
		RimPipeDebugUtil.DestroyAt(map, posEx);
		net.ClearChemReactors();

		Building tankLox = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Dev_Tank, posLox, map, Rot4.North);
		Building tankRp1 = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Dev_Tank, posRp1, map, Rot4.North);
		Building tankEx = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Dev_Tank, posEx, map, Rot4.North);
		net.DebugProcessTopology();

		CompPipeNetworkMember? cLox = tankLox.TryGetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember? cRp1 = tankRp1.TryGetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember? cEx = tankEx.TryGetComp<CompPipeNetworkMember>();
		if (cLox == null || cRp1 == null || cEx == null)
		{
			Messages.Message("[RimPipe] 化学场景失败：缺 Comp。", MessageTypeDefOf.RejectInput, historical: false);
			return;
		}

		Container inLox = cLox.Containers[0];
		Container inRp1 = cRp1.Containers[0];
		Container outEx = cEx.Containers[0];
		inLox.fluid = RimPipeDefOf.RimPipe_Fluid_LOX;
		inRp1.fluid = RimPipeDefOf.RimPipe_Fluid_RP1;
		outEx.fluid = RimPipeDefOf.RimPipe_Fluid_TestExhaust;
		net.TrySetAmount(inLox, 50f);
		net.TrySetAmount(inRp1, 50f);
		net.TrySetAmount(outEx, 0f);

		object owner = "RimPipe.Debug.ChemP2";
		bool ok = net.TryRegisterChemReactor(
			RimPipeDefOf.RimPipe_Reaction_LoxRp1,
			new List<Container> { inLox, inRp1 },
			new List<Container> { outEx },
			owner);
		if (!MapComponent_PipeNetwork.QuietDebugLogs) { Log.Message(net.Dump()); }
		Messages.Message(
			ok
				? "[RimPipe] 化学 P2 场景已生成（LOX50 / RP1 50 / Ex0，罐间隔布局，已绑定配方）。"
				: "[RimPipe] 化学 P2 场景绑定失败。",
			ok ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput,
			historical: false);
	}

	/// <summary>
	/// Stress：约 100 储罐 + 500 管道（10 组 × 5 廊道；每廊道罐—10 管—罐；组间空 2 格）。
	/// 批量 Spawn 时 Quiet；结束后强制一次计时重建。
	/// </summary>
	internal static void SpawnStressGrid(Map map, IntVec3 origin)
	{
		const int Groups = 10;
		const int CorridorsPerGroup = 5;
		const int PipesPerCorridor = 10;
		const int GroupGap = 2;

		ThingDef tankDef = RimPipeDefOf.RimPipe_StorageTank;
		ThingDef pipeDef = RimPipeDefOf.RimPipe_Pipe;
		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();

		int tankCount = 0;
		int pipeCount = 0;
		int skipped = 0;
		bool prevQuiet = MapComponent_PipeNetwork.QuietDebugLogs;
		MapComponent_PipeNetwork.QuietDebugLogs = true;
		try
		{
			for (int g = 0; g < Groups; g++)
			{
				int baseZ = origin.z + g * (CorridorsPerGroup + GroupGap);
				for (int c = 0; c < CorridorsPerGroup; c++)
				{
					IntVec3 left = new IntVec3(origin.x, 0, baseZ + c);
					IntVec3 right = left + new IntVec3(PipesPerCorridor + 1, 0, 0);

					if (!left.InBounds(map) || !right.InBounds(map))
					{
						skipped++;
						continue;
					}

					RimPipeDebugUtil.DestroyAt(map, left);
					RimPipeDebugUtil.DestroyAt(map, right);
					for (int p = 1; p <= PipesPerCorridor; p++)
					{
						IntVec3 pipeCell = left + new IntVec3(p, 0, 0);
						if (!pipeCell.InBounds(map))
						{
							skipped++;
							continue;
						}
						RimPipeDebugUtil.DestroyAt(map, pipeCell);
					}

					GenSpawn.Spawn(tankDef, left, map, Rot4.North);
					tankCount++;
					GenSpawn.Spawn(tankDef, right, map, Rot4.North);
					tankCount++;
					for (int p = 1; p <= PipesPerCorridor; p++)
					{
						IntVec3 pipeCell = left + new IntVec3(p, 0, 0);
						if (!pipeCell.InBounds(map))
						{
							continue;
						}
						GenSpawn.Spawn(pipeDef, pipeCell, map, Rot4.East);
						pipeCount++;
					}
				}
			}
		}
		finally
		{
			MapComponent_PipeNetwork.QuietDebugLogs = prevQuiet;
		}

		net.DebugTimedFullRebuild();
		string msg =
			$"[RimPipe] Stress：罐={tankCount} 管={pipeCount} Mapping={net.Mappings.Count} nets={net.NetCount} " +
			$"重建={net.LastTopologyRebuildMs:F3}ms skippedRows={skipped} @ {origin}";
		Log.Message(msg);
		Messages.Message(msg, MessageTypeDefOf.TaskCompletion, historical: false);
	}

	/// <summary>
	/// 双通道十字场景：中央管道格 A={E,W}、B={N,S}（十字交叉互不相连）。
	/// 东西罐经 A 通道连通（端口 channel=0），南北罐经 B 通道连通（端口 channel=1）。
	/// </summary>
	internal static void SpawnChannelCrossScene(Map map, IntVec3 origin)
	{
		IntVec3 center = origin;
		IntVec3 eastPos = origin + IntVec3.East;
		IntVec3 westPos = origin + IntVec3.West;
		IntVec3 northPos = origin + IntVec3.North;
		IntVec3 southPos = origin + IntVec3.South;

		RimPipeDebugUtil.DestroyAt(map, center);
		RimPipeDebugUtil.DestroyAt(map, eastPos);
		RimPipeDebugUtil.DestroyAt(map, westPos);
		RimPipeDebugUtil.DestroyAt(map, northPos);
		RimPipeDebugUtil.DestroyAt(map, southPos);

		Building pipe = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, center, map, Rot4.North);
		Building east = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, eastPos, map, Rot4.North);
		Building west = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, westPos, map, Rot4.North);
		Building north = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, northPos, map, Rot4.North);
		Building south = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, southPos, map, Rot4.North);

		// 中央管道格：A={E,W}，B={N,S}
		CompPipeCell? cell = pipe.GetComp<CompPipeCell>();
		if (cell != null)
		{
			cell.SetDir(Rot4.North, CompPipeCell.GroupA, false);
			cell.SetDir(Rot4.South, CompPipeCell.GroupA, false);
			cell.SetDir(Rot4.North, CompPipeCell.GroupB, true);
			cell.SetDir(Rot4.South, CompPipeCell.GroupB, true);
		}

		// 南北罐朝中央的端口接到 B 组
		CompPipeNetworkMember cNorth = north.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cSouth = south.GetComp<CompPipeNetworkMember>();
		cNorth.FindPortFacingWorld(Rot4.South)!.channel = 1;
		cSouth.FindPortFacingWorld(Rot4.North)!.channel = 1;

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		CompPipeNetworkMember cEast = east.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cWest = west.GetComp<CompPipeNetworkMember>();
		cEast.Containers[0].fluid = fluid;
		cWest.Containers[0].fluid = fluid;
		cNorth.Containers[0].fluid = fluid;
		cSouth.Containers[0].fluid = fluid;
		net.DebugFillContainer(cEast.Containers[0], 1f);
		net.DebugFillContainer(cWest.Containers[0], 0f);
		net.DebugFillContainer(cNorth.Containers[0], 0.6f);
		net.DebugFillContainer(cSouth.Containers[0], 0.6f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message(
			$"[RimPipe] 双通道十字场景 @ {origin} 东={cEast.Containers[0].amount:0.##} 西={cWest.Containers[0].amount:0.##} " +
			$"北={cNorth.Containers[0].amount:0.##} 南={cSouth.Containers[0].amount:0.##} " +
			$"管格A={cell?.groupAMask} B={cell?.groupBMask}");
		Messages.Message("[RimPipe] 双通道十字验收场景已生成（东西=A 线，南北=B 线，互不相连）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	/// <summary>粘度对比场景：一套水（v=1）、一套稠液（v=2），各为 罐—管—罐 直列，左满右空。</summary>
	internal static void SpawnViscosityScene(Map map, IntVec3 origin)
	{
		IntVec3 wL = origin;
		IntVec3 wPipe = origin + IntVec3.East;
		IntVec3 wR = origin + new IntVec3(2, 0, 0);
		IntVec3 tL = origin + new IntVec3(0, 0, 2);
		IntVec3 tPipe = tL + IntVec3.East;
		IntVec3 tR = tL + new IntVec3(2, 0, 0);

		RimPipeDebugUtil.DestroyAt(map, wL);
		RimPipeDebugUtil.DestroyAt(map, wPipe);
		RimPipeDebugUtil.DestroyAt(map, wR);
		RimPipeDebugUtil.DestroyAt(map, tL);
		RimPipeDebugUtil.DestroyAt(map, tPipe);
		RimPipeDebugUtil.DestroyAt(map, tR);

		Building wTankL = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, wL, map, Rot4.North);
		GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, wPipe, map, Rot4.North);
		Building wTankR = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, wR, map, Rot4.North);
		Building tTankL = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tL, map, Rot4.North);
		GenSpawn.Spawn(RimPipeDefOf.RimPipe_Pipe, tPipe, map, Rot4.North);
		Building tTankR = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cWL = wTankL.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cWR = wTankR.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cTL = tTankL.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cTR = tTankR.GetComp<CompPipeNetworkMember>();
		// 基准线用 TestFuel（v=1 c=1，行为同水），避免与其他场景的水罐混淆
		FluidDef baseline = RimPipeDefOf.RimPipe_Fluid_TestFuel;
		FluidDef thick = RimPipeDefOf.RimPipe_Fluid_TestThick;
		cWL.Containers[0].fluid = baseline;
		cWR.Containers[0].fluid = baseline;
		cTL.Containers[0].fluid = thick;
		cTR.Containers[0].fluid = thick;
		net.DebugFillContainer(cWL.Containers[0], 1f);
		net.DebugFillContainer(cWR.Containers[0], 0f);
		net.DebugFillContainer(cTL.Containers[0], 1f);
		net.DebugFillContainer(cTR.Containers[0], 0f);

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message(
			$"[RimPipe] 粘度场景 @ {origin} 基准={baseline.defName}(v={baseline.viscosity}) 稠液={thick.defName}(v={thick.viscosity}) " +
			$"基准右={cWR.Containers[0].amount:0.##} 稠右={cTR.Containers[0].amount:0.##}");
		Messages.Message("[RimPipe] 粘度验收场景已生成（基准 v=1 vs 稠液 v=2，罐—管—罐）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

	/// <summary>比热对比场景：换热器内部两腔，西腔水（c=1）、东腔高比热液（c=2），同量同初温差。</summary>
	internal static void SpawnSpecificHeatScene(Map map, IntVec3 origin)
	{
		RimPipeDebugUtil.DestroyAt(map, origin);

		Building hx = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_HeatExchanger, origin, map, Rot4.North);
		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember mem = hx.GetComp<CompPipeNetworkMember>();
		Container hot = mem.Containers[0];
		Container cold = mem.Containers[1];
		hot.fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		cold.fluid = RimPipeDefOf.RimPipe_Fluid_TestHeatSink;
		net.DebugFillContainer(hot, 1f);
		net.DebugFillContainer(cold, 1f);
		net.DebugSetTemperature(hot, 80f);
		net.DebugSetTemperature(cold, 20f);

		CompPipeHeatExchanger? hxComp = hx.GetComp<CompPipeHeatExchanger>();
		if (hxComp != null)
		{
			hxComp.IsOpen = true;
		}

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message(
			$"[RimPipe] 比热场景 @ {origin} 水腔(c={hot.fluid.specificHeat}) T={hot.temperature} " +
			$"高比热腔(c={cold.fluid.specificHeat}) T={cold.temperature} 开={hxComp?.IsOpen}");
		Messages.Message("[RimPipe] 比热验收场景已生成（换热器两腔 c=1 vs c=2）。", MessageTypeDefOf.TaskCompletion, historical: false);
	}
	/// <summary>Bridge-H 注入验收场景：左罐 — BridgeH 目标（动态注入 Breachable）— 右罐。</summary>
	internal static void SpawnBridgeHScene(Map map, IntVec3 origin)
	{
		IntVec3 targetPos = origin;
		IntVec3 tankL = origin + IntVec3.West;
		IntVec3 tankR = origin + IntVec3.East;

		RimPipeDebugUtil.DestroyAt(map, tankL);
		RimPipeDebugUtil.DestroyAt(map, targetPos);
		RimPipeDebugUtil.DestroyAt(map, tankR);

		Building target = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_Dev_BridgeHTarget, targetPos, map, Rot4.North);
		Building left = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankL, map, Rot4.North);
		Building right = (Building)GenSpawn.Spawn(RimPipeDefOf.RimPipe_StorageTank, tankR, map, Rot4.North);

		MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember cl = left.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember cr = right.GetComp<CompPipeNetworkMember>();
		CompPipeNetworkMember ct = target.GetComp<CompPipeNetworkMember>();
		FluidDef fluid = RimPipeDefOf.RimPipe_Fluid_TestWater;
		cl.Containers[0].fluid = fluid;
		cr.Containers[0].fluid = fluid;
		ct.Containers[0].fluid = fluid;
		net.DebugFillContainer(cl.Containers[0], 0.8f);
		net.DebugFillContainer(cr.Containers[0], 0f);
		net.DebugFillContainer(ct.Containers[0], 0f);

		CompPipeBreachable? br = target.GetComp<CompPipeBreachable>();

		net.RequestFullRebuild();
		net.DebugProcessTopology();
		Log.Message(
			$"[RimPipe] Bridge-H 场景 @ {origin} 注入Breachable={(br != null)} " +
			$"左={cl.Containers[0].amount} 通={ct.Containers[0].amount} 右={cr.Containers[0].amount}");
		Messages.Message("[RimPipe] Bridge-H 注入验收场景已生成。", MessageTypeDefOf.TaskCompletion, historical: false);
	}

}
