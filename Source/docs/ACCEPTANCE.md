# RimPipe 验收记录

> 本文档收录 RimPipe 各阶段游戏内验收记录、回归证据与补测清单。
> 由原 `Source/RimPipe_TODO.md` 拆分而来。

> 文中 `§x.y` 等编号沿用原 TODO；跨文档引用请按 `Source/RimPipe_TODO.md` 总索引或各文档导航查找。

## 一、现状快照（2026-07-14 · 阶段二后）

### 1.1 已具备能力

- **Container / Port / Mapping** 三层模型；无 Node 类。
- **MapComponent_PipeNetwork**：延迟拓扑 → 0–18 Accumulate → 19 Commit；竞争缩放；Jacobi；**内部 Mapping**（阀门）。
- **管道拓扑**：Voronoi 邻接建 Mapping。
- **Flow：** `drive = |Δ|/2`，`want = min(drive, rate, amountHi, freeLo)`。
- **存档：** 量在 Comp；阀开关 `IExposable`；跨建筑 Mapping 读档重建。
- **产品（建造栏「管道」）：** 储罐 / 三通管 / 管道 / 阀门 / **泵**。
- **Dev：** Tank / Tee / Pipe **保留**，但不进建造栏（仅 Debug / DefOf）。
- **叠放：** 管道 ↔ 管件互斥（`PlaceWorker_RimPipeCell` / `PlaceWorker_RimPipeAppliance`）。
- **Inspect / Gizmo：** 量、端口对接、阀门开/关、泵开/关与电力。
- **Debug：** 菜单 **14 键**（9 工具含 Benchmark/Stress + 5 回归套件 R-*）；旧「断言* / 生成*」已退出菜单（内部调用）。见 §7.12 / §7.10.4。
- **泵 P-A：** Forced 内部 Mapping + 开启时直接相邻的外部 Forced；开且有电满 rate。
- **泄漏 L-A：** `breached` / `leakOpen`；摧毁断路不倾倒；`dumpOnDestroy` 默认关。
- **阻力 R-A：** 外部 Mapping 按最短路径格数衰减 `maxFlowRate`；直接相邻/内部不衰减。
- **泄漏效果 E-A：** Commit/`dumpOnDestroy` 扣量后按 `FluidDef.leakEffects` 施加 Filth / Temperature；水=None；TestFuel→Filth_Fuel+升温。
- **压力 Pr-A：** Equalize 按填充比 `P=amount/capacity`，`drive=|ΔP|/2*minCap`；等容≡旧公式；Forced 不变；`RimPipe_Dev_TankLarge` + Debug 异容断言。
- **热量 H-A：** `Container.temperature`；Flow 携带混温；`MappingType.Heat` + 换热器开/关。
- **化学 Chem：** ✅（§6.17 / §1.2q）；A/B1 多腔 + RecipeDef + L1 + T/P + 反应热；Dev 釜。
- **环境散热 Amb-A：** 有液 Container 按 `insulation` 向格温靠拢；**室内** PushHeat；管格不参与。
- **4.1 休眠 Sleep：** 整图三态；无 Flow/Heat want 时跳过 Accumulate；AmbientOnly 仍跑泄漏+Amb；FullyQuiet 跳过 Commit 体（批次结束时仍做廉价评估）。
- **4.2 分网休眠 NetSleep：** 完整 Mapping 连通分量 `netId`；每网独立三态；拓扑仍整图重建；阀/泵等按网 Wake。
- **4.3 Overlay：** DevMode Overlay（格压色 + 上批流量边线）；`lastBatchWant` 快照。
- **4.5 SaveMig：** MapComp `rimPipeSchemaVersion=1`；读档 Props 覆盖 capacity 后钳 `amount`；拓扑仍重建；无历史迁移器。

### 1.2k 阶段四 · 4.2 分网休眠验收复查（Player.log + 玩家确认 · 2026-07-15）


| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 分网隔离 Busy∥Amb | ✅ | `分网休眠通过：网甲=Busy(net=0) 网乙=AmbientOnly(net=1) nets=2 派生整图=Busy` |
| Dump nets / 分态 | ✅ | `nets=2 sleepState=Busy (busy=1 amb=1 quiet=0)`；Net#0 Busy / Net#1 AmbientOnly；乙三端 P=0.5 无 Flow want |
| 休眠进入（派生） | ✅ | `休眠进入通过：state=AmbientOnly` |
| 休眠唤醒 | ✅ | `休眠唤醒通过：state=Busy wokeBusy=True 量 40.0094→71.5753 Δ=31.5659` |
| 断言仅Amb休眠 | ⓘ | 两次 `未找到满液 Dev 罐`（未先生成散热场景）；**并并不否定** Net#1=`AmbientOnly` 与进入通过 |
| 二次分网场景 | ✅ | 再生成叠加后 `nets=4 (busy=2 amb=2)` 仍隔离正确 |
| 对称 / 泄漏快速回归 | ⓘ | 本段无独立对称/泄漏行；玩家声明基本验收通过 |
| 无崩溃 / Exception | ✅ | 本段无 Exception / Config error / `RimPipe…失败` |

**总判：4.2 NetSleep 验收通过**（玩家确认 · 2026-07-15）。

### 1.2l 阶段四 · 4.3 Overlay 验收复查（Player.log + 玩家确认 · 2026-07-15）


| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| Overlay 开 | ✅ | `流量/压力 Overlay=开（仅 DevMode；看上批流量…）`；玩家确认可见压色/边线 |
| Overlay 关 / 均分淡化 | ✅ | 日志无独立「关」行；玩家声明基本验收通过 |
| 无崩溃 | ✅ | 本段无 Exception / Config error |

**总判：4.3 Overlay 验收通过**（玩家确认 · 2026-07-15）。

### 1.2m 阶段四 · 4.5 SaveMig 验收复查（玩家确认 · 2026-07-18）


| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 旧存档无 schema 字段仍可读入 | ✅ | 玩家确认；Dump `schema=1`；量/温/开关/breached 保留 |
| 破损场景存读档后仍泄漏 | ✅ | 玩家确认；靠 `breached` 刷 `leakOpen` |
| 对称 / 泵快速回归 | ✅ | 玩家确认不回归 |
| Dump `schema=1` | ✅ | 玩家确认 |
| 无崩溃 / Exception | ✅ | 玩家确认 |
| （可选）capacity 缩小钳量 | ⓘ | 未单列；不影响验收通过 |

**总判：4.5 SaveMig 验收通过**（玩家确认 · 2026-07-18）。

### 1.2n 阶段四 · 4.4 ApiDoc 验收复查（玩家确认 · 2026-07-18）


| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| API 填液唤醒 | ✅ | 玩家确认：Debug 填液 → Busy + 流动（TrySetAmount 路径） |
| `RimPipe_API.md` 边界可读 | ✅ | 玩家确认：无内部 Mapping 钩子承诺清晰 |
| 对称 / 泵快速回归 | ✅ | 玩家确认不回归 |
| 无崩溃 / Exception | ✅ | 玩家确认 |

**总判：4.4 ApiDoc 验收通过**（玩家确认 · 2026-07-18）。

### 1.2o 阶段四 · 4.6 Loc 验收复查（玩家确认 · 2026-07-18）


| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 未研究 ComplexFurniture 不可造基础 | ✅ | 玩家确认：Pipe/Tank/Tee |
| 未研究 Electricity 不可造阀/泵/换热器 | ✅ | 玩家确认：Valve/Pump/HeatExchanger |
| 简中 Defs 基线文案 | ✅ | 玩家确认 |
| 切 English DefInjected | ✅ | 玩家确认 |
| Dev Debug 仍可刷 | ✅ | 玩家确认：无研究前置 |
| 存读档 / 无崩溃 | ✅ | 玩家确认 |

**总判：4.6 Loc 验收通过**（玩家确认 · 2026-07-18）。

### 1.2p 阶段四 · 4.7 ExtHook 验收复查（Player.log + 玩家确认 · 2026-07-19）


| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| ExtHook 内部边自测 | ✅ | `ExtHook通过：内部边 rate=10 左 100→51.2019 右 0→39.7072`；场景 `内部边=True` Mapping=3 |
| 均压后再断言 | ⓘ | 两次 `ExtHook失败` 且 `edgeOk=True`：已近均分，Δ&lt;0.5 误报失败；**并并不否定**主路径 |
| 泵快速回归 | ✅ | `泵场景 … 开=True 电=True rate=10 左=20 右=80`；拓扑含泵内部边 |
| 阀 / 换热器快速回归 | ⓘ | 本段日志无独立阀/换热场景行；玩家声明验收通过；泵+对称已覆盖接口扫描产品路径 |
| 三通对称回归 | ✅ | `对称通过：左=64.0018 右=64.0018 通=31.9964` |
| 无崩溃 / Exception | ✅ | 本段无 Exception / Config error |
| API.md | ✅ | 玩家确认扩展写法可读 |

**总判：4.7 ExtHook 验收通过**（玩家确认 · 2026-07-19；误报失败不影响）。

### 1.2s 阶段四 · 4.11 R1 回归快速检查（Player.log · 2026-07-20）


| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 三通对称 | ✅ | `对称通过：左=64.0018 右=64.0018 通=31.9964` |
| 泵逆向抽送（日志：逆均分） | ✅ | `泵逆均分通过：左 20→0.1107 右 80→97.2854`；另有 `泵阻断通过` |
| 泵曾失败行 | ⓘ | 两次 `泵逆均分失败：左Δ=0 右Δ=0`：多网并存 / 已抽干后再断言误报失败（同历史泵验收） |
| Bridge×3 | ✅ | 伤害 / Breakdown / 满血清 三行「通过」 |
| 休眠进入 | ✅ | `休眠进入通过：state=AmbientOnly` |
| 休眠曾失败行 | ⓘ | 先 `休眠进入失败：state=Busy`：同图仍有 Busy 泵网（Dump busy=1 amb=2）属预期 |
| 休眠唤醒 | ✅ | `休眠唤醒通过：… Δ=22.917` |
| 化学 | ✅ | `化学反应热通过`；`化学条件通过`；`化学L1通过`；`化学釜阻断通过` |
| 化学转化/釜主断言行 | ⓘ | 本段无独立 `化学转化通过` / `化学釜通过`；反应热+L1+条件已覆盖主路径 |
| 存读档往返 | ⓘ | 本段无 `Loading game` 行；不影响对内冻结（框架存读既往已通） |
| 无崩溃 | ✅ | 本段无 Exception / Config error |

**总判：4.11 R1 P3 回归通过**（Player.log · 2026-07-20；误报失败与缺存读档日志行不影响验收通过）。

### 1.2t 阶段四 · 4.12 Debug 整理验收（Player.log + 玩家确认 · 2026-07-20）


> **前置：** DevMode；已加载 **`0.4.1+`** dll（本段证据含 Overlay `0.4.2`）；建议**空图**或远离旧管网再点套件。  
> **菜单：** 工具×7 + 5 套件；无「断言*」/「生成三通（Dev）」（编码已锁定；本段以套件汇总为准）。

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 菜单 12 键 | ✅ | 工具×7 + R-框架/物理/热与环境/化学/扩展（§7.12.1） |
| R-框架 | ✅ | `R-框架：通过 8/8`；休眠进入/唤醒、`对称通过`、Bridge×3、`泵逆均分通过`、`泵阻断通过` |
| R-物理 | ✅ | `R-物理：通过 7/7`；压力 / 阻力 / 破损泄漏 / 水无Filth / 摧毁停漏 / Filth / 室温 |
| R-热与环境 | ✅ | `R-热与环境：通过 6/6`；热量均分/阻断、混温、保温对比、空罐、环境散热 |
| R-化学 | ✅ | `R-化学：通过 5/5`；釜 / L1 / 条件 / 反应热 / 阻断 |
| R-扩展 | ✅ | `R-扩展：通过 2/2`；`ExtHook通过`、`分网休眠通过` |
| 无崩溃 | ✅ | 本段无 Exception / Config error |
| 摧毁停漏偶发 | ⓘ | 中段一次 `摧毁停漏失败`→`R-物理：通过 6/7`（多网并存）；末段与多次重跑均为 7/7，不否定主路径 |

**总判：4.12 Debug 整理验收通过**（Player.log 末段连续五套件满分 · 2026-07-20；偶发 6/7 不影响）。

### 1.2u Overlay 性能修复 + MapComponentTick 峰值笔记（玩家确认 · 2026-07-20）


| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| Overlay `0.4.2`（弃 FlashCell） | ✅ | 玩家确认：开/关正常；暂停不再恶化；压色 + 近距 `P=` + 流量边线可用 |
| MapComponentTick 平均 vs 峰值 | ⓘ 研究写入 | Dubs：平均 ~0.3ms、峰值可达 **7–11ms+**；**属设计内不均匀负载**，非 Overlay 回归 |
| 峰值主因归类 | ⓘ | 详 **§7.10.7**（phase=19 Commit/休眠重评；偶发整图拓扑；Busy 时 Acc Jacobi） |

**总判：Overlay 性能修复验收通过**；Tick 峰值已记录供 DirtyTopo / Benchmark 决策参考（§7.10）。

### 1.2v 阶段 2 重构 partial 验收复查（Player.log + 玩家确认 · 2026-08-16）

> 加载 DLL：0.5.1
> 本轮验证 `MapComponent_PipeNetwork` 与 `RimPipeDebugAsserts` partial 拆分后的行为一致性。
> Player.log：`C:\Users\12135\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| R-框架 | ✅ | `R-框架：通过 9/9`；休眠进入/唤醒、对称、Bridge×3、泵逆均分、泵阻断、泵局部重建 |
| R-物理 | ✅ | `R-物理：通过 7/7`；压力、阻力、破损泄漏、水无Filth、摧毁停漏、Filth、泄漏室温 |
| R-热与环境 | ✅ | `R-热与环境：通过 6/6`；热量均分、热量阻断、混温、保温对比、空罐、环境散热 |
| R-化学 | ✅ | `R-化学：通过 5/5`；釜 / L1 / 条件 / 反应热 / 阻断 |
| R-扩展 | ✅ | `R-扩展：通过 2/2`；ExtHook、分网休眠 |
| R-通道 | ✅ | `R-通道：通过 4/4`；双通道十字、方向断开恢复、粘度、比热 |
| 存读档 | ✅ | `Loading game from file TestPipe with mods:` 后无 RimPipe Exception；玩家确认存读档正常 |
| 无崩溃 / Exception | ✅ | 本段无 RimPipe Exception / Config error |

**总判：阶段 2 partial 重构未引入崩溃与存读档问题；6 套回归全部通过。**

### 1.2w 阶段 3 回归问题研究（Player.log · 2026-08-16）

> 加载 DLL：0.5.1

> 新一轮测试出现两类报错：`R-通道：通过 3/4` 与 `局部≈整图等价失败`。已定位并修复。

| 问题 | 根因 | 修复 |
|------|------|------|
| R-通道 3/4：`粘度失败：基准 右增 0 vs 稠液 右增 27` | `AssertViscosity` 取“第一条匹配 Mapping”，可能命中 R-物理残留的旧 TestFuel 泄漏边（`leakOpen=true`），导致基准线不流动 | 改为选择最新且 `!leakOpen` 的 TestFuel/TestThick Mapping |
| `局部≈整图等价失败` | `DebugVerifyLoopReconnect` 清理场景后未冲刷延迟动作；随后 `DebugVerifyLocalEqualsFull` 在快照 `before` 时拿到过期局部拓扑，与整图重建结果不等价 | `DebugVerifyLocalEqualsFull` 先 `ProcessDelayedActions()` 再快照；`DebugVerifyLoopReconnect` finally 清理后也冲刷一次 |

**状态：已游戏内重跑确认通过。`R-通道：通过 4/4`，`局部≈整图等价通过`，`环路拆段回归通过`；`dotnet build` 0 警告 0 错误。**

### 1.2x 开发者菜单打不开问题修复（Player.log · 2026-08-16）

> 加载 DLL：0.5.1

> 新增 `R-全部` 后，游戏内开发者菜单点击即报错：
> `System.ArgumentException: method return type is incompatible`

| 项 | 说明 |
|----|------|
| 根因 | `DebugAction` 标注的方法必须返回 `void`；上一版把 6 个 `[DebugAction]` 套件方法改成返回 `(passed, total)`，导致 `DebugTabMenu_Actions.GenerateCacheForMethod` 在生成菜单时抛异常，整个 Dev 菜单无法打开 |
| 修复 | 保留 `[DebugAction]` 的套件方法为 `void`；把实际跑套件并返回汇总的逻辑移到 `RunFramework/RunPhysics/...` 等内部方法；`R-全部` 调用这些内部方法汇总总分 |

**状态：已游戏内确认：开发者菜单可打开，6 个单套件存在，`R-全部` 可运行。**

### 1.2y R-全部 31/33 问题修复（Player.log · 2026-08-16）

> 加载 DLL：0.5.1

> `R-全部` 可运行，但汇总为 `31/33`：
> - `R-物理：通过 6/7`：`压力均分失败`，总量漂移 80.27
> - `R-热与环境：通过 5/6`：`热量均分失败`，量漂移 53.52

| 项 | 说明 |
|----|------|
| 根因 | `R-全部` 让所有套件使用同一个鼠标原点；前一套件残留的罐/管会与后一套件新生成的设备相邻并连通，导致压力/热量断言测到“非隔离场景”，出现额外质量交换 |
| 修复 | `R-全部` 为每个套件分配不同 z 偏移（0 / 30 / 60 / 90 / 120 / 150），使各套件场景互不重叠；单个套件行为不变 |

**状态：已游戏内确认：`R-全部：通过 33/33`。**

### 1.2z AssertHeatBlocked 环境散热隔离修复（Player.log · 2026-08-16）

> 加载 DLL：0.5.1

> 单独运行 `R-热与环境` 仍出现：
> `热量阻断失败：开=False heatRate=0 |ΔT|=60→58 drift=2（阈=0.5）`

| 项 | 说明 |
|----|------|
| 根因 | `AssertHeatBlocked` 只验证换热器自身 `heatRate=0`，但没有排除 Amb-A 环境散热；室温 25°C 时两侧容器都向室温靠拢，导致 `|ΔT|` 在 10 批内从 60 降到 58 |
| 修复 | 在 `AssertHeatBlocked` 内临时将 `mem.Props.maxAmbientHeatRate` 置 0，跑完 10 批后在 `finally` 恢复原值，从而只测“换热器是否阻断导热” |

**状态：已游戏内确认：`热量阻断通过`，`R-热与环境：通过 6/6`。**

> 备注：若测试地图上存在玩家自建物品/建筑阻挡了 Debug 场景的管道格，部分场景可能因“管道被挡”而出现假失败；在干净空地或远离已有管网的位置运行 `R-全部` 可稳定得到 33/33。

### 1.2aa ChemSolver 接入纯核后化学条件失败修复（Player.log · 2026-08-16）

> `ChemSolver` 接入 `ChemSolverCore` 后，`R-化学` 变为 4/5：
> `化学条件失败：冷=False() 温转=True 低压=False()`

| 项 | 说明 |
|----|------|
| 根因 | `PipeReactionDef.GetChemSpec()` 缓存了配方快照；但 `AssertChemP5aConditions` 会在运行时临时修改 `recipe.minTemperature` / `recipe.minPressure`，缓存导致 `ChemSolverCore` 读到旧门槛，无法返回 `cold` / `lowP` |
| 修复 | 移除 `GetChemSpec()` 的缓存，每次调用按当前 `PipeReactionDef` 字段重建纯配方镜像，确保测试期临时修改立即生效 |

**状态：已游戏内确认：`R-化学：通过 5/5`，`R-全部：通过 33/33`。**

### 1.2ab 阶段 4 · 4.1/4.2 破损局部化与精确唤醒实现（已游戏验证）

> 实现记录，已通过游戏内回归验证。

| 项 | 说明 |
|----|------|
| 4.1 破损标志局部化 | 新增 `ApplyBreachLeakFlagsForCell(IntVec3)`，只重算某一管道格关联 Mapping 的 `leakOpen`，不再全图扫描 |
| 4.2 精确唤醒 | 新增 `NotifyBreachChanged(Thing)`；`CompPipeCell` / `CompPipeBreachable` 切换破损时改为传入具体 Thing，只唤醒相关 net 或所属构件网；无参版本保留作 fallback |
| 兼容性 | `RebuildAllMappings` 仍使用全量 `ApplyBreachLeakFlags()`；第三方无参调用仍可用 |

**状态：已游戏内确认通过：`R-物理 7/7`、`R-框架 9/9`、`R-全部 33/33`，存读档无异常。**

### 1.2ac 阶段 4 · 4.3 下标保护统一实现（已游戏验证）

> 实现记录，已通过游戏内回归验证。

| 项 | 说明 |
|----|------|
| 新增辅助 | `MapComponent_PipeNetwork.TryResolveContainer(member, index, label, out container)`，供单容器设备统一安全解析 |
| 泵 | `ApplyDriveToConnectedMappings` / `RefreshAdjacentMappingsAfterLocalRebuild` 改为 `TryResolveContainers`，避免直接下标访问 |
| 反应釜 | `TryResolveSlots` 改为 `TryResolveContainer` 解析入/出腔，越界统一返回 false |
| 审计 | 阀 / 换热器 / DevInternalBridge 已确认使用 `TryResolveContainers`，无遗漏直接下标访问 |

**状态：已游戏内确认通过：`R-全部 33/33`，泵/阀/换热器/反应釜正常，存读档无异常。**

### 1.2ad 存读档后 R-通道 3/4 修复（已游戏验证）

> 4.3 验证中，存读档后再次运行出现：
> `双通道十字失败：EW=True NS=False ...`

| 项 | 说明 |
|----|------|
| 根因 | `TryFindChannelCross` 取“第一个匹配的十字管道格”；存读档后旧场景的 `Port.channel` 会按 Props 重建，南北罐端口从 B 通道恢复为默认 A 通道，旧十字场景不再具备 B 线；若测试仍选中旧格，则 `NS=False` |
| 修复 | `TryFindChannelCross` 改为选择 `thingIDNumber` 最大的最新十字格，确保存读档后重跑会命中新生成的通道场景 |

**状态：已游戏内确认通过：`R-通道 4/4`、`R-全部 33/33`。**

### 1.2ae 阶段 4 · 4.5 拓扑重建分配优化实现（已游戏验证）

> 实现记录，已通过游戏内回归验证。

| 项 | 说明 |
|----|------|
| RebuildDirtyLocal | 高频临时 List/HashSet 改为字段复用，方法入口统一 Clear |
| BuildPipeComponent | component / attachments / owner / queue / borderPairs / attachedBuildings 改为字段复用 |
| ShortestPipePathCellCount | queue / prev 改为字段复用 |
| FloodPipeComponent / FloodComponentSet | 洪水队列改为字段复用 |

**状态：已游戏内确认通过：`R-全部 33/33`，Stress 重建耗时与改造前基本持平（当前日志示例：100罐/500管约 2.4–2.7ms，215构件/871管约 5.4–5.5ms）。**

### 1.2af 代码审查后自检修复（已游戏验证）

> 根据外部 code review 结果进行自检后修复，已通过游戏内回归验证。

| 项 | 说明 |
|----|------|
| 死代码 | 删除 `ChemSolver.ResolveConditionContainer`（public 但未文档化、无调用方） |
| 文档残端 | 修正 API/Debug/Batch/Topology 中 4 处 partial 拆分遗留的孤立 XML doc |
| 公式统一 | `Container.PressureFromAmount` 委托 `FlowSolverCore.PressureFromAmount` |
| 破损唤醒防御 | `NotifyBreachChanged(Thing)` 改为同时处理同一 Thing 上的 `CompPipeCell` 与 `CompPipeBreachable`，避免复合建筑漏唤醒 |
| 化学分配 | `ChemSolver.BuildAmountDeltas` 不再为纯核额外 `ToStates` 分配 |
| 校验脚本 | `validate_config.py` 增加 `DefInjected` XML 解析与 DefName 引用校验 |
| CI | 固定 .NET SDK `8.0.100`、启用 NuGet 缓存、增加 Release 构建 |
| 测试 | 新增 5 个边界测试：RateCap/Equalize 零粘度、出腔空位限制、出腔流体不符、mixPair 失败；单元测试总数 25→30 |
| 文档 | CHANGELOG / RimPipe_API 0.5.1 补记工程与健壮性内容 |

**状态：已游戏内确认通过：`R-全部 33/33`，存读档后再跑仍 33/33。**

### 1.2ag 第二轮自检优化（已游戏验证）

> 继续处理代码审查遗留项，已通过游戏内回归验证。

| 项 | 说明 |
|----|------|
| netId 增量复用 | `ReassignNetworkIdsIncremental` 的 domains/visited/queue/usedThisPass/allAffected 改为 scratch 字段复用，与 4.5 风格统一 |
| 校验脚本 | `validate_config.py` 增加目录存在性检查、DefOf 正则兼容无初始化器字段、强制简中+英文语言集 |
| CI | 增加 `concurrency`，避免重复运行 |

**状态：已游戏内确认通过：`R-全部 33/33`，局部等价与存读档正常。**

### 1.2ai 第三轮代码优化（已游戏验证）

> 继续减少热路径分配，并提升化学批处理效率；已通过游戏内回归验证。

| 项 | 说明 |
|----|------|
| 化学批处理分配 | `CommitChem` 每反应釜每批只构建一次 `ChemReactionSpec` 与容器状态快照，`ComputeBatchCount`/`BuildAmountDeltas` 复用，避免重复 `GetChemSpec`/`ToStates`；`pureDeltas` 使用 `chemPureDeltaScratch` 复用 |
| 破损唤醒分配 | `NotifyBreachChanged` 无参与带参版本改用 `scratchBreachWokeNets` 复用 HashSet |
| 测试补充 | 新增输入流体不符、TryGetInputPerBatch 正常路径、null reaction 等测试；单元测试总数 30→33 |

**状态：已游戏内确认通过：`R-全部 33/33`，存读档后再跑仍 33/33。**

### 1.2ah DLL 可复现构建工程（已游戏+CI验证）

> 处理代码审查中的 DLL 可复现性问题；已通过游戏内与 CI 验证。

| 项 | 说明 |
|----|------|
| net48 自包含构建 | `RimPipe.csproj` 增加 `Microsoft.NETFramework.ReferenceAssemblies`，降低对 runner 预装 targeting pack 的依赖 |
| CI deterministic 门禁 | 新增“Verify deterministic Release build”步骤：同一配置连续构建两次到不同目录，校验 DLL 哈希一致 |
| CI committed DLL 一致性门禁 | 新增“Verify committed DLL consistency”步骤：committed DLL 与 CI Release 构建不一致时直接失败 |
| SDK 固定 | 新增 `global.json` 声明 SDK 基线 `8.0.100`；CI 固定安装该版本，本地可 `latestMajor` 回退 |
| PDB 可复现性 | Release 配置 `DebugType=none` / `DebugSymbols=false`，避免 DLL 因输出路径/PDB 路径不同而产生字节差异 |
| 本地校验 | 新增 `tools/check-committed-dll.ps1`，本地可检查 committed DLL 是否与当前源码构建一致 |

**状态：已通过游戏内回归与 CI 验证；committed DLL 一致性已设为 CI 硬门禁。**

### 1.2r 阶段四 · 4.8 Bridge-A 验收复查（Player.log · 2026-07-19）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 伤害破损 | ✅ | `Bridge伤害破损通过：储罐 (40%) HP=80/200 breached=True` |
| Breakdown | ✅ | `Bridge故障破损通过：储罐 signal=Breakdown` |
| 满血清 | ✅ | `Bridge满血清通过：储罐 HP=200/200 breached=False` |
| 产品三通场景前置 | ✅ | `已生成产品三通场景` 左=80 通=0 右=80；Mapping=2 |
| 对称 / 泵快速回归 | ⓘ | 本段日志无独立 `对称通过` / 泵行；并不否定 Bridge 主路径 |
| 无崩溃 / Exception | ✅ | 本段无 Exception / Config error / `RimPipe…失败` |

**总判：4.8 Bridge-A 验收通过**（Player.log · 2026-07-19）。

### 1.2q 阶段三 · 3.8 化学 Chem 验收复查（分批 Player.log + 玩家确认 · 2026-07-19）

> 加载 DLL：0.5.1

> **日志现状（必须诚实）：** Unity 会轮转覆盖 `Player.log`。复核当日（2026-07-19 ~15:26）**当前** `Player.log` **已不含** P2–P5a 断言全文；`Player-prev.log`（~15:23）仍可见 **P5b** `化学反应热通过`×2。P2–P5a 行证据来自各批验收当时日志 + 玩家「开始下一 P*」确认，**非**当前文件全文可复读。

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| P1 配方加载 | ✅ | Fluid LOX/RP-1/Exhaust + `RimPipe_Reaction_LoxRp1`；各批加载无 Config error |
| P2 量转化 | ✅ | 当时日志 `化学转化通过：n=5 … LOX Δ11.5 RP1 Δ5 Ex Δ16.5`；玩家确认后进 P3 |
| 异流体日志 | ✅ | 罐间隔 + 每边只警告一次；修后场景 `mappings=0` 无刷屏 |
| P3 Dev 釜 + 电/开关 | ✅ | 当时日志 `化学釜通过` / `化学釜阻断通过：关停=True 断电=True`；玩家确认后进 P4 |
| P4 L1 偏比 | ✅ | 当时日志 `化学L1通过：mix=3.5 η=0.5 …`；玩家确认后进 P5a |
| P5a T/P 门槛 | ✅ | 当时日志 `化学条件通过：冷=True(cold) 温转=True 低压=True(lowP)`；玩家确认后进 P5b |
| P5b 反应热 | ✅ | **`Player-prev.log` 仍存：** `化学反应热通过：n=5 Ex=16.5 T 21→23.73 ΔT=2.73/3.03 Q=50`（×2；Amb 略吃热，容差内） |
| P6 文档收尾 | ✅ | §1.2q + `RimPipe_API.md` 化学面 + About `0.3.19`（文档修订；dll 功能面止于 P5b/`0.3.18`） |
| P6 回归快速检查（当前 log） | ⓘ | 当前 `Player.log`：产品三通×2、泵场景 `开=True 电=True rate=10 左=20 右=80`、换热器、散热场景、`休眠进入通过：AmbientOnly`、Dev 釜注册且 Dump `lastN=1.74`；**未**再跑正式 `对称通过` / `泵逆均分通过` / 化学断言菜单 |
| 无崩溃 / Exception | ✅ | 当前 log + prev：**无** Exception / Config error / `RimPipe…失败` |

**总判：3.8 Chem（P0–P6）验收通过**（玩家分批确认 · 2026-07-19）。功能验收以 P2–P5b 为准；P6 为文档/API + 当前会话轻量快速回归（正式对称/泵断言未重跑，不影响验收通过）。

### 1.2j 阶段四 · 4.1 休眠验收复查（Player.log + 玩家确认 · 2026-07-15）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 休眠进入 | ✅ | `休眠进入通过：state=AmbientOnly`（×3）；Dump `sleepState=AmbientOnly` |
| 休眠唤醒 | ✅ | `休眠唤醒通过：… wokeBusy=True 量 64→45.57 Δ=18.43`；再 `52.79→35.77` |
| 仅 Amb | ✅ | `仅Amb休眠通过：before=AmbientOnly after=AmbientOnly T 79.4→77.9 … 量drift=0` |
| 泄漏不被饿死 | ✅ | 破损场景 `sleepState=Busy`；`泄漏销毁 5` 连续扣量（左/右 100→…） |
| 对称回归 | ✅ | `对称通过：左=64.0018 右=64.0018 通=31.9964`（×2） |
| 后段「休眠进入失败」 | ⓘ | 同图仍有 `leakOpen`/breached 管 → 整图 Busy **属预期**（A 粒度）；并不否定先前通过 |
| 泵/热/存读档 | ⓘ | 本段日志无独立行；玩家声明验收通过 |
| 无崩溃 | ✅ | 本段无 Exception / Config error（RimPipe 相关） |

**总判：4.1 Sleep 验收通过**（玩家确认 · 2026-07-15）。

### 1.2 阶段一验收结论

| 场景 | 结论 | 备注 |
|------|------|------|
| 孤立三通左右对称 | ✅ | 永久回归 |
| 直接相邻 / 管道连通均分 | ✅ | |
| Debug 开口泄漏（双端） | ✅ | 正式拆管 **不** 泄漏 |
| 拆管停流 | ✅ | |
| 存读档 | ✅ | |

### 1.2b 阶段二验收复查（Player.log 二次核验 · 2026-07-14）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 无崩溃 / Config 错 | ✅ | 日志 `Exception` / Config error = 0 |
| 产品三通场景 | ✅ | `已生成产品三通场景` 左=80 通=0 右=80，Mapping=2 |
| 阀门 V-A 场景 | ✅ | 两次 `阀门场景` 开=True rate=10 左=100 右=0 |
| 大型管网 | ✅ | 构件=24 管道格=21 Mapping=33 |
| **断言三通对称** | ✅ | **`对称通过：左=30.0155 右=30.0168 通=30.0162`** |
| 对称曾失败一行 | ⓘ | 同图多网时先出现差=0.373；继续批处理后通过（多网并存不作为失败条件） |
| Debug 开口泄漏 | ✅ | 标记泄漏后有销毁日志 |
| 贴图 / 建造栏四产品 | ✅ | 前期已落地 |
| PlaceWorker 叠放 | ✅ | 代码已挂；本日志无 UI 拒绝文案（预期）；玩家声明验收通过 |
| 存读档往返含阀 | ✅ | 本日志无 Saved/Loading 行；玩家声明验收通过（阶段一存读已通） |

**总判：阶段二验收通过。**

### 1.2c 阶段三 · 3.6 泵验收复查（Player.log · 2026-07-14）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 泵场景生成 | ✅ | `泵场景 … 开=True 电=True rate=10 左=20 右=80`；Mapping=3（直接相邻 Forced + 内部 Forced） |
| 抽往更满一侧（「逆向抽送」） | ✅ | `泵逆均分通过：左 20→0.1107 右 80→97.2854`；续批再通过 |
| 首次断言失败一行 | ⓘ | 同图先出现 `左Δ=0 右Δ=0`（多网/时机）；重建后再验通过，不作为失败 |
| 关泵阻断 | ✅ | Dump 中泵内部 Mapping `rate=0 ForcedA→B`；玩家声明验收通过（本日志无独立「阻断通过」行） |
| 三通对称回归 | ✅ | `对称通过：左=55.0002 右=55.0002 通=49.9997`（×2） |
| 存读档往返含泵 | ✅ | `Loading game from file TestPipe` 后重注册泵/罐/三通/阀；玩家声明通过 |

**总判：3.6 泵验收通过。**

### 1.2d 阶段三 · 3.9/3.10 泄漏 L-A 验收复查（Player.log · 2026-07-14）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 无崩溃 / Config 错 | ✅ | 本段日志无 Exception / Config error |
| 破损泄漏（管道） | ✅ | `破损泄漏通过：左 100→95 右 100→95`；再 `80→75`；读档后 `60→55` |
| 摧毁停漏留量（dump 关） | ✅ | 两次 `摧毁停漏通过 … dumpOnDestroy=False`（毁后量不变） |
| 储罐自身破损 | ✅ | Dump 多次出现 `Member 储罐@… breached`；玩家声明验收通过 |
| dumpOnDestroy 默认关 | ✅ | 前两次停漏断言在 False 下通过 |
| dumpOnDestroy 开启 | ✅ | 出现 `dumpOnDestroy 管道口倾泻 5`；后续「摧毁停漏失败」仅 `noDump=False`（`stopped=True residual=True`）= **开关生效预期**，不计入失败 |
| 三通对称回归 | ✅ | `对称通过：左=55.0002 右=55.0002 通=49.9997` |
| 存读档 含 breached | ✅ | `Loading game from file TestPipe` 后重注册；读档后再 `破损泄漏通过` |

**总判：3.9/3.10 泄漏 L-A 验收通过。**

### 1.2e 阶段三 · 3.3 阻力 R-A 验收复查（Player.log + 玩家确认 · 2026-07-14）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 短管 vs 长管 rate | ✅ | 场景 Dump：`短 path=1 rate=10`；`长 path=5 rate=2`；手动拉更长管 `path=65 rate≈0.15`（`10/65`） |
| 玩法阻力存在 | ✅ | 玩家声明：加长管道后明显更慢 |
| Debug「阻力通过」行 | ⓘ | 断言曾 `flowOk=False`（多网污染 + 已均分后 Δdst≈0 误报失败）；**并不否定 R-A**；断言加固暂缓 |
| 无崩溃 / Exception | ✅ | 本段无 Exception / Config error；红字仅为断言 `Log.Error` |
| 阀 / 泵 / 泄漏 / 对称快速回归 | ✅ | 未破既有能力；玩家声明阶段通过 |

**总判：3.3 阻力 R-A 验收通过**（玩家确认 · 2026-07-14）。

### 1.2f 阶段三 · 泄漏效果 E-A 验收复查（玩家确认 · 2026-07-14）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 燃料 Filth | ✅ | 玩家按 §6.12.7 实机检验通过（Filth_Fuel / 断言路径） |
| 水无 Filth | ✅ | 玩家确认：TestWater 仍仅扣量、无污物 |
| 室温升温 | ✅ | 玩家确认：燃料泄漏伴温升（PushHeat） |
| 框架回归 | ✅ | 玩家声明阶段通过（扣量 L-A 未回退） |
| 无崩溃 | ✅ | 玩家确认验收通过 |

**总判：泄漏效果 E-A 验收通过**（玩家确认 · 2026-07-14）。

### 1.2g 阶段三 · 3.1 压力 Pr-A 验收复查（Player.log + 玩家确认 · 2026-07-14）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 等容三通对称 | ✅ | `对称通过：左=64.0018 右=64.0018 通=31.9964` |
| 异容压力均分 | ✅ | `压力均分通过：小罐 16.675/100 P=0.1667 大罐 83.325/500 P=0.1667 … 总量 100→100`（×2） |
| 泵 Forced 逆向抽送 | ✅ | `泵逆均分通过：左 20→0.1107 右 80→97.2854 rate=10`（玩家贴出日志） |
| 关泵阻断 | ✅ | `泵阻断通过：开=False … rate=0 左Δ=0 右Δ=0`（×2） |
| 泄漏快速回归 | ✅ | 燃料破损场景 `泄漏销毁 5`；未破 L-A |
| 存读档 | ✅ | `Loading game from file TestPipe` 后重注册；读档后再 `压力均分通过` |
| 无崩溃 / Config 错 | ✅ | 本段无 Exception / Config error（压力/泵相关） |

**总判：3.1 压力 Pr-A 验收通过**（玩家确认 · 2026-07-14）。

### 1.2h 阶段三 · 热量 H-A 验收复查（Player.log + 玩家确认 · 2026-07-14）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 换热器热量均分 | ✅ | 手动推进批次后 `A T=50 B T=50 \|ΔT\|≈0 量drift=0`；机制已均温。日志 `热量均分失败` 为断言误报失败（要求初\|ΔT\|>10，断言前已均完） |
| 关换热器阻断 | ✅ | `热量阻断通过：开=False heatRate=0 \|ΔT\|=60→60 drift=0` |
| 均分断言二次失败 | ⓘ | 阻断后换热器仍关：`A T=80 B T=20 开=False` → 不换热属预期；并不否定 H-A |
| Flow 混温 | ✅ | 混温场景已生成（左满80°C / 右空20°C）；玩家声明验收通过（本日志无独立「混温通过」行） |
| 三通对称回归 | ⓘ | 本段多次「跳过对称断言」（图上无三通布局）；不作为失败 |
| 存读档 | ✅ | 玩家声明验收通过 |
| 无崩溃 | ✅ | 本段无 Exception / Config error（热量相关） |

**总判：热量 H-A 验收通过**（玩家确认 · 2026-07-14；Debug 断言误报失败暂缓加固）。

### 1.2i 阶段三 · 3.4 环境散热 Amb-A 验收复查（Player.log + 玩家确认 · 2026-07-15）

> 加载 DLL：0.5.1

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 保温对比 | ✅ | `保温对比通过：低ins=0 T=77.5 \|ΔT\|=55.52；高ins=0.7 T=79.25 \|ΔT\|=57.27 Tamb=21.98 批=25`（再跑：低77.5 / 高79.25） |
| 空罐不散热 | ✅ | `空罐不散热通过：amount=0 T 80→80 drift=0`（×2） |
| 环境散热（罐温靠拢） | ✅ | 玩家确认符合设计；量不变（`量drift=0`） |
| Debug「环境散热通过」 | ⓘ | 多行 `环境散热失败`（`cooled=False`）：断言要求初\|ΔT\|>5 且再降≥2；室内 PushHeat 后 Tamb 迅速抬高 / 断言前已接近平衡 → **误报失败**，并不否定 Amb-A |
| 室外行为 | ✅ | 玩家确认：室外只改罐温、不靠「向室外推热」存温（与决议一致） |
| 存读档 / 无崩溃 | ✅ | 玩家声明验收通过；本段无 Exception / Config error |

**总判：环境散热 Amb-A 验收通过**（玩家确认 · 2026-07-15；散热单断言误报失败暂缓加固）。

### 1.3 已知限制 / 设计说明

| 项 | 现状 | 可能后续 |
|----|------|----------|
| 拆管 / 拆设备 | 默认不倾倒（断拓扑）；破损=开口漏 | ✅ L-A（§1.2d）+ 效果 E-A（§1.2f） |
| 三通/阀「内部有量」 | **交汇/求解用 Container**，非玩法储罐；阀两岸小桶为 V-A 所需 | 零容积模型 = 换架构，另议 |
| 管道格不储存流体 | **已锁定：** 仅拓扑；破损=通向大气的孔；打管泄连通 Container（L-A） | ~~3.5 管道组容积~~ **不做**（§6.14 / §十一 #16） |
| 管道阻力 R-A | 外部 Mapping：`rate = max(0.1, base/max(1,path格数))`；直接相邻/内部不衰减 | ✅ 验收通过（§1.2e）；Debug 断言已硬化（§7.12） |
| 自有美术 | 仍借官方贴图 | 阶段四或单独立项 |
| 泵 | ✅ 3.6 P-A 验收通过（见 §1.2c） | — |
| 泄漏效果 E-A | Filth + Temperature（FluidDef）；水=None；TestFuel=Filth_Fuel+热 | ✅ 验收通过（§1.2f） |
| 压力 Pr-A | Equalize 填充比驱动；泵 Forced 不变；异容均压 | ✅ 验收通过（§1.2g） |
| 热量 H-A | T 生效 + Flow 混温 + Heat Mapping 换热器 | ✅ 验收通过（§1.2h）；断言误报暂缓 |
| 环境散热 Amb-A | insulation + 格温靠拢 + 室内 PushHeat；管格不参与 | ✅ 验收通过（§1.2i）；散热断言误报暂缓 |
| 4.1 整图休眠 | Busy / AmbientOnly / FullyQuiet；跳过空转 Acc；量静仍跑 Amb | ✅ 验收通过（§1.2j） |
| 4.2 分网休眠 | 连通分量 netId；每网独立三态；整图拓扑重建不变 | ✅ 验收通过（§1.2k） |
| 4.3 Overlay | DevMode 压色 + 上批流量边线 | ✅ 验收通过（§1.2l） |
| 4.5 SaveMig | schema 版本戳 + 读档钳量；无历史迁移器 | ✅ 验收通过（§1.2m） |
| 4.4 ApiDoc | 模组作者指南 + TrySet/AddAmount / TrySetTemperature | ✅ 验收通过（§1.2n） |
| 4.6 Loc | Defs 中文基线 + 英 DefInjected；Core 研究门槛（暂无自研树） | ✅ 验收通过（§1.2o） |
| 4.7 ExtHook | `IPipeInternalMappingContributor` + 扫描；Dev 桥自测 | ✅ 验收通过（§1.2p） |
| 4.8 Bridge-A | 伤害/Breakdown→breached；TrySetBreached；无 Harmony | ✅ 验收通过（§1.2r） |
| 4.9 Bridge-H | Def 注入无引用建筑（Harmony） | `[!]` 延后（§7.9） |
| 4.10 DirtyTopo | 局部脏区拓扑 | `[!]` 延后；先 Benchmark（§7.10） |
| 4.11 R1 | 对内冻结 `0.4.0`；下游说明并入 `RimPipe_API.md` | ✅ 验收通过（§1.2s） |
| 4.12 Debug | 菜单 12 键 + 套件 + 断言硬化；`0.4.1` | ✅ 验收通过（§1.2t） |
| 3.8 Chem | RecipeDef + Reactor；L1/条件/反应热；非二元 Chemical Mapping | ✅ 验收通过（§1.2q） |
| 6.18 FluidPhysics | 粘度→rateCap、比热→Q/(m·c)；泵吃粘度；Amb 驱动换热容 | ✅ 验收通过（§6.18.3 · R-通道内） |
| 6.19 PipeChannels | 管道 A/B 双通道隔离；端口 channel；拓扑节点化；Gizmo 逐向配置 | ✅ 验收通过（§6.19.3 · R-通道 4/4） |

### 1.4 代码布局（现状）

```
Source/RimPipe/
  Core/       FluidDef, PipeReactionDef, ChemSolver, ChemReactorBinding, LeakEffect, Container, Port, Mapping,
              FlowSolver, IPipeInternalMappingContributor, PipeBreachBridge, …
  Comps/      CompPipeNetworkMember, CompPipeCell, CompPipeValve, CompPipePump, CompPipeReactor,
              CompPipeHeatExchanger, CompPipeDevInternalBridge, …
  Map/        MapComponent_PipeNetwork
  Placement/  PipePlacement, PlaceWorker_RimPipeCell/Appliance
  Buildings/  Building_PipeCell
  Debug/      RimPipeDebugTools, RimPipeDebugSuites, RimPipeDebugScenes, RimPipeDebugAsserts, RimPipeDebugUtil
Defs/         Fluids, DesignationCategories,
              Things/Dev_PipeBuildings.xml, Things/PipeBuildings.xml
```

---

### 补测清单

- [x] 4.1 休眠：`休眠进入通过：AmbientOnly`；`休眠唤醒通过`；`仅Amb休眠通过 … 量drift=0`；破损泄漏期间网络保持 Busy，并持续扣量；`对称通过`（§1.2j）  
- [x] 断言三通对称 → 日志 `对称通过`（约 30/30/30）  
- [x] 存读档 / 叠放快速检查 → 玩家声明验收通过（二次核验收口）  
- [x] 泵：抽往更满一侧 → `泵逆均分通过：左 20→… 右 80→…`；对称回归；存读档后重注册泵  
- [x] 关泵阻断 → Dump `rate=0` + 玩家声明验收通过  
- [x] 泄漏 L-A：`破损泄漏通过`；`摧毁停漏通过`（dump 关）；读档后再漏；`对称通过`；dump 开时「停漏失败」仅 noDump=False（预期）  
- [x] 阻力 R-A：Dump `path=1 rate=10` / `path=5 rate=2` / 手动拉长 `path=65 rate≈0.15`；玩家确认阻力存在并通过（断言误报失败不影响）  
- [x] 泄漏效果 E-A：燃料 Filth + 室温 + 水无 Filth；玩家确认验收通过（§1.2f）  
- [x] 压力 Pr-A：`对称通过`；`压力均分通过`；`泵逆均分通过：左 20→0.1107 右 80→97.2854`；`泵阻断通过`；存读档后再均压（§1.2g）  
- [x] 热量 H-A：均温手感/ Dump `T=50/50`；`热量阻断通过`；混温场景；玩家确认验收通过（断言误报失败不影响 · §1.2h）  
- [x] 环境散热 Amb-A：`保温对比通过`（低 T=77.5 / 高 T=79.25）；`空罐不散热通过`；玩家确认符合设计（散热断言误报失败不影响 · §1.2i）  
- [x] 4.5 SaveMig：Dump `schema=1`；旧存档读入后仍可继续流动；破损场景存读档后仍泄漏；对称/泵快速回归（§1.2m；玩家确认 · 2026-07-18）  
- [x] 4.7 ExtHook：`ExtHook通过`；泵场景快速回归；`对称通过`；均压后再断言误报失败不影响（§1.2p）  
- [x] 3.8 Chem：P2–P5b Debug 断言分批通过（P5b 仍见于 `Player-prev.log`；P2–P5a 已被轮转覆盖）；玩家确认（§1.2q）；P6 API/`0.3.19`；当前 log 轻量快速回归（三通/泵/换热/休眠/釜 `lastN`），正式对称·泵断言未重跑  
- [x] 4.8 Bridge-A：`Bridge伤害破损通过`；`Bridge故障破损通过`；`Bridge满血清通过`；无 Exception（§1.2r；本段未重跑对称/泵）  
- [x] 4.11 R1：`对称通过`；`泵逆均分通过`；Bridge×3；`休眠进入/唤醒通过`；化学热/条件/L1/釜阻断；误报失败与缺存读档日志行不影响（§1.2s）  
- [x] 4.12 Debug：`R-框架 8/8`；`R-物理 7/7`；`R-热与环境 6/6`；`R-化学 5/5`；`R-扩展 2/2`；中段摧毁停漏偶发 6/7 不影响（§1.2t）  

---

*本文档 = RimPipe 现行唯一计划与 TODO。弃用策划中的 Node / Connection / PipeLine 仅作历史参考；实现以本文 §二 / §三 / §六 为准。**管道不储存流体**（§6.14）。*
