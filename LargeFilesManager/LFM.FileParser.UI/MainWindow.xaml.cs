using LFM.FileSorter.ViewModels;
using System.Windows;

namespace LFM.FileSorter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.StateChanged += (s, e) =>
            {
                if (this.WindowState == WindowState.Maximized)
                    this.WindowState = WindowState.Normal;
            };

            DataContext = new FileSorterViewModel();
        }
    }
}