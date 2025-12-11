using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace JsonDataViewer.Models
{
    // --- Root Data Model ---
    public class GroupData
    {
        [JsonProperty("DataUpdated")]
        public DateTimeOffset? LastUpdated { get; set; }

        [JsonProperty("groups")]
        public List<Group> Groups { get; set; } = new List<Group>();
    }

    // --- Group Model ---
    public class Group
    {
        [JsonProperty("name")]
        public string GroupName { get; set; } = string.Empty;
        
        [JsonProperty("domainName")]
        public string Domain { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty; 
        
        [JsonProperty("secureId")]
        public string SID { get; set; } = string.Empty;

        public string? SamAccountName { get; set; }
        
        [JsonProperty("users")]
        public List<User> Users { get; set; } = new List<User>();

        [JsonProperty("appPermissions")]
        public List<AppPermission> AppPermissions { get; set; } = new List<AppPermission>();

        // XAML-friendly adapter properties (some XAML binds to these names)
        public string Name => GroupName;
        public string DomainName => Domain;
        public string SecureId => SID;
    }

    // --- AppPermission Model ---
    public class AppPermission
    {
        [JsonProperty("appName")]
        public string AppName { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, object> PermissionsData { get; set; } = new Dictionary<string, object>();

        // Adapter for bindings that expect an AppId property
        public string AppId
        {
            get
            {
                if (PermissionsData == null) return string.Empty;
                // common key in json is "appId" (case-sensitive in source) - handle case-insensitively
                var key = PermissionsData.Keys.FirstOrDefault(k => string.Equals(k, "appId", StringComparison.OrdinalIgnoreCase));
                if (key == null) return string.Empty;
                return PermissionsData[key]?.ToString() ?? string.Empty;
            }
        }

        // DisplayName used when AppName is null or empty
        public string DisplayName => string.IsNullOrWhiteSpace(AppName) ? (string.IsNullOrWhiteSpace(AppId) ? "(Unknown)" : AppId) : AppName;
    }

    // --- User Model ---
    public class User
    {
        public string SamAccountName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty; 
        public string Department { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public object Manager { get; set; } = new object();
    }

    // --- Helper Model for Right Panel (Permission List) ---
    public class Permission
    {
        public string PermissionName { get; set; } = string.Empty;
        public string PermissionCode { get; set; } = string.Empty;
        // IsGranted property removed
    }
}