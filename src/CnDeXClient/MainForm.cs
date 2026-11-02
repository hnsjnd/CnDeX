using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CnDeXClient;

public sealed class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly ComboBox _cmbDevices = new();
    private readonly TextBox _txtScrcpy = new();
    private readonly TextBox _txtAdb = new();
    private readonly TextBox _txtResolution = new();
    private readonly TextBox _txtDpi = new();
    private readonly TextBox _txtBitrate = new();
    private readonly TextBox _txtFps = new();
    private readonly ComboBox _cmbMode = new();
    private readonly TextBox _txtLog = new();
    private readonly Button _btnRefresh = new();
    private readonly Button _btnBrowseScrcpy = new();
    private readonly Button _btnBrowseAdb = new();
    private readonly Button _btnStart = new();
    private readonly Button _btnStop = new();
    private readonly Label _lblStatus = new();

    // Wireless
    private readonly TextBox _txtWifiHost = new();
    private readonly TextBox _txtWifiPort = new();
    private readonly TextBox _txtPairPort = new();
    private readonly TextBox _txtPairCode = new();
    private readonly Button _btnPair = new();
    private readonly Button _btnConnect = new();
    private readonly Button _btnDisconnect = new();

    // Keyboard / mouse
    private readonly ComboBox _cmbKeyboard = new();
    private readonly ComboBox _cmbMouse = new();
    private readonly CheckBox _chkRawKeys = new();
    private readonly CheckBox _chkPreferText = new();
    private readonly CheckBox _chkNoKeyRepeat = new();
    private readonly TrackBar _trkSensitivity = new();
    private readonly Label _lblSensitivity = new();
    private readonly CheckBox _chkApplySensToDpi = new();
    private readonly Button _btnDpiSlow = new();
    private readonly Button _btnDpiNormal = new();
    private readonly Button _btnDpiFast = new();

    private Process? _scrcpyProcess;
    private bool _busy;

    public MainForm()
    {
        _settings = AppSettings.Load();
        Text = "CnDeX — 三星 DeX 简易客户端";
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 700);
        ClientSize = new Size(700, 780);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        AutoScroll = true;

        BuildUi();
        LoadSettingsToUi();
        UpdateKeyboardOptionEnabled();
        FormClosing += OnFormClosing;
        Shown += async (_, _) =>
        {
            AutoFindToolsIfEmpty();
            await RefreshDevicesAsync();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(12),
            AutoSize = false
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // title
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // paths
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // wireless
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // device
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // input
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // log
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // footer

        var title = new Label
        {
            Text = "通过 scrcpy + adb 启动三星 DeX 虚拟桌面（简体中文原版界面）",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = Color.FromArgb(30, 30, 30)
        };
        root.Controls.Add(title, 0, 0);

        root.Controls.Add(BuildPathGroup(), 0, 1);
        root.Controls.Add(BuildWirelessGroup(), 0, 2);
        root.Controls.Add(BuildDeviceGroup(), 0, 3);
        root.Controls.Add(BuildInputGroup(), 0, 4);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        _btnStart.Text = "启动 DeX";
        _btnStart.Width = 120;
        _btnStart.Height = 36;
        _btnStart.Click += async (_, _) => await StartDexAsync();
        _btnStop.Text = "停止";
        _btnStop.Width = 100;
        _btnStop.Height = 36;
        _btnStop.Enabled = false;
        _btnStop.Click += (_, _) => StopDex();
        btnPanel.Controls.Add(_btnStart);
        btnPanel.Controls.Add(_btnStop);
        _lblStatus.Text = "就绪";
        _lblStatus.AutoSize = true;
        _lblStatus.Margin = new Padding(16, 10, 0, 0);
        btnPanel.Controls.Add(_lblStatus);
        root.Controls.Add(btnPanel, 0, 5);

        var logGroup = new GroupBox
        {
            Text = "日志",
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        _txtLog.Multiline = true;
        _txtLog.ReadOnly = true;
        _txtLog.ScrollBars = ScrollBars.Vertical;
        _txtLog.Dock = DockStyle.Fill;
        _txtLog.Font = new Font("Consolas", 9F);
        logGroup.Controls.Add(_txtLog);
        root.Controls.Add(logGroup, 0, 6);

        var footer = new Label
        {
            Text = "需要：三星 DeX 机型 + 官方 scrcpy（建议 ≥ 3.0，键盘/鼠标 uhid 推荐 5.x）+ USB/无线调试。本程序不附带 scrcpy/adb。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 0)
        };
        root.Controls.Add(footer, 0, 7);

        Controls.Add(root);
    }

    private Control BuildPathGroup()
    {
        var pathGroup = new GroupBox
        {
            Text = "工具路径",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8)
        };
        var pathTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 2,
            AutoSize = true
        };
        pathTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        pathTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        pathTable.Controls.Add(new Label { Text = "scrcpy.exe", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _txtScrcpy.Dock = DockStyle.Fill;
        pathTable.Controls.Add(_txtScrcpy, 1, 0);
        _btnBrowseScrcpy.Text = "浏览…";
        _btnBrowseScrcpy.Dock = DockStyle.Fill;
        _btnBrowseScrcpy.Click += (_, _) => BrowseFile(_txtScrcpy, "scrcpy.exe|scrcpy.exe|所有文件|*.*");
        pathTable.Controls.Add(_btnBrowseScrcpy, 2, 0);

        pathTable.Controls.Add(new Label { Text = "adb（可选）", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _txtAdb.Dock = DockStyle.Fill;
        pathTable.Controls.Add(_txtAdb, 1, 1);
        _btnBrowseAdb.Text = "浏览…";
        _btnBrowseAdb.Dock = DockStyle.Fill;
        _btnBrowseAdb.Click += (_, _) => BrowseFile(_txtAdb, "adb.exe|adb.exe|所有文件|*.*");
        pathTable.Controls.Add(_btnBrowseAdb, 2, 1);

        var hint = new Label
        {
            Text = "提示：若未填 adb，将自动使用 scrcpy 同目录下的 adb.exe，或系统 PATH 中的 adb。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 0)
        };
        var pathStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };
        pathStack.Controls.Add(pathTable);
        pathStack.Controls.Add(hint);
        pathGroup.Controls.Add(pathStack);
        return pathGroup;
    }

    private Control BuildWirelessGroup()
    {
        var group = new GroupBox
        {
            Text = "无线调试（adb pair / connect）",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8)
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 6,
            RowCount = 2,
            AutoSize = true
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        table.Controls.Add(new Label { Text = "手机 IP", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _txtWifiHost.Dock = DockStyle.Fill;
        _txtWifiHost.PlaceholderText = "例如 192.168.1.8";
        table.Controls.Add(_txtWifiHost, 1, 0);
        table.Controls.Add(new Label { Text = "连接端口", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _txtWifiPort.Dock = DockStyle.Fill;
        _txtWifiPort.PlaceholderText = "5555";
        table.Controls.Add(_txtWifiPort, 3, 0);
        table.Controls.Add(new Label { Text = "配对端口", AutoSize = true, Anchor = AnchorStyles.Left }, 4, 0);
        _txtPairPort.Dock = DockStyle.Fill;
        _txtPairPort.PlaceholderText = "无线调试页显示";
        table.Controls.Add(_txtPairPort, 5, 0);

        table.Controls.Add(new Label { Text = "配对码", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _txtPairCode.Dock = DockStyle.Fill;
        _txtPairCode.PlaceholderText = "六位数字";
        table.SetColumnSpan(_txtPairCode, 2);
        table.Controls.Add(_txtPairCode, 1, 1);

        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false
        };
        _btnPair.Text = "配对";
        _btnPair.Width = 72;
        _btnPair.Click += async (_, _) => await WirelessPairAsync();
        _btnConnect.Text = "连接";
        _btnConnect.Width = 72;
        _btnConnect.Click += async (_, _) => await WirelessConnectAsync();
        _btnDisconnect.Text = "断开无线";
        _btnDisconnect.Width = 88;
        _btnDisconnect.Click += async (_, _) => await WirelessDisconnectAsync();
        btns.Controls.Add(_btnPair);
        btns.Controls.Add(_btnConnect);
        btns.Controls.Add(_btnDisconnect);
        table.SetColumnSpan(btns, 3);
        table.Controls.Add(btns, 3, 1);

        var hint = new Label
        {
            Text = "步骤：开发者选项 → 无线调试 → 使用配对码配对（填 IP+配对端口+配对码）→ 再填连接端口点「连接」→ 刷新设备。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 0),
            MaximumSize = new Size(660, 0)
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };
        stack.Controls.Add(table);
        stack.Controls.Add(hint);
        group.Controls.Add(stack);
        return group;
    }

    private Control BuildDeviceGroup()
    {
        var mid = new GroupBox
        {
            Text = "设备与参数",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8)
        };
        var midTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            RowCount = 3,
            AutoSize = true
        };
        midTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        midTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        midTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        midTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        midTable.Controls.Add(new Label { Text = "已连接设备", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _cmbDevices.Dock = DockStyle.Fill;
        _cmbDevices.DropDownStyle = ComboBoxStyle.DropDownList;
        midTable.SetColumnSpan(_cmbDevices, 2);
        midTable.Controls.Add(_cmbDevices, 1, 0);
        _btnRefresh.Text = "刷新设备";
        _btnRefresh.Dock = DockStyle.Fill;
        _btnRefresh.Click += async (_, _) => await RefreshDevicesAsync();
        midTable.Controls.Add(_btnRefresh, 3, 0);

        midTable.Controls.Add(new Label { Text = "分辨率", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _txtResolution.Dock = DockStyle.Fill;
        midTable.Controls.Add(_txtResolution, 1, 1);
        midTable.Controls.Add(new Label { Text = "DPI", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
        _txtDpi.Dock = DockStyle.Fill;
        midTable.Controls.Add(_txtDpi, 3, 1);

        midTable.Controls.Add(new Label { Text = "码率", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _txtBitrate.Dock = DockStyle.Fill;
        midTable.Controls.Add(_txtBitrate, 1, 2);
        midTable.Controls.Add(new Label { Text = "帧率 FPS", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 2);
        _txtFps.Dock = DockStyle.Fill;
        midTable.Controls.Add(_txtFps, 3, 2);

        var modeRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        modeRow.Controls.Add(new Label { Text = "启动方式", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbMode.Dock = DockStyle.Fill;
        _cmbMode.Items.AddRange(new object[]
        {
            "新建虚拟显示（推荐 One UI 8+：scrcpy --new-display）",
            "附着已有 DeX 显示（dumpsys 找 display-id）"
        });
        modeRow.Controls.Add(_cmbMode, 1, 0);

        var midStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };
        midStack.Controls.Add(midTable);
        midStack.Controls.Add(modeRow);
        mid.Controls.Add(midStack);
        return mid;
    }

    private Control BuildInputGroup()
    {
        var group = new GroupBox
        {
            Text = "键盘与鼠标（DeX 输入）",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8)
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            RowCount = 3,
            AutoSize = true
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        table.Controls.Add(new Label { Text = "键盘模式", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _cmbKeyboard.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbKeyboard.Dock = DockStyle.Fill;
        _cmbKeyboard.Items.AddRange(new object[]
        {
            "uhid（推荐，无线/USB，全字符）",
            "aoa（仅 USB；Windows 镜像时可能失败）",
            "sdk（兼容模式；可开原始按键）"
        });
        _cmbKeyboard.SelectedIndexChanged += (_, _) => UpdateKeyboardOptionEnabled();
        table.Controls.Add(_cmbKeyboard, 1, 0);

        table.Controls.Add(new Label { Text = "鼠标模式", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _cmbMouse.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbMouse.Dock = DockStyle.Fill;
        _cmbMouse.Items.AddRange(new object[]
        {
            "uhid（推荐）",
            "aoa（仅 USB）",
            "sdk（绝对坐标）"
        });
        table.Controls.Add(_cmbMouse, 3, 0);

        var sdkOpts = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true
        };
        _chkRawKeys.Text = "原始按键 (--raw-key-events)";
        _chkRawKeys.AutoSize = true;
        _chkPreferText.Text = "优先文本 (--prefer-text)";
        _chkPreferText.AutoSize = true;
        _chkNoKeyRepeat.Text = "禁用按键重复 (--no-key-repeat)";
        _chkNoKeyRepeat.AutoSize = true;
        sdkOpts.Controls.Add(_chkRawKeys);
        sdkOpts.Controls.Add(_chkPreferText);
        sdkOpts.Controls.Add(_chkNoKeyRepeat);
        table.SetColumnSpan(sdkOpts, 3);
        table.Controls.Add(new Label { Text = "SDK 选项", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        table.Controls.Add(sdkOpts, 1, 1);

        table.Controls.Add(new Label { Text = "鼠标灵敏度", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        var sensPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true
        };
        _trkSensitivity.Minimum = 1;
        _trkSensitivity.Maximum = 10;
        _trkSensitivity.TickFrequency = 1;
        _trkSensitivity.Width = 160;
        _trkSensitivity.Height = 36;
        _trkSensitivity.ValueChanged += (_, _) =>
        {
            _lblSensitivity.Text = $"{_trkSensitivity.Value}/10";
            if (_chkApplySensToDpi.Checked)
                ApplySensitivityToDpiField();
        };
        _lblSensitivity.AutoSize = true;
        _lblSensitivity.Margin = new Padding(4, 10, 8, 0);
        _chkApplySensToDpi.Text = "启动时用灵敏度覆盖 DPI";
        _chkApplySensToDpi.AutoSize = true;
        _chkApplySensToDpi.Checked = true;
        _chkApplySensToDpi.Margin = new Padding(4, 8, 8, 0);
        _btnDpiSlow.Text = "慢";
        _btnDpiSlow.Width = 40;
        _btnDpiSlow.Click += (_, _) => { _trkSensitivity.Value = 2; ApplySensitivityToDpiField(); };
        _btnDpiNormal.Text = "标准";
        _btnDpiNormal.Width = 48;
        _btnDpiNormal.Click += (_, _) => { _trkSensitivity.Value = 5; ApplySensitivityToDpiField(); };
        _btnDpiFast.Text = "快";
        _btnDpiFast.Width = 40;
        _btnDpiFast.Click += (_, _) => { _trkSensitivity.Value = 8; ApplySensitivityToDpiField(); };
        sensPanel.Controls.Add(_trkSensitivity);
        sensPanel.Controls.Add(_lblSensitivity);
        sensPanel.Controls.Add(_chkApplySensToDpi);
        sensPanel.Controls.Add(_btnDpiSlow);
        sensPanel.Controls.Add(_btnDpiNormal);
        sensPanel.Controls.Add(_btnDpiFast);
        table.SetColumnSpan(sensPanel, 3);
        table.Controls.Add(sensPanel, 1, 2);

        var hint = new Label
        {
            Text = "说明：scrcpy 无独立鼠标灵敏度参数。默认 --mouse=uhid / --keyboard=uhid；灵敏度通过 DPI 预设近似。" +
                   "UHID 失败时可改 aoa（USB）或 sdk。首次 UHID 请在 scrcpy 窗口按 MOD+k 配置实体键盘布局。" +
                   "Windows 指针速度也可在「设置 → 蓝牙和其他设备 → 鼠标」中调节。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 0),
            MaximumSize = new Size(660, 0)
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };
        stack.Controls.Add(table);
        stack.Controls.Add(hint);
        group.Controls.Add(stack);
        return group;
    }

    private void UpdateKeyboardOptionEnabled()
    {
        var sdk = _cmbKeyboard.SelectedIndex == 2;
        _chkRawKeys.Enabled = sdk;
        _chkPreferText.Enabled = sdk;
        // no-key-repeat applies to all modes in practice for games; keep always enabled
        _chkNoKeyRepeat.Enabled = true;
    }

    private void ApplySensitivityToDpiField()
    {
        int? baseDpi = null;
        if (int.TryParse(_txtDpi.Text.Trim(), out var parsed) && parsed > 0)
            baseDpi = parsed;
        // When applying from presets, use absolute table (ignore current as base once)
        var dpi = AdbService.SensitivityToDpi(_trkSensitivity.Value, baseDpi: null);
        _txtDpi.Text = dpi.ToString();
    }

    private void LoadSettingsToUi()
    {
        _txtScrcpy.Text = _settings.ScrcpyPath;
        _txtAdb.Text = _settings.AdbPath;
        _txtResolution.Text = string.IsNullOrWhiteSpace(_settings.Resolution) ? "1920x1080" : _settings.Resolution;
        _txtDpi.Text = string.IsNullOrWhiteSpace(_settings.Dpi) ? "240" : _settings.Dpi;
        _txtBitrate.Text = string.IsNullOrWhiteSpace(_settings.Bitrate) ? "8M" : _settings.Bitrate;
        _txtFps.Text = string.IsNullOrWhiteSpace(_settings.Fps) ? "60" : _settings.Fps;
        _cmbMode.SelectedIndex = string.Equals(_settings.LaunchMode, "display-id", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        _txtWifiHost.Text = _settings.WirelessHost;
        _txtWifiPort.Text = string.IsNullOrWhiteSpace(_settings.WirelessPort) ? "5555" : _settings.WirelessPort;
        _txtPairPort.Text = _settings.PairPort;

        _cmbKeyboard.SelectedIndex = ModeToIndex(_settings.KeyboardMode, "uhid", "aoa", "sdk");
        _cmbMouse.SelectedIndex = ModeToIndex(_settings.MouseMode, "uhid", "aoa", "sdk");
        _chkRawKeys.Checked = _settings.SdkRawKeyEvents;
        _chkPreferText.Checked = _settings.SdkPreferText;
        _chkNoKeyRepeat.Checked = _settings.NoKeyRepeat;
        var sens = Math.Clamp(_settings.MouseSensitivity <= 0 ? 5 : _settings.MouseSensitivity, 1, 10);
        _trkSensitivity.Value = sens;
        _lblSensitivity.Text = $"{sens}/10";
    }

    private static int ModeToIndex(string? mode, params string[] modes)
    {
        if (string.IsNullOrWhiteSpace(mode)) return 0;
        for (var i = 0; i < modes.Length; i++)
        {
            if (string.Equals(mode, modes[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    private static string IndexToMode(int index, params string[] modes)
    {
        if (index < 0 || index >= modes.Length) return modes[0];
        return modes[index];
    }

    private void SaveUiToSettings()
    {
        _settings.ScrcpyPath = _txtScrcpy.Text.Trim();
        _settings.AdbPath = _txtAdb.Text.Trim();
        _settings.Resolution = _txtResolution.Text.Trim();
        _settings.Dpi = _txtDpi.Text.Trim();
        _settings.Bitrate = _txtBitrate.Text.Trim();
        _settings.Fps = _txtFps.Text.Trim();
        _settings.LaunchMode = _cmbMode.SelectedIndex == 1 ? "display-id" : "new-display";
        if (_cmbDevices.SelectedItem is AdbDevice d)
            _settings.LastSerial = d.Serial;

        _settings.WirelessHost = _txtWifiHost.Text.Trim();
        _settings.WirelessPort = _txtWifiPort.Text.Trim();
        _settings.PairPort = _txtPairPort.Text.Trim();

        _settings.KeyboardMode = IndexToMode(_cmbKeyboard.SelectedIndex, "uhid", "aoa", "sdk");
        _settings.MouseMode = IndexToMode(_cmbMouse.SelectedIndex, "uhid", "aoa", "sdk");
        _settings.SdkRawKeyEvents = _chkRawKeys.Checked;
        _settings.SdkPreferText = _chkPreferText.Checked;
        _settings.NoKeyRepeat = _chkNoKeyRepeat.Checked;
        _settings.MouseSensitivity = _trkSensitivity.Value;
        _settings.Save();
    }

    private void AutoFindToolsIfEmpty()
    {
        if (string.IsNullOrWhiteSpace(_txtScrcpy.Text))
        {
            var found = AdbService.ResolveScrcpyPath(null);
            if (found != null)
            {
                _txtScrcpy.Text = found;
                Log($"已自动找到 scrcpy：{found}");
            }
        }

        if (string.IsNullOrWhiteSpace(_txtAdb.Text))
        {
            var found = AdbService.ResolveAdbPath(null, _txtScrcpy.Text.Trim());
            if (found != null)
            {
                _txtAdb.Text = found;
                Log($"已自动找到 adb：{found}");
            }
        }
    }

    private void BrowseFile(TextBox target, string filter)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true
        };
        if (!string.IsNullOrWhiteSpace(target.Text))
        {
            try
            {
                var dir = Path.GetDirectoryName(target.Text);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    dlg.InitialDirectory = dir;
            }
            catch { /* ignore */ }
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
            target.Text = dlg.FileName;
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(message));
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _txtLog.AppendText(line + Environment.NewLine);
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(text));
            return;
        }

        _lblStatus.Text = text;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _btnRefresh.Enabled = !busy;
        _btnStart.Enabled = !busy && (_scrcpyProcess == null || _scrcpyProcess.HasExited);
        _btnStop.Enabled = _scrcpyProcess != null && !_scrcpyProcess.HasExited;
        _btnBrowseScrcpy.Enabled = !busy;
        _btnBrowseAdb.Enabled = !busy;
        _btnPair.Enabled = !busy;
        _btnConnect.Enabled = !busy;
        _btnDisconnect.Enabled = !busy;
    }

    private string? ResolveAdbOrWarn()
    {
        SaveUiToSettings();
        var adb = AdbService.ResolveAdbPath(_txtAdb.Text.Trim(), _txtScrcpy.Text.Trim());
        if (adb == null)
        {
            Log("未找到 adb。请填写路径，或把 adb.exe 放在 scrcpy 同目录。");
            SetStatus("未找到 adb");
            return null;
        }
        if (string.IsNullOrWhiteSpace(_txtAdb.Text))
            _txtAdb.Text = adb;
        return adb;
    }

    private string BuildHostPort(string portField)
    {
        var host = _txtWifiHost.Text.Trim();
        var port = portField.Trim();
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("请填写手机 IP。");
        if (string.IsNullOrWhiteSpace(port))
            throw new InvalidOperationException("请填写端口。");
        if (host.Contains(':') && !host.StartsWith('['))
        {
            // already host:port
            return host;
        }
        return $"{host}:{port}";
    }

    private async Task WirelessPairAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var adb = ResolveAdbOrWarn();
            if (adb == null) return;

            var hostPort = BuildHostPort(_txtPairPort.Text);
            var code = _txtPairCode.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(this, "请填写无线调试配对码。", "CnDeX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Log($"配对中：adb pair {hostPort} …");
            var (ok, output) = await AdbService.PairAsync(adb, hostPort, code);
            Log(output);
            if (ok)
            {
                SetStatus("配对成功");
                Log("配对成功。请确认「连接端口」后点击「连接」。");
            }
            else
            {
                SetStatus("配对失败");
                MessageBox.Show(this, "配对失败，请检查 IP、配对端口与配对码。\n\n" + output, "CnDeX",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            Log("配对出错：" + ex.Message);
            SetStatus("配对失败");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task WirelessConnectAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var adb = ResolveAdbOrWarn();
            if (adb == null) return;

            var hostPort = BuildHostPort(_txtWifiPort.Text);
            Log($"连接中：adb connect {hostPort} …");
            var (ok, output) = await AdbService.ConnectAsync(adb, hostPort);
            Log(output);
            if (ok)
            {
                SetStatus("已无线连接");
                await RefreshDevicesAsync();
            }
            else
            {
                SetStatus("连接失败");
                MessageBox.Show(this, "连接失败。Android 11+ 需先配对。\n\n" + output, "CnDeX",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            Log("连接出错：" + ex.Message);
            SetStatus("连接失败");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task WirelessDisconnectAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var adb = ResolveAdbOrWarn();
            if (adb == null) return;

            string? target = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(_txtWifiHost.Text) && !string.IsNullOrWhiteSpace(_txtWifiPort.Text))
                    target = BuildHostPort(_txtWifiPort.Text);
            }
            catch { /* disconnect all */ }

            Log(target == null ? "断开所有无线连接…" : $"断开 {target} …");
            var output = await AdbService.DisconnectAsync(adb, target);
            if (!string.IsNullOrWhiteSpace(output))
                Log(output);
            await RefreshDevicesAsync();
            SetStatus("已断开无线");
        }
        catch (Exception ex)
        {
            Log("断开出错：" + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshDevicesAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var adb = ResolveAdbOrWarn();
            if (adb == null)
            {
                _cmbDevices.DataSource = null;
                return;
            }

            Log($"正在检测设备… ({adb})");
            var devices = await AdbService.ListDevicesAsync(adb);
            var ready = devices.Where(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase)).ToList();
            _cmbDevices.DataSource = null;
            _cmbDevices.DisplayMember = nameof(AdbDevice.ToString);
            _cmbDevices.DataSource = ready;

            if (ready.Count == 0)
            {
                Log("未发现已授权设备。请连接 USB，或使用上方无线配对/连接，并在手机上允许调试。");
                SetStatus("无设备");
            }
            else
            {
                Log($"发现 {ready.Count} 台设备。");
                var last = _settings.LastSerial;
                if (!string.IsNullOrEmpty(last))
                {
                    var idx = ready.FindIndex(d => d.Serial == last);
                    if (idx >= 0) _cmbDevices.SelectedIndex = idx;
                }
                SetStatus($"已连接 {ready.Count} 台");
            }
        }
        catch (Exception ex)
        {
            Log("刷新失败：" + ex.Message);
            SetStatus("刷新失败");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string BuildInputFlags()
    {
        var kb = IndexToMode(_cmbKeyboard.SelectedIndex, "uhid", "aoa", "sdk");
        var mouse = IndexToMode(_cmbMouse.SelectedIndex, "uhid", "aoa", "sdk");
        var parts = new List<string>
        {
            $"--keyboard={kb}",
            $"--mouse={mouse}"
        };

        if (kb == "sdk")
        {
            if (_chkRawKeys.Checked)
                parts.Add("--raw-key-events");
            if (_chkPreferText.Checked)
                parts.Add("--prefer-text");
        }

        if (_chkNoKeyRepeat.Checked)
            parts.Add("--no-key-repeat");

        return string.Join(" ", parts);
    }

    private async Task StartDexAsync()
    {
        if (_busy) return;
        if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
        {
            Log("已有会话在运行，请先停止。");
            return;
        }

        SaveUiToSettings();
        var scrcpy = AdbService.ResolveScrcpyPath(_txtScrcpy.Text.Trim());
        if (scrcpy == null)
        {
            MessageBox.Show(this, "请先选择 scrcpy.exe 路径。", "CnDeX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _txtScrcpy.Text = scrcpy;
        var adb = AdbService.ResolveAdbPath(_txtAdb.Text.Trim(), scrcpy);
        if (adb == null)
        {
            MessageBox.Show(this, "未找到 adb.exe。", "CnDeX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _txtAdb.Text = adb;

        if (_cmbDevices.SelectedItem is not AdbDevice device)
        {
            await RefreshDevicesAsync();
            if (_cmbDevices.SelectedItem is not AdbDevice d2)
            {
                MessageBox.Show(this, "没有可用设备。", "CnDeX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            device = d2;
        }

        var resolution = string.IsNullOrWhiteSpace(_txtResolution.Text) ? "1920x1080" : _txtResolution.Text.Trim();
        var dpi = string.IsNullOrWhiteSpace(_txtDpi.Text) ? "240" : _txtDpi.Text.Trim();
        if (_chkApplySensToDpi.Checked)
        {
            dpi = AdbService.SensitivityToDpi(_trkSensitivity.Value, baseDpi: null).ToString();
            _txtDpi.Text = dpi;
            Log($"灵敏度 {_trkSensitivity.Value}/10 → DPI {dpi}（scrcpy 无原生灵敏度，用 DPI 近似）");
        }

        var bitrate = string.IsNullOrWhiteSpace(_txtBitrate.Text) ? "8M" : _txtBitrate.Text.Trim();
        var fps = string.IsNullOrWhiteSpace(_txtFps.Text) ? "60" : _txtFps.Text.Trim();
        var useNewDisplay = _cmbMode.SelectedIndex == 0;
        var inputFlags = BuildInputFlags();

        SetBusy(true);
        try
        {
            Environment.SetEnvironmentVariable("ADB", adb);

            string args;
            if (useNewDisplay)
            {
                args = $"-s \"{device.Serial}\" --new-display={resolution}/{dpi} " +
                       $"--video-bit-rate={bitrate} --max-fps={fps} " +
                       $"{inputFlags} --window-title=\"CnDeX\"";
                Log("启动方式：新建虚拟显示（--new-display）");
            }
            else
            {
                Log("正在解析已有显示（dumpsys display）…");
                var displays = await AdbService.ListDisplaysAsync(adb, device.Serial);
                foreach (var disp in displays)
                    Log($"  显示 {disp.Id}: {disp.Name} {disp.UniqueId}");

                var id = AdbService.FindDexDisplayId(displays);
                if (id == null)
                {
                    Log("未找到非主屏显示。请先在手机上开启 DeX / 外接显示，或改用「新建虚拟显示」。");
                    MessageBox.Show(this,
                        "未找到 DeX/虚拟显示。\n请先开启 DeX，或切换到「新建虚拟显示」模式（One UI 8+）。",
                        "CnDeX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Log($"将附着显示 ID = {id}");
                args = $"-s \"{device.Serial}\" --display-id={id} " +
                       $"--video-bit-rate={bitrate} --max-fps={fps} " +
                       $"{inputFlags} --window-title=\"CnDeX\"";
            }

            Log($"输入：{inputFlags}");
            if (IndexToMode(_cmbKeyboard.SelectedIndex, "uhid", "aoa", "sdk") == "uhid")
                Log("提示：UHID 键盘首次使用请在 scrcpy 窗口按 MOD+k（默认 MOD 为 Alt）打开实体键盘布局设置。");
            if (IndexToMode(_cmbKeyboard.SelectedIndex, "uhid", "aoa", "sdk") == "aoa")
                Log("提示：AOA 在 Windows 上镜像时可能失败，可改回 uhid 或 sdk。");

            Log($"命令：{scrcpy} {args}");
            var psi = new ProcessStartInfo
            {
                FileName = scrcpy,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scrcpy) ?? Environment.CurrentDirectory
            };
            psi.Environment["ADB"] = adb;

            _scrcpyProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _scrcpyProcess.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Log(e.Data); };
            _scrcpyProcess.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Log(e.Data); };
            _scrcpyProcess.Exited += (_, _) =>
            {
                BeginInvoke(() =>
                {
                    Log($"scrcpy 已退出（代码 {_scrcpyProcess?.ExitCode}）");
                    SetStatus("已停止");
                    _scrcpyProcess = null;
                    SetBusy(false);
                });
            };

            if (!_scrcpyProcess.Start())
                throw new InvalidOperationException("无法启动 scrcpy");

            _scrcpyProcess.BeginOutputReadLine();
            _scrcpyProcess.BeginErrorReadLine();
            SetStatus("DeX 会话运行中");
            Log("已启动。关闭 scrcpy 窗口或点「停止」可结束。");
            _btnStop.Enabled = true;
            _btnStart.Enabled = false;
        }
        catch (Exception ex)
        {
            Log("启动失败：" + ex.Message);
            SetStatus("启动失败");
            MessageBox.Show(this, "启动失败：" + ex.Message, "CnDeX", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetBusy(false);
        }
        finally
        {
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited)
                SetBusy(false);
            else
            {
                _btnRefresh.Enabled = true;
                _btnBrowseScrcpy.Enabled = true;
                _btnBrowseAdb.Enabled = true;
                _btnPair.Enabled = true;
                _btnConnect.Enabled = true;
                _btnDisconnect.Enabled = true;
                _busy = false;
            }
        }
    }

    private void StopDex()
    {
        try
        {
            if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
            {
                Log("正在停止 scrcpy…");
                _scrcpyProcess.Kill(entireProcessTree: true);
                _scrcpyProcess.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            Log("停止时出错：" + ex.Message);
        }
        finally
        {
            _scrcpyProcess = null;
            SetStatus("已停止");
            SetBusy(false);
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        SaveUiToSettings();
        StopDex();
    }
}
