using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimPipe;

/// <summary>管道格：仅拓扑；breached=通向大气的孔（路径 Mapping.leakOpen）。Bridge-A 含伤害/Breakdown。</summary>
/// <remarks>
/// 连接分组：4 个方向出口各属 A / B / 不通（AB 不能同占一个方向）。
/// 格内同组出口互连、跨组隔离（A={E,W} B={N,S} = 互不相连的十字交叉）；
/// 邻居之间只要双方对向出口都开就连通，**不看组**（本格 A 出口可直接连邻居 B 出口）。
/// </remarks>
public class CompPipeCell : ThingComp
{
	/// <summary>方向组：0=不通，1=A，2=B。A/B 只是两个无顺序的标签。</summary>
	public const int GroupNone = 0;
	public const int GroupA = 1;
	public const int GroupB = 2;

	/// <summary>A 组方向掩码。bit = Rot4.AsInt（0北 1东 2南 3西）。默认全通 = 旧行为。</summary>
	public uint groupAMask = 0b1111u;

	/// <summary>B 组方向掩码。默认空。与 groupAMask 不允许同占一个方向。</summary>
	public uint groupBMask;

	/// <summary>Gizmo 当前编辑的组（运行时状态，不存档）。</summary>
	public int editGroup = GroupA;

	private bool breached;

	public CompProperties_PipeCell Props => (CompProperties_PipeCell)props;

	public bool Breached
	{
		get => breached;
		set
		{
			if (breached == value)
			{
				return;
			}
			breached = value;
			if (parent.Spawned)
			{
				parent.Map.GetComponent<MapComponent_PipeNetwork>()?.NotifyBreachChanged(parent);
			}
		}
	}

	public bool ClearBreachOnRepaired => Props.clearBreachOnRepaired;

	/// <summary>方向所属组：A/B/不通。</summary>
	public int DirGroup(Rot4 worldDir)
	{
		uint bit = 1u << worldDir.AsInt;
		if ((groupAMask & bit) != 0)
		{
			return GroupA;
		}
		if ((groupBMask & bit) != 0)
		{
			return GroupB;
		}
		return GroupNone;
	}

	/// <summary>该方向出口是否打开（任意组）。</summary>
	public bool IsOpenWorld(Rot4 worldDir) => DirGroup(worldDir) != GroupNone;

	/// <summary>
	/// 把某方向设进/移出某组（从另一组自动移除，保证 AB 不同占）。
	/// 变更后通知管网做局部拓扑重建。
	/// </summary>
	public void SetDir(Rot4 worldDir, int group, bool open)
	{
		uint bit = 1u << worldDir.AsInt;
		if (open)
		{
			if (group == GroupA)
			{
				groupAMask |= bit;
				groupBMask &= ~bit;
			}
			else
			{
				groupBMask |= bit;
				groupAMask &= ~bit;
			}
		}
		else
		{
			groupAMask &= ~bit;
			groupBMask &= ~bit;
		}
		if (parent.Spawned)
		{
			parent.Map.GetComponent<MapComponent_PipeNetwork>()?.NotifyPipeConnectionChanged(this);
		}
	}

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref breached, "breached", false);
		Scribe_Values.Look(ref groupAMask, "groupAMask", 0b1111u);
		Scribe_Values.Look(ref groupBMask, "groupBMask", 0u);
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		parent.Map.GetComponent<MapComponent_PipeNetwork>()?.RegisterPipeCell(this);
	}

	public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
	{
		map.GetComponent<MapComponent_PipeNetwork>()?.DeregisterPipeCell(this, mode);
		base.PostDeSpawn(map, mode);
	}

	public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
	{
		base.PostPostApplyDamage(dinfo, totalDamageDealt);
		if (PipeBreachBridge.ShouldBreachFromHitPoints(parent, Props.breachBelowHitPointsPercent))
		{
			Breached = true;
		}
	}

	public override void ReceiveCompSignal(string signal)
	{
		base.ReceiveCompSignal(signal);
		if (Props.breachOnBreakdown && signal == PipeBreachBridge.BreakdownSignal)
		{
			Breached = true;
		}
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
			defaultLabel = breached ? "RimPipe_Gizmo_PipeBreached".Translate() : "RimPipe_Gizmo_PipeIntact".Translate(),
			defaultDesc = "RimPipe_Gizmo_PipeBreachDesc".Translate(),
			icon = TexCommand.DesirePower,
			isActive = () => breached,
			toggleAction = () => Breached = !Breached
		};

		// 连接分组：先选编辑组 A/B，再切 4 个方向
		yield return new Command_Toggle
		{
			defaultLabel = "RimPipe_Gizmo_GroupA".Translate(),
			defaultDesc = "RimPipe_Gizmo_GroupDesc".Translate("A"),
			isActive = () => editGroup == GroupA,
			toggleAction = () => editGroup = GroupA
		};
		yield return new Command_Toggle
		{
			defaultLabel = "RimPipe_Gizmo_GroupB".Translate(),
			defaultDesc = "RimPipe_Gizmo_GroupDesc".Translate("B"),
			isActive = () => editGroup == GroupB,
			toggleAction = () => editGroup = GroupB
		};
		for (int i = 0; i < 4; i++)
		{
			Rot4 dir = new Rot4(i);
			int group = editGroup;
			yield return new Command_Toggle
			{
				defaultLabel = "RimPipe_Gizmo_DirLabel".Translate(dir.ToStringHuman()),
				defaultDesc = "RimPipe_Gizmo_DirDesc".Translate(dir.ToStringHuman(), group == GroupA ? "A" : "B"),
				isActive = () => DirGroup(dir) == group,
				toggleAction = () => SetDir(dir, group, DirGroup(dir) != group)
			};
		}
	}

	private string DirListText(int group)
	{
		uint mask = group == GroupA ? groupAMask : groupBMask;
		if (mask == 0)
		{
			return "RimPipe_Inspect_DirNone".Translate();
		}
		string names = "";
		bool first = true;
		for (int i = 0; i < 4; i++)
		{
			if ((mask & (1u << i)) == 0)
			{
				continue;
			}
			if (!first)
			{
				names += "、";
			}
			names += new Rot4(i).ToStringHuman();
			first = false;
		}
		return names;
	}

	public override string? CompInspectStringExtra()
	{
		string state = breached ? "RimPipe_Inspect_TankBreach".Translate() : "RimPipe_Inspect_Intact".Translate();
		string dirInfo = "RimPipe_Inspect_PipeDir".Translate(
			"RimPipe_Gizmo_GroupA".Translate(), DirListText(GroupA),
			"RimPipe_Gizmo_GroupB".Translate(), DirListText(GroupB));
		return "RimPipe_Inspect_PipeCell".Translate(state) + "\n" + dirInfo;
	}
}
