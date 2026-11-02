# CnDeX — 三星 DeX 简易中文客户端

独立编写的 **简体中文 Windows 图形界面**，通过本机已安装的 [scrcpy](https://github.com/Genymobile/scrcpy) + `adb` 启动 / 附着三星 DeX 虚拟桌面。

> 本项目为原创简洁界面，**不是** ScrcpyDex / DX-Manager 的分支或汉化包，也不包含其专有服务端。

## 功能

- 检测已通过 USB 或无线调试连接的 `adb` 设备
- **无线调试 UI**：填写 IP、配对端口、配对码 → `adb pair`；再填连接端口 → `adb connect`；可断开并刷新设备列表
- 配置 `scrcpy.exe` 路径；`adb` 可选手填，默认自动在 scrcpy 同目录或 PATH 中查找
- 参数：分辨率、DPI、码率、帧率
- **键盘输入**：`--keyboard=uhid`（默认推荐）/ `aoa` / `sdk`；SDK 模式下可选 `--raw-key-events`、`--prefer-text`；可选 `--no-key-repeat`
- **鼠标**：`--mouse=uhid`（默认）/ `aoa` / `sdk`；灵敏度滑条 + 慢/标准/快 预设（scrcpy 无原生灵敏度，用 DPI 近似）
- 按钮：**刷新设备**、**启动 DeX**、**停止**
- 两种启动方式：
  1. **新建虚拟显示**（推荐 One UI 8+）：`scrcpy --new-display=宽x高/DPI` + 所选键盘/鼠标标志
  2. **附着已有 DeX 显示**：`adb shell dumpsys display` 解析显示 ID 后使用 `scrcpy --display-id=N`

## 键盘 / 鼠标说明与回退

| 模式 | 适用 | 说明 |
| --- | --- | --- |
| **uhid**（推荐） | USB 与无线 | 模拟实体 HID；全字符与 IME；Windows 上通常最稳。首次请在 scrcpy 窗口按 **MOD+k**（默认 MOD 为 Alt）配置实体键盘布局 |
| **aoa** | 仅 USB | AOAv2；Windows 上**镜像时可能失败**（adb 已占用 USB），可改回 uhid 或 sdk |
| **sdk** | 通用兼容 | 注入 Android 按键；可勾选「原始按键」(`--raw-key-events`) 或「优先文本」(`--prefer-text`) |

scrcpy **没有**独立的鼠标灵敏度参数。本程序用 DPI 预设近似：灵敏度越高 → DPI 越低（界面更大、相同位移覆盖更少内容）。也可在 Windows「设置 → 蓝牙和其他设备 → 鼠标」中调节指针速度；UHID 下设备侧指针速度也会影响手感。建议 scrcpy **≥ 3.0**，键盘/鼠标 UHID 体验以 **5.x** 更佳（含 UHID 按键修复）。

## 系统要求

1. Windows 10/11 x64
2. 支持 Samsung DeX 的三星手机 / 平板
   - **One UI 8 及以上**：可用「新建虚拟显示」直接出 DeX 桌面
   - 更早系统：请先在手机上开启 DeX / 外接显示，再选「附着已有 DeX 显示」
3. 手机开启 **开发者选项 → USB 调试**；无线调试请用界面配对/连接，或自行 `adb pair` / `adb connect`
4. 本机已准备好官方 **scrcpy ≥ 3.0**（需支持 `--new-display` / `--display-id` / `--mouse=uhid` / `--keyboard=uhid`），解压后记住 `scrcpy.exe` 所在文件夹

本发布包 **不捆绑** scrcpy 与 adb，请自行从官方渠道获取。

## 安装步骤

1. 从 [Releases](https://github.com/hnsjnd/CnDeX/releases) 下载 `CnDeX-win-x64.zip` 并解压
2. 另行下载官方 scrcpy Windows 包并解压（例如 `scrcpy-win64-v3.x.x` / `v5.x`）
3. 用 USB 连接手机，首次请在手机上点「允许 USB 调试」；或使用界面「无线调试」配对并连接
4. 运行解压目录中的 **`CnDeX.exe`**
5. 在界面中选择 `scrcpy.exe`（若与 `adb.exe` 同目录，adb 会自动识别）
6. 点 **刷新设备**，确认列表中出现手机
7. 设置分辨率 / 码率 / 帧率 / 键盘鼠标模式（默认 `1920x1080`、`240` DPI、`8M`、`60`、uhid）
8. 点 **启动 DeX**；结束时点 **停止** 或关闭 scrcpy 窗口

## 常见问题

| 现象 | 处理 |
| --- | --- |
| 无设备 | 检查数据线/驱动、是否已授权调试；无线请先配对再连接，或使用界面「连接」 |
| 键盘在 DeX 中无效 | 确认使用 uhid；在 scrcpy 窗口按 MOD+k 配置布局；uhid 失败则试 sdk + 原始按键，USB 可试 aoa |
| 鼠标过快/过慢 | 拖动灵敏度或点慢/标准/快（改 DPI）；并检查 Windows 鼠标指针速度 |
| 新建虚拟显示空白或非 DeX | 确认系统为 One UI 8+；可尝试调整 DPI（如 150 / 240 / 284） |
| 附着模式找不到显示 | 先在手机上进入 DeX，再刷新并启动；或改用新建虚拟显示 |
| 找不到 adb | 把官方 scrcpy 包里的 `adb.exe` 放在 `scrcpy.exe` 同目录，或在界面中指定 |

## 自行编译

```bash
# 需安装 .NET 8 SDK，可在 Linux/Windows 交叉发布 win-x64
dotnet publish src/CnDeXClient/CnDeXClient.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableWindowsTargeting=true -o publish/win-x64
```

## 版本

- **v1.1.0**：无线配对/连接 UI；键盘模式（uhid/aoa/sdk + SDK 选项）；鼠标模式与灵敏度（DPI 近似）
- **v1.0.0**：初版启动 DeX / 附着显示

## 许可证

MIT。Samsung / DeX / Galaxy / One UI 为三星商标；scrcpy 为 Genymobile 开源项目。本仓库与上述公司无任何关联。
