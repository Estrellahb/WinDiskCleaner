# WinDiskCleaner

WinDiskCleaner 是一个面向 Windows 11 的本地磁盘清理辅助工具，使用 WPF + .NET 8 构建。当前版本定位为 Preview / 测试版，已经完成空间扫描、清理建议、重复文件检测、注册表工具、快捷方式检测等核心 MVP 能力。

项目目标是提供一个可审计、可回退、尽量保守的 Windows 清理工具。所有涉及删除、注册表恢复、批量处理的功能都需要经过显式确认，并在 Core 层保留必要的安全约束。

## 当前状态

- 当前主分支：`main`
- 最新合并：`merge: MVP5 shortcut tools`
- 当前定位：测试版 / Preview
- 已完成阶段：MVP1-MVP5
- 推荐运行环境：Windows 11
- 开发框架：.NET 8、WPF、xUnit

当前验证结果：

```bash
dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore --filter Mvp5
```

- Passed：10
- Failed：0

```bash
dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore
```

- Passed：60
- Failed：0

```bash
dotnet build WinDiskCleaner.sln --no-restore
```

- Build succeeded
- Warning：0
- Error：0

## 功能概览

### 空间扫描与空间地图

- 扫描指定磁盘或目录。
- 生成目录树与文件统计。
- 展示目录大小、文件数量、扫描进度。
- 支持空间地图 / Treemap 可视化基础能力。
- 对扫描结果进行风险分类，为后续清理建议提供依据。

### 清理建议

- 基于扫描报告生成清理候选项。
- 按风险等级区分低风险、中风险、高风险、未知风险。
- 支持清理建议页面展示。
- 支持报告生成与历史辅助能力。
- 对高风险位置保持保守策略，避免系统目录、程序目录被误判为可清理对象。

### AI 清理建议

- 支持 OpenAI-compatible 接口配置。
- 可基于本地扫描结果请求 AI 分析。
- AI 建议不会直接驱动删除操作。
- 删除执行前仍由 Core 层重新校验风险和候选路径。
- 涉及本地路径发送给外部 AI 服务前，需要用户显式确认。

### 重复文件检测

- 支持指定目录扫描重复文件。
- 通过文件大小与内容哈希识别重复组。
- 支持重复文件列表展示、推荐保留项、手动选择删除项。
- 删除前重新校验重复组内容，避免扫描后文件变化造成误删。
- 每组至少保留一个文件。
- 删除失败记录错误，不影响其他项继续处理。
- 支持 CSV / HTML 报告导出。
- CSV 导出包含公式注入防护。

### 注册表工具

注册表功能聚焦 Windows 卸载项辅助检查，当前支持三个卸载注册表分支：

- `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall`
- `HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall`
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall`

已实现能力：

- 扫描已安装软件相关卸载项。
- 解析 DisplayName、版本、发布者、安装路径、估算大小、卸载命令、注册表路径。
- 标记安装目录缺失的软件为疑似残留项。
- 标记空白或异常软件名为无效项。
- 支持 `.reg` 备份。
- 支持备份历史展示和备份文件删除。
- 支持 `.reg` 恢复前预览。
- 支持恢复前二次确认。
- 支持操作日志。

注册表安全约束：

- `.reg` 恢复只允许三个 Uninstall 分支及其子项。
- 拒绝 `[-HKEY...]` 破坏性删除段。
- `.reg` header 校验使用首个有效行，不接受正文中夹带 header 的文件。
- 备份删除限制在配置的备份根目录内。
- UI 恢复流程需要二次确认。

### 快捷方式检测与修复

快捷方式功能聚焦桌面、开始菜单、启动项中的 `.lnk` 文件。

扫描范围：

- 当前用户桌面
- 公共桌面
- 当前用户开始菜单
- 公共开始菜单
- 启动文件夹

已实现能力：

- 递归扫描 `.lnk` 文件。
- 解析快捷方式目标路径和参数。
- 检查目标文件或目录是否存在。
- 标记目标不存在的失效快捷方式。
- 支持扫描取消。
- UI 展示名称、快捷方式路径、目标路径、状态、来源目录。
- 失效项红色高亮。
- 支持搜索过滤。
- 支持按路径、状态排序。
- 支持全选失效、反选失效。
- 支持删除选中的失效快捷方式。
- 删除前展示快捷方式路径与目标路径。
- 支持 CSV / HTML 报告导出。

快捷方式安全约束：

- 删除只处理 `.lnk` 快捷方式文件本身。
- 不删除快捷方式指向的目标程序或文件。
- 有效快捷方式不会进入删除候选。
- Core 删除服务会重新校验扩展名为 `.lnk`。
- Core 删除服务会限制快捷方式路径必须位于允许扫描根目录内。
- 单个损坏快捷方式解析失败不会中断整体扫描。
- CSV 导出会防护公式注入，包括前导空白/控制字符后的公式前缀。

## 界面结构

主窗口左侧为功能导航，当前包含：

- 空间地图
- 清理建议
- 重复文件
- 注册表
- 快捷方式
- 设置

右侧为对应功能页面。MVP4 接入注册表页，MVP5 接入快捷方式页。

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
    MVP4 注册表工具 Spec Kit artifacts
  002-mvp5-shortcuts/
    MVP5 快捷方式检测 Spec Kit artifacts
```

## 技术栈

- .NET 8
- WPF
- C# nullable enabled
- xUnit
- Windows Registry APIs / `reg.exe`
- WScript Shell COM 用于 Windows `.lnk` 解析

## 环境要求

开发和构建：

- .NET SDK 8.x
- Windows 11 推荐用于运行 WPF UI
- Linux 环境可执行 Core 测试和跨目标 build，但 WPF UI 真实交互需要 Windows 环境验证

项目启用了 Windows targeting：

```xml
<TargetFramework>net8.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<EnableWindowsTargeting>true</EnableWindowsTargeting>
```

## 构建与测试

还原依赖：

```bash
dotnet restore WinDiskCleaner.sln
```

运行 Core 测试：

```bash
dotnet test tests/WinDiskCleaner.Core.Tests/WinDiskCleaner.Core.Tests.csproj --no-restore
```

构建解决方案：

```bash
dotnet build WinDiskCleaner.sln --no-restore
```

构建 Release：

```bash
dotnet build WinDiskCleaner.sln -c Release
```

发布 Windows 测试版：

```bash
dotnet publish src/WinDiskCleaner.App/WinDiskCleaner.App.csproj -c Release -r win-x64 --self-contained false -o artifacts/win-x64
```

如需自包含版本，可改为：

```bash
dotnet publish src/WinDiskCleaner.App/WinDiskCleaner.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64-self-contained
```

## 测试版发布建议

当前代码已经满足测试版发布条件：

- Core 测试通过。
- 解决方案构建通过。
- MVP3 / MVP4 / MVP5 均经过独立 review。
- 涉及删除和注册表操作的高风险点已有 Core 层安全约束。

发布测试版前建议在 Windows 11 测试机执行手动检查：

- 启动应用，确认左侧各 Tab 正常切换。
- 执行空间扫描，确认进度、统计、空间地图无异常。
- 执行重复文件扫描，确认删除前提示和报告导出。
- 执行注册表扫描，确认不会自动修改注册表。
- 执行注册表备份，确认 `.reg` 文件生成。
- 使用测试 `.reg` 文件验证恢复预览和二次确认流程。
- 执行快捷方式扫描，确认失效项高亮、删除确认、报告导出。
- 在非管理员权限和管理员权限下分别启动，观察权限相关提示。

测试版说明中建议明确：

- 该版本为 Preview / 内测版。
- 注册表恢复、快捷方式删除、重复文件删除属于有副作用操作。
- 建议先在测试机或非关键环境验证。
- 注册表操作前建议保留备份。
- 不建议在生产工作机上直接执行大批量删除。

## 安全设计原则

WinDiskCleaner 当前采用保守清理策略：

- AI 建议不能直接删除文件。
- 删除执行必须基于当前扫描结果和 Core 层校验。
- 高风险路径默认不进入自动删除流程。
- 重复文件删除前重新校验大小与哈希。
- 注册表恢复限制在明确支持的 Uninstall 分支内。
- 快捷方式删除只删除 `.lnk` 文件本身。
- 报告导出对 CSV 公式注入做防护。
- 删除失败记录错误，不阻断其他项处理。

## 已知限制

- 当前仍是测试版，尚未提供正式安装包。
- WPF UI 需要在 Windows 上进行真实交互验证。
- 快捷方式扫描只覆盖桌面、开始菜单、启动项，不扫描自定义目录。
- 快捷方式功能不修复或重建目标路径。
- 注册表功能只覆盖卸载项相关分支，不做泛注册表清理。
- 当前不提供自动清理模式。
- 当前不提供云同步、多用户策略、企业部署策略。

## 开发流程

项目使用阶段分支推进：

- MVP3：`mvp3-duplicate-files`
- MVP4：`mvp4-registry`
- MVP5：`mvp5-shortcuts`

阶段完成标准：

- Spec Kit artifacts 完整。
- Core 行为使用 TDD 验证。
- Focused tests 通过。
- Full Core tests 通过。
- Solution build 通过。
- 独立 review 通过。
- 使用 `[verified]` 前缀提交。
- 验收后合并回 `main`。

## 许可证

当前仓库尚未添加许可证文件。发布公开版本前建议补充 `LICENSE`。
