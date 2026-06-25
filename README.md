# WinDiskCleaner

WinDiskCleaner 是一个面向 Windows 11 的本地磁盘清理辅助工具，使用 .NET 8 和 WPF 构建。应用围绕磁盘空间扫描、清理建议、重复文件检测、注册表卸载项检查、失效快捷方式检测等场景展开，所有高风险操作都需要明确确认后才会执行。

## 主要功能

### 空间扫描

- 扫描指定磁盘或目录。
- 展示目录大小、文件数量和扫描进度。
- 生成目录树和空间占用统计。
- 提供空间地图 / Treemap 可视化基础能力。
- 根据路径和文件类型对扫描结果进行风险分类。

### 清理建议

- 基于扫描结果生成清理候选项。
- 按低风险、中风险、高风险、未知风险分类展示。
- 高风险位置默认保持保守策略，避免系统目录、程序目录被误判为普通清理对象。
- 删除执行前会重新校验候选路径和风险等级。

### AI 清理建议

- 支持 OpenAI-compatible 接口配置。
- 可将本地扫描结果提交给 AI 服务生成分析建议。
- AI 建议只作为参考，不会直接触发删除。
- 涉及本地路径发送到外部 AI 服务前，需要用户确认。

### 重复文件检测

- 支持指定目录扫描重复文件。
- 通过文件大小和内容哈希识别重复组。
- 展示重复文件列表、推荐保留项和可删除项。
- 每组至少保留一个文件。
- 删除前会重新校验文件大小和哈希，避免扫描后文件变化导致误删。
- 支持 CSV / HTML 报告导出。

### 注册表卸载项检查

注册表功能聚焦已安装软件的卸载项，不做泛注册表清理。

支持扫描的分支：

```text
HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall
HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall
HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall
```

已支持能力：

- 扫描已安装软件相关卸载项。
- 读取软件名称、版本、发布者、安装路径、估算大小、卸载命令、注册表路径。
- 标记安装目录缺失的软件为疑似残留项。
- 标记空白或异常软件名为无效项。
- 支持 `.reg` 备份。
- 支持备份历史查看和备份文件删除。
- 支持 `.reg` 恢复前预览。
- 恢复前需要二次确认。

### 快捷方式检测

快捷方式功能用于检查桌面、开始菜单、启动项中的 `.lnk` 文件。

扫描范围：

- 当前用户桌面
- 公共桌面
- 当前用户开始菜单
- 公共开始菜单
- 启动文件夹

已支持能力：

- 递归扫描 `.lnk` 文件。
- 解析快捷方式目标路径和参数。
- 检查目标文件或目录是否存在。
- 标记目标不存在的失效快捷方式。
- 支持搜索、排序、全选失效项、反选失效项。
- 支持删除选中的失效快捷方式。
- 删除时只删除 `.lnk` 文件本身，不删除目标程序或目标文件。
- 支持 CSV / HTML 报告导出。

## 使用方式

### 下载运行

在 GitHub Releases 页面下载 Windows x64 便携版程序：

```text
WinDiskCleaner-win-x64.exe
```

下载后双击运行，无需安装 .NET Runtime，也不需要额外复制依赖文件。

### 基本操作

打开应用后，左侧导航包含以下页面：

- 空间地图
- 清理建议
- 重复文件
- 注册表
- 快捷方式
- 设置

常见使用流程：

1. 在“空间地图”页面选择需要扫描的磁盘或目录。
2. 等待扫描完成后查看目录占用和文件统计。
3. 在“清理建议”页面查看可清理候选项。
4. 对需要删除的项目进行确认后再执行清理。
5. 在“重复文件”页面选择目录并扫描重复文件。
6. 检查每组重复文件的保留项和删除项。
7. 在“注册表”页面扫描卸载项残留，必要时先导出 `.reg` 备份。
8. 在“快捷方式”页面扫描失效 `.lnk` 文件，确认后删除无效快捷方式。

## 安全机制

WinDiskCleaner 默认采用保守处理策略：

- AI 建议不能直接删除文件。
- 删除执行必须经过 Core 层安全校验。
- 高风险路径不会进入自动删除流程。
- 重复文件删除前会重新校验文件大小和哈希。
- 注册表恢复只允许已支持的 Uninstall 分支。
- 快捷方式删除只处理 `.lnk` 文件本身。
- CSV 报告导出带有公式注入防护。
- 删除失败会记录错误，并继续处理其他项目。

## 技术栈

- .NET 8
- C#
- WPF
- xUnit
- LiteDB
- Microsoft.Xaml.Behaviors.Wpf
- Windows Registry APIs
- `reg.exe`
- WScript Shell COM，用于解析 Windows `.lnk` 快捷方式
- GitHub Actions，用于 Windows 预览版自动构建和发布

## 项目结构

```text
WinDiskCleaner.sln
src/
  WinDiskCleaner.App/
    WPF 应用、窗口、页面、ViewModel
  WinDiskCleaner.Core/
    核心模型、接口、服务、扫描、删除、报告、安全规则
  WinDiskCleaner.Infrastructure/
    Windows 注册表读取、导出、导入等系统集成实现
tests/
  WinDiskCleaner.Core.Tests/
    Core 层 xUnit 测试
specs/
  001-mvp4-registry/
    注册表工具规格文档
  002-mvp5-shortcuts/
    快捷方式检测规格文档
```

## 本地开发

还原依赖：

```bash
dotnet restore WinDiskCleaner.sln
```

运行测试：

```bash
dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore
```

构建解决方案：

```bash
dotnet build WinDiskCleaner.sln --no-restore
```

发布 Windows x64 版本：

```bash
dotnet publish src/WinDiskCleaner.App/WinDiskCleaner.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o artifacts/WinDiskCleaner-preview-win-x64
```

## 运行环境

- Windows 11 推荐
- x64 系统
- 自包含发布包无需预装 .NET Runtime
- 源码构建需要 .NET SDK 8.x

## License

MIT License. See [LICENSE](LICENSE) for details.
