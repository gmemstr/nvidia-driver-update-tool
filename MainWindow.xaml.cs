using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NvidiaDrivers.Nvidia;
using System.Diagnostics;

namespace NvidiaDrivers
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ContentRendered += MainWindow_ContentRendered;
        }

        // Open url on button click.
        private void Button_Click(object sender, RoutedEventArgs e) {
            Button button = (Button)sender;
            string url = (string)button.Tag;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        public void MainWindow_ContentRendered(object sender, EventArgs e) {
            (bool, string) result = Nvidia.API.IsNewDriver();
            Grid appGrid = (Grid)this.FindName("appGrid");

            (string, decimal) gpu = GPU.Current();
            gpuName.Text = gpu.Item1;
            string guessedGpu = API.GuessGPU();
            gpuName.Text += $" ({guessedGpu})";


            if (result.Item1) {
                // Show new driver available in application window.
                textBlock.Text = "New NVIDIA driver found!";
                // Create a button with a link.
                Button button = new Button();
                button.Content = "Download";
                button.Click += Button_Click;
                button.Width = 100;
                button.Height = 30;
                // Add url as button even argument
                button.Tag = result.Item2;
                // Add button to window.
                appGrid.Children.Add(button);
                // center button in appgrid.
                Grid.SetColumn(button, 1);
                Grid.SetRow(button, 2);
            } else {
                textBlock.Text = "You're up to date!\n(or unknown GPU)";
            }
        }

    }
}
