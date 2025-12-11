namespace JsonDataViewer.ViewModels
{
    public enum ViewMode
    {
        // Left Panel: Group | Middle Panel: App | Right Panel: Perm (Original)
        UserGroupAppPerm, 
        
        // Left Panel: App | Middle Panel: Group | Right Panel: Perm
        UserAppGroupPerm, 
        
        // Left Panel: Perm | Middle Panel: App | Right Panel: Group
        UserPermAppGroup
    }
}