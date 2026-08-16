# RimPipe 构建说明

本文说明如何在本机编译本模组。依赖都放在项目内的 `Libs` 里，不依赖每个人电脑上的 Steam 安装路径。

## 环境要求

- Visual Studio 2022（或带 MSBuild 的 Build Tools）
- .NET Framework 4.8 开发包（Target Framework）
- 本机已安装对应版本的 RimWorld（用于从游戏目录拷贝依赖 DLL）

打开工程请用：

- `Source/RimPipe/RimPipe.sln`

## 依赖放在哪里

编译引用统一走 `Libs`，路径写在 `RimPipe.csproj` 里，都是相对路径。

当前需要这些文件：

| 路径 | 用途 |
|---|---|
| `Libs/RimWorld/Assembly-CSharp.dll` | 游戏主程序集 |
| `Libs/Unity/UnityEngine.dll` | Unity 基础 |
| `Libs/Unity/UnityEngine.CoreModule.dll` | Unity 核心 |
| `Libs/Unity/UnityEngine.IMGUIModule.dll` | IMGUI |
| `Libs/Unity/UnityEngine.TextRenderingModule.dll` | 文字渲染 |

游戏相关 DLL 从这里拷：

`\<你的 RimWorld 安装目录\>\RimWorldWin64_Data\Managed\`

注意：

- 游戏更新后，若编译或运行异常，优先用当前游戏的 Managed 重新覆盖 `Libs/RimWorld` 与 `Libs/Unity`。
- 以后要加新依赖，放在 `Libs/<包名>/` 下，并在 csproj 里用相对路径引用；不要写本机绝对路径。
- `Private=False`，编译产物不会把依赖 DLL 再复制进输出目录。

## 怎么跑单元测试与校验

```bat
dotnet test ..\RimPipe.Tests\RimPipe.Tests.csproj
python ..\..\tools\validate_config.py
```


- 测试工程只链接 `Source/RimPipe/Core/Pure/` 下的纯逻辑源码，不依赖游戏 DLL。
- CI 使用 `windows-latest`，依次执行：build RimPipe → dotnet test → XML/DefOf/本地化校验。

## DLL 提交约定

- 代码提交不要混入 DLL；DLL 由单独提交承载。
- 提交前使用固定 SDK 重建：
  ```bat
  powershell -ExecutionPolicy Bypass -File ..\\..\\tools\\rebuild-dll.ps1
  ```
  或
  ```bat
  dotnet build ..\..\Source\RimPipe\RimPipe.csproj -c Release
  ```
- CI 会执行 deterministic 校验：同一配置连续构建两次，确认 DLL 哈希一致。
- 若 committed DLL 与 CI 构建产物不一致，应先用上述脚本重建后再单独提交 DLL。

## 怎么编译


任选一种即可：

1. Visual Studio：打开 `RimPipe.sln`，选 Debug 或 Release，生成解决方案。
2. 命令行（示例）：

```bat
msbuild RimPipe.csproj /p:Configuration=Debug
```

Debug 与 Release 的输出目录相同：

- `Assemblies/RimPipe.dll`

也就是模组根目录下的 `Assemblies/`。编完一般不用再手动拷贝，游戏加载该模组时会直接读这里。

同目录可能生成 `RimPipe.pdb`（调试符号）。本地调试可以留着；已通过 `.gitignore` 忽略 `*.pdb`，不会提交。

## 目录约定（和编译相关的部分）

```
Source/RimPipe/
├── Libs/                     编译依赖（不要删）
├── Core/ Comps/ Debug/ …     业务源码
├── Properties/
├── RimPipe.csproj
├── RimPipe.sln
└── README_build.md           本说明
```

模组运行时内容在仓库/模组根的 `About/`、`Defs/`、`Assemblies/`、`Languages/` 等目录；本文件只覆盖「怎么把 C# 编成 DLL」。

## 常见问题

**Libs 缺文件或路径不对**  
先对照上面的表格检查文件是否齐全，再确认 csproj 里的 `HintPath` 仍指向 `Libs\...`。

**换了电脑 / 换了 RimWorld 安装盘符**  
只要 `Libs` 完整，一般不用改工程。不要再把引用改回 `C:\Program Files\...` 这类绝对路径。

**不该放进工程目录的东西**  
`bin/`、`obj/`、`.vs/`、散落在 `Source` 根目录的游戏 DLL 等都不需要。依赖只保留在 `Libs` 里。
