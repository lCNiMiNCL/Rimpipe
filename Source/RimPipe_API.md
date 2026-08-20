# RimPipe 下游说明

这篇文档给想依赖 RimPipe 的下游模组看：怎么挂依赖、本版能依赖什么、怎么用 XML / C# 扩展。  
这不是 Steam 工坊公开包说明。

**packageId：** `rimpipe.core` · **游戏：** RimWorld **1.6** · **存档 schema：** **1**（玩法稳定面以 **0.4.0** 对内冻结为准）

计划与决议细节见 [`docs/ROADMAP.md`](docs/ROADMAP.md) 与 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)；`Source/RimPipe_TODO.md` 为总索引。

---

## 0. 版本与交付

### 0.1 本版能做什么（摘要）

- **拓扑：** 设备直接相邻对接 + 管道 Voronoi 连通；每 20 tick 一批（Accumulate → Commit）
- **设备：** 储罐 / 三通 / 管道 / 阀门 / 泵 / 换热器；Dev 反应釜与测试件**不进建造栏**
- **物理：** 压力均分、路径阻力、破损泄漏与环境效果、热量与混温、环境散热、分网休眠
- **化学：** `PipeReactionDef` + `CompPipeReactor`（或运行时 `TryRegisterChemReactor`）
- **扩展：** `IPipeInternalMappingContributor`；`TrySetAmount` / `TrySetTemperature` / `TrySetBreached`
- **破损桥接（Bridge）：** Bridge-A 伤害阈值 / Breakdown → 破损，修满血后可清除（无 Harmony）；Bridge-H 可向已有 NetworkMember 的第三方建筑注入 Breachable（需 Harmony）

**已锁定：** 管道格**不储存流体**——量只在设备/管件的 `Container` 里。

### 0.2 本版可依赖 vs 暂不建议依赖

| 本版可依赖（0.7.x） | 延后 / 勿当稳定承诺 |
|--------------------|---------------------|
| FluidDef；标准 NetworkMember / PipeCell / 阀·泵·换热器 / Reactor 的 XML Comp；`PipeBridgeInjectDef`（Bridge-H 无引用注入） | 正式建造栏反应釜产品化、B0 混管 |
| `IPipeInternalMappingContributor`（**只**登记同建筑内部 Mapping） | DirtyTopo 等内部实现，不构成下游 API |
| `TrySetAmount` / `TryAddAmount` / `TrySetTemperature` | 通用「把直接相邻外部边改成 Forced」API |
| `TrySetBreached` / `TryGetBreached`；伤害/Breakdown 自动破损 | 正式建造栏反应釜产品化、B0 混管 |
| `TryRegisterChemReactor` 等；只读 Members / Mappings / ChemReactors / Sleep | 自有美术定稿；工坊 R2 包装 |
| 存档 schema=1；跨建筑 Mapping 读档后重建 | 擅自改 DefName / 未公告就升 schema |

多流体转化用 **`PipeReactionDef` + 反应釜**，不要用成对 `MappingType.Chemical` 硬凑。

### 0.3 运行时需要带走的路径

| 路径 | 用途 |
|------|------|
| `About/` | 元数据 |
| `Assemblies/RimPipe.dll` | 程序集 |
| `Defs/` | 流体 / 建筑 / 反应 / 分类 |
| `Languages/` | 简中 Keyed + 英文 DefInjected / Keyed |

可选随仓、游戏加载不强制要：

| 路径 | 用途 |
|------|------|
| `Source/RimPipe_API.md` | 本文件（下游说明） |
| `docs/ROADMAP.md` / `docs/ARCHITECTURE.md` | 计划与决议 |
| `Source/RimPipe/` | C# 源码 |

一般**不必**打进玩家包：`Source/RimPipe/obj/`、`Source/Libs/` 下的本地引用 dll（按你们工程约定即可）。

### 0.4 冻结回归（记录）

清单见 [`docs/ACCEPTANCE.md`](docs/ACCEPTANCE.md)（Debug 套件与旧逐步键）：对称、泵、Bridge×3、休眠、化学、存读档。  
通过记录：**§1.2s**（✅ 2026-07-20 · R1）；**§1.2t**（✅ 2026-07-20 · 4.12 Debug 整理 · 五套件满分）。

### 0.5 版本短记

| 版本 | 要点 |
|------|------|
| **0.7.0** | Bridge-H：新增 `PipeBridgeInjectDef`，可向目标 ThingDef 注入 `CompPipeBreachable`；新增 Harmony 前置；Dev/R-扩展增加 Bridge-H 自测。**新增下游 API**；schema 仍 1 |
| **0.6.0** | 阶段 2 重构遗留：核心数据类字段封装为只读属性，`CompPipeNetworkMember.Containers` / `Ports` 改为只读视图；删除 `ContainerDelta` 与 `ReevaluateSleepState()`。**API 变更**：核心数据字段从 public field 改为 read-only property；schema 仍 1 |
| **0.5.1** | 6.19 管道 A/B 双通道（方向分组、端口 channel、Gizmo 逐向配置）；6.18 流体物理量（粘度→流动阻力、比热→传热）；工程与健壮性（partial 拆分、纯核抽取、单测+CI、R-全部、4.1-4.5 修复）。**无下游 API 变更**；schema 仍 1 |
| **0.4.7** | DirtyTopo 局部脏区拓扑重建（放/拆管道不再整图重建）。**无下游 API 变更** |
| **0.4.4–0.4.6** | 批级优化、休眠重评估单遍聚合、代码审查修复、批级缓存。**无下游 API 变更**；schema 仍 1 |
| **0.4.3** | Debug：拓扑重建 Stopwatch（日志 `耗时=…ms`）+ Stress 一键场景 +「计时整图重建」。**无下游 API 变更**；当时 DirtyTopo 尚未启动 |
| **0.4.2** | DevMode Overlay 性能：弃每帧 `FlashCell`，改 `CellRenderer` + 近距 OnGUI `P=`（内部调试绘制，不影响下游 API） |
| **0.4.1** | Debug 菜单收成 12 键（7 工具 + 5 回归套件）；旧「断言* / 生成*」退出菜单；断言误报硬化。**勿依赖**旧 Debug 菜单项名称 |
| **0.4.0** | 对内冻结：阶段四收官口径；稳定面 + 交付说明合并进本文 |
| 0.3.20 | Bridge：伤害/Breakdown→破损；`TrySetBreached` |
| 0.3.19 | 化学文档收尾 |
| 0.3.12–0.3.18 | ExtHook · 化学分批落地 |
| 0.3.x 更早 | 泵/泄漏/阻力/压力/热量/散热/休眠/Overlay/存档迁移/研究本地化 |

> 旧文档 `Source/RELEASE.md` 已并入本文，只需维护这一份。

---

## 1. 怎么挂依赖

**消费模组 `About.xml`：**

```xml
<modDependencies>
  <li>
    <packageId>rimpipe.core</packageId>
    <displayName>RimPipe</displayName>
  </li>
</modDependencies>
<loadAfter>
  <li>rimpipe.core</li>
</loadAfter>
```

**C#：** 引用 `Assemblies/RimPipe.dll`，`Private=false`。

---

## 2. 核心模型（沿用这套抽象）

| 层级 | 类型 | 职责 |
|------|------|------|
| 存储 | `Container` | 一种 `FluidDef` + `amount`；挂在带 `CompPipeNetworkMember` 的建筑上 |
| 对接 | `Port` | 本地朝向 → 世界朝向 → 外面那一格；**不算**流量 |
| 传输 | `Mapping` | 容器↔容器；`maxFlowRate` = 每 20-tick 一批的上限 |

不要再新增 `Node` / `Connection` / `PipeLine` 这类命名。  
**已锁定：** 管道格**不储存流体**——只管拓扑和破损标记。  
**化学：** 多流体转化走 **`PipeReactionDef` + `CompPipeReactor`（或 `TryRegisterChemReactor`）**，不要用成对 `MappingType.Chemical` 模拟多入多出。

---

## 3. 纯 XML 扩展

### 3.1 FluidDef

```xml
<RimPipe.FluidDef>
  <defName>YourMod_Fluid_Oil</defName>
  <label>oil</label>
  <description>Custom fluid.</description>
  <leakEffects>
    <li>Filth</li>
    <li>Temperature</li>
  </leakEffects>
  <leakFilthDef>Filth_Fuel</leakFilthDef>
  <leakFilthUnitsPerFilth>10</leakFilthUnitsPerFilth>
  <leakHeatEnergyPerUnit>5</leakHeatEnergyPerUnit>
</RimPipe.FluidDef>
```

`leakEffects` 留空或写 `None` = 只扣量，不往环境泼脏污/热量。

### 3.2 储罐类（NetworkMember + 可选破损）

参考 `Defs/Things/PipeBuildings.xml` 里的 `RimPipe_StorageTank`：

- `CompProperties_PipeNetworkMember`：`containers` / `ports`（`localRot` + `containerIndex`）/ `defaultMaxFlowRate` / `insulation`
- 管道铺在端口朝向的**邻格**（外一格）
- 叠放：挂 `PlaceWorker_RimPipeAppliance`（不能叠在管道上）
- 破损（可选）：挂 `CompProperties_PipeBreachable`  
  Props 可调：`breachBelowHitPointsPercent`（默认 0.5）、`clearBreachOnRepaired`、`breachOnBreakdown`

### 3.3 管道格

- `thingClass` = `RimPipe.Building_PipeCell`
- `CompProperties_PipeCell`（破损 Props 同上）
- `PlaceWorker_RimPipeCell`

### 3.4 阀 / 泵 / 换热器

用内置 `CompProperties_PipeValve` / `PipePump` / `PipeHeatExchanger`。  
这些都需要双腔 `containers`，以及两岸端口。

### 3.5 自定义内部 Mapping（ExtHook）

自研双腔设备：实现 `IPipeInternalMappingContributor`，在拓扑重建时登记**同建筑内部**那条边。

```csharp
public class CompMyBridge : ThingComp, IPipeInternalMappingContributor
{
    public void ContributeInternalMapping(MapComponent_PipeNetwork net, HashSet<long> linkedPairs)
    {
        CompPipeNetworkMember mem = parent.GetComp<CompPipeNetworkMember>();
        Container a = mem.Containers[0];
        Container b = mem.Containers[1];
        net.AddInternalMapping(a, b, parent as Building, maxFlowRate: 10f, linkedPairs);
        // 可选：FlowDriveMode.Forced / MappingType.Heat
    }
}
```

XML：双腔 `CompProperties_PipeNetworkMember` + 你自己的 CompProps（`compClass` 指向实现了接口的 Comp）。  
重建时会扫 `parent.AllComps`；同一建筑可以有多个贡献者。

**本版不提供：** 通用的「把直接相邻外部边改成 Forced」API。泵自己的那套 Forced 贴邻逻辑仍写在 `CompPipePump` 里面。

### 3.6 化学反应

**配方 `PipeReactionDef`：**

```xml
<RimPipe.PipeReactionDef>
  <defName>YourMod_Reaction_Demo</defName>
  <label>demo reaction</label>
  <inputs>
    <li><fluid>YourMod_Fluid_A</fluid><stoichAmount>2</stoichAmount></li>
    <li><fluid>YourMod_Fluid_B</fluid><stoichAmount>1</stoichAmount></li>
  </inputs>
  <outputs>
    <li><fluid>YourMod_Fluid_C</fluid><stoichAmount>3</stoichAmount></li>
  </outputs>
  <maxRate>5</maxRate>
  <baseMixRatio>2</baseMixRatio>
  <ratioMin>1.5</ratioMin>
  <ratioMax>3</ratioMax>
  <efficiencyAtStoich>1</efficiencyAtStoich>
  <efficiencyAtRatioEdge>0.5</efficiencyAtRatioEdge>
  <minTemperature>-273</minTemperature>
  <minPressure>0</minPressure>
  <heatPerBatch>10</heatPerBatch>
  <requirePower>true</requirePower>
</RimPipe.PipeReactionDef>
```

**要点：**

- 一种流体一个 `Container`（没有「混合物管道」模型）
- `mixRatio` = 氧化剂量 / 燃料量；产出再乘效率 η
- `minTemperature` / `minPressure`：看监测入腔（默认 `inputs[0]`）够不够门槛
- `heatPerBatch`：放热写进目标腔（默认第一产出腔），`ΔT = Q/m`

**设备：** 多腔 `CompProperties_PipeNetworkMember` + `CompProperties_PipeReactor`（`reactionDefName` + 入/出腔下标）。  
可参考 Dev：`RimPipe_Dev_Reactor`。开着且有电才转化；混合比可用 Gizmo 调。

**C# 高级：** `MapComponent_PipeNetwork.TryRegisterChemReactor(...)`（运行时绑定，**不写进存档**；Comp 应在 Spawn 注册、DeSpawn 注销）。

---

## 4. 运行时访问（C#）

```csharp
MapComponent_PipeNetwork net = map.GetComponent<MapComponent_PipeNetwork>();
```

只读观察：`Members` / `Mappings` / `PipeCells` / `ChemReactors` / `SleepState` / `GetNetSleepState(netId)`。

> **核心数据封装：** `Container` / `Mapping` / `Port` / `ChemReactorBinding` 的运行时字段已封装为只读属性。下游可以读 `amount` / `capacity` / `temperature` / `pressure` / `netId` / `maxFlowRate` 等，但不能直接赋值；`CompPipeNetworkMember.Containers` / `Ports` 也以 `IReadOnlyList` 形式暴露。改量请走 `TrySetAmount` / `TryAddAmount`，改温请走 `TrySetTemperature`。

### 4.1 改量 / 改温（稳定公开面）

```csharp
net.TrySetAmount(container, 50f);       // 钳到 [0, capacity]，并唤醒所属网
net.TryAddAmount(container, -10f);      // 相对增减
net.TrySetTemperature(container, 40f);  // 改温并唤醒
```

- `Container.CommitAmount` / `CommitTemperature` 是 **`internal`**，第三方程序集**不能**直接调。
- `DebugFillContainer` / `DebugForceOneBatch` 等 Debug 入口已降为 `internal`，下游模组不要依赖；业务逻辑请用 `TrySetAmount` / `TryAddAmount` / `TrySetTemperature`。
- 突然改量会打断休眠；API 内部已经会 `WakeContainer`。

### 4.2 破损桥接（Bridge）

```csharp
net.TrySetBreached(thing, true);   // 管道格或 CompPipeBreachable
net.TryGetBreached(thing, out bool breached);
```

**自动触发（无 Harmony）：**

| 事件 | 行为 |
|------|------|
| 受伤后 HP/MaxHP &lt; `breachBelowHitPointsPercent`（默认 0.5） | → `breached=true` |
| `BroadcastCompSignal("Breakdown")`（官方 `CompBreakdownable`） | → `breached=true`（看 Props.`breachOnBreakdown`） |
| 满血且当前没有 BrokenDown | MapComp 大约每 250 tick 清一次 `breached`（看 Props.`clearBreachOnRepaired`） |

#### Bridge-A（需要引用 RimPipe）

消费模组需要**引用** `rimpipe.core`，在目标 ThingDef 的 `<comps>` 里直接挂 `CompProperties_PipeBreachable`，即可获得伤害 / Breakdown 自动破损。

#### Bridge-H（无引用注入，需要 Harmony）

如果目标建筑来自**停更 / 不引用 RimPipe** 的第三方模组，但它的 ThingDef 已经带 `CompPipeNetworkMember`，可以新增一个 `PipeBridgeInjectDef` 让 RimPipe 在加载阶段向该 ThingDef 注入 `CompPipeBreachable`：

```xml
<PipeBridgeInjectDef>
  <defName>MyMod_BridgeInject</defName>
  <targetThingDef>SomeThirdParty_StorageTank</targetThingDef>
  <injectComps>
    <li Class="RimPipe.CompProperties_PipeBreachable">
      <breachBelowHitPointsPercent>0.5</breachBelowHitPointsPercent>
      <clearBreachOnRepaired>true</clearBreachOnRepaired>
      <breachOnBreakdown>true</breachOnBreakdown>
    </li>
  </injectComps>
</PipeBridgeInjectDef>
```

规则：

- 目标 Def 必须已带 `CompPipeNetworkMember`，否则跳过并 `Log.Warning`。
- 目标 Def 不存在时也跳过并 `Log.Warning`。
- 当前只注入 `CompPipeBreachable`；不做全 NetworkMember 模板注入。
- 你的模组需要 `loadAfter` / 依赖 `rimpipe.core` 和 `brrainz.harmony`，但**不需要**引用 `RimPipe.dll` 来写这个注入 Def。

### 4.3 其它常用入口

- `RequestFullRebuild()` — 拓扑脏了要重建时
- `WakeMember` / `WakeContainer` / `NotifyBreachChanged`
- `TryRegisterChemReactor` / `UnregisterChemReactor` / `TryUpdateChemReactor`
- 构件 `PostSpawnSetup` 一般会自动 `RegisterMember`，多数情况不用手写注册

---

## 5. 能力边界（本版 0.7.0）

| 可以做 | 不要做 |
|--------|--------|
| 新 FluidDef / 新 ThingDef + 标准 Comp | 直接改流量公式，或绕过 Delta→Commit 改量 |
| TrySet/AddAmount、TrySetTemperature | 指望跨建筑 Mapping 持久化进存档 |
| TrySetBreached / TryGetBreached；`PipeBridgeInjectDef` 向第三方 Def 注入 Breachable | 注入非 Breachable 的通用 NetworkMember 模板（H2） |
| 读 Mappings / ChemReactors / 休眠态 | 通用「直接相邻外部 Forced」API |
| 挂 Breachable / 阀 / 泵 / 换热器 / **Reactor** | 真混合物 Container / 混管反应（B0） |
| `PipeReactionDef` + Reactor 或 `TryRegisterChemReactor` | 用二元 `MappingType.Chemical` 做多入多出 |
| 实现 `IPipeInternalMappingContributor` 登记内部边 | 强迫产品必须带 CompBreakdownable |

存档 schema 细则见 `docs/ARCHITECTURE.md`（SaveMig）。化学反应决议见 `docs/ARCHITECTURE.md` §6.17。破损桥接见 `docs/ARCHITECTURE.md` §7.8。

---

## 6. 最小上手清单

1. 定义 `YourMod_Fluid_*`
2. 定义一个四向储罐 `ThingDef`，`fluidDefName` 指到你的流体
3. （可选）复用或自建管道 `Building_PipeCell`
4. 模组 `loadAfter` → `rimpipe.core`
5. 游戏里：储罐端口邻格铺管，接到另一储罐；等几批处理后应能流动
6. （化学）定义 `PipeReactionDef`，多腔建筑挂 `CompPipeReactor`
7. （破损）挂 `CompProperties_PipeBreachable`，或运行时调用 `TrySetBreached`
