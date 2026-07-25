using System.Collections.Generic;
using Verse;

namespace RimPipe;

/// <summary>
/// 化学配方：写明每批消耗哪些入料、产出哪些出料。运行时由反应釜 Comp 读取并执行。
/// <para>
/// <b>mixRatio 怎么读：</b>
/// mixRatio = 氧化剂输入量 / 燃料输入量（同一批单位里）。
/// 化学计量比 = 配方里氧化剂计量 / 燃料计量；
/// 若 <c>baseMixRatio</c>≤0，就自动用这个化学计量比当默认混合比。
/// </para>
/// </summary>
public class PipeReactionDef : Def
{
	public List<PipeReactionFluidAmount> inputs = new List<PipeReactionFluidAmount>();
	public List<PipeReactionFluidAmount> outputs = new List<PipeReactionFluidAmount>();

	/// <summary>每一批（20 tick）最多做多少「批单位」的反应。</summary>
	public float maxRate = 1f;

	/// <summary>
	/// 默认混合比（氧化剂/燃料）。写成 ≤0 就改用配方里的化学计量比。
	/// </summary>
	public float baseMixRatio;

	public float ratioMin = 0.5f;
	public float ratioMax = 2f;

	/// <summary>inputs 中氧化剂下标（mixRatio 分子）。</summary>
	public int mixRatioOxidizerInputIndex;

	/// <summary>inputs 中燃料下标（mixRatio 分母）。</summary>
	public int mixRatioFuelInputIndex = 1;

	/// <summary>化学计量比处效率（P4）。</summary>
	public float efficiencyAtStoich = 1f;

	/// <summary>在 ratioMin / ratioMax 端点效率；中间向 stoich 线性插值（P4）。</summary>
	public float efficiencyAtRatioEdge = 0.5f;

	/// <summary>反应要看的入腔最低温度（°C）。默认极低，等于不设门槛。</summary>
	public float minTemperature = -273f;

	/// <summary>反应要看的入腔最低填充比压力。默认 0，等于不设门槛。</summary>
	public float minPressure;

	/// <summary>每完成 1 个批单位反应带进的热量代理（正数放热）。</summary>
	public float heatPerBatch;

	/// <summary>
	/// 反应热写入目标：&lt;0 → 第一产出腔（binding.outputs[0]）；
	/// ≥0 → 所属建筑 CompPipeNetworkMember.Containers 下标（P5b 钉）。
	/// </summary>
	public int heatTargetContainerIndex = -1;

	/// <summary>条件监测的入腔下标；&lt;0 → inputs[0]（P5a 钉：相对配方 inputs）。</summary>
	public int conditionContainerIndex = -1;

	public bool requirePower = true;

	/// <summary>解析后的默认 mixRatio（氧化剂/燃料）。</summary>
	public float ResolvedBaseMixRatio
	{
		get
		{
			if (baseMixRatio > 0f)
			{
				return baseMixRatio;
			}
			return StoichMixRatio;
		}
	}

	/// <summary>inputs 化学计量给出的氧化剂/燃料比；无效时返回 1。</summary>
	public float StoichMixRatio
	{
		get
		{
			if (!TryGetMixPairAmounts(out float ox, out float fuel) || fuel <= 0f)
			{
				return 1f;
			}
			return ox / fuel;
		}
	}

	public bool TryGetMixPairAmounts(out float oxidizerStoich, out float fuelStoich)
	{
		oxidizerStoich = 0f;
		fuelStoich = 0f;
		if (inputs == null
			|| mixRatioOxidizerInputIndex < 0 || mixRatioOxidizerInputIndex >= inputs.Count
			|| mixRatioFuelInputIndex < 0 || mixRatioFuelInputIndex >= inputs.Count)
		{
			return false;
		}
		PipeReactionFluidAmount ox = inputs[mixRatioOxidizerInputIndex];
		PipeReactionFluidAmount fuel = inputs[mixRatioFuelInputIndex];
		if (ox?.fluid == null || fuel?.fluid == null)
		{
			return false;
		}
		oxidizerStoich = ox.stoichAmount;
		fuelStoich = fuel.stoichAmount;
		return fuelStoich > 0f && oxidizerStoich > 0f;
	}

	public override IEnumerable<string> ConfigErrors()
	{
		foreach (string e in base.ConfigErrors())
		{
			yield return e;
		}
		if (inputs == null || inputs.Count == 0)
		{
			yield return "inputs 为空";
		}
		else
		{
			for (int i = 0; i < inputs.Count; i++)
			{
				PipeReactionFluidAmount row = inputs[i];
				if (row == null || row.fluid == null)
				{
					yield return $"inputs[{i}] fluid 无效";
				}
				else if (row.stoichAmount <= 0f)
				{
					yield return $"inputs[{i}] stoichAmount 须 > 0";
				}
			}
		}
		if (outputs == null || outputs.Count == 0)
		{
			yield return "outputs 为空";
		}
		else
		{
			for (int i = 0; i < outputs.Count; i++)
			{
				PipeReactionFluidAmount row = outputs[i];
				if (row == null || row.fluid == null)
				{
					yield return $"outputs[{i}] fluid 无效";
				}
				else if (row.stoichAmount <= 0f)
				{
					yield return $"outputs[{i}] stoichAmount 须 > 0";
				}
			}
		}
		if (maxRate <= 0f)
		{
			yield return "maxRate 须 > 0";
		}
		if (ratioMin <= 0f || ratioMax <= 0f || ratioMin > ratioMax)
		{
			yield return "ratioMin/ratioMax 无效（须 0 < min ≤ max）";
		}
		float resolved = ResolvedBaseMixRatio;
		if (resolved < ratioMin || resolved > ratioMax)
		{
			yield return $"ResolvedBaseMixRatio={resolved} 不在 [{ratioMin},{ratioMax}] 内";
		}
		if (inputs != null && inputs.Count >= 2)
		{
			if (mixRatioOxidizerInputIndex == mixRatioFuelInputIndex)
			{
				yield return "mixRatio 氧化剂/燃料下标不能相同";
			}
			if (!TryGetMixPairAmounts(out _, out _))
			{
				yield return "mixRatio 下标指向的 inputs 无效";
			}
		}
		else if (inputs != null && inputs.Count < 2)
		{
			yield return "L1 需要至少 2 个 inputs（氧化剂/燃料）";
		}
		if (efficiencyAtStoich <= 0f || efficiencyAtStoich > 1f)
		{
			yield return "efficiencyAtStoich 须在 (0,1]";
		}
		if (efficiencyAtRatioEdge <= 0f || efficiencyAtRatioEdge > 1f)
		{
			yield return "efficiencyAtRatioEdge 须在 (0,1]";
		}
	}
}
