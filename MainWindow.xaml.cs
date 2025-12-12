using System.Windows;
using System.Reflection;
using System.IO;
using Newtonsoft.Json;
using JsonDataViewer.Models; 
using JsonDataViewer.ViewModels;
using System.Windows.Input; // Needed for GotFocus/LostFocus events

namespace JsonDataViewer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Resolve data file relative to the running executable directory
            GroupData? loadedData = null;
            string jsonFilePath = ResolveJsonFilePath();
            try
            {
                string json = File.ReadAllText(jsonFilePath);
                loadedData = JsonConvert.DeserializeObject<GroupData>(json);
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show($"Data file not found. Tried:\n  {Path.Combine("C:\\Winstall", "group_data.json")}\n  {Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory(), "group_data.json")}\nMake sure the file exists in one of these locations.", "File Not Found Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (JsonException ex)
            {
                MessageBox.Show($"Error deserializing JSON data:\n{ex.Message}", "JSON Parsing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Set the DataContext to the ViewModel
            var viewModel = new MainWindowViewModel(loadedData);
            viewModel.SetHeaderForTab(0); // Initialize with User View header
            DataContext = viewModel;
        }

        // --- XAML Menu Click Handlers (Resolves CS1061 errors) ---
        private void MenuRefresh_Click(object sender, RoutedEventArgs e)
        {
            // Reload the JSON from disk and replace the ViewModel (keeps the same window)
            string jsonFilePath = ResolveJsonFilePath();

            try
            {
                string json = File.ReadAllText(jsonFilePath);
                var loadedData = JsonConvert.DeserializeObject<GroupData>(json);
                if (loadedData != null)
                {
                    DataContext = new MainWindowViewModel(loadedData);
                }
                else
                {
                    MessageBox.Show("Reload succeeded but data was empty.", "Reload", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show($"Data file not found. Tried:\n  {Path.Combine("C:\\Winstall", "group_data.json")}\n  {Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory(), "group_data.json")}\nMake sure the file exists in one of these locations.", "File Not Found Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (JsonException ex)
            {
                MessageBox.Show($"Error deserializing JSON data on reload:\n{ex.Message}", "JSON Parsing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        // Prefer configuration from installer folder, then fallback to exe folder.
        private string ResolveJsonFilePath()
        {
            string installerFolder = Path.Combine("C:", "Winstall");
            string installerPath = Path.Combine(installerFolder, "group_data.json");
            if (File.Exists(installerPath)) return installerPath;

            string exeFolder = System.AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
            string exePath = Path.Combine(exeFolder, "group_data.json");
            return File.Exists(exePath) ? exePath : installerPath; // return installerPath even if missing for helpful error messages
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var name = asm.GetName().Name ?? "JsonDataViewer";
            var version = asm.GetName().Version?.ToString() ?? "1.0.0";
            MessageBox.Show($"{name} version {version}", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SendIssue_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder
            MessageBox.Show("Contact IT support regarding issues.", "Report Issue");
        }
        
        // --- XAML Filter Event Handlers (Resolves CS1061 errors) ---
        private void FilterTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
             // Pass the filtering task to the ViewModel
             if (DataContext is MainWindowViewModel viewModel)
             {
                 viewModel.FilterUsers(((System.Windows.Controls.TextBox)sender).Text);
             }
        }
        
        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TabControl tabControl && DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SetHeaderForTab(tabControl.SelectedIndex);
            }
        }

        private const string FilterPlaceholder = "Filter by Teller No, Name, Title, Department";

        private void FilterTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = FilterPlaceholder;
                    tb.Foreground = System.Windows.Media.Brushes.Gray;
                    if (DataContext is MainWindowViewModel vm)
                    {
                        vm.UserSearchText = string.Empty; // ensure ViewModel not set to placeholder
                    }
                }
            }
        }

        private void FilterTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                if (tb.Text == FilterPlaceholder)
                {
                    tb.Text = string.Empty;
                }
                tb.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void FilterTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = FilterPlaceholder;
                    tb.Foreground = System.Windows.Media.Brushes.Gray;
                    if (DataContext is MainWindowViewModel vm)
                    {
                        vm.UserSearchText = string.Empty;
                    }
                }
                else
                {
                    if (DataContext is MainWindowViewModel vm)
                    {
                        vm.UserSearchText = tb.Text;
                    }
                }
            }
        }
    }
}