//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;

namespace LFM.Core.Themes
{
    public partial class DarkTheme
    {
        //private readonly ILogger<DarkTheme> _logger;

        //public DarkTheme(ILogger<DarkTheme> logger)
        //{
        //    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        //}

        private void CloseWindow_Event(object sender, RoutedEventArgs e)
        {
            if (e.Source != null)
                try
                {
                    this.CloseWind(Window.GetWindow((FrameworkElement)e.Source));
                throw new System.Exception();
                }
                catch
                {
                    Log.Information("Test");
                }
        }

        private void AutoMinimize_Event(object sender, RoutedEventArgs e)
        {
            if (e.Source != null)
                try
                {
                    this.MaximizeRestore(Window.GetWindow((FrameworkElement)e.Source));
                }
                catch
                {
                }
        }

        private void Minimize_Event(object sender, RoutedEventArgs e)
        {
            if (e.Source != null)
                try
                {
                    this.MinimizeWind(Window.GetWindow((FrameworkElement)e.Source));
                }
                catch
                {
                }
        }

        public void CloseWind(Window window) => window.Close();

        public void MaximizeRestore(Window window)
        {
            if (window.WindowState == WindowState.Maximized)
                window.WindowState = WindowState.Normal;
            else if (window.WindowState == WindowState.Normal)
                window.WindowState = WindowState.Maximized;
        }

        public void MinimizeWind(Window window) => window.WindowState = WindowState.Minimized;
    }
}
