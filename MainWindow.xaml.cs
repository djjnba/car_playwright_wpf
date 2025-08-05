using Hardcodet.Wpf.TaskbarNotification;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace car_playwright_wpf
{


    public partial class MainWindow : Window
    {
        // 单例模式，确保只有一个 MainWindow 实例
        private static MainWindow _instance;
        public static MainWindow Instance => _instance ??= new MainWindow();

        private readonly PaletteHelper _paletteHelper = new();

        // 添加类成员变量
        private Process? runningProcess;
        private CancellationTokenSource? processCancellationToken;

        public MainWindow()
        {
            InitializeComponent();
            // 设置单例实例
            Closed += (s, e) => _instance = null;
            // 获取程序集版本号
            string version = Assembly.GetExecutingAssembly()
                                     .GetName()
                                     .Version?
                                     .ToString() ?? "未知版本";
            this.Title = $"用车复核工具 - v{version}";

            // 启动时检查脚本路径
            this.Loaded += MainWindow_Loaded;
        }

        // 添加定时任务开关方法（供托盘菜单调用）
        public void ToggleScheduledTask()
        {
            // 这里调用你现有的定时任务切换逻辑
            ToggleTaskButton_Click(null, null);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            // 最小化时隐藏到托盘
            if (WindowState == WindowState.Minimized)
            {
                //Hide();
            }
            //base.OnStateChanged(e);
        }

        // 重写关闭按钮行为（直接最小化）
        protected override void OnClosing(CancelEventArgs e)
        {
            // 仅当用户点击红 X 时隐藏到托盘
            if (this.WindowState != WindowState.Minimized)
            {
                e.Cancel = true;
                this.WindowState = WindowState.Minimized;
                this.Hide();

                var tray = (TaskbarIcon)Application.Current.FindResource("TrayIcon");
                tray?.ShowBalloonTip("提示", "已最小化到托盘", BalloonIcon.Info);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string userConfigDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CarPlaywrightWpf"
            );
            string configPath = Path.Combine(userConfigDir, "csharp_config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // 脚本路径
                    if (root.TryGetProperty("script_file", out var scriptFile))
                    {
                        PythonCodeBox.Text = scriptFile.GetString() ?? "";
                        RunButton.IsEnabled = !string.IsNullOrWhiteSpace(PythonCodeBox.Text);
                    }

                    // Python路径（如有控件可赋值）
                    if (root.TryGetProperty("python_path", out var pythonPath))
                    {
                        // 例如有 TextBox 名为 PythonPathBox
                        // PythonPathBox.Text = pythonPath.GetString() ?? "";
                    }

                    // 主题
                    if (root.TryGetProperty("theme", out var theme))
                    {
                        if (theme.GetString() == "dark")
                        {
                            DarkModeToggle.IsChecked = true;
                        }
                        else
                        {
                            DarkModeToggle.IsChecked = false;
                        }
                    }

                    // 其它参数可按需补充
                }
                catch
                {
                    RunButton.IsEnabled = false;
                    System.Windows.MessageBox.Show("C#配置文件读取失败，请重新选择脚本路径。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                RunButton.IsEnabled = false;
                System.Windows.MessageBox.Show("请先选择 Python 脚本路径。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 移除重复定义和赋值
            string pythonConfigPath = Path.Combine(userConfigDir, "python_config.json");
            if (File.Exists(pythonConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(pythonConfigPath);
                    using var doc = JsonDocument.Parse(json);
                    ApplyPythonConfig(doc.RootElement);
                }
                catch
                {
                    System.Windows.MessageBox.Show("Python配置文件读取失败，请检查格式。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            // 其它逻辑不变
        }

        private void DarkModeToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_paletteHelper.GetTheme() is Theme theme)
            {
                theme.SetDarkTheme();
                _paletteHelper.SetTheme(theme);
            }
        }

        private void DarkModeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_paletteHelper.GetTheme() is Theme theme)
            {
                theme.SetLightTheme();
                _paletteHelper.SetTheme(theme);
            }
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            RunButton.Visibility = Visibility.Collapsed;
            RunningButton.Visibility = Visibility.Visible;

            StatusLabel.Text = "🕐 正在运行，请稍候...";
            LogBox.AppendText("🕐 正在运行，请稍候...\n");
            ProgressBarControl.Value = 0;
            //LogBox.Clear();

            processCancellationToken = new CancellationTokenSource();

            string args = string.Join(" ", new string[]
            {
                $"--username \"{UsernameBox.Text}\"",
                $"--password \"{PasswordBox.Password}\"",
                $"--headless {HeadlessToggle.IsChecked?.ToString().ToLower()}",
                $"--slow_mo {SlowMoBox.Text}",
                $"--navigation_timeout {NavigationTimeoutBox.Text}",
                $"--default_timeout {DefaultTimeoutBox.Text}",
                $"--auto_timeout {AutoTimeoutBox.Text}",
                $"--auto_detect_timeout {AutoDetectTimeoutToggle.IsChecked?.ToString().ToLower()}",
                $"--retry_times {RetryTimesBox.Text}",
                $"--delay_after_click {DelayAfterClickBox.Text}",
                $"--only_login {OnlyLoginToggle.IsChecked?.ToString().ToLower()}",
                $"--auto_submit {AutoSubmitToggle.IsChecked?.ToString().ToLower()}",
                $"--auto_exit {AutoExitToggle.IsChecked?.ToString().ToLower()}",
                $"--export_json {ExportJsonBox.IsChecked?.ToString().ToLower()}",
                $"--export_excel {ExportExcelToggle.IsChecked?.ToString().ToLower()}",
                $"--log_file \"{LogFileBox.Text}\"",
                $"--base_url \"{BaseUrlBox.Text}\"",
                //$"--captcha_ocr_lang {OcrLangBox.Text}",
                $"--captcha_ocr_lang {(OcrLangBox.SelectedItem as ComboBoxItem)?.Content?.ToString()}",
                $"--tesseract_path \"{TesseractPathBox.Text}\"",
                $"--excel_prefix \"{ExcelPrefixBox.Text}\"",
                $"--excel_monthly {ExcelMonthlyBox.IsChecked?.ToString().ToLower()}",
                $"--excel_dir \"{ExcelDirBox.Text}\"",
                $"--max_retry_on_error {MaxRetryOnErrorBox.Text}",
                $"--input_timeout {InputTimeoutBox.Text}",
                $"--order_time_threshold {OrderTimeThresholdBox.Text}",
                $"--order_max_retry {OrderMaxRetryBox.Text}",
                $"--order_retry_delay {OrderRetryDelayBox.Text}",
                $"--debug_mode {DebugModeBox.IsChecked?.ToString().ToLower()}",
                $"--log_level {(LogLevelBox.SelectedItem as ComboBoxItem)?.Content?.ToString()}"
            });

            // 获取脚本路径
            string scriptPath = PythonCodeBox.Text;
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
            {
                StatusLabel.Text = "⚠️ 未选择有效脚本";
                LogBox.AppendText("⚠️ 请先选择有效的 Python 脚本文件。\n");
                RunButton.Visibility = Visibility.Visible;
                RunningButton.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{scriptPath}\" {args}", // 使用选中的脚本路径
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        Environment =
                        {
                            ["PYTHONIOENCODING"] = "utf-8",
                            ["PYTHONUTF8"] = "1",
                            ["LC_ALL"] = "en_US.UTF-8",
                            ["LANG"] = "en_US.UTF-8"
                        }
                    };

                    runningProcess = new Process { StartInfo = psi };

                    runningProcess.OutputDataReceived += (s, ea) =>
                    {
                        if (!string.IsNullOrEmpty(ea.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                LogBox.AppendText(ea.Data + "\n");
                                LogBox.ScrollToEnd();
                            });
                        }
                    };

                    runningProcess.ErrorDataReceived += (s, ea) =>
                    {
                        if (!string.IsNullOrEmpty(ea.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                LogBox.AppendText("❌ " + ea.Data + "\n");
                                LogBox.ScrollToEnd();
                            });
                        }
                    };

                    processCancellationToken.Token.ThrowIfCancellationRequested();

                    runningProcess.Start();
                    runningProcess.BeginOutputReadLine();
                    runningProcess.BeginErrorReadLine();

                    while (!runningProcess.WaitForExit(100))
                    {
                        processCancellationToken.Token.ThrowIfCancellationRequested();
                    }
                }, processCancellationToken.Token);

                StatusLabel.Text = "✅ 运行完成";
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Text = "⏹ 已手动停止";
                LogBox.AppendText("\n⏹ 用户手动终止了进程\n");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "⚠️ 运行出错";
                LogBox.AppendText($"\n⚠️ 错误: {ex.Message}\n");
            }
            finally
            {
                RunButton.Visibility = Visibility.Visible;
                RunningButton.Visibility = Visibility.Collapsed;
                ProgressBarControl.Value = 100;

                runningProcess?.Dispose();
                processCancellationToken?.Dispose();
                runningProcess = null;
                processCancellationToken = null;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            RunningButton.Visibility = Visibility.Collapsed;
            RunButton.Visibility = Visibility.Visible;

            try
            {
                if (runningProcess != null && !runningProcess.HasExited)
                {
                    runningProcess.Kill(entireProcessTree: true);
                    processCancellationToken?.Cancel();
                    LogBox.AppendText("\n🛑 正在停止进程...\n");
                }
            }
            catch (Exception ex)
            {
                LogBox.AppendText($"\n⚠️ 停止失败: {ex.Message}\n");
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            // Python配置字典
            var pythonConfig = new Dictionary<string, object>
            {
                ["username"] = UsernameBox.Text,
                ["password"] = PasswordBox.Password,
                ["headless"] = HeadlessToggle.IsChecked == true,
                ["slow_mo"] = int.Parse(SlowMoBox.Text),
                ["navigation_timeout"] = int.Parse(NavigationTimeoutBox.Text),
                ["default_timeout"] = int.Parse(DefaultTimeoutBox.Text),
                ["auto_timeout"] = AutoTimeoutBox.Text,
                ["auto_detect_timeout"] = AutoDetectTimeoutToggle.IsChecked == true,
                ["retry_times"] = int.Parse(RetryTimesBox.Text),
                ["delay_after_click"] = double.Parse(DelayAfterClickBox.Text),
                ["only_login"] = OnlyLoginToggle.IsChecked == true,
                ["auto_submit"] = AutoSubmitToggle.IsChecked == true,
                ["auto_exit"] = AutoExitToggle.IsChecked == true,
                ["export_json"] = ExportJsonBox.IsChecked == true,
                ["export_excel"] = ExportExcelToggle.IsChecked == true,
                ["log_file"] = LogFileBox.Text,
                ["base_url"] = BaseUrlBox.Text,
                //["captcha_ocr_lang"] = OcrLangBox.Text,
                ["captcha_ocr_lang"] = (OcrLangBox.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                ["tesseract_path"] = TesseractPathBox.Text,
                ["excel_prefix"] = ExcelPrefixBox.Text,
                ["excel_monthly"] = ExcelMonthlyBox.IsChecked == true,
                ["excel_dir"] = ExcelDirBox.Text,
                ["max_retry_on_error"] = int.Parse(MaxRetryOnErrorBox.Text),
                ["input_timeout"] = int.Parse(InputTimeoutBox.Text),
                ["order_time_threshold"] = int.Parse(OrderTimeThresholdBox.Text),
                ["order_max_retry"] = int.Parse(OrderMaxRetryBox.Text),
                ["order_retry_delay"] = double.Parse(OrderRetryDelayBox.Text),
                ["debug_mode"] = DebugModeBox.IsChecked == true,
                ["log_level"] = ((ComboBoxItem)LogLevelBox.SelectedItem)?.Content?.ToString()
            };

            string pythonJson = JsonSerializer.Serialize(pythonConfig, new JsonSerializerOptions { WriteIndented = true });

            // 用户路径
            string userConfigDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CarPlaywrightWpf"
            );
            Directory.CreateDirectory(userConfigDir);
            string pythonConfigPath = Path.Combine(userConfigDir, "python_config.json");
            File.WriteAllText(pythonConfigPath, pythonJson);

            // 脚本同目录（如有脚本路径）
            if (!string.IsNullOrWhiteSpace(PythonCodeBox.Text))
            {
                try
                {
                    string scriptDir = Path.GetDirectoryName(PythonCodeBox.Text);
                    if (!string.IsNullOrEmpty(scriptDir) && Directory.Exists(scriptDir))
                    {
                        string scriptConfigPath = Path.Combine(scriptDir, "python_config.json");
                        File.WriteAllText(scriptConfigPath, pythonJson);
                        AppendLog($"配置已保存到脚本目录: {scriptConfigPath}");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"保存到脚本目录时出错: {ex.Message}");
                }
            }

            StatusLabel.Text = "✅ Python配置已保存";
            AppendLog($"配置已保存到用户目录: {pythonConfigPath}");


        }



        private void AppendLog(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}\n";

                var paragraph = new Paragraph(new Run(logEntry));
                LogBox.Document.Blocks.Add(paragraph);

                LogBox.ScrollToEnd();
            }), DispatcherPriority.Background);
        }

        // 按钮点击或按回车时调用
        private async void SendContinueSignal()
        {
            if (runningProcess == null || runningProcess.HasExited)
            {
                AppendLog("⚠️ 脚本未运行，无需发送继续信号。");
                StatusLabel.Text = "⚠️ 脚本未运行";

                string continuePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "continue.txt");
                if (File.Exists(continuePath))
                {
                    File.Delete(continuePath);
                    AppendLog("🗑️ 已删除残留的 continue.txt 文件。");
                }
                return;
            }

            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "continue.txt");
            try
            {
                // 无论是否存在都重新写入信号
                File.WriteAllText(filePath, "go");
                AppendLog("📨 已发送继续信号 (continue.txt)");
                StatusLabel.Text = "✅ 已发送继续信号";

                // 延迟 2 秒自动删除
                await Task.Delay(2000);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    AppendLog("🗑️ 已自动清除 continue.txt（超时未被脚本读取）");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️ 写入 continue.txt 失败: {ex.Message}");
            }
        }



        // 按钮点击事件
        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            SendContinueSignal();
        }

        // 全局按键监听（只在主窗口聚焦时）
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (!(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
                {
                    SendContinueSignal();
                    e.Handled = true;
                }
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 同样阻止 TextBox 中按回车冒泡
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (!(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
                {
                    SendContinueSignal();
                    e.Handled = true;
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopButton_Click(null, null); // 终止进程
            // trayIcon?.Dispose(); // 已删除
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("这里是设置窗口，占位中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 清空日志按钮点击事件
        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.Document.Blocks.Clear();
            });  // 清空日志内容
        }

        // 导出日志按钮点击事件
        private void ExportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "log",
                    DefaultExt = ".txt",
                    Filter = "Text documents (.txt)|*.txt"
                };

                if (dlg.ShowDialog() == true)
                {
                    // 修复：RichTextBox 没有 Text 属性，需通过 TextRange 获取内容
                    var textRange = new TextRange(LogBox.Document.ContentStart, LogBox.Document.ContentEnd);
                    System.IO.File.WriteAllText(dlg.FileName, textRange.Text);
                    System.Windows.MessageBox.Show("日志导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("导出日志失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "请选择脚本文件",
                Filter = "Python 脚本 (*.py)|*.py|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                PythonCodeBox.Text = dialog.FileName;
                RunButton.IsEnabled = true;
                SaveCSharpConfig();

                // 自动读取同目录下的 python_config.json
                string configPath = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, "python_config.json");
                if (File.Exists(configPath))
                {
                    try
                    {
                        string json = File.ReadAllText(configPath);
                        using var doc = JsonDocument.Parse(json);
                        ApplyPythonConfig(doc.RootElement);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("配置文件读取失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SaveCSharpConfig()
        {
            var csharpConfig = new Dictionary<string, object>
            {
                ["python_path"] = "python",
                ["script_file"] = PythonCodeBox.Text,
                ["auto_restart"] = false,
                ["run_in_tray"] = false,
                ["config_ui_mode"] = "simple",
                ["show_notifications"] = true,
                ["theme"] = (DarkModeToggle.IsChecked == true) ? "dark" : "light"
            };

            string userConfigDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CarPlaywrightWpf"
            );
            Directory.CreateDirectory(userConfigDir);
            string configPath = Path.Combine(userConfigDir, "csharp_config.json");

            string json = JsonSerializer.Serialize(csharpConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        private void ApplyPythonConfig(JsonElement pyConfig)
        {
            UsernameBox.Text = pyConfig.GetProperty("username").GetString() ?? "";
            PasswordBox.Password = pyConfig.GetProperty("password").GetString() ?? "";
            HeadlessToggle.IsChecked = pyConfig.GetProperty("headless").GetBoolean();
            SlowMoBox.Text = pyConfig.GetProperty("slow_mo").ToString();
            NavigationTimeoutBox.Text = pyConfig.GetProperty("navigation_timeout").ToString();
            DefaultTimeoutBox.Text = pyConfig.GetProperty("default_timeout").ToString();
            AutoTimeoutBox.Text = pyConfig.GetProperty("auto_timeout").GetString() ?? "";
            AutoDetectTimeoutToggle.IsChecked = pyConfig.GetProperty("auto_detect_timeout").GetBoolean();
            RetryTimesBox.Text = pyConfig.GetProperty("retry_times").ToString();
            DelayAfterClickBox.Text = pyConfig.GetProperty("delay_after_click").ToString();
            OnlyLoginToggle.IsChecked = pyConfig.GetProperty("only_login").GetBoolean();
            AutoSubmitToggle.IsChecked = pyConfig.GetProperty("auto_submit").GetBoolean();
            AutoExitToggle.IsChecked = pyConfig.GetProperty("auto_exit").GetBoolean();
            ExportJsonBox.IsChecked = pyConfig.GetProperty("export_json").GetBoolean();
            ExportExcelToggle.IsChecked = pyConfig.GetProperty("export_excel").GetBoolean();
            LogFileBox.Text = pyConfig.GetProperty("log_file").GetString() ?? "";
            BaseUrlBox.Text = pyConfig.GetProperty("base_url").GetString() ?? "";
            //OcrLangBox.Text = pyConfig.GetProperty("captcha_ocr_lang").GetString() ?? "";
            TesseractPathBox.Text = pyConfig.GetProperty("tesseract_path").GetString() ?? "";
            ExcelPrefixBox.Text = pyConfig.GetProperty("excel_prefix").GetString() ?? "";
            ExcelMonthlyBox.IsChecked = pyConfig.GetProperty("excel_monthly").GetBoolean();
            ExcelDirBox.Text = pyConfig.GetProperty("excel_dir").GetString() ?? "";
            MaxRetryOnErrorBox.Text = pyConfig.GetProperty("max_retry_on_error").ToString();
            InputTimeoutBox.Text = pyConfig.GetProperty("input_timeout").ToString();
            OrderTimeThresholdBox.Text = pyConfig.GetProperty("order_time_threshold").ToString();
            OrderMaxRetryBox.Text = pyConfig.GetProperty("order_max_retry").ToString();
            OrderRetryDelayBox.Text = pyConfig.GetProperty("order_retry_delay").ToString();
            DebugModeBox.IsChecked = pyConfig.GetProperty("debug_mode").GetBoolean();

            // 日志等级 ComboBox
            string logLevel = pyConfig.GetProperty("log_level").GetString() ?? "DEBUG";
            foreach (ComboBoxItem item in LogLevelBox.Items)
            {
                if ((item.Content?.ToString() ?? "") == logLevel)
                {
                    LogLevelBox.SelectedItem = item;
                    break;
                }
            }
            string ocrLang = pyConfig.GetProperty("captcha_ocr_lang").GetString() ?? "eng";
            foreach (ComboBoxItem item in OcrLangBox.Items)
            {
                if ((item.Content?.ToString() ?? "") == ocrLang)
                {
                    OcrLangBox.SelectedItem = item;
                    break;
                }
            }

        }

        //定时任务点击无反应？
        private TaskSchedulerService? _scheduler;

        private async void ToggleTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (_scheduler == null)
            {
                if (!(FrequencyComboBox.SelectedItem is ComboBoxItem freqItem) ||
                    !double.TryParse(freqItem.Tag?.ToString(), out double hours))
                {
                    hours = 24;
                }

                if (TaskTimePicker.SelectedTime == null)
                {
                    MessageBox.Show("请选择时间");
                    return;
                }

                var ts = TaskTimePicker.SelectedTime.Value.TimeOfDay;
                var now = DateTime.Now;
                var first = now.Date + ts;
                var interval = TimeSpan.FromHours(hours);

                if (first <= now)
                    first = first.Add(interval);

                _scheduler = new TaskSchedulerService(first, interval, async () =>
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        LastRunTimeLabel.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        TaskStatusLabel.Text = "运行中";
                        NextRunTimeLabel.Text = _scheduler!.NextRun.ToString("yyyy-MM-dd HH:mm");
                        // LogBox.Text += $"定时任务开始执行》》》";
                        // 将所有 LogBox.Text += ... 替换为通过 TextRange 追加文本
                        // 例如，将
                        // LogBox.Text += $"定时任务开始执行》》》";
                        // 替换为如下代码：

                        var textRange = new TextRange(LogBox.Document.ContentEnd, LogBox.Document.ContentEnd);
                        textRange.Text = $"定时任务开始执行》》》";
                        //时间到了点击按钮？
                        RunButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        
                    });
                });

                TaskStatusLabel.Text = "已启用";
                NextRunTimeLabel.Text = _scheduler.NextRun.ToString("yyyy‑MM‑dd HH:mm");
                // 其它类似的 LogBox.Text += ... 也需做同样替换
                // 例如：
                // LogBox.Text += $"定时任务已启用，上一次运行时间:{LastRunTimeLabel.Text}，下一次运行时间: {NextRunTimeLabel.Text}\n";
                // 替换为：
                var textRange1 = new TextRange(LogBox.Document.ContentEnd, LogBox.Document.ContentEnd);
                textRange1.Text = $"定时任务已启用，上一次运行时间:{LastRunTimeLabel.Text}，下一次运行时间: {NextRunTimeLabel.Text}\n";
                ToggleTaskButton.Visibility = Visibility.Collapsed;
                RunningToggleTaskButton.Visibility = Visibility.Visible;
            }
            else
            {
                _scheduler.Dispose();
                _scheduler = null;
                TaskStatusLabel.Text = "未启用";
                NextRunTimeLabel.Text = "--";
                // LogBox.Text += $"定时任务已停止，上一次运行时间:{LastRunTimeLabel.Text}，下一次运行时间: {NextRunTimeLabel.Text}\n";
                // 替换为：
                var textRange2 = new TextRange(LogBox.Document.ContentEnd, LogBox.Document.ContentEnd);
                textRange2.Text = $"定时任务已停止，上一次运行时间:{LastRunTimeLabel.Text}，下一次运行时间: {NextRunTimeLabel.Text}\n";
                RunningToggleTaskButton.Visibility = Visibility.Collapsed;
                ToggleTaskButton.Visibility = Visibility.Visible;
            }

            await Task.CompletedTask;

        }
        private async void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            // 恢复为 Settings.settings 里的默认值
            Properties.Settings.Default.Reset();
            Properties.Settings.Default.Save();

            await MDMessage.Show("设置已恢复为默认值");
        }

    }
}
