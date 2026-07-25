（本文档已弃用，仅做参考）
# RimPipe - 低性能消耗高仿真管道系统

## 一、设计理念

### 核心目标
- **低性能消耗**：消除管道网络的持续 tick 计算，改为事件驱动
- **高仿真度**：支持复杂的流体物理特性（压力、温度、粘度等）和化学/热交换反应

### 核心抽象
将管道系统抽象为两种基本元件：

| 元件类型 | 描述 | 职责 |
|---------|------|------|
| **线（PipeLine）** | 1对1的管道，由多个管道格子建筑组成 | 仅作为物理连接，不参与流体计算 |
| **节点（Node）** | 三通、四通、阀门、热交换器、化学反应釜等 | 承担所有智能逻辑：流量分配、压力计算、反应处理 |

---

## 二、架构设计

### 2.1 全局管理器 - MapComp_PipeNetwork

**职责**：
- 维护所有管道（PipeLine）的全局注册表
- 管理管道的创建、销毁、合并、分裂事件
- 提供管道查询接口（通过位置/建筑获取所属管道）

**数据结构**：
```
PipeNetwork
├── connections: Dictionary<ConnectionId, Connection>  // 所有连接实例（外部+内部）
├── cellToConnection: Dictionary<IntVec3, ConnectionId>  // 格子到外部连接的映射
└── nodeRegistry: Dictionary<Buildable, Node>           // 节点注册表
```

### 2.2 连接 - Connection

**定义**：两个容器之间的交互关系。连接分为两个维度：
- **空间维度**：外部连接（跨节点）/ 内部连接（同节点内）
- **交互维度**：流动连接（流体交换）/ 热连接（热量传递）/ 化学连接（化学反应）

**核心模型**：容器 ←→ 连接(1对1) ←→ 容器

**数据结构**：
```
Connection
├── id: Guid                  // 唯一标识（映射关系ID）
├── scope: ConnectionScope    // 空间维度：External（跨节点）/ Internal（同节点内）
├── interaction: InteractionType  // 交互维度：Flow（流体流动）/ Heat（热传递）/ Chemical（化学反应）
├── startContainerId: Guid    // 起始容器ID
├── endContainerId: Guid      // 终止容器ID
├── startNode: Node           // 起始容器所属节点
├── endNode: Node             // 终止容器所属节点
├── startPortId: string?      // 起始端口ID（外部连接时存在）
├── endPortId: string?        // 终止端口ID（外部连接时存在）
├── cells: List<IntVec3>?     // 组成管道的格子（外部流动连接时存在）
├── diameter: float?          // 管径/等效截面积（流动连接时存在）
├── maxPressure: float?       // 最大承受压力（流动连接时存在）
├── insulation: float         // 保温系数（影响热传递）
├── length: float             // 长度（外部连接：格子数×格子尺寸；内部连接：固定值）
├── resistance: float?        // 阻力（流动连接时存在）
├── heatTransferCoeff: float? // 热传递系数（热连接时存在）
├── surfaceArea: float?       // 换热面积（热连接时存在）
├── reaction: ChemicalReaction? // 化学反应定义（化学连接时存在）
└── conditions: List<Condition>? // 触发条件
```

**特性**：
- 连接本身不参与任何计算，只定义交互关系和参数
- 连接的存在只是为了建立**容器之间的映射关系**
- **外部连接**的成型与破坏是**事件驱动**的，无需 tick
- **内部连接**在节点创建时自动建立，不会随管道破坏而消失
- 同一对容器之间可以存在多种连接类型（如同时存在流动和热传递）

**交互类型详解**：

| 交互类型 | 描述 | 核心参数 | 典型场景 |
|---------|------|---------|---------|
| **Flow** | 流体从一个容器流向另一个容器 | diameter, resistance, maxPressure | 管道、泵的吸入排出 |
| **Heat** | 热量从一个容器传递到另一个容器 | heatTransferCoeff, surfaceArea, insulation | 热交换器、管道散热 |
| **Chemical** | 化学反应将一个容器的流体转化为另一个容器的流体 | reaction, conditions | 化学反应釜 |

**空间维度对比**：

| 属性 | 外部连接（跨节点） | 内部连接（同节点内） |
|------|-------------------|---------------------|
| 连接对象 | 不同节点的容器 | 同一节点的容器 |
| 物理格子 | 流动连接需要 | 不需要 |
| 创建方式 | 玩家放置管道格子 | 节点初始化时自动创建 |
| 破坏方式 | 玩家拆除管道格子 | 节点销毁时自动销毁 |
| 典型类型 | Flow（物理管道） | Heat（热交换）、Chemical（化学反应） |

**典型连接配置示例**：

| 场景 | scope | interaction | 说明 |
|------|-------|------------|------|
| 物理管道连接两个储罐 | External | Flow | 储罐A的tank容器 → 管道 → 储罐B的tank容器 |
| 泵的吸入腔到排出腔 | Internal | Flow | suction容器 → 压缩连接 → discharge容器 |
| 热交换器的热侧到冷侧 | Internal | Heat | hot_side容器 → 热传递连接 → cold_side容器 |
| 化学反应釜的反应物到产物 | Internal | Chemical | reactants容器 → 反应连接 → products容器 |
| 管道的散热 | External | Heat | 容器 → 管道 → 环境（虚拟环境容器） |

### 2.3 节点 - Node

**定义**：所有非管道的流体相关建筑

**核心设计**：节点内部由**容器（Container）**、**端口（Port）**和**转化规则（Transform）**三层组成：
- **容器**：存储流体的内部空间，可独立维护压力、温度、成分
- **端口**：连接管道的接口，映射到特定容器
- **转化规则**：容器之间的流体转化关系（如化学反应、热交换）

**分类**：

| 节点类型 | 输入口数量 | 输出口数量 | 容器数量 | 核心功能 |
|---------|-----------|-----------|---------|---------|
| **源头节点** | 0 | N | 1+ | 产生流体（水泵、储罐出口等） |
| **终端节点** | N | 0 | 1+ | 消耗流体（用水设备、储罐入口等） |
| **分流节点** | 1 | N | 1 | 三通、四通等，仅分配流量 |
| **合流节点** | N | 1 | 1 | 合并多股流体 |
| **处理节点** | N | M | 2+ | 阀门、热交换器、化学反应釜等 |

**数据结构**：
```
Node
├── buildable: Buildable          // 对应的建筑实例
├── containers: List<Container>   // 内部容器列表
├── ports: List<Port>             // 所有接口
├── transforms: List<Transform>   // 容器间的转化规则
└── tickRate: int                 // 计算频率（每多少tick计算一次）
```

**容器 - Container**：
```
Container
├── id: string                    // 容器标识（如 "input", "output", "reactor"）
├── contents: Dictionary<Gas, float>  // 内部流体组成（流体类型 → 量）
├── pressure: float               // 当前压力
├── temperature: float            // 当前温度
├── capacity: float               // 最大容量（体积）
├── maxPressure: float            // 最大承受压力
├── isSealed: bool                // 是否密封（密封容器压力可积累）
└── insulation: float             // 保温系数（0=完全导热，1=完全绝热）
```

**接口 - Port**：
```
Port
├── id: string                    // 端口标识（如 "inlet1", "outlet2"）
├── direction: Rot4               // 接口方向
├── connectedPipe: PipeLine       // 连接的管道（可为null）
├── connectedContainer: Container // 映射到的内部容器
├── maxFlowRate: float            // 最大流量
├── currentFlowRate: float        // 当前流量
├── pressureDrop: float           // 压力降系数
├── isInput: bool                 // 是否为输入端口
└── filter: List<Gas>             // 允许通过的流体类型（null=全部允许）
```

**转化规则 - Transform**：
```
Transform
├── sourceContainers: List<Container>   // 源容器（消耗流体）
├── targetContainers: List<Container>   // 目标容器（产出流体）
├── reaction: ChemicalReaction          // 化学反应定义（可为null）
├── heatTransfer: HeatTransfer          // 热传递定义（可为null）
├── efficiency: float                   // 转化效率 (0~1)
├── maxRate: float                      // 最大转化速率
├── requiredPower: float                // 所需功率
└── isActive: bool                      // 是否激活
```

**典型节点内部结构示例**：

| 节点类型 | 容器配置 | 端口配置 | 转化规则 |
|---------|---------|---------|---------|
| **简单阀门** | 1个混合容器 | 1输入口+1输出口，均映射到同一容器 | 无（仅压力调节） |
| **三通** | 1个混合容器 | 1输入口+2输出口，均映射到同一容器 | 无（仅流量分配） |
| **热交换器** | 2个独立容器（热侧、冷侧） | 2输入口+2输出口，各映射到对应容器 | 热传递规则 |
| **化学反应釜** | 3个容器（反应物、产物、催化剂） | 2输入口（反应物）+1输出口（产物） | 化学反应规则 |
| **储罐** | 1个主容器 | N个输入口+N个输出口，均映射到主容器 | 无（仅存储） |

---

## 二、元件详细设计

### 2.4 管道格子 - PipeCell

**定义**：最基础的管道建筑，多个 PipeCell 组成一段 PipeLine

**特性**：
- 无任何流体计算逻辑
- 仅作为物理连接的载体
- 可连接上下左右四个方向的相邻格子

**数据结构**：
```
PipeCell
├── pipeLineId: Guid?          // 所属管道线ID（可为null，表示未连接）
├── diameter: float           // 管径（所有格子必须一致才能组成管道）
├── maxPressure: float        // 最大承受压力
└── insulation: float         // 保温系数
```

**事件处理**：
- `OnSpawn()`：通知 MapComp 尝试合并到相邻管道
- `OnDeSpawn()`：通知 MapComp 从管道中移除，可能导致管道分裂

---

### 2.5 阀门 - Valve

**定义**：控制管道通断和流量的基本元件

**容器配置**：
```
Container: "chamber"
├── capacity: 10L
├── maxPressure: 10MPa
├── isSealed: false
└── insulation: 0.5
```

**端口配置**：
| 端口ID | 方向 | 类型 | 映射容器 | 最大流量 | 压力降 |
|-------|------|------|---------|---------|--------|
| inlet | 任意 | 输入 | chamber | 100L/s | 0.1MPa |
| outlet | 对侧 | 输出 | chamber | 100L/s | 0.1MPa |

**特有属性**：
```
Valve
├── isOpen: bool              // 是否开启
├── openingRatio: float       // 开度 (0~1)，0=关闭，1=全开
├── minPressureToOpen: float  // 开启所需最小压力
└── flowPriority: int         // 流量优先级（用于多阀门场景）
```

**工作逻辑**：
1. 输入端口接收流体进入 chamber
2. 根据 openingRatio 和压力决定流量
3. 输出端口排出流体
4. 阀门关闭时，chamber 内流体被隔离

---

### 2.6 三通 - TeeJunction

**定义**：一分二或二合一的分流/合流元件

**容器配置**：
```
Container: "mixing_chamber"
├── capacity: 20L
├── maxPressure: 5MPa
├── isSealed: false
└── insulation: 0.3
```

**端口配置**：
| 端口ID | 方向 | 类型 | 映射容器 | 最大流量 |
|-------|------|------|---------|---------|
| main | 前 | 输入/输出 | mixing_chamber | 200L/s |
| branch1 | 左 | 输入/输出 | mixing_chamber | 100L/s |
| branch2 | 右 | 输入/输出 | mixing_chamber | 100L/s |

**特有属性**：
```
TeeJunction
├── mode: TeeMode             // 模式：Split（分流）/ Merge（合流）/ Auto（自动）
├── splitRatio: float         // 分流比例 branch1:branch2
└── mergePriority: int[]      // 合流时各输入端口的优先级
```

**工作逻辑**：
- **分流模式**：main 输入，按比例分配到 branch1 和 branch2
- **合流模式**：branch1 和 branch2 输入，合并后从 main 输出
- **自动模式**：根据压力方向自动决定流向

---

### 2.7 储罐 - StorageTank

**定义**：存储流体的容器节点

**容器配置**：
```
Container: "tank"
├── capacity: 1000L           // 大型储罐可配置更大容量
├── maxPressure: 2MPa
├── isSealed: true            // 密封容器，压力可积累
└── insulation: 0.8           // 良好保温
```

**端口配置**：
| 端口ID | 方向 | 类型 | 映射容器 | 最大流量 |
|-------|------|------|---------|---------|
| inlet1 | 前 | 输入 | tank | 50L/s |
| inlet2 | 左 | 输入 | tank | 50L/s |
| outlet1 | 后 | 输出 | tank | 50L/s |
| outlet2 | 右 | 输出 | tank | 50L/s |

**特有属性**：
```
StorageTank
├── fillLevel: float          // 当前填充量 (0~capacity)
├── fillPercent: float        // 填充百分比 (0~1)
├── maxOutputPressure: float  // 最大输出压力（由液位决定）
└── allowOverflow: bool       // 是否允许溢出
```

**工作逻辑**：
1. 输入端口向 tank 注入流体，增加 fillLevel 和压力
2. 输出端口根据压力和优先级排出流体
3. 压力由 fillPercent 和流体密度共同决定：P = ρgh
4. 密封特性：即使没有输入，内部压力也会保持

---

### 2.8 泵 - Pump

**定义**：主动产生压力、推动流体流动的源头节点

**容器配置**：
```
Container: "suction"          // 吸入腔
├── capacity: 5L
├── maxPressure: 0.5MPa
├── isSealed: false
└── insulation: 0.2

Container: "discharge"        // 排出腔
├── capacity: 5L
├── maxPressure: 15MPa
├── isSealed: true
└── insulation: 0.2
```

**端口配置**：
| 端口ID | 方向 | 类型 | 映射容器 | 最大流量 |
|-------|------|------|---------|---------|
| inlet | 前 | 输入 | suction | 200L/s |
| outlet | 后 | 输出 | discharge | 200L/s |

**特有属性**：
```
Pump
├── isRunning: bool           // 是否运行
├── powerConsumption: float   // 功率消耗
├── maxHead: float            // 最大扬程（产生的最大压力）
├── flowRate: float           // 当前流量
├── efficiency: float         // 效率
└── requiredPower: float      // 所需最小功率
```

**工作逻辑**：
1. 从 inlet 吸入流体到 suction 腔
2. 通过机械压缩将流体送入 discharge 腔
3. discharge 腔产生高压，推动流体从 outlet 流出
4. 功率不足时，效率下降，流量减少

**转化规则**：
```
Transform: pumping
├── sources: [suction]
├── targets: [discharge]
├── maxRate: 200L/s
├── requiredPower: 500W
├── isActive: depends on isRunning and power
```

---

### 2.9 热交换器 - HeatExchanger

**定义**：实现两股流体之间热传递的处理节点

**容器配置**：
```
Container: "hot_side"         // 热侧流体
├── capacity: 30L
├── maxPressure: 5MPa
├── isSealed: false
└── insulation: 0.1

Container: "cold_side"        // 冷侧流体
├── capacity: 30L
├── maxPressure: 5MPa
├── isSealed: false
└── insulation: 0.1
```

**端口配置**：
| 端口ID | 方向 | 类型 | 映射容器 | 最大流量 |
|-------|------|------|---------|---------|
| hot_in | 前上 | 输入 | hot_side | 100L/s |
| hot_out | 后上 | 输出 | hot_side | 100L/s |
| cold_in | 前下 | 输入 | cold_side | 100L/s |
| cold_out | 后下 | 输出 | cold_side | 100L/s |

**特有属性**：
```
HeatExchanger
├── isActive: bool            // 是否运行
├── heatTransferCoeff: float  // 热传递系数
├── surfaceArea: float        // 换热面积
├── minTempDiff: float        // 最小温差（低于此值不换热）
└── maxHeatTransfer: float    // 最大换热量
```

**工作逻辑**：
1. 热侧流体从 hot_in 进入，从 hot_out 流出
2. 冷侧流体从 cold_in 进入，从 cold_out 流出
3. 通过热传递规则，热量从高温侧传递到低温侧
4. 热侧温度降低，冷侧温度升高

**转化规则**：
```
Transform: heat_transfer
├── sources: [hot_side]
├── targets: [cold_side]
├── heatTransfer: Q = k * A * ΔT
├── maxRate: 100kW
├── isActive: true
```

---

### 2.10 化学反应釜 - ChemicalReactor

**定义**：实现化学反应的处理节点

**容器配置**：
```
Container: "reactants"        // 反应物腔
├── capacity: 50L
├── maxPressure: 20MPa
├── isSealed: true
└── insulation: 0.9

Container: "products"         // 产物腔
├── capacity: 50L
├── maxPressure: 10MPa
├── isSealed: true
└── insulation: 0.9
```

**端口配置**：
| 端口ID | 方向 | 类型 | 映射容器 | 最大流量 |
|-------|------|------|---------|---------|
| reactant1_in | 前 | 输入 | reactants | 50L/s |
| reactant2_in | 左 | 输入 | reactants | 50L/s |
| product_out | 后 | 输出 | products | 100L/s |

**特有属性**：
```
ChemicalReactor
├── isActive: bool            // 是否运行
├── reaction: ChemicalReaction // 当前反应定义
├── requiredTemperature: float // 所需温度
├── requiredPressure: float   // 所需压力
├── catalystAmount: float     // 催化剂含量
├── reactionRate: float       // 当前反应速率
└── efficiency: float         // 反应效率
```

**工作逻辑**：
1. 反应物通过 reactant1_in 和 reactant2_in 进入 reactants 腔
2. 当温度和压力达到阈值时，化学反应开始
3. 反应物消耗，产物生成并进入 products 腔
4. 产物通过 product_out 排出

**转化规则**：
```
Transform: reaction
├── sources: [reactants]
├── targets: [products]
├── reaction: 3H₂ + N₂ → 2NH₃ (放热 -46kJ/mol)
├── conditions: [temp>450°C, pressure>200atm]
├── maxRate: 50L/s
├── requiredPower: 2000W
├── isActive: depends on conditions
```

---

## 三、核心机制

### 3.1 连接管理（事件驱动）

**核心模型**：连接的本质是**两个容器之间的映射关系**

#### 3.1.1 外部连接创建流程（物理管道）
1. 玩家放置管道格子建筑（PipeCell）
2. PipeCell 触发 `OnSpawn()` 事件，通知 MapComp
3. MapComp 检测相邻格子：
   - 相邻是管道格子：合并到已有 Connection
   - 相邻是节点建筑：记录潜在连接点
4. MapComp 检查连接两端是否接触到节点的端口
5. 如果两端都连接到节点端口，建立容器映射关系：
   - 设置 `startContainerId` 和 `endContainerId`
   - 设置 `startPortId` 和 `endPortId`
6. 更新 `cellToConnection` 映射

#### 3.1.2 外部连接破坏流程
1. 玩家拆除管道格子建筑
2. PipeCell 触发 `OnDeSpawn()` 事件，通知 MapComp
3. MapComp 从对应 Connection 的 cells 中移除该格子
4. 如果连接被拆分成两段：
   - 创建两个新的 Connection
   - 分别检查新连接两端是否连接到节点端口
   - 更新容器映射关系
5. 更新 `cellToConnection` 映射

#### 3.1.3 内部连接创建流程（虚拟管道）
1. 节点建筑被放置（触发 `OnSpawn()`）
2. 节点初始化其内部容器
3. 根据节点定义，自动创建内部连接：
   - 遍历节点的 transforms（转化规则）
   - 为每个 transform 创建对应的内部连接
   - 设置 `type = Internal`
   - 设置 `startContainerId` 和 `endContainerId`（均属于同一节点）
   - 设置 `cells = null`（不需要物理格子）
4. 将内部连接注册到 MapComp

#### 3.1.4 内部连接破坏流程
1. 节点建筑被拆除（触发 `OnDeSpawn()`）
2. 节点通知 MapComp 销毁所有内部连接
3. MapComp 从 `connections` 字典中移除这些连接

#### 3.1.5 外部连接合并场景
- 两段独立的外部连接通过新放置的管道格子连接
- MapComp 检测到相邻属于不同 Connection 的格子
- 合并两个 Connection 为一个新的 Connection
- 检查合并后的连接两端是否连接到节点端口
- 更新容器映射关系

#### 3.1.6 外部连接分裂场景
- 一段外部连接中间的格子被拆除
- 原来的 Connection 分裂为两段或多段
- 每段成为独立的 Connection
- 分别检查各段两端是否连接到节点端口
- 更新容器映射关系

#### 3.1.7 容器连接检测
- 当管道格子放置在节点建筑相邻格子时，触发连接检测
- 检测节点在该方向是否有端口
- 如果有端口，将连接连接到该端口对应的容器

### 3.2 容器交互模型（统一连接处理）

**核心思想**：所有容器间的交互（流动、热传递、化学反应）都通过 Connection 统一处理，直接刷新容器状态，无需中间层

**计算流程**：
1. 每个节点按自身的 `tickRate` 进行计算
2. 节点遍历所有容器
3. 对于每个容器，查找所有连接到该容器的 Connection
4. 根据连接的 `interaction` 类型执行相应计算，直接更新两端容器状态：
   - **Flow**：流体从高压容器流向低压容器
   - **Heat**：热量从高温容器传递到低温容器
   - **Chemical**：反应物转化为产物
5. 容器状态在每个 tick 结束时同步更新

#### 3.2.1 流体流动（Flow 连接）

**基本假设**：
- 流体流动遵循压力梯度：从高压容器流向低压容器
- 连接存在阻力，导致压力损失
- 流量与压差成正比，与阻力成反比
- 直接通过连接计算并刷新容器状态，无需中间流体包

**核心公式**：
```
流量 Q = (P_start - P_end) / R
其中：
- P_start: 起始容器压力
- P_end: 终止容器压力
- R: 连接阻力（由管径、长度、流体粘度、粗糙度决定）

连接阻力 R = (8 * μ * L) / (π * r^4)
其中：
- μ: 流体粘度
- L: 连接长度（外部连接：格子数×格子尺寸；内部连接：固定设计值）
- r: 连接半径/等效半径
```

**流动计算流程**：
1. 获取起始容器（A）和终止容器（B）的状态
2. 计算压力差 ΔP = P_A - P_B
3. 如果 ΔP > 0，计算流量 Q = ΔP / R
4. 限制流量不超过容器 A 的可用量和容器 B 的剩余容量
5. 更新容器状态：
   - 容器 A：减少流体量，降低压力
   - 容器 B：增加流体量，升高压力
   - 流体成分按比例转移
   - 温度按混合规则更新

**连续性保证**：
- 每个 tick 计算一次流量，通过时间步长实现连续流动
- 流量 = 流速 × 时间步长
- 容器状态在每个 tick 结束时同步更新

#### 3.2.2 热传递（Heat 连接）

**基本假设**：
- 热量从高温容器传递到低温容器
- 热传递速率与温差成正比
- 连接的保温系数影响热传递效率

**核心公式**：
```
热传递速率 Q = U * A * ΔT * (1 - insulation)
其中：
- U: 总传热系数
- A: 换热面积
- ΔT: 两容器温差
- insulation: 保温系数 (0~1)
```

**热传递流程**：
1. 计算两容器的温差 ΔT = |T1 - T2|
2. 根据连接参数计算热传递速率
3. 更新两容器温度：
   - 高温容器降温：T1 = T1 - Q / (m1 * c1)
   - 低温容器升温：T2 = T2 + Q / (m2 * c2)

#### 3.2.3 化学反应（Chemical 连接）

**基本假设**：
- 化学反应将反应物转化为产物
- 反应需要满足特定条件（温度、压力）
- 反应速率受反应物浓度、温度、催化剂影响

**核心公式**：
```
反应速率 r = k * [A]^a * [B]^b * exp(-E/(R*T))
其中：
- k: 反应速率常数
- [A], [B]: 反应物浓度
- a, b: 反应级数
- E: 活化能
- R: 气体常数
- T: 温度
```

**化学反应流程**：
1. 检查是否满足触发条件（温度、压力）
2. 计算反应速率
3. 消耗反应物容器中的流体
4. 在产物容器中生成产物流体
5. 更新反应热（影响容器温度）

### 3.3 压力计算

**压力来源**：
- 源头节点产生压力（如水泵的排出腔）
- 流体高度差产生压力（储罐液位）
- 化学反应产生压力
- 外部气源输入

**压力计算公式**：
```
密封容器压力 P = nRT/V （理想气体定律）
或 P = ρgh （液体静压）

储罐压力 P = fillPercent * maxPressure
```

**压力损失**：
- 管道阻力导致的压力损失：ΔP = Q * R
- 阀门节流产生的压力损失：ΔP = k * Q^2
- 管道弯曲产生的压力损失

**压力传播**：
- 压力变化由容器计算并通过管道传递
- 管道本身不存储压力状态
- 连接的另一端容器在下一次计算时感知压力变化
- 压力传递有延迟（与管道长度成正比）

### 3.4 热交换模型

**热传递方式**：
- 管道与环境的热交换（根据保温系数）
- 容器之间的热交换（通过管道传递）
- 化学反应产生或吸收热量
- 节点与环境的热交换

**热传递公式**：
```
管道热损失 Q_loss = k * A * ΔT * (1 - insulation)
其中：
- k: 导热系数
- A: 管道表面积
- ΔT: 管道与环境温差
- insulation: 保温系数 (0~1)

容器间热传递 Q = U * A * ΔT
其中：
- U: 总传热系数
- A: 换热面积
- ΔT: 两容器温差
```

**温度计算**：
- 每个容器独立维护温度
- 管道传递热量时考虑保温系数
- 热交换器节点通过转化规则主动调节容器温度
- 流体流动时携带热量：Q = m * c * ΔT

---

## 四、性能优化策略

### 4.1 事件驱动架构
- 管道拓扑变化仅在建筑放置/拆除时触发
- 无需每 tick 扫描整个管道网络
- 节点计算频率可配置，非关键节点可降低频率

### 4.2 懒惰计算
- 仅当流体状态发生变化时才进行计算
- 稳定状态下节点进入休眠
- 节点间的数据传递采用延迟更新

### 4.3 层级优化
- 大规模网络时，将相似节点合并为虚拟节点
- 非关键流体（如气体）采用更低精度计算

### 4.4 空间分区
- 使用四叉树/八叉树管理管道空间分布
- 查询时只检查邻近区域
- 减少全局遍历

---

## 五、代码实现结构

### 5.1 模组目录结构

```
RimPipe/
├── About/
│   ├── About.xml
│   └── Preview.png
├── Assemblies/
│   └── RimPipe.dll
├── Defs/
│   ├── PipeSystem/
│   │   ├── FluidDefs/
│   │   │   ├── Water.xml
│   │   │   ├── Hydrogen.xml
│   │   │   └── ...
│   │   ├── NodeDefs/
│   │   │   ├── Valve.xml
│   │   │   ├── Pump.xml
│   │   │   └── ...
│   │   └── ReactionDefs/
│   │       ├── HaberProcess.xml
│   │       └── ...
│   ├── Buildings/
│   │   ├── PipeCell.xml
│   │   ├── Valve.xml
│   │   ├── Pump.xml
│   │   └── ...
│   └── ResearchProjectDefs/
│       └── PipeSystemResearch.xml
├── Languages/
│   └── ChineseSimplified/
│       └── Keyed/
│           └── RimPipe_Language.xml
├── Patches/
│   ├── Harmony/
│   │   ├── BuildingSpawnPatch.cs
│   │   └── BuildingDeSpawnPatch.cs
│   └── XML/
│       └── ...
└── Source/
    ├── RimPipe.csproj
    └── RimPipe/
        ├── Core/
        │   ├── MapComp_PipeNetwork.cs
        │   ├── Connection.cs
        │   ├── Container.cs
        │   ├── Node.cs
        │   └── Port.cs
        ├── Defs/
        │   ├── FluidDef.cs
        │   ├── NodeDef.cs
        │   ├── ContainerDef.cs
        │   ├── PortDef.cs
        │   ├── ConnectionDef.cs
        │   └── ReactionDef.cs
        ├── Buildings/
        │   ├── Building_PipeCell.cs
        │   ├── Building_Valve.cs
        │   ├── Building_Pump.cs
        │   ├── Building_HeatExchanger.cs
        │   └── Building_ChemicalReactor.cs
        ├── Systems/
        │   ├── FlowSystem.cs
        │   ├── HeatSystem.cs
        │   └── ChemicalSystem.cs
        ├── Utilities/
        │   ├── PressureCalculator.cs
        │   ├── ResistanceCalculator.cs
        │   └── HeatTransferCalculator.cs
        └── Patches/
            ├── BuildingSpawnPatch.cs
            └── BuildingDeSpawnPatch.cs
```

### 5.2 核心类设计

#### 5.2.1 MapComp_PipeNetwork

```csharp
public class MapComp_PipeNetwork : MapComponent
{
    public Dictionary<Guid, Connection> connections;
    public Dictionary<IntVec3, Guid> cellToConnection;
    public Dictionary<Buildable, Node> nodeRegistry;
    
    public void RegisterNode(Node node);
    public void UnregisterNode(Node node);
    public void CreateExternalConnection(List<IntVec3> cells, Container start, Container end);
    public void DestroyExternalConnection(Guid connectionId);
    public void MergeConnections(Guid id1, Guid id2);
    public void SplitConnection(Guid id, IntVec3 splitCell);
    public Connection GetConnectionAt(IntVec3 cell);
    public List<Connection> GetConnectionsForContainer(Guid containerId);
}
```

#### 5.2.2 Connection

```csharp
public enum ConnectionScope { External, Internal }
public enum InteractionType { Flow, Heat, Chemical }

public class Connection
{
    public Guid id;
    public ConnectionScope scope;
    public InteractionType interaction;
    public Guid startContainerId;
    public Guid endContainerId;
    public Node startNode;
    public Node endNode;
    public string startPortId;
    public string endPortId;
    public List<IntVec3> cells;
    public float diameter;
    public float maxPressure;
    public float insulation;
    public float length;
    public float resistance;
    public float heatTransferCoeff;
    public float surfaceArea;
    public ChemicalReaction reaction;
    public List<Condition> conditions;
    
    public void ProcessTick();
}
```

#### 5.2.3 Container

```csharp
public class Container
{
    public Guid id;
    public string name;
    public Node owner;
    public Dictionary<FluidDef, float> contents;
    public float pressure;
    public float temperature;
    public float capacity;
    public float maxPressure;
    public bool isSealed;
    public float insulation;
    
    public float TotalVolume { get; }
    public float FillPercent { get; }
    
    public void AddFluid(FluidDef fluid, float amount);
    public void RemoveFluid(FluidDef fluid, float amount);
    public void TransferTo(Container target, float amount);
    public void UpdatePressure();
    public void UpdateTemperature(float deltaHeat);
}
```

#### 5.2.4 Node

```csharp
public class Node
{
    public Buildable buildable;
    public List<Container> containers;
    public List<Port> ports;
    public List<Connection> internalConnections;
    public int tickRate;
    public int tickCounter;
    
    public Node(Buildable buildable);
    
    public void Initialize();
    public void ProcessTick();
    public Container GetContainer(Guid id);
    public Container GetContainer(string name);
    public Port GetPort(Rot4 direction);
    public void ConnectToPort(Port port, Container container);
}
```

#### 5.2.5 Port

```csharp
public class Port
{
    public string id;
    public Rot4 direction;
    public Container connectedContainer;
    public Connection connectedConnection;
    public float maxFlowRate;
    public float currentFlowRate;
    public float pressureDrop;
    public bool isInput;
    public List<FluidDef> filter;
    
    public bool CanAcceptFluid(FluidDef fluid);
}
```

### 5.3 系统类设计

#### 5.3.1 FlowSystem

```csharp
public static class FlowSystem
{
    public static void ProcessFlow(Connection connection);
    public static float CalculateFlowRate(Container start, Container end, float resistance);
    public static float CalculateResistance(float diameter, float length, float viscosity);
    public static void TransferFluid(Container from, Container to, float amount);
}
```

#### 5.3.2 HeatSystem

```csharp
public static class HeatSystem
{
    public static void ProcessHeatTransfer(Connection connection);
    public static float CalculateHeatTransferRate(Container hot, Container cold, 
                                                  float heatTransferCoeff, float surfaceArea, float insulation);
    public static void TransferHeat(Container from, Container to, float amount);
}
```

#### 5.3.3 ChemicalSystem

```csharp
public static class ChemicalSystem
{
    public static void ProcessReaction(Connection connection);
    public static bool CheckConditions(Container container, List<Condition> conditions);
    public static float CalculateReactionRate(Container reactants, ChemicalReaction reaction);
    public static void PerformReaction(Container reactants, Container products, ChemicalReaction reaction, float rate);
}
```

### 5.4 建筑类设计

#### 5.4.1 Building_PipeCell

```csharp
public class Building_PipeCell : Building
{
    public Guid pipeLineId;
    public float diameter;
    public float maxPressure;
    public float insulation;
    
    public override void SpawnSetup(Map map, bool respawningAfterLoad);
    public override void DeSpawn(DestroyMode mode);
}
```

#### 5.4.2 Building_Valve

```csharp
public class Building_Valve : Building, IThingHolder
{
    public Node node;
    public bool isOpen;
    public float openingRatio;
    
    public override void SpawnSetup(Map map, bool respawningAfterLoad);
    public override void DeSpawn(DestroyMode mode);
    public override void Tick();
}
```

#### 5.4.3 Building_Pump

```csharp
public class Building_Pump : Building, IThingHolder
{
    public Node node;
    public CompPowerTrader powerComp;
    public bool isRunning;
    
    public override void SpawnSetup(Map map, bool respawningAfterLoad);
    public override void DeSpawn(DestroyMode mode);
    public override void Tick();
}
```

### 5.5 Def 定义设计

#### 5.5.1 FluidDef

```csharp
public class FluidDef : Def
{
    public float viscosity;
    public float density;
    public float heatCapacity;
    public float boilingPoint;
    public float freezingPoint;
    public bool isGas;
}
```

#### 5.5.2 NodeDef

```csharp
public class NodeDef : Def
{
    public List<ContainerDef> containers;
    public List<PortDef> ports;
    public List<ConnectionDef> internalConnections;
    public int tickRate;
}
```

#### 5.5.3 ContainerDef

```csharp
public class ContainerDef : Def
{
    public string name;
    public float capacity;
    public float maxPressure;
    public bool isSealed;
    public float insulation;
}
```

#### 5.5.4 PortDef

```csharp
public class PortDef : Def
{
    public string name;
    public Rot4 direction;
    public string connectedContainer;
    public float maxFlowRate;
    public float pressureDrop;
    public bool isInput;
    public List<FluidDef> filter;
}
```

#### 5.5.5 ConnectionDef

```csharp
public class ConnectionDef : Def
{
    public InteractionType interaction;
    public string startContainer;
    public string endContainer;
    public float diameter;
    public float resistance;
    public float heatTransferCoeff;
    public float surfaceArea;
    public ChemicalReactionDef reaction;
    public List<ConditionDef> conditions;
}
```

### 5.6 Harmony Patch 设计

#### 5.6.1 BuildingSpawnPatch

```csharp
[HarmonyPatch(typeof(Building), nameof(Building.SpawnSetup))]
public static class BuildingSpawnPatch
{
    public static void Postfix(Building __instance, Map map, bool respawningAfterLoad)
    {
        if (__instance is Building_PipeCell pipeCell)
        {
            // 通知 MapComp 创建/合并管道
        }
        else if (__instance is IHasNode nodeBuilding)
        {
            // 创建 Node 并注册到 MapComp
        }
    }
}
```

#### 5.6.2 BuildingDeSpawnPatch

```csharp
[HarmonyPatch(typeof(Building), nameof(Building.DeSpawn))]
public static class BuildingDeSpawnPatch
{
    public static void Prefix(Building __instance, DestroyMode mode)
    {
        if (__instance is Building_PipeCell pipeCell)
        {
            // 通知 MapComp 拆除管道
        }
        else if (__instance is IHasNode nodeBuilding)
        {
            // 销毁 Node 并从 MapComp 移除
        }
    }
}
```

### 5.7 数据流图

```
建筑放置/拆除
    │
    ▼
Harmony Patch (SpawnSetup/DeSpawn)
    │
    ├── Building_PipeCell → MapComp_PipeNetwork
    │       ├── CreateExternalConnection
    │       ├── DestroyExternalConnection
    │       ├── MergeConnections
    │       └── SplitConnection
    │
    └── IHasNode → Node.Initialize()
            ├── 创建 Containers
            ├── 创建 Ports
            └── 创建 InternalConnections → MapComp_PipeNetwork.RegisterNode()

游戏 Tick
    │
    ▼
Node.ProcessTick()
    │
    └── 遍历所有 Connection
            ├── Flow → FlowSystem.ProcessFlow() → 更新两端 Container
            ├── Heat → HeatSystem.ProcessHeatTransfer() → 更新两端 Container
            └── Chemical → ChemicalSystem.ProcessReaction() → 更新两端 Container
```

---

## 六、技术实现路线

### 阶段一：基础框架
- [ ] 实现 Def 定义系统（FluidDef, NodeDef, ContainerDef, PortDef, ConnectionDef）
- [ ] 实现 MapComp_PipeNetwork 全局管理器
- [ ] 实现 Connection 数据结构（统一外部/内部连接）
- [ ] 实现 Container 数据结构（存储流体、压力、温度）
- [ ] 实现管道格子建筑（Building_PipeCell）及事件驱动逻辑
- [ ] 实现 Harmony Patch（建筑放置/拆除事件捕获）

### 阶段二：核心节点系统
- [ ] 实现 Node 数据结构（管理容器、端口、内部连接）
- [ ] 实现 Port 数据结构（映射到容器）
- [ ] 实现 FlowSystem（流体流动计算）
- [ ] 实现 PressureCalculator（压力计算）
- [ ] 实现 ResistanceCalculator（阻力计算）
- [ ] 实现基础阀门节点（Building_Valve）
- [ ] 实现三通节点（Building_TeeJunction）

### 阶段三：高级节点与系统
- [ ] 实现 HeatSystem（热传递计算）
- [ ] 实现 ChemicalSystem（化学反应计算）
- [ ] 实现储罐节点（Building_StorageTank）
- [ ] 实现泵节点（Building_Pump）
- [ ] 实现热交换器节点（Building_HeatExchanger）
- [ ] 实现化学反应釜节点（Building_ChemicalReactor）

### 阶段四：优化与扩展
- [ ] 实现性能优化策略（懒惰计算、节点休眠、空间分区）
- [ ] 实现流体可视化（管道流体流动效果）
- [ ] 提供自定义节点接口（IHasNode）
- [ ] 提供自定义流体定义接口
- [ ] 实现存档兼容机制
- [ ] 编写模组文档和API参考

---

## 七、兼容性考虑

### 与现有管道模组的交互
- 提供转换工具，将现有管道系统转换为 RimPipe
- 支持与其他模组的管道系统共存
- 提供桥接节点，连接不同管道系统

### 存档兼容性
- 支持从旧版本存档迁移
- 提供数据迁移工具
- 保持存档格式稳定

---

## 八、未来展望

### 长期目标
- 支持气体和液体的混合流动
- 支持管道腐蚀和维护系统
- 支持动态管道（可弯曲、可伸缩）
- 支持多维度管道（地面、地下、空中）
- 支持流体可视化和调试工具
