# RimPipe 变更记录

> 版本记录主要面向下游模组与维护者。由原 `Source/RimPipe_API.md` 的版本短记整理。

## 0.6.0
- 阶段 2 重构遗留处理：核心数据封装与死代码清理。
- `Container` / `Mapping` / `Port` / `ChemReactorBinding` 的运行时字段改为只读属性，写操作收敛到 `internal`。
- `CompPipeNetworkMember.Containers` / `Ports` 改为 `IReadOnlyList` 只读视图。
- 删除无引用代码：`ContainerDelta`、`MapComponent_PipeNetwork.ReevaluateSleepState()`。
- `MappingType.Chemical` 保留为预留占位，不参与当前运行路径。
- **API 变更：** 核心数据字段从 public field 改为 read-only property；当前无下游 Mod 引用，因此不保留旧字段兼容层。schema 仍 1。

## 0.5.1
- 6.19 管道 A/B 双通道（方向分组、端口 channel、Gizmo 逐向配置）。
- 6.18 流体物理量（粘度→流动阻力、比热→传热）。
- 工程与健壮性：MapComponent/DebugAsserts partial 拆分、Flow/Heat/Chem 纯逻辑核抽取、单元测试与 CI、配置校验脚本、R-全部、4.1-4.5 健壮性修复。
- **无下游 API 变更**；schema 仍 1。

## 0.4.7
- DirtyTopo 局部脏区拓扑重建：放/拆管道不再整图重建。
- **无下游 API 变更**。

## 0.4.4–0.4.6
- 批级优化、休眠重评估单遍聚合、代码审查修复、批级缓存。
- **无下游 API 变更**；schema 仍 1。

## 0.4.3
- Debug：拓扑重建 Stopwatch + Stress 一键场景 +「计时整图重建」。
- **无下游 API 变更**。

## 0.4.2
- DevMode Overlay 性能：弃每帧 `FlashCell`，改 `CellRenderer` + 近距 OnGUI `P=`。

## 0.4.1
- Debug 菜单收成 12 键（7 工具 + 5 回归套件）；旧「断言* / 生成*」退出菜单；断言误报硬化。
- **勿依赖**旧 Debug 菜单项名称。

## 0.4.0
- 对内冻结：阶段四收官口径；稳定面 + 交付说明合并进 `Source/RimPipe_API.md`。

## 0.3.x
- Bridge、化学、ExtHook、存档迁移、本地化、休眠、Overlay、泵/泄漏/阻力/压力/热量/散热等阶段性落地。
