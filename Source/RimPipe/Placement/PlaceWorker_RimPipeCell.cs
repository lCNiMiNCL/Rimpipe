using System.Collections.Generic;
using Verse;

namespace RimPipe;

/// <summary>管道格：不可叠在管件或其它管道上（对齐电力 PlaceWorker_Conduit 思路）。</summary>
public class PlaceWorker_RimPipeCell : PlaceWorker
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
				return "RimPipe_Place_CellOccupied".Translate();
			}
			if (PipePlacement.IsApplianceOccupant(t))
			{
				return "RimPipe_Place_CellOnAppliance".Translate();
			}
		}
		return true;
	}
}
