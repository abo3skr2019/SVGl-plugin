using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.svgl
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private readonly SettingsViewModel _viewModel;

        /// <summary>
        /// Default constructor required for XAML designer
        /// </summary>
        public SettingsControl()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// Constructor with plugin context and view model
        /// </summary>
        /// <param name="context">Plugin initialization context</param>
        /// <param name="viewModel">Settings view model</param>
        public SettingsControl(PluginInitContext context, SettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }        /// <summary>
        /// Event handler for the Clear Cache button
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearCacheCommand();
        }
    }
}
