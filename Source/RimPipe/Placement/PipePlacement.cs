using Verse;

namespace RimPipe;

/// <summary>叠放规则：管道格和管件（储罐/三通/阀门等）互斥，不能占同一格。</summary>
public static class PipePlacement
{
	public static bool IsPipeCellDef(ThingDef? def)
	{
		if (def?.comps == null)
		{
			return false;
		}
		for (int i = 0; i < def.comps.Count; i++)
		{
			if (def.comps[i] is CompProperties_PipeCell)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsApplianceDef(ThingDef? def)
	{
		if (def?.comps == null)
		{
			return false;
		}
		for (int i = 0; i < def.comps.Count; i++)
		{
			if (def.comps[i] is CompProperties_PipeNetworkMember)
			{
				return true;
			}
		}
		return false;
	}

	public static ThingDef? BuiltThingDef(Thing thing)
	{
		if (thing?.def == null)
		{
			return null;
		}
		if (thing.def.entityDefToBuild is ThingDef built)
		{
			return built;
		}
		return thing.def;
	}

	public static bool IsPipeOccupant(Thing thing)
	{
		if (thing is ThingWithComps twc && twc.GetComp<CompPipeCell>() != null)
		{
			return true;
		}
		return IsPipeCellDef(BuiltThingDef(thing));
	}

	public static bool IsApplianceOccupant(Thing thing)
	{
		if (thing is ThingWithComps twc && twc.GetComp<CompPipeNetworkMember>() != null)
		{
			return true;
		}
		return IsApplianceDef(BuiltThingDef(thing));
	}
}
