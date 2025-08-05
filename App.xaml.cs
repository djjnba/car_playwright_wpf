using System;
using System.Threading;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace car_playwright_wpf
{
    public partial class App : Application
    {
        private const string MutexName = "car_playwright_wpf_unique_app_mutex";
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 单实例检查
            // 确保MainWindow只初始化一次
            if (MainWindow == null)
            {
                MainWindow = new MainWindow();
                MainWindow.Closed += (s, args) => Shutdown();
            }
            MainWindow.Show();

            // 初始化托盘图标
            var trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            trayIcon.DataContext = new TrayCommands();

            // 如果需要，可以在此决定是否显示窗口
            // MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {

            //退出时清理托盘图标
            var trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            if (trayIcon != null)
            {
                trayIcon.Dispose();
            }
            base.OnExit(e);

            // 释放互斥锁资源
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (MainWindow == null) return;

            if (!MainWindow.IsVisible || MainWindow.WindowState == WindowState.Minimized)
            {
                MainWindow.Show();
                MainWindow.WindowState = WindowState.Normal;
            }
            else
            {
                MainWindow.WindowState = WindowState.Minimized;
                //托盘弹出提示
                var trayIcon = (TaskbarIcon)FindResource("TrayIcon");
                //trayIcon.ShowBalloonTip("提示", "应用程序已最小化到托盘",BalloonIcon.Info);
            }
            MainWindow.Activate();
        }



    }
}
