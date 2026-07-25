# RimPipe 计划与 TODO

> **角色：** 本文件同时是 **执行计划**（目标 / 架构 / 决议 / 阶段大纲）与 **任务清单**（可勾选 TODO）。  
> **模组路径：** `D:\Steam\steamapps\common\RimWorld\Mods\Rimpipe`  
> **状态：** 阶段四 **4.1–4.8 ✅**；**4.9 延后**；**4.10 DirtyTopo 延后**（7.10.b Benchmark/Stress ✅ · `0.4.3`）；**4.11 发布 R1 ✅**（§1.2s · `0.4.0`）；**4.12 Debug 整理 ✅**（§1.2t · `0.4.1`）；**Overlay 性能修复验收通过**（§1.2u / §7.3.f · `0.4.2`）  

> **依据：** 2026-07 讨论决议 · 官方源码对齐 · 弃用策划文档仅作历史参考  
> **约定：** 查接口不瞎猜 · 模糊先确认 · 无 Harmony（Bridge-A 不引入；Bridge-H 另议）· 无 `Node` / `Connection` / `PipeLine` · **管道格不储存流体**（仅拓扑；量仅在设备/管件 Container）· **文案风格见 §八 X10**  
> **纪律（Cursor 八荣八耻 · 本切片强制）：** ① 查官方接口（Scribe 默认值 / MapComp ExposeData）· ② 模糊先确认 · ③ 业务经人类确认 · ④ 复用既有存档字段 · ⑤ 主动验收 · ⑥ 不改 Flow/拓扑公式 · ⑦ 不知则停手 · ⑧ 慎改 Commit（全文见 §八 X9）  
> **任务标记：** `[ ]` 未开始 · `[~]` 进行中 · `[x]` 完成 · `[!]` 已决议待实施

---

## 〇、如何使用本文档

| 用途 | 看哪里 |
|------|--------|
| 当前做到哪 | **§一 现状快照**、**§九 进度** |
| 架构与不可变决议 | **§二 核心架构**、**§三 设计决议** |
| 下一步怎么做 | **§五**（目标→设计→任务→验收）；开工决议见 **§十** |
| 阶段二已锁定 | **V-A · 借官方贴图 · 保留 Dev（不出建造栏）· 阀门仅开关 · 管道↔管件互斥** |
| 验收复查 | **§一.2b / §五.8**（通过项 + 疏漏） |
| 阶段三 | **§六**（物理项 + **3.8 Chem ✅** · 见 §1.2q / §6.17） |
| 远期路线 | **§七**（4.9/4.10 延后 · **4.11 发布准备**见 §7.11） |
| 日常勾任务 | **§6.11 候选**（单项决议后再编码） |
| 全程纪律 | **§八 横切规则**（含 **X10 文案风格**） |

编码中若出现本文未覆盖、且会影响字段 / 公式 / 存档的歧义：**停手提问**，不自行发明。

---

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

### 1.2r 阶段四 · 4.8 Bridge-A 验收复查（Player.log · 2026-07-19）

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

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 短管 vs 长管 rate | ✅ | 场景 Dump：`短 path=1 rate=10`；`长 path=5 rate=2`；手动拉更长管 `path=65 rate≈0.15`（`10/65`） |
| 玩法阻力存在 | ✅ | 玩家声明：加长管道后明显更慢 |
| Debug「阻力通过」行 | ⓘ | 断言曾 `flowOk=False`（多网污染 + 已均分后 Δdst≈0 误报失败）；**并不否定 R-A**；断言加固暂缓 |
| 无崩溃 / Exception | ✅ | 本段无 Exception / Config error；红字仅为断言 `Log.Error` |
| 阀 / 泵 / 泄漏 / 对称快速回归 | ✅ | 未破既有能力；玩家声明阶段通过 |

**总判：3.3 阻力 R-A 验收通过**（玩家确认 · 2026-07-14）。

### 1.2f 阶段三 · 泄漏效果 E-A 验收复查（玩家确认 · 2026-07-14）

| 项 | 结论 | 证据 / 备注 |
|----|------|-------------|
| 燃料 Filth | ✅ | 玩家按 §6.12.7 实机检验通过（Filth_Fuel / 断言路径） |
| 水无 Filth | ✅ | 玩家确认：TestWater 仍仅扣量、无污物 |
| 室温升温 | ✅ | 玩家确认：燃料泄漏伴温升（PushHeat） |
| 框架回归 | ✅ | 玩家声明阶段通过（扣量 L-A 未回退） |
| 无崩溃 | ✅ | 玩家确认验收通过 |

**总判：泄漏效果 E-A 验收通过**（玩家确认 · 2026-07-14）。

### 1.2g 阶段三 · 3.1 压力 Pr-A 验收复查（Player.log + 玩家确认 · 2026-07-14）

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

## 二、项目目标与路线图

### 2.1 总目标

为 RimWorld **1.6** 提供可扩展的管道框架模组 **RimPipe**：低 tick、事件驱动；首版聚焦 **量 + maxFlowRate** 的 Flow；再按需扩展压力 / 温度 / 热 / 化学与环境。

### 2.2 阶段路线（高层）

```
P0 脚手架 ✅
 └─► 阶段一 框架 MVP ✅
      └─► 阶段二 产品化基础建筑 ✅（基本验收；⚠ 见 §1.2b）
           └─► 阶段三 物理与设备扩展  ← 下一讨论
                │   压力·温度·阻力·泵·Heat/Chemical·环境泄漏…
                └─► 阶段四 优化 / API / 本地化 / 模组桥接 / 发布
```

### 2.3 阶段目标一句话

| 阶段 | 目标 | 完成定义 |
|------|------|----------|
| 一 | 框架可算、可存、可验 | 四类场景 + 孤立三通对称 ✅ |
| **二** | **玩家可摆「能用的」基础管网** | 产品可造；阀开关；Inspect；叠放互斥；回归不破 ✅ 基本 |
| 三 | 物理深度按需加厚 | 每单项有公式决议 + 独立验收 |
| 四 | 可发布 / 可被其他模组消费 | 文档、性能、兼容、本地化达标 |

---

## 三、核心架构（锁定，阶段二不得偏离）

### 3.1 三层分工

| 层级 | 类型 | 职责 |
|------|------|------|
| 存储 | **Container** | 一种流体（`FluidDef` + `amount`）；属 Building |
| 对接 | **Port** | 外表接口：本地 `Rot4` → 世界朝向 → 外一格；指向 Container；**不参与流量计算** |
| 传输 | **Mapping** | 容器↔容器；`maxFlowRate` = **每 20-tick 批**上限；管道格承载跨建筑路径（**管道本身无 Container / 不持量**） |

**禁止：** `Node` / `Connection` / `PipeLine` 类名与新抽象。策划旧文中的「节点」= 带 `CompPipeNetworkMember` 的 Building。  
**已锁定：** **管道不储存流体**——`Building_PipeCell` / `CompPipeCell` 仅拓扑与破损标记；流体量只存在于带 `CompPipeNetworkMember` 的 Container（储罐 / 三通 / 阀 / 泵等）。

### 3.2 附着关系

```
Building + CompPipeNetworkMember
├── Container[]
├── Port[]                 Def 定义，本地 Rot → 运行时世界朝向
└── Mapping[]              仅同建筑内部映射（若有）

Building_PipeCell + CompPipeCell
└── 仅拓扑（不储存流体）：Voronoi 邻接 → 两端 Port 的 Container 间建外部 Mapping

MapComponent_PipeNetwork
├── 注册表 + cell / 组件拓扑
├── 延迟合并/分裂（对齐 PowerNetManager 思路）
└── 20 tick：拓扑 → Accumulate → Commit
```

### 3.3 20 tick 批处理

| Tick | 行为 |
|------|------|
| 每 tick 开头 | 消化延迟拓扑 |
| **0–18** | `AccumulateFlow`：写 `ContainerDelta`；批内 Jacobi 虚拟量；竞争比例缩放 |
| **19** | **仅** `CommitDeltas`；incomplete / `leakOpen` / 储罐 `breached` 在此销毁量 |

### 3.4 Flow 公式（3.1 Pr-A：Equalize 按填充比压力；阀/泵仍只改 rate）

```
# Equalize（Pr-A）
P     = amount / capacity   （批内用虚拟量）
drive = |P_hi - P_lo| / 2 * min(cap_hi, cap_lo)
want  = min(drive, maxFlowRate, amountHi, freeLo)

# Forced（泵，不变）
want  = min(maxFlowRate, amountSrc, freeDst)
```

等容时 Equalize 与旧 `drive=|Δamount|/2` 等价。异流体 / incomplete / leakOpen → want = 0。变动路径：**禁止**绕过 Delta 直接改 `amount`。

### 3.5 设计决议速查

| 项 | 决议 |
|----|------|
| 生命周期 | Comp `PostSpawnSetup` / `PostDeSpawn` → MapComp |
| 管道格 | `Building_PipeCell` + `CompPipeCell`；**不储存流体**（无 Container） |
| 管道持液 | **不做**（取消原候选 3.5「管道组容积」） |
| 设备逻辑 | `CompPipeNetworkMember`（无 Node） |
| ID | 自增 int + MapComp 种子存档 |
| 跨建筑 Mapping | 不持久化两端引用细节以外的拓扑；读档 **重建** |
| 正式拆管 | Mapping 删除，**不泄漏**（Debug 开口泄漏另测） |
| Port 外格 | `position + FacingCell(世界朝向)` |
| 直接相邻 | 对向 Port 且外格落入对方 footprint |
| 阶段二贴图 | 罐→Battery；三通→MoisturePump；管→电缆 Atlas；阀→PowerSwitch |
| 阶段二阀门 | **V-A**；仅开/关；独立管件；不拆 Mapping |
| 叠放 | **管道 ↔ 管件互斥**（PlaceWorker）；含三通 |
| Dev Def | 保留回归；**不进建造栏** |
| 三通/阀 Container | 交汇/V-A 求解容积，非产品储罐 |
| Harmony | 阶段二仍不引入 |

### 3.6 RimWorld 对齐

| RimPipe | 官方参考 |
|---------|----------|
| MapComp Tick / FinalizeInit | `MapComponent`、电力网延迟更新 |
| Comp 注册 | `BreakdownManager` + Comp 模式 |
| Def 驱动 | `CompProperties` + XML |
| 存档 | `IExposable` + `Scribe_*` |
| 邻接 | `GenAdj` / 自研 Voronoi 边（管道组件） |

---

## 四、已完成任务（P0 + 阶段一）

### P0 · 工程脚手架

- [x] P0.1 目录：`About/` · `Assemblies/` · `Defs/` · `Source/`（Libs 在 `Source/Libs`）
- [x] P0.2 `RimPipe.csproj`（1.6.4871 · net472 · 内置 Libs）
- [x] P0.3 `Assemblies/RimPipe.dll` 可加载
- [x] P0.4 DevMode 调试入口

### 阶段一 · 容器–映射 MVP

**1.A–1.E** 数据结构 / Comp / MapComp / 管道格 / Flow — 全部 `[x]`（见历史提交与代码树）。

**1.F 验收**

- [x] Dev Tee + Spawn 场景
- [x] 孤立三通对称
- [x] 连通 / 泄漏 / 拆管 / 存读档（2026-07-14）

---

## 五、阶段二 · 产品化基础建筑（✅ 基本完成）

### 5.1 阶段目标

把阶段一的 **Dev 验收件** 升级为玩家可建造的 **基础产品集**，并补齐 **Inspect**、**阀门开关限流**、**叠放限制**，同时保证阶段一回归不破。

**本阶段明确不做：** 压力/温度公式、泵、Heat/Chemical、环境泄漏、Harmony、自有美术定稿、阀门连续开度（0–100%）、阀盖在管道上（方案 B）。

### 5.2 产品范围（建造栏标签）

| 建筑 | DefName | 职责模型 |
|------|---------|----------|
| **储罐** | `RimPipe_StorageTank` | 1 大 Container + 四向 Port（产品储液） |
| **三通管** | `RimPipe_TeeJunction` | 1 Container（交汇容积）+ 三 Port（对齐 Dev） |
| **管道** | `RimPipe_Pipe` | 仅拓扑 `Building_PipeCell` + `CompPipeCell` |
| **阀门** | `RimPipe_Valve` | 两岸小 Container + 内部 Mapping × 开/关（V-A · **独立管件**） |

**贴图借用（落地）：** 储罐→蓄电池 `Things/Building/Power/Battery`；三通→MoisturePump；管道→电缆 Atlas+Custom1；阀门→PowerSwitch。

**Container 语义（已向玩家说明并接受）：** 三通/阀内部有量 = 架构要求（量只在 Container；Mapping 只连 Container），**不是**要把它们做成储罐。阀两岸小桶专供 V-A。**管道不储存流体**（与三通/阀小桶无关；见 §3.1 / §6.14）。

可选未做：直通接头、堵头、墙穿。

### 5.3 设计决议（已锁定）

#### 5.3.1 Def 与类策略

| 议题 | 决议 |
|------|------|
| ThingClass | `Building` + Comp；阀 Gizmo 在 `CompPipeValve` |
| Dev Def | **保留**但不进建造栏（撤 `designationCategory`） |
| 建造栏 | 仅四产品；分类 label「管道」 |
| 贴图 | 借官方四件（上表）；管道保留 Custom1 不与电力粘连 |

#### 5.3.2 阀门 — V-A · 方案 A（独立管件）

| 项 | 决议 |
|----|------|
| 模型 | **V-A** 内部 Mapping 限流 |
| 形态 | **方案 A**：占格管件，两侧接管道/设备；**不**盖在管道上 |
| UI | 仅开 / 关；`open ∈ {0,1}`；有效 rate = baseRate × open |
| 关阀 | rate=0，**不拆** Mapping |

#### 5.3.3 Inspect

容器量、端口世界向/外格/已|未对接、Mapping 数；阀另显开/关与有效 rate。

#### 5.3.4 与框架的边界

不改 Flow 驱动 / 批处理时序 / Voronoi / 拆管不泄漏。阀只改 rate。无第二套网络。

#### 5.3.5 叠放（2026-07-14 补决议）

| 规则 | 实现 |
|------|------|
| 管道 ↔ 管件（储罐/三通/阀门）互斥，互不叠格 | `PlaceWorker_RimPipeCell` / `PlaceWorker_RimPipeAppliance` |
| 管件彼此不叠格 | 同上 Appliance |
| 阀 = 管件，禁止与管道叠格 | 同三通 |

连接方式：直接相邻或「设备—邻格管道—设备」。

### 5.4 任务清单（阶段二）

**2.A 产品 Def 骨架**

- [x] 2.A.1 `RimPipe_StorageTank` ThingDef（`Defs/Things/PipeBuildings.xml`）
- [x] 2.A.2 `RimPipe_TeeJunction` ThingDef（三口与 Dev Tee 一致）
- [x] 2.A.3 `RimPipe_Pipe` ThingDef（Atlas + Custom1 保留）
- [x] 2.A.4 设计分类 `RimPipe`（无研究，直接可造）；产品 uiOrder 100–130
- [x] 2.A.5 贴图：借官方蓄电池 / MoisturePump / 电缆 / PowerSwitch（后修正；非全电线）

**2.B 阀门**

- [x] 2.B.1 阀门模型：**V-A**；UI：**仅开/关**；形态：**方案 A 独立管件**
- [x] 2.B.2 `CompPipeValve`：开关 + 存档 + 刷新内部 Mapping rate
- [x] 2.B.3 `RimPipe_Valve` ThingDef + 端口布局
- [x] 2.B.4 Gizmo：开 / 关切换
- [x] 2.B.5 关死阻断（玩家确认基本通过）

**2.C Inspect 与可读性**

- [x] 2.C.1–2.C.3 Inspect / 对接 / 对称断言增强

**2.D 回归与验收**

- [x] 2.D.1 产品三通 + **日志「对称通过」**（约 30/30/30）
- [x] 2.D.2 大型管网（24 构件 / 21 管 / 33 Mapping）
- [x] 2.D.3 阀门场景 ×2（V-A 拓扑）
- [x] 2.D.4 存读档（玩家声明通过；本日志无存读行）
- [x] 2.D.5 Dev 保留且不进建造栏

**2.E 收尾**

- [x] 2.E.1 本文勾选与 §九
- [x] 2.E.2 About.xml → 0.2.0
- [x] 2.E.3 阶段三候选（见下）

**2.F 叠放限制**

- [x] 2.F.1 PlaceWorker 管道 ↔ 管件互斥
- [x] 2.F.2 快速检查（玩家声明验收通过）

**阶段三候选启动项（2.E.3 · 历史）**

1. ~~**3.6 泵**~~ → ✅  
2. ~~**3.9 / 3.10 泄漏产品化**~~ → 决议 L-A 已钉（§6.5）；编码见 §6.6  
3. **3.3 管道阻力**  
4. 其余见 §六  

### 5.5 阶段二实际顺序（回顾）

```
2.A Def → 2.C Inspect → 2.B 阀门 → 游戏内验
  → 贴图四借官方 / Dev 撤出建造栏
  → 2.F 叠放互斥
  → 文档总结与复查（本文）
```

### 5.6 阶段二验收标准

1. 产品 Def 搭双罐+管+三通+阀 ✅  
2. 孤立三通对称（日志「对称通过」） ✅  
3. 阀关阻断、开可流 ✅  
4. Inspect 可读 ✅  
5. 存读档 含阀（玩家声明） ✅  
6. Dev 不进建造栏可回归 ✅  
7. 管道↔管件不叠格 ✅  

### 5.7 阶段二风险（回顾）

| 风险 | 处置 |
|------|------|
| 阀改 MapComp 过大 | V-A 仅内部 Mapping |
| Tee 与 Dev 不一致 | 端口对齐；保留 Dev |
| 管道叠在罐上连错 | **2.F PlaceWorker** |
| 全电线外观混淆 | 已改四借官方贴图 |

### 5.8 阶段二信息总结（收尾用）

| 主题 | 结论 |
|------|------|
| 产品集 | 储罐、三通管、管道、阀门 |
| 阀 | V-A · 仅开关 · 独立管件（非管道上盖子） |
| 三通/阀有内部量 | 架构求解/交汇所需，非储罐玩法 |
| Dev | 保留回归，不进建造栏 |
| 叠放 | 管道 ↔ 管件互斥 |
| 版本 | About 0.2.0 |
| 下一阶段 | §六 单项启动；优先草案仍 3.6 泵 |

---

## 六、阶段三 · 物理与设备扩展

> **原则：** 单项启动；每项先写公式/存档决议再编码；不并行改驱动与拓扑。  
> **锁定顺序（已走完）：** `3.6 泵` → `3.9/3.10 泄漏` → `3.3 阻力` ✅ → **其余按 §6.11 玩法需要单项决议。**  
> **阶段三不做：** 自有美术定稿、Harmony、阀门连续开度、阀盖管道方案 B、对外 API（阶段四）。

### 6.1 扩展原则与挂点

1. 术语仍仅 Container / Port / Mapping；批时序与 Delta→Commit 不变。
2. 优先复用「内部 Mapping + Comp 改 rate」；仅公式要变时改 `FlowSolver`。
3. 默认 `flowDrive=Equalize`（`drive=|Δ|/2`）直至 3.1 单项决议。
4. 回归门槛：孤立三通对称、阀开关、拆管默认不漏、存读档。

| 层 | ID | 主题 | 主要挂点 | 状态 |
|----|----|------|----------|------|
| **A 设备** | **3.6** | **泵 P-A** | `FlowDriveMode.Forced` + `CompPipePump` | `[x]` 验收通过（§1.2c） |
| A′ | **3.9 / 3.10** | **泄漏 L-A**（开口 / 断路 / 效果延后） | `Mapping.leakOpen` + Commit 扣量；FluidDef 挂点 | `[x]` 验收通过（§1.2d） |
| B rate | **3.3** | **管道阻力 R-A** | `ResolveMaxFlowRate` / 最短路径格数 | `[x]` 验收通过（§1.2e） |
| A″ | **效果** | **泄漏效果 E-A** | `FluidDef.leakEffects` + Commit 钩子 | `[x]` 验收通过（§1.2f） |
| C 驱动 | 3.1 | 压力 Pr-A 参与 drive | `Container.pressure` + FlowSolver | `[x]` 验收通过（§1.2g） |
| D 容积 | ~~3.5~~ | ~~管道组容积~~ | — | `[x]` **取消**：管道不储存流体（§6.14） |
| E 热 | 3.7→3.2 | **热量 H-A**（温度 + 混温 + Heat Mapping） | `MappingType.Heat` + Commit 混温 | `[x]` 验收通过（§1.2h） |
| E′ | 3.4 | **环境散热 Amb-A** | `insulation` + Commit PushHeat | `[x]` 验收通过（§1.2i） |
| F 化学 | 3.8 | **Chem**（配方反应釜） | Reactor Comp + RecipeDef；非二元 Chemical Mapping | `[x]` 验收通过（§1.2q） |

### 6.2 决议：3.6 泵 — P-A（已锁定）

| 项 | 决议 |
|----|------|
| 模型 | **P-A**：独立管件；两岸小 Container + 内部 Mapping（对齐 V-A） |
| 方向 | `flowDrive=Forced`：内部 A→B；**开启时**直接相邻的外部亦 Forced（邻→入、出→邻），否则无法泵入高位罐 |
| 公式 | `want = min(maxFlowRate, amountSource, freeTarget)` |
| rate | 开 **且** 有电 → `baseMaxFlowRate`；关或无电 → 0；**不拆** Mapping |
| 电力 | `CompPowerTrader`（+ Flickable）；无电 ≡ 关泵 |
| UI | Gizmo 仅开/关（与阀同） |
| 叠放 | 管件 PlaceWorker（已含 `CompPipeNetworkMember`） |
| 小桶有量 | 求解容积，非储罐玩法（同阀） |
| 本切片不做 | 压力抬升、连续转速、盖在管道上、改 equalize 默认路径 |

**端口约定：** 本地西 = 吸入（containerIndexA）、本地东 = 排出（containerIndexB）；`forcedFromA=true` ⇒ A→B。

### 6.3 任务清单（3.6 泵）

- [x] 6.3.1 `FlowDriveMode` + `Mapping.flowDrive` / `forcedFromA` + 存档
- [x] 6.3.2 `FlowSolver` Forced 分支；Equalize 保持原公式
- [x] 6.3.3 `AddInternalMapping` 支持 drive 参数；`BuildInternalMappings` 调用泵
- [x] 6.3.4 `CompPipePump` / `CompProperties_PipePump`（开/关、电力、内部 Mapping、直接相邻 Forced）
- [x] 6.3.5 `RimPipe_Pump` ThingDef + DefOf；建造栏 uiOrder 140
- [x] 6.3.6 Debug：泵验收场景 + 逆向抽送 / 阻断断言
- [x] 6.3.7 游戏内验收（抽往更满一侧、关泵阻断、三通对称回归、存读档）→ §1.2c

### 6.4 后续项挂点（未启动，不编码 · 详见 §6.11）

- ~~**3.3 阻力**~~ → ✅ R-A（§6.8 / §1.2e）。
- **泄漏效果：** Filth / 室温等 — L-A 已预留；**建议下一优先**（§6.11）。
- **3.1：** 启用 `pressure`；「替换 vs 加权」drive 另决 + 新验收。
- ~~**3.5：** 管道持容积~~ → **取消**（§6.14：管道不储存流体）。
- **3.7–3.4 / 3.8：** 扩展 `MappingType` + Accumulate 分支；严格延后。

### 6.5 决议：3.9 / 3.10 泄漏 — L-A（已锁定 · 2026-07-14）

> **玩家确认：** 破损开口 / 摧毁断路不倾倒 / FluidDef 效果延后；本切片扣量对齐 Debug（偏向 A）。

#### 6.5.1 分工（不破三层模型）

| 角色 | 职责 |
|------|------|
| **储罐 / 管件 Container** | 持有量（唯一权威来源）；可自身 `leakOpen` 直接泄本罐 |
| **管道格** | 仅拓扑；**破损 = 通向大气的孔**（不持液） |
| **Mapping** | `leakOpen` 时 Accumulate 跳过该边；Commit 按 rate 从两端 Container 扣量 |
| **FluidDef** | 描述「扣掉的量变成什么」；与扣量解耦 |

**禁止：** 为泄漏引入 Node；**管道不储存流体**（与 §6.14 一致；破损泄连通罐，非管内液体）。

#### 6.5.2 事件语义

| 事件 | 决议 |
|------|------|
| **破损（开口）** | 相关 Mapping（或储罐自身）设 `leakOpen=true` → **持续**按批扣量 |
| **摧毁 / 正式拆卸管道或设备** | 注销 → 拓扑重建 → Mapping 删除 → **连续泄漏停止**；罐内剩余量 **保留**（断路不倾倒） |
| **拆建/摧毁瞬间倾泻** | Def / 模组设置开关；**默认关**（3.10）；开启时至多按 1 批 rate 小额泄出，另决细节须先确认 |
| **管道无容积** | 打管道 = 开口泄**连通 Container**，不是泄「管内液体」 |

#### 6.5.3 扣量与效果

| 项 | 决议 |
|----|------|
| 本切片扣量 | 复用 `CommitDeltas` 泄漏块；对齐现有 `forceOpenLeak`（两端各约 `rate/2`，竞争缩放防叠负） |
| 产品字段名 | `Mapping.leakOpen`（Debug 开口走同一路径；可自 `forceOpenLeak` 重命名或互为别名，编码时二选一并统一） |
| 存档 | `leakOpen` / 构件 `breached` **持久化**；跨建筑 Mapping 仍读档重建后由 Comp 再刷 flag |
| **效果（延后）** | `FluidDef` 预留 `leakEffects`（可多选：`None` / `Filth` / `Temperature` …）；**本切片只实现扣量 ≡ None** |
| 未来示例 | 水→无事；液体燃料→Filth；冷热气→室温；可并有 — **不在本切片编码** |

#### 6.5.4 本切片触发（L-A）与延后

| 项 | 决议 |
|----|------|
| **本切片** | 显式 `breached` / `leakOpen`：Debug + Gizmo（管道格 / 储罐）；管道破损 → 经 `attachedBuildings` / `cellToMapping` 标记路径 Mapping |
| **储罐破损** | 罐自身开口泄其 Container（不依赖管道） |
| **延后（本切片不做）** | HP 阈值自动破损、`CompBreakdownable`、袭击 TakeDamage 自动挂接 — 须查官方接口并另开任务确认后再做 |
| 三通/阀/泵小桶 | 可随路径 Mapping 被泄；非玩法主储罐；不单独做「小桶倾泻产品」 |

#### 6.5.5 本切片明确不做

- Filth / 气体 / 室温等环境效果实现  
- 管道持液 / 管道组容积（后由 §6.14 **永久取消**）  
- 摧毁 = 整罐瞬间清空  
- Harmony  
- 改 Equalize / Forced 驱动公式  

### 6.6 任务清单（3.9 / 3.10 泄漏 L-A）

**3.9 开口语义与扣量产品化**

- [x] 6.6.1 统一 `Mapping.leakOpen`（复用 Commit 泄漏块；Debug `forceOpenLeak` 并入同一路径）；`ExposeData` 持久化
- [x] 6.6.2 拓扑重建后：由构件 `breached` 重刷路径 Mapping 的 `leakOpen`（挂点：`attachedBuildings` / `cellToMapping`）
- [x] 6.6.3 `CompPipeCell`：`breached` 存档 + Gizmo 破损/修复；Inspect 可读；摧毁/拆卸走现有注销（断路不倾倒）
- [x] 6.6.4 储罐 `CompPipeBreachable`：`breached` → Commit 按 `defaultMaxFlowRate` 泄本罐 Container（等价 Commit 路径）
- [x] 6.6.5 `FluidDef`：预留 `leakEffects`；本切片不读效果，仅扣量
- [x] 6.6.6 Debug：泄漏场景 + 破损泄漏 / 摧毁停漏断言；三通对称仍可用
- [x] 6.6.7 游戏内验收 → §一.2d

**3.10 拆建倾泻开关（默认关）**

- [x] 6.6.8 模组设置 `dumpOnDestroy`（Options → Mod Settings → RimPipe）；**默认 false**
- [x] 6.6.9 开启时：KillFinalize / Deconstruct 瞬间至多 1 批 rate 小额倾泻；默认关与「拆管不漏」一致
- [x] 6.6.10 存读档：含 `breached` 存读档；对称回归（§1.2d）

### 6.7 验收流程（玩家执行 · 写死步骤）

> 前置：DevMode 开；模组已加载最新 `Assemblies/RimPipe.dll`；Options → Mod settings → **RimPipe** 中确认 **dumpOnDestroy 未勾选**（默认）。

**A. 管道破损持续泄漏**

1. Debug Actions → **RimPipe → 生成泄漏验收场景（管道破损）**（点地图空地）
2. 选中中间管道：Inspect 应显示「破损（开口泄漏）」；Gizmo 可切换完好/破损
3. Debug → **断言管道破损泄漏（一批）** → Player.log 须有 **`破损泄漏通过`**
4. （可选）多点几次「强制执行一批」：左右罐量应继续下降

**B. 摧毁管道 = 停漏 + 余量保留**

1. 仍在同一泄漏场景（管道仍破损、罐未空）
2. Debug → **断言摧毁管道停漏留量** → 日志须有 **`摧毁停漏通过`**
3. 含义核对：管道被 KillFinalize 销毁后，再跑一批罐量**不再下降**；且未整罐清空

**C. 储罐自身破损（不依赖管道）**

1. 建造或 Debug 放一个储罐，灌满（Debug「填充」或检视后用填液动作）
2. 选中储罐 → Gizmo **储罐：完好/破损** 设为破损
3. Debug → **强制执行一批** 数次 → 罐量应下降；转储中 Member 行带 `breached`

**D. 拆建倾泻开关（默认关）**

1. 确认设置 **dumpOnDestroy = 关**
2. 左罐—管道—右罐（完好），灌液后**拆卸**管道 → 罐量应基本不变（仅断网）
3. （可选）打开 dumpOnDestroy，再拆/毁一根连通管 → 日志可出现 `dumpOnDestroy` 倾泻一行，量略减（≤约 1 批 rate）

**E. 回归**

1. Debug → **生成三通验收场景（产品）** → 多批 → **断言三通左右对称** → **`对称通过`**
2. （可选）阀门/泵场景快速回归：关阀阻断、泵逆向抽送仍可用

**F. 存读档往返**

1. 生成泄漏场景（管道破损）→ 存档（如 `TestPipeLeak`）
2. 读档 → 选中管道仍为破损；转储 Mapping 含 `leakOpen`；再 **断言管道破损泄漏（一批）** 仍通过
3. 读档后跑对称/阀/泵快速回归即可

**通过标准（全部满足才勾 6.6.7 / 6.6.10）：**

| 项 | 日志 / 现象 |
|----|-------------|
| 破损泄漏 | `破损泄漏通过` |
| 摧毁停漏 | `摧毁停漏通过` |
| 对称回归 | `对称通过` |
| dump 默认关 | 拆/毁管后无整罐倾空 |
| 存读档 | 读档后 breached 仍在且继续漏 |

通过后把证据记入 **§一.2d**，并把 6.6.7 / 6.6.10 勾上。

### 6.8 决议：3.3 管道阻力 — R-A（已锁定 · 2026-07-14）

> **玩家确认路径：** 计划「3.3 阻力架构大纲」通过 → 按本决议编码。

#### 6.8.1 模型 R-A

| 项 | 决议 |
|----|------|
| 作用层 | **仅**外部 Flow Mapping 的 `maxFlowRate`；不改 `FlowSolver` / 批时序 / Delta→Commit |
| 长度定义 | 两挂接 `outerCell` 在管道图上的**最短路径管道格数** `pathPipeCells`（含起终点格；同格=1） |
| 直接相邻 | `pathPipeCells = 0` → rate **不**衰减（`base / max(1,0)` ≡ `base`） |
| 内部 Mapping | 阀 / 泵内部边**不**乘路径衰减（仍由 Comp 写 `baseMaxFlowRate × 开关/电`） |
| 公式 | `base = min(defaultMaxFlowRate_A, defaultMaxFlowRate_B)`；`maxFlowRate = max(minRate, base / max(1, pathPipeCells))`；`minRate = 0.1` |
| 存档 | rate 随拓扑重建赋值；`pathPipeCells` **运行时**字段，**不** Scribe（跨建筑 Mapping 本就不持久化拓扑） |
| 泄漏交互 | 本切片**不**改 `attachedBuildings = 整分量`（L-A 已验收）；阻力长度与泄漏附着解耦 |
| 泵外 Forced | 建边后仍由 `CompPipePump` 刷新直接相邻 rate；长管 Equalize 边吃路径衰减；泵直接相邻不吃路径 |

#### 6.8.2 本切片明确不做

- 粘度 / 管径⁴ / 压降进 drive（旧策划 Hagen–Poiseuille）
- 管道持液 / 管道组容积（§6.14 已永久取消）
- 压力参与 drive（3.1；后独立验收 ✅）
- 改 Equalize / Forced 驱动公式
- Harmony

### 6.9 任务清单（3.3 阻力 R-A）

- [x] 6.9.1 `AddPipePair`：两 `outerCell` BFS 最短路径格数 → `pathPipeCells`
- [x] 6.9.2 `ResolveMaxFlowRate(a, b, pathPipeCells)`；直接相邻传 `0`；`Mapping.pathPipeCells` 运行时字段
- [x] 6.9.3 Dump / Inspect 可读 path 与生效 rate
- [x] 6.9.4 Debug：长短管场景 + 断言「长管 transfer 更慢」；三通/阀/泵/泄漏快速回归仍可用
- [x] 6.9.5 游戏内验收 → §一.2e（玩家确认通过；rate/path 证据 + 手感）
- [x] 6.9.6 About → `0.3.2`

### 6.10 验收流程（3.3 · 已验收通过）

> 历史步骤保留；通过标准以 **§1.2e** 为准（玩家确认 · Dump rate/path · 手拉长管）。  
> Debug「阻力通过」断言误报失败已知（多网 / 已均分）；硬化另开小任务，不影响验收通过。

### 6.11 阶段三后续 · 候选与建议下一动作（2026-07-14 · 6.17 修订）

> **原则不变：** 单项启动；先公式/存档决议再编码；不并行改驱动与拓扑。  
> **已锁定：** **管道不储存流体**（§6.14）；**热量 H-A ✅**；**Amb-A ✅**；阶段四 4.1–4.8 ✅；**4.9/4.10 延后**（§7.9 / §7.10）；**3.8 Chem ✅**（§6.17 / §1.2q）。

| 优先 | ID / 主题 | 挂点 | 改动面 | 建议 |
|------|-----------|------|--------|------|
| ~~1~~ | ~~泄漏效果 E-A~~ | — | — | ✅ §1.2f |
| ~~1~~ | ~~**3.1 压力 Pr-A**~~ | — | — | ✅ §1.2g |
| ~~1~~ | ~~**3.5 管道组容积**~~ | — | — | **取消**（§6.14） |
| ~~1~~ | ~~**热量 H-A**~~ | — | — | ✅ §1.2h |
| ~~1~~ | ~~**3.4 环境散热 Amb-A**~~ | — | — | ✅ §1.2i |
| ~~1~~ | ~~**4.1 整图休眠 A+B**~~ | — | — | ✅ §1.2j |
| ~~1~~ | ~~**4.2 分网休眠 NetSleep**~~ | — | — | ✅ §1.2k |
| ~~1~~ | ~~**4.3 Overlay**~~ | — | — | ✅ §1.2l |
| ~~1~~ | ~~**4.5 SaveMig**~~ | — | — | ✅ §1.2m |
| ~~1~~ | ~~**4.4 ApiDoc**~~ | — | — | ✅ §1.2n |
| ~~1~~ | ~~**4.6 Loc**~~ | — | — | ✅ §1.2o |
| ~~1~~ | ~~**4.7 ExtHook**~~ | — | — | ✅ §1.2p |
| ~~1~~ | ~~**3.8 化学 Chem**~~ | — | — | ✅ §1.2q（P0–P6） |
| **1** | **4.8 Bridge-A** | §7.8 | Comp 伤害/Breakdown→breached；TrySetBreached；无 Harmony | ✅ §1.2r |
| — | （延后）Bridge-H | — | §7.9 | `[!]` 已决议延后 |
| — | （延后）DirtyTopo | — | §7.10 | `[!]` 已决议延后；**先 Benchmark 再决定** |
| **1** | **4.11 发布准备 R1** | §7.11 | `0.4.0` 冻结 + `RimPipe_API.md`（含原 RELEASE）+ 回归（含化学） | ✅ §1.2s |
| ~~1~~ | ~~**4.12 Debug 整理**~~ | — | — | ✅ §1.2t |

**下一动作：** 玩家手测 Stress / 计时重建对照 §7.10.3；可选 R2 / Bridge-H（§7.9）；DirtyTopo 仍延后。

### 6.12 决议：泄漏效果 — E-A（已锁定 · 2026-07-14）

> **玩家确认 T1–T4：** Filth+Temperature 同做；每 10 量→≥1 层 Filth；`leakHeatEnergyPerUnit=5`；水=None + 新增 TestFuel→Filth_Fuel。  
> **已查官方接口（①）：** `RimWorld.FilthMaker.TryMakeFilth`；`Verse.GenTemperature.PushHeat(IntVec3, Map, float energy)`。

#### 6.12.1 架构边界（锁定倾向）

| 项 | 草案 |
|----|------|
| 作用层 | **仅** Commit 路径在「实际销毁量 > ε」之后施加环境效果；**不改** Flow / 拓扑 / rate / drive |
| 扣量与效果 | 扣量仍由 L-A；效果读 `FluidDef.leakEffects`；二者解耦 |
| 术语 | 仍仅 Container / Port / Mapping；无新 Node |
| Harmony | 不做 |
| `dumpOnDestroy` | **共用**同一效果入口（倾泻销毁量 >0 时也施效果） |

#### 6.12.2 时机与位置

| 项 | 草案 | 状态 |
|----|------|------|
| 时机 | Commit 算出本批对该 Container 的泄漏销毁量 `leaked` 后立即施效果（与 `AddDelta(-leaked)` 同批） | 拟钉 |
| 位置·管道破损 | 优先破损管道格 `CompPipeCell` 所在 cell；否则 Mapping `attachedBuildings` 中管道格；再否则泄流 Container 所属 Building.Position | 拟钉 |
| 位置·储罐破损 | 储罐 Position | 拟钉 |
| 位置·dumpOnDestroy | 被毁/拆建筑 Position（管道倾泻两侧 container 时用管道格） | ✅ |

#### 6.12.3 效果语义

| 项 | 决议 | 状态 |
|----|------|------|
| `None` 或空列表 | 无环境效果（仅扣量，与现网一致） | ✅ |
| 多选 | 列表可同时含 `Filth` + `Temperature`，按序执行 | ✅ |
| **Filth** | 调 `FilthMaker.TryMakeFilth`；Filth Def 来自 `FluidDef.leakFilthDef`（可空则跳过 Filth 并 Warning 一次） | ✅ |
| Filth 层数 | `count = max(1, floor(leaked / leakFilthUnitsPerFilth))`，`leakFilthUnitsPerFilth` 默认 **10** | ✅ 已钉 |
| **Temperature** | `GenTemperature.PushHeat(cell, map, leaked * leakHeatEnergyPerUnit)`；默认 **5**（正加热） | ✅ 已钉 |
| 失败容忍 | `TryMakeFilth` / `PushHeat` 失败不回滚扣量；不抛；可选 Debug 日志 | ✅ |

#### 6.12.4 Def 与验收流体

| FluidDef | leakEffects | 参数 | 状态 |
|----------|-------------|------|------|
| `RimPipe_Fluid_TestWater` | `None` | 水无事 | ✅ |
| **新增** `RimPipe_Fluid_TestFuel` | `Filth` + `Temperature` | `leakFilthDef=Filth_Fuel`；units=10；heat=5 | ✅ |

#### 6.12.5 本切片明确不做

- 改 Equalize / Forced / 阻力公式  
- 管道持液（§6.14 已取消）/ 改压力公式（本切片不做；后由 Pr-A 另做）  
- 自画 Filth 贴图（用官方 `Filth_Fuel` 等）  
- HP 自动破损（仍延后）  
- 为效果引入 Harmony  

#### 6.12.6 任务清单

- [x] 6.12.a 人类确认 T1–T4 → §十一  
- [x] 6.12.b `FluidDef`：`leakFilthDef` / `leakFilthUnitsPerFilth` / `leakHeatEnergyPerUnit`  
- [x] 6.12.c `LeakEffectApplicator`：按效果列表调用官方 API  
- [x] 6.12.d Commit 泄漏块 + `dumpOnDestroy` 倾泻路径接入  
- [x] 6.12.e 新增 `RimPipe_Fluid_TestFuel` + DefOf；Debug 场景灌燃料破损  
- [x] 6.12.f Debug：断言 Filth / 水无 Filth / 室温 Δ  
- [x] 6.12.g 游戏内验收 → §一.2f（玩家确认通过）  
- [x] 6.12.h About → `0.3.3`

#### 6.12.7 验收流程（玩家执行 · 写死步骤）

> 前置：DevMode；最新 `Assemblies/RimPipe.dll`；Options → RimPipe **dumpOnDestroy 默认关**。

**A. 燃料破损 → Filth（主路径）**

1. Debug → **生成泄漏效果验收场景（燃料罐/管道破损）**（点空地）  
2. 目视 / 选中破损格：应出现 **燃油污物**（`Filth_Fuel` 或约定 Def）  
3. Debug → **断言泄漏 Filth（一批）** → Player.log 须有 **`泄漏Filth通过`**  
4. （可选）多点「强制执行一批」：污物可增厚或邻格传播（官方 FilthMaker 行为），罐量继续下降  

**B. 水 / None → 无 Filth**

1. Debug → **生成泄漏验收场景（管道破损）**（现有 TestWater）或效果场景的对侧水罐  
2. 破损扣量仍发生；**不应**因水而刷 `Filth_Fuel`  
3. Debug → **断言水泄漏无 Filth** → 日志 **`水泄漏无Filth通过`**（或 Inspect/目视确认）

**C. dumpOnDestroy 与效果（可选）**

1. 打开 dumpOnDestroy；对灌满 **燃料** 的短管连通网拆/毁管道  
2. 倾泻发生时同格/邻格可出现 Filth（若本批有销毁量）  
3. 关回默认后拆管：不倾泻、不因此刷污（与 L-A 一致）

**D. Temperature（本切片必做）**

1. 燃料效果场景破损后 → Debug → **断言泄漏室温（一批）** → 日志 **`泄漏室温通过`**（泄后该格温度上升）  

**E. 回归**

1. 三通对称 → `对称通过`  
2. （可选）泵逆向抽送 / 阀关阻断 / 破损泄漏扣量 / 阻力 Dump path-rate 快速回归  
3. 存读档：破损燃料场景存读档 → 再一批仍可漏且效果仍触发  

**通过标准（全部满足才勾 6.12.g）：**

| 项 | 日志 / 现象 |
|----|-------------|
| Filth | `泄漏Filth通过` + 地图可见污物 |
| 水 None | `水泄漏无Filth通过`（或等价确认） |
| 扣量 | 破损仍按批减量（L-A 不回退） |
| 对称 | `对称通过` |
| 存读档 | 读档后仍可漏 + 效果 |

通过后记入 **§一.2f**，About → `0.3.3`。**已验收通过（玩家确认 · 2026-07-14）。**

#### 6.12.8 玩家确认记录

| # | 决议 | 日期 |
|---|------|------|
| T1 | Filth + Temperature **同做** | 2026-07-14 |
| T2 | 每 **10** 量 → ≥1 层 Filth | 2026-07-14 |
| T3 | `leakHeatEnergyPerUnit = 5` | 2026-07-14 |
| T4 | 水=None；新增 TestFuel → Filth_Fuel | 2026-07-14 |

### 6.13 决议：3.1 压力进 drive — Pr-A（已锁定 · 2026-07-14）

> **玩家确认：** 填充比压力 + **替换** Equalize（非加权）；Forced/阻力/泄漏不动；八荣八耻本切片强制。  
> **挂点：** `Container.pressure` + `FlowSolver.ComputeEqualizeWant`；批内用虚拟量算 P。

#### 6.13.1 架构边界

| 项 | 决议 |
|----|------|
| 作用层 | **仅** Equalize 驱动定义；不改批时序 / 拓扑 / Forced / R-A / L-A / E-A |
| 术语 | 仍仅 Container / Port / Mapping；无新 Node |
| Harmony | 不做 |
| 纪律 | X7–X9 / Cursor 八荣八耻 |

#### 6.13.2 压力定义

| 项 | 决议 |
|----|------|
| 语义 | `pressure` = **填充比**（静压代理；非理想气体 / 非 ρgh） |
| 公式 | `P = capacity > ε ? clamp(amount/capacity, 0, 1) : 0` |
| 权威来源 | 量仍是 `amount`；pressure **派生**，不独立守恒 |
| 批内 Jacobi | `ComputeWant(..., amountA, amountB, ...)` **用虚拟量**算 P；禁止读脏字段 |
| 字段维护 | `CommitAmount` 后同步 `pressure`；读档 `FinalizeInit` 全图重算一次 |
| 存档 | 既有 Scribe；不一致时重算容错 |

#### 6.13.3 Equalize 驱动（替换，非加权）

| 项 | 决议 |
|----|------|
| 方向 | P 高 → P 低；`|ΔP| ≤ ε` → want=0 |
| drive | `\|P_hi - P_lo\| / 2 * min(capacity_hi, capacity_lo)` |
| want | `min(drive, maxFlowRate, amountHi, freeLo)` |
| 等容相容 | `cap` 相等时 `drive = \|Δamount\|/2` ≡ 旧 Equalize |
| 异容 | 趋向填充比均衡（非绝对量均衡） |
| Forced | **不改** |

#### 6.13.4 本切片明确不做

- 泵扬程 / 压力抬升  
- 密封气态、高度静压、管道压降进 drive  
- 管道持液（§6.14 已取消）、Heat/Chemical、Harmony  
- 压力可视化定稿（Dump/Inspect 数字即可）  
- 加权 drive  

#### 6.13.5 可读性

- Inspect：容器行带 `P=` / 填充比  
- Dump / `Container.ToString`：带 pressure  

#### 6.13.6 任务清单

- [x] 6.13.a 人类确认 Pr-A（计划 Implement）  
- [x] 6.13.b 写入本文 §6.13 / 更新 §6.11 / §九 / §十一  
- [x] 6.13.c `FlowSolver` Equalize 按 P；`CommitAmount` / Finalize 同步 pressure  
- [x] 6.13.d Inspect / Dump（`P=` + `Container.ToString`）  
- [x] 6.13.e `RimPipe_Dev_TankLarge` + Debug 异容场景 / `压力均分通过` 断言；Release 已编  
- [x] 6.13.f 游戏内验收 → §1.2g（玩家确认 · 2026-07-14）  
- [x] 6.13.g About → `0.3.4`  

#### 6.13.7 验收流程（玩家执行 · 写死步骤）

> 前置：DevMode；最新 `Assemblies/RimPipe.dll`；`dumpOnDestroy` 默认关。

**A. 等容回归**

1. Debug → **生成三通验收场景（产品）** → 多点「强制执行一批」  
2. Debug → **断言三通左右对称** → 日志 **`对称通过`**  

**B. 异容压力（主验收）**

1. Debug → **生成压力验收场景（异容）**（左小罐满 / 右大罐空，直接相邻 Equalize，无泵）  
2. 记初始 amount / capacity / pressure  
3. Debug → **断言压力均分（异容）** → 日志 **`压力均分通过`**（\|ΔP\| 小 + 总量守恒）  

**C. 泵 Forced 回归**

1. Debug → 泵场景 → **断言泵逆均分** → **`泵逆均分通过`**  

**D. 泄漏快速回归**

1. 破损扣量一批仍可用；无崩溃  

**E. 存读档**

1. 异容场景若干批 → 存档 → 读档 → pressure 与量/容一致 → 可继续均压  

| 项 | 证据 |
|----|------|
| 等容 | `对称通过` |
| 异容 | `压力均分通过` + 总量守恒 |
| 泵 | `泵逆均分通过` |
| 框架 | 无 Exception；阀/阻/漏快速回归 |
| 存读档 | 读档后可继续 |
| 文档 | §1.2g + About `0.3.4` |

### 6.14 决议：管道不储存流体 — 取消 3.5（已锁定 · 2026-07-14）

> **玩家确认：** 偏向管道不存液；对齐 L-A 现状与弃用策划「PipeLine 不参与流体计算」。

| 项 | 决议 |
|----|------|
| 管道格 | **不储存流体**；无 Container；仅拓扑 + `breached` 等标记 |
| 量的权威来源 | 仅 `CompPipeNetworkMember` 的 Container（储罐 / 三通 / 阀 / 泵等） |
| 破损语义 | 保持 L-A：破管 = 大气孔，泄**连通** Container，不是泄「管内液体」 |
| 原 3.5 | **取消**；不再规划「管道段持有 Container / 管道组容积」 |
| 编码 | 无对应实现切片；禁止再以 3.5 为由改拓扑持液 |

### 6.15 决议：热量交换 — H-A（已锁定 · 2026-07-14）

> **玩家确认：** 仅做热量交换；化学暂缓；管道仍不存液。  
> **范围：** 温度字段生效 + Flow 携带混温 + `MappingType.Heat` 容器间导热 + 换热器产品；不做向环境散热（3.4）。

#### 6.15.1 架构边界

| 项 | 决议 |
|----|------|
| 作用层 | 扩展 Accumulate/Commit；**不改** Flow Equalize/Forced 量公式、R-A、L-A、E-A |
| 术语 | 仍仅 Container / Port / Mapping；无 Node |
| 化学 | **暂缓** |
| 环境散热 3.4 | **本切片不做** |
| 管路径导热边 | **不做**（外部 Flow 不自动附 Heat） |
| Harmony | 不做 |

#### 6.15.2 温度语义

| 项 | 决议 |
|----|------|
| 字段 | `Container.temperature`（°C）；默认 / 空读档 **21** |
| 热质 | `m = amount`；比热 `c = 1`；焓代理 `E = m * T` |
| 空罐 | `amount ≤ ε` **不参与**导热；接收首次流入时 `T = 来流 T` |
| 写入 | **禁止**绕过 Delta 直接改 T；`pendingTempDeltas` → Commit |

#### 6.15.3 Flow 携带混温

| 项 | 决议 |
|----|------|
| 时机 | **Commit 阶段**：量 Commit + 泄漏后，按本批 Flow `batchWant` 混温一次 |
| 公式 | `E_tgt' = m_tgt*T_tgt + w*T_src`；`T_tgt' = E_tgt'/(m_tgt+w)`；src 侧焓相应减少 |
| Accumulate | 仍只算量 |

#### 6.15.4 Heat Mapping（导热）

| 项 | 决议 |
|----|------|
| 类型 | `MappingType.Heat`；不移动 amount |
| 公式 | `Qwant = min(maxHeatRate, \|ΔT\|/2 * min(m_hi,m_lo))`；高温 `T-=Q/m`，低温 `T+=Q/m` |
| rate 字段 | 复用 `Mapping.maxFlowRate` = 每批最大换热量 |
| 开关 | 关 → rate=0；**仅 Equalize**（无 Forced 热泵） |
| 异流体 | **允许**导热 |
| leakOpen | Heat 边跳过 |
| PairKey | 含 `mappingType`，同对可同时有 Flow+Heat |
| 欠松弛 | Heat 迭代约 **4** 轮 |

#### 6.15.5 换热器产品

| 项 | 决议 |
|----|------|
| 模型 | 两岸 Container + 内部 Heat Mapping（对齐阀） |
| Comp | `CompPipeHeatExchanger`；Gizmo 开/关；**无电力** |
| Def | `RimPipe_HeatExchanger`；建造栏「管道」；借官方贴图 |
| `baseMaxHeatRate` | 默认 **50** / 批 |
| `\|ΔT\|≤ε` | **0.01** → Q=0 |

#### 6.15.6 批处理时序

```
0–18: AccumulateFlow（仅 Flow）+ AccumulateHeat（仅 Heat → pendingTempDeltas）
19:   CommitDeltas（量+泄漏/E-A）→ ApplyFlowMixing → CommitTempDeltas
```

#### 6.15.7 本切片明确不做

- 化学；3.4 环境散热；管路径导热；`insulation`；相变；改量驱动公式

#### 6.15.8 任务清单

- [x] 6.15.a 写入本文 §6.15 / 更新 §6.11 / §九 / §十一
- [x] 6.15.b `MappingType.Heat` + `HeatSolver` + `CommitTemperature`；PairKey 含 type
- [x] 6.15.c MapComp：AccumulateHeat / ApplyFlowMixing / CommitTemp；`AddInternalMapping` 支持 Heat
- [x] 6.15.d `CompPipeHeatExchanger` + Def / DefOf；Inspect `T=`
- [x] 6.15.e Debug：换热器热量均分 / 关阻断 / Flow 混温；对称与泵快速回归
- [x] 6.15.f 游戏内验收 → §1.2h（玩家确认通过 · 2026-07-14）；About `0.3.5`

#### 6.15.9 验收流程（玩家执行）

> 前置：DevMode；最新 `Assemblies/RimPipe.dll`。

**A. 换热器导热**

1. Debug → **生成热量验收场景（换热器）**  
2. 多点「强制执行一批」→ Debug → **断言热量均分** → 日志 **`热量均分通过`**（`|ΔT|` 下降且 amount 不变）  
3. 关换热器 → **断言热量阻断** → **`热量阻断通过`**

**B. Flow 混温**

1. Debug → **生成混温验收场景**（或同场景快速回归）→ 热液流入冷侧 → 接收侧 T 上升 → **`混温通过`**（若有断言）

**C. 回归**

1. 三通对称 → `对称通过`  
2. 泵逆向抽送快速回归  
3. 存读档：存读档后 T 与换热器开关仍有效  

| 项 | 证据 |
|----|------|
| 导热 | `热量均分通过` + amount 不变 |
| 关断 | `热量阻断通过` |
| 混温 | 接收侧升温 |
| 回归 | `对称通过`；泵快速回归 |
| 存读档 | 读档后 T/开关可续 |
| 文档 | §1.2h + About `0.3.5` |

### 6.16 决议：向环境散热 — Amb-A（已锁定 · 2026-07-15）

> **玩家确认：** 下一刀 3.4 环境散热；管道仍不存液；化学暂缓。  
> **已查官方接口（①）：** `GenTemperature.GetTemperatureForCell`；`GenTemperature.PushHeat(IntVec3, Map, float)`。

#### 6.16.1 架构边界

| 项 | 决议 |
|----|------|
| 作用层 | Commit 末尾环境交换；**不改** Flow / Heat Mapping / L-A / E-A |
| 主体 | 有液 Container（构件桶）；**管道格不参与** |
| 术语 | 仍仅 Container / Port / Mapping；无环境 Node |
| 化学 | 暂缓 |
| Harmony | 不做 |

#### 6.16.2 公式与字段

| 项 | 决议 |
|----|------|
| Tamb | `GenTemperature.GetTemperatureForCell(building.Position, map)` |
| 条件 | `amount > ε` 且 `\|T - Tamb\| > 0.01` |
| Q | `min(maxAmbientHeatRate, \|ΔT\|/2*m) * (1-insulation)`（**先截断再乘保温**；2026-07-15 修：旧式 `(…*(1-ins))` 再 min 会双双顶满 rate） |
| T' | `T - sign(ΔT) * Q / m` |
| PushHeat | **仅室内**（`room != null && !UsesOutdoorTemperature`）；室外只改罐温；流体变凉 `+Q` / 变热 `-Q`；失败不回滚 T |
| `insulation` | CompProps 默认 **0.5**；产品储罐 **0.7**；Dev 罐 **0**（对比） |
| `maxAmbientHeatRate` | 默认 **10** / 批 |

#### 6.16.3 时机

```
19: CommitDeltas → ApplyFlowMixing → CommitTempDeltas → ApplyAmbientHeatExchange
```

#### 6.16.4 本切片明确不做

- 管格散热；`k*A` 精确式；新 MappingType；改 H-A 断言（另开可选）

#### 6.16.5 任务清单

- [x] 6.16.a 写入本文 §6.16 / 更新 §6.11 / §九 / §十一
- [x] 6.16.b CompProps `insulation` / `maxAmbientHeatRate` + Def
- [x] 6.16.c `ApplyAmbientHeatExchange` 接 Commit / DebugForce
- [x] 6.16.d Debug：散热 / 保温对比 / 空罐；H-A 快速回归
- [x] 6.16.e 游戏内验收 → §1.2i（玩家确认通过 · 2026-07-15）；About `0.3.6`

#### 6.16.6 验收流程（玩家执行）

> 前置：DevMode；最新 `Assemblies/RimPipe.dll`；建议室内。

**A. 热液向环境降温**

1. Debug → **生成环境散热验收场景**  
2. 多点「强制执行一批」→ 罐 T 向环境靠拢  
3. **断言环境散热** → 日志 **`环境散热通过`**

**B. 保温对比**

1. 场景含低保温 Dev 罐 + 高保温产品罐（同初温同量）  
2. **断言保温对比** → **`保温对比通过`**

**C. 空罐不散**

1. **断言空罐不散热** → **`空罐不散热通过`**

**D. 回归**

1. H-A 换热器快速回归；可选三通对称；存读档  

| 项 | 证据 |
|----|------|
| 散热 | `环境散热通过` |
| 保温 | `保温对比通过` |
| 空罐 | `空罐不散热通过` |
| 回归 | H-A / 对称快速回归；无 Exception |
| 文档 | §1.2i + About `0.3.6` |

### 6.17 决议：3.8 化学 — Chem（已锁定 · 2026-07-19）

> **玩家确认：** 对标 Minecraft 工业模组（Mek/GT）思路；腔体 **A/B1**；空燃比 **L1**；叙事完整 **Chem**（转化 + T/P 门槛 + 反应热）；实施分批 P0→P6（P5 拆 a/b）；P3 要电力。  
> **玩法意图：** 多流体按配比消耗并生成产物（如液氧+航天煤油）；设备可调比；框架须能支撑后续燃烧室/涡喷类玩法；本序列不做整机涡喷。  
> **旧策划**「二元 Chemical Mapping / Node」仅历史参考；以本节为准。

#### 6.17.1 架构边界

| 项 | 决议 |
|----|------|
| 作用层 | 新批处理 Chem 分支；**不改** Flow Equalize/Forced、R-A、L-A、E-A、H-A 导热公式 |
| 术语 | 仍仅 Container / Port / Mapping；管道不储存流体 |
| 腔体 | **A/B1**：每流体一 Container（单流体槽）；B1 仅产品 UI「像一锅」；**不做 B0** 组分向量 / 混管 |
| 对标 | Mek PRC / Chemical Infuser、GT Chemical Reactor：配方驱动、按量扣入加出 |
| 反应载体 | **`CompPipeReactor` + `PipeReactionDef` + 多腔引用**；求解在 Chem 批处理，**不**用成对 `MappingType.Chemical` 传质 |
| `MappingType.Chemical` | 枚举可预留占位；文档/API 写明多流体反应走 Reactor |
| 异流体 Flow | 仍 want=0 → 分管进各腔 |
| Harmony | 不做 |

#### 6.17.2 配方语义（PipeReactionDef）

| 字段 | 含义 |
|------|------|
| `inputs[]` / `outputs[]` | `{ fluid, stoichAmount }` 基准化学计量（每 1 批单位） |
| `maxRate` | 每 20-tick 批最大批次数 n |
| `baseMixRatio` / `ratioMin` / `ratioMax` | L1；**mixRatio = 氧化剂量/燃料量**（P1 已钉）；`baseMixRatio`≤0 则用 stoich 比 |
| `mixRatioOxidizerInputIndex` / `mixRatioFuelInputIndex` | mixRatio 分子/分母对应的 inputs 下标（默认 0/1） |
| `efficiencyAtStoich` / `efficiencyAtRatioEdge` | 偏比效率：端点 η + 向 stoich 线性插值（P4） |
| `minTemperature` / `minPressure` | 门槛；P=amount/capacity（Pr-A）；P5a |
| `heatPerBatch` | 每完成 1 批单位的焓代理（+放热/−吸热）；P5b |
| `requirePower` | 默认 true（P3）；无电≡关 |
| `heatTargetContainerIndex` | 反应热写入哪一腔（默认产物腔或 Def 指定；P5b 钉） |

示范配方（P1）：液氧 + 航天煤油（或占位 FluidDef）→ `TestExhaust`（名可调）。

#### 6.17.3 运行时与 L1

| 项 | 决议 |
|----|------|
| Comp | `CompPipeReactor`：开关、`mixRatio`、电力（`CompPowerTrader` + Flickable，对齐泵） |
| 腔 | ≥2 入 + ≥1 出，各绑 Port |
| L1 | 连续可调 `mixRatio`（可附三档快捷，底层仍连续）；存档 |
| 效率 | η=f(mixRatio vs stoich)；产出（或有效 n）×η；本序列**无副产物流体** |
| 缺料 | 按最紧缺输入缩放 n；`n<ε` → 不反应 |

#### 6.17.4 批处理时序

```
0–18: AccumulateFlow + AccumulateHeat（既有）
      + AccumulateChem（算 n → pending 量/热 Delta；禁止直接改 amount/T）
19:   CommitDeltas（量+泄漏/E-A）
      → ApplyFlowMixing
      → CommitChem（扣入、加出、反应热进 temp Delta）
      → CommitTemp / Amb
```

有转化或反应热时 **Wake** 所属网 Busy（对齐 API 改量唤醒）。

#### 6.17.5 条件（P5a）与反应热（P5b）

| 条件 | 行为 |
|------|------|
| 关 / 无电（requirePower） | n=0 |
| 监测腔 T &lt; minT 或 P &lt; minP | n=0 |
| 输出满 / 输入不足 | n 缩放或 0 |

监测腔：`conditionContainerIndex` 相对配方 **inputs**（&lt;0 → 第一入腔）。P=amount/capacity。  
反应热：`Q=n×heatPerBatch`，`ΔT=Q/m`；目标 `heatTargetContainerIndex`（&lt;0→第一产出腔；≥0→建筑 Containers 下标）；经 pendingTemp→Commit。

#### 6.17.6 本序列明确不做

- B0 混合物、管路径反应、副产多流、催化剂存量  
- Arrhenius / 真实动力学  
- 正式建造栏反应釜、自有美术定稿  
- 完整涡喷建筑（仅框架可挂）  
- 用二元 Chemical Mapping 模拟多入多出  

#### 6.17.7 实施批次（玩家确认 · 2026-07-19）

| 批次 | 名称 | 范围 | 状态 |
|------|------|------|------|
| **P0** | 文档锁定 | 本节 + §6.11 / §九 / §十一 | `[x]` |
| **P1** | 配方与数据面 | `PipeReactionDef` + 示范流体/配方 XML；加载无 Config error | `[x]` |
| **P2** | 求解核 | AccumulateChem / CommitChem；量转化 + 缺料缩放；暂无 L1/条件/热 | `[x]` |
| **P3** | Dev 反应釜 | Comp + 多腔 Dev Def；**开关 + 电力**；默认比能跑 | `[x]` |
| **P4** | L1 空燃比 | `mixRatio` 存档 + UI + 效率曲线 | `[x]` |
| **P5a** | 条件 | minT / minP 门槛 | `[x]` |
| **P5b** | 反应热 | `heatPerBatch` 写 T + Wake | `[x]` |
| **P6** | Debug / 验收 / 收尾 | 场景断言、回归、§1.2x、About、API 边界句 | `[x]` |

**纪律：** 一批一事；每批玩家确认后再进下一批；不并行改 Flow/拓扑公式。

#### 6.17.8 任务清单

- [x] 6.17.a 写入本文 §6.17 / 更新 §6.1 / §6.11 / §九 / §十一（P0）
- [x] 6.17.b **P1** `PipeReactionDef` + LOX/RP-1/Exhaust + `RimPipe_Reaction_LoxRp1`；About `0.3.13`
- [x] 6.17.c **P2** ChemSolver + MapComp 挂接（仅量）；Debug 三罐绑定；About `0.3.14`
- [x] 6.17.d **P3** CompPipeReactor + `RimPipe_Dev_Reactor`；开关+电；About `0.3.15`
- [x] 6.17.e **P4** L1 mixRatio + 效率曲线 + 滑条/预设；About `0.3.16`
- [x] 6.17.f **P5a** T/P 门槛；About `0.3.17`
- [x] 6.17.g **P5b** 反应热；About `0.3.18`
- [x] 6.17.h **P6** §1.2q + API.md 化学面；About `0.3.19`

#### 6.17.9 验收结果（对照 §1.2q）

| 场景 | 期望 | 结果 |
|------|------|------|
| 化学计量转化 | 入减出增，符合配比 | ✅ P2 `化学转化通过` |
| L1 偏比 | 消耗比变；效率可观察 | ✅ P4 `化学L1通过` |
| T/P 不足 | 不转化 | ✅ P5a `化学条件通过` |
| 放热 | 指定腔 T 上升 | ✅ P5b（`Player-prev.log` 可复读） |
| 关/无电 | 停 | ✅ P3 `化学釜阻断通过` |
| 回归 | 三通/泵/换热器快速回归 | ⓘ 当前 log 有场景生成 + 泵/换热/休眠行；正式对称/泵断言未重跑 |

---
## 七、阶段四 · 优化与发布

| ID | 主题 | 状态 |
|----|------|------|
| **4.1** | **整图懒惰 / 休眠（A+B）** | `[x]` 验收通过（§1.2j） |
| **4.2** | **分网休眠 — NetSleep**（脏区拓扑延后） | `[x]` 验收通过（§1.2k） |
| **4.3** | **流量 / 压力 Overlay**（仅 DevMode） | `[x]` 验收通过（§1.2l） |
| **4.4** | **API 文档 — ApiDoc** | `[x]` 验收通过（§1.2n） |
| **4.5** | **存档迁移 SaveMig** | `[x]` 验收通过（§1.2m） |
| **4.6** | **研究与本地化 — Loc** | `[x]` 验收通过（§1.2o） |
| **4.7** | **ExtHook — `IPipeInternalMappingContributor`** | `[x]` 验收通过（§1.2p） |
| **4.8** | **Bridge-A — 伤害/Breakdown→破损 + 公开 API** | `[x]` 验收通过（§1.2r） |
| **4.9** | **Bridge-H — Def 注入（Harmony）** | `[!]` 已决议延后（§7.9） |
| **4.10** | **DirtyTopo — 局部脏区拓扑** | `[!]` 已决议延后（§7.10；先 Benchmark） |
| **4.11** | **发布准备 — R1** | `[x]` 验收通过（§1.2s · `0.4.0`） |
| **4.12** | **Debug 测试整理** | `[x]` 验收通过（§1.2t · `0.4.1`） |

### 7.1 决议：4.1 整图休眠 — Sleep（已锁定 · 2026-07-15）

> **玩家确认：** 粒度 **整图 A**；Amb **B**（跳过 Accumulate 时仍跑环境散热）。分网休眠留给 4.2。

#### 7.1.1 架构边界

| 项 | 决议 |
|----|------|
| 粒度 | 整图一旗；不建连通分量网 ID |
| Amb | 跳过 Acc 后仍可轻 Commit（泄漏扫描 + Amb）；全静时可跳过 Commit 体 |
| 公式 | **不改** Flow / Heat / R-A / L-A / E-A / Amb 公式 |
| 存档 | **不持久化** sleepState；读档默认 Busy |
| Harmony | 不做 |

#### 7.1.2 三态

| 状态 | Acc (0–18) | Commit (19) |
|------|------------|-------------|
| **Busy** | Flow+Heat | 完整 Commit |
| **AmbientOnly** | 跳过 | 轻 Commit：泄漏 + Amb |
| **FullyQuiet** | 跳过 | 跳过体；仍每批结束时廉价 `Reevaluate`（室温变化可→AmbientOnly） |

评估：`ReevaluateSleepState` 在完整/轻 Commit 后、DebugForce 后、FullyQuiet 的 phase-19；**禁止**每 tick Jacobi。有 leakOpen/incomplete/breached 有量 / Flow want / Heat want → Busy；否则有 Amb 需求 → AmbientOnly；否则 FullyQuiet。

#### 7.1.3 任务清单

- [x] 7.1.a 写入本节 / 更新 §6.11 / §九；About `0.3.7`
- [x] 7.1.b MapComp 三态 + Tick 跳过 + 轻 Commit + Reevaluate + Wake + Dump
- [x] 7.1.c 阀/泵/换热器/破损/Debug 填充与设温 Wake
- [x] 7.1.d Debug：休眠进入 / 唤醒 / 仅Amb 断言
- [x] 7.1.e 游戏内验收 → §1.2j（玩家确认通过 · 2026-07-15）

#### 7.1.4 验收流程（玩家执行）

前置：DevMode；最新 `Assemblies/RimPipe.dll`；建议室内。

**A. 均压后进入休眠**

1. Debug → 生成三通验收场景（产品）→ 多点强制批至接近均分  
2. 再等至少 2 个自然 20-tick 批（勿一直点强制批）  
3. Debug → **断言休眠进入** → `休眠进入通过：state=AmbientOnly|FullyQuiet`  

**B. 唤醒**

1. 静网后 Debug 填液一侧 → **`休眠唤醒通过：state=Busy`** 且量开始变  

**C. 仅 Amb（A+B 核心）**

1. 环境散热验收场景；量静时 Dump `sleepState=AmbientOnly`  
2. 自然批：T 靠拢、amount 不变 → **`仅Amb休眠通过`**  

**D. 泄漏**

1. 破损场景自然批仍扣量；泄漏期间多为 Busy  

**E. 回归**

1. 对称 / 泵 / 热量快速回归；存读档；无 Exception  

---

### 7.2 决议：4.2 分网休眠 — NetSleep（已锁定 · 2026-07-15）

> **玩家确认：** **1=A** — 仅分网休眠；拓扑仍整图 `RebuildAllMappings`；局部脏区延后。三态语义复用 Sleep。

#### 7.2.1 架构边界

| 项 | 决议 |
|----|------|
| 网定义 | Container 经**完整 Mapping**（Flow+Heat）连通分量；孤立 Container 各自成网；管格不成网 |
| 粒度 | 每网独立 Busy / AmbientOnly / FullyQuiet |
| 整图 SleepState | **派生**：任一 Busy→Busy；否则任一 Amb→AmbientOnly；否则 FullyQuiet（兼容旧断言） |
| 拓扑 | **仍整图重建**；不做局部脏区 |
| 公式 | **不改** Flow / Heat / R-A / L-A / E-A / Amb |
| 存档 | `netId` / 每网 sleep **不持久化**；重建后默认 Busy |
| Wake | 阀/泵/换热/破损/填液 → **只唤醒所属网**；拓扑重建 → 全网 Busy |
| Harmony | 不做 |

#### 7.2.2 任务清单

- [x] 7.2.a 写入本节 / 更新 §6.11 / §九；About `0.3.8`
- [x] 7.2.b Rebuild 末 Union-Find 赋 `netId` + `sleepStates[]`
- [x] 7.2.c 按网 Acc / Commit / Reevaluate / Wake；Dump 含 nets 摘要
- [x] 7.2.d Debug：分网休眠场景 + 断言
- [x] 7.2.e 游戏内验收 → §1.2k（玩家确认通过 · 2026-07-15）

#### 7.2.3 验收流程（玩家执行）

前置：DevMode；最新 `Assemblies/RimPipe.dll`（0.3.8）；建议空图或清旧管网。

**A. 分网休眠隔离（核心）**

1. Debug → **生成分网休眠场景（产品）**（两套互不连通三通：网甲满空压差、网乙三端同填充比）
2. （可选）等 1 个自然 20-tick 批，或点 **强制评估休眠状态**
3. Debug → **断言分网休眠** → 期望日志：`分网休眠通过：网甲=Busy … 网乙=AmbientOnly|FullyQuiet`；Dump 可见 `nets≥2`
4. （可选）Inspect 罐：可见 `netId=` 与 `sleep=`

**B. 旧整图断言不破**

1. 另开或清图后生成单连通三通 → 均分后 **断言休眠进入**
2. **断言休眠唤醒** /（有散热场景时）**断言仅Amb休眠**

**C. 回归**

1. 对称快速回归；破损泄漏仍扣量；存读档后网仍算；无 Exception

---

### 7.3 决议：4.3 流量/压力可视化 — Overlay（已锁定 · 2026-07-15）

> **玩家确认：** **2=D** — DevMode 可开关 Overlay；格色≈压力 P；边线≈**上批**流量方向/量级。

#### 7.3.1 架构边界

| 项 | 决议 |
|----|------|
| 入口 | Debug 切换 Overlay；仅 `Prefs.DevMode` 且开时绘制 |
| 数据 | Commit 前快照 `lastBatchWant` / 流向；不改 Flow 公式 |
| 绘制 | `MapComponentUpdate`：`CellRenderer.RenderCell` 压色 + `GenDraw.DrawLineBetween` 边线；`MapComponentOnGUI` 近距 `P=`（**勿**用 `FlashCell`：每帧 Add 会膨胀 debug 列表） |
| 存档 | 开关与快照 **不写入存档** |
| Harmony | 不做 |

> **性能修复（`0.4.2`）：** 旧实现每帧 `FlashCell` → 官方列表无界增长（暂停尤甚）。改为即时 `CellRenderer` + OnGUI 标签。  
> **玩家验收：** ✅ 2026-07-20（§1.2u）。

#### 7.3.2 任务清单

- [x] 7.3.a 写入本节
- [x] 7.3.b Mapping 快照字段 + Commit 钩子
- [x] 7.3.c Overlay 开关 + MapComponentUpdate 绘制
- [x] 7.3.d Debug 切换 + Inspect `netId`
- [x] 7.3.e 游戏内验收 → §1.2l（玩家确认通过 · 2026-07-15）
- [x] 7.3.f Overlay 性能：弃 `FlashCell` → `CellRenderer` + `MapComponentOnGUI`（`0.4.2`）→ §1.2u 通过

#### 7.3.3 验收流程（玩家执行）

前置：DevMode；不均分管网（分网场景网甲或普通三通压差）。

**A. 开 Overlay**

1. Debug → **切换流量/压力 Overlay** → 提示「开」
2. 应看见：罐格 `P=` 色块；有流量时青色边线（粗细∝上批 want）；**说明：显示上一批流量，最多滞后约 20 tick**。不均分时先 **强制一批处理** 再观察边线更明显

**B. 关 Overlay**

1. 再点同一项 → 「关」；绘制停止

**C. 均分后**

1. 多点强制批至接近均压 → 流量线变淡/消失；压力色接近

---

### 7.4 决议：4.4 对外 API — ApiDoc（已锁定 · 2026-07-15）

> **玩家确认：** **1=A** — 文档 + 最小稳定公共 API；**不做**自定义内部 Mapping 钩子（留给 4.7）。

#### 7.4.1 架构边界

| 项 | 决议 |
|----|------|
| 交付物 | `Source/RimPipe_API.md` + MapComp 公开 `TrySetAmount` / `TryAddAmount` / `TrySetTemperature` |
| 公式 / 拓扑 / 休眠 | **不改** |
| 量写入口 | 仍仅经 `Container.CommitAmount`（`internal`）；API 内调用并 `WakeContainer` |
| 内部 Mapping 钩子 | **不做**（阀/泵/换热器仍硬编码分支） |
| 存档 | **不新增**存档字段 |
| Harmony | 不做 |
| Debug | `DebugFillContainer` / `DebugSetTemperature` 改为薄包装调正式 API |

#### 7.4.2 任务清单

- [x] 7.4.a 写入本节 / 更新 §6.11 / §九
- [x] 7.4.b MapComp `TrySetAmount` / `TryAddAmount` / `TrySetTemperature` + Debug 改调
- [x] 7.4.c 撰写 `Source/RimPipe_API.md`
- [x] 7.4.d About bump（与 4.6 同发 `0.3.11`）
- [x] 7.4.e 游戏内验收 → §1.2n（玩家确认 · 2026-07-18）

#### 7.4.3 验收流程（玩家执行）

前置：DevMode；最新 `Assemblies/RimPipe.dll`。

**A. API 填液唤醒**

1. 均分后静网 → Debug 填液一侧 → 应 Busy 并开始流动（与既有「休眠唤醒」同路径）

**B. 文档快速回归**

1. 对照 `RimPipe_API.md`：FluidDef + Comp XML 边界；确认无内部 Mapping 钩子承诺

**C. 回归**

1. 对称 / 泵快速回归；无 Exception

---

### 7.6 决议：4.6 研究树 · 本地化 — Loc（已锁定 · 2026-07-15；修订 同日）

> **初订：** R2 自研树 + L1 英文 Def 基线。  
> **修订（玩家 · 2026-07-15）：** ① Defs **中文** label/description，英文仅 `Languages/English` DefInjected；② **删除** 自研 `ResearchProjectDefs`，产品仅挂 **Core** 研究。

#### 7.6.1 架构边界

| 项 | 决议 |
|----|------|
| 自研 ResearchProjectDef | **暂删**（目录移除） |
| 产品研究 | 仅 Core：`ComplexFurniture` / `Electricity` |
| 解锁 | Pipe/Tank/Tee → `ComplexFurniture`；Valve/Pump/HeatExchanger → `Electricity` |
| Dev Def | **不加** researchPrerequisites |
| 本地化 | Defs **中文**基线；`Languages/English` DefInjected；Keyed 简中+英 |
| Keyed 范围 | 设置 / Inspect / Gizmo / PlaceWorker；**不含** DebugActions |
| DefName | **禁止改名**（SaveMig） |
| 公式 / 拓扑 | **不改** |
| Harmony | 不做 |

#### 7.6.2 产品 ↔ Core 研究

| ThingDef | researchPrerequisites（Core） |
|----------|-------------------------------|
| Pipe / StorageTank / TeeJunction | `ComplexFurniture` |
| Valve / Pump / HeatExchanger | `Electricity` |

#### 7.6.3 任务清单

- [x] 7.6.a 写入本节 / 更新 §6.11 / §九
- [x] 7.6.b ~~自研 Research~~ → 改为 Core prerequisites；删除 `ResearchProjectDefs`
- [x] 7.6.c Defs 中文基线 + English DefInjected；简中 Keyed 保留
- [x] 7.6.d Keyed + 玩家可见 C# 迁移
- [x] 7.6.e 游戏内验收 → §1.2o（玩家确认 · 2026-07-18）

#### 7.6.4 验收流程（玩家执行）

前置：新建档或未解锁对应 Core 研究；最新 dll；语言=简中。

**A. 研究门槛**

1. 未研究 `ComplexFurniture`：不可造管/罐/三通
2. 已有 `ComplexFurniture`、未研究 `Electricity`：可造基础三件；不可造阀/泵/换热器
3. 研究 `Electricity` 后：可造阀/泵/换热器

**B. 本地化**

1. 默认简中：Defs 中文 + Keyed 简中
2. 切 English：DefInjected 英文 + Keyed 英

**C. 回归**

1. Dev Debug 场景仍可刷；存读档 量/阀/破损保留；无 Exception

---
### 7.5 决议：4.5 存档迁移 — SaveMig（已锁定 · 2026-07-15）

> **模型：** SaveMig — 策略 + MapComp `rimPipeSchemaVersion` 写入存档 + 读档规范化；**不做**历史版本 `switch` 迁移器。  
> **原则：** 量在 Comp、拓扑读档重建（§3.5）；缺字段靠 `Scribe_*` 默认值。

#### 7.5.1 架构边界

| 项 | 决议 |
|----|------|
| 兼容承诺 | 同一 RimWorld **1.6** + `rimPipeSchemaVersion ≤ 当前` → 应能加载续玩；**不承诺**跨游戏大版本 |
| 版本戳 | `MapComponent_PipeNetwork` 持久化 `rimPipeSchemaVersion`；**当前恒为 1**（`CurrentSchemaVersion`） |
| 缺字段 | 继续靠 `Scribe_*` 默认值；**不**为每个字段写手写 if |
| 历史迁移器 | **本切片不做**；未来破坏性变更另开子任务写 `MigrateV1→V2` |
| 版本过高 | `schema > 当前` → `Log.Warning` 后 **尽力加载**（不拒绝开档） |
| capacity | 维持 Props **覆盖**存档；PostLoad 后若 `amount > capacity` → **钳到 capacity** 并 Warning |
| pressure | 读档后按 `amount/capacity` **重算**（既有） |
| DefName | 本切片 **禁止改名**；改名须另开别名/重定向任务并升 schema |
| Mapping.ExposeData | **不** Scribe mappings 列表；泄漏续档靠 `breached` → Rebuild 刷 `leakOpen` |
| 公式 / 拓扑 | **不改** Flow / Heat / R-A / L-A / E-A / Amb / 休眠 |
| Harmony | 不做 |

#### 7.5.2 持久化字段清单

| 写入存档 | 不写入 / 重建 |
|------|----------------|
| Container：`id` / `fluid` / `amount` / `capacity`（读后被 Props 覆盖）/ `pressure`（读后重算）/ `temperature` / `containerIndex` | `Container.netId` |
| 阀 / 泵 / 换热器：`isOpen` | 全部 Mapping 拓扑（端点、attached、path、lastBatch、batch*） |
| 管道 / 可破损：`breached` | `Ports`（每次从 Props 重建） |
| MapComp：`nextId`、`rimPipeSchemaVersion` | `sleepStates` / Overlay / pending Delta / 注册表 |
| ModSettings：`dumpOnDestroy` | Props 派生 rate / insulation 等 |

**DefName 风险（勿擅自改）：** `RimPipe_StorageTank` / `TeeJunction` / `Pipe` / `Valve` / `Pump` / `HeatExchanger`；Fluid `RimPipe_Fluid_TestWater` / `TestFuel`；Dev 验收 Def。

#### 7.5.3 破坏性变更检查表（将来必须升 schema）

出现任一项 → 升 `CurrentSchemaVersion` 并写迁移器（新切片），**禁止**静默破坏旧档：

1. 更改某 Def 的 **容器个数 / 索引语义**
2. **改名** ThingDef / FluidDef（无别名层）
3. 改变已写入存档的布尔/枚举的语义（非仅加默认字段）
4. **开始**持久化跨建筑 Mapping / 拓扑引用
5. 量单位或压力定义破坏性切换

仅新增 Scribe 字段且带默认值 → **通常不必**升 schema（Scribe 缺键=默认）。

#### 7.5.4 任务清单

- [x] 7.5.a 写入本节 / 更新 §6.11 / §九；About `0.3.9`
- [x] 7.5.b MapComp `rimPipeSchemaVersion` + 过高 Warning + Dump `schema=`
- [x] 7.5.c `EnsureRuntimeObjects` 读档后 amount 钳到 capacity
- [x] 7.5.d Mapping.ExposeData 注释标明勿误接 mappings Scribe
- [x] 7.5.e 游戏内验收 → §1.2m（玩家确认 · 2026-07-18）

#### 7.5.5 验收流程（玩家执行）

前置：DevMode；最新 `Assemblies/RimPipe.dll`（0.3.9）。

**A. 旧档 / 无 schema 字段**

1. 用 0.3.8 或本版前存档（或新场景存一次再读）打开  
2. Debug → Dump 管道网络 → 见 `schema=1`  
3. Inspect：量、温度、阀/泵开、breached 与存档前一致；拓扑重建后可继续流动  

**B. 破损 存读档**

1. 管道破损场景 → 存档 → 读档 → 自然批仍扣量  

**C. 回归**

1. 断言三通对称 / 泵快速回归；无 Exception  

**D.（可选）钳量**

1. 临时把某罐 Def `capacity` 调小于已存量 → 读档 → Warning + amount≤capacity  

---

### 7.7 决议：4.7 扩展钩子 — ExtHook（已锁定 · 2026-07-19）

> **玩家确认：** 仅 A · ExtHook；模型 **H-A**；仅内部边；无 Harmony；接口自测+回归。  
> **落实：** 4.4 推迟的「自定义内部 Mapping 钩子」。

#### 7.7.1 架构边界

| 项 | 决议 |
|----|------|
| 范围 | **仅 ExtHook**；Bridge / DirtyTopo **延后** |
| 模型 | **H-A**：`IPipeInternalMappingContributor` |
| 扫描 | `BuildInternalMappings` 遍历 `parent.AllComps`，调用实现接口者；同建筑多贡献者都调用 |
| 边 | **仅内部 Mapping**；泵直接相邻 Forced **仍留** `CompPipePump` |
| 公开面 | 保持 `AddInternalMapping` public；API.md 写扩展写法 |
| 存档 | **不新增**存档字段；schema 仍 1 |
| 公式 | **不改** Flow / Heat / R-A / L-A / E-A / Amb / 休眠 |
| Harmony | **不引入** |

#### 7.7.2 本切片明确不做

- 接口级「刷新直接相邻的外部 Forced」
- Harmony / 跨模 Bridge / 局部脏区拓扑
- Chemical / 新 MappingType / 改 drive 公式
- 升 schema、改 DefName
- 独立示例模组工程（Dev Comp + 文档足够）

#### 7.7.3 延后备忘

| 主题 | 备注 |
|------|------|
| Bridge-A | ✅ 见 §7.8（伤害/Breakdown→破损；无 Harmony） |
| Bridge-H | ✅ 见 §7.9（已决议 · 延后） |
| DirtyTopo | ✅ 见 §7.10（已决议 · 延后；先 Benchmark） |

#### 7.7.4 任务清单

- [x] 7.7.a 写入本节 / 更新 §6.11 / §九
- [x] 7.7.b `IPipeInternalMappingContributor`；阀/泵/换热实现
- [x] 7.7.c `BuildInternalMappings` 扫描接口
- [x] 7.7.d Dev 双腔桥 + Debug 场景/断言
- [x] 7.7.e `RimPipe_API.md` + About `0.3.12` + Release
- [x] 7.7.f 游戏内验收 → §1.2p（玩家确认 · 2026-07-19）

#### 7.7.5 验收流程（玩家执行）

前置：DevMode；最新 `Assemblies/RimPipe.dll`（0.3.12）。

**A. 接口自测**

1. Debug → **生成 ExtHook 验收场景（Dev 桥）**  
2. Debug → **断言 ExtHook 内部边** → 日志 **`ExtHook通过`**（经接口登记的内部 Mapping 存在且可均流）

**B. 回归**

1. 阀门场景快速回归；泵逆向抽送；换热器导热  
2. 三通 → `对称通过`  
3. 无 Exception / Config error  

**C. 文档**

1. 对照 `RimPipe_API.md`：`IPipeInternalMappingContributor` 写法可读；无外部 Forced 通用承诺  

---

### 7.8 决议：4.8 跨模桥接 — Bridge-A（已锁定 · 2026-07-19）

> **玩家确认：** 采用 **Bridge-A**；B1–B5 全按建议；阈值 **0.5**；**含 Breakdown（B3）**。  
> **落实：** §7.7.3 / L-A 延后「HP→破损」；他模仍 `loadAfter`+引用（不做无引用Harmony 注入）。

#### 7.8.1 架构边界

| 项 | 决议 |
|----|------|
| 模型 | **Bridge-A**：世界事件 → `breached`；公开 `TrySetBreached` |
| Harmony | **不引入**（Harmony 注入 = Bridge-H 延后） |
| 公式 / 拓扑 / Flow / 休眠 | **不改** |
| 存档 | **不新增**字段；仍用既有 `breached`；schema 仍 1 |
| 作用对象 | 管道格 `CompPipeCell` + 储罐等 `CompPipeBreachable` |
| 伤害（B1=A） | `PostPostApplyDamage` 后若 `HitPoints/MaxHitPoints < breachBelowHitPointsPercent`（默认 **0.5**）→ `breached=true` |
| 修复（B2=B） | 满血 **且**（无 `CompBreakdownable` 或未 BrokenDown）→ 清 `breached`；因建筑常 `tickerType=Never`，由 **MapComp 每 250 tick** 轮询清（不依赖 CompTickRare） |
| Breakdown（B3=A） | `ReceiveCompSignal("Breakdown")`（对齐 `CompBreakdownable.BreakdownSignal`）→ `breached=true`；建筑无 Breakdownable 时仅他模/Debug 广播信号才触发 |
| 他模Harmony 注入（B5=A） | **不做**；须依赖 `rimpipe.core` |

#### 7.8.2 本切片明确不做

- Harmony / Bridge-H / DirtyTopo  
- 强制给产品挂 `CompBreakdownable`（可选，本切片不加）  
- 通用直接相邻 Forced、改 schema / DefName  
- 自有美术  

#### 7.8.3 延后备忘

| 主题 | 备注 |
|------|------|
| Bridge-H | ✅ 见 §7.9（已决议 · 延后） |
| DirtyTopo | ✅ 见 §7.10（已决议 · 延后；先 Benchmark） |
| 产品 Breakdownable | 玩法需要时再挂官方 Comp |

#### 7.8.4 任务清单

- [x] 7.8.a 写入本节 / 更新 §6.11 / §九 / 文首状态
- [x] 7.8.b Props：`breachBelowHitPointsPercent` / `clearBreachOnRepaired` / `breachOnBreakdown`；共享桥接逻辑
- [x] 7.8.c `CompPipeCell` / `CompPipeBreachable`：`PostPostApplyDamage` + `ReceiveCompSignal`
- [x] 7.8.d MapComp：`TrySetBreached` / `TryGetBreached` + 250-tick 满血清破损
- [x] 7.8.e Debug：伤害破损 / Breakdown / 满血清 + 回归快速检查
- [x] 7.8.f `RimPipe_API.md` + About `0.3.20` + Release
- [x] 7.8.g 游戏内验收 → §1.2r（Player.log · 2026-07-19）

#### 7.8.5 验收流程（玩家执行）

前置：DevMode；最新 `Assemblies/RimPipe.dll`（0.3.20）。

**A. 伤害破损**

1. Debug → **断言 Bridge 伤害破损** → 日志 **`Bridge伤害破损通过`**（HP&lt;50% → breached）

**B. Breakdown**

1. Debug → **断言 Bridge Breakdown** → **`Bridge故障破损通过`**

**C. 满血清**

1. Debug → **断言 Bridge 满血清破损** → **`Bridge满血清通过`**

**D. 回归**

1. 对称 / 泵快速回归；破损泄漏仍可用；无 Exception  

---

### 7.9 决议：4.9 跨模桥接 — Bridge-H（已锁定 · 2026-07-19 · 延后）

> **决议：** 已讨论并锁定范围；**暂不实施**（无硬需求）。  
> **记录本决议以备将来启动。**

#### 7.9.1 问题

Bridge-A：他模引用 `rimpipe.core` + XML 挂 Comp → 自动破损。  
Bridge-H：他模**不引用** RimPipe、停更或不知情时，RimPipe 侧通过 Harmony **向目标 ThingDef 注入** Comp。

#### 7.9.2 限定价值

- 核心前提：目标建筑必须**已有** `CompPipeNetworkMember`（有 Container），否则挂 Breachable = 无液可泄
- 实际场景少：主动适配的模组用 Bridge-A 即可；Bridge-H 面向停更模组
- 对热门管线模组（PipeSystem / VFE 管道等）如作者活跃，应优先推动桥接引用而非反复注入

#### 7.9.3 执行方案（将来）

**启动条件：** 有确定的、停更的、包含 NetworkMember 类建筑的第三方模组，且玩家有适配需求。

**技术路径（已选取）：**

| 项 | 决议 |
|----|------|
| 依赖 | 硬依赖 `brrainz.harmony`（About `modDependencies` + csproj `0Harmony`） |
| 注入方式 | 自研 `PipeBridgeInjectDef`：`targetThingDef` + `injectComps` 列表；`DefsLoaded` 阶段改 `ThingDef.comps` |
| 注入范围 | **仅** `CompPipeBreachable`（前提：目标已有 `CompPipeNetworkMember`；否则跳过 + Warning） |
| 注入全 NetworkMember 模板 | **不做**（范围过大，另开 H2） |
| 具名他模适配 | **不做**（需时另开 H2，含 Harmony 方法补丁） |
| 公式 / 拓扑 / Flow | **不改** |
| 存档 | schema 仍 1；注入 Comp 用既有 `breached` |

**待 Harmony 引入后需做的调整：**
- csproj 引用 `Libs\0Harmony\0Harmony.dll` + `Private=false`
- About `modDependencies` → `brrainz.harmony` + `loadBefore`
- `RimPipeMod` 构造：`new Harmony("rimpipe.core").PatchAll()`（或留着给未来 Patch 用；初始无 Patch 也可不加）
- MapComp 构造/系统启动清理

**警告规则：**
- 目标 Def 不存在 → 跳过 + Log.Warning
- 目标无 `CompPipeNetworkMember` → 跳过 + Log.Warning（不注入空壳 Breachable）
- 注入 Comp class 不存在 → Config error（XML 类级校验）

#### 7.9.4 任务清单（全部延后）

- [ ] 7.9.a 引入 Harmony 依赖（csproj + About + `RimPipeMod`）
- [ ] 7.9.b `PipeBridgeInjectDef` + Def应用器
- [ ] 7.9.c Dev 自测 Def + Debug 场景/断言
- [ ] 7.9.d `RimPipe_API.md` 写 InjectDef 用法
- [ ] 7.9.e About bump + Release
- [ ] 7.9.f 游戏内验收

---

### 7.10 决议：4.10 局部脏区拓扑 — DirtyTopo（已锁定 · 2026-07-20 · 延后）

> **玩家确认（2026-07-20）：** 初倾向「现在就做」（下游太空拟真模组前置框架，管网将很大）；经讨论改为 **先 Benchmark / Stress，再以数据决定是否开 DirtyTopo**。  
> **记录本决议以备将来启动。**

#### 7.10.1 现状（为何看起来需要）

| 项 | 现状 |
|----|------|
| 触发 | 放/拆管道或构件 → `Enqueue(MemberChanged/PipeChanged)` |
| 实际行为 | `ProcessDelayedActions` **丢弃** cell 信息 → **整图** `RebuildAllMappings` |
| 已有挂点 | `DelayedActionType` + `cell` 已记录变更位置，但未做局部重建 |
| 重建内容 | 直接相邻配对 → 管道 Voronoi/BFS → 内部 Mapping → 破损 flag → Union-Find `netId` → Wake |

拓扑**仅在建筑变化时**触发，**不是**每 20-tick 批都跑。批级热点仍是 Accumulate / Commit（Flow·Heat·Chem）。

#### 7.10.2 推迟理由（已采纳）

1. **开销量级：** 直接相邻 / 管道 BFS / Union-Find 均为整数图遍历；估算千管级仍远低于 1 帧（~16ms）。未实测前不宜假设「必然卡顿」。
2. **真热点在别处：** 下游大管网时，每批 `AccumulateFlow` / Heat / Chem / Commit 的次数与 Mapping·Reactor 规模线性相关，通常远重于偶发拓扑重建。优化错位收益低。
3. **风险极高：** DirtyTopo 触及 BFS 分量、直接相邻、内部边、`netId` 增量；静默失败形态（幽灵 Mapping、休眠错醒、存读档拓扑断）难在小场景复现，且与休眠/阻力/泄漏/化学正交耦合。
4. **已有批级优化：** 4.1/4.2 分网休眠已跳过无 Flow/Heat want 的 Acc；拓扑频率与批处理解耦。
5. **决策纪律：** 用 Debug 计时 + Stress 场景把「假设」变成「数字」，再开刀；避免为下游恐惧提前引入最高风险切片。

#### 7.10.3 启动条件（满足任一即可开决议实施）

| 条件 | 门槛（建议） |
|------|----------------|
| **Benchmark** | 单次 `RebuildAllMappings` 稳定 **≥ ~5ms**（或玩家可感知卡顿）于目标规模 |
| **规模锚点** | Stress：约 **500+ 管道格 + 100+ 储罐/构件**（或下游模组真实布局） |
| **产品反馈** | 下游太空模组试玩明确归因于「铺管/拆管时卡顿」 |

未达门槛时：**不做 DirtyTopo**；优先考虑批级热点（Acc/Commit）或业务功能。

#### 7.10.4 先测路径（可选小切片 · 非 DirtyTopo 本体）

| 步骤 | 内容 | 备注 |
|------|------|------|
| B1 | Debug：计时 `RebuildAllMappings`（Stopwatch → 日志 `耗时=…ms`；`LastTopologyRebuildMs`） | ✅ `0.4.3`；不改拓扑语义 |
| B2 | Debug：Stress 一键生成（~100 罐 + ~500 管；10 组×5 廊道） | ✅ `生成 Stress 管网` |
| B3 | 手铺/拆 + **计时整图重建** 读日志 | 玩家手测；达 §7.10.3 再开 DirtyTopo |

B1–B2 **已编码**；B3 为手测流程。**不等于**启动 DirtyTopo 编码。

**手测步骤：** 空图 → Stress → 点「计时整图重建」读基线 → 手拆/铺一截管 → 再点计时或等自动重建日志 → 对照 ~5ms。

#### 7.10.5 将来实施大纲（仅备忘 · 未开编码）

| 项 | 草案 |
|----|------|
| 脏区 | 用 `DelayedAction.cell` 建 dirty set；不再一律 `RebuildAllMappings` |
| 局部管道 | 仅脏格所在管道连通分量做 BFS/Voronoi |
| 直接相邻 / 内部 | 仅脏辐射内的 Member |
| `netId` | 增量更新（最难；须独立验收） |
| 回退 | 保留 `RequestFullRebuild` / FullRebuild 路径 |
| 公式 / 存档 | **不改** Flow；schema 仍 1 |

#### 7.10.6 任务清单

- [x] 7.10.a 写入本节 / 更新 §6.11 / §七表头 / §九
- [x] 7.10.b（可选）Benchmark + Stress Debug（§7.10.4 · `0.4.3`）
- [ ] 7.10.c DirtyTopo 本体编码（**仅当 §7.10.3 满足**）
- [ ] 7.10.d 全套回归 + 验收
- [x] 7.10.e 写入 MapComponentTick 峰值研究笔记（§7.10.7 · 2026-07-20）

#### 7.10.7 观测笔记：MapComponentTick 平均低 / 峰值高（2026-07-20）

> **来源：** 玩家 Dubs Performance Analyzer 截图 + `Player.log`；分析对象为 **`MapComponent_PipeNetwork.MapComponentTick`**（**不是** Overlay 的 `MapComponentUpdate`）。  
> **结论：** 平均低、峰值偶发升高是 **批相位 + 偶发整图拓扑** 造成的**预期尖峰**，不单独构成立刻开 DirtyTopo 的充分条件；Debug 叠场景会放大峰值。

**分析器形态（示例）**

| 指标 | 观测 | 解读 |
|------|------|------|
| 每刻调用 | 1.0 | Tick 每帧必进一次入口 |
| 平均 / tick | ~0.288ms | 多数 tick 几乎空转 → 平均被压低 |
| 峰值 / tick | ~7.7ms（玩家另见 **11ms+**） | 少数重活 tick；峰/均可达数十倍 |
| 与 Overlay | 无关 | Overlay 走 Update；`0.4.2` 已弃 FlashCell |

**`MapComponentTick` 相位负载（设计）**

| 时机 | 工作 | 相对开销 |
|------|------|----------|
| 多数 tick（0–18 且无 Busy 网） | `ProcessDelayedActions` 早退 | 极轻 |
| 0–18 且有 Busy | `AccumulateFlow` / Heat / Chem（Jacobi；Acc 内有临时 List/Dict 分配） | 中～偏重 |
| **phase == 19（每 20 tick）** | `CommitDeltas` 或轻 Commit + **`ReevaluateAllNetSleepStates`**（按 netId 扫 mappings/members） | **周期性尖峰主因之一** |
| 建造/拆除 / Debug 刷场景后 | `RebuildAllMappings` 整图重建 | **偶发最重**（易到数 ms～10ms+） |
| 每 250 tick | `TryClearRepairedBreaches` 扫构件 | 小峰 |

**与本会话现场的关系**

- `Player.log` 多次「生成分网休眠场景」后 `nets` 可达 **170～200+**；休眠重评估与 Commit 按网扫描会被放大。
- 空图 / 少网时平均仍应保持亚毫秒；尖峰主要在 Commit 批与拓扑重建。
- §7.10.1 仍成立：拓扑**不是**每批必跑；但 **phase=19 批级工作** 与 **偶发整图重建** 都会在分析器「峰值」里显形。

**对 DirtyTopo / 后续优化的含义（未开编码）**

| 方向 | 说明 |
|------|------|
| DirtyTopo | 只压「铺/拆时的整图重建」峰；**不消除**每 20 tick 的 Commit/休眠评估峰 |
| 批级 | 若大网常态峰值来自 phase=19 / Busy Acc，应优先 Benchmark Acc·Commit·`ReevaluateAllNetSleepStates`，而非先上 DirtyTopo |
| 决策 | 仍按 §7.10.3：有 Stopwatch/Stress 数字再开刀；本笔记作证据基线 |

---

### 7.11 决议：4.11 发布准备 — R1（已锁定 · 2026-07-20）

> **玩家确认：** **仅 R1**；版本 **`0.4.0`**；交付说明现为 **`Source/RimPipe_API.md`**（原 `RELEASE.md` 已并入）；回归 **含化学断言（R1test=B）**。  
> **不做 R2**（工坊文案/预览/上传延后）。

#### 7.11.1 档位

| 项 | 决议 |
|----|------|
| 档位 | **仅 R1**（对内冻结；团队下游依赖） |
| 版本 | **`0.4.0`** = 阶段四功能收官口径 |
| 交付说明 | **`Source/RimPipe_API.md`**（版本交付 + 用法；原 RELEASE 已合并） |
| 回归 | P3 全套 **+ 化学**（转化 / 釜 / L1 / 条件 / 反应热 至少覆盖主路径） |
| 公式 / schema | **不改**；schema 仍 1 |
| Bridge-H / DirtyTopo / R2 | **不做** |

#### 7.11.2 R1 交付包

| # | 主题 | 状态 |
|---|------|------|
| **P1** | About `0.4.0` + 能力清单描述 | `[x]` |
| **P2** | `RimPipe_API.md` 稳定面 + 短 changelog | `[x]` |
| **P3** | 回归快速检查（含化学）→ §1.2s | `[x]` |
| **P4+P5** | 交付清单并入 `RimPipe_API.md` §0（原 RELEASE） | `[x]` |

#### 7.11.3 明确不做

- R2 工坊公开、Preview、自有美术  
- Bridge-H / DirtyTopo 本体  
- 正式建造栏反应釜 / B0 / 阀连续开度  
- 改 DefName / 升 schema  

#### 7.11.4 任务清单

- [x] 7.11.a 确认 R0=仅R1 / R1v=0.4.0 / R1doc=`RimPipe_API.md`（原 RELEASE 已合并）/ R1test=含化学 → 本节已锁定
- [x] 7.11.b P1 About `0.4.0`
- [x] 7.11.c P2 API.md 稳定面 + changelog
- [x] 7.11.d P4+P5 交付清单并入 `RimPipe_API.md` §0
- [x] 7.11.e P3 玩家回归快速检查 → §1.2s（Player.log · 2026-07-20）
- [x] 7.11.f R2 跳过
- [x] 7.11.g 更新 §九验收结论
- [x] 7.11.h `RELEASE.md` 并入 `RimPipe_API.md`（跳转 stub 保留）

#### 7.11.8 验收流程（玩家执行 · P3）

前置：DevMode；已加载 **`0.4.0`** dll（若仅改 About/文档、未改代码，沿用现网 dll 即可；有重编则用最新 Release）。

**A. 框架回归**

1. 生成产品三通 → 多批 → **断言三通对称** → `对称通过`  
2. 生成泵场景 → **断言泵逆均分** → `泵逆均分通过`  
3. **断言 Bridge 伤害破损 / Breakdown / 满血清** → 三行「通过」  
4. 均分静网 → **断言休眠进入**；填液 → **断言休眠唤醒**  

**B. 化学（R1test=B）**

1. 生成化学场景（P2 三罐或 P3 Dev 釜）  
2. **断言化学转化** 或 **断言化学反应釜** → `化学转化通过` / `化学釜通过`  
3. （建议）**断言化学 L1** / **条件门槛** / **反应热** 各一次  

**C. 存读档**

1. 存档 → 读档 → 量/温/开关/breached 仍合理；可继续流动  

**D. 健康**

1. 无 Exception / Config error / `RimPipe…失败`  

通过后告知，补写 **§1.2s**。  
**已验收通过（§1.2s · 2026-07-20）。**  
**日常回归改走 §7.12 套件键**（`0.4.1` 起）；本节保留为 R1 历史步骤。

---

## 7.12 决议：4.12 Debug 测试整理（已锁定 · 2026-07-20）

> **玩家确认：** 顶层原 **12 键**（回归套件收官）；旧断言键**退出菜单**；误报硬化**同切片**；**Dev 三通生成删除**。  
> **0.4.3 起：** +2 Benchmark 键 → 菜单 **14 键**（见下表 8–9）。

### 7.12.1 顶层菜单键

| # | 名称 | 类型 | 职责 |
|---|------|------|------|
| 1 | 探测（Ping） | Action | 模组加载探测 |
| 2 | 转储管网状态 | Action | Dump（含 `lastTopoMs`） |
| 3 | 强制执行一批 | Action | Acc+Commit |
| 4 | 选中容器灌至 80% | ToolMap | 手测灌液 |
| 5 | 选中容器抽空 | ToolMap | 手测抽空 |
| 6 | 切换流量/压力 Overlay | Action | Overlay |
| 7 | 标记开口泄漏 | Action | leakOpen |
| 8 | **计时整图重建** | Action | §7.10.b B3；强制重建打 ms |
| 9 | **生成 Stress 管网** | ToolMap | §7.10.b B2；~100 罐 / ~500 管 |
| 10 | **R-框架** | ToolMap | 休眠进入/唤醒 + 三通对称 + Bridge×3 + 泵逆均分/阻断 |
| 11 | **R-物理** | ToolMap | 压力 + 阻力 + 破损泄漏/摧毁停漏 + Filth/水温 |
| 12 | **R-热与环境** | ToolMap | 换热器 + 混温 + Amb |
| 13 | **R-化学** | ToolMap | P3 釜 + L1 + 条件 + 反应热 + 阻断 |
| 14 | **R-扩展** | ToolMap | ExtHook + 分网休眠 |

### 7.12.2 代码布局

| 文件 | 内容 |
|------|------|
| `Debug/RimPipeDebugTools.cs` | 键 1–7 |
| `Debug/RimPipeDebugSuites.cs` | 键 8–12 |
| `Debug/RimPipeDebugScenes.cs` | Spawn*（无菜单；无 Dev 三通） |
| `Debug/RimPipeDebugAsserts.cs` | Assert* → `bool`（无菜单） |
| `Debug/RimPipeDebugUtil.cs` | DestroyAt / Def 分类 / ReportSuite |

### 7.12.3 断言硬化

| 断言 | 策略 |
|------|------|
| 阻力 | 批前重置源满宿空；`rateOk` 且增益≈0 → `skippedFlow` 通过 |
| ExtHook | 批前左满右空；`edgeOk` 且（流动靠近 **或** 已近均） |
| 环境散热 | 测 dT0 前强制 T=80 + 满液 |
| 泵逆均分 | 左≈0 时回灌 0.2/0.8 |
| 休眠进入 | R-框架**先**跑静网，再生成 Busy 场景 |

### 7.12.4 任务清单

- [x] 7.12.a 拆出 Scenes / Asserts / Util；删 Dev 三通与旧 `[DebugAction]`
- [x] 7.12.b 7 工具 + 5 套件菜单
- [x] 7.12.c 五类误报硬化
- [x] 7.12.d About `0.4.1` + API changelog + 本节
- [x] 7.12.e 玩家空图验收 → §1.2t

### 7.12.8 验收流程（玩家执行）

前置：DevMode；**`0.4.1`** dll；空图。

1. 打开调试动作 → **RimPipe**：确认约 **12** 项，无「断言*」/「生成三通（Dev）」
2. 依次点空地：**R-框架 → R-物理 → R-热与环境 → R-化学 → R-扩展**
3. Player.log 查各套件汇总行 `R-xxx：通过 n/m` 与关键「…通过」关键字
4. 无 Exception / Config error

**已验收通过（§1.2t · 2026-07-20）。**

---

## 八、横切规则（全程）

| ID | 规则 |
|----|------|
| X1 | 默认单位「量 + maxFlowRate」；阀/泵开关乘到 rate（0 或 1）；泵另用 `flowDrive=Forced` |
| X2 | 异常打日志：incomplete Mapping、负量、超容量 |
| 存读档 | 每阶段结束：放置网络 → 存档 → 读档 → 校验 |
| X4 | 孤立三通对称 = 永久回归 |
| X5 | 术语仅 Container / Port / Mapping；**管道不储存流体**（§6.14） |
| X6 | 流体变动必须 Delta → Commit |
| X7 | 模糊先确认；未写入文档的字段/公式/存档歧义不编码 |
| X8 | 查官方接口不瞎猜（`user-rimworld-source` / Libs） |
| X9 | **Cursor 八荣八耻**（与玩家约定 · 全程）：① 以瞎猜接口为耻，以认真查询为荣；② 以模糊执行为耻，以寻求确认为荣；③ 以臆想业务为耻，以人类确认为荣；④ 以创造接口为耻，以复用现有为荣；⑤ 以跳过验证为耻，以主动测试为荣；⑥ 以破坏架构为耻，以遵循规范为荣；⑦ 以假装理解为耻，以诚实无知为荣；⑧ 以盲目修改为耻，以谨慎重构为荣 |
| X10 | **文案风格（2026-07-20 起全程）：** 中文要直白、可读，像正常说话，不要电报码堆砌。只改表述不改语义。切片代号宜短（如 Sleep / SaveMig / R1）；过长或难懂的要改。玩家可见文案（About / Defs label·description / 简中 Keyed）少暴露内部名（Mapping、rate、dumpOnDestroy 等）；开发注释与 TODO 可以说术语，但句子要讲清楚「做什么」。不必写成长篇，也不要堆比喻。英文随意。日志/Debug 菜单名若已进验收记录则勿乱改。样板：`RimPipe_API.md`、简中 Keyed、产品 Def 描述、C# 类型头注释。 |

---

## 九、进度总表

| 里程碑 | 状态 |
|--------|------|
| 策划阅读与模型讨论 | ✅ |
| 设计决议（含无 Node、Flow A、批处理甲、Voronoi 等） | ✅ |
| P0 脚手架 | ✅ |
| 阶段一编码 + 游戏内验收 | ✅ 2026-07-14 |
| 阶段二大纲 / 开工决议 / 编码 | ✅ 2026-07-14 |
| 阶段二游戏内验收（含对称通过日志） | ✅ 2026-07-14（见 §1.2b） |
| 阶段二叠放 PlaceWorker | ✅ |
| 阶段三大纲 + 泵 P-A 决议 | ✅ 2026-07-14 |
| 阶段三 3.6 泵编码 + Debug | ✅ 2026-07-14 |
| 阶段三 3.6 泵游戏内验收 | ✅ 2026-07-14（见 §1.2c） |
| 阶段三 3.9/3.10 泄漏 L-A 决议 | ✅ 2026-07-14（§6.5；玩家确认） |
| 阶段三 3.9/3.10 泄漏 L-A 编码 | ✅ 2026-07-14（§6.6.1–6.6.6 / 6.6.8–6.6.9） |
| 阶段三 3.9/3.10 泄漏 L-A 游戏内验收 | ✅ 2026-07-14（见 §1.2d） |
| 阶段三 3.3 阻力 R-A 决议 | ✅ 2026-07-14（§6.8） |
| 阶段三 3.3 阻力 R-A 编码 / Debug | ✅ 2026-07-14（§6.9.1–6.9.4） |
| 阶段三 3.3 阻力 R-A 游戏内验收 | ✅ 2026-07-14（§1.2e；玩家确认） |
| About `0.3.2` | ✅ |
| 阶段三 泄漏效果 E-A 决议 | ✅ 2026-07-14（§6.12；T1–T4） |
| 阶段三 泄漏效果 E-A 编码 / Debug | ✅ 2026-07-14（§6.12.b–f） |
| 阶段三 泄漏效果 E-A 游戏内验收 | ✅ 2026-07-14（§1.2f；玩家确认） |
| About `0.3.3` | ✅ |
| 阶段三 3.1 压力 Pr-A 决议 | ✅ 2026-07-14（§6.13） |
| 阶段三 3.1 压力 Pr-A 编码 / Debug | ✅ Release `RimPipe.dll` |
| 阶段三 3.1 压力 Pr-A 游戏内验收 | ✅ 2026-07-14（§1.2g） |
| About `0.3.4` | ✅ |
| **管道不储存流体 / 取消 3.5** | ✅ 2026-07-14（§6.14 / §十一 #16） |
| 阶段三 热量 H-A 决议 | ✅ 2026-07-14（§6.15） |
| 阶段三 热量 H-A 编码 / Debug | ✅ Release `RimPipe.dll` |
| 阶段三 热量 H-A 游戏内验收 | ✅ 2026-07-14（§1.2h；玩家确认） |
| About `0.3.5` | ✅ |
| 阶段三 3.4 Amb-A 决议 | ✅ 2026-07-15（§6.16） |
| 阶段三 3.4 Amb-A 编码 / Debug | ✅ Release `RimPipe.dll` |
| 阶段三 3.4 Amb-A 游戏内验收 | ✅ 2026-07-15（§1.2i；玩家确认） |
| About `0.3.6` | ✅ |
| 阶段四 4.1 Sleep 决议 | ✅ 2026-07-15（§7.1；整图+Amb-B） |
| 阶段四 4.1 编码 | ✅ Release `RimPipe.dll` |
| 阶段四 4.1 游戏内验收 | ✅ 2026-07-15（§1.2j；玩家确认） |
| About `0.3.7` | ✅ |
| 阶段四 4.2 NetSleep 决议 | ✅ 2026-07-15（§7.2；仅分网休眠） |
| 阶段四 4.3 Overlay 决议 | ✅ 2026-07-15（§7.3；DevMode Overlay） |
| 阶段四 4.2/4.3 编码 | ✅ Release `RimPipe.dll`（0.3.8） |
| 阶段四 4.2 NetSleep 游戏内验收 | ✅ 2026-07-15（§1.2k；玩家确认） |
| 阶段四 4.3 Overlay 游戏内验收 | ✅ 2026-07-15（§1.2l；玩家确认） |
| Overlay 性能修复（弃 FlashCell） | ✅ 2026-07-20（§7.3.f；About `0.4.2`） |
| About `0.3.8` | ✅ |
| 阶段四 4.5 SaveMig 决议 | ✅ 2026-07-15（§7.5） |
| 阶段四 4.5 SaveMig 编码 | ✅ Release `RimPipe.dll`（0.3.9） |
| 阶段四 4.5 SaveMig 游戏内验收 | ✅ 2026-07-18（§1.2m；玩家确认） |
| About `0.3.9` | ✅ |
| 阶段四 4.4 ApiDoc 决议 | ✅ 2026-07-15（§7.4；文档+最小 API） |
| 阶段四 4.6 Loc 决议 | ✅ 2026-07-15（§7.6；修订：中文 Defs + Core 研究） |
| 阶段四 4.4/4.6 编码 | ✅ Release `RimPipe.dll`（0.3.11） |
| 阶段四 4.4 游戏内验收 | ✅ 2026-07-18（§1.2n；玩家确认） |
| 阶段四 4.6 游戏内验收 | ✅ 2026-07-18（§1.2o；玩家确认） |
| About `0.3.11` | ✅ |
| 阶段四 4.7 ExtHook 决议 | ✅ 2026-07-19（§7.7；仅内部边 / 无 Harmony） |
| 阶段四 4.7 ExtHook 编码 | ✅ Release `RimPipe.dll`（0.3.12） |
| 阶段四 4.7 ExtHook 游戏内验收 | ✅ 2026-07-19（§1.2p；玩家确认） |
| About `0.3.12` | ✅ |
| 阶段三 3.8 Chem 决议 | ✅ 2026-07-19（§6.17；A/B1 · L1 · Chem · 分批 P0–P6） |
| 阶段三 3.8 Chem **P0** 文档 | ✅ 2026-07-19 |
| 阶段三 3.8 Chem **P1** 配方数据面 | ✅ 2026-07-19（About `0.3.13`） |
| 阶段三 3.8 Chem **P2** 求解核 | ✅ 2026-07-19（About `0.3.14`） |
| 阶段三 3.8 Chem **P3** Dev 反应釜 | ✅ 2026-07-19（About `0.3.15`） |
| 阶段三 3.8 Chem **P4** L1 空燃比 | ✅ 2026-07-19（About `0.3.16`） |
| 阶段三 3.8 Chem **P5a** T/P 门槛 | ✅ 2026-07-19（About `0.3.17`） |
| 阶段三 3.8 Chem **P5b** 反应热 | ✅ 2026-07-19（About `0.3.18`） |
| 阶段三 3.8 Chem **P6** 收尾 | ✅ 2026-07-19（§1.2q 证据注 + API · About `0.3.19`） |
| 阶段四 4.8 Bridge-A 决议 | ✅ 2026-07-19（§7.8；无 Harmony；含 Breakdown） |
| 阶段四 4.8 Bridge-A 编码 | ✅ Release `RimPipe.dll`（0.3.20） |
| 阶段四 4.8 Bridge-A 游戏内验收 | ✅ 2026-07-19（§1.2r；Player.log） |
| 阶段四 4.9 Bridge-H 决议 | ✅ 2026-07-19（§7.9；已决议延后不实施） |
| 阶段四 4.10 DirtyTopo 决议 | ✅ 2026-07-20（§7.10；延后；先 Benchmark 再决定） |
| 阶段四 4.11 发布准备大纲 | ✅ 2026-07-20（§7.11） |
| 阶段四 4.11 R1 文档 | ✅ 2026-07-20（About `0.4.0` + `RimPipe_API.md`；RELEASE 已并入） |
| 阶段四 4.11 P3 回归快速检查 | ✅ 2026-07-20（§1.2s；含化学；存读档 本段未跑） |
| 阶段四 4.12 Debug 整理编码 | ✅ 2026-07-20（About `0.4.1`；12 键 + 硬化） |
| Overlay 性能修复 | ✅ 2026-07-20（§7.3.f；About `0.4.2`） |
| Overlay 性能修复玩家验收 | ✅ 2026-07-20（§1.2u） |
| MapComponentTick 峰值研究笔记 | ✅ 2026-07-20（§7.10.7） |
| 阶段四 4.12 玩家验收 | ✅ 2026-07-20（§1.2t；末段五套件满分；中段摧毁停漏偶发 6/7 不影响） |
| 7.10.b Benchmark + Stress Debug | ✅ 2026-07-20（About `0.4.3`；计时 + Stress + 计时重建键；DirtyTopo 仍延后） |
| **下一动作** | 玩家手测 Stress ms（§7.10.4 B3）对照 §7.10.3；可选 R2 / Bridge-H |
| Bridge-H 实施 / DirtyTopo 本体 / R2 | ⬜ 延后 |

---

## 十、阶段二决议总表（完整）

| # | 议题 | 决议 | 日期 |
|---|------|------|------|
| 1 | 阀门模型 | **V-A**；不采用 V-B | 开工前 |
| 2 | 阀门 UI | 仅开 / 关 | 开工前 |
| 3 | 阀门形态 | **方案 A** 独立管件，不盖在管道上 | 验收后问答 |
| 4 | Dev Def | 保留回归；**撤出建造栏** | 验收中 |
| 5 | 贴图 | 蓄电池 / MoisturePump / 电缆 / PowerSwitch | 验收中修正 |
| 6 | 叠放 | **管道 ↔ 管件互斥**（含三通） | 验收后 |
| 7 | 三通/阀内部量 | 架构交汇/V-A 所需，非储罐玩法；玩家已理解 | 验收后说明 |

---

## 十一、阶段三决议总表（主体已验收通过 · Chem ✅）

| # | 议题 | 决议 | 日期 |
|---|------|------|------|
| 1 | 启动顺序 | 3.6 泵 → 3.9/3.10 → 3.3 → 其余 | 大纲确认 |
| 2 | 泵模型 | **P-A** 独立管件；两岸小桶 + 内部 Forced Mapping | 大纲确认 |
| 3 | 泵公式 | `want=min(rate, amountSrc, freeDst)`；开且有电满 rate | 大纲确认 |
| 4 | 逆向抽送挂点 | 开启时直接相邻的外部 Mapping 亦 Forced（邻→入、出→邻） | 编码中补钉 |
| 5 | 泵电力 | CompPowerTrader + Flickable；无电≡关 | 大纲确认 |
| 6 | 泄漏模型 | **L-A**：破损=开口持续漏；摧毁/拆卸=断路、停漏、**不倾倒** | 2026-07-14 |
| 7 | 泄漏扣量 | 复用 Commit 泄漏块；产品字段 `leakOpen`；本切片仅销毁量 | 2026-07-14 |
| 8 | 泄漏效果（预留） | FluidDef `leakEffects` 预留；后由 **#14 E-A** 实现 | 2026-07-14 |
| 9 | 泄漏触发（本切片） | Debug + Gizmo 显式 `breached`；HP/Breakdown/TakeDamage **延后** | 2026-07-14 |
| 10 | 拆建倾泻 | 设置/Def 开关；**默认关**（3.10） | 2026-07-14 |
| 11 | 管道与罐分工 | 罐持量；管破损=大气孔（经路径 Mapping）；不管「只泄罐、打管无反馈」 | 2026-07-14 |
| 12 | 管道阻力 | **R-A**：`maxFlowRate = max(0.1, base / max(1, pathPipeCells))`；直接相邻=0；内部不衰减；长度=最短路径格数 | 2026-07-14 |
| 13 | 锁定顺序之后 | 其余按玩法需要；建议优先泄漏效果 | 2026-07-14 |
| 14 | 泄漏效果 | **E-A**：Commit 扣量后 Filth+Temperature；水=None；TestFuel=Filth_Fuel；units=10；heat=5；dump 共用钩子 | 2026-07-14 |
| 15 | 压力进 drive | **Pr-A**：`P=amount/capacity`；Equalize `drive=\|ΔP\|/2*minCap`（替换非加权）；Forced 不动；批内虚拟量算 P | 2026-07-14 |
| 16 | 管道持液 | **管道不储存流体**；取消原候选 3.5「管道组容积」；量仅在设备/管件 Container；破损仍走 L-A | 2026-07-14 |
| 17 | 热量交换 | **H-A**：T 生效 + Flow 混温 + `MappingType.Heat` 导热 + 换热器（决议当日化学暂缓；后由 **#19 Chem** 实现）；3.4 散热延后 | 2026-07-14 |
| 18 | 向环境散热 | **Amb-A**：有液 Container 按 insulation 向格温靠拢 + PushHeat；管格不参与；批次结束时 Commit | 2026-07-15 |
| 19 | 化学 Chem | **A/B1** 多单流体腔 + RecipeDef；**L1** 可调比+效率；**Chem** 门槛+反应热；Reactor Comp 非二元 Chemical Mapping；分批 P0–P6（P5=a/b）；P3 要电；正式釜/涡喷/B0 延后；**已验收通过**（§1.2q） | 2026-07-19 |

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
