using UnityEngine;
using Verse;

namespace RimPipe;

public sealed class RimPipeMod : Mod
{
	public static RimPipeSettings Settings = null!;

	public RimPipeMod(ModContentPack content) : base(content)
	{
		Settings = GetSettings<RimPipeSettings>();
		Log.Message("[RimPipe] Framework loaded (1.6).");
	}

	public override string SettingsCategory()
	{
		return "RimPipe_SettingsCategory".Translate();
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		Listing_Standard listing = new Listing_Standard();
		listing.Begin(inRect);
		listing.CheckboxLabeled(
			"RimPipe_Settings_DumpOnDestroy".Translate(),
			ref Settings.dumpOnDestroy,
			"RimPipe_Settings_DumpOnDestroyDesc".Translate());
		listing.End();
		base.DoSettingsWindowContents(inRect);
	}

	public override void WriteSettings()
	{
		base.WriteSettings();
	}
}
