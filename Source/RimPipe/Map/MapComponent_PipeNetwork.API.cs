using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using RimPipe.Debug;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimPipe;

/// <summary>
/// 整张地图的管网总管（partial 拆分版）。主文件保留状态/生命周期，职责见各 partial。
/// </summary>
public partial class MapComponent_PipeNetwork : MapComponent
{

	/// <summary>
	/// P2：注册化学绑定（运行时，不写入存档）。inputs/outputs 数量须与配方一致。
	/// P3 起由 CompPipeReactor 调用；同 owner 重复注册会先撤旧。
	/// </summary>
	public bool TryRegisterChemReactor(
		PipeReactionDef? reaction,
		IList<Container>? inputs,
		IList<Container>? outputs,
		object? owner = null,
		float mixRatio = -1f,
		bool enabled = true)
	{
		if (reaction == null || inputs == null || outputs == null)
		{
			return false;
		}
		if (inputs.Count != reaction.inputs.Count || outputs.Count != reaction.outputs.Count)
		{
			Log.Warning($"[RimPipe] Chem 注册失败：槽位数与配方不符 ({reaction.defName})。");
			return false;
		}
		for (int i = 0; i < inputs.Count; i++)
		{
			if (inputs[i] == null)
			{
				return false;
			}
		}
		for (int i = 0; i < outputs.Count; i++)
		{
			if (outputs[i] == null)
			{
				return false;
			}
		}
		if (owner != null)
		{
			UnregisterChemReactor(owner);
		}
		ChemReactorBinding b = new ChemReactorBinding
		{
			reaction = reaction,
			enabled = enabled,
			mixRatio = mixRatio > 0f ? mixRatio : reaction.ResolvedBaseMixRatio,
			owner = owner
		};
		b.inputs.AddRange(inputs);
		b.outputs.AddRange(outputs);
		chemReactors.Add(b);
		for (int i = 0; i < b.inputs.Count; i++)
		{
			WakeContainer(b.inputs[i], "chemRegister");
		}
		for (int i = 0; i < b.outputs.Count; i++)
		{
			WakeContainer(b.outputs[i], "chemRegister");
		}
		return true;
	}

	/// <summary>按 owner 更新已注册绑定的开关/mix（找不到则 false）。</summary>
	public bool TryUpdateChemReactor(object? owner, bool enabled, float mixRatio = -1f)
	{
		if (owner == null)
		{
			return false;
		}
		for (int i = 0; i < chemReactors.Count; i++)
		{
			ChemReactorBinding b = chemReactors[i];
			if (!ReferenceEquals(b.owner, owner))
			{
				continue;
			}
			b.enabled = enabled;
			if (mixRatio > 0f)
			{
				b.mixRatio = mixRatio;
			}
			for (int c = 0; c < b.inputs.Count; c++)
			{
				WakeContainer(b.inputs[c], "chemUpdate");
			}
			return true;
		}
		return false;
	}

	public ChemReactorBinding? FindChemReactor(object? owner)
	{
		if (owner == null)
		{
			return null;
		}
		for (int i = 0; i < chemReactors.Count; i++)
		{
			if (ReferenceEquals(chemReactors[i].owner, owner))
			{
				return chemReactors[i];
			}
		}
		return null;
	}

	public void UnregisterChemReactor(object? owner)
	{
		if (owner == null)
		{
			return;
		}
		for (int i = chemReactors.Count - 1; i >= 0; i--)
		{
			if (ReferenceEquals(chemReactors[i].owner, owner))
			{
				chemReactors.RemoveAt(i);
			}
		}
	}

	public void ClearChemReactors()
	{
		chemReactors.Clear();
	}

	private void UnregisterChemReactorsTouching(CompPipeNetworkMember? mem)
	{
		if (mem?.Containers == null)
		{
			return;
		}
		for (int i = chemReactors.Count - 1; i >= 0; i--)
		{
			ChemReactorBinding b = chemReactors[i];
			if (BindingTouchesMember(b, mem))
			{
				chemReactors.RemoveAt(i);
			}
		}
	}

	private static bool BindingTouchesMember(ChemReactorBinding b, CompPipeNetworkMember mem)
	{
		for (int i = 0; i < b.inputs.Count; i++)
		{
			if (b.inputs[i]?.owner == mem)
			{
				return true;
			}
		}
		for (int i = 0; i < b.outputs.Count; i++)
		{
			if (b.outputs[i]?.owner == mem)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>扫 mappings 判断两个容器间是否存在 Flow mapping。</summary>
	public bool HasFlowMappingBetween(Container a, Container b)
	{
		if (a == null || b == null || a.id < 0 || b.id < 0)
		{
			return false;
		}
		long key = ContainerPairKey(a.id, b.id, MappingType.Flow);
		for (int i = 0; i < mappings.Count; i++)
		{
			Mapping m = mappings[i];
			if (m.mappingType != MappingType.Flow || m.IsIncomplete || m.containerA == null || m.containerB == null)
			{
				continue;
			}
			if (ContainerPairKey(m.containerA.id, m.containerB.id, MappingType.Flow) == key)
			{
				return true;
			}
		}
		return false;
	}
	/// </summary>
	public bool TrySetBreached(Thing? thing, bool breached)
	{
		if (thing == null)
		{
			return false;
		}
		CompPipeCell? cell = thing.TryGetComp<CompPipeCell>();
		if (cell != null)
		{
			cell.Breached = breached;
			return true;
		}
		CompPipeBreachable? br = thing.TryGetComp<CompPipeBreachable>();
		if (br != null)
		{
			br.Breached = breached;
			return true;
		}
		return false;
	}

	/// <summary>Bridge-A：读取 breached；无对应 Comp 返回 false。</summary>
	public bool TryGetBreached(Thing? thing, out bool breached)
	{
		breached = false;
		if (thing == null)
		{
			return false;
		}
		CompPipeCell? cell = thing.TryGetComp<CompPipeCell>();
		if (cell != null)
		{
			breached = cell.Breached;
			return true;
		}
		CompPipeBreachable? br = thing.TryGetComp<CompPipeBreachable>();
		if (br != null)
		{
			breached = br.Breached;
			return true;
		}
		return false;
	}

	/// <summary>
	/// 稳定公开 API：设置容器量（钳到 0～capacity），同步压力，并唤醒它所在的网。
	/// 第三方模组不要直接调 Container.CommitAmount。
	/// </summary>
	public bool TrySetAmount(Container? c, float amount)
	{
		if (c == null)
		{
			return false;
		}
		c.CommitAmount(Mathf.Clamp(amount, 0f, c.capacity));
		WakeContainer(c, "apiSetAmount");
		return true;
	}

	/// <summary>相对增减量；内部还是走 TrySetAmount。</summary>
	public bool TryAddAmount(Container? c, float delta)
	{
		if (c == null)
		{
			return false;
		}
		return TrySetAmount(c, c.amount + delta);
	}

	/// <summary>设置容器温度，并唤醒它所在的网。</summary>
	public bool TrySetTemperature(Container? c, float temperature)
	{
		if (c == null)
		{
			return false;
		}
		c.CommitTemperature(temperature);
		WakeContainer(c, "apiSetTemp");
		return true;
	}

	/// <summary>某构件所属主网 ID（取首个 Container）；无则 -1。</summary>
	public int GetMemberNetId(CompPipeNetworkMember mem)
	{
		if (mem?.Containers == null || mem.Containers.Count == 0)
		{
			return -1;
		}
		return mem.Containers[0].netId;
	}
}
