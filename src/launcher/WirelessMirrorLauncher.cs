using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WirelessMirror
{
    public class LauncherForm : Form
    {
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

        private RadioButton rbUniversal;
        private RadioButton rbIphone14Plus;
        private RadioButton rbIpadAir5;
        private RadioButton rbIphone11;

        private CheckBox chkUltraLowLatency;
        private CheckBox chkHwAccel;
        private CheckBox chkHevc;
        private CheckBox chkDiagnostics;

        private Button btnLaunch;
        private Button btnStop;
        private Button btnCreateShortcuts;
        private Label lblActiveSettings;
        private Timer statusTimer;

        public LauncherForm()
        {
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
            this.Text = "Wireless Mirror - Ultra Low Latency AirPlay Receiver";
            this.Size = new Size(620, 680);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 20, 26);
            this.ForeColor = Color.FromArgb(240, 243, 246);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // Header Banner
            Panel pnlHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(620, 80),
                BackColor = Color.FromArgb(24, 28, 38)
            };
            pnlHeader.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(45, 52, 70), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 79, 620, 79);
                }
            };

            lblHeaderTitle = new Label
            {
                Text = "⚡ Wireless Mirror Control Center",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(96, 165, 250),
                Location = new Point(20, 15),
                AutoSize = true
            };

            lblHeaderSubtitle = new Label
            {
                Text = "Native Resolution & Sub-Frame Low Latency AirPlay Receiver for iOS",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(156, 163, 175),
                Location = new Point(24, 45),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);
            this.Controls.Add(pnlHeader);

            // Device Resolution Presets Group
            GroupBox grpPresets = new GroupBox
            {
                Text = "  📱 Target Device Native Resolution  ",
                Location = new Point(20, 95),
                Size = new Size(565, 180),
                ForeColor = Color.FromArgb(96, 165, 250),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            rbUniversal = CreatePresetRadio(
                "✨ Universal Auto-Fit (2778 x 2778 box - 100% Native 1:1 for All Devices)",
                "Best Choice: Dynamically streams at exact native unscaled resolution in both portrait & landscape",
                15, 28, true
            );

            rbIphone14Plus = CreatePresetRadio(
                "📱 iPhone 14 Plus Native (2778 x 1284 @ 60 Hz)",
                "Full 19.53:9 OLED Retina unscaled mapping (Portrait: 1284x2778 / Landscape: 2778x1284)",
                15, 66, false
            );

            rbIpadAir5 = CreatePresetRadio(
                "💻 iPad Air 5 Native (2360 x 1640 @ 60 Hz)",
                "Liquid Retina 4.3:3 unscaled mapping (Portrait: 1640x2360 / Landscape: 2360x1640)",
                15, 104, false
            );

            rbIphone11 = CreatePresetRadio(
                "📱 iPhone 11 Native (1792 x 828 @ 60 Hz)",
                "Liquid Retina HD 19.48:9 unscaled mapping (Portrait: 828x1792 / Landscape: 1792x828)",
                15, 142, false
            );

            rbUniversal.CheckedChanged += PresetChanged;
            rbIphone14Plus.CheckedChanged += PresetChanged;
            rbIpadAir5.CheckedChanged += PresetChanged;
            rbIphone11.CheckedChanged += PresetChanged;

            grpPresets.Controls.Add(rbUniversal);
            grpPresets.Controls.Add(rbIphone14Plus);
            grpPresets.Controls.Add(rbIpadAir5);
            grpPresets.Controls.Add(rbIphone11);
            this.Controls.Add(grpPresets);

            // Latency & Acceleration Group
            GroupBox grpLatency = new GroupBox
            {
                Text = "  ⚡ Latency & Hardware Acceleration  ",
                Location = new Point(20, 285),
                Size = new Size(565, 135),
                ForeColor = Color.FromArgb(96, 165, 250),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            chkUltraLowLatency = new CheckBox
            {
                Text = "🚀 Extreme Low Latency (VSync Bypass / Zero-Buffer Presentation ~1ms local pipeline)",
                Location = new Point(15, 28),
                Size = new Size(540, 24),
                ForeColor = Color.FromArgb(52, 211, 153),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Checked = true
            };
            chkUltraLowLatency.CheckedChanged += OptionChanged;

            chkHwAccel = new CheckBox
            {
                Text = "Direct3D 11 GPU Hardware Accelerated Video Decoder (D3D11VA Zero-Copy)",
                Location = new Point(15, 58),
                Size = new Size(540, 22),
                ForeColor = Color.FromArgb(229, 231, 235),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Checked = true
            };
            chkHwAccel.CheckedChanged += OptionChanged;

            chkHevc = new CheckBox
            {
                Text = "Enable HEVC / H.265 Stream Encoding (Lower Wi-Fi bandwidth & faster decode on A13/A15/M1)",
                Location = new Point(15, 82),
                Size = new Size(540, 22),
                ForeColor = Color.FromArgb(229, 231, 235),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Checked = true
            };
            chkHevc.CheckedChanged += OptionChanged;

            chkDiagnostics = new CheckBox
            {
                Text = "Show In-App Performance Overlay (Real-time FPS, Bitrate, Pipeline Latency ms)",
                Location = new Point(15, 106),
                Size = new Size(540, 22),
                ForeColor = Color.FromArgb(156, 163, 175),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Checked = false
            };
            chkDiagnostics.CheckedChanged += OptionChanged;

            grpLatency.Controls.Add(chkUltraLowLatency);
            grpLatency.Controls.Add(chkHwAccel);
            grpLatency.Controls.Add(chkHevc);
            grpLatency.Controls.Add(chkDiagnostics);
            this.Controls.Add(grpLatency);

            // Active Status Card
            Panel pnlStatus = new Panel
            {
                Location = new Point(20, 430),
                Size = new Size(565, 80),
                BackColor = Color.FromArgb(24, 28, 38)
            };
            pnlStatus.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(45, 52, 70), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, 564, 79);
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
                Text = "Advertised Name: 'Wireless Mirror' | Native Resolution: 2778x2778 | FPS: 60 | GPU D3D11VA: ON",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(156, 163, 175),
                Location = new Point(16, 42),
                Size = new Size(535, 30)
            };

            pnlStatus.Controls.Add(pnlStatusIndicator);
            pnlStatus.Controls.Add(lblStatus);
            pnlStatus.Controls.Add(lblActiveSettings);
            this.Controls.Add(pnlStatus);

            // Buttons
            btnLaunch = new Button
            {
                Text = "▶ Launch Wireless Mirror",
                Location = new Point(20, 525),
                Size = new Size(275, 45),
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
                Text = "⏹ Stop / Close Receiver",
                Location = new Point(310, 525),
                Size = new Size(275, 45),
                BackColor = Color.FromArgb(55, 65, 81),
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
                Location = new Point(20, 580),
                Size = new Size(565, 36),
                BackColor = Color.FromArgb(31, 41, 55),
                ForeColor = Color.FromArgb(209, 213, 219),
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
                Text = title,
                Location = new Point(x, y),
                Size = new Size(535, 24),
                ForeColor = Color.FromArgb(243, 244, 246),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
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
                    int w = 2778, h = 2778;
                    bool hw = true, hevc = true, vsync = false, diag = false;

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
                    }

                    chkUltraLowLatency.Checked = !vsync;
                    chkHwAccel.Checked = hw;
                    chkHevc.Checked = hevc;
                    chkDiagnostics.Checked = diag;

                    if (w == 1792 && (h == 1792 || h == 828)) rbIphone11.Checked = true;
                    else if (w == 2360 && (h == 2360 || h == 1640)) rbIpadAir5.Checked = true;
                    else if (w == 2778 && h == 1284) rbIphone14Plus.Checked = true;
                    else rbUniversal.Checked = true;
                }
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            int w = 2778;
            int h = 2778;

            if (rbIphone14Plus.Checked)
            {
                w = 2778;
                h = 1284;
            }
            else if (rbIpadAir5.Checked)
            {
                w = 2360;
                h = 1640;
            }
            else if (rbIphone11.Checked)
            {
                w = 1792;
                h = 828;
            }

            int vsync = chkUltraLowLatency.Checked ? 0 : 1;
            int hwaccel = chkHwAccel.Checked ? 1 : 0;
            int hevc = chkHevc.Checked ? 1 : 0;
            int diag = chkDiagnostics.Checked ? 1 : 0;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("receiver_name=Wireless Mirror");
            sb.AppendLine("mirror_width=" + w);
            sb.AppendLine("mirror_height=" + h);
            sb.AppendLine("hevc_enabled=" + hevc);
            sb.AppendLine("max_fps=60");
            sb.AppendLine("refresh_rate=60");
            sb.AppendLine("mirror_hwaccel=" + hwaccel);
            sb.AppendLine("vsync_enabled=" + vsync);
            sb.AppendLine("fullscreen=0");
            sb.AppendLine("chromeless=0");
            sb.AppendLine("video_rotation=-1");
            sb.AppendLine("diagnostics=" + diag);
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
                pnlStatusIndicator.BackColor = Color.FromArgb(156, 163, 175);
                lblStatus.Text = "Status: Receiver Idle (Click Launch below)";
                lblStatus.ForeColor = Color.FromArgb(209, 213, 219);
                btnLaunch.Text = "▶ Launch Wireless Mirror";
                btnLaunch.BackColor = Color.FromArgb(37, 99, 235);
            }

            pnlStatusIndicator.Invalidate();

            int w = 2778, h = 2778;
            string deviceDesc = "Universal (Auto 1:1)";
            if (rbIphone14Plus.Checked) { w = 2778; h = 1284; deviceDesc = "iPhone 14 Plus Native"; }
            else if (rbIpadAir5.Checked) { w = 2360; h = 1640; deviceDesc = "iPad Air 5 Native"; }
            else if (rbIphone11.Checked) { w = 1792; h = 828; deviceDesc = "iPhone 11 Native"; }

            lblActiveSettings.Text = string.Format(
                "Profile: {0} ({1}x{2}) | Refresh: 60Hz/60FPS | Pipeline: {3} | GPU D3D11VA: {4}",
                deviceDesc, w, h,
                chkUltraLowLatency.Checked ? "1ms Ultra-Low (VSync 0)" : "Cinema (VSync 1)",
                chkHwAccel.Checked ? "Active" : "Software"
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
                Process.Start(psi);
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

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }
    }
}
