# RimPipe 计划与文档索引

> 本文档是 RimPipe 的**总索引**，不再维护完整正文。完整内容已拆分到 `docs/` 下。
> 历史完整副本保留在 `docs/archive/RimPipe_TODO.full.md`。

## 快速导航

| 文档 | 内容 |
|------|------|
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | 当前状态、目标、路线图、进度总表、下一动作 |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | 核心架构、公式、锁定决议、横切规则、阶段设计 |
| [`docs/ACCEPTANCE.md`](docs/ACCEPTANCE.md) | 各阶段游戏内验收记录、回归证据、补测清单 |
| [`docs/CHANGELOG.md`](docs/CHANGELOG.md) | 版本变更记录 |
| [`docs/archive/RimPipe_TODO.full.md`](docs/archive/RimPipe_TODO.full.md) | 拆分前的完整历史 TODO（只读存档） |
| [`Source/RimPipe_API.md`](RimPipe_API.md) | 下游模组 API 与使用说明 |

## 当前状态（摘要）

- **版本：** 0.7.0（RimWorld 1.6）
- **存档 schema：** 1
- **回归套件：** R-框架 / R-物理 / R-热与环境 / R-化学 / R-扩展 / R-通道 / R-全部
- **已实现：** 三层模型、压力、阻力、热量、环境散热、化学、破损桥接、分网休眠、DirtyTopo、A/B 双通道、粘度/比热
- **已完成：** Bridge-H（§7.9，0.7.0 游戏内验收通过）
- **延后：** R2 工坊公开包装、正式反应釜产品化、自有美术

## 维护约定

- 新功能/决议先更新 `docs/ROADMAP.md` 与 `docs/ARCHITECTURE.md`。
- 验收证据写入 `docs/ACCEPTANCE.md`。
- 版本变化写入 `docs/CHANGELOG.md`。
- 本索引文件保持简短，不承载正文。
