# 手动创建仓库与发布

项目仓库：[xsmfder/polytopia-singleplayer-library](https://github.com/xsmfder/polytopia-singleplayer-library)。以下流程供维护者复用；发布文件不包含远程凭据。

## 上传源码

1. 在 GitHub 新建空仓库。这里已经准备好 README、MIT LICENSE 和 .gitignore，建仓时无需再生成同名文件。
2. 将本目录的**内容**放到仓库根目录。不要上传它的上级工作目录。
3. 可以使用 GitHub 网页上传，也可以在本目录运行以下 Git 命令；把 `$repoUrl` 改成自己的仓库地址：

```powershell
$repoUrl = '粘贴你创建的GitHub仓库地址'
git init
git add .
git diff --cached --stat
git commit -m "Initial release: Single Player Library 0.1.7"
git branch -M main
git remote add origin $repoUrl
git push -u origin main
```

执行 `git diff --cached --stat` 时，确认没有 evidence、build、dist、存档、日志和 DLL。网页拖拽上传不会自动执行 .gitignore 过滤，因此直接上传准备好的干净目录即可，不要先把构建结果输出到其中再整目录拖拽。

## 创建 Release

1. 源码提交后，在 Releases 创建标签 **v0.1.7**，目标为对应源码提交。
2. 标题可用 **Single Player Library v0.1.7**。
3. 正文使用 [RELEASE-v0.1.7.md](RELEASE-v0.1.7.md)。
4. 附件从本目录之外单独准备的 `release-assets/` 选择 `.polymod`、`.polymod.sha256`、公开源码 ZIP、校验文件及 SHA256SUMS.txt。
5. GitHub 自动生成的 Source code 归档也可以下载；不要将开发机的旧私有源码包或 evidence 上传为附件。

二进制附件来自已安装验证的 0.1.7，其 C# 源码、图标、清单和 MIT 许可证与本仓库对应。公开源码 ZIP 使用本次整理后的说明文档。重新编译可能产生不同 DLL 哈希；版本和源码／素材绑定仍需验证。

## 后续版本

同步版本、更新记录和说明，测试后构建／打包，退出游戏再安装验证。只将 Release 附件上传到 Releases，不把本机 package.json、编译引用路径表或测试证据提交进源码仓库。
