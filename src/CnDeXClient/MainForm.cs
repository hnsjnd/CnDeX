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

    private Process? _scrcpyProcess;
    private bool _busy;

    public MainForm()
    {
        _settings = AppSettings.Load();
        Text = "CnDeX — 三星 DeX 简易客户端";
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 520);
        ClientSize = new Size(640, 560);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        BuildUi();
        LoadSettingsToUi();
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
            RowCount = 6,
            Padding = new Padding(12),
            AutoSize = false
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Title
        var title = new Label
        {
            Text = "通过 scrcpy + adb 启动三星 DeX 虚拟桌面（简体中文原版界面）",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = Color.FromArgb(30, 30, 30)
        };
        root.Controls.Add(title, 0, 0);

        // Paths
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
        root.Controls.Add(pathGroup, 0, 1);

        // Device + params
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
        root.Controls.Add(mid, 0, 2);

        // Buttons
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
        root.Controls.Add(btnPanel, 0, 3);

        // Log
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
        root.Controls.Add(logGroup, 0, 4);

        var footer = new Label
        {
            Text = "需要：三星 DeX 机型 + 官方 scrcpy（建议 ≥ 3.0）+ USB/无线调试。本程序不附带 scrcpy/adb。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 0)
        };
        root.Controls.Add(footer, 0, 5);

        Controls.Add(root);
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
    }

    private async Task RefreshDevicesAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            SaveUiToSettings();
            var adb = AdbService.ResolveAdbPath(_txtAdb.Text.Trim(), _txtScrcpy.Text.Trim());
            if (adb == null)
            {
                Log("未找到 adb。请填写路径，或把 adb.exe 放在 scrcpy 同目录。");
                SetStatus("未找到 adb");
                _cmbDevices.DataSource = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtAdb.Text))
                _txtAdb.Text = adb;

            Log($"正在检测设备… ({adb})");
            var devices = await AdbService.ListDevicesAsync(adb);
            var ready = devices.Where(d => d.State.Equals("device", StringComparison.OrdinalIgnoreCase)).ToList();
            _cmbDevices.DataSource = null;
            _cmbDevices.DisplayMember = nameof(AdbDevice.ToString);
            _cmbDevices.DataSource = ready;

            if (ready.Count == 0)
            {
                Log("未发现已授权设备。请连接 USB 或确保无线调试已连接，并在手机上允许调试。");
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
        var bitrate = string.IsNullOrWhiteSpace(_txtBitrate.Text) ? "8M" : _txtBitrate.Text.Trim();
        var fps = string.IsNullOrWhiteSpace(_txtFps.Text) ? "60" : _txtFps.Text.Trim();
        var useNewDisplay = _cmbMode.SelectedIndex == 0;

        SetBusy(true);
        try
        {
            Environment.SetEnvironmentVariable("ADB", adb);

            string args;
            if (useNewDisplay)
            {
                // One UI 8+: DeX can run on scrcpy virtual display
                args = $"-s \"{device.Serial}\" --new-display={resolution}/{dpi} " +
                       $"--video-bit-rate={bitrate} --max-fps={fps} " +
                       "--mouse=uhid --keyboard=uhid --window-title=\"CnDeX\"";
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
                       "--mouse=uhid --keyboard=uhid --window-title=\"CnDeX\"";
            }

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
            // Keep busy=false only if process not running; Exited handler manages otherwise
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited)
                SetBusy(false);
            else
            {
                _btnRefresh.Enabled = true;
                _btnBrowseScrcpy.Enabled = true;
                _btnBrowseAdb.Enabled = true;
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
