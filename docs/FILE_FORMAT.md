# 存档和回放文件

## 封装

`.polysave` 与 `.polyreplay` 使用相同外层封装（格式版本 1）：

| 顺序 | 内容 |
|---|---|
| 8 字节 | ASCII `POLYRPL1` |
| 4 字节 | JSON 元数据长度，little-endian Int32 |
| 4 字节 | 原生载荷长度，little-endian Int32 |
| 可变 | UTF-8 JSON 元数据 |
| 可变 | 原生压缩载荷 |
| 32 字节 | 前述所有字节的 SHA-256 |

载荷上限 64 MiB；元数据上限 64 KiB。完整规则见 `LibraryStore.Validate`。校验用于检测损坏，不是文件作者身份签名。

元数据含格式／游戏／原生版本、对局 GUID、部落、实际模式、回合、时间、完成与历史完整性、原生编码、状态哈希及结果。模式为 Perfection、Domination 或 Infinity。可选 `Terminated=true` 仅允许已完成的 Infinity；旧文件默认为 false。

## 原生编码

- 存档的 `local-client` 是原生 LocalClient 保存编码。
- 回放的 `wrapped-client` 使用 ClientSerializationWrapper / ForceSaveAsReplay，封装初始状态、命令历史和终态。

两种载荷不能直接改扩展名互换。回放需完成对局且历史完整；初始状态与命令栈缺失时不能补造完整回放。

## Infinity 终止

用户确认后，把原生 EndMatchCommand 追加到命令历史。原生规则不允许直接结束 Sandbox，因此校验适配仅覆盖已确认的当前会话及本模组标记的回放。文件保留真实终止命令和终态；结算显示 YOU WIN，不将其作为官方成绩提交。

## 原子写入与恢复

写入先创建相邻临时文件、刷新数据，再替换目标。结束捕获进入 Pending；保存成功或选择不保存时，先写决定标记再清理暂存记录。写入失败时保留可用数据，避免重复决定。具体持久化边界与错误处理以 `LibraryScript.cs`、`LibraryStore.cs` 为准。

已完成的恢复存档可以保留，但不会列为 Ongoing；恢复工具只接受未结束的存档。导入同一回放的副本按标识和载荷去重。
