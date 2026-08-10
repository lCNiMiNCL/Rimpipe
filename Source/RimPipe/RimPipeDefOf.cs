using RimWorld;
using Verse;

namespace RimPipe;

[DefOf]
public static class RimPipeDefOf
{
	public static FluidDef RimPipe_Fluid_TestWater = null!;
	public static FluidDef RimPipe_Fluid_TestFuel = null!;
	public static FluidDef RimPipe_Fluid_TestThick = null!;
	public static FluidDef RimPipe_Fluid_TestHeatSink = null!;
	public static FluidDef RimPipe_Fluid_LOX = null!;
	public static FluidDef RimPipe_Fluid_RP1 = null!;
	public static FluidDef RimPipe_Fluid_TestExhaust = null!;
	public static PipeReactionDef RimPipe_Reaction_LoxRp1 = null!;
	public static ThingDef RimPipe_Dev_Tank = null!;
	public static ThingDef RimPipe_Dev_TankLarge = null!;

	public static ThingDef RimPipe_Dev_Tee = null!;
	public static ThingDef RimPipe_Dev_Pipe = null!;
	public static ThingDef RimPipe_Dev_InternalBridge = null!;
	public static ThingDef RimPipe_Dev_Reactor = null!;
	public static ThingDef RimPipe_StorageTank = null!;
	public static ThingDef RimPipe_TeeJunction = null!;
	public static ThingDef RimPipe_Pipe = null!;
	public static ThingDef RimPipe_Valve = null!;
	public static ThingDef RimPipe_Pump = null!;
	public static ThingDef RimPipe_HeatExchanger = null!;

	static RimPipeDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(RimPipeDefOf));
	}
}
