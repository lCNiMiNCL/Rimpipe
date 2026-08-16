# RimPipe 变更记录

> 版本记录主要面向下游模组与维护者。由原 `Source/RimPipe_API.md` 的版本短记整理。

## 0.5.1
- 6.19 管道 A/B 双通道（方向分组、端口 channel、Gizmo 逐向配置）。
- 6.18 流体物理量（粘度→流动阻力、比热→传热）。
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
