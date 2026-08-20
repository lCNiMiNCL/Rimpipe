using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimPipe;

public sealed class RimPipeMod : Mod
{
	public static RimPipeSettings Settings = null!;

	private static readonly Harmony HarmonyInstance = new Harmony("rimpipe.core");

	public RimPipeMod(ModContentPack content) : base(content)
	{
		Settings = GetSettings<RimPipeSettings>();
		HarmonyInstance.PatchAll();
		Log.Message("[RimPipe] Framework loaded (1.6). Bridge-H ready.");
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
