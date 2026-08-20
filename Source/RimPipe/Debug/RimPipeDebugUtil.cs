using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimPipe.Debug;

/// <summary>Debug 共用工具：Def 分类、清场、套件汇报。</summary>
internal static class RimPipeDebugUtil
{
	internal const float SymmetryTolerance = 1e-3f;

	internal static bool IsTankDef(ThingDef def)
	{
		return def == RimPipeDefOf.RimPipe_Dev_Tank
			|| def == RimPipeDefOf.RimPipe_Dev_TankLarge
			|| def == RimPipeDefOf.RimPipe_StorageTank;
	}

	internal static bool IsTeeDef(ThingDef def)
	{
		return def == RimPipeDefOf.RimPipe_Dev_Tee || def == RimPipeDefOf.RimPipe_TeeJunction;
	}

	internal static bool IsPipeDef(ThingDef def)
	{
		return def == RimPipeDefOf.RimPipe_Dev_Pipe || def == RimPipeDefOf.RimPipe_Pipe;
	}

	internal static bool IsBridgeHTargetDef(ThingDef def)
	{
		return def == RimPipeDefOf.RimPipe_Dev_BridgeHTarget;
	}

	internal static bool AreAdjacentCardinal(IntVec3 a, IntVec3 b)
	{
		int dx = System.Math.Abs(a.x - b.x);
		int dz = System.Math.Abs(a.z - b.z);
		return (dx == 1 && dz == 0) || (dx == 0 && dz == 1);
	}

	internal static int CountFilthThickness(IntVec3 cell, Map map, ThingDef filthDef)
	{
		int n = 0;
		List<Thing> things = cell.GetThingList(map);
		for (int i = 0; i < things.Count; i++)
		{
			Thing t = things[i];
			if (t.def != filthDef)
			{
				continue;
			}
			if (t is Filth f)
			{
				n += f.thickness;
			}
			else
			{
				n++;
			}
		}
		return n;
	}

	internal static void DestroyAt(Map map, IntVec3 cell)
	{
		for (int i = cell.GetThingList(map).Count - 1; i >= 0; i--)
		{
			Thing t = cell.GetThingList(map)[i];
			if (IsTankDef(t.def) || IsTeeDef(t.def) || IsPipeDef(t.def) || t.def == RimPipeDefOf.RimPipe_Valve
			    || t.def == RimPipeDefOf.RimPipe_Pump || t.def == RimPipeDefOf.RimPipe_HeatExchanger
			    || t.def == RimPipeDefOf.RimPipe_Dev_InternalBridge
			    || t.def == RimPipeDefOf.RimPipe_Dev_Reactor
			    || IsBridgeHTargetDef(t.def))
			{
				t.Destroy();
			}
		}
	}

	/// <summary>套件汇总：`[RimPipe] R-xxx：通过 n/m`</summary>
	internal static void ReportSuite(string name, int passed, int total)
	{
		Log.Message($"[RimPipe] {name}：通过 {passed}/{total}");
	}

	/// <summary>同图多次刷场景时，优先认 thingID 更大的（刚刷的）。</summary>
	internal static bool IsNewer(Thing? candidate, Thing? currentBest)
	{
		if (candidate == null)
		{
			return false;
		}
		if (currentBest == null)
		{
			return true;
		}
		return candidate.thingIDNumber > currentBest.thingIDNumber;
	}
}
