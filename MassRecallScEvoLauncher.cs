using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("MassRecall SC Evo Launcher")]
[assembly: AssemblyDescription("Windows installer for StarCraft Mass Recall SC Evo patch archives")]
[assembly: AssemblyProduct("MassRecall SC Evo Launcher")]
[assembly: AssemblyCompany("MassRecall SC Evo")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("26.5.9.0")]
[assembly: AssemblyFileVersion("26.5.9.0")]
[assembly: AssemblyInformationalVersion("26.05.09")]
[assembly: ComVisible(false)]

namespace MassRecallScEvo
{
    internal static class Program
    {
        private const string AppName = "MassRecall SC Evo Launcher";
        private const string Version = "26.05.09";
        private const string LegacyPlaceholderVersion = "1.0.0";
        private const string LatestInfoUrl = "https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo_latest.json";
        private const string ChangeLogUrl = "https://potenking.blogspot.com/2024/04/httpsdrive.html";
        private const string HttpUserAgent = "SCMR-SC-Evo-Installer/26.05.09";
        private static readonly string StateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MassRecallSCEvoLauncher");
        private static readonly string CacheDir = Path.Combine(StateDir, "Cache");
        private static readonly DateTime ProcessStartUtc = DateTime.UtcNow;
        private static readonly object InstalledItemsLock = new object();
        private static readonly HashSet<string> InstalledItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Package[] Packages =
        {
            new Package(
                "메인 파일",
                "https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo.zip",
                "massrecall-main.zip"),
            new Package(
                "한국어 음성 파일",
                "https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo.KR.VO.Assets.Local.zip",
                "massrecall-korean-voice.zip"),
            new Package(
                "영어 음성 파일",
                "https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo.EN.VO.Assets.Local.zip",
                "massrecall-english-voice.zip")
        };

        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }

        private sealed class LauncherForm : Form
        {
            private const int WmNclButtonDown = 0xA1;
            private const int HtCaption = 0x2;

            private static readonly Color BackgroundColor = Color.FromArgb(7, 13, 18);
            private static readonly Color PanelColor = Color.FromArgb(10, 18, 24);
            private static readonly Color BorderColor = Color.FromArgb(21, 58, 73);
            private static readonly Color AccentColor = Color.FromArgb(0, 255, 156);
            private static readonly Color CyanColor = Color.FromArgb(0, 184, 255);
            private static readonly Color NotInstalledColor = Color.FromArgb(235, 74, 74);
            private static readonly Color InstallingColor = Color.FromArgb(255, 158, 45);
            private static readonly Color TextColor = Color.FromArgb(218, 238, 239);
            private static readonly Color MutedTextColor = Color.FromArgb(130, 154, 164);
            private readonly Label statusDot;
            private readonly Label statusLabel;
            private readonly Label installPathLabel;
            private readonly TechProgressBar progressBar;
            private readonly Button actionButton;
            private readonly Button checkButton;
            private readonly Button uninstallButton;
            private readonly TechCheckBox koreanVoiceCheckBox;
            private readonly TechCheckBox englishVoiceCheckBox;
            private readonly TechCheckBox removeBankCheckBox;
            private readonly Label versionLabel;
            private UpdateInfo latestInfo;
            private bool suppressLanguageOptionChange;

            public LauncherForm()
            {
                Text = AppName;
                ClientSize = new Size(720, 350);
                BackColor = BackgroundColor;
                FormBorderStyle = FormBorderStyle.None;
                MaximizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;
                Paint += DrawWindowBorder;

                Icon = LoadAppIcon();


                var headerTitle = new Label();
                headerTitle.Text = "MASS RECALL SC EVO LAUNCHER";
                headerTitle.Font = CreateTechFont(10, FontStyle.Bold);
                headerTitle.ForeColor = AccentColor;
                headerTitle.BackColor = Color.Transparent;
                headerTitle.TextAlign = ContentAlignment.MiddleLeft;
                headerTitle.SetBounds(12, 6, 430, 22);
                headerTitle.MouseDown += DragWindow;

                versionLabel = new Label();
                versionLabel.Text = "";
                versionLabel.Font = CreateTechFont(8, FontStyle.Bold);
                versionLabel.ForeColor = AccentColor;
                versionLabel.BackColor = Color.Transparent;
                versionLabel.TextAlign = ContentAlignment.MiddleRight;
                versionLabel.SetBounds(470, 6, 174, 22);
                versionLabel.MouseDown += DragWindow;

                var minimizeButton = CreateWindowButton("-");
                minimizeButton.SetBounds(652, 4, 28, 24);
                minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };

                var closeButton = CreateWindowButton("X");
                closeButton.SetBounds(684, 4, 28, 24);
                closeButton.Click += delegate { Close(); };

                var topLine = new Panel();
                topLine.BackColor = BorderColor;
                topLine.SetBounds(0, 32, 720, 1);

                statusDot = new Label();
                statusDot.Text = "●";
                statusDot.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                statusDot.ForeColor = CyanColor;
                statusDot.BackColor = Color.Transparent;
                statusDot.SetBounds(20, 282, 20, 20);

                statusLabel = new Label();
                statusLabel.Font = new Font("Segoe UI", 10);
                statusLabel.ForeColor = TextColor;
                statusLabel.BackColor = Color.Transparent;
                statusLabel.TextAlign = ContentAlignment.MiddleLeft;
                statusLabel.SetBounds(42, 280, 320, 24);

                installPathLabel = new Label();
                installPathLabel.Font = new Font("Segoe UI", 9);
                installPathLabel.ForeColor = MutedTextColor;
                installPathLabel.BackColor = Color.Transparent;
                installPathLabel.TextAlign = ContentAlignment.MiddleRight;
                installPathLabel.SetBounds(380, 280, 320, 24);

                var bannerPanel = CreatePanel(20, 50, 400, 154);
                var bannerBox = new PictureBox();
                bannerBox.BackColor = Color.Black;
                bannerBox.SizeMode = PictureBoxSizeMode.Zoom;
                bannerBox.SetBounds(1, 1, 398, 152);
                bannerBox.Image = LoadBannerImage();
                bannerBox.Cursor = Cursors.Hand;
                bannerBox.Click += OpenChangeLog;

                var changeLogHint = new Label();
                changeLogHint.Text = "수정 내역 확인";
                changeLogHint.Font = new Font("Segoe UI", 8, FontStyle.Bold);
                changeLogHint.ForeColor = AccentColor;
                changeLogHint.BackColor = Color.FromArgb(16, 0, 0, 0);
                changeLogHint.TextAlign = ContentAlignment.MiddleLeft;
                changeLogHint.SetBounds(8, 6, 100, 18);
                changeLogHint.Cursor = Cursors.Hand;
                changeLogHint.Click += OpenChangeLog;
                bannerBox.Controls.Add(changeLogHint);

                bannerPanel.Controls.Add(bannerBox);

                var optionPanel = CreatePanel(444, 50, 256, 154);
                var optionTitle = new Label();
                optionTitle.Text = "설치 옵션";
                optionTitle.Font = new Font("Segoe UI", 12, FontStyle.Bold);
                optionTitle.ForeColor = AccentColor;
                optionTitle.BackColor = Color.Transparent;
                optionTitle.SetBounds(20, 12, 160, 26);
                optionPanel.Controls.Add(optionTitle);

                koreanVoiceCheckBox = new TechCheckBox();
                koreanVoiceCheckBox.Text = "한국어 음성";
                koreanVoiceCheckBox.Font = new Font("Segoe UI", 10);
                koreanVoiceCheckBox.ForeColor = TextColor;
                koreanVoiceCheckBox.BackColor = Color.Transparent;
                koreanVoiceCheckBox.AutoSize = false;
                koreanVoiceCheckBox.Checked = true;
                koreanVoiceCheckBox.SetBounds(20, 42, 220, 24);
                koreanVoiceCheckBox.CheckedChanged += OnLanguageOptionChanged;
                optionPanel.Controls.Add(koreanVoiceCheckBox);

                englishVoiceCheckBox = new TechCheckBox();
                englishVoiceCheckBox.Text = "영어 음성";
                englishVoiceCheckBox.Font = new Font("Segoe UI", 10);
                englishVoiceCheckBox.ForeColor = TextColor;
                englishVoiceCheckBox.BackColor = Color.Transparent;
                englishVoiceCheckBox.AutoSize = false;
                englishVoiceCheckBox.SetBounds(20, 64, 220, 24);
                englishVoiceCheckBox.CheckedChanged += OnLanguageOptionChanged;
                optionPanel.Controls.Add(englishVoiceCheckBox);

                removeBankCheckBox = new TechCheckBox();
                removeBankCheckBox.Text = "SCMR.SC2Bank 제거";
                removeBankCheckBox.Font = new Font("Segoe UI", 10);
                removeBankCheckBox.ForeColor = TextColor;
                removeBankCheckBox.BackColor = Color.Transparent;
                removeBankCheckBox.AutoSize = false;
                removeBankCheckBox.SetBounds(20, 98, 220, 24);
                removeBankCheckBox.CheckedChanged += OnLanguageOptionChanged;
                optionPanel.Controls.Add(removeBankCheckBox);

                var bankHelpLabel = new Label();
                bankHelpLabel.Text = "SC Evo 버전 처음 설치 시 체크";
                bankHelpLabel.Font = new Font("Segoe UI", 9);
                bankHelpLabel.ForeColor = Color.FromArgb(95, 115, 125);
                bankHelpLabel.BackColor = Color.Transparent;
                bankHelpLabel.TextAlign = ContentAlignment.MiddleLeft;
                bankHelpLabel.SetBounds(42, 122, 200, 18);
                optionPanel.Controls.Add(bankHelpLabel);

                OnLanguageOptionChanged(null, EventArgs.Empty);

                progressBar = new TechProgressBar();
                progressBar.SetBounds(20, 312, 680, 18);

                actionButton = new Button();
                actionButton.SetBounds(550, 224, 150, 44);
                actionButton.Click += OnActionClick;
                StyleNeonButton(actionButton, true);

                checkButton = new Button();
                checkButton.Text = "파일 확인";
                checkButton.SetBounds(306, 226, 96, 40);
                checkButton.Click += OnCheckClick;
                StyleNeonButton(checkButton, false);

                uninstallButton = new Button();
                uninstallButton.Text = "제거";
                uninstallButton.SetBounds(420, 226, 96, 40);
                uninstallButton.Click += OnUninstallClick;
                StyleRedButton(uninstallButton);

                Controls.Add(headerTitle);
                Controls.Add(versionLabel);
                Controls.Add(minimizeButton);
                Controls.Add(closeButton);
                Controls.Add(topLine);
                Controls.Add(statusDot);
                Controls.Add(statusLabel);
                Controls.Add(installPathLabel);
                Controls.Add(bannerPanel);
                Controls.Add(optionPanel);
                Controls.Add(progressBar);
                Controls.Add(actionButton);
                Controls.Add(checkButton);
                Controls.Add(uninstallButton);

                RefreshUi();
                if (IsInstalled())
                {
                    BeginCheckForUpdate();
                }
            }

            private void RefreshUi(bool updateStatus)
            {
                bool installed = IsInstalled();
                string installedVersion = GetInstalledVersion();
                bool updateAvailable = installed && latestInfo != null && IsNewerVersion(latestInfo.Version, installedVersion);
                actionButton.Text = updateAvailable
                    ? "업데이트 설치"
                    : installed ? "실행" : "설치";
                uninstallButton.Enabled = installed || HasInstallState();
                statusDot.ForeColor = installed ? CyanColor : NotInstalledColor;
                versionLabel.Visible = installed;

                koreanVoiceCheckBox.Enabled = !installed;
                englishVoiceCheckBox.Enabled = !installed;
                removeBankCheckBox.Enabled = !installed;
                if (!installed)
                {
                    versionLabel.Text = "";
                }
                else if (latestInfo != null && updateAvailable)
                {
                    versionLabel.Text = "v" + installedVersion + " -> v" + latestInfo.Version;
                }
                else
                {
                    versionLabel.Text = "v" + installedVersion;
                }

                // Show StarCraft II path
                string path = ReadInstallPath();
                if (string.IsNullOrEmpty(path))
                {
                    path = FindKnownStarCraft2Path();
                }
                if (!string.IsNullOrEmpty(path))
                {
                    installPathLabel.Text = "📁 " + path;
                    installPathLabel.Visible = true;
                }
                else
                {
                    installPathLabel.Text = "";
                    installPathLabel.Visible = false;
                }

                if (updateStatus)
                {
                    if (!installed)
                    {
                        statusLabel.Text = HasInstallState() ? "설치 파일 확인 필요" : "준비됨";
                    }
                    else if (updateAvailable)
                    {
                        statusLabel.Text = "업데이트 가능: v" + latestInfo.Version;
                    }
                    else
                    {
                        statusLabel.Text = "설치됨";
                    }
                }
            }

            private void RefreshUi()
            {
                RefreshUi(true);
            }

            private static Panel CreatePanel(int x, int y, int width, int height)
            {
                var panel = new Panel();
                panel.BackColor = PanelColor;
                panel.SetBounds(x, y, width, height);
                panel.Paint += delegate(object sender, PaintEventArgs e)
                {
                    using (var pen = new Pen(BorderColor))
                    {
                        e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
                    }
                };
                return panel;
            }

            private static void StyleNeonButton(Button button, bool primary)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = primary ? AccentColor : BorderColor;
                button.BackColor = primary ? Color.FromArgb(0, 55, 40) : Color.FromArgb(9, 22, 29);
                button.ForeColor = primary ? AccentColor : TextColor;
                button.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                button.Cursor = Cursors.Hand;
            }

            private static void StyleRedButton(Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Color.FromArgb(235, 74, 74);
                button.BackColor = Color.FromArgb(45, 12, 12);
                button.ForeColor = Color.FromArgb(255, 120, 120);
                button.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                button.Cursor = Cursors.Hand;
            }


            private static Font CreateTechFont(float size, FontStyle style)
            {
                return new Font("OCR A Extended", size, style, GraphicsUnit.Point);
            }

            private static Button CreateWindowButton(string text)
            {
                var button = new Button();
                button.Text = text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = BorderColor;
                button.BackColor = Color.FromArgb(9, 22, 29);
                button.ForeColor = TextColor;
                button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                button.Cursor = Cursors.Hand;
                button.TabStop = false;
                return button;
            }

            private void DragWindow(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                ReleaseCapture();
                SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
            }

            private void DrawWindowBorder(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(BorderColor))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                }
            }

            private static void OpenChangeLog(object sender, EventArgs e)
            {
                string url = ChangeLogUrl;
                var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] as LauncherForm : null;
                if (form != null && form.latestInfo != null && !string.IsNullOrEmpty(form.latestInfo.ChangeLogUrl))
                {
                    url = form.latestInfo.ChangeLogUrl;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }

            private void BeginCheckForUpdate()
            {
                if (!IsInstalled())
                {
                    latestInfo = null;
                    versionLabel.Text = "";
                    versionLabel.Visible = false;
                    return;
                }

                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        UpdateInfo info = LoadLatestInfo();
                        if (info == null || string.IsNullOrEmpty(info.Version))
                        {
                            return;
                        }

                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (!IsInstalled())
                            {
                                latestInfo = null;
                                versionLabel.Text = "";
                                versionLabel.Visible = false;
                                RefreshUi(true);
                                return;
                            }

                            latestInfo = info;
                            string installedVersion = GetInstalledVersion();
                            if (IsNewerVersion(info.Version, installedVersion))
                            {
                                versionLabel.Text = "v" + installedVersion + " -> v" + info.Version;
                                statusDot.ForeColor = InstallingColor;
                                RefreshUi(true);
                            }
                            else
                            {
                                versionLabel.Text = "v" + installedVersion;
                                RefreshUi(true);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                });
            }

            private void RefreshLatestInfoForInstall()
            {
                try
                {
                    UpdateInfo info = LoadLatestInfo();
                    if (info != null && !string.IsNullOrEmpty(info.Version))
                    {
                        latestInfo = info;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            private static Image LoadBannerImage()
            {
                Stream embedded = typeof(Program).Assembly.GetManifestResourceStream("MassRecallBanner.png");
                if (embedded != null)
                {
                    return Image.FromStream(embedded);
                }

                string bannerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MassRecallBanner.png");
                return File.Exists(bannerPath) ? Image.FromFile(bannerPath) : null;
            }

            private static Icon LoadAppIcon()
            {
                try
                {
                    Stream embedded = typeof(Program).Assembly.GetManifestResourceStream("icon.ico");
                    if (embedded != null)
                    {
                        return new Icon(embedded);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                try
                {
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                    if (File.Exists(iconPath))
                    {
                        return new Icon(iconPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                return null;
            }

            [DllImport("user32.dll")]
            private static extern bool ReleaseCapture();

            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

            private void OnLanguageOptionChanged(object sender, EventArgs e)
            {
                if (suppressLanguageOptionChange)
                {
                    return;
                }

                suppressLanguageOptionChange = true;
                try
                {
                    if (sender == koreanVoiceCheckBox && koreanVoiceCheckBox.Checked)
                    {
                        englishVoiceCheckBox.Checked = false;
                    }
                    else if (sender == englishVoiceCheckBox && englishVoiceCheckBox.Checked)
                    {
                        koreanVoiceCheckBox.Checked = false;
                    }

                    bool installed = IsInstalled();
                    koreanVoiceCheckBox.Enabled = !installed;
                    englishVoiceCheckBox.Enabled = !installed;
                }
                finally
                {
                    suppressLanguageOptionChange = false;
                }
            }

            private void RestoreLanguageOptionState()
            {
                bool installed = IsInstalled();
                koreanVoiceCheckBox.Enabled = !installed;
                englishVoiceCheckBox.Enabled = !installed;
                OnLanguageOptionChanged(null, EventArgs.Empty);
            }
            private void OnActionClick(object sender, EventArgs e)
            {
                if (IsInstalled() && !HasUpdateAvailable())
                {
                    LaunchInstalledMassRecall();
                    return;
                }

                if (!koreanVoiceCheckBox.Checked && !englishVoiceCheckBox.Checked)
                {
                    koreanVoiceCheckBox.Checked = true;
                }

                actionButton.Enabled = false;
                checkButton.Enabled = false;
                uninstallButton.Enabled = false;
                koreanVoiceCheckBox.Enabled = false;
                englishVoiceCheckBox.Enabled = false;
                removeBankCheckBox.Enabled = false;
                progressBar.Value = 0;
                statusDot.ForeColor = InstallingColor;

                try
                {
                    string installPath = FindStarCraft2Path();
                    bool installEnglishVoice = englishVoiceCheckBox.Checked;
                    bool removeBankFiles = removeBankCheckBox.Checked;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        InstallInBackground(installPath, installEnglishVoice, removeBankFiles);
                    });
                }
                catch (Exception ex)
                {
                    FinishBusyState();
                    statusDot.ForeColor = IsInstalled() ? CyanColor : NotInstalledColor;
                    statusLabel.Text = "실패";
                    MessageBox.Show(this, ex.Message, "런처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            private void InstallInBackground(string installPath, bool installEnglishVoice, bool removeBankFiles)
            {
                try
                {
                    lock (InstalledItemsLock)
                    {
                        InstalledItems.Clear();
                    }

                    RefreshLatestInfoForInstall();
                    if (removeBankFiles)
                    {
                        SetStatus("SCMR.SC2Bank 제거 중...");
                        RemoveScmrBankFiles();
                    }
                    InstallPackages(installPath, installEnglishVoice);
                    RefreshLatestInfoForInstall();
                    SaveState(installPath);
                    RunOnUi(delegate
                    {
                        progressBar.Value = 100;
                        RefreshUi(false);
                        statusLabel.Text = "설치 완료: " + installPath;
                        BeginCheckForUpdate();
                    });
                }
                catch (Exception ex)
                {
                    RunOnUi(delegate
                    {
                        statusDot.ForeColor = IsInstalled() ? CyanColor : NotInstalledColor;
                        statusLabel.Text = "실패";
                        MessageBox.Show(this, ex.Message, "런처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
                finally
                {
                    RunOnUi(FinishBusyState);
                }
            }

            private void LaunchInstalledMassRecall()
            {
                try
                {
                    string installPath = ReadInstallPath();
                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                    {
                        throw new InvalidOperationException("설치 경로를 찾지 못했습니다. 파일 확인을 실행하거나 다시 설치하세요.");
                    }

                    string targetPath = FindSwitcherPath(installPath);
                    string mapPath = FindCampaignLauncherMap(installPath);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetPath,
                        Arguments = "\"" + mapPath + "\"",
                        WorkingDirectory = Path.GetDirectoryName(targetPath),
                        UseShellExecute = false
                    });
                    statusLabel.Text = "실행됨";
                }
                catch (Exception ex)
                {
                    statusDot.ForeColor = NotInstalledColor;
                    statusLabel.Text = "실행 실패";
                    MessageBox.Show(this, ex.Message, "런처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    RefreshUi(false);
                }
            }
            private bool HasUpdateAvailable()
            {
                return IsInstalled() &&
                    latestInfo != null &&
                    IsNewerVersion(latestInfo.Version, GetInstalledVersion());
            }

            private void OnCheckClick(object sender, EventArgs e)
            {
                RefreshUi(false);
                if (IsInstalled())
                {
                    statusDot.ForeColor = CyanColor;
                    statusLabel.Text = "파일 확인 완료";
                    MessageBox.Show(this, "설치된 파일이 정상입니다.", "파일 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string detectedInstallPath;
                if (TryDetectExistingInstall(out detectedInstallPath))
                {
                    SaveDetectedState(detectedInstallPath);
                    RefreshUi(false);
                    statusDot.ForeColor = CyanColor;
                    statusLabel.Text = "파일 확인 완료";
                    MessageBox.Show(this, "파일 확인이 완료되었습니다.", "파일 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                statusDot.ForeColor = NotInstalledColor;
                if (HasInstallState())
                {
                    statusLabel.Text = "설치 파일 확인 필요";
                    MessageBox.Show(this, "설치 파일이 일부 누락되었습니다. 설치 버튼을 눌러 다시 설치하세요.", "파일 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                statusLabel.Text = "준비됨";
                MessageBox.Show(this, "설치 기록이 없습니다.", "파일 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            private void OnUninstallClick(object sender, EventArgs e)
            {
                DialogResult result = MessageBox.Show(
                    this,
                    "파일을 제거할까요?",
                    "제거",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    return;
                }

                actionButton.Enabled = false;
                checkButton.Enabled = false;
                uninstallButton.Enabled = false;
                koreanVoiceCheckBox.Enabled = false;
                englishVoiceCheckBox.Enabled = false;
                removeBankCheckBox.Enabled = false;
                progressBar.Value = 0;
                statusDot.ForeColor = InstallingColor;

                ThreadPool.QueueUserWorkItem(delegate
                {
                    UninstallInBackground();
                });
            }

            private void UninstallInBackground()
            {
                try
                {
                    SetStatus("제거 중...");
                    UninstallMassRecall();
                    RunOnUi(delegate
                    {
                        progressBar.Value = 100;
                        RefreshUi(false);
                        statusLabel.Text = "제거 완료";
                    });
                }
                catch (Exception ex)
                {
                    RunOnUi(delegate
                    {
                        statusDot.ForeColor = IsInstalled() ? CyanColor : NotInstalledColor;
                        statusLabel.Text = "실패";
                        MessageBox.Show(this, ex.Message, "런처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
                finally
                {
                    RunOnUi(FinishBusyState);
                }
            }

            private void FinishBusyState()
            {
                actionButton.Enabled = true;
                checkButton.Enabled = true;
                uninstallButton.Enabled = true;
                RestoreLanguageOptionState();
                removeBankCheckBox.Enabled = !IsInstalled();
                RefreshUi(false);
            }

            private void InstallPackages(string installPath, bool installEnglishVoice)
            {
                Package voicePackage = installEnglishVoice ? Packages[2] : Packages[1];

                SetDownloadProgress(0);
                string mainArchivePath = DownloadPackage(Packages[0], 0, 45);

                ExtractMainPackage(mainArchivePath, installPath, installEnglishVoice);
                TryDeleteFile(mainArchivePath);
                SetProgress(50);

                SetDownloadProgress(50);
                string voiceArchivePath = DownloadPackage(voicePackage, 50, 45);

                ExtractVoicePackage(voicePackage, voiceArchivePath, installPath);
                TryDeleteFile(voiceArchivePath);
                SetProgress(100);
            }

            private void SetStatus(string text)
            {
                RunOnUi(delegate { statusLabel.Text = text; });
            }

            private void SetProgress(int value)
            {
                int percent = Math.Max(0, Math.Min(100, value));
                RunOnUi(delegate { progressBar.Value = percent; });
            }

            private void SetDownloadProgress(int value)
            {
                int percent = Math.Max(0, Math.Min(100, value));
                RunOnUi(delegate
                {
                    progressBar.Value = percent;
                    statusLabel.Text = "다운로드 중... " + percent + "%";
                });
            }

            private void RunOnUi(MethodInvoker action)
            {
                if (IsDisposed)
                {
                    return;
                }

                if (InvokeRequired)
                {
                    BeginInvoke(action);
                    return;
                }

                action();
            }

            private string DownloadPackage(Package package, int basePercent, int rangePercent)
            {
                Directory.CreateDirectory(CacheDir);
                string target = Path.Combine(CacheDir, package.FileName);
                DownloadToFile(GetPackageUrl(package), target, basePercent, rangePercent);
                VerifyPackageSha256(package, target);

                if (!LooksLikeZip(target))
                {
                    throw new InvalidOperationException(
                        package.Name + "이(가) 올바른 zip 파일로 다운로드되지 않았습니다. GitHub Release 파일을 확인하세요.");
                }

                return target;
            }

            private string GetPackageUrl(Package package)
            {
                if (latestInfo != null)
                {
                    if (package == Packages[0] && !string.IsNullOrEmpty(latestInfo.MainUrl))
                    {
                        return latestInfo.MainUrl;
                    }

                    if (package == Packages[1] && !string.IsNullOrEmpty(latestInfo.KoreanVoiceUrl))
                    {
                        return latestInfo.KoreanVoiceUrl;
                    }

                    if (package == Packages[2])
                    {
                        if (!string.IsNullOrEmpty(latestInfo.EnglishVoiceUrl))
                        {
                            return latestInfo.EnglishVoiceUrl;
                        }

                        if (!string.IsNullOrEmpty(latestInfo.VoiceUrl))
                        {
                            return latestInfo.VoiceUrl;
                        }
                    }
                }

                return package.Url;
            }

            private void DownloadToFile(
                string url,
                string target,
                int basePercent,
                int rangePercent)
            {
                EnsureHttpsUrl(url);
                using (HttpClient client = CreateHttpClient())
                using (HttpResponseMessage response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    response.EnsureSuccessStatusCode();
                    EnsureHttpsUrl(response.RequestMessage.RequestUri.AbsoluteUri);
                    using (var input = response.Content.ReadAsStreamAsync().Result)
                using (var output = File.Create(target))
                {
                    long total = response.Content.Headers.ContentLength.HasValue
                        ? response.Content.Headers.ContentLength.Value
                        : -1;
                    long downloaded = 0;
                    byte[] buffer = new byte[256 * 1024];

                    while (true)
                    {
                        int read = input.Read(buffer, 0, buffer.Length);
                        if (read <= 0)
                        {
                            break;
                        }

                        output.Write(buffer, 0, read);
                        downloaded += read;

                        if (total > 0)
                        {
                            int percent = basePercent + (int)((downloaded / (double)total) * rangePercent);
                            SetDownloadProgress(percent);
                        }
                    }
                }
                }
            }

            private void VerifyPackageSha256(Package package, string path)
            {
                string expected = GetPackageSha256(package);
                if (string.IsNullOrEmpty(expected))
                {
                    return;
                }

                expected = NormalizeSha256(expected);
                if (expected.Length != 64)
                {
                    throw new InvalidOperationException(package.Name + " SHA256 값 형식이 올바르지 않습니다.");
                }

                string actual = ComputeSha256(path);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(package.Name + " SHA256 검증에 실패했습니다.");
                }
            }

            private string GetPackageSha256(Package package)
            {
                if (latestInfo == null)
                {
                    return "";
                }

                if (package == Packages[0])
                {
                    return latestInfo.MainSha256;
                }

                if (package == Packages[1])
                {
                    return latestInfo.KoreanVoiceSha256;
                }

                if (package == Packages[2])
                {
                    return !string.IsNullOrEmpty(latestInfo.EnglishVoiceSha256)
                        ? latestInfo.EnglishVoiceSha256
                        : latestInfo.VoiceSha256;
                }

                return "";
            }

            private static string NormalizeSha256(string value)
            {
                return value.Trim().Replace(" ", "").Replace("-", "").ToLowerInvariant();
            }

            private static string ComputeSha256(string path)
            {
                using (var sha256 = SHA256.Create())
                using (var input = File.OpenRead(path))
                {
                    byte[] hash = sha256.ComputeHash(input);
                    var builder = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash)
                    {
                        builder.Append(b.ToString("x2"));
                    }

                    return builder.ToString();
                }
            }

            private static UpdateInfo LoadLatestInfo()
            {
                string json = DownloadText(LatestInfoUrl);

                return new UpdateInfo
                {
                    Version = ReadJsonString(json, "version"),
                    MainUrl = ReadJsonString(json, "main_url"),
                    KoreanVoiceUrl = ReadJsonString(json, "korean_voice_url"),
                    EnglishVoiceUrl = ReadJsonString(json, "english_voice_url"),
                    VoiceUrl = ReadJsonString(json, "voice_url"),
                    MainSha256 = ReadJsonString(json, "main_sha256"),
                    KoreanVoiceSha256 = ReadJsonString(json, "korean_voice_sha256"),
                    EnglishVoiceSha256 = ReadJsonString(json, "english_voice_sha256"),
                    VoiceSha256 = ReadJsonString(json, "voice_sha256"),
                    ChangeLogUrl = ReadJsonString(json, "changelog_url")
                };
            }

            private static string DownloadText(string url)
            {
                EnsureHttpsUrl(url);
                using (HttpClient client = CreateHttpClient())
                using (HttpResponseMessage response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    response.EnsureSuccessStatusCode();
                    EnsureHttpsUrl(response.RequestMessage.RequestUri.AbsoluteUri);
                    using (var input = response.Content.ReadAsStreamAsync().Result)
                    using (var reader = new StreamReader(input, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }

            private static HttpClient CreateHttpClient()
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true
                };
                var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(HttpUserAgent);
                return client;
            }

            private static void EnsureHttpsUrl(string url)
            {
                Uri uri;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                    !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("HTTPS 다운로드 URL만 허용됩니다: " + url);
                }
            }

            private static string ReadJsonString(string json, string key)
            {
                Match match = Regex.Match(
                    json,
                    "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"",
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    return "";
                }

                return Regex.Unescape(match.Groups[1].Value);
            }

            private static bool IsNewerVersion(string onlineVersion, string currentVersion)
            {
                System.Version online;
                System.Version current;
                if (System.Version.TryParse(NormalizeVersion(onlineVersion), out online) &&
                    System.Version.TryParse(NormalizeVersion(currentVersion), out current))
                {
                    return online > current;
                }

                return string.Compare(onlineVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
            }

            private static string NormalizeVersion(string version)
            {
                if (string.IsNullOrWhiteSpace(version))
                {
                    return "0.0.0";
                }

                return version.Trim().TrimStart('v', 'V');
            }

            private static bool LooksLikeZip(string path)
            {
                byte[] buffer = new byte[4];
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Read(buffer, 0, buffer.Length) < 4)
                    {
                        return false;
                    }
                }

                return buffer[0] == 0x50 && buffer[1] == 0x4B;
            }

            private static void ExtractMainPackage(string archivePath, string installPath, bool skipEnglishVoiceFiles)
            {
                string tempExtract = ExtractArchiveToTemp(Packages[0], archivePath);
                try
                {
                    string[] excludedModRoots = skipEnglishVoiceFiles ? new[] { "Assets", "Local" } : null;
                    CopyNamedDirectory(tempExtract, installPath, new[] { "Mods", "Mod", "mods", "mod" }, "Mods", excludedModRoots);
                    CopyNamedDirectory(tempExtract, installPath, new[] { "Maps", "Map", "maps", "map" }, "Maps");
                }
                finally
                {
                    TryDeleteDirectory(tempExtract);
                }
            }

            private static void ExtractVoicePackage(Package package, string archivePath, string installPath)
            {
                string tempExtract = ExtractArchiveToTemp(package, archivePath);
                try
                {
                    string modsTarget = Path.Combine(installPath, "Mods");
                    Directory.CreateDirectory(modsTarget);

                    string modsSource = FindFirstDirectory(tempExtract, new[] { "Mods", "Mod", "mods", "mod" });
                    CopyVoiceFilesToMods(package, modsSource ?? tempExtract, modsTarget);
                }
                finally
                {
                    TryDeleteDirectory(tempExtract);
                }
            }

            private static void CopyVoiceFilesToMods(Package package, string sourceRoot, string modsTarget)
            {
                List<VoicePackageEntry> voiceEntries = FindVoicePackageEntries(sourceRoot);
                if (voiceEntries.Count != 2)
                {
                    throw new InvalidOperationException(
                        package.Name + " 압축파일에서 Mods로 복사할 음성 항목 2개를 찾지 못했습니다. 찾은 항목 수: " + voiceEntries.Count);
                }

                var copiedNames = new List<string>();
                foreach (VoicePackageEntry entry in voiceEntries)
                {
                    string target = Path.Combine(modsTarget, entry.Name);
                    if (entry.IsDirectory)
                    {
                        Directory.CreateDirectory(target);
                        CopyDirectoryContents(entry.Path, target);
                    }
                    else
                    {
                        File.Copy(entry.Path, target, true);
                        AddInstalledItem(target);
                    }

                    copiedNames.Add(entry.Name);
                }

                DeleteVoiceWrapperDirectories(modsTarget, copiedNames);
            }

            private static List<VoicePackageEntry> FindVoicePackageEntries(string sourceRoot)
            {
                string payloadRoot = FindVoicePayloadRoot(sourceRoot);
                var entries = new List<VoicePackageEntry>();

                foreach (string directory in Directory.GetDirectories(payloadRoot))
                {
                    if (IsLikelyVoiceEntry(directory))
                    {
                        entries.Add(new VoicePackageEntry(directory, true));
                    }
                }

                foreach (string file in Directory.GetFiles(payloadRoot))
                {
                    if (IsLikelyVoiceEntry(file))
                    {
                        entries.Add(new VoicePackageEntry(file, false));
                    }
                }

                if (entries.Count > 0)
                {
                    return entries;
                }

                foreach (string file in Directory.GetFiles(payloadRoot))
                {
                    if (!IsAuxiliaryPackageFile(file))
                    {
                        entries.Add(new VoicePackageEntry(file, false));
                    }
                }

                return entries;
            }

            private static string FindVoicePayloadRoot(string sourceRoot)
            {
                string current = sourceRoot;
                while (true)
                {
                    if (HasLikelyVoiceEntry(current) || HasNonAuxiliaryFiles(current))
                    {
                        return current;
                    }

                    string[] directories = Directory.GetDirectories(current);
                    if (directories.Length != 1)
                    {
                        return current;
                    }

                    current = directories[0];
                }
            }

            private static bool HasLikelyVoiceEntry(string root)
            {
                foreach (string directory in Directory.GetDirectories(root))
                {
                    if (IsLikelyVoiceEntry(directory))
                    {
                        return true;
                    }
                }

                foreach (string file in Directory.GetFiles(root))
                {
                    if (IsLikelyVoiceEntry(file))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool HasNonAuxiliaryFiles(string root)
            {
                foreach (string file in Directory.GetFiles(root))
                {
                    if (!IsAuxiliaryPackageFile(file))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsLikelyVoiceEntry(string path)
            {
                string fileName = Path.GetFileName(path);
                return fileName.EndsWith(".SC2Mod", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("Assets", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("Local", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsAuxiliaryPackageFile(string path)
            {
                string fileName = Path.GetFileName(path);
                string extension = Path.GetExtension(path);
                return fileName.Equals("README", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".htm", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".url", StringComparison.OrdinalIgnoreCase);
            }

            private static void DeleteVoiceWrapperDirectories(string modsTarget, List<string> copiedNames)
            {
                var expectedNames = new HashSet<string>(copiedNames, StringComparer.OrdinalIgnoreCase);
                foreach (string directory in Directory.GetDirectories(modsTarget))
                {
                    if (Directory.GetCreationTimeUtc(directory) < ProcessStartUtc.AddSeconds(-5))
                    {
                        continue;
                    }

                    if (Directory.GetDirectories(directory, "*", SearchOption.AllDirectories).Length > 0)
                    {
                        continue;
                    }

                    string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                    if (files.Length != expectedNames.Count)
                    {
                        continue;
                    }

                    bool containsOnlyCopiedFiles = true;
                    foreach (string file in files)
                    {
                        if (!expectedNames.Contains(Path.GetFileName(file)))
                        {
                            containsOnlyCopiedFiles = false;
                            break;
                        }
                    }

                    if (containsOnlyCopiedFiles)
                    {
                        TryDeleteDirectory(directory);
                    }
                }
            }

            private static string ExtractArchiveToTemp(Package package, string archivePath)
            {
                Directory.CreateDirectory(CacheDir);
                string tempExtract = Path.Combine(CacheDir, "extract-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempExtract);

                try
                {
                    ExtractArchive(package, archivePath, tempExtract);
                    return tempExtract;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    TryDeleteDirectory(tempExtract);
                    throw;
                }
            }

            private static void ExtractArchive(Package package, string archivePath, string extractRoot)
            {
                string extension = Path.GetExtension(archivePath).ToLowerInvariant();
                if (extension != ".zip")
                {
                    throw new InvalidOperationException("지원하지 않는 압축 형식입니다: " + package.Name + " / " + extension);
                }

                try
                {
                    ExtractZipSafely(archivePath, extractRoot);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        package.Name + " ZIP 압축 해제에 실패했습니다: " + ex.Message,
                        ex);
                }
            }

            private static void ExtractZipSafely(string archivePath, string extractRoot)
            {
                string normalizedRoot = Path.GetFullPath(extractRoot);
                if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    normalizedRoot += Path.DirectorySeparatorChar;
                }

                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string entryName = NormalizeZipEntryName(entry.FullName);
                        if (string.IsNullOrWhiteSpace(entryName))
                        {
                            continue;
                        }

                        if (IsUnsafeZipEntryName(entryName))
                        {
                            throw new InvalidOperationException("ZIP 경로가 안전하지 않습니다: " + entry.FullName);
                        }

                        string destination = Path.GetFullPath(Path.Combine(normalizedRoot, entryName));
                        if (!destination.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("ZIP 경로가 압축 해제 폴더 밖을 가리킵니다: " + entry.FullName);
                        }

                        if (entryName.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        {
                            Directory.CreateDirectory(destination);
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        entry.ExtractToFile(destination, true);
                    }
                }
            }

            private static string NormalizeZipEntryName(string entryName)
            {
                return (entryName ?? "")
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Trim();
            }

            private static bool IsUnsafeZipEntryName(string entryName)
            {
                if (string.IsNullOrWhiteSpace(entryName) ||
                    Path.IsPathRooted(entryName) ||
                    entryName.IndexOf(Path.VolumeSeparatorChar) >= 0)
                {
                    return true;
                }

                string[] parts = entryName.Split(Path.DirectorySeparatorChar);
                foreach (string part in parts)
                {
                    if (part == "..")
                    {
                        return true;
                    }
                }

                return false;
            }

            private static void CopyNamedDirectory(string searchRoot, string installPath, string[] names, string targetName)
            {
                CopyNamedDirectory(searchRoot, installPath, names, targetName, null);
            }

            private static void CopyNamedDirectory(string searchRoot, string installPath, string[] names, string targetName, string[] excludedRootNames)
            {
                string source = FindFirstDirectory(searchRoot, names);
                if (source == null)
                {
                    throw new InvalidOperationException("메인 압축파일에서 " + targetName + " 폴더를 찾지 못했습니다.");
                }

                string destination = Path.Combine(installPath, targetName);
                Directory.CreateDirectory(destination);
                CopyDirectoryContents(source, destination, excludedRootNames);
            }

            private static string FindFirstDirectory(string root, string[] names)
            {
                var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
                foreach (string directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                {
                    if (wanted.Contains(Path.GetFileName(directory)))
                    {
                        return directory;
                    }
                }

                return null;
            }

            private static void CopyDirectoryContents(string source, string destination)
            {
                CopyDirectoryContents(source, destination, null);
            }

            private static void CopyDirectoryContents(string source, string destination, string[] excludedRootNames)
            {
                var excludedRoots = excludedRootNames == null
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(excludedRootNames, StringComparer.OrdinalIgnoreCase);

                foreach (string directory in Directory.GetDirectories(source))
                {
                    if (excludedRoots.Contains(Path.GetFileName(directory)))
                    {
                        continue;
                    }

                    AddInstalledItem(Path.Combine(destination, Path.GetFileName(directory)));
                }

                foreach (string file in Directory.GetFiles(source))
                {
                    if (IsExcludedByRoot(source, file, excludedRoots))
                    {
                        continue;
                    }

                    AddInstalledItem(Path.Combine(destination, Path.GetFileName(file)));
                }

                foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                {
                    if (IsExcludedByRoot(source, directory, excludedRoots))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.Combine(destination, GetRelativePath(source, directory)));
                }

                foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    if (IsExcludedByRoot(source, file, excludedRoots))
                    {
                        continue;
                    }

                    string target = Path.Combine(destination, GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, true);
                    AddInstalledItem(target);
                }
            }

            private static string GetRelativePath(string root, string path)
            {
                string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string normalizedPath = Path.GetFullPath(path);
                string rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
                if (!normalizedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("복사 대상 경로가 원본 폴더 밖에 있습니다: " + path);
                }

                return normalizedPath.Substring(rootWithSeparator.Length);
            }

            private static bool IsExcludedByRoot(string source, string path, HashSet<string> excludedRoots)
            {
                if (excludedRoots.Count == 0)
                {
                    return false;
                }

                string relative = GetRelativePath(source, path);
                int separator = relative.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                string rootName = separator >= 0 ? relative.Substring(0, separator) : relative;
                return excludedRoots.Contains(rootName);
            }

            private static void AddInstalledItem(string path)
            {
                string fullPath = Path.GetFullPath(path);
                lock (InstalledItemsLock)
                {
                    InstalledItems.Add(fullPath);
                }
            }

            private static void TryDeleteDirectory(string path)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        DeleteDirectorySafely(path, true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            private static void TryDeleteFile(string path)
            {
                try
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        FileAttributes attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(path, FileAttributes.Normal);
                        }
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            private static void DeleteInstalledPath(string path)
            {
                try
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        return;
                    }

                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        DeleteDirectorySafely(path, true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            private static void DeleteInstalledPath(string installPath, string path)
            {
                if (!IsSafeUninstallTarget(installPath, path))
                {
                    Debug.WriteLine("Skipped unsafe uninstall target: " + path);
                    return;
                }

                DeleteInstalledPath(path);
            }

            private static void DeleteDirectorySafely(string path, bool recursive)
            {
                if (IsReparsePoint(path))
                {
                    Debug.WriteLine("Deleting directory reparse point without recursion: " + path);
                    Directory.Delete(path, false);
                    return;
                }

                if (recursive)
                {
                    try
                    {
                        var di = new DirectoryInfo(path);
                        if (di.Exists)
                        {
                            foreach (FileInfo file in di.GetFiles("*", SearchOption.AllDirectories))
                            {
                                if ((file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                {
                                    file.Attributes = FileAttributes.Normal;
                                }
                            }
                            foreach (DirectoryInfo dir in di.GetDirectories("*", SearchOption.AllDirectories))
                            {
                                if ((dir.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                {
                                    dir.Attributes = FileAttributes.Normal;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Failed to strip ReadOnly attributes: " + ex.Message);
                    }
                }

                Directory.Delete(path, recursive);
            }

            private static bool IsReparsePoint(string path)
            {
                try
                {
                    return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    return false;
                }
            }

            private static void UninstallMassRecall()
            {
                string installPath = ReadInstallPath();
                string manifestPath = Path.Combine(StateDir, "manifest.txt");
                var paths = new List<string>();

                if (File.Exists(manifestPath))
                {
                    foreach (string line in File.ReadAllLines(manifestPath))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            paths.Add(line.Trim());
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(installPath))
                {
                    AddFallbackUninstallPaths(installPath, paths);
                }

                paths.Sort((left, right) => right.Length.CompareTo(left.Length));
                foreach (string path in paths)
                {
                    DeleteInstalledPath(installPath, path);
                }

                TryDeleteFile(Path.Combine(StateDir, "state.txt"));
                TryDeleteFile(Path.Combine(StateDir, "install-path.txt"));
                TryDeleteFile(manifestPath);
            }

            private static bool IsSafeUninstallTarget(string installPath, string path)
            {
                if (string.IsNullOrEmpty(installPath) || string.IsNullOrEmpty(path))
                {
                    return false;
                }

                string normalizedInstall = Path.GetFullPath(installPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string normalizedPath = Path.GetFullPath(path);
                string modsRoot = Path.Combine(normalizedInstall, "Mods") + Path.DirectorySeparatorChar;
                string mapsRoot = Path.Combine(normalizedInstall, "Maps") + Path.DirectorySeparatorChar;

                return normalizedPath.StartsWith(modsRoot, StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.StartsWith(mapsRoot, StringComparison.OrdinalIgnoreCase);
            }

            private static void AddFallbackUninstallPaths(string installPath, List<string> paths)
            {
                string massRecallMaps = Path.Combine(installPath, @"Maps\Starcraft Mass Recall");
                if (Directory.Exists(massRecallMaps))
                {
                    paths.Add(massRecallMaps);
                }

                string modsRoot = Path.Combine(installPath, "Mods");
                if (!Directory.Exists(modsRoot))
                {
                    return;
                }

                foreach (string entry in Directory.GetFileSystemEntries(modsRoot))
                {
                    string name = Path.GetFileName(entry);
                    if (name.IndexOf("SCMR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("MassRecall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Mass Recall", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        paths.Add(entry);
                    }
                }
            }

            private static void RemoveScmrBankFiles()
            {
                string documentsSc2 = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "StarCraft II");

                if (!Directory.Exists(documentsSc2))
                {
                    return;
                }

                foreach (string bankFile in Directory.GetFiles(documentsSc2, "SCMR.SC2Bank", SearchOption.AllDirectories))
                {
                    TryDeleteFile(bankFile);
                }
            }

            private static string FindSwitcherPath(string installPath)
            {
                string[] candidates =
                {
                    Path.Combine(installPath, @"Support64\SC2Switcher_x64.exe"),
                    Path.Combine(installPath, @"Support\SC2Switcher.exe"),
                    Path.Combine(installPath, "StarCraft II.exe")
                };

                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                throw new InvalidOperationException("StarCraft II 실행 파일을 찾지 못했습니다.");
            }

            private static string FindCampaignLauncherMap(string installPath)
            {
                string mapsRoot = Path.Combine(installPath, "Maps");
                string expected = Path.Combine(mapsRoot, @"Starcraft Mass Recall\SCMR Campaign Launcher.SC2Map");
                if (File.Exists(expected))
                {
                    return expected;
                }

                if (Directory.Exists(mapsRoot))
                {
                    string[] matches = Directory.GetFiles(mapsRoot, "SCMR Campaign Launcher.SC2Map", SearchOption.AllDirectories);
                    if (matches.Length > 0)
                    {
                        Array.Sort(matches, delegate(string left, string right)
                        {
                            return File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left));
                        });
                        return matches[0];
                    }
                }

                throw new InvalidOperationException("설치된 Maps 폴더에서 SCMR Campaign Launcher.SC2Map을 찾지 못했습니다.");
            }

            private static string FindStarCraft2Path()
            {
                string knownPath = FindKnownStarCraft2Path();
                if (!string.IsNullOrEmpty(knownPath))
                {
                    return knownPath;
                }

                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "StarCraft II 설치 폴더를 선택하세요.";
                    dialog.ShowNewFolderButton = false;
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        return dialog.SelectedPath;
                    }
                }

                throw new InvalidOperationException("StarCraft II 설치 폴더가 선택되지 않았습니다.");
            }

            private static string FindKnownStarCraft2Path()
            {
                string[] candidates =
                {
                    @"C:\Program Files (x86)\StarCraft II",
                    @"C:\Program Files\StarCraft II",
                    @"D:\Program Files (x86)\StarCraft II",
                    @"D:\Program Files\StarCraft II",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "StarCraft II")
                };

                foreach (string candidate in candidates)
                {
                    if (IsStarCraft2Path(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }

                return null;
            }

            private static bool IsStarCraft2Path(string path)
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    return false;
                }

                return File.Exists(Path.Combine(path, "StarCraft II.exe")) ||
                    File.Exists(Path.Combine(path, @"Support64\SC2Switcher_x64.exe")) ||
                    File.Exists(Path.Combine(path, @"Support\SC2Switcher.exe"));
            }

            private static bool IsInstalled()
            {
                return !string.IsNullOrEmpty(GetInstalledVersion()) && HasInstalledFiles();
            }

            private static bool HasInstallState()
            {
                return File.Exists(Path.Combine(StateDir, "state.txt"));
            }

            private static bool HasInstalledFiles()
            {
                string installPath = ReadInstallPath();
                if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                {
                    return false;
                }

                string manifestPath = Path.Combine(StateDir, "manifest.txt");
                if (!File.Exists(manifestPath))
                {
                    return HasMassRecallFiles(installPath);
                }

                bool hasEntries = false;
                bool allEntriesExist = true;
                foreach (string line in File.ReadAllLines(manifestPath))
                {
                    string path = line.Trim();
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    hasEntries = true;
                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        allEntriesExist = false;
                        break;
                    }
                }

                if (hasEntries && allEntriesExist)
                {
                    return true;
                }

                return HasMassRecallFiles(installPath);
            }

            private static bool TryDetectExistingInstall(out string installPath)
            {
                installPath = null;

                string knownPath = FindKnownStarCraft2Path();
                if (!string.IsNullOrEmpty(knownPath) && HasMassRecallFiles(knownPath))
                {
                    installPath = knownPath;
                    return true;
                }

                return false;
            }

            private static bool HasMassRecallFiles(string installPath)
            {
                if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                {
                    return false;
                }

                try
                {
                    FindCampaignLauncherMap(installPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    return false;
                }

                return HasMassRecallMods(installPath);
            }

            private static bool HasMassRecallMods(string installPath)
            {
                string modsRoot = Path.Combine(installPath, "Mods");
                if (!Directory.Exists(modsRoot))
                {
                    return false;
                }

                foreach (string entry in Directory.GetFileSystemEntries(modsRoot))
                {
                    string name = Path.GetFileName(entry);
                    if (name.IndexOf("SCMR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("MassRecall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Mass Recall", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static string GetInstalledVersion()
            {
                string statePath = Path.Combine(StateDir, "state.txt");
                if (!File.Exists(statePath))
                {
                    return "";
                }

                string installedVersion = File.ReadAllText(statePath).Trim();
                if (string.Equals(installedVersion, LegacyPlaceholderVersion, StringComparison.OrdinalIgnoreCase))
                {
                    TryWriteAllText(statePath, Version);
                    return Version;
                }

                return installedVersion;
            }

            private static void TryWriteAllText(string path, string text)
            {
                try
                {
                    File.WriteAllText(path, text);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            private void SaveState(string installPath)
            {
                Directory.CreateDirectory(StateDir);
                string installedVersion = latestInfo != null && !string.IsNullOrEmpty(latestInfo.Version)
                    ? latestInfo.Version
                    : Version;
                File.WriteAllText(Path.Combine(StateDir, "state.txt"), installedVersion);
                File.WriteAllText(Path.Combine(StateDir, "install-path.txt"), installPath);
                string[] installedItems;
                lock (InstalledItemsLock)
                {
                    installedItems = new string[InstalledItems.Count];
                    InstalledItems.CopyTo(installedItems);
                }

                File.WriteAllLines(Path.Combine(StateDir, "manifest.txt"), DistinctPaths(installedItems));
            }

            private static void SaveDetectedState(string installPath)
            {
                Directory.CreateDirectory(StateDir);
                File.WriteAllText(Path.Combine(StateDir, "state.txt"), Version);
                File.WriteAllText(Path.Combine(StateDir, "install-path.txt"), installPath);
                var detectedPaths = new List<string>();
                AddFallbackUninstallPaths(installPath, detectedPaths);
                if (detectedPaths.Count > 0)
                {
                    File.WriteAllLines(Path.Combine(StateDir, "manifest.txt"), DistinctPaths(detectedPaths.ToArray()));
                }
                else
                {
                    TryDeleteFile(Path.Combine(StateDir, "manifest.txt"));
                }
            }

            private static string[] DistinctPaths(string[] paths)
            {
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var result = new List<string>();
                foreach (string path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    string normalized = Path.GetFullPath(path.Trim());
                    if (unique.Add(normalized))
                    {
                        result.Add(normalized);
                    }
                }

                return result.ToArray();
            }

            private static string ReadInstallPath()
            {
                string path = Path.Combine(StateDir, "install-path.txt");
                if (!File.Exists(path))
                {
                    return null;
                }

                return File.ReadAllText(path).Trim();
            }

        }

        private sealed class Package
        {
            public readonly string Name;
            public readonly string Url;
            public readonly string FileName;

            public Package(string name, string url, string fileName)
            {
                Name = name;
                Url = url;
                FileName = fileName;
            }
        }

        private sealed class VoicePackageEntry
        {
            public readonly string Path;
            public readonly string Name;
            public readonly bool IsDirectory;

            public VoicePackageEntry(string path, bool isDirectory)
            {
                Path = path;
                Name = System.IO.Path.GetFileName(path);
                IsDirectory = isDirectory;
            }
        }

        private sealed class UpdateInfo
        {
            public string Version;
            public string MainUrl;
            public string KoreanVoiceUrl;
            public string EnglishVoiceUrl;
            public string VoiceUrl;
            public string MainSha256;
            public string KoreanVoiceSha256;
            public string EnglishVoiceSha256;
            public string VoiceSha256;
            public string ChangeLogUrl;
        }

        private sealed class TechProgressBar : Control
        {
            private int _value;
            public int Value
            {
                get { return _value; }
                set
                {
                    _value = Math.Max(0, Math.Min(100, value));
                    Invalidate();
                }
            }

            public TechProgressBar()
            {
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var bgBrush = new SolidBrush(Color.FromArgb(9, 22, 29)))
                {
                    e.Graphics.FillRectangle(bgBrush, ClientRectangle);
                }
                using (var borderPen = new Pen(Color.FromArgb(21, 58, 73)))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
                }
                if (_value > 0)
                {
                    int fillWidth = (int)((Width - 2) * (_value / 100.0));
                    if (fillWidth > 0)
                    {
                        using (var fillBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                            new Rectangle(1, 1, fillWidth, Height - 2),
                            Color.FromArgb(0, 184, 255),
                            Color.FromArgb(0, 255, 156),
                            0.0f))
                        {
                            e.Graphics.FillRectangle(fillBrush, 1, 1, fillWidth, Height - 2);
                        }
                    }
                }
            }
        }

        private sealed class TechCheckBox : CheckBox
        {
            public TechCheckBox()
            {
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Color parentBg = Color.FromArgb(10, 18, 24);
                if (Parent != null)
                {
                    parentBg = Parent.BackColor;
                }
                using (var bgBrush = new SolidBrush(parentBg))
                {
                    e.Graphics.FillRectangle(bgBrush, ClientRectangle);
                }

                int boxSize = 14;
                int boxX = 0;
                int boxY = (Height - boxSize) / 2;
                Rectangle boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

                Color borderColor = Checked ? Color.FromArgb(0, 255, 156) : Color.FromArgb(21, 58, 73);
                Color fillColor = Checked ? Color.FromArgb(0, 55, 40) : Color.FromArgb(9, 22, 29);

                using (var boxBg = new SolidBrush(fillColor))
                {
                    e.Graphics.FillRectangle(boxBg, boxRect);
                }

                using (var boxBorder = new Pen(borderColor, 1.5f))
                {
                    e.Graphics.DrawRectangle(boxBorder, boxX, boxY, boxSize - 1, boxSize - 1);
                }

                if (Checked)
                {
                    using (var checkPen = new Pen(Color.FromArgb(0, 255, 156), 2f))
                    {
                        e.Graphics.DrawLine(checkPen, boxX + 3, boxY + 7, boxX + 6, boxY + 10);
                        e.Graphics.DrawLine(checkPen, boxX + 6, boxY + 10, boxX + 11, boxY + 4);
                    }
                }

                Color textColor = Enabled ? Color.FromArgb(218, 238, 239) : Color.FromArgb(130, 154, 164);
                using (var textBrush = new SolidBrush(textColor))
                {
                    Rectangle textRect = new Rectangle(boxSize + 8, 0, Width - boxSize - 8, Height);
                    TextRenderer.DrawText(
                        e.Graphics,
                        Text,
                        Font,
                        textRect,
                        textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }
}
