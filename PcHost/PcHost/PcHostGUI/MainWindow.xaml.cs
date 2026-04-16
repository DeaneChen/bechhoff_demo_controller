using System;
using System.Windows;
using ModernWpf.Controls;
using PcHostGUI.ViewModels;
using PcHostGUI.Views;

namespace PcHostGUI
{
    public partial class MainWindow : Window
    {
        private readonly OverviewView _overviewView = new OverviewView();
        private readonly NimServoView _nimServoView = new NimServoView();
        private readonly Rs485SensorView _rs485View = new Rs485SensorView();
        private readonly AnalogSensorView _analogView = new AnalogSensorView();
        private readonly LogsView _logsView = new LogsView();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                // Default selection
                if (Nav != null && Nav.SelectedItem == null && Nav.MenuItems.Count > 0)
                {
                    Nav.SelectedItem = Nav.MenuItems[0];
                }
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is IDisposable d)
            {
                try { d.Dispose(); } catch { }
            }

            base.OnClosed(e);
        }

        private void Nav_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var main = DataContext as MainViewModel;
            if (main == null)
            {
                PageHost.Content = null;
                return;
            }

            var item = args.SelectedItem as NavigationViewItem;
            string tag = item?.Tag as string;
            if (string.IsNullOrWhiteSpace(tag))
            {
                PageHost.Content = null;
                return;
            }

            switch (tag)
            {
                case "overview":
                    _overviewView.DataContext = main;
                    PageHost.Content = _overviewView;
                    break;
                case "motor":
                    _nimServoView.DataContext = main.NimServo;
                    PageHost.Content = _nimServoView;
                    break;
                case "laser":
                    _rs485View.DataContext = main.LaserDistance;
                    PageHost.Content = _rs485View;
                    break;
                case "vibration":
                    _rs485View.DataContext = main.Vibration;
                    PageHost.Content = _rs485View;
                    break;
                case "pressure":
                    _analogView.DataContext = main.Pressure;
                    PageHost.Content = _analogView;
                    break;
                case "torque":
                    _analogView.DataContext = main.Torque;
                    PageHost.Content = _analogView;
                    break;
                case "logs":
                    _logsView.DataContext = main;
                    PageHost.Content = _logsView;
                    break;
                default:
                    PageHost.Content = null;
                    break;
            }
        }
    }
}
