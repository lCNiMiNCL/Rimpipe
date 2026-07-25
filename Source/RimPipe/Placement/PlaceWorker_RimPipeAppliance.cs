using System.Collections.Generic;
using Verse;

namespace RimPipe;

/// <summary>管件（储罐/三通/阀门等）：不能叠在管道上，也不能叠在别的管件上。</summary>
public class PlaceWorker_RimPipeAppliance : PlaceWorker
{
	public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing? thingToIgnore = null, Thing? thing = null)
	{
		List<Thing> list = loc.GetThingList(map);
		for (int i = 0; i < list.Count; i++)
		{
			Thing t = list[i];
			if (t == thingToIgnore || t == thing)
			{
				continue;
			}
			if (PipePlacement.IsPipeOccupant(t))
			{
				return "RimPipe_Place_ApplianceOnPipe".Translate();
			}
			if (PipePlacement.IsApplianceOccupant(t))
			{
				return "RimPipe_Place_ApplianceOccupied".Translate();
			}
		}
		return true;
	}
}
