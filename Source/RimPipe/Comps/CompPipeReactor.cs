using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe;

/// <summary>
/// 多腔反应釜。要开着、有电才会按配方转化；混合比可用滑条或贫氧/化学计量/富氧三档快捷设定。
/// </summary>
public class CompPipeReactor : ThingComp
{
	private bool isOpen = true;
	private float mixRatio = -1f;
	private CompPowerTrader? powerComp;
	private PipeReactionDef? cachedReaction;

	/// <summary>只给 Debug 用：假装有电。不写进存档。</summary>
	public bool debugForcePowered;

	public CompProperties_PipeReactor Props => (CompProperties_PipeReactor)props;

	public PipeReactionDef? Reaction
	{
		get
		{
			if (cachedReaction == null && !Props.reactionDefName.NullOrEmpty())
			{
				cachedReaction = DefDatabase<PipeReactionDef>.GetNamedSilentFail(Props.reactionDefName);
			}
			return cachedReaction;
		}
	}

	public bool IsOpen
	{
		get => isOpen;
		set
		{
			if (isOpen == value)
			{
				return;
			}
			isOpen = value;
			SyncChemBinding("reactorToggle");
		}
	}

	public bool HasPower => debugForcePowered || powerComp == null || powerComp.PowerOn;

	public bool RequiresPower => Reaction == null || Reaction.requirePower;

	public bool EffectiveEnabled => isOpen && (!RequiresPower || HasPower);

	public float MixRatio
	{
		get
		{
			PipeReactionDef? rx = Reaction;
			if (rx == null)
			{
				return mixRatio > 0f ? mixRatio : 1f;
			}
			if (mixRatio <= 0f)
			{
				return rx.ResolvedBaseMixRatio;
			}
			return ChemSolver.ClampMixRatio(rx, mixRatio);
		}
		set
		{
			PipeReactionDef? rx = Reaction;
			mixRatio = rx != null ? ChemSolver.ClampMixRatio(rx, value) : value;
			SyncChemBinding("reactorMix");
		}
	}

	public float CurrentEfficiency
	{
		get
		{
			PipeReactionDef? rx = Reaction;
			return rx != null ? ChemSolver.ComputeEfficiency(rx, MixRatio) : 1f;
		}
	}

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref isOpen, "isOpen", true);
		Scribe_Values.Look(ref mixRatio, "mixRatio", -1f);
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		powerComp = parent.TryGetComp<CompPowerTrader>();
		if (Reaction != null)
		{
			mixRatio = ChemSolver.ClampMixRatio(Reaction, mixRatio > 0f ? mixRatio : Reaction.ResolvedBaseMixRatio);
		}
		RegisterChemBinding();
	}

	public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
	{
		map.GetComponent<MapComponent_PipeNetwork>()?.UnregisterChemReactor(this);
		base.PostDeSpawn(map, mode);
	}

	public override void ReceiveCompSignal(string signal)
	{
		base.ReceiveCompSignal(signal);
		if (signal == CompPowerTrader.PowerTurnedOnSignal || signal == CompPowerTrader.PowerTurnedOffSignal)
		{
			SyncChemBinding("reactorPower");
		}
	}

	public void RegisterChemBinding()
	{
		if (!parent.Spawned)
		{
			return;
		}
		MapComponent_PipeNetwork? net = parent.Map.GetComponent<MapComponent_PipeNetwork>();
		CompPipeNetworkMember? mem = parent.GetComp<CompPipeNetworkMember>();
		PipeReactionDef? rx = Reaction;
		if (net == null || mem == null || rx == null)
		{
			return;
		}
		if (!TryResolveSlots(mem, rx, out List<Container> inputs, out List<Container> outputs))
		{
			Log.Warning($"[RimPipe] 反应釜 {parent.LabelCap} 腔位解析失败。");
			return;
		}
		net.TryRegisterChemReactor(rx, inputs, outputs, this, MixRatio, EffectiveEnabled);
	}

	public void SyncChemBinding(string? reason = null)
	{
		if (!parent.Spawned)
		{
			return;
		}
		MapComponent_PipeNetwork? net = parent.Map.GetComponent<MapComponent_PipeNetwork>();
		if (net == null)
		{
			return;
		}
		if (!net.TryUpdateChemReactor(this, EffectiveEnabled, MixRatio))
		{
			RegisterChemBinding();
		}
		else
		{
			CompPipeNetworkMember? mem = parent.GetComp<CompPipeNetworkMember>();
			net.WakeMember(mem, reason ?? "reactor");
		}
	}

	private bool TryResolveSlots(
		CompPipeNetworkMember mem,
		PipeReactionDef rx,
		out List<Container> inputs,
		out List<Container> outputs)
	{
		inputs = new List<Container>();
		outputs = new List<Container>();
		List<int> inIdx = Props.inputContainerIndices;
		List<int> outIdx = Props.outputContainerIndices;
		if (inIdx == null || inIdx.Count == 0)
		{
			inIdx = new List<int>();
			for (int i = 0; i < rx.inputs.Count; i++)
			{
				inIdx.Add(i);
			}
		}
		if (outIdx == null || outIdx.Count == 0)
		{
			outIdx = new List<int>();
			int baseIdx = inIdx.Count;
			for (int i = 0; i < rx.outputs.Count; i++)
			{
				outIdx.Add(baseIdx + i);
			}
		}
		if (inIdx.Count != rx.inputs.Count || outIdx.Count != rx.outputs.Count)
		{
			return false;
		}
		for (int i = 0; i < inIdx.Count; i++)
		{
			int idx = inIdx[i];
			if (!MapComponent_PipeNetwork.TryResolveContainer(mem, idx, "反应釜", out Container? c) || c == null)
			{
				return false;
			}
			inputs.Add(c);
		}
		for (int i = 0; i < outIdx.Count; i++)
		{
			int idx = outIdx[i];
			if (!MapComponent_PipeNetwork.TryResolveContainer(mem, idx, "反应釜", out Container? c) || c == null)
			{
				return false;
			}
			outputs.Add(c);
		}
		return true;
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		foreach (Gizmo g in base.CompGetGizmosExtra())
		{
			yield return g;
		}
		if (parent.Faction != null && parent.Faction != Faction.OfPlayer)
		{
			yield break;
		}
		yield return new Command_Toggle
		{
			defaultLabel = isOpen ? "RimPipe_Gizmo_ReactorOpen".Translate() : "RimPipe_Gizmo_ReactorClosed".Translate(),
			defaultDesc = "RimPipe_Gizmo_ReactorDesc".Translate(),
			icon = TexCommand.DesirePower,
			isActive = () => isOpen,
			toggleAction = () => IsOpen = !isOpen
		};

		PipeReactionDef? rx = Reaction;
		if (rx == null)
		{
			yield break;
		}

		yield return new Command_Action
		{
			defaultLabel = "RimPipe_Gizmo_ReactorMix".Translate(MixRatio.ToString("0.##")),
			defaultDesc = "RimPipe_Gizmo_ReactorMixDesc".Translate(
				rx.ratioMin.ToString("0.##"),
				rx.ResolvedBaseMixRatio.ToString("0.##"),
				rx.ratioMax.ToString("0.##"),
				CurrentEfficiency.ToStringPercent()),
			icon = TexCommand.ForbidOff,
			action = OpenMixSlider
		};

		yield return new Command_Action
		{
			defaultLabel = "RimPipe_Gizmo_ReactorMixPreset".Translate(),
			defaultDesc = "RimPipe_Gizmo_ReactorMixPresetDesc".Translate(),
			icon = TexCommand.HoldOpen,
			action = OpenMixPresets
		};
	}

	private void OpenMixSlider()
	{
		PipeReactionDef? rx = Reaction;
		if (rx == null)
		{
			return;
		}
		int from = Mathf.RoundToInt(rx.ratioMin * 100f);
		int to = Mathf.RoundToInt(rx.ratioMax * 100f);
		int cur = Mathf.RoundToInt(MixRatio * 100f);
		Find.WindowStack.Add(new Dialog_Slider(
			val => "RimPipe_Dialog_ReactorMix".Translate((val / 100f).ToString("0.00"),
				ChemSolver.ComputeEfficiency(rx, val / 100f).ToStringPercent()),
			from,
			to,
			val => MixRatio = val / 100f,
			cur,
			1f));
	}

	private void OpenMixPresets()
	{
		PipeReactionDef? rx = Reaction;
		if (rx == null)
		{
			return;
		}
		List<FloatMenuOption> opts = new List<FloatMenuOption>
		{
			new FloatMenuOption(
				"RimPipe_MixPreset_Lean".Translate(rx.ratioMin.ToString("0.##")),
				() => MixRatio = rx.ratioMin),
			new FloatMenuOption(
				"RimPipe_MixPreset_Stoich".Translate(rx.ResolvedBaseMixRatio.ToString("0.##")),
				() => MixRatio = rx.ResolvedBaseMixRatio),
			new FloatMenuOption(
				"RimPipe_MixPreset_Rich".Translate(rx.ratioMax.ToString("0.##")),
				() => MixRatio = rx.ratioMax)
		};
		Find.WindowStack.Add(new FloatMenu(opts));
	}

	public override string? CompInspectStringExtra()
	{
		string state = isOpen ? "RimPipe_State_Open".Translate() : "RimPipe_State_Closed".Translate();
		string power = HasPower ? "RimPipe_State_Powered".Translate() : "RimPipe_State_Unpowered".Translate();
		string rxLabel = Reaction != null ? Reaction.label : "?";
		float lastN = 0f;
		float lastEta = CurrentEfficiency;
		if (parent.Spawned)
		{
			ChemReactorBinding? b = parent.Map.GetComponent<MapComponent_PipeNetwork>()?.FindChemReactor(this);
			if (b != null)
			{
				lastN = b.lastBatchN;
				if (b.lastBatchN > FlowSolver.AmountEpsilon)
				{
					lastEta = b.lastEfficiency;
				}
			}
		}
		return "RimPipe_Inspect_Reactor".Translate(
			state, power, rxLabel, MixRatio.ToString("0.##"), lastN.ToString("0.##"))
			+ "\n" + "RimPipe_Inspect_ReactorEta".Translate(lastEta.ToStringPercent())
			+ CondInspectExtra();
	}

	private string CondInspectExtra()
	{
		PipeReactionDef? rx = Reaction;
		if (rx == null || !parent.Spawned)
		{
			return "";
		}
		MapComponent_PipeNetwork? net = parent.Map.GetComponent<MapComponent_PipeNetwork>();
		ChemReactorBinding? b = net?.FindChemReactor(this);
		if (b == null)
		{
			return "";
		}
		if (ChemSolver.PassesConditions(rx, b.inputs, out string? reason))
		{
			return "";
		}
		if (reason == "cold")
		{
			return "\n" + "RimPipe_Inspect_ReactorBlockedCold".Translate(rx.minTemperature.ToString("0.#"));
		}
		if (reason == "lowP")
		{
			return "\n" + "RimPipe_Inspect_ReactorBlockedPressure".Translate(rx.minPressure.ToString("0.##"));
		}
		return "";
	}
}
