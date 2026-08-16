using System.Collections.Generic;
using System.Text;
using Verse;

namespace RimPipe;

/// <summary>
/// 管网上的一个设备构件：自己带着 Container（装液）和 Port（对外接口）。
/// 生成时向 MapComp 注册，拆除时注销。
/// </summary>
public class CompPipeNetworkMember : ThingComp
{
	private List<Container> _containers = new List<Container>();
	private List<Port> _ports = new List<Port>();

	public IReadOnlyList<Container> Containers => _containers;
	public IReadOnlyList<Port> Ports => _ports;

	public CompProperties_PipeNetworkMember Props => (CompProperties_PipeNetworkMember)props;

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		EnsureRuntimeObjects(respawningAfterLoad);
		parent.Map.GetComponent<MapComponent_PipeNetwork>()?.RegisterMember(this, respawningAfterLoad);
	}

	public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
	{
		map.GetComponent<MapComponent_PipeNetwork>()?.DeregisterMember(this, mode);
		base.PostDeSpawn(map, mode);
	}

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Collections.Look(ref _containers, "containers", LookMode.Deep);
		if (_containers == null)
		{
			_containers = new List<Container>();
		}
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			EnsureRuntimeObjects(respawningAfterLoad: true);
		}
	}

	private void EnsureRuntimeObjects(bool respawningAfterLoad)
	{
		CompProperties_PipeNetworkMember p = Props;
		if (!respawningAfterLoad || _containers.Count == 0)
		{
			_containers.Clear();
			for (int i = 0; i < p.containers.Count; i++)
			{
				PipeContainerProp cp = p.containers[i];
				Container c = new Container
				{
					containerIndex = i,
					capacity = cp.capacity,
					amount = cp.initialAmount,
					owner = this
				};
				if (!cp.fluidDefName.NullOrEmpty())
				{
					c.fluid = DefDatabase<FluidDef>.GetNamedSilentFail(cp.fluidDefName);
				}
				c.SyncPressureFromAmount();
				_containers.Add(c);
			}
		}
		else
		{
			for (int i = 0; i < _containers.Count; i++)
			{
				Container c = _containers[i];
				c.owner = this;
				c.containerIndex = i;
				if (i < p.containers.Count)
				{
					c.capacity = p.containers[i].capacity;
				}
				// 4.5 SaveMig：Props 覆盖 capacity 后显式钳量（勿等 Commit）
				if (c.amount > c.capacity)
				{
					Log.Warning(
						$"[RimPipe] 读档钳量 {parent?.LabelCap} 容器[{i}] {c.amount:0.####}→{c.capacity:0.####}（capacity 来自 Props）。");
					c.amount = c.capacity;
				}
				else if (c.amount < 0f)
				{
					Log.Warning(
						$"[RimPipe] 读档负量钳零 {parent?.LabelCap} 容器[{i}] {c.amount:0.####}→0。");
					c.amount = 0f;
				}
				c.SyncPressureFromAmount();
			}

			// 4.4 旧档容器补齐：若旧存档容器数少于当前 Props，按 Props 追加默认容器。
			// 这是 additive 迁移，不升 schema，也不改动已有容器数据。
			if (_containers.Count < p.containers.Count)
			{
				for (int i = _containers.Count; i < p.containers.Count; i++)
				{
					PipeContainerProp cp = p.containers[i];
					Container c = new Container
					{
						containerIndex = i,
						capacity = cp.capacity,
						amount = cp.initialAmount,
						owner = this
					};
					if (!cp.fluidDefName.NullOrEmpty())
					{
						c.fluid = DefDatabase<FluidDef>.GetNamedSilentFail(cp.fluidDefName);
					}
					c.SyncPressureFromAmount();
					_containers.Add(c);
					Log.Warning(
						$"[RimPipe] 读档补齐容器 {parent?.LabelCap} 容器[{i}]（Props 新增，按默认值初始化）。");
				}
			}
		}

		_ports.Clear();
		if (p.ports != null)
		{
			for (int i = 0; i < p.ports.Count; i++)
			{
				PipePortProp pp = p.ports[i];
				_ports.Add(new Port
				{
					localRot = pp.localRot,
					containerIndex = pp.containerIndex,
					channel = pp.channel,
					owner = this
				});
			}
		}
	}

	public Port? FindPortFacingWorld(Rot4 worldRot)
	{
		for (int i = 0; i < _ports.Count; i++)
		{
			if (_ports[i].WorldRot == worldRot)
			{
				return _ports[i];
			}
		}
		return null;
	}

	public Port? FindPortWhoseOuterCellIs(IntVec3 cell)
	{
		for (int i = 0; i < _ports.Count; i++)
		{
			if (_ports[i].OuterCell == cell)
			{
				return _ports[i];
			}
		}
		return null;
	}

	public override string? CompInspectStringExtra()
	{
		if (_containers.Count == 0 && _ports.Count == 0)
		{
			return null;
		}
		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < _containers.Count; i++)
		{
			Container c = _containers[i];
			string fluidName = c.fluid != null ? c.fluid.label : "RimPipe_Inspect_Empty".Translate().ToString();
			sb.AppendLine(
				"RimPipe_Inspect_Container".Translate(
					i,
					fluidName,
					c.amount.ToString("0.#"),
					c.capacity.ToString("0.#"),
					c.pressure.ToString("0.###"),
					c.temperature.ToString("0.#"),
					c.netId).ToString());
		}

		MapComponent_PipeNetwork? net = parent.Spawned ? parent.Map.GetComponent<MapComponent_PipeNetwork>() : null;
		if (_ports.Count > 0 && parent.Spawned)
		{
			for (int i = 0; i < _ports.Count; i++)
			{
				Port p = _ports[i];
				bool docked = net != null && net.IsPortLikelyDocked(p);
				sb.Append("RimPipe_Inspect_Port".Translate(p.WorldRot.ToStringHuman(), p.OuterCell));
				sb.Append(docked ? "RimPipe_Inspect_Docked".Translate() : "RimPipe_Inspect_Undocked".Translate());
				sb.AppendLine();
			}
		}

		if (net != null)
		{
			int mapCount = net.CountMappingsFor(this);
			int netId = net.GetMemberNetId(this);
			sb.Append("Mapping: ");
			sb.Append(mapCount);
			sb.Append("  netId=");
			sb.Append(netId);
			if (netId >= 0)
			{
				sb.Append("  sleep=");
				sb.Append(net.GetNetSleepState(netId));
			}
			sb.Append("  defaultRate=");
			sb.Append(Props.defaultMaxFlowRate.ToString("0.#"));
			sb.Append("  insulation=");
			sb.Append(Props.insulation.ToString("0.##"));
			sb.AppendLine();
			net.AppendMappingsInspect(this, sb);
		}

		return sb.ToString().TrimEnd();
	}
}
