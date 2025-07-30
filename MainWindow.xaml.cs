using MaterialDesignThemes.Wpf;
using MaterialDesignColors;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace car_playwright_wpf
{
    

    public partial class MainWindow : Window
    {
        private NotifyIcon? trayIcon;
        private readonly PaletteHelper _paletteHelper = new();

        // 添加类成员变量
        private Process? runningProcess;
        private CancellationTokenSource? processCancellationToken;

        public MainWindow()
        {
            InitializeComponent();

            // 初始化开关状态
          

            // 获取程序集版本号
            string version = Assembly.GetExecutingAssembly()
                                     .GetName()
                                     .Version?
                                     .ToString() ?? "未知版本";

            // 设置窗口标题
            this.Title = $"用车复核工具 - v{version}";

            InitializeTrayIcon();
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


        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "用车复核助手"
            };

            trayIcon.DoubleClick += (s, e) => Dispatcher.Invoke(() => this.Show());
            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.Items.Add("显示", null, (s, e) => Dispatcher.Invoke(() => this.Show()));
            trayIcon.ContextMenuStrip.Items.Add("重启", null, (s, e) => System.Windows.Forms.Application.Restart());
            trayIcon.ContextMenuStrip.Items.Add("退出", null, (s, e) => System.Windows.Application.Current.Shutdown());
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            RunButton.IsEnabled = false;
            StopButton.IsEnabled = true;

            StatusLabel.Text = "🕐 正在运行，请稍候...";
            ProgressBarControl.Value = 0;
            LogBox.Clear();

            processCancellationToken = new CancellationTokenSource();

            string args = string.Join(" ", new string[]
            {
                $"--username \"{UsernameBox.Text}\"",
                $"--password \"{PasswordBox.Password}\"",
                $"--headless {HeadlessToggle.IsChecked?.ToString().ToLower()}",
                $"--slow_mo {SlowMoBox.Text}",
                $"--navigation_timeout {NavigationTimeoutBox.Text}",
                $"--default_timeout {DefaultTimeoutBox.Text}",
                $"--retry_times {RetryTimesBox.Text}",
                $"--delay_after_click {DelayAfterClickBox.Text}",
                $"--only_login {OnlyLoginToggle.IsChecked?.ToString().ToLower()}",
                $"--auto_submit {AutoSubmitToggle.IsChecked?.ToString().ToLower()}",
                $"--auto_exit {AutoExitToggle.IsChecked?.ToString().ToLower()}",
                $"--export_json {ExportJsonBox.IsChecked?.ToString().ToLower()}",
                $"--export_excel {ExportExcelToggle.IsChecked?.ToString().ToLower()}",
                $"--log_file \"{LogFileBox.Text}\"",
                $"--base_url \"{BaseUrlBox.Text}\"",
                $"--captcha_ocr_lang {OcrLangBox.Text}",
                $"--tesseract_path \"{TesseractPathBox.Text}\"",
                $"--excel_prefix \"{ExcelPrefixBox.Text}\"",
                $"--excel_monthly {ExcelMonthlyBox.IsChecked?.ToString().ToLower()}",
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
                RunButton.IsEnabled = true;
                StopButton.IsEnabled = false;
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
                RunButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                ProgressBarControl.Value = 100;

                runningProcess?.Dispose();
                processCancellationToken?.Dispose();
                runningProcess = null;
                processCancellationToken = null;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
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
            var config = new Dictionary<string, object>
            {
                ["username"] = UsernameBox.Text,
                ["password"] = PasswordBox.Password,
                ["headless"] = HeadlessToggle.IsChecked == true,
                ["slow_mo"] = int.Parse(SlowMoBox.Text),
                ["navigation_timeout"] = int.Parse(NavigationTimeoutBox.Text),
                ["default_timeout"] = int.Parse(DefaultTimeoutBox.Text),
                ["retry_times"] = int.Parse(RetryTimesBox.Text),
                ["delay_after_click"] = double.Parse(DelayAfterClickBox.Text),
                ["only_login"] = OnlyLoginToggle.IsChecked == true,
                ["auto_submit"] = AutoSubmitToggle.IsChecked == true,
                ["auto_exit"] = AutoExitToggle.IsChecked == true,
                ["export_json"] = ExportJsonBox.IsChecked == true,
                ["export_excel"] = ExportExcelToggle.IsChecked == true,
                ["log_file"] = LogFileBox.Text,
                ["base_url"] = BaseUrlBox.Text,
                ["captcha_ocr_lang"] = OcrLangBox.Text,
                ["tesseract_path"] = TesseractPathBox.Text,
                ["excel_prefix"] = ExcelPrefixBox.Text,
                ["excel_monthly"] = ExcelMonthlyBox.IsChecked == true,
                ["max_retry_on_error"] = int.Parse(MaxRetryOnErrorBox.Text),
                ["input_timeout"] = int.Parse(InputTimeoutBox.Text),
                ["order_time_threshold"] = int.Parse(OrderTimeThresholdBox.Text),
                ["order_max_retry"] = int.Parse(OrderMaxRetryBox.Text),
                ["order_retry_delay"] = double.Parse(OrderRetryDelayBox.Text),
                ["debug_mode"] = DebugModeBox.IsChecked == true,
                ["log_level"] = ((ComboBoxItem)LogLevelBox.SelectedItem)?.Content?.ToString()
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("config.json", json);

            StatusLabel.Text = "✅ 配置已保存";
        }

        private void AppendLog(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}\n";

            LogBox.AppendText(message + "\n");

            // WPF
            LogBox.ScrollToEnd();

            // 或者 WinForms
            // LogBox.SelectionStart = LogBox.TextLength;
            // LogBox.ScrollToCaret();
        }

        // 按钮点击或按回车时调用
        private void SendContinueSignal()
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

            string filePath = "continue.txt";
            try
            {
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, "go");
                    LogBox.AppendText("📨 已发送继续信号 (continue.txt)\n");
                    StatusLabel.Text = "✅ 已发送继续信号";
                }
                else
                {
                    LogBox.AppendText("📁 continue.txt 已存在，无需重复发送\n");
                }
            }
            catch (Exception ex)
            {
                LogBox.AppendText($"⚠️ 写入 continue.txt 失败: {ex.Message}\n");
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
            trayIcon?.Dispose();          // 清理托盘资源
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("这里是设置窗口，占位中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 清空日志按钮点击事件
        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Clear();  // 清空日志内容
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
                    System.IO.File.WriteAllText(dlg.FileName, LogBox.Text);
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
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "请选择脚本文件",
                Filter = "Python 脚本 (*.py)|*.py|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                PythonCodeBox.Text = dialog.FileName;
            }
        }

    }
}
