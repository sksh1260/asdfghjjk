using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: AssemblyTitle("MassRecall SC Evo Launcher")]
[assembly: AssemblyDescription("Windows launcher for installing MassRecall SC Evo patch archives")]
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
        private const string LatestInfoUrl = "https://drive.google.com/file/d/1XiEN8y6h4VuCCvJUxYnRkFm5ixMQV_jD/view?usp=drive_link";
        private const string LatestInfoFileName = "latest.json";
        private const string ChangeLogUrl = "https://potenking.blogspot.com/2024/04/httpsdrive.html";
        private static readonly string StateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MassRecallSCEvoLauncher");
        private static readonly List<string> InstalledItems = new List<string>();

        private static readonly Package[] Packages =
        {
            new Package(
                "메인 파일",
                "https://drive.google.com/file/d/10dY2bqNPpNDpZwNaEjOWp6W6qkucNI50/view?usp=drive_link",
                "massrecall-main.zip"),
            new Package(
                "한국어 음성 파일",
                "https://drive.google.com/file/d/16P1eeCS-C2b0QI-fmCEaD8Q5_lKAwPS6/view?usp=drive_link",
                "massrecall-korean-voice.zip"),
            new Package(
                "영어 음성 파일",
                "https://drive.google.com/file/d/1Xa4nVuvgLnFXdK24e0deMthilHzE7qgA/view?usp=drive_link",
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
            private readonly ProgressBar progressBar;
            private readonly Button actionButton;
            private readonly Button checkButton;
            private readonly Button uninstallButton;
            private readonly CheckBox koreanVoiceCheckBox;
            private readonly CheckBox englishVoiceCheckBox;
            private readonly CheckBox removeBankCheckBox;
            private readonly Label versionLabel;
            private UpdateInfo latestInfo;
            private bool suppressLanguageOptionChange;
            private static readonly uint[] MpqCryptTable = BuildMpqCryptTable();

            public LauncherForm()
            {
                Text = AppName;
                ClientSize = new Size(720, 380);
                BackColor = BackgroundColor;
                FormBorderStyle = FormBorderStyle.None;
                MaximizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;
                Paint += DrawWindowBorder;

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
                statusDot.SetBounds(20, 304, 20, 20);

                statusLabel = new Label();
                statusLabel.Font = new Font("Segoe UI", 10);
                statusLabel.ForeColor = TextColor;
                statusLabel.BackColor = Color.Transparent;
                statusLabel.TextAlign = ContentAlignment.MiddleLeft;
                statusLabel.SetBounds(42, 302, 360, 24);

                var bannerPanel = CreatePanel(20, 72, 400, 154);
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

                var optionPanel = CreatePanel(444, 72, 256, 154);
                var optionTitle = new Label();
                optionTitle.Text = "설치 옵션";
                optionTitle.Font = new Font("Segoe UI", 12, FontStyle.Bold);
                optionTitle.ForeColor = AccentColor;
                optionTitle.BackColor = Color.Transparent;
                optionTitle.SetBounds(18, 14, 160, 26);
                optionPanel.Controls.Add(optionTitle);

                koreanVoiceCheckBox = new CheckBox();
                koreanVoiceCheckBox.Text = "한국어 음성";
                koreanVoiceCheckBox.Font = new Font("Segoe UI", 10);
                koreanVoiceCheckBox.ForeColor = TextColor;
                koreanVoiceCheckBox.BackColor = Color.Transparent;
                koreanVoiceCheckBox.AutoSize = true;
                koreanVoiceCheckBox.Checked = true;
                koreanVoiceCheckBox.SetBounds(20, 46, 170, 24);
                koreanVoiceCheckBox.CheckedChanged += OnLanguageOptionChanged;
                optionPanel.Controls.Add(koreanVoiceCheckBox);

                englishVoiceCheckBox = new CheckBox();
                englishVoiceCheckBox.Text = "영어 음성";
                englishVoiceCheckBox.Font = new Font("Segoe UI", 10);
                englishVoiceCheckBox.ForeColor = TextColor;
                englishVoiceCheckBox.BackColor = Color.Transparent;
                englishVoiceCheckBox.AutoSize = true;
                englishVoiceCheckBox.SetBounds(20, 72, 170, 24);
                englishVoiceCheckBox.CheckedChanged += OnLanguageOptionChanged;
                optionPanel.Controls.Add(englishVoiceCheckBox);

                removeBankCheckBox = new CheckBox();
                removeBankCheckBox.Text = "SCMR.SC2Bank 제거";
                removeBankCheckBox.Font = new Font("Segoe UI", 10);
                removeBankCheckBox.ForeColor = TextColor;
                removeBankCheckBox.BackColor = Color.Transparent;
                removeBankCheckBox.AutoSize = true;
                removeBankCheckBox.SetBounds(20, 108, 170, 24);
                optionPanel.Controls.Add(removeBankCheckBox);

                var bankHelpLabel = new Label();
                bankHelpLabel.Text = "SC Evo 버전을 처음 설치하시면 체크";
                bankHelpLabel.Font = new Font("Segoe UI", 9);
                bankHelpLabel.ForeColor = MutedTextColor;
                bankHelpLabel.BackColor = Color.Transparent;
                bankHelpLabel.TextAlign = ContentAlignment.MiddleLeft;
                bankHelpLabel.SetBounds(40, 130, 200, 18);
                optionPanel.Controls.Add(bankHelpLabel);

                OnLanguageOptionChanged(null, EventArgs.Empty);

                progressBar = new ProgressBar();
                progressBar.SetBounds(20, 330, 680, 22);

                actionButton = new Button();
                actionButton.SetBounds(550, 260, 150, 44);
                actionButton.Click += OnActionClick;
                StyleNeonButton(actionButton, true);

                checkButton = new Button();
                checkButton.Text = "파일 검사";
                checkButton.SetBounds(306, 261, 96, 40);
                checkButton.Click += OnCheckClick;
                StyleNeonButton(checkButton, false);

                uninstallButton = new Button();
                uninstallButton.Text = "제거";
                uninstallButton.SetBounds(420, 261, 96, 40);
                uninstallButton.Click += OnUninstallClick;
                StyleNeonButton(uninstallButton, false);

                Controls.Add(headerTitle);
                Controls.Add(versionLabel);
                Controls.Add(minimizeButton);
                Controls.Add(closeButton);
                Controls.Add(topLine);
                Controls.Add(statusDot);
                Controls.Add(statusLabel);
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
                    catch
                    {
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
                catch
                {
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

                    koreanVoiceCheckBox.Enabled = true;
                    englishVoiceCheckBox.Enabled = true;
                }
                finally
                {
                    suppressLanguageOptionChange = false;
                }
            }

            private void RestoreLanguageOptionState()
            {
                koreanVoiceCheckBox.Enabled = true;
                englishVoiceCheckBox.Enabled = true;
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
                    InstalledItems.Clear();
                    RefreshLatestInfoForInstall();
                    if (removeBankCheckBox.Checked)
                    {
                        SetStatus("SCMR.SC2Bank 제거 중...");
                        RemoveScmrBankFiles();
                    }
                    InstallPackages(installPath, englishVoiceCheckBox.Checked);
                    CreateDesktopShortcut(installPath);
                    RefreshLatestInfoForInstall();
                    SaveState(installPath);
                    progressBar.Value = 100;
                    RefreshUi(false);
                    statusLabel.Text = "설치 완료: " + installPath;
                    BeginCheckForUpdate();
                }
                catch (Exception ex)
                {
                    statusDot.ForeColor = IsInstalled() ? CyanColor : NotInstalledColor;
                    statusLabel.Text = "실패";
                    MessageBox.Show(this, ex.Message, "런처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    actionButton.Enabled = true;
                    checkButton.Enabled = true;
                    RestoreLanguageOptionState();
                    removeBankCheckBox.Enabled = true;
                    RefreshUi(false);
                }
            }

            private void LaunchInstalledMassRecall()
            {
                try
                {
                    string installPath = ReadInstallPath();
                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                    {
                        throw new InvalidOperationException("설치 경로를 찾지 못했습니다. 파일 검사를 실행하거나 다시 설치하세요.");
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
                    statusLabel.Text = "파일 검사 완료";
                    MessageBox.Show(this, "설치된 파일이 정상입니다.", "파일 검사", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string detectedInstallPath;
                if (TryDetectExistingInstall(out detectedInstallPath))
                {
                    SaveDetectedState(detectedInstallPath);
                    RefreshUi(false);
                    statusDot.ForeColor = CyanColor;
                    statusLabel.Text = "파일 검사 완료";
                    MessageBox.Show(this, "파일 확인이 완료되었습니다.", "파일 검사", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                statusDot.ForeColor = NotInstalledColor;
                if (HasInstallState())
                {
                    statusLabel.Text = "설치 파일 확인 필요";
                    MessageBox.Show(this, "설치 파일이 일부 누락되었습니다. 설치 버튼을 눌러 다시 설치하세요.", "파일 검사", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                statusLabel.Text = "준비됨";
                MessageBox.Show(this, "설치 기록이 없습니다.", "파일 검사", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                try
                {
                    SetStatus("제거 중...");
                    UninstallMassRecall();
                    progressBar.Value = 100;
                    RefreshUi(false);
                    statusLabel.Text = "제거 완료";
                }
                catch (Exception ex)
                {
                    statusDot.ForeColor = IsInstalled() ? CyanColor : NotInstalledColor;
                    statusLabel.Text = "실패";
                    MessageBox.Show(this, ex.Message, "런처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    actionButton.Enabled = true;
                    checkButton.Enabled = true;
                    RestoreLanguageOptionState();
                    removeBankCheckBox.Enabled = true;
                    RefreshUi(false);
                }
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
                statusLabel.Text = text;
                Application.DoEvents();
            }

            private void SetProgress(int value)
            {
                progressBar.Value = Math.Max(0, Math.Min(100, value));
                Application.DoEvents();
            }

            private void SetDownloadProgress(int value)
            {
                int percent = Math.Max(0, Math.Min(100, value));
                progressBar.Value = percent;
                statusLabel.Text = "다운로드 중... " + percent + "%";
                Application.DoEvents();
            }

            private string DownloadPackage(Package package, int basePercent, int rangePercent)
            {
                string target = Path.Combine(Path.GetTempPath(), package.FileName);
                var cookies = new CookieContainer();
                string packageUrl = GetPackageUrl(package);
                string url = ConvertGoogleDriveUrl(packageUrl, package.FileName);
                string fileId = GetGoogleDriveFileId(packageUrl);

                DownloadToFile(url, target, cookies, basePercent, rangePercent);

                string preview = ReadPreview(target);
                if (LooksLikeHtml(preview))
                {
                    string confirmUrl;
                    if (TryBuildGoogleDriveConfirmationUrl(preview, url, out confirmUrl))
                    {
                        DownloadToFile(confirmUrl, target, cookies, basePercent, rangePercent);
                        preview = ReadPreview(target);
                    }
                }

                if (LooksLikeHtml(preview) && IsGoogleDriveQuotaExceeded(preview) && !string.IsNullOrEmpty(fileId))
                {
                    string fallbackUrl = "https://drive.usercontent.google.com/download?id=" +
                        fileId + "&export=download&authuser=0&confirm=t";
                    DownloadToFile(fallbackUrl, target, cookies, basePercent, rangePercent);
                    preview = ReadPreview(target);
                }

                if (LooksLikeHtml(preview))
                {
                    SaveHtmlDebug(package, preview);
                    if (IsGoogleDriveQuotaExceeded(preview))
                    {
                        throw new InvalidOperationException(
                            package.Name + " 다운로드가 Google Drive 다운로드 제한에 걸렸습니다. 잠시 후 다시 시도하세요.");
                    }

                    throw new InvalidOperationException(
                        package.Name + " 다운로드가 파일이 아닌 Google Drive 안내 페이지로 처리되었습니다. 링크 공유 권한을 확인하세요.");
                }

                if (Path.GetExtension(package.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase) && !LooksLikeZip(target))
                {
                    throw new InvalidOperationException(
                        package.Name + "이(가) 올바른 zip 파일로 다운로드되지 않았습니다. Google Drive 공유 권한 또는 다운로드 제한을 확인하세요.");
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
                CookieContainer cookies,
                int basePercent,
                int rangePercent)
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "Mozilla/5.0 MassRecallSCEvoLauncher/1.0";
                request.AllowAutoRedirect = true;
                request.CookieContainer = cookies;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var input = response.GetResponseStream())
                using (var output = File.Create(target))
                {
                    long total = response.ContentLength;
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

            private static string ConvertGoogleDriveUrl(string url)
            {
                string fileId = GetGoogleDriveFileId(url);
                if (!string.IsNullOrEmpty(fileId))
                {
                    string converted = "https://drive.google.com/uc?export=download&id=" + fileId;
                    Match resourceKey = Regex.Match(url, "[?&]resourcekey=([^&]+)");
                    if (resourceKey.Success)
                    {
                        converted += "&resourcekey=" + resourceKey.Groups[1].Value;
                    }
                    return converted;
                }

                if (IsGoogleDriveFolderUrl(url))
                {
                    throw new InvalidOperationException("Google Drive 폴더 링크는 파일 이름이 필요합니다.");
                }

                return url;
            }

            private static string ConvertGoogleDriveUrl(string url, string fileName)
            {
                if (IsGoogleDriveFolderUrl(url))
                {
                    return ResolveGoogleDriveFolderFileUrl(url, fileName);
                }

                return ConvertGoogleDriveUrl(url);
            }

            private static string GetGoogleDriveFileId(string url)
            {
                Match fileMatch = Regex.Match(url, "drive\\.google\\.com/file/d/([^/]+)", RegexOptions.IgnoreCase);
                if (fileMatch.Success)
                {
                    return fileMatch.Groups[1].Value;
                }

                Match idMatch = Regex.Match(url, "[?&]id=([^&]+)", RegexOptions.IgnoreCase);
                return idMatch.Success ? idMatch.Groups[1].Value : "";
            }

            private static bool IsGoogleDriveFolderUrl(string url)
            {
                return Regex.IsMatch(url, "drive\\.google\\.com/drive/folders/([^?/&]+)", RegexOptions.IgnoreCase);
            }

            private static string ResolveGoogleDriveFolderFileUrl(string folderUrl, string fileName)
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    throw new InvalidOperationException("Google Drive 폴더 링크는 파일 이름이 필요합니다.");
                }

                string html = DownloadText(folderUrl);
                string decoded = WebUtility.HtmlDecode(html);
                string id = FindGoogleDriveFileId(decoded, fileName);
                if (string.IsNullOrEmpty(id))
                {
                    throw new InvalidOperationException("Google Drive 폴더에서 " + fileName + " 파일을 찾지 못했습니다. 파일 이름과 공유 권한을 확인하세요.");
                }

                return "https://drive.google.com/uc?export=download&id=" + id;
            }

            private static UpdateInfo LoadLatestInfo()
            {
                string jsonUrl = LatestInfoUrl;
                string json;
                if (IsGoogleDriveFolderUrl(jsonUrl))
                {
                    jsonUrl = ResolveGoogleDriveFolderFileUrl(jsonUrl, LatestInfoFileName);
                    json = DownloadText(jsonUrl);
                }
                else
                {
                    jsonUrl = ConvertGoogleDriveUrl(jsonUrl, LatestInfoFileName);
                    json = DownloadText(jsonUrl);
                }

                return new UpdateInfo
                {
                    Version = ReadJsonString(json, "version"),
                    MainUrl = ReadJsonString(json, "main_url"),
                    KoreanVoiceUrl = ReadJsonString(json, "korean_voice_url"),
                    EnglishVoiceUrl = ReadJsonString(json, "english_voice_url"),
                    VoiceUrl = ReadJsonString(json, "voice_url"),
                    ChangeLogUrl = ReadJsonString(json, "changelog_url")
                };
            }

            private static string DownloadText(string url)
            {
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 MassRecallSCEvoLauncher/1.0";
                    return client.DownloadString(url);
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

            private static string FindGoogleDriveFileId(string html, string fileName)
            {
                string name = Regex.Escape(fileName);
                string idPattern = "([A-Za-z0-9_-]{20,})";
                string[] patterns =
                {
                    "\"" + idPattern + "\"\\s*,\\s*\"" + name + "\"",
                    "\"" + name + "\"\\s*,\\s*\"" + idPattern + "\"",
                    "\\[" + "\\s*\"" + idPattern + "\"\\s*,\\s*\"" + name + "\""
                };

                foreach (string pattern in patterns)
                {
                    Match match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                int nameIndex = html.IndexOf(fileName, StringComparison.OrdinalIgnoreCase);
                if (nameIndex < 0)
                {
                    return null;
                }

                int start = Math.Max(0, nameIndex - 600);
                int length = Math.Min(html.Length - start, 1200);
                string nearby = html.Substring(start, length);
                Match nearbyMatch = Regex.Match(nearby, idPattern);
                return nearbyMatch.Success ? nearbyMatch.Groups[1].Value : null;
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

            private static bool TryBuildGoogleDriveConfirmationUrl(string html, string originalUrl, out string confirmUrl)
            {
                confirmUrl = null;

                Match formMatch = Regex.Match(
                    html,
                    "<form[^>]+(?:id=[\"']download-form[\"'][^>]+)?action=[\"']([^\"']+)[\"'][^>]*>",
                    RegexOptions.IgnoreCase);

                if (formMatch.Success)
                {
                    string action = WebUtility.HtmlDecode(formMatch.Groups[1].Value);
                    var parameters = new List<string>();
                    MatchCollection inputs = Regex.Matches(
                        html,
                        "<input[^>]+type=[\"']hidden[\"'][^>]*>",
                        RegexOptions.IgnoreCase);

                    foreach (Match input in inputs)
                    {
                        Match nameMatch = Regex.Match(input.Value, "name=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
                        Match valueMatch = Regex.Match(input.Value, "value=[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase);
                        if (!nameMatch.Success)
                        {
                            continue;
                        }

                        string name = WebUtility.HtmlDecode(nameMatch.Groups[1].Value);
                        string value = valueMatch.Success ? WebUtility.HtmlDecode(valueMatch.Groups[1].Value) : "";
                        parameters.Add(Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(value));
                    }

                    if (parameters.Count > 0)
                    {
                        confirmUrl = action + (action.Contains("?") ? "&" : "?") + string.Join("&", parameters.ToArray());
                        return true;
                    }
                }

                Match hrefMatch = Regex.Match(
                    html,
                    "href=[\"']([^\"']*(?:drive\\.usercontent\\.google\\.com/download|/uc\\?export=download)[^\"']+)[\"']",
                    RegexOptions.IgnoreCase);
                if (hrefMatch.Success)
                {
                    confirmUrl = WebUtility.HtmlDecode(hrefMatch.Groups[1].Value);
                    if (confirmUrl.StartsWith("/"))
                    {
                        confirmUrl = "https://drive.google.com" + confirmUrl;
                    }
                    return true;
                }

                Match confirmMatch = Regex.Match(html, "confirm=([0-9A-Za-z_]+)", RegexOptions.IgnoreCase);
                Match idMatch = Regex.Match(originalUrl, "id=([^&]+)", RegexOptions.IgnoreCase);
                if (confirmMatch.Success && idMatch.Success)
                {
                    confirmUrl = "https://drive.google.com/uc?export=download&id=" +
                        idMatch.Groups[1].Value + "&confirm=" + confirmMatch.Groups[1].Value;

                    Match uuidMatch = Regex.Match(html, "uuid=([^&\"'<>]+)", RegexOptions.IgnoreCase);
                    if (uuidMatch.Success)
                    {
                        confirmUrl += "&uuid=" + uuidMatch.Groups[1].Value;
                    }
                    return true;
                }

                return false;
            }

            private static string ReadPreview(string path)
            {
                byte[] buffer = new byte[8192];
                int read;
                using (var stream = File.OpenRead(path))
                {
                    read = stream.Read(buffer, 0, buffer.Length);
                }
                return Encoding.UTF8.GetString(buffer, 0, read);
            }

            private static bool LooksLikeHtml(string preview)
            {
                return preview.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    preview.IndexOf("<!doctype html", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static bool IsGoogleDriveQuotaExceeded(string preview)
            {
                return preview.IndexOf("Quota exceeded", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    preview.IndexOf("Too many users have viewed or downloaded this file recently", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    preview.IndexOf("you can't view or download this file at this time", StringComparison.OrdinalIgnoreCase) >= 0;
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

            private static string SaveHtmlDebug(Package package, string html)
            {
                string safeName = Regex.Replace(package.Name, "[^A-Za-z0-9_-]+", "-");
                string path = Path.Combine(Path.GetTempPath(), "MassRecallSCEvo-" + safeName + "-download.html");
                File.WriteAllText(path, html, Encoding.UTF8);
                return path;
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
                string tempExtract = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempExtract);

                try
                {
                    ExtractArchive(package, archivePath, tempExtract);
                    return tempExtract;
                }
                catch
                {
                    TryDeleteDirectory(tempExtract);
                    throw;
                }
            }

            private static void ExtractArchive(Package package, string archivePath, string extractRoot)
            {
                string extension = Path.GetExtension(archivePath).ToLowerInvariant();
                if (extension == ".zip")
                {
                    try
                    {
                        ZipFile.ExtractToDirectory(archivePath, extractRoot);
                    }
                    catch (Exception ex)
                    {
                        if (!TryExtractWithTar(archivePath, extractRoot))
                        {
                            throw new InvalidOperationException(
                                package.Name + " ZIP 압축 해제에 실패했습니다: " + ex.Message,
                                ex);
                        }
                    }
                    return;
                }

                if (extension == ".7z" || extension == ".rar")
                {
                    string sevenZip = FindOnPath("7z.exe");
                    if (sevenZip != null)
                    {
                        var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = sevenZip,
                            Arguments = "x -y -o\"" + extractRoot + "\" \"" + archivePath + "\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            throw new InvalidOperationException(package.Name + " 압축 해제에 실패했습니다.");
                        }
                        return;
                    }

                    if (TryExtractWithTar(archivePath, extractRoot))
                    {
                        return;
                    }

                    throw new InvalidOperationException(package.Name + "은(는) " + extension + " 형식입니다. 7-Zip 설치 후 런처를 다시 실행하세요.");
                }

                throw new InvalidOperationException("지원하지 않는 압축 형식입니다: " + package.Name + " / " + extension);
            }

            private static bool TryExtractWithTar(string archivePath, string extractRoot)
            {
                string tar = FindOnPath("tar.exe");
                if (tar == null)
                {
                    return false;
                }

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = tar,
                    Arguments = "-xf \"" + archivePath + "\" -C \"" + extractRoot + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process.WaitForExit();
                return process.ExitCode == 0;
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
                foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
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
                if (normalizedPath.Length <= normalizedRoot.Length)
                {
                    return Path.GetFileName(normalizedPath);
                }

                return normalizedPath.Substring(normalizedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            private static bool IsExcludedByRoot(string source, string path, HashSet<string> excludedRoots)
            {
                if (excludedRoots.Count == 0)
                {
                    return false;
                }

                string relative = path.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                int separator = relative.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                string rootName = separator >= 0 ? relative.Substring(0, separator) : relative;
                return excludedRoots.Contains(rootName);
            }

            private static void AddInstalledItem(string path)
            {
                string fullPath = Path.GetFullPath(path);
                if (!InstalledItems.Contains(fullPath))
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
                        Directory.Delete(path, true);
                    }
                }
                catch
                {
                }
            }

            private static void TryDeleteFile(string path)
            {
                try
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
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
                        Directory.Delete(path, true);
                    }
                }
                catch
                {
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
                    DeleteInstalledPath(path);
                }

                DeleteDesktopShortcut();
                TryDeleteFile(Path.Combine(StateDir, "state.txt"));
                TryDeleteFile(Path.Combine(StateDir, "install-path.txt"));
                TryDeleteFile(manifestPath);
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

            private static void DeleteDesktopShortcut()
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                TryDeleteFile(Path.Combine(desktopPath, "Mass Recall.lnk"));
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

            private static void CreateDesktopShortcut(string installPath)
            {
                string targetPath = FindSwitcherPath(installPath);
                string mapPath = FindCampaignLauncherMap(installPath);
                string iconPath = FindMassRecallIcon(installPath);
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktopPath, "Mass Recall.lnk");

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    throw new InvalidOperationException("Windows 바로가기 생성 기능을 사용할 수 없어 바탕화면 바로가기를 만들지 못했습니다.");
                }

                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                shortcutType.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "\"" + mapPath + "\"" });
                shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetPath) });
                shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "Mass Recall" });

                if (!string.IsNullOrEmpty(iconPath))
                {
                    shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                }

                shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
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
                        return matches[0];
                    }
                }

                throw new InvalidOperationException("설치된 Maps 폴더에서 SCMR Campaign Launcher.SC2Map을 찾지 못했습니다.");
            }

            private static string FindMassRecallIcon(string installPath)
            {
                string mapsRoot = Path.Combine(installPath, "Maps");
                string expected = Path.Combine(mapsRoot, @"Starcraft Mass Recall\icon.ico");
                if (File.Exists(expected))
                {
                    return expected;
                }

                if (Directory.Exists(mapsRoot))
                {
                    string[] matches = Directory.GetFiles(mapsRoot, "icon.ico", SearchOption.AllDirectories);
                    if (matches.Length > 0)
                    {
                        return matches[0];
                    }
                }

                return null;
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
                        return false;
                    }
                }

                return hasEntries;
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
                catch
                {
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
                    if (name.Equals("Assets", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".SC2Mod", StringComparison.OrdinalIgnoreCase) ||
                        name.IndexOf("SCMR", StringComparison.OrdinalIgnoreCase) >= 0 ||
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
                string mapVersion = GetInstalledMapVersion();
                if (!string.IsNullOrEmpty(mapVersion))
                {
                    return mapVersion;
                }

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

            private static string GetInstalledMapVersion()
            {
                try
                {
                    string installPath = ReadInstallPath();
                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                    {
                        return "";
                    }

                    string mapPath = FindCampaignLauncherMap(installPath);
                    return ReadScEvoVersionFromMap(mapPath);
                }
                catch
                {
                    return "";
                }
            }

            private static string GetInstalledMapVersionFromPath(string installPath)
            {
                try
                {
                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                    {
                        return "";
                    }

                    string mapPath = FindCampaignLauncherMap(installPath);
                    return ReadScEvoVersionFromMap(mapPath);
                }
                catch
                {
                    return "";
                }
            }

            private static string ReadScEvoVersionFromMap(string mapPath)
            {
                string[] stringFiles =
                {
                    @"koKR.SC2Data\LocalizedData\GameStrings.txt",
                    @"enUS.SC2Data\LocalizedData\GameStrings.txt"
                };

                foreach (string stringFile in stringFiles)
                {
                    byte[] data = ReadMpqFile(mapPath, stringFile);
                    if (data == null)
                    {
                        continue;
                    }

                    string text = Encoding.UTF8.GetString(data);
                    Match match = Regex.Match(text, @"SC Evo\s+([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                return "";
            }

            private static byte[] ReadMpqFile(string archivePath, string internalPath)
            {
                byte[] archive = File.ReadAllBytes(archivePath);
                if (archive.Length < 32 || archive[0] != 'M' || archive[1] != 'P' || archive[2] != 'Q' || archive[3] != 0x1A)
                {
                    return null;
                }

                uint hashTableOffset = ReadUInt32LittleEndian(archive, 16);
                uint blockTableOffset = ReadUInt32LittleEndian(archive, 20);
                uint hashTableEntries = ReadUInt32LittleEndian(archive, 24);
                uint blockTableEntries = ReadUInt32LittleEndian(archive, 28);
                if (hashTableEntries == 0 || blockTableEntries == 0)
                {
                    return null;
                }

                byte[] hashTable = CopyRange(archive, (int)hashTableOffset, (int)hashTableEntries * 16);
                byte[] blockTable = CopyRange(archive, (int)blockTableOffset, (int)blockTableEntries * 16);
                DecryptMpqTable(hashTable, HashMpqString("(hash table)", 3));
                DecryptMpqTable(blockTable, HashMpqString("(block table)", 3));

                uint hashA = HashMpqString(internalPath, 1);
                uint hashB = HashMpqString(internalPath, 2);
                uint start = HashMpqString(internalPath, 0) % hashTableEntries;
                uint blockIndex = 0xFFFFFFFF;

                for (uint probe = 0; probe < hashTableEntries; probe++)
                {
                    uint hashIndex = (start + probe) % hashTableEntries;
                    int entryOffset = (int)hashIndex * 16;
                    uint entryHashA = ReadUInt32LittleEndian(hashTable, entryOffset);
                    uint entryHashB = ReadUInt32LittleEndian(hashTable, entryOffset + 4);
                    uint entryBlockIndex = ReadUInt32LittleEndian(hashTable, entryOffset + 12);
                    if (entryBlockIndex == 0xFFFFFFFF)
                    {
                        return null;
                    }

                    if (entryHashA == hashA && entryHashB == hashB && entryBlockIndex != 0xFFFFFFFE)
                    {
                        blockIndex = entryBlockIndex;
                        break;
                    }
                }

                if (blockIndex == 0xFFFFFFFF || blockIndex >= blockTableEntries)
                {
                    return null;
                }

                int blockOffset = (int)blockIndex * 16;
                uint fileOffset = ReadUInt32LittleEndian(blockTable, blockOffset);
                uint compressedSize = ReadUInt32LittleEndian(blockTable, blockOffset + 4);
                uint fileSize = ReadUInt32LittleEndian(blockTable, blockOffset + 8);
                uint flags = ReadUInt32LittleEndian(blockTable, blockOffset + 12);
                byte[] data = CopyRange(archive, (int)fileOffset, (int)compressedSize);

                if ((flags & 0x00010000) != 0)
                {
                    DecryptMpqTable(data, HashMpqString(internalPath, 3));
                }

                if ((flags & 0x00000200) != 0)
                {
                    data = DecompressMpqSingleUnit(data, (int)fileSize);
                }

                return data;
            }

            private static byte[] DecompressMpqSingleUnit(byte[] data, int expectedSize)
            {
                if (data.Length == expectedSize || data.Length == 0)
                {
                    return data;
                }

                byte compression = data[0];
                if ((compression & 0x02) == 0)
                {
                    return data;
                }

                try
                {
                    using (var input = new MemoryStream(data, 3, data.Length - 7))
                    using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        deflate.CopyTo(output);
                        return output.ToArray();
                    }
                }
                catch
                {
                    using (var input = new MemoryStream(data, 1, data.Length - 1))
                    using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        deflate.CopyTo(output);
                        return output.ToArray();
                    }
                }
            }

            private static byte[] CopyRange(byte[] source, int offset, int count)
            {
                byte[] result = new byte[count];
                Buffer.BlockCopy(source, offset, result, 0, count);
                return result;
            }

            private static uint ReadUInt32LittleEndian(byte[] data, int offset)
            {
                return (uint)(data[offset] |
                    (data[offset + 1] << 8) |
                    (data[offset + 2] << 16) |
                    (data[offset + 3] << 24));
            }

            private static uint[] BuildMpqCryptTable()
            {
                uint seed = 0x00100001;
                uint[] table = new uint[0x500];
                for (uint index = 0; index < 0x100; index++)
                {
                    for (uint i = 0; i < 5; i++)
                    {
                        seed = (seed * 125 + 3) % 0x2AAAAB;
                        uint temp1 = (seed & 0xFFFF) << 16;
                        seed = (seed * 125 + 3) % 0x2AAAAB;
                        uint temp2 = seed & 0xFFFF;
                        table[i * 0x100 + index] = temp1 | temp2;
                    }
                }

                return table;
            }

            private static uint HashMpqString(string text, int hashType)
            {
                uint seed1 = 0x7FED7FED;
                uint seed2 = 0xEEEEEEEE;
                string normalized = text.Replace('/', '\\').ToUpperInvariant();
                foreach (char ch in normalized)
                {
                    byte value = (byte)ch;
                    seed1 = MpqCryptTable[(hashType << 8) + value] ^ (seed1 + seed2);
                    seed2 = value + seed1 + seed2 + (seed2 << 5) + 3;
                }

                return seed1;
            }

            private static void DecryptMpqTable(byte[] data, uint key)
            {
                uint seed = 0xEEEEEEEE;
                for (int offset = 0; offset + 3 < data.Length; offset += 4)
                {
                    seed += MpqCryptTable[0x400 + (key & 0xFF)];
                    uint encrypted = ReadUInt32LittleEndian(data, offset);
                    uint decrypted = encrypted ^ (key + seed);
                    key = ((~key << 21) + 0x11111111) | (key >> 11);
                    seed = decrypted + seed + (seed << 5) + 3;
                    data[offset] = (byte)(decrypted & 0xFF);
                    data[offset + 1] = (byte)((decrypted >> 8) & 0xFF);
                    data[offset + 2] = (byte)((decrypted >> 16) & 0xFF);
                    data[offset + 3] = (byte)((decrypted >> 24) & 0xFF);
                }
            }
            private static void TryWriteAllText(string path, string text)
            {
                try
                {
                    File.WriteAllText(path, text);
                }
                catch
                {
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
                File.WriteAllLines(Path.Combine(StateDir, "manifest.txt"), InstalledItems.ToArray());
            }

            private static void SaveDetectedState(string installPath)
            {
                Directory.CreateDirectory(StateDir);
                string installedVersion = GetInstalledMapVersionFromPath(installPath);
                if (string.IsNullOrEmpty(installedVersion))
                {
                    installedVersion = Version;
                }

                File.WriteAllText(Path.Combine(StateDir, "state.txt"), installedVersion);
                File.WriteAllText(Path.Combine(StateDir, "install-path.txt"), installPath);
                TryDeleteFile(Path.Combine(StateDir, "manifest.txt"));
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

            private static string FindOnPath(string fileName)
            {
                string[] knownPaths =
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", fileName),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", fileName),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", fileName)
                };

                foreach (string knownPath in knownPaths)
                {
                    if (File.Exists(knownPath))
                    {
                        return knownPath;
                    }
                }

                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string directory in path.Split(Path.PathSeparator))
                {
                    try
                    {
                        string candidate = Path.Combine(directory.Trim(), fileName);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                    catch
                    {
                    }
                }

                return null;
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
            public string ChangeLogUrl;
        }
    }
}
