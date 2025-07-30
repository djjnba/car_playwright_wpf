using System;
using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;

namespace car_playwright_wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 应用唯一互斥锁名，请确保唯一（可以加项目名）
            bool isNewInstance;
            _mutex = new Mutex(true, "car_playwright_wpf_single_instance_mutex", out isNewInstance);

            if (!isNewInstance)
            {
                System.Windows.MessageBox.Show("⚠️ 程序已经在运行中！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }
    }
}
