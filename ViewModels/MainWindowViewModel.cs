using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input; 
using System.Windows.Threading;
using JsonDataViewer.Models; 
// The ViewMode enum is now accessed via the namespace JsonDataViewer.ViewModels
namespace JsonDataViewer.ViewModels
{
    // RelayCommand is provided in ViewModels/RelayCommand.cs

    public class MainWindowViewModel : ObservableObject
    {
        private GroupData? _data;
        private List<Group>? _allGroups;

        public CollectionViewSource UsersView { get; } = new CollectionViewSource();
        public CollectionViewSource GroupUsersView { get; } = new CollectionViewSource();
        public CollectionViewSource AppUsersView { get; } = new CollectionViewSource();
        public ObservableCollection<Group> UserGroups { get; } = new ObservableCollection<Group>();
        public ObservableCollection<AppPermission> GroupApps { get; } = new ObservableCollection<AppPermission>();
        public ObservableCollection<Permission> AppPermissions { get; } = new ObservableCollection<Permission>();
        public ObservableCollection<Group> AppGroups { get; } = new ObservableCollection<Group>();
        public ObservableCollection<Permission> AllPermissions { get; } = new ObservableCollection<Permission>();
        public ObservableCollection<AppPermission> PermApps { get; } = new ObservableCollection<AppPermission>();
        public CollectionViewSource PermUsersView { get; } = new CollectionViewSource();
        // Group View tab collections and filter
        public CollectionViewSource GroupViewUsers { get; } = new CollectionViewSource();
        private string _groupViewUserSearchText = string.Empty;
        public string GroupViewUserSearchText
        {
            get => _groupViewUserSearchText;
            set
            {
                if (SetProperty(ref _groupViewUserSearchText, value))
                {
                    _groupFilterTimer.Stop();
                    _groupFilterTimer.Start();
                }
            }
        }
        
        // Application View filter
        private string _appUserSearchText = string.Empty;
        public string AppUserSearchText
        {
            get => _appUserSearchText;
            set
            {
                if (SetProperty(ref _appUserSearchText, value))
                {
                    _appFilterTimer.Stop();
                    _appFilterTimer.Start();
                }
            }
        }
        
        // Permission View filter
        private string _permUserSearchText = string.Empty;
        public string PermUserSearchText
        {
            get => _permUserSearchText;
            set
            {
                if (SetProperty(ref _permUserSearchText, value))
                {
                    _permFilterTimer.Stop();
                    _permFilterTimer.Start();
                }
            }
        }
        public ICommand ClearSelectedUserCommand { get; private set; }

        // Adapter for XAML: a small item type used by the ComboBox in XAML
        public class DetailViewModeItem
        {
            public string Name { get; set; } = string.Empty;
            public ViewMode Mode { get; set; }
        }

        // XAML-friendly collections / properties (adapters)
        public System.ComponentModel.ICollectionView? AllUsersList => UsersView.View;
        public IEnumerable<Group> AllGroupsList => _allGroups ?? Enumerable.Empty<Group>();
        public IEnumerable<AppPermission> AllAppsList => (_allGroups?.SelectMany(g => g.AppPermissions ?? Enumerable.Empty<AppPermission>()).GroupBy(ap => ap.AppName).Select(g => g.First()).OrderBy(ap => ap.AppName) ?? Enumerable.Empty<AppPermission>()).ToList();
        public IEnumerable<Permission> AllPermissionsList => AllPermissions.OrderBy(p => p.PermissionName);

        public ObservableCollection<DetailViewModeItem> DetailViewModes { get; } = new ObservableCollection<DetailViewModeItem>();
        private DetailViewModeItem? _selectedDetailViewMode;
        public DetailViewModeItem? SelectedDetailViewMode
        {
            get => _selectedDetailViewMode;
            set
            {
                if (SetProperty(ref _selectedDetailViewMode, value))
                {
                    if (value != null) CurrentViewMode = value.Mode;
                }
            }
        }

        private string _userSearchText = string.Empty;
        public string UserSearchText
        {
            get => _userSearchText;
            set
            {
                if (SetProperty(ref _userSearchText, value))
                {
                    _userFilterTimer.Stop();
                    _userFilterTimer.Start();
                }
            }
        }

        // Panel adapter properties used by XAML
        public string Panel1Header => LeftPanelHeader;
        public string Panel2Header => MiddlePanelHeader;
        public string Panel3Header => RightPanelHeader;

        public IEnumerable<Group> Panel1ItemsSource => UserGroups;
        public IEnumerable<AppPermission> Panel2ItemsSource => GroupApps;
        public IEnumerable<Permission> Panel3ItemsSource => AppPermissions;

        // Selected item adapters used by XAML
        public Group? SelectedPanel1Item
        {
            get => SelectedGroup;
            set => SelectedGroup = value;
        }

        public AppPermission? SelectedPanel2Item
        {
            get => SelectedApp;
            set => SelectedApp = value;
        }

        // Tab-specific adapters
        public Group? SelectedGroupTabGroup
        {
            get => SelectedGroup;
            set => SelectedGroup = value;
        }

        public AppPermission? SelectedAppTabApp
        {
            get => SelectedApp;
            set => SelectedApp = value;
        }

        public Permission? SelectedPermTabPerm
        {
            get => _selectedPerm;
            set => SelectedPerm = value;
        }

        // App selected within the Permission View tab
        public AppPermission? SelectedPermTabApp
        {
            get => _selectedApp;
            set => SelectedApp = value;
        }

        // Adapter properties for Permission View XAML
        public IEnumerable<AppPermission> PermApplications => PermApps;
        public IEnumerable<Group> PermGroups =>
            (_allGroups != null
                ? _allGroups.Where(g => g.AppPermissions?.Any(ap =>
                        ((_selectedApp == null)
                            || string.Equals(ap.AppName, _selectedApp.AppName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(ap.DisplayName, _selectedApp.DisplayName, StringComparison.OrdinalIgnoreCase))
                        && ap.PermissionsData != null
                        && ap.PermissionsData.Any(p => p.Key == _selectedPerm?.PermissionCode && (int.TryParse(p.Value.ToString() ?? "", out int val) && val == 1))
                    ) == true)
                : Enumerable.Empty<Group>())
            .ToList();

        // ---------------------------------------------
        // VIEW MODE PROPERTIES AND CONTROL
        // ---------------------------------------------
        
        public Dictionary<ViewMode, string> ViewModeDisplayMap { get; } = new Dictionary<ViewMode, string>
        {
            { ViewMode.UserGroupAppPerm, "User-Group-App-Perm" },
            { ViewMode.UserAppGroupPerm, "User-App-Group-Perm" },
            { ViewMode.UserPermAppGroup, "User-Perm-App-Group" }
        };

        private ViewMode _currentViewMode = ViewMode.UserGroupAppPerm;
        public ViewMode CurrentViewMode
        {
            get => _currentViewMode;
            set
            {
                if (SetProperty(ref _currentViewMode, value))
                {
                    _selectedViewModeName = ViewModeDisplayMap[value];
                    OnPropertyChanged(nameof(SelectedViewModeName)); 

                    LoadDetailDataForMode(SelectedUser);

                    // notify both original headers and adapter panel headers used by XAML
                    OnPropertyChanged(nameof(LeftPanelHeader));
                    OnPropertyChanged(nameof(MiddlePanelHeader));
                    OnPropertyChanged(nameof(RightPanelHeader));
                    OnPropertyChanged(nameof(Panel1Header));
                    OnPropertyChanged(nameof(Panel2Header));
                    OnPropertyChanged(nameof(Panel3Header));
                    OnPropertyChanged(nameof(Panel1ColumnHeader));
                    OnPropertyChanged(nameof(Panel2ColumnHeader));
                    OnPropertyChanged(nameof(Panel3ColumnHeader));
                }
            }
        }
        
        private string _selectedViewModeName;
        public string SelectedViewModeName
        {
            get => _selectedViewModeName;
            set
            {
                if (SetProperty(ref _selectedViewModeName, value))
                {
                    var selectedMode = ViewModeDisplayMap.FirstOrDefault(x => x.Value == value).Key;
                    CurrentViewMode = selectedMode; 
                }
            }
        }

        public string LeftPanelHeader => 
            CurrentViewMode == ViewMode.UserGroupAppPerm ? "Groups" :
            CurrentViewMode == ViewMode.UserAppGroupPerm ? "Applications" :
            "Permissions";

        public string MiddlePanelHeader => 
            CurrentViewMode == ViewMode.UserGroupAppPerm ? "Applications" :
            CurrentViewMode == ViewMode.UserAppGroupPerm ? "Groups" :
            "Applications";

        public string RightPanelHeader => 
            CurrentViewMode == ViewMode.UserGroupAppPerm ? "Permissions" :
            CurrentViewMode == ViewMode.UserAppGroupPerm ? "Permissions" :
            "Groups";

        public string Panel1ColumnHeader => 
            CurrentViewMode == ViewMode.UserGroupAppPerm ? "Group" :
            CurrentViewMode == ViewMode.UserAppGroupPerm ? "Application" :
            "Permission";

        public string Panel2ColumnHeader => 
            CurrentViewMode == ViewMode.UserGroupAppPerm ? "Application" :
            CurrentViewMode == ViewMode.UserAppGroupPerm ? "Group" :
            "Application";

        public string Panel3ColumnHeader => 
            CurrentViewMode == ViewMode.UserGroupAppPerm ? "Permission" :
            CurrentViewMode == ViewMode.UserAppGroupPerm ? "Permission" :
            "Group";


        // --- Global Selection Properties ---

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    // When the tab changes, re-evaluate if the button should be enabled.
                    OnPropertyChanged(nameof(IsAnythingSelected));
                    (ClearAllSelectionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsAnythingSelected
        {
            get
            {
                return _selectedTabIndex switch
                {
                    0 => SelectedUser != null, // User View
                    1 => SelectedGroupTabGroup != null, // Group View
                    2 => SelectedAppTabApp != null, // Application View
                    3 => SelectedPermTabPerm != null, // Permission View
                    _ => false,
                };
            }
        }

        public ICommand ClearAllSelectionsCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }

        private void ExecuteClearAllSelections()
        {
            switch (SelectedTabIndex)
            {
                case 0: // User View
                    SelectedUser = null;
                    break;
                case 1: // Group View
                    SelectedGroupTabGroup = null;
                    break;
                case 2: // Application View
                    SelectedAppTabApp = null;
                    break;
                case 3: // Permission View
                    SelectedPermTabPerm = null;
                    break;
            }
        }


        // Debounce timers for filtering
        private readonly DispatcherTimer _userFilterTimer;
        private readonly DispatcherTimer _groupFilterTimer;
        private readonly DispatcherTimer _appFilterTimer;
        private readonly DispatcherTimer _permFilterTimer;

        public MainWindowViewModel(GroupData? data)
        {
            _data = data;
            _allGroups = _data?.Groups;
            // Initialize debounce timers (200ms)
            _userFilterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _userFilterTimer.Tick += (_, __) => { _userFilterTimer.Stop(); FilterUsers(_userSearchText); };
            _groupFilterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _groupFilterTimer.Tick += (_, __) => { _groupFilterTimer.Stop(); FilterGroupUsers(_groupUserSearchText); };
            _appFilterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _appFilterTimer.Tick += (_, __) => { _appFilterTimer.Stop(); FilterAppUsers(_appUserSearchText); };
            _permFilterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _permFilterTimer.Tick += (_, __) => { _permFilterTimer.Stop(); FilterPermUsers(_permUserSearchText); };
            LoadAllUsers();
            LoadAllPermissions();
            SelectedUser = null; 
            
            _selectedViewModeName = ViewModeDisplayMap[CurrentViewMode];

            // Populate DetailViewModes for XAML ComboBox
            foreach (var kv in ViewModeDisplayMap)
            {
                DetailViewModes.Add(new DetailViewModeItem { Name = kv.Value, Mode = kv.Key });
            }
            SelectedDetailViewMode = DetailViewModes.FirstOrDefault(d => d.Mode == CurrentViewMode);

            ClearSelectedUserCommand = new RelayCommand(_ => ClearSelectedUser());
            ClearAllSelectionsCommand = new RelayCommand(_ => ExecuteClearAllSelections(), _ => IsAnythingSelected);
            NavigateToSettingsCommand = new RelayCommand(_ => SelectedTabIndex = 4);
            TogglePanel1SortCommand = new RelayCommand(_ => TogglePanel1Sort());
            TogglePanel2SortCommand = new RelayCommand(_ => TogglePanel2Sort());
            TogglePanel3SortCommand = new RelayCommand(_ => TogglePanel3Sort());
            ToggleGroupTabPanel1SortCommand = new RelayCommand(_ => ToggleSort(GroupTabPanel1SortDirection, dir => GroupTabPanel1SortDirection = dir, AllGroupsList, "Name"));
            ToggleGroupTabPanel2SortCommand = new RelayCommand(_ => ToggleSort(GroupTabPanel2SortDirection, dir => GroupTabPanel2SortDirection = dir, GroupApps, "AppName"));
            ToggleGroupTabPanel3SortCommand = new RelayCommand(_ => ToggleSort(GroupTabPanel3SortDirection, dir => GroupTabPanel3SortDirection = dir, AppPermissions, "PermissionName"));
            ToggleAppTabPanel1SortCommand = new RelayCommand(_ => ToggleSort(AppTabPanel1SortDirection, dir => AppTabPanel1SortDirection = dir, AllAppsList, "AppName"));
            ToggleAppTabPanel2SortCommand = new RelayCommand(_ => ToggleSort(AppTabPanel2SortDirection, dir => AppTabPanel2SortDirection = dir, AppGroups, "Name"));
            ToggleAppTabPanel3SortCommand = new RelayCommand(_ => ToggleSort(AppTabPanel3SortDirection, dir => AppTabPanel3SortDirection = dir, AppPermissions, "PermissionName"));
            TogglePermTabPanel1SortCommand = new RelayCommand(_ => ToggleSort(PermTabPanel1SortDirection, dir => PermTabPanel1SortDirection = dir, AllPermissionsList, "PermissionName"));
            TogglePermTabPanel2SortCommand = new RelayCommand(_ => ToggleSort(PermTabPanel2SortDirection, dir => PermTabPanel2SortDirection = dir, PermApplications, "AppName"));
            TogglePermTabPanel3SortCommand = new RelayCommand(_ => ToggleSort(PermTabPanel3SortDirection, dir => PermTabPanel3SortDirection = dir, PermGroups, "Name"));
        }

        // Sort state properties for the User View small panels
        private ListSortDirection? _panel1SortDirection;
        public ListSortDirection? Panel1SortDirection
        {
            get => _panel1SortDirection;
            set => SetProperty(ref _panel1SortDirection, value);
        }

        private ListSortDirection? _panel2SortDirection;
        public ListSortDirection? Panel2SortDirection
        {
            get => _panel2SortDirection;
            set => SetProperty(ref _panel2SortDirection, value);
        }

        private ListSortDirection? _panel3SortDirection;
        public ListSortDirection? Panel3SortDirection
        {
            get => _panel3SortDirection;
            set => SetProperty(ref _panel3SortDirection, value);
        }

        // Commands to toggle sorting for the small panels
        public ICommand TogglePanel1SortCommand { get; }
        public ICommand TogglePanel2SortCommand { get; }
        public ICommand TogglePanel3SortCommand { get; }

        // Sort state and commands for Group View panels
        private ListSortDirection? _groupTabPanel1SortDirection;
        public ListSortDirection? GroupTabPanel1SortDirection { get => _groupTabPanel1SortDirection; set => SetProperty(ref _groupTabPanel1SortDirection, value); }
        public ICommand ToggleGroupTabPanel1SortCommand { get; }

        private ListSortDirection? _groupTabPanel2SortDirection;
        public ListSortDirection? GroupTabPanel2SortDirection { get => _groupTabPanel2SortDirection; set => SetProperty(ref _groupTabPanel2SortDirection, value); }
        public ICommand ToggleGroupTabPanel2SortCommand { get; }

        private ListSortDirection? _groupTabPanel3SortDirection;
        public ListSortDirection? GroupTabPanel3SortDirection { get => _groupTabPanel3SortDirection; set => SetProperty(ref _groupTabPanel3SortDirection, value); }
        public ICommand ToggleGroupTabPanel3SortCommand { get; }

        // Sort state and commands for App View panels
        private ListSortDirection? _appTabPanel1SortDirection;
        public ListSortDirection? AppTabPanel1SortDirection { get => _appTabPanel1SortDirection; set => SetProperty(ref _appTabPanel1SortDirection, value); }
        public ICommand ToggleAppTabPanel1SortCommand { get; }

        private ListSortDirection? _appTabPanel2SortDirection;
        public ListSortDirection? AppTabPanel2SortDirection { get => _appTabPanel2SortDirection; set => SetProperty(ref _appTabPanel2SortDirection, value); }
        public ICommand ToggleAppTabPanel2SortCommand { get; }

        private ListSortDirection? _appTabPanel3SortDirection;
        public ListSortDirection? AppTabPanel3SortDirection { get => _appTabPanel3SortDirection; set => SetProperty(ref _appTabPanel3SortDirection, value); }
        public ICommand ToggleAppTabPanel3SortCommand { get; }

        // Sort state and commands for Perm View panels
        private ListSortDirection? _permTabPanel1SortDirection;
        public ListSortDirection? PermTabPanel1SortDirection { get => _permTabPanel1SortDirection; set => SetProperty(ref _permTabPanel1SortDirection, value); }
        public ICommand TogglePermTabPanel1SortCommand { get; }

        private ListSortDirection? _permTabPanel2SortDirection;
        public ListSortDirection? PermTabPanel2SortDirection { get => _permTabPanel2SortDirection; set => SetProperty(ref _permTabPanel2SortDirection, value); }
        public ICommand TogglePermTabPanel2SortCommand { get; }

        private ListSortDirection? _permTabPanel3SortDirection;
        public ListSortDirection? PermTabPanel3SortDirection { get => _permTabPanel3SortDirection; set => SetProperty(ref _permTabPanel3SortDirection, value); }
        public ICommand TogglePermTabPanel3SortCommand { get; }

        // Generic sorting method
        private void ToggleSort(ListSortDirection? currentDirection, Action<ListSortDirection?> setDirection, System.Collections.IEnumerable itemsSource, string propertyName)
        {
            var view = CollectionViewSource.GetDefaultView(itemsSource);
            if (view == null) return;

            view.SortDescriptions.Clear();
            ListSortDirection newDirection;

            if (currentDirection == ListSortDirection.Ascending)
            {
                newDirection = ListSortDirection.Descending;
            }
            else
            {
                newDirection = ListSortDirection.Ascending;
            }
            setDirection(newDirection);
            view.SortDescriptions.Add(new SortDescription(propertyName, newDirection));
            view.Refresh();
        }

        private void TogglePanel1Sort()
        {
            var view = CollectionViewSource.GetDefaultView(Panel1ItemsSource);
            if (view == null) return;
            view.SortDescriptions.Clear();
            if (Panel1SortDirection == ListSortDirection.Ascending)
            {
                Panel1SortDirection = ListSortDirection.Descending;
                view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Descending));
            }
            else
            {
                Panel1SortDirection = ListSortDirection.Ascending;
                view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            }
            view.Refresh();
        }

        private void TogglePanel2Sort()
        {
            var view = CollectionViewSource.GetDefaultView(Panel2ItemsSource);
            if (view == null) return;
            view.SortDescriptions.Clear();
            if (Panel2SortDirection == ListSortDirection.Ascending)
            {
                Panel2SortDirection = ListSortDirection.Descending;
                view.SortDescriptions.Add(new SortDescription("AppName", ListSortDirection.Descending));
            }
            else
            {
                Panel2SortDirection = ListSortDirection.Ascending;
                view.SortDescriptions.Add(new SortDescription("AppName", ListSortDirection.Ascending));
            }
            view.Refresh();
        }

        private void TogglePanel3Sort()
        {
            var view = CollectionViewSource.GetDefaultView(Panel3ItemsSource);
            if (view == null) return;
            view.SortDescriptions.Clear();
            if (Panel3SortDirection == ListSortDirection.Ascending)
            {
                Panel3SortDirection = ListSortDirection.Descending;
                view.SortDescriptions.Add(new SortDescription("PermissionName", ListSortDirection.Descending));
            }
            else
            {
                Panel3SortDirection = ListSortDirection.Ascending;
                view.SortDescriptions.Add(new SortDescription("PermissionName", ListSortDirection.Ascending));
            }
            view.Refresh();
        }

        // ---------------------------------------------
        // Public Properties for DataGrid SelectedItem Bindings
        // ---------------------------------------------

        private User? _selectedUser;
        public User? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    LoadDetailDataForMode(value);
                    OnPropertyChanged(nameof(UserCountText));
                    OnPropertyChanged(nameof(IsUserSelected));
                    OnPropertyChanged(nameof(IsAnythingSelected));
                    (ClearAllSelectionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        
        public bool IsUserSelected => _selectedUser != null;

        private Group? _selectedGroup;
        public Group? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value))
                {
                    UpdateGroupApps(value);
                    LoadGroupUsers(value);
                    OnPropertyChanged(nameof(IsAnythingSelected));
                    (ClearAllSelectionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private AppPermission? _selectedApp;
        public AppPermission? SelectedApp
        {
            get => _selectedApp;
            set
            {
                if (SetProperty(ref _selectedApp, value))
                {
                    UpdateAppPermissions(value);
                    LoadAppUsers(value);
                    OnPropertyChanged(nameof(IsAnythingSelected));
                    (ClearAllSelectionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    // Update dependent computed collections
                    OnPropertyChanged(nameof(PermGroups));
                }
            }
        }
        
        private void ClearSelectedUser()
        {
            SelectedUser = null;
        }

        // ---------------------------------------------
        // Remaining Methods (Unchanged Logic)
        // ---------------------------------------------
        
        private void LoadDetailDataForMode(User? user)
        {
            SelectedGroup = null;
            SelectedApp = null;
            UserGroups.Clear();
            GroupApps.Clear();
            AppPermissions.Clear();
            
            if (user == null) return; 

            switch (CurrentViewMode)
            {
                case ViewMode.UserGroupAppPerm:
                    LoadUserGroupAppPerm(user);
                    break;
                case ViewMode.UserAppGroupPerm:
                    LoadUserAppGroupPerm(user);
                    break;
                case ViewMode.UserPermAppGroup:
                    LoadUserPermAppGroup(user);
                    break;
            }
        }

        private void LoadUserGroupAppPerm(User user)
        {
            if (_allGroups == null) return; 
            
            var groups = _allGroups
                .Where(g => g.Users?.Any(u => u.SamAccountName == user.SamAccountName) == true)
                .ToList();
            groups.ForEach(g => UserGroups.Add(g));
            
            SelectedGroup = UserGroups.FirstOrDefault(); 
        }

        private void LoadUserAppGroupPerm(User user)
        {
            if (_allGroups == null) return; 
            
            var appsForUser = _allGroups
                .Where(g => g.Users?.Any(u => u.SamAccountName == user.SamAccountName) == true)
                .SelectMany(g => g.AppPermissions ?? Enumerable.Empty<AppPermission>()) 
                .GroupBy(ap => ap.AppName)
                .Select(g => g.First())
                .ToList();

            appsForUser.ForEach(ap => UserGroups.Add(new Group { GroupName = ap.AppName, AppPermissions = new List<AppPermission> { ap } }));
            
            SelectedGroup = UserGroups.FirstOrDefault();
        }

        private void LoadUserPermAppGroup(User user)
        {
            if (_allGroups == null) return; 

            var allGrantedPerms = _allGroups
                .Where(g => g.Users?.Any(u => u.SamAccountName == user.SamAccountName) == true)
                .SelectMany(g => g.AppPermissions ?? Enumerable.Empty<AppPermission>()) 
                .SelectMany(ap => ap.PermissionsData ?? Enumerable.Empty<KeyValuePair<string, object>>())
                .Where(p => p.Key.StartsWith("perm", StringComparison.OrdinalIgnoreCase) && p.Value.ToString() == "1")
                .GroupBy(p => p.Key)
                .Select(g => g.First())
                .ToList();
            
            allGrantedPerms.ForEach(p => 
            {
                string permCode = p.Key;
                string permName = permCode; // Use the code directly
                
                UserGroups.Add(new Group { GroupName = permName, SamAccountName = permCode }); 
            });

            SelectedGroup = UserGroups.FirstOrDefault();
        }

        private void UpdateGroupApps(Group? selectedItem)
        {
            GroupApps.Clear();
            AppPermissions.Clear();
            SelectedApp = null;
            
            if (selectedItem == null || _allGroups == null) return;
            
            // Always add the group's applications (works for both User View and Group View tab)
            selectedItem.AppPermissions?.ToList().ForEach(ap => GroupApps.Add(ap));
            
            // Also handle User View mode-specific logic for permutation views
            switch (CurrentViewMode)
            {
                case ViewMode.UserAppGroupPerm:
                    GroupApps.Clear();
                    var groupsForApp = _allGroups
                        .Where(g => g.AppPermissions?.Any(ap => ap.AppName == selectedItem.GroupName) == true)
                        .ToList();
                        
                    groupsForApp.ForEach(g => GroupApps.Add(new AppPermission { AppName = g.GroupName })); 
                    break;
                    
                case ViewMode.UserPermAppGroup:
                    GroupApps.Clear();
                    string? permCode = selectedItem.SamAccountName; 
                    if (string.IsNullOrEmpty(permCode)) return;

                    var appsForPerm = _allGroups
                        .SelectMany(g => g.AppPermissions ?? Enumerable.Empty<AppPermission>()) 
                        .Where(ap => ap.PermissionsData?.Any(p => p.Key == permCode && p.Value.ToString() == "1") == true)
                        .GroupBy(ap => ap.AppName)
                        .Select(g => g.First())
                        .ToList();
                        
                    appsForPerm.ForEach(ap => GroupApps.Add(ap));
                    break;
            }

            SelectedApp = GroupApps.FirstOrDefault();
        }

        private void UpdateAppPermissions(AppPermission? selectedItem)
        {
            AppPermissions.Clear();
            AppGroups.Clear();

            if (selectedItem == null || _allGroups == null) return;
            
            // Populate AppGroups - groups that have this application
            var groupsWithApp = _allGroups
                .Where(g => g.AppPermissions?.Any(ap => 
                    string.Equals(ap.AppName, selectedItem.AppName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ap.DisplayName, selectedItem.DisplayName, StringComparison.OrdinalIgnoreCase)) == true)
                .ToList();
            
            groupsWithApp.ForEach(g => AppGroups.Add(g));

            switch (CurrentViewMode)
            {
                case ViewMode.UserGroupAppPerm:
                case ViewMode.UserAppGroupPerm:
                    
                    if (selectedItem.PermissionsData == null) return;
                    
                    var permissions = selectedItem.PermissionsData
                        .Where(p => p.Key.StartsWith("perm", StringComparison.OrdinalIgnoreCase))
                        .Where(p => (int.TryParse(p.Value.ToString() ?? "", out int value) && value == 1)) 
                        
                        .Select(p => 
                        {
                            string permissionCode = p.Key;
                            
                            return new Permission
                            {
                                PermissionName = permissionCode, // Use the code directly
                                PermissionCode = permissionCode,
                            };
                        }).ToList();
                    
                    permissions.ForEach(p => AppPermissions.Add(p));
                    break;
                    
                case ViewMode.UserPermAppGroup:
                    var groups = _allGroups
                        .Where(g => g.AppPermissions?.Any(ap => ap.AppName == selectedItem.AppName) == true)
                        .ToList();
                        
                    groups.ForEach(g => AppPermissions.Add(new Permission { PermissionName = g.GroupName })); 
                    break;
            }
        }
        
        public string UserCountText
        {
            get
            {
                if (_selectedUser == null) return string.Empty;
                return $"Teller No: {_selectedUser.SamAccountName}";
            }
        }

        private string _mainHeaderText = "User Permission Viewer";
        public string MainHeaderText
        {
            get => _mainHeaderText;
            set => SetProperty(ref _mainHeaderText, value);
        }

        public string AppVersionText
        {
            get
            {
                try
                {
                    var ver = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                    return $"Version {ver?.Major}.{ver?.Minor}.{ver?.Build}";
                }
                catch { return "Version"; }
            }
        }

        public void SetHeaderForTab(int tabIndex)
        {
            MainHeaderText = tabIndex switch
            {
                0 => "User Permission Viewer",
                1 => "Group Permission Viewer",
                2 => "Application Permission Viewer",
                3 => "Permission Viewer",
                4 => "Settings",
                _ => "AppEnhancer Permission Viewer"
            };
        }
        
        public string DataUpdatedText
        {
            get
            {
                if (_data?.LastUpdated == null) return "Data Status: Unknown";
                
                TimeSpan elapsed = DateTimeOffset.Now - _data.LastUpdated.Value;
                string status;

                if (elapsed.TotalHours < 1)
                {
                    status = $"Data Updated: {elapsed.Minutes} minutes ago";
                }
                else if (elapsed.TotalDays < 1)
                {
                    status = $"Data Updated: {elapsed.Hours} hours ago";
                }
                else
                {
                    status = $"Data Updated: {_data.LastUpdated.Value:MM/dd/yyyy}";
                }
                return status;
            }
        }

        private void LoadAllUsers()
        {
            if (_data?.Groups == null) return;
            
            var allUsers = _data.Groups
                .SelectMany(g => g.Users ?? Enumerable.Empty<User>())
                .Where(u => u != null) // Add this line to filter out null users
                .GroupBy(u => u.SamAccountName)
                .Select(g => g.First())
                .OrderBy(u => u.Name)
                .ToList();
                
            UsersView.Source = allUsers;
            OnPropertyChanged(nameof(AllUsersList));
            SelectedUser = null;
        }

        private void LoadAllPermissions()
        {
            AllPermissions.Clear();
            
            if (_allGroups == null) return;

            // Extract all unique permissions from all groups' apps
            var allPerms = _allGroups
                .SelectMany(g => g.AppPermissions ?? Enumerable.Empty<AppPermission>())
                .SelectMany(ap => ap.PermissionsData?.Where(p => p.Key.StartsWith("perm", StringComparison.OrdinalIgnoreCase) && 
                    (int.TryParse(p.Value.ToString() ?? "", out int val) && val == 1)) ?? Enumerable.Empty<KeyValuePair<string, object>>())
                .GroupBy(p => p.Key)
                .Select(g => 
                {
                    string permCode = g.Key;
                    return new Permission { PermissionName = permCode, PermissionCode = permCode };
                })
                .OrderBy(p => p.PermissionName)
                .ToList();

            allPerms.ForEach(p => AllPermissions.Add(p));
        }

        public void FilterUsers(string filterText)
        {
            if (UsersView.View == null) return;
            if (string.IsNullOrWhiteSpace(filterText) || string.Equals(filterText, "filter...", StringComparison.OrdinalIgnoreCase))
            {
                UsersView.View.Filter = null;
            }
            else
            {
                UsersView.View.Filter = item =>
                {
                    if (item is not User user) return false;
                    if (!string.IsNullOrEmpty(user.SamAccountName) && user.SamAccountName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (!string.IsNullOrEmpty(user.Name) && user.Name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (!string.IsNullOrEmpty(user.Department) && user.Department.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (!string.IsNullOrEmpty(user.Title) && user.Title.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (!string.IsNullOrEmpty(user.Email) && user.Email.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    return false;
                };
            }
            UsersView.View.Refresh();
            OnPropertyChanged(nameof(UserCountText)); 
        }

        // --- Group Users (for Group View tab) ---
        public System.ComponentModel.ICollectionView? GroupUsersList => GroupUsersView.View;

        private string _groupUserSearchText = string.Empty;
        public string GroupUserSearchText
        {
            get => _groupUserSearchText;
            set
            {
                if (SetProperty(ref _groupUserSearchText, value))
                {
                    FilterGroupUsers(value);
                }
            }
        }

        private void LoadGroupUsers(Group? group)
        {
            if (group == null)
            {
                GroupUsersView.Source = null;
                OnPropertyChanged(nameof(GroupUsersList));
                return;
            }

            var users = group.Users ?? new List<User>();
            GroupUsersView.Source = users;
            FilterGroupUsers(_groupUserSearchText);
            OnPropertyChanged(nameof(GroupUsersList));
        }

        private void FilterGroupUsers(string filterText)
        {
            if (GroupUsersView.View == null) return;

            if (string.IsNullOrWhiteSpace(filterText))
            {
                GroupUsersView.View.Filter = null;
            }
            else
            {
                GroupUsersView.View.Filter = item =>
                {
                    if (item is not User u) return false;
                    return (!string.IsNullOrEmpty(u.Name) && u.Name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(u.SamAccountName) && u.SamAccountName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(u.Department) && u.Department.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(u.Title) && u.Title.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);
                };
            }
            GroupUsersView.View.Refresh();
        }

        // --- App Users (for Application View / Group View selection) ---
        public System.ComponentModel.ICollectionView? AppUsersList => AppUsersView.View;

        private void LoadAppUsers(AppPermission? app)
        {
            if (app == null || _allGroups == null)
            {
                AppUsersView.Source = null;
                OnPropertyChanged(nameof(AppUsersList));
                return;
            }

            var users = _allGroups
                .Where(g => g.AppPermissions?.Any(ap => string.Equals(ap.AppName, app.AppName, StringComparison.OrdinalIgnoreCase) || string.Equals(ap.DisplayName, app.DisplayName, StringComparison.OrdinalIgnoreCase)) == true)
                .SelectMany(g => g.Users ?? Enumerable.Empty<User>())
                .GroupBy(u => u.SamAccountName)
                .Select(g => g.First())
                .OrderBy(u => u.Name)
                .ToList();

            AppUsersView.Source = users;
            FilterAppUsers(_appUserSearchText);
            OnPropertyChanged(nameof(AppUsersList));
        }

        private void FilterAppUsers(string filterText)
        {
            if (AppUsersView.View == null) return;

            if (string.IsNullOrWhiteSpace(filterText))
            {
                AppUsersView.View.Filter = null;
            }
            else
            {
                AppUsersView.View.Filter = item =>
                {
                    if (item is not User u) return false;
                    return (!string.IsNullOrEmpty(u.Name) && u.Name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(u.SamAccountName) && u.SamAccountName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(u.Department) && u.Department.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(u.Title) && u.Title.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);
                };
            }
            AppUsersView.View.Refresh();
        }

        // --- Permission View: Load and Filter ---
        public System.ComponentModel.ICollectionView? PermUsersList => PermUsersView.View;

        private Permission? _selectedPerm;
        public Permission? SelectedPerm
        {
            get => _selectedPerm;
            set
            {
                if (SetProperty(ref _selectedPerm, value))
                {
                    LoadPermissionAppsUsers(value);
                    OnPropertyChanged(nameof(IsAnythingSelected));
                    (ClearAllSelectionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(PermGroups));
                }
            }
        }

        private void LoadPermissionAppsUsers(Permission? perm)
        {
            PermApps.Clear();
            PermUsersView.Source = null;

            if (perm == null || _allGroups == null)
            {
                OnPropertyChanged(nameof(PermUsersList));
                return;
            }

            // Load all applications that have this permission
            var appsWithPerm = _allGroups
                .SelectMany(g => g.AppPermissions ?? Enumerable.Empty<AppPermission>())
                .Where(ap => ap.PermissionsData != null && 
                    ap.PermissionsData.Any(p => p.Key == perm.PermissionCode && 
                    (int.TryParse(p.Value.ToString() ?? "", out int val) && val == 1)))
                .GroupBy(ap => ap.AppName)
                .Select(g => g.First())
                .OrderBy(ap => ap.AppName)
                .ToList();

            appsWithPerm.ForEach(ap => PermApps.Add(ap));

            // Load all users that have this permission (through groups that have apps with this permission)
            var usersWithPerm = _allGroups
                .Where(g => g.AppPermissions?.Any(ap => ap.PermissionsData != null &&
                    ap.PermissionsData.Any(p => p.Key == perm.PermissionCode && 
                    (int.TryParse(p.Value.ToString() ?? "", out int val) && val == 1))) == true)
                .SelectMany(g => g.Users ?? Enumerable.Empty<User>())
                .GroupBy(u => u.SamAccountName)
                .Select(g => g.First())
                .OrderBy(u => u.Name)
                .ToList();

            PermUsersView.Source = usersWithPerm;
            FilterPermUsers(_permUserSearchText);
            OnPropertyChanged(nameof(PermUsersList));
        }

        private void FilterPermUsers(string filterText)
        {
            if (PermUsersView.View == null) return;

            if (string.IsNullOrWhiteSpace(filterText))
            {
                PermUsersView.View.Filter = null;
            }
            else
            {
                PermUsersView.View.Filter = item =>
                {
                    if (item is not User u) return false;
                    return (!string.IsNullOrEmpty(u.Name) && u.Name.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(u.SamAccountName) && u.SamAccountName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(u.Department) && u.Department.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(u.Title) && u.Title.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);
                };
            }
            PermUsersView.View.Refresh();
        }
    }
}