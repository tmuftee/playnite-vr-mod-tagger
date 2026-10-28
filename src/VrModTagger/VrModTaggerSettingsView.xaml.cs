using System.Windows;
using System.Windows.Controls;

namespace VrModTagger
{
    public partial class VrModTaggerSettingsView : UserControl
    {
        public VrModTaggerSettingsView()
        {
            InitializeComponent();

            // PasswordBox can't be data-bound (by WPF design, so the secret isn't kept in bindings), so it's synced by hand.
            DataContextChanged += (_, __) => LoadToken();
            Loaded += (_, __) => LoadToken();
        }

        private VrModTaggerSettingsViewModel ViewModel => DataContext as VrModTaggerSettingsViewModel;

        private void LoadToken()
        {
            var token = ViewModel?.Settings?.GitHubToken ?? string.Empty;
            if (GitHubTokenBox.Password != token)
            {
                GitHubTokenBox.Password = token;
            }
        }

        private void GitHubTokenBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.Settings != null)
            {
                ViewModel.Settings.GitHubToken = GitHubTokenBox.Password;
            }
        }
    }
}
