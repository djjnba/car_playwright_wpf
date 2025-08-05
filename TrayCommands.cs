using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace car_playwright_wpf
{
    public class TrayCommands
    {
        public ICommand OpenCommand => new RelayCommand(OpenMainWindow);
        public ICommand RestartCommand => new RelayCommand(RestartApp);
        public ICommand ExitCommand => new RelayCommand(ExitApp);
        public ICommand ToggleTaskCommand => new RelayCommand(ToggleScheduledTask);

        private void OpenMainWindow()
        {
            Application.Current.MainWindow?.Show();
            Application.Current.MainWindow.WindowState = WindowState.Normal;
            Application.Current.MainWindow.Activate();
        }

        private void RestartApp()
        {
            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void ExitApp()
        {
            Application.Current.Shutdown();
        }

        private void ToggleScheduledTask()
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ToggleScheduledTask();
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged;
    }
}