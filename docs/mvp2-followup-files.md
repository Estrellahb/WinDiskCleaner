# WinDiskCleaner MVP2 后续待改文件清单

生成时间：2026-06-24
当前基线分支：review/mvp2-safety-hardening

## 当前状态

MVP2 P0 安全收尾已完成：

- RiskClassifier 已修复保护目录优先级。
- Tab2 执行清理已强制使用当前扫描 LowRiskItems 白名单。
- AI 分析前已增加隐私确认。
- Core 测试 21/21 通过。
- Solution build 0 Warning / 0 Error。

后续内容不阻塞 MVP2 安全验收，属于质量、体验、长期安全和清理项。

## 1. 测试代码质量

### 文件

`tests/WinDiskCleaner.Core.Tests/Mvp2ServiceTests.cs`

### 当前问题

测试中存在 xUnit analyzer 警告：

```text
xUnit1031: Test methods should not use blocking task operations
```

触发点：

```csharp
request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()
```

### 建议修改

- 将 `StubHttpMessageHandler` 支持 async delegate。
- 测试中使用 `await request.Content.ReadAsStringAsync()`。
- 避免在测试线程中阻塞异步任务。

### 优先级

P1，测试质量改进。

## 2. API Key 明文存储

### 文件

`src/WinDiskCleaner.App/Views/SettingsView.xaml.cs`

### 当前问题

配置保存到：

```text
%LocalAppData%/WinDiskCleaner/settings.json
```

其中 API Key 为明文保存。

### 风险

- 本机其他可读该文件的进程可能读取 API Key。
- 正式版不适合明文保存密钥。

### 建议修改

MVP 后续方案：

- Windows DPAPI：`ProtectedData.Protect/Unprotect`
- 或 Windows Credential Manager
- 至少补充文件 ACL，仅当前用户可读写
- UI 明示“API Key 将保存在本机配置中”

### 优先级

P1，发布前安全加固。

## 3. 释放空间统计不够准确

### 文件

`src/WinDiskCleaner.Core/Services/CleanExecutor.cs`

### 当前问题

清理结果中的 `FreedBytes` 当前来自 AI 建议：

```csharp
result.FreedBytes += item.EstimatedSpace;
```

### 风险

- AI 返回值错误时，释放空间显示不准确。
- 恶意或异常返回可能夸大清理效果。

### 建议修改

- 删除或移动前计算真实文件大小。
- 目录应递归计算大小。
- `EstimatedSpace` 只作为 UI 预估字段。
- `FreedBytes` 使用实际清理前读取到的大小。

### 优先级

P1，结果可信度改进。

## 4. AI HTTP 调用体验

### 文件

`src/WinDiskCleaner.Core/Services/AIService.cs`

`src/WinDiskCleaner.App/Views/CleanSuggestionView.xaml.cs`

### 当前问题

AI HTTP 请求缺少明确超时和取消机制。

### 影响

- 网络慢或接口无响应时，UI 长时间停留在“AI 分析中...”。
- 用户无法取消当前请求。

### 建议修改

- 设置请求超时，例如 30 秒。
- AI 分析期间禁用按钮。
- 请求完成或失败后恢复按钮状态。
- 后续可加 CancellationToken。

### 优先级

P2，交互体验优化。

## 5. 风险分类器长期精细化

### 文件

`src/WinDiskCleaner.Core/Services/RiskClassifier.cs`

### 当前状态

P0 已修复：

- Windows 目录优先 Forbidden
- Program Files 目录优先 High
- Low 风险规则不再覆盖保护目录

### 仍需优化

当前分类仍基于字符串规则，适合 MVP，但长期需要更细的规则体系：

- 用户临时目录白名单
- 浏览器缓存目录白名单
- 崩溃 dump 文件规则限制为文件扩展名匹配
- 避免任意路径包含 `Cache` 就被判定为 Low
- 按路径根、目录段、文件扩展名分层匹配

### 优先级

P2，长期安全与准确性优化。

## 6. UI 清理后刷新逻辑进一步明确

### 文件

`src/WinDiskCleaner.App/Views/CleanSuggestionView.xaml.cs`

### 当前状态

清理完成后已做到：

- 移除成功清理的 AI 建议
- 从 `_currentReport.LowRiskItems` 移除成功路径
- 扣减 `EstimatedSafeClean`
- 重新加载报告显示

### 后续建议

- 明确区分普通建议列表和 AI 建议列表刷新逻辑。
- 后续可在清理完成后触发重新扫描，得到更准确状态。
- 对失败项显示更明确的错误详情。

### 优先级

P2，体验优化。

## 7. 模板与重复文件清理

### 文件

`src/WinDiskCleaner.Core/Class1.cs`

`src/WinDiskCleaner.Infrastructure/Class1.cs`

`tests/WinDiskCleaner.Core.Tests/UnitTest1.cs`

`src/WinDiskCleaner.App/Views/SettingsTab.xaml`

`src/WinDiskCleaner.App/Views/SettingsTab.xaml.cs`

### 当前问题

- `Class1.cs` 是 classlib 模板文件。
- `UnitTest1.cs` 是测试模板文件。
- `SettingsTab` 与 `SettingsView` 功能重复，当前 `MainWindow` 使用的是 `SettingsView`。

### 建议修改

- 删除无用模板文件。
- 确认是否保留 `SettingsTab`，若无引用则删除。
- 删除后运行 build 确认没有 XAML 或引用残留。

### 优先级

P3，代码整理。

## 8. 成功清理日志增强

### 文件

`src/WinDiskCleaner.Core/Services/CleanExecutor.cs`

### 当前问题

清理日志主要记录统计与错误，成功路径可追溯性不足。

### 建议修改

- `CleanResult` 已有 `SucceededPaths`。
- `WriteLogAsync` 中追加成功路径列表。
- 日志中区分：成功、失败、跳过。

### 优先级

P3，可审计性增强。

## 建议后续阶段

推荐新开分支处理：

```text
review/mvp2-quality-followup
```

建议分批：

1. P1：修 xUnit1031、真实 FreedBytes、API Key 本地保护。
2. P2：AI timeout / cancel、RiskClassifier 精细化。
3. P3：模板文件清理、日志增强。

每个阶段继续使用独立分支，完成后再合并回 main。
