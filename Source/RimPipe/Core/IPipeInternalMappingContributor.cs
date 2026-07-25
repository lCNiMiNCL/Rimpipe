using System.Collections.Generic;

namespace RimPipe;

/// <summary>
/// 扩展钩子（ExtHook）：同建筑内部 Mapping 的登记口。
/// 拓扑重建时，MapComp 会扫一遍实现了本接口的 Comp（阀、泵、换热器，以及第三方双腔设备），
/// 让它们把自己建筑内部那条边挂上去。
/// 注意：这里只负责「内部边」；不会自动去改外面直接相邻那几条边的 Forced 标记
/// （泵自己的 Forced 贴邻逻辑仍由泵 Comp 处理）。
/// </summary>
public interface IPipeInternalMappingContributor
{
	void ContributeInternalMapping(MapComponent_PipeNetwork net, HashSet<long> linkedPairs);
}
