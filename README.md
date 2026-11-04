# 模拟输入器（SimulateInputer）

一个基于 .NET 10 + WPF 的 Windows 小工具：把一段文本放在窗口里，设置好热键，然后切到任意程序的输入框按下热键，程序会**像人打字（或输入法上屏）一样逐字模拟输入**，全部输入完成后在屏幕右下角弹出提示。

适合把长段落、固定话术、验证码模板等一次性"敲"进任何只支持键盘输入的地方。

## 功能

- **大文本编辑区**：支持换行、自动换行、滚动与实时字数统计
- **热键录制**：点击热键框后直接按下组合键即可完成设置，`Delete` / `Backspace` / `Esc` 清除
- **逐字延迟**：可设 0–5000 ms；`0` 表示尽可能快
- **全局生效**：热键通过 `RegisterHotKey` 注册，任意前台程序（记事本、浏览器、聊天窗口……）都可用
- **完成提示**：输入结束在屏幕右下角弹出 Toast，空文本、热键被占用、输入失败等情况也会给出提示
- **可中止**：输入过程中按「停止输入」按钮或再次按下热键即可中断

## 使用

1. 在「待输入文本」区域输入要发送的内容
2. 点击「触发热键」框，按下想要的组合键（默认 `Ctrl + J`）
3. 按需调整「逐字延迟」（默认 `20` ms）
4. 把光标点到目标程序的输入框里，按下热键
5. 输入完成后右下角会出现「输入完成，共 N 个字符」提示

注意：本程序窗口处于激活状态时按热键只会弹出提示，不会输入，避免把内容打进自己窗口。

## 高可靠输入的实现

"逐字输入"看起来简单，但在真实环境里很容易翻车，本项目的做法是：

- **Unicode 事件而非虚拟键**：使用 `SendInput` + `KEYEVENTF_UNICODE` 直接下发字符码位，因此中文、emoji（代理对成对成组下发）在绝大多数程序里都能正确上屏，不依赖目标程序的输入法状态
- **特殊字符单独处理**：`\n` 走 `VK_RETURN`、`\t` 走 `VK_TAB`，其余控制字符跳过，避免出现空行或乱码
- **等修饰键松开再开始**：热键触发时 Ctrl / Alt / Shift 往往还按着，程序会先等 150 ms，再轮询 `GetAsyncKeyState` 直到修饰键全部释放（最多 4 s），否则输入内容会被修饰键污染
- **按时间基准节流**：用 `Stopwatch` 累计计划时间而不是简单 `Sleep`，长文本下不会累积漂移；进度回报每 8 个字符一次，避免刷爆 UI 线程
- **失败可诊断**：`SendInput` 返回数量不足时判断错误码，权限被拦截（UIPI，目标程序以管理员运行）会明确提示以管理员身份运行本程序
- **绝不抢焦点**：Toast 使用 `ShowActivated=False` + `WS_EX_NOACTIVATE`，弹出和消失都不会打断目标输入框
- **随时可中断**：每个字符间检查 `CancellationToken`，中止后立即停止后续输入

## 项目结构

```
SimulateInputer/
├─ App.xaml(.cs)                   应用入口、合并主题资源、兜底异常处理
├─ MainWindow.xaml(.cs)            主界面与输入流程编排
├─ Controls/HotkeyBox.xaml(.cs)    热键录制框
├─ Services/
│  ├─ HotkeyDefinition.cs          热键模型（修饰符/虚拟键/显示文案）
│  ├─ GlobalHotkeyService.cs       RegisterHotKey 全局热键注册与 WM_HOTKEY 处理
│  └─ InputSimulator.cs            SendInput 逐字输入引擎
├─ Styles/Theme.xaml               配色、卡片、输入框、按钮样式
└─ Views/
   ├─ ToastKind.cs                 Toast 类型
   └─ ToastWindow.xaml(.cs)        右下角提示窗
```

## 构建

```powershell
dotnet build SimulateInputer.sln -c Release
```

## 发布

框架依赖单文件（体积小，需目标机安装 .NET 10 桌面运行时）：

```powershell
dotnet publish SimulateInputer/SimulateInputer.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true
```

自包含单文件（免安装运行时，体积大；加压缩可减小约一半）：

```powershell
dotnet publish SimulateInputer/SimulateInputer.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

## 环境要求

- Windows 10 / 11
- .NET 10 SDK（构建）
- 若使用框架依赖版本，运行需 .NET 10 桌面运行时
