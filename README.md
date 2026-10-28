# Polytopia Single Player Library

独立的 PolyMod 客户端模组：为普通单人游戏提供多存档管理与可分享的本地回放。

[English](README.en.md) · [开发与构建](docs/DEVELOPMENT.md) · [文件格式](docs/FILE_FORMAT.md) · [验证范围](VERIFICATION.md) · [更新记录](CHANGELOG.md)

## 兼容性

- 模组版本：**0.1.7**。
- 已验证平台：**Windows Steam**。
- 游戏版本：**2.17.3.16375**；PolyMod：**1.2.18**。
- 支持普通单人 Perfection、Domination，以及 Creative 中的 Perfection、Domination、Infinity。
- 可以独立使用；Agent Bridge 是可选兼容项，不是依赖。

当前版本会拒绝其他游戏版本。Pass & Play、周挑战及其他规则模组组合不在已验证范围。

## 功能

- 主菜单使用 **SINGLE PLAYER** 统一入口，内含 **Ongoing / Replays**。
- 管理多个未完成对局，新建游戏保留其他存档；按保存时间排序并支持滚动。
- 点击条目先显示详情。Ongoing 提供 **BACK / OPEN / DELETE / COPY / OPEN FOLDER**；DELETE 二次确认，COPY 创建独立副本。
- 详情按实际规则显示模式；Creative–Perfection / Domination 不会被误标为 Infinity。
- 完整对局结束后，可选择保存或不保存本地回放。
- 回放使用原生播放、暂停、时间轴和视角功能；复制一个 `.polyreplay` 文件即可分享。
- Infinity 地图内 Settings 的 Exit to menu 下方有红色 **TERMINATE**。确认后结束本局，显示 **YOU WIN**，随后询问是否保存回放；其他模式及主菜单不显示此按钮。

回放从本地文件加载，不调用官方回放上传／查询服务。游戏自身的正常登录与联网功能仍由游戏处理。

## 安装

1. 准备上述版本的游戏，并安装 PolyMod 1.2.18。
2. 从 [Releases](https://github.com/xsmfder/polytopia-singleplayer-library/releases) 下载 `single_player_library-0.1.7.polymod`。
3. 正常退出游戏，将文件放入**实际游戏安装目录的 `Mods` 文件夹**。
4. 同一模组只保留一个启用副本，然后重新启动游戏。

不要把包内 DLL 单独安装为另一个加载器的插件。图标已内嵌，不需要额外复制 PNG。

升级前保留自己的游戏资料备份。管理工具会检查游戏进程并备份原安装包；默认只输出计划，加入 `--apply` 才执行：

```powershell
$gameRoot = 'D:\Steam\steamapps\common\The Battle of Polytopia' # 修改为自己的安装路径
python .\tools\manage.py install --game-dir $gameRoot --package '.\dist\single_player_library-0.1.7.polymod' --apply
```

## 存档和回放目录

数据放在当前游戏用户资料目录的 `SinglePlayerLibrary` 下。Windows 常见位置：

```text
%USERPROFILE%/AppData/LocalLow/Midjiwan/Polytopia/<用户ID>/SinglePlayerLibrary/
```

以游戏实际返回的用户目录为准；游戏内 OPEN FOLDER 可直接打开对应目录。

| 子目录 | 内容 |
|---|---|
| `Ongoing` | 独立 `.polysave` 存档；完成对局的恢复记录不会列为进行中 |
| `Replays` | 自包含 `.polyreplay` 文件 |
| `Pending` | 待决定／待重试的结束记录和决定标记 |
| `Backups` | 原生存档接纳和恢复过程的备份 |

首次启用会备份并接纳原生单人存档。升级也会接纳仍留在原生目录、此前未纳入的 Infinity 存档；无法恢复已经不存在的数据。

## 分享回放

1. 在 Replays 页面选择 Open Folder。
2. 将所需 `.polyreplay` 文件交给接收者。
3. 接收者使用相同游戏版本和本模组，将文件放入自己的 `Replays` 目录并刷新列表。

文件包含版本和完整性校验。损坏、未复制完成、不兼容或缺少完整历史的文件会提示错误。旧存档若缺少完整命令历史，不能生成完整回放。

## 停用与恢复原版入口

退出游戏后，将模组移出 `Mods`，或使用：

```powershell
python .\tools\manage.py disable --game-dir $gameRoot --apply
python .\tools\manage.py enable --game-dir $gameRoot --apply
```

停用恢复原版菜单，保留模组数据。需要把某一局恢复到原版 Resume Game 时，先停用模组，然后查看恢复计划，再决定执行：

```powershell
python .\tools\replay_file.py '选定存档的完整路径.polysave' --restore-profile '游戏用户ID目录的完整路径'
# 确认显示的来源、备份和目标路径后执行：
python .\tools\replay_file.py '选定存档的完整路径.polysave' --restore-profile '游戏用户ID目录的完整路径' --apply
```

恢复工具会备份原生单人槽位，再写入所选未完成对局；其他模组存档不受影响。

## 构建与贡献

仓库只包含自己的代码和自有图标，不附带游戏、PolyMod、互操作 DLL、用户资料或测试回放。构建需要本机已有游戏／PolyMod 安装，步骤见 [开发文档](docs/DEVELOPMENT.md)。

欢迎提交问题和改动。请附游戏／模组版本、复现步骤及相关错误文字；贡献约定见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 许可证

本项目使用 [MIT License](LICENSE)，保留现有版权声明。自有图标随项目提供；运行时引用的游戏图像、字体和商标不包含在 MIT 授权范围内，也未随仓库分发，参见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
