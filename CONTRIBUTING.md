# 贡献指南

想给 RimPipe 提交代码的话，先看一下这篇文档，里面是开发环境、代码风格和提 PR 的流程。

## 开发环境

具体步骤见 [`Source/RimPipe/README_build.md`](Source/RimPipe/README_build.md)，要点：

- 用 Visual Studio 2022（或带 MSBuild 的 Build Tools）
- 装 .NET Framework 4.8 开发包
- 依赖 DLL 放在 `Source/Libs/` 下，用相对路径引用（`Private=false`），别写本机绝对路径
- 打开 `Source/RimPipe/RimPipe.sln`，编译产物直接输出到 `Assemblies/RimPipe.dll`
- `*.pdb` 已被 `.gitignore` 忽略，不会提交

游戏相关 DLL 从 `<RimWorld 安装目录>\RimWorldWin64_Data\Managed\` 拷贝到 `Source/Libs/RimWorld/` 和 `Source/Libs/Unity/`。

## 代码风格

- **语言版本：** C# 12.0 / .NET Framework 4.8（见 `RimPipe.csproj`）
- **可空：** `<Nullable>enable</Nullable>`——新增公共 API 注意 nullability 标注
- **命名空间：** `RimPipe`（根）；子目录用子命名空间（如 `RimPipe.Comps`、`RimPipe.Core`）
- **风格对齐：** 跟随既有代码——`sealed` 类优先、必填字段用 `null!` 初始化、能写 expression-bodied 就写
- **不引入 Harmony：** 当前阶段 Bridge-H 延后，除非另有决议，PR 里不要引入 Harmony 依赖
- **不破坏架构约束：**
  - 不要新增 `Node` / `Connection` / `PipeLine` 类名
  - 管道格不储存流体（量只在设备/管件的 `Container`）
  - 不改 Flow / 拓扑 / 存档 schema（schema=1），除非有明确决议

## 计划与决议

项目没有独立的 `docs/plans/` 工作流，所有目标、架构决议、阶段大纲和任务清单都集中在 [`Source/RimPipe_TODO.md`](Source/RimPipe_TODO.md)。提交前确认改动符合文档里的「已锁定」决议；如果文档没覆盖，且改动会影响字段 / 公式 / 存档，停下来问一下再动手，不要自行发明。

## PR 流程

1. **先开 Issue：** 新功能或修 bug 先开 Issue 讨论，避免做完被驳回
2. **分支：** 从 `main` 拉特性分支（如 `feat/xxx`、`fix/yyy`），不要直接推 `main`
3. **提交信息：** 用中文写清楚「做了什么 + 为什么」，参考既有 commit 风格（一句话主题行 + 空行 + 详细说明）
4. **验收：** 涉及运行时行为变更的 PR，在 DevMode 下跑过 5 套回归套件（R-框架 / R-物理 / R-热与环境 / R-化学 / R-扩展 + R-通道），并在 PR 描述里贴日志关键行
5. **不破坏存档：** 不要擅自升级存档 schema；新增字段必须带默认值，旧档读入要能退化到旧行为
6. **代码卫生：** 运行时资源文件（XML Defs、DefInjected、Languages/Keyed、Patches、About.xml、LoadFolders.xml）只放运行时内容，不夹带开发元数据（CR 编号、TODO 标记、审查状态、计划引用）
