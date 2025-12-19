using System.Windows;
using System.Reflection;
using System.IO;
using Newtonsoft.Json;
using JsonDataViewer.Models; 
using JsonDataViewer.ViewModels;
using System.Windows.Input; // Needed for GotFocus/LostFocus events
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Controls;

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

            // Calculate column widths based on data after data is loaded
            Loaded += (s, e) => 
            {
                if (loadedData?.Groups != null)
                {
                    // User View: All users from all groups
                    var allUsers = loadedData.Groups.SelectMany(g => g.Users).ToList();
                    if (UsersDataGrid != null)
                        AutoCalculateColumnWidths(UsersDataGrid, allUsers);

                    // Group View, App View, Permission View will auto-update as users select items
                    // because the ViewModel's observable collections will be re-bound
                }
            };
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
            var version = asm.GetName().Version?.ToString() ?? "1.0.0";
            MessageBox.Show($"AppEnhancer Permissions Viewer version {version}", "About", MessageBoxButton.OK, MessageBoxImage.Information);
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

                // Recalculate column widths for the newly selected tab
                switch (tabControl.SelectedIndex)
                {
                    case 0:
                        if (UsersDataGrid != null)
                            UpdateColumnWidthsForGrid(UsersDataGrid);
                        break;
                    case 1:
                        if (GroupUsersDataGrid != null)
                            UpdateColumnWidthsForGrid(GroupUsersDataGrid);
                        break;
                    case 2:
                        if (AppUsersDataGrid != null)
                            UpdateColumnWidthsForGrid(AppUsersDataGrid);
                        break;
                    case 3:
                        if (PermUsersDataGrid != null)
                            UpdateColumnWidthsForGrid(PermUsersDataGrid);
                        break;
                    case 4:
                        // Settings tab: no data grids to adjust
                        break;
                }
            }
        }

        // Placeholder handling is removed; XAML uses an overlay TextBlock placeholder.

        private void FocusFilter_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;
            switch (vm.SelectedTabIndex)
            {
                case 0:
                    FilterTextBox?.Focus();
                    break;
                case 1:
                    GroupFilterTextBox?.Focus();
                    break;
                case 2:
                    AppFilterTextBox?.Focus();
                    break;
                case 3:
                    PermFilterTextBox?.Focus();
                    break;
            }
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.CommandParameter is System.Windows.Controls.TextBox tb)
            {
                tb.Text = string.Empty;
                tb.Focus();
            }
        }

        /// <summary>
        /// Measures the width of text based on font settings and calculates column widths
        /// by finding the longest value in each property and adding a buffer.
        /// </summary>
        private double MeasureTextWidth(string text, FontFamily fontFamily, double fontSize, FontWeight fontWeight)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily, FontStyles.Normal, fontWeight, FontStretches.Normal),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );

            return formattedText.Width;
        }

        /// <summary>
        /// Updates column widths for a specific DataGrid based on the current bound data.
        /// </summary>
        public void UpdateColumnWidthsForGrid(DataGrid grid)
        {
            if (grid == null)
                return;

            // Get the ItemsSource and recalculate widths
            if (grid.ItemsSource is System.Collections.IEnumerable items)
            {
                var userList = items.Cast<User>().ToList();
                if (userList.Count > 0)
                {
                    AutoCalculateColumnWidths(grid, userList);
                }
            }
        }

        /// <summary>
        /// Calculates fixed column widths based on data content. Finds the longest value
        /// in each column and adds a buffer for padding and header text.
        /// </summary>
        private void AutoCalculateColumnWidths(DataGrid grid, List<User> users)
        {
            if (grid?.Columns == null || users == null || users.Count == 0)
                return;

            // Column: index, property selector, header name
            var columnConfig = new List<(int Index, Func<User, string> Selector, string HeaderText)>
            {
                (0, u => u?.SamAccountName ?? "", "Teller No."),
                (1, u => u?.Name ?? "", "Name"),
                (2, u => u?.Title ?? "", "Title"),
                // Department is flexible (Width="*"), so skip it
            };

            // Font settings (match the DataGrid styling)
            var fontFamily = new FontFamily("Segoe UI");
            double fontSize = 12;
            var fontWeight = FontWeights.Normal;
            double headerPadding = 20; // Horizontal padding in header
            double buffer = 10;        // Extra buffer for comfort

            foreach (var (index, selector, headerText) in columnConfig)
            {
                // Find longest value in the column, filtering out nulls and handling exceptions
                var maxLength = users
                    .Select(u => 
                    {
                        try { return selector(u) ?? ""; }
                        catch { return ""; }
                    })
                    .Where(s => !string.IsNullOrEmpty(s))
                    .OrderByDescending(s => s.Length)
                    .FirstOrDefault() ?? "";

                // Measure header and content widths
                double headerWidth = MeasureTextWidth(headerText, fontFamily, fontSize, FontWeights.SemiBold);
                double contentWidth = MeasureTextWidth(maxLength, fontFamily, fontSize, fontWeight);

                // Take the maximum and add padding
                double calculatedWidth = Math.Max(headerWidth, contentWidth) + headerPadding + buffer;

                // Set the column width (minimum 80 for usability)
                if (grid.Columns.Count > index)
                {
                    grid.Columns[index].Width = Math.Max(80, calculatedWidth);
                }
            }
        }
    }
}