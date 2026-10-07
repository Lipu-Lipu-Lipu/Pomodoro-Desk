# 🍅 番茄钟 (Pomodoro Desk)

一个用 C# + WinForms 写的 Windows 桌面番茄钟。深色界面、圆环倒计时、置顶提醒卡片，
到点不抢你手里的活——只提醒，切不切换由你说了算。

## 特性

- **工作 / 休息交替**：默认工作 25 分钟、休息 5 分钟，时长可自定义
  （工作 0–120 分 0–59 秒，休息 0–60 分 0–59 秒）。
- **圆环倒计时**：进度环随剩余时间收缩，文字自动缩放，保证任何时长都不压到圆弧。
- **到点不自动切换**：阶段结束后进入**超时正计时**（橙色脉冲显示 `+MM:SS`），
  由你手动点击“结束，进入休息 / 进入工作”才切换，避免打断手头的事。
- **三重提醒**：
  - 音频：开始 / 最后 1 分钟 / 到时各有一段内存生成的提示音（无需音频资源文件）。
  - 系统气泡通知 + 任务栏闪烁。
  - **置顶提醒卡片**：最后 1 分钟自动弹出、可自动关闭；到时提醒需手动“知道了”确认。
- **托盘常驻**：最小化后可从托盘右键菜单“打开番茄钟 / 退出”，双击图标也可唤回窗口。
- **设置自动保存**：时长改动即写入 `%APPDATA%\PomodoroTimer\settings.json`，下次启动自动恢复。
- **高 DPI 友好**：Per-Monitor V2，跨屏缩放时自动校正窗口尺寸与布局。
- **深色主题**：主窗口、提醒卡片、托盘菜单配色统一。

## 运行环境

- Windows 10 / 11
- [.NET 10 桌面运行时](https://dotnet.microsoft.com/download/dotnet/10.0)（或 SDK，用于从源码构建）

## 快速开始

### 方式一：运行已发布版本

```
run.bat
```

首次运行若 `publish\` 为空，会先自动 `dotnet publish` 再启动。

### 方式二：从源码构建

```powershell
# 构建并自测
.\rebuild.bat

# 或手动构建
dotnet publish -c Release -o publish
```

生成的可执行文件位于 `publish\PomodoroTimer.exe`。

## 使用说明

1. 在主界面设置工作 / 休息时长（空闲时才可修改，运行中会被锁定）。
2. 点击 **开始**，进入工作阶段。
3. 阶段结束时会响铃并弹出置顶卡片——工作/休息的切换**不会自动发生**，
   请在主窗口点击 **结束，进入休息** 或 **结束，进入工作**。
4. 想从头再来，点击底部的 **返回设置** 回到空闲状态。
5. 关闭窗口即退出程序，计时状态不保留。

## 创建桌面快捷方式

在 PowerShell 中运行：

```powershell
.\make-shortcut.ps1
```

会在桌面生成指向 `publish\PomodoroTimer.exe` 的 `Pomodoro Timer.lnk`
（快捷方式直接指向 exe，避免 .bat 启动时闪出控制台窗口）。

## 自测

核心状态机与音频生成带有无界面的自动化自测：

```
publish\PomodoroTimer.exe --selftest
```

会打印 24 条 `PASS` 与最后的 `ALL PASS`，全部通过时进程返回 `0`。
`rebuild.bat` 已把构建 + 自测串在一起，是最快的健康检查方式。

## 项目结构

| 文件 | 说明 |
| --- | --- |
| `Program.cs` | 程序入口，含 `--selftest` 自测逻辑 |
| `MainForm.cs` | 主窗口 UI、计时循环、圆环绘制、提醒联动 |
| `PomodoroEngine.cs` | 番茄钟核心状态机（纯逻辑，无 UI） |
| `AlertForm.cs` | 置顶提醒卡片 |
| `BeepPlayer.cs` | 内存生成 WAV 提示音并播放 |
| `AccentButton.cs` | 圆角胶囊强调按钮 |
| `DarkMenuRenderer.cs` | 托盘菜单深色渲染器 |
| `SettingsStore.cs` | 设置读写与范围钳制 |
| `Win32Interop.cs` | 窗口置顶 / 前台激活等 Win32 调用 |
| `tomato.ico` | 应用图标 |
| `rebuild.bat` / `run.bat` / `make-shortcut.ps1` | 构建、运行、创建快捷方式脚本 |

## 备注

`publish\` 是本机发布产物（桌面快捷方式指向它），已被 `.gitignore` 忽略，
仓库中不保存编译产物。

## 许可证

[MIT](LICENSE) © 2026 李普
