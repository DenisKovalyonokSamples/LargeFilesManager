//using Microsoft.Extensions.Logging;
using System.Windows;

namespace LFM.Core.Themes
{
    public partial class ColourfulDarkTheme
    {
        //private readonly ILogger<ColourfulDarkTheme> _logger;

        //public ColourfulDarkTheme(ILogger<ColourfulDarkTheme> logger)
        //{
        //    _logger = logger;
        //}

        private void CloseWindow_Event(object sender, RoutedEventArgs e)
        {
            if (e.Source != null)
                try
                {
                    this.CloseWind(Window.GetWindow((FrameworkElement)e.Source));
                }
                catch (Exception ex)
                {
                    //_logger.LogError(ex, "Error on closing window event.");
                }
        }

        private void AutoMinimize_Event(object sender, RoutedEventArgs e)
        {
            if (e.Source != null)
                try
                {
                    this.MaximizeRestore(Window.GetWindow((FrameworkElement)e.Source));
                }
                catch (Exception ex)
                {
                    //_logger.LogError(ex, "Error on auto minimaze window event.");
                }
        }

        private void Minimize_Event(object sender, RoutedEventArgs e)
        {
            if (e.Source != null)
                try
                {
                    this.MinimizeWind(Window.GetWindow((FrameworkElement)e.Source));
                }
                catch (Exception ex)
                {
                    //_logger.LogError(ex, "Error on minimaze window event.");
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
