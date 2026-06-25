# Tasks: MVP5 快捷方式检测与修复

**Input**: `specs/002-mvp5-shortcuts/spec.md`, `specs/002-mvp5-shortcuts/plan.md`

## Phase 1: Setup

- [x] T001 从 `main` 迁出 `mvp5-shortcuts` 分支
- [x] T002 创建 MVP5 Spec Kit artifacts

## Phase 2: Core Models and Contracts

- [x] T003 添加 `ShortcutItem` 模型
- [x] T004 添加删除结果与报告相关模型
- [x] T005 添加 `IShortcutScanner`、`IShortcutDeleteService`、`IShortcutReportExporter` 接口

## Phase 3: Core Tests First

- [x] T006 添加扫描空目录返回空列表的 RED 测试
- [x] T007 添加 `.lnk` 解析目标路径与参数的 RED 测试
- [x] T008 添加目标存在标记有效、目标不存在标记失效的 RED 测试
- [x] T009 添加非 `.lnk` 文件忽略的 RED 测试
- [x] T010 添加无权限/不可访问目录跳过不崩溃的 RED 测试
- [x] T011 添加扫描可取消的 RED 测试
- [x] T012 添加删除只处理选中失效 `.lnk` 且不删除目标的 RED 测试
- [x] T013 添加 CSV/HTML 报告导出的 RED 测试

## Phase 4: Core Implementation

- [x] T014 实现 `ShortcutScanner`
- [x] T015 实现 `ShortcutDeleteService`
- [x] T016 实现 `ShortcutReportExporter`
- [x] T017 运行 focused MVP5 tests 并修复失败

## Phase 5: UI Implementation

- [x] T018 添加 `ShortcutViewModel`，包含列表、搜索、排序、统计、选择状态
- [x] T019 添加 `ShortcutTab.xaml` 和 `ShortcutTab.xaml.cs`
- [x] T020 在主窗口接入 Tab5 快捷方式页
- [x] T021 实现删除前确认与删除后刷新列表
- [x] T022 实现报告导出按钮

## Phase 6: Verification and Review

- [x] T023 运行 focused MVP5 tests
- [x] T024 运行 full Core tests
- [x] T025 运行 solution build
- [x] T026 执行静态扫描与独立 code review
- [ ] T027 提交 `[verified] feat: add MVP5 shortcut tools`
- [ ] T028 验收通过后合并回 `main`
