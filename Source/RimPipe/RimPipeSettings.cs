using Verse;

namespace RimPipe;

public class RimPipeSettings : ModSettings
{
	/// <summary>摧毁/拆卸瞬间按至多 1 批 rate 小额倾泻。默认关（断路不倾倒）。</summary>
	public bool dumpOnDestroy;

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref dumpOnDestroy, "dumpOnDestroy", false);
	}
}
