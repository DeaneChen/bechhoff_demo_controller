using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ModernWpf;

namespace PcHostGUI
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // WinUI/Fluent-like look & feel (Windows 10/11). Default to Light for readability.
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            base.OnStartup(e);
        }
    }
}
