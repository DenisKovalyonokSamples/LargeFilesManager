using Serilog;
using System.Windows;

namespace LFM.Core.Themes
{
    public partial class DarkTheme
    {
        private void CloseWindow_Event(object sender, RoutedEventArgs e)
        {
            if (e.Source != null)
            {
                try
                {
                    this.CloseWind(Window.GetWindow((FrameworkElement)e.Source));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "DarkTheme CloseWindow_Event error.");
                }
            }
        }

        private void AutoMinimize_Event(object sender, RoutedEventArgs e)
        {
            if (e.Source != null)
            {
                try
                {
                    this.MaximizeRestore(Window.GetWindow((FrameworkElement)e.Source));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "DarkTheme AutoMinimize_Event error.");
                }
            }
        }

        private void Minimize_Event(object sender, RoutedEventArgs e)
        {
            if (e.Source != null)
            {
                try
                {
                    this.MinimizeWind(Window.GetWindow((FrameworkElement)e.Source));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "DarkTheme Minimize_Event error.");
                }
            }
        }

        public void CloseWind(Window window) => window.Close();

        public void MaximizeRestore(Window window)
        {
            if (window.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;
            }
            else if (window.WindowState == WindowState.Normal)
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        public void MinimizeWind(Window window) => window.WindowState = WindowState.Minimized;
    }
}
