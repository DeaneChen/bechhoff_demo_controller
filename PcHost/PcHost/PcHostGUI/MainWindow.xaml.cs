using System;
using System.Windows;
using ModernWpf.Controls;
using PcHostGUI.ViewModels;
using PcHostGUI.Views;

namespace PcHostGUI
{
    public partial class MainWindow : Window
    {
        private readonly ControlPanelView _controlPanelView = new ControlPanelView();
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
                case "control":
                    _controlPanelView.DataContext = main.ControlPanel;
                    PageHost.Content = _controlPanelView;
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
