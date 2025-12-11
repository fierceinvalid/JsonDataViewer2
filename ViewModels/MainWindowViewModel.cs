using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input; 
using JsonDataViewer.Models; 
// The ViewMode enum is now accessed via the namespace JsonDataViewer.ViewModels

namespace JsonDataViewer.ViewModels
{
    // RelayCommand is provided in ViewModels/RelayCommand.cs

    public class MainWindowViewModel : ObservableObject
    {
        private GroupData? _data;
        private List<Group>? _allGroups;
        
        private Dictionary<string, string> _permissionMapping = new Dictionary<string, string>();

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
                    FilterGroupUsers(value);
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
                    FilterAppUsers(value);
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
                    FilterPermUsers(value);
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
                    FilterUsers(value);
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

        // Adapter properties for Permission View XAML
        public IEnumerable<AppPermission> PermApplications => PermApps;
        public IEnumerable<Group> PermGroups => (_allGroups?.Where(g => g.AppPermissions?.Any(ap => ap.PermissionsData != null &&
                    ap.PermissionsData.Any(p => p.Key == _selectedPerm?.PermissionCode && 
                    (int.TryParse(p.Value.ToString() ?? "", out int val) && val == 1))) == true) ?? Enumerable.Empty<Group>()).ToList();

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


        public MainWindowViewModel(GroupData? data)
        {
            _data = data;
            _allGroups = _data?.Groups;
            LoadPermissionMapping();
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
                string permName = _permissionMapping.TryGetValue(permCode, out string? friendlyName) ? friendlyName : permCode;
                
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
                            string displayValue = _permissionMapping.TryGetValue(permissionCode, out string? friendlyName) ? friendlyName : permissionCode;
                            
                            return new Permission
                            {
                                PermissionName = displayValue,
                                PermissionCode = permissionCode
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

        public void SetHeaderForTab(int tabIndex)
        {
            MainHeaderText = tabIndex switch
            {
                0 => "User Permission Viewer",
                1 => "Group Permission Viewer",
                2 => "Application Permission Viewer",
                3 => "Permission Viewer",
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

        private void LoadPermissionMapping()
        {
            _permissionMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "permScanOnline", "Scan / Index Online" },
                { "permBatchScan", "Batch Scan" },
                { "permBatchIndex", "Batch Index" },
                { "permModifyIndex", "Modify Index" },
                { "permAddObject", "Add Object" },
                { "permAddPage", "Add Page" },
                { "permDeleteDoc", "Delete Doc" },
                { "permDeletePage", "Delete Page" },
                { "permDeleteObject", "Delete Object" },
                { "permCreateApp", "Create Application" },
                { "permModifyApp", "Modify Application" },
                { "permDeleteApp", "Delete Application" },
                { "permUserSecurityAdmin", "User Security Admin" },
                { "permDocLevelSecAdmin", "Doc Level Sec Admin" },
                { "permAutoIndexFileMaint", "Auto Index Maintenance" },
                { "permKeyRefFileMaint", "Key Reference Maintenance" },
                { "permCOLDImportMaint", "COLD Import Maintenance" },
                { "permCOLDImport", "COLD Import" },
                { "permView", "View" },
                { "permPrint", "Print" },
                { "permFaxIn", "Fax In" },
                { "permFaxOut", "Fax Out" },
                { "permReffileImport", "Key Reference Import" },
                { "permMigrateApp", "Migrate Application" },
                { "permImageUtilities", "Image Utilities" },
                { "permIndexImageImport", "Index/Image Import" },
                { "permAdmin", "Administrator" },
                { "permConfig", "Configure Workstation" },
                { "permEditRedactions", "Edit Redactions" },
                { "permEditAnnotations", "Edit Annotations" },
                { "permMultipleLogins", "Multiple Logins" },
                { "permFullTextIndex", "Full Text Index" },
                { "permFullTextQuery", "Full Text Query" },
                { "permOCR", "OCR" },
                { "permScanFix", "Scan Fix" },
                { "permBatchExtract", "COLD Batch Extract" },
                { "permGlobalAnnot", "Global Annotations" },
                { "permCXReports", "CX Reports" },
                { "permCreateAnnotations", "Create Annotations" },
                { "permCreateRedactions", "Create Redactions" },
                { "permRetentionUser", "Retention User" },
                { "permRetentionAdmin", "Retention Administrator" },
            };
        }

        private void LoadAllUsers()
        {
            if (_data?.Groups == null) return;
            
            var allUsers = _data.Groups
                .SelectMany(g => g.Users ?? Enumerable.Empty<User>())
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
                    string displayName = _permissionMapping.TryGetValue(permCode, out string? friendly) ? friendly : permCode;
                    return new Permission { PermissionName = displayName, PermissionCode = permCode };
                })
                .OrderBy(p => p.PermissionName)
                .ToList();

            allPerms.ForEach(p => AllPermissions.Add(p));
        }

        public void FilterUsers(string filterText)
        {
            if (UsersView.View == null) return;

            if (string.IsNullOrWhiteSpace(filterText) || filterText.ToLower() == "filter...")
            {
                UsersView.View.Filter = null;
            }
            else
            {
                string lowerFilter = filterText.ToLower();
                UsersView.View.Filter = item =>
                {
                    if (!(item is User user)) return false; 
                    
                    if (user.SamAccountName?.ToLower().Contains(lowerFilter) == true) return true;
                    if (user.Name?.ToLower().Contains(lowerFilter) == true) return true;
                    if (user.Department?.ToLower().Contains(lowerFilter) == true) return true; 
                    if (user.Title?.ToLower().Contains(lowerFilter) == true) return true;     
                    if (user.Email?.ToLower().Contains(lowerFilter) == true) return true; 

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
                string lower = filterText.ToLower();
                GroupUsersView.View.Filter = item =>
                {
                    if (item is not User u) return false;
                    return (u.Name?.ToLower().Contains(lower) == true)
                        || (u.SamAccountName?.ToLower().Contains(lower) == true)
                        || (u.Department?.ToLower().Contains(lower) == true)
                        || (u.Title?.ToLower().Contains(lower) == true);
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
                string lower = filterText.ToLower();
                AppUsersView.View.Filter = item =>
                {
                    if (item is not User u) return false;
                    return (u.Name?.ToLower().Contains(lower) == true)
                        || (u.SamAccountName?.ToLower().Contains(lower) == true)
                        || (u.Department?.ToLower().Contains(lower) == true)
                        || (u.Title?.ToLower().Contains(lower) == true);
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
                string lower = filterText.ToLower();
                PermUsersView.View.Filter = item =>
                {
                    if (item is not User u) return false;
                    return (u.Name?.ToLower().Contains(lower) == true)
                        || (u.SamAccountName?.ToLower().Contains(lower) == true)
                        || (u.Department?.ToLower().Contains(lower) == true)
                        || (u.Title?.ToLower().Contains(lower) == true);
                };
            }
            PermUsersView.View.Refresh();
        }
    }
}