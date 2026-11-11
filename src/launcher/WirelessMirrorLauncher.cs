using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WirelessMirror
{
    public class LauncherForm : Form
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint uMilliseconds);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public short dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);
        private const int ENUM_CURRENT_SETTINGS = -1;

        public static int GetNativeRefreshRate()
        {
            try
            {
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) && dm.dmDisplayFrequency > 0)
                {
                    return dm.dmDisplayFrequency;
                }
            }
            catch { }
            return 60;
        }

        private int nativeRefreshRate = 60;

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AirPlay-Windows"
        );
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.ini");

        private string appDir;
        private string coreExePath;

        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private Label lblStatus;
        private Panel pnlStatusIndicator;

        // Presets: Optimized for Max 15,000 kbps (15 Mbps)
        private RadioButton rbPreset720p;
        private RadioButton rbPreset1080p;
        private RadioButton rbPresetSquare;
        private RadioButton rbPresetIphone;
        private RadioButton rbPresetIpad;
        private RadioButton rbPresetUncapped;

        private CheckBox chkSmoothCursor;
        private CheckBox chkHwAccel;
        private CheckBox chkHevc;
        private ComboBox cmbFpsCap;

        private int nativeMonitorW = 1920;
        private int nativeMonitorH = 1080;

        private Button btnLaunch;
        private Button btnStop;
        private Button btnCreateShortcuts;
        private Label lblActiveSettings;
        private Timer statusTimer;

        public LauncherForm()
        {
            // Request 1ms timer precision for jitter-free audio and cursor responsiveness
            TimeBeginPeriod(1);

            nativeRefreshRate = GetNativeRefreshRate();
            if (nativeRefreshRate <= 0) nativeRefreshRate = 60;

            try
            {
                nativeMonitorW = Screen.PrimaryScreen.Bounds.Width;
                nativeMonitorH = Screen.PrimaryScreen.Bounds.Height;
            }
            catch { }

            appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            coreExePath = Path.Combine(appDir, "WirelessMirror.exe");

            InitializeComponents();
            LoadCurrentSettings();
            UpdateStatus();

            statusTimer = new Timer();
            statusTimer.Interval = 1000;
            statusTimer.Tick += (s, e) => UpdateStatus();
            statusTimer.Start();
        }

        private void InitializeComponents()
        {
            this.Text = "Wireless Mirror Control Center - High Stability & Ultra-Low Latency";
            this.Size = new Size(640, 785);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(15, 23, 42);
            this.ForeColor = Color.FromArgb(241, 245, 249);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // Header Banner
            Panel pnlHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(640, 85),
                BackColor = Color.FromArgb(24, 33, 56)
            };
            pnlHeader.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(51, 65, 85), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 84, 640, 84);
                }
            };

            lblHeaderTitle = new Label
            {
                Text = "⚡ Wireless Mirror Control Center",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new Point(20, 15),
                AutoSize = true
            };

            lblHeaderSubtitle = new Label
            {
                Text = "High-Stability AirPlay Receiver • Anti-Bufferbloat • Native Refresh Sync",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(24, 48),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);
            this.Controls.Add(pnlHeader);

            // Device Resolution Presets Group
            GroupBox grpPresets = new GroupBox
            {
                Text = "  📱 Target Stream Bitrate & Resolution Presets  ",
                Location = new Point(20, 95),
                Size = new Size(585, 270),
                ForeColor = Color.FromArgb(56, 189, 248),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            rbPreset1080p = CreatePresetRadio(
                string.Format("⚡ Native Monitor Match ({0} x {1} @ Native {2}Hz) — Max 15,000 kbps [Recommended]", nativeMonitorW, nativeMonitorH, nativeRefreshRate),
                "Pixel-perfect native monitor resolution with synchronized 144Hz VSync display presentation",
                15, 22, true
            );

            rbPreset720p = CreatePresetRadio(
                string.Format("📶 720p Anti-Bufferbloat (1280 x 720 @ Native {0}Hz) — Max 5,000 kbps [Best for 2.4GHz Wi-Fi]", nativeRefreshRate),
                "Ultra-Light 5 Mbps Stream: Eliminates 2.4GHz Wi-Fi packet congestion, ping spikes & audio delay",
                15, 62, false
            );

            rbPresetSquare = CreatePresetRadio(
                string.Format("✨ Universal Auto-Fit Square (1440 x 1440 @ Native {0}Hz) — Max 15,000 kbps", nativeRefreshRate),
                "Dynamic 1:1 Auto-Rotation: Pixel-perfect portrait & landscape display safely under 15 Mbps",
                15, 102, false
            );

            rbPresetIphone = CreatePresetRadio(
                string.Format("📱 iPhone 14 Plus / OLED Fit (1920 x 888 @ Native {0}Hz) — Max 14,000 kbps", nativeRefreshRate),
                "Full 19.53:9 unscaled OLED Retina density without network jitter or screen freeze",
                15, 142, false
            );

            rbPresetIpad = CreatePresetRadio(
                string.Format("💻 iPad Air / Pro Fit (1600 x 1200 @ Native {0}Hz) — Max 15,000 kbps", nativeRefreshRate),
                "Crisp 4:3 display ratio with zero frame latency and synchronized RAOP audio",
                15, 182, false
            );

            rbPresetUncapped = CreatePresetRadio(
                string.Format("⚠️ Uncapped Retina (2778 x 2778 @ Native {0}Hz) — Up to 30,000 kbps", nativeRefreshRate),
                "Heavy Wi-Fi stream: May cause packet queue congestion, audio drops & screen freeze on busy Wi-Fi",
                15, 222, false
            );

            rbPreset1080p.CheckedChanged += PresetChanged;
            rbPreset720p.CheckedChanged += PresetChanged;
            rbPresetSquare.CheckedChanged += PresetChanged;
            rbPresetIphone.CheckedChanged += PresetChanged;
            rbPresetIpad.CheckedChanged += PresetChanged;
            rbPresetUncapped.CheckedChanged += PresetChanged;

            grpPresets.Controls.Add(rbPreset1080p);
            grpPresets.Controls.Add(rbPreset720p);
            grpPresets.Controls.Add(rbPresetSquare);
            grpPresets.Controls.Add(rbPresetIphone);
            grpPresets.Controls.Add(rbPresetIpad);
            grpPresets.Controls.Add(rbPresetUncapped);
            this.Controls.Add(grpPresets);

            // Latency, Hardware & Cursor Optimization Group
            GroupBox grpLatency = new GroupBox
            {
                Text = "  ⚡ Latency, Hardware & Stream FPS Settings  ",
                Location = new Point(20, 375),
                Size = new Size(585, 145),
                ForeColor = Color.FromArgb(56, 189, 248),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            chkSmoothCursor = new CheckBox
            {
                Text = string.Format("🎯 Smooth Hardware Cursor & Native VSync ({0}Hz Monitor Refresh Sync)", nativeRefreshRate),
                Location = new Point(15, 24),
                Size = new Size(560, 22),
                ForeColor = Color.FromArgb(52, 211, 153),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Checked = true
            };
            chkSmoothCursor.CheckedChanged += OptionChanged;

            chkHwAccel = new CheckBox
            {
                Text = "Direct3D 11 GPU Hardware Accelerated Video Decoder (D3D11VA Zero-Copy)",
                Location = new Point(15, 50),
                Size = new Size(560, 20),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Checked = true
            };
            chkHwAccel.CheckedChanged += OptionChanged;

            chkHevc = new CheckBox
            {
                Text = "Enable HEVC / H.265 Stream Encoding (Lower Wi-Fi bandwidth & faster decode on A13/A15/M1)",
                Location = new Point(15, 74),
                Size = new Size(560, 20),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Checked = true
            };
            chkHevc.CheckedChanged += OptionChanged;

            Label lblFpsCap = new Label
            {
                Text = "🎬 Cast Stream FPS Cap:",
                Location = new Point(15, 107),
                Size = new Size(175, 22),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            cmbFpsCap = new ComboBox
            {
                Location = new Point(190, 103),
                Size = new Size(380, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(56, 189, 248),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            cmbFpsCap.Items.Add("60 FPS — Standard iOS Cap (Recommended - Max Smoothness)");
            cmbFpsCap.Items.Add("30 FPS — Low Bandwidth / Anti-Bufferbloat (Best for 2.4GHz Wi-Fi)");
            cmbFpsCap.Items.Add("45 FPS — Balanced Cadence");
            cmbFpsCap.Items.Add(string.Format("{0} FPS — Uncapped (Match Native Monitor Refresh)", nativeRefreshRate));
            cmbFpsCap.SelectedIndex = 0;
            cmbFpsCap.SelectedIndexChanged += OptionChanged;

            grpLatency.Controls.Add(chkSmoothCursor);
            grpLatency.Controls.Add(chkHwAccel);
            grpLatency.Controls.Add(chkHevc);
            grpLatency.Controls.Add(lblFpsCap);
            grpLatency.Controls.Add(cmbFpsCap);
            this.Controls.Add(grpLatency);

            // Active Status Card
            Panel pnlStatus = new Panel
            {
                Location = new Point(20, 530),
                Size = new Size(585, 84),
                BackColor = Color.FromArgb(24, 33, 56)
            };
            pnlStatus.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(51, 65, 85), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, 584, 83);
                }
            };

            pnlStatusIndicator = new Panel
            {
                Location = new Point(16, 16),
                Size = new Size(12, 12),
                BackColor = Color.Gray
            };
            pnlStatusIndicator.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(pnlStatusIndicator.BackColor))
                {
                    e.Graphics.FillEllipse(b, 0, 0, 11, 11);
                }
            };

            lblStatus = new Label
            {
                Text = "Status: Receiver Idle",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(36, 12),
                AutoSize = true
            };

            lblActiveSettings = new Label
            {
                Text = "Advertised Name: 'Wireless Mirror' | Native Resolution: 1920x1080 | Bitrate Cap: 15,000 kbps",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(16, 42),
                Size = new Size(555, 34)
            };

            pnlStatus.Controls.Add(pnlStatusIndicator);
            pnlStatus.Controls.Add(lblStatus);
            pnlStatus.Controls.Add(lblActiveSettings);
            this.Controls.Add(pnlStatus);

            // Action Buttons
            btnLaunch = new Button
            {
                Text = "▶ Launch Wireless Mirror",
                Location = new Point(20, 625),
                Size = new Size(285, 46),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnLaunch.FlatAppearance.BorderSize = 0;
            btnLaunch.Click += BtnLaunch_Click;

            btnStop = new Button
            {
                Text = "⏹ Stop Receiver",
                Location = new Point(320, 625),
                Size = new Size(285, 46),
                BackColor = Color.FromArgb(71, 85, 105),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.Click += BtnStop_Click;

            btnCreateShortcuts = new Button
            {
                Text = "📌 Create Desktop & Start Menu Shortcuts",
                Location = new Point(20, 680),
                Size = new Size(585, 38),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCreateShortcuts.FlatAppearance.BorderSize = 0;
            btnCreateShortcuts.Click += BtnCreateShortcuts_Click;

            this.Controls.Add(btnLaunch);
            this.Controls.Add(btnStop);
            this.Controls.Add(btnCreateShortcuts);
        }

        private RadioButton CreatePresetRadio(string title, string desc, int x, int y, bool isChecked)
        {
            RadioButton rb = new RadioButton
            {
                Text = title + "\n  ↳ " + desc,
                Location = new Point(x, y),
                Size = new Size(555, 36),
                ForeColor = Color.FromArgb(241, 245, 249),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Checked = isChecked,
                Cursor = Cursors.Hand
            };
            return rb;
        }

        private void PresetChanged(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            UpdateStatus();
        }

        private void OptionChanged(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            UpdateStatus();
        }

        private void LoadCurrentSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string[] lines = File.ReadAllLines(SettingsFile);
                    int w = nativeMonitorW, h = nativeMonitorH;
                    bool hw = true, hevc = true, vsync = true, diag = false;
                    int maxFps = 60;

                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length != 2) continue;
                        string k = parts[0].Trim().ToLower();
                        string v = parts[1].Trim();

                        if (k == "mirror_width") int.TryParse(v, out w);
                        if (k == "mirror_height") int.TryParse(v, out h);
                        if (k == "mirror_hwaccel") hw = (v == "1");
                        if (k == "hevc_enabled") hevc = (v == "1");
                        if (k == "vsync_enabled") vsync = (v == "1");
                        if (k == "diagnostics") diag = (v == "1");
                        if (k == "max_fps") int.TryParse(v, out maxFps);
                    }

                    chkSmoothCursor.Checked = vsync;
                    chkHwAccel.Checked = hw;
                    chkHevc.Checked = hevc;

                    if (cmbFpsCap != null)
                    {
                        if (maxFps == 30) cmbFpsCap.SelectedIndex = 1;
                        else if (maxFps == 45) cmbFpsCap.SelectedIndex = 2;
                        else if (maxFps >= 100) cmbFpsCap.SelectedIndex = 3;
                        else cmbFpsCap.SelectedIndex = 0; // 60 FPS default
                    }

                    if (w == 2778 && h == 2778) rbPresetUncapped.Checked = true;
                    else if (w == 1280 && h == 720) rbPreset720p.Checked = true;
                    else if (w == 1440 && h == 1440) rbPresetSquare.Checked = true;
                    else if (w == 1920 && h == 888) rbPresetIphone.Checked = true;
                    else if (w == 1600 && h == 1200) rbPresetIpad.Checked = true;
                    else rbPreset1080p.Checked = true;
                }
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            int w = nativeMonitorW;
            int h = nativeMonitorH;

            if (rbPreset720p.Checked)
            {
                w = 1280;
                h = 720;
            }
            else if (rbPresetSquare.Checked)
            {
                w = 1440;
                h = 1440;
            }
            else if (rbPresetIphone.Checked)
            {
                w = 1920;
                h = 888;
            }
            else if (rbPresetIpad.Checked)
            {
                w = 1600;
                h = 1200;
            }
            else if (rbPresetUncapped.Checked)
            {
                w = 2778;
                h = 2778;
            }
            else
            {
                w = nativeMonitorW;
                h = nativeMonitorH;
            }

            int selectedFps = 60;
            if (cmbFpsCap != null)
            {
                if (cmbFpsCap.SelectedIndex == 1) selectedFps = 30;
                else if (cmbFpsCap.SelectedIndex == 2) selectedFps = 45;
                else if (cmbFpsCap.SelectedIndex == 3) selectedFps = nativeRefreshRate > 0 ? nativeRefreshRate : 144;
                else selectedFps = 60;
            }

            int vsync = chkSmoothCursor.Checked ? 1 : 0;
            int hwaccel = chkHwAccel.Checked ? 1 : 0;
            int hevc = chkHevc.Checked ? 1 : 0;
            int nativeHz = nativeRefreshRate > 0 ? nativeRefreshRate : GetNativeRefreshRate();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("receiver_name=Wireless Mirror");
            sb.AppendLine("mirror_width=" + w);
            sb.AppendLine("mirror_height=" + h);
            sb.AppendLine("hevc_enabled=" + hevc);
            sb.AppendLine("max_fps=" + selectedFps);
            sb.AppendLine("refresh_rate=" + nativeHz);
            sb.AppendLine("mirror_hwaccel=" + hwaccel);
            sb.AppendLine("vsync_enabled=" + vsync);
            sb.AppendLine("fullscreen=0");
            sb.AppendLine("chromeless=0");
            sb.AppendLine("video_rotation=-1");
            sb.AppendLine("diagnostics=1");
            sb.AppendLine("window_placement_valid=1");
            sb.AppendLine("window_x=200");
            sb.AppendLine("window_y=100");
            sb.AppendLine("window_width=1280");
            sb.AppendLine("window_height=720");
            sb.AppendLine("window_maximized=0");
            sb.AppendLine("recording_directory=" + appDir);
            sb.AppendLine("recording_codec=0");
            sb.AppendLine("recording_encoder=0");
            sb.AppendLine("recording_bitrate=8");

            try
            {
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);
                File.WriteAllText(SettingsFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private void UpdateStatus()
        {
            Process[] procs = Process.GetProcessesByName("WirelessMirror");
            bool isRunning = procs.Length > 0;

            if (isRunning)
            {
                pnlStatusIndicator.BackColor = Color.FromArgb(34, 197, 94);
                lblStatus.Text = "Status: Wireless Mirror Active (Ready for Screen Mirroring)";
                lblStatus.ForeColor = Color.FromArgb(74, 222, 128);
                btnLaunch.Text = "🔄 Restart Wireless Mirror";
                btnLaunch.BackColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                pnlStatusIndicator.BackColor = Color.FromArgb(148, 163, 184);
                lblStatus.Text = "Status: Receiver Idle (Click Launch below)";
                lblStatus.ForeColor = Color.FromArgb(203, 213, 225);
                btnLaunch.Text = "▶ Launch Wireless Mirror";
                btnLaunch.BackColor = Color.FromArgb(37, 99, 235);
            }

            pnlStatusIndicator.Invalidate();

            int w = nativeMonitorW, h = nativeMonitorH;
            string capDesc = "Max 15 Mbps Cap";
            if (rbPreset720p.Checked) { w = 1280; h = 720; capDesc = "Max 5 Mbps (Anti-Bufferbloat)"; }
            else if (rbPresetSquare.Checked) { w = 1440; h = 1440; capDesc = "Max 15 Mbps Cap"; }
            else if (rbPresetIphone.Checked) { w = 1920; h = 888; capDesc = "Max 14 Mbps Cap"; }
            else if (rbPresetIpad.Checked) { w = 1600; h = 1200; capDesc = "Max 15 Mbps Cap"; }
            else if (rbPresetUncapped.Checked) { w = 2778; h = 2778; capDesc = "Uncapped 30 Mbps"; }

            int selectedFps = 60;
            if (cmbFpsCap != null)
            {
                if (cmbFpsCap.SelectedIndex == 1) selectedFps = 30;
                else if (cmbFpsCap.SelectedIndex == 2) selectedFps = 45;
                else if (cmbFpsCap.SelectedIndex == 3) selectedFps = nativeRefreshRate;
                else selectedFps = 60;
            }

            lblActiveSettings.Text = string.Format(
                "Resolution: {0}x{1} ({2}) | Monitor: {3}Hz | Cast Cap: {4} FPS | Cursor: {5}",
                w, h, capDesc,
                nativeRefreshRate,
                selectedFps,
                chkSmoothCursor.Checked ? string.Format("Smooth {0}Hz VSync", nativeRefreshRate) : "Unlocked"
            );
        }

        private void BtnLaunch_Click(object sender, EventArgs e)
        {
            SaveCurrentSettings();

            // Kill any previous instance first
            foreach (Process p in Process.GetProcessesByName("WirelessMirror"))
            {
                try { p.Kill(); p.WaitForExit(1000); } catch { }
            }

            if (!File.Exists(coreExePath))
            {
                MessageBox.Show(
                    "WirelessMirror.exe was not found in: " + appDir,
                    "Executable Missing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = coreExePath,
                    WorkingDirectory = appDir,
                    UseShellExecute = true
                };
                Process newProc = Process.Start(psi);
                if (newProc != null)
                {
                    try
                    {
                        // Give receiver AboveNormal priority for stutter-free audio & rendering
                        newProc.PriorityClass = ProcessPriorityClass.AboveNormal;
                    }
                    catch { }
                }

                System.Threading.Thread.Sleep(500);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to start Wireless Mirror: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            Process[] procs = Process.GetProcessesByName("WirelessMirror");
            if (procs.Length == 0)
            {
                MessageBox.Show("Wireless Mirror is not currently running.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (Process p in procs)
            {
                try { p.Kill(); } catch { }
            }
            System.Threading.Thread.Sleep(300);
            UpdateStatus();
        }

        private void BtnCreateShortcuts_Click(object sender, EventArgs e)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string startMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "Wireless Mirror"
                );

                if (!Directory.Exists(startMenu))
                    Directory.CreateDirectory(startMenu);

                CreateShortcut(
                    Path.Combine(desktop, "Wireless Mirror.lnk"),
                    coreExePath,
                    appDir,
                    "Wireless Mirror - High Performance AirPlay Receiver"
                );

                CreateShortcut(
                    Path.Combine(desktop, "Wireless Mirror Control Center.lnk"),
                    Application.ExecutablePath,
                    appDir,
                    "Wireless Mirror Settings & Device Resolution Presets"
                );

                CreateShortcut(
                    Path.Combine(startMenu, "Wireless Mirror.lnk"),
                    coreExePath,
                    appDir,
                    "Wireless Mirror - High Performance AirPlay Receiver"
                );

                CreateShortcut(
                    Path.Combine(startMenu, "Wireless Mirror Control Center.lnk"),
                    Application.ExecutablePath,
                    appDir,
                    "Wireless Mirror Settings & Device Resolution Presets"
                );

                MessageBox.Show(
                    "Shortcuts created successfully on your Desktop and in the Start Menu under 'Wireless Mirror'!",
                    "Shortcuts Created",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating shortcuts: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.Description = description;
            shortcut.Save();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            TimeEndPeriod(1);
            base.OnFormClosing(e);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            LauncherForm form = new LauncherForm();

            bool startMinimized = false;
            bool autoLaunch = false;

            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (arg == "--start-minimized") startMinimized = true;
                    if (arg == "--launch") autoLaunch = true;
                }
            }

            if (startMinimized)
            {
                form.WindowState = FormWindowState.Minimized;
                form.ShowInTaskbar = false;
            }

            if (autoLaunch)
            {
                form.BtnLaunch_Click(null, EventArgs.Empty);
            }

            Application.Run(form);
        }
    }
}
