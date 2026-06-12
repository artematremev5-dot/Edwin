using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using LibreHardwareMonitor.Hardware;

namespace Edwin
{
    public partial class Form1 : Form
    {
        private Panel leftPanel;
        private Panel mainPanel;
        private Button btnPerformance;
        private Button btnTweaks;
        private Button btnCustomization;

        private Label lblCpu;
        private Label lblTemp;
        private Label lblFps;
        private System.Windows.Forms.Timer updateTimer;

        private CheckBox chkOnedrive;
        private CheckBox chkStartRecommendations;
        private CheckBox chkOldContextMenu;

        // Элемент для кастомизации темы
        private CheckBox chkDarkTheme;

        private PerformanceCounter cpuCounter;
        private Computer computer;

        private int frameCount = 0;
        private int actualFps = 0;
        private Stopwatch fpsStopwatch = new Stopwatch();

        private bool isOneDriveVisible = true;
        private bool isStartRecommendationsVisible = true;
        private bool useOldContextMenu = false;
        private bool useDarkTheme = false; // Переменная для хранения состояния темы

        private readonly string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");

        public Form1()
        {
            this.Text = "Edwin";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;

            LoadSettings();
            InitUI();
            InitHardware();

            ShowPerformanceView();
        }

        private void InitUI()
        {
            leftPanel = new Panel { Dock = DockStyle.Left, Width = 180, BackColor = Color.FromArgb(45, 45, 48) };
            this.Controls.Add(leftPanel);

            mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(28, 28, 28) };
            this.Controls.Add(mainPanel);

            btnPerformance = new Button { Text = "Производительность", Size = new Size(160, 40), Location = new Point(10, 20), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            btnPerformance.Click += (s, e) => ShowPerformanceView();
            leftPanel.Controls.Add(btnPerformance);

            btnTweaks = new Button { Text = "Твики", Size = new Size(160, 40), Location = new Point(10, 70), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            btnTweaks.Click += (s, e) => ShowTweaksView();
            leftPanel.Controls.Add(btnTweaks);

            btnCustomization = new Button { Text = "Кастомизация", Size = new Size(160, 40), Location = new Point(10, 120), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            btnCustomization.Click += (s, e) => ShowCustomizationView();
            leftPanel.Controls.Add(btnCustomization);
        }

        private void InitHardware()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
            }
            catch { cpuCounter = null; }

            try
            {
                computer = new Computer { IsCpuEnabled = true, IsMotherboardEnabled = true, IsGpuEnabled = true };
                computer.Open();
            }
            catch { computer = null; }

            updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

            fpsStopwatch.Start();
        }

        private void ShowPerformanceView()
        {
            mainPanel.Controls.Clear();
            lblCpu = CreateLabel("Загрузка CPU: 0%", 190, 30);
            lblTemp = CreateLabel("Температура CPU: -- °C", 190, 80);
            lblFps = CreateLabel("FPS приложения: --", 190, 130);
            mainPanel.Controls.Add(lblCpu);
            mainPanel.Controls.Add(lblTemp);
            mainPanel.Controls.Add(lblFps);
            UpdateHardwareData();
        }

        private void ShowTweaksView()
        {
            mainPanel.Controls.Clear();

            chkOnedrive = new CheckBox { Text = "Отображать OneDrive в системе", ForeColor = Color.White, Font = new Font("Segoe UI", 14), AutoSize = true, Location = new Point(190, 35), Checked = isOneDriveVisible };
            chkOnedrive.CheckedChanged += (s, e) => { isOneDriveVisible = chkOnedrive.Checked; SetOneDriveVisibility(isOneDriveVisible); SaveSettings(); };
            mainPanel.Controls.Add(chkOnedrive);

            chkStartRecommendations = new CheckBox { Text = "Рекомендации в меню Пуск", ForeColor = Color.White, Font = new Font("Segoe UI", 14), AutoSize = true, Location = new Point(190, 85), Checked = isStartRecommendationsVisible };
            chkStartRecommendations.CheckedChanged += (s, e) => { isStartRecommendationsVisible = chkStartRecommendations.Checked; SetStartRecommendationsVisibility(isStartRecommendationsVisible); SaveSettings(); };
            mainPanel.Controls.Add(chkStartRecommendations);

            chkOldContextMenu = new CheckBox { Text = "Классическое меню ПКМ (Win 10)", ForeColor = Color.White, Font = new Font("Segoe UI", 14), AutoSize = true, Location = new Point(190, 135), Checked = useOldContextMenu };
            chkOldContextMenu.CheckedChanged += (s, e) => { useOldContextMenu = chkOldContextMenu.Checked; SetOldContextMenu(useOldContextMenu); SaveSettings(); };
            mainPanel.Controls.Add(chkOldContextMenu);
        }

        private void ShowCustomizationView()
        {
            mainPanel.Controls.Clear();

            // Заменяем кнопку на удобный чекбокс переключения тем
            chkDarkTheme = new CheckBox
            {
                Text = "Включить Тёмную Тему системы",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14),
                AutoSize = true,
                Location = new Point(190, 35),
                Checked = useDarkTheme
            };
            chkDarkTheme.CheckedChanged += (s, e) => { useDarkTheme = chkDarkTheme.Checked; SetSystemTheme(useDarkTheme); SaveSettings(); };
            mainPanel.Controls.Add(chkDarkTheme);

            Label lblInfo = new Label
            {
                Text = "*Включение или отключение тёмного режима в обход ограничений персонализации.",
                ForeColor = Color.DarkGray,
                Font = new Font("Segoe UI", 10, FontStyle.Italic),
                Size = new Size(350, 80),
                Location = new Point(190, 85)
            };
            mainPanel.Controls.Add(lblInfo);
        }

        // Логика переключения темы (Темная / Светлая) через реестр
        private void SetSystemTheme(bool dark)
        {
            try
            {
                string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    if (key != null)
                    {
                        // В реестре: 0 = Тёмная тема, 1 = Светлая тема
                        int themeValue = dark ? 0 : 1;

                        key.SetValue("AppsUseLightTheme", themeValue, RegistryValueKind.DWord);
                        key.SetValue("SystemUsesLightTheme", themeValue, RegistryValueKind.DWord);
                    }
                    else
                    {
                        MessageBox.Show("Не удалось получить доступ к разделу тем.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Запустите программу от имени Администратора.\n" + ex.Message, "Ошибка доступа", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetOldContextMenu(bool useOld)
        {
            try
            {
                string keyPath = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";

                if (useOld)
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                    {
                        if (key != null) key.SetValue("", "");
                    }
                }
                else
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}"); } catch { }
                }
                RestartExplorer();
            }
            catch (Exception ex) { MessageBox.Show("Не удалось изменить меню мыши.\n" + ex.Message, "Ошибка реестра", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void SetStartRecommendationsVisibility(bool visible)
        {
            try
            {
                int v = visible ? 1 : 0;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true)) { if (k != null) { k.SetValue("Start_TrackPrograms", v); k.SetValue("Start_TrackDocs", v); } }
                using (RegistryKey k2 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", true)) { if (k2 != null) k2.SetValue("SubscribedContent-338388Enabled", v); }
                using (RegistryKey k3 = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Explorer", true)) { if (k3 != null) k3.SetValue("HideRecommendedSection", visible ? 0 : 1); }
                RestartExplorer();
            }
            catch (Exception ex) { MessageBox.Show("Не удалось изменить настройки. Запустите VS от Администратора.\n\n" + ex.Message, "Ошибка доступа", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void SetOneDriveVisibility(bool visible)
        {
            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", true)) { if (key != null) key.SetValue("System.IsPinnedToNameSpaceTree", visible ? 1 : 0); }
                using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (runKey != null)
                    {
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string oneDrivePath = $"{localAppData}\\Microsoft\\OneDrive\\OneDrive.exe";
                        if (visible)
                        {
                            runKey.SetValue("OneDrive", $"\"{oneDrivePath}\" /background");
                            string tName = "Edwin_Start_OneDrive";
                            ProcessStartInfo cInfo = new ProcessStartInfo { FileName = "schtasks.exe", Arguments = $"/create /tn \"{tName}\" /tr \"'{oneDrivePath}' /background\" /sc once /sd 01/01/2026 /st 00:00 /rl limited /f", WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                            Process.Start(cInfo).WaitForExit();
                            ProcessStartInfo rInfo = new ProcessStartInfo { FileName = "schtasks.exe", Arguments = $"/run /tn \"{tName}\"", WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                            Process.Start(rInfo);
                            System.Threading.Tasks.Task.Run(async () => { await System.Threading.Tasks.Task.Delay(1000); ProcessStartInfo dInfo = new ProcessStartInfo { FileName = "schtasks.exe", Arguments = $"/delete /tn \"{tName}\" /f", WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true }; Process.Start(dInfo); });
                        }
                        else
                        {
                            if (runKey.GetValue("OneDrive") != null) runKey.DeleteValue("OneDrive");
                            foreach (Process p in Process.GetProcessesByName("OneDrive")) p.Kill();
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Не удалось изменить настройки OneDrive. Запустите VS от Администратора.\n\n" + ex.Message, "Ошибка доступа", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void RestartExplorer()
        {
            try { foreach (Process p in Process.GetProcessesByName("explorer")) { p.Kill(); p.WaitForExit(); } } catch { }
            try { Process.Start("explorer.exe"); } catch { }
        }

        private Label CreateLabel(string text, int left, int top)
        {
            return new Label { Text = text, ForeColor = Color.GreenYellow, Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Location = new Point(left, top) };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); frameCount++;
            if (fpsStopwatch.ElapsedMilliseconds >= 1000) { actualFps = frameCount; frameCount = 0; fpsStopwatch.Restart(); }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            this.Invalidate();
            if (mainPanel.Controls.Contains(lblCpu)) UpdateHardwareData();
        }

        private void UpdateHardwareData()
        {
            if (cpuCounter != null) lblCpu.Text = $"Загрузка CPU: {Math.Round(cpuCounter.NextValue())}%";
            if (computer != null)
            {
                float? maxTemp = null;
                foreach (var h in computer.Hardware) { h.Update(); foreach (var s in h.Sensors) { if (s.SensorType == SensorType.Temperature && s.Value.HasValue) { if (maxTemp == null || s.Value > maxTemp) maxTemp = s.Value; } } }
                lblTemp.Text = maxTemp.HasValue ? $"Температура CPU: {Math.Round(maxTemp.Value)} °C" : "Температура CPU: -- °C";
            }
            lblFps.Text = $"FPS приложения: {actualFps}";
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string text = File.ReadAllText(configFilePath).Trim();
                    string[] parts = text.Split(',');

                    if (parts.Length >= 4)
                    {
                        isOneDriveVisible = (parts[0] == "True");
                        isStartRecommendationsVisible = (parts[1] == "True");
                        useOldContextMenu = (parts[2] == "True");
                        useDarkTheme = (parts[3] == "True");
                    }
                }
                else
                {
                    isOneDriveVisible = GetOneDriveVisibilityFromSystem();
                    isStartRecommendationsVisible = GetStartRecommendationsFromSystem();
                    useOldContextMenu = GetOldContextMenuFromSystem();
                    useDarkTheme = GetDarkThemeFromSystem(); // Проверяем тему при отсутствии конфига
                }
            }
            catch
            {
                isOneDriveVisible = true;
                isStartRecommendationsVisible = true;
                useOldContextMenu = false;
                useDarkTheme = false;
            }
        }

        private void SaveSettings()
        {
            try { File.WriteAllText(configFilePath, $"{isOneDriveVisible},{isStartRecommendationsVisible},{useOldContextMenu},{useDarkTheme}"); } catch { }
        }

        private bool GetOneDriveVisibilityFromSystem()
        {
            try { using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}")) { if (key != null) { object v = key.GetValue("System.IsPinnedToNameSpaceTree"); if (v != null && (int)v == 0) return false; } } } catch { }
            return true;
        }

        private bool GetStartRecommendationsFromSystem()
        {
            try { using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced")) { if (key != null) { object v = key.GetValue("Start_TrackPrograms"); if (v != null && (int)v == 0) return false; } } } catch { }
            return true;
        }

        private bool GetOldContextMenuFromSystem()
        {
            try { using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32")) { return key != null; } } catch { return false; }
        }

        // Считываем, какая тема установлена в системе прямо сейчас
        private bool GetDarkThemeFromSystem()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object v = key.GetValue("AppsUseLightTheme");
                        if (v != null && (int)v == 0) return true; // Если значение 0, значит тема Тёмная
                    }
                }
            }
            catch { }
            return false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (computer != null) computer.Close();
            base.OnFormClosing(e);
        }
    }
}