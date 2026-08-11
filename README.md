# RimPipe

RimPipe 是 RimWorld 1.6 的流体管道框架模组，基于容器–端口–映射三层模型，实现了流体传输、压力、热量、化学反应、破损泄漏和分网休眠。模组本身面向扩展，下游模组可以依赖 `rimpipe.core` 来定义自己的流体和反应。

> **English summary:** RimPipe is a fluid pipeline framework mod for RimWorld 1.6. It implements a Container / Port / Mapping model with pressure equalization, heat exchange, chemical reactions, breach leakage, and per-network sleep. Downstream mods can depend on `rimpipe.core` and extend it via `FluidDef`, `PipeReactionDef`, and `IPipeInternalMappingContributor`. See [`Source/RimPipe_API.md`](Source/RimPipe_API.md) for the full downstream API.

- **作者 / 维护者：** NewFrontierTeam
- **当前版本：** 0.5.1
- **支持游戏版本：** RimWorld 1.6
- **packageId：** `rimpipe.core`
- **存档 schema：** 1（稳定面以 0.4.0 对内冻结为准）
- **仓库：** https://github.com/lCNiMiNCL/Rimpipe
- **许可证：** GPL-3.0-or-later（见 [LICENSE](LICENSE)）

---

## 特性

- **三层模型：** `Container`（一种流体 + 量）、`Port`（本地朝向 → 世界朝向 → 邻格）、`Mapping`（容器↔容器，每 20 tick 一批处理）
- **拓扑：** 设备直接相邻对接 + 管道 Voronoi 连通；DirtyTopo 局部脏区重建，放/拆管道不需要整图重建
- **设备（建造栏「管道」分类）：** 储罐、三通、管道、阀门、泵、换热器
- **物理：** 压力均分（按填充比 `P=amount/capacity`）、路径阻力（外部 Mapping 按最短路径格数衰减）、破损泄漏与环境效果、热量混温、环境散热、分网休眠
- **流体物理量：** `FluidDef.viscosity`（粘度→流动阻力）、`specificHeat`（比热→传热）；v=c=1 时退化为旧行为
- **A/B 双通道：** 管道 4 个方向可逐向配置 A/B 通道，格内同组互连、跨组隔离；端口可指定 channel
- **化学：** `PipeReactionDef` + `CompPipeReactor`（或运行时 `TryRegisterChemReactor`）支持多入多出反应、空燃比、温度/压力门槛、反应热
- **破损桥接：** 伤害/Breakdown 自动触发 `breached`，修满血后可清除（无 Harmony）
- **扩展接口：** `IPipeInternalMappingContributor` 登记同建筑内部 Mapping；`TrySetAmount` / `TrySetTemperature` / `TrySetBreached` 等运行时 API
- **DevMode 工具：** Overlay（压色 + 上批流量边线）、Benchmark / Stress 计时、5 套回归测试套件（R-框架 / R-物理 / R-热与环境 / R-化学 / R-扩展 + R-通道）

> 管道格本身不存流体，流体量都在设备/管件的 `Container` 里。

---

## 安装与依赖

### 玩家安装

1. 把整个 `Rimpipe` 文件夹放进 RimWorld 的 `Mods` 目录（Steam 安装路径一般在 `Steam/steamapps/common/RimWorld/Mods`）。
2. 启动游戏，在模组菜单里勾选「RimPipe」。
3. 建造栏出现「管道」分类，里面是储罐、三通、管道、阀门、泵、换热器。

### 依赖

- **必需：** RimWorld 1.6
- **运行时无前置：** 不需要 Harmony 或其他前置 mod（仓库 `Source/Libs/0Harmony/` 只是为后续阶段预留，当前未引用）
- **下游模组：** 通过 C# 引用 RimPipe.dll 时，在 About.xml 里 `loadAfter` `rimpipe.core`，引用 `Assemblies/RimPipe.dll` 并设 `Private=false`

### 运行时需要的文件

| 路径 | 用途 |
|------|------|
| `About/` | 元数据 |
| `Assemblies/RimPipe.dll` | 程序集 |
| `Defs/` | 流体 / 建筑 / 反应 / 分类 |
| `Languages/` | 简中 Keyed + 英文 DefInjected / Keyed |

`Source/`（C# 源码 + 文档）、`LICENSE`、`README.md`、`CONTRIBUTING.md` 属于仓库内容，游戏加载时不强制要求。

---

## 下游模组开发指引

想基于 RimPipe 做扩展（自定义流体、反应、双腔设备等），完整说明见：

**👉 [`Source/RimPipe_API.md`](Source/RimPipe_API.md)**

内容包括：怎么挂依赖、本版能依赖什么、纯 XML 扩展（FluidDef / 储罐 / 管道 / 阀·泵·换热器 / 自定义内部 Mapping / 化学反应）、运行时 C# API（`TrySetAmount` / `TrySetTemperature` / `TrySetBreached` / `TryRegisterChemReactor` 等）、以及能力边界。

> 多流体转化用 **`PipeReactionDef` + 反应釜**，不要用成对的 `MappingType.Chemical` 去模拟。

---

## 贡献

提交 Issue 或 Pull Request 前，可以先看一下 [CONTRIBUTING.md](CONTRIBUTING.md) 里的开发环境与代码风格说明。

---

## 许可证

本项目基于 [GNU General Public License v3](LICENSE) 或更高版本发布（SPDX: `GPL-3.0-or-later`）。

- 可以自由复制、分发与修改本项目，但衍生作品必须以相同协议开源
- 下游 mod 引用 `RimPipe.dll` 不触发 GPL 的传染条款，只有修改 RimPipe 本体源码才需要开源
- 许可证全文见 [LICENSE](LICENSE)
