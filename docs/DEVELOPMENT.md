# 开发与构建

以下命令在仓库根目录运行，不会自动安装模组或启动游戏。

## 环境

- Windows、PowerShell 7（`pwsh`，包含 Add-Type 使用的 Roslyn 编译程序集）。
- Python 3.9 或更新版本；附带 Python 工具仅依赖标准库。
- 本机已有 Polytopia 2.17.3.16375 和 PolyMod 1.2.18。
- 游戏安装中已有 `dotnet/`、`BepInEx/core/`、`BepInEx/interop/` 及 `BepInEx/plugins/PolyMod.dll`。互操作引用应由正常的模组环境准备。

本项目通过游戏安装中的 .NET 6 / IL2CPP 互操作程序集编译，不附带这些 DLL，不自动下载游戏或依赖。

## 1. 存储测试

```powershell
pwsh -NoProfile -File .\tools\test_store.ps1
```

0.1.7 对应 **42 项断言**，覆盖校验、截断、版本、独立存档、复制、删除、未决记录、重试和 Infinity 终止标记。测试数据输出到被 Git 忽略的 `evidence/`。

## 2. 正式构建

```powershell
$gameRoot = 'D:\Steam\steamapps\common\The Battle of Polytopia' # 修改为自己的安装位置
pwsh -NoProfile -File .\tools\build.ps1 -GameRoot $gameRoot -OutputDirectory '.\build\release-0.1.7'
```

输出目录必须尚不存在，以保留构建快照。重复构建时换一个目录名。

构建输出包含 DLL、源文件／素材快照、编译日志及 `build.json`。正式符号为 `POLYLIB_RELEASE`，排除 `TestHarness.cs` 中的测试执行器；两个自有 PNG 内嵌在 DLL 中。

## 3. 打包

```powershell
python .\tools\package.py --build '.\build\release-0.1.7\build.json' --out '.\dist'
```

打包前检查源码、素材、二进制及本地编译引用的 SHA-256；发生漂移时拒绝打包。相同版本的输出文件已存在时也会拒绝覆盖。

输出：`.polymod`、源码 ZIP、各自的 `.sha256`、本机 `package.json`。`.polymod` 只包含 `manifest.json`、自己的 DLL 和 `LICENSE`。源码 ZIP 使用公开文件白名单，不包含构建输出、日志、用户资料或游戏 DLL。

需要安装时，按 README 中的管理工具命令执行。安装与启动游戏不属于构建步骤。

## 图标

`assets/refresh.png` 和 `assets/folder.png` 是可直接构建的 128×128 透明白色图标。批准的蓝底源图在 `assets/sources/`，来源与哈希见 `assets/provenance.json`。

可选重建图标：

```powershell
pwsh -NoProfile -File .\tools\prepare_icons.ps1
```

该步骤使用 Windows System.Drawing，将蓝／白源图的覆盖率转换为透明度并重采样；普通构建无需重新导图。

## 原生集成诊断

```powershell
pwsh -NoProfile -File .\tools\build.ps1 -GameRoot $gameRoot -OutputDirectory '.\build\diagnostic-new' -TestHarness
```

诊断构建仅在显式 `--spl-test-root=<绝对目录>` 启动参数下执行文件请求。它将游戏用户目录重定向到该目录的 `profile/`；请自行建立隔离夹具。相关命令在 `src/TestHarness.cs`，请求工具为 `tools/native_test.py`。请求工具的目录参数相对于仓库 `evidence/`。

诊断动作会改变其目标对局，不能作为正式发行包。仓库不包含开发过程中的私人回放、账号资料和完整实机测试数据。不要对用户正在玩的真实对局运行这些动作。可选 Bridge 诊断需要另外安装 Agent Bridge，基础功能与存储测试不需要它。

## 代码结构

| 文件 | 作用 |
|---|---|
| `LibraryScript.cs` | 模组生命周期、Harmony 接入、接纳／保存／结束捕获 |
| `NativeSession.cs` | 原生编码、会话恢复、回放加载及原始终态标注 |
| `LibraryStore.cs` | 文件封装、完整性、索引、复制／删除、暂存与决定 |
| `LibraryUI.cs` / `LibraryDetails.cs` | 菜单、列表、详情及叠加说明 |
| `LibraryNavigation.cs` | 返回路径、载入遮罩、回放结果操作 |
| `LibraryTermination.cs` | Infinity 确认终止、原生命令适配、最小结算内容 |
| `LibraryAssets.cs` | 内嵌图标和 Unity 资源生命周期 |
| `BridgeCompatibility.cs` | 可选 Agent Bridge 菜单兼容 |
| `TestHarness.cs` | 正式构建排除的隔离诊断入口 |

更新版本时同步 `manifest.json`、加载日志、README、CHANGELOG 和 Release 说明。游戏升级需要重新验证互操作接口，不能仅修改版本字符串后宣称兼容。
