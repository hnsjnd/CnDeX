# CnDeX — 三星 DeX 简易中文客户端

独立编写的 **简体中文 Windows 图形界面**，通过本机已安装的 [scrcpy](https://github.com/Genymobile/scrcpy) + `adb` 启动 / 附着三星 DeX 虚拟桌面。

> 本项目为原创简洁界面，**不是** ScrcpyDex / DX-Manager 的分支或汉化包，也不包含其专有服务端。

## 功能

- 检测已通过 USB 或无线调试连接的 `adb` 设备
- 配置 `scrcpy.exe` 路径；`adb` 可选手填，默认自动在 scrcpy 同目录或 PATH 中查找
- 参数：分辨率、DPI、码率、帧率
- 按钮：**刷新设备**、**启动 DeX**、**停止**
- 两种启动方式：
  1. **新建虚拟显示**（推荐 One UI 8+）：`scrcpy --new-display=宽x高/DPI --mouse=uhid --keyboard=uhid`
  2. **附着已有 DeX 显示**：`adb shell dumpsys display` 解析显示 ID 后使用 `scrcpy --display-id=N`

## 系统要求

1. Windows 10/11 x64
2. 支持 Samsung DeX 的三星手机 / 平板
   - **One UI 8 及以上**：可用「新建虚拟显示」直接出 DeX 桌面
   - 更早系统：请先在手机上开启 DeX / 外接显示，再选「附着已有 DeX 显示」
3. 手机开启 **开发者选项 → USB 调试**（无线调试亦可，需已 `adb connect`）
4. 本机已准备好官方 **scrcpy ≥ 3.0**（需支持 `--new-display` / `--display-id` / `--mouse=uhid`），解压后记住 `scrcpy.exe` 所在文件夹

本发布包 **不捆绑** scrcpy 与 adb，请自行从官方渠道获取。

## 安装步骤

1. 从 [Releases](https://github.com/hnsjnd/CnDeX/releases) 下载 `CnDeX-win-x64.zip` 并解压
2. 另行下载官方 scrcpy Windows 包并解压（例如 `scrcpy-win64-v3.x.x`）
3. 用 USB 连接手机，首次请在手机上点「允许 USB 调试」
4. 运行解压目录中的 **`CnDeX.exe`**
5. 在界面中选择 `scrcpy.exe`（若与 `adb.exe` 同目录，adb 会自动识别）
6. 点 **刷新设备**，确认列表中出现手机
7. 设置分辨率 / 码率 / 帧率（默认 `1920x1080`、`240` DPI、`8M`、`60`）
8. 点 **启动 DeX**；结束时点 **停止** 或关闭 scrcpy 窗口

## 常见问题

| 现象 | 处理 |
| --- | --- |
| 无设备 | 检查数据线/驱动、是否已授权调试；无线请先 `adb connect IP:端口` |
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

## 许可证

MIT。Samsung / DeX / Galaxy / One UI 为三星商标；scrcpy 为 Genymobile 开源项目。本仓库与上述公司无任何关联。
