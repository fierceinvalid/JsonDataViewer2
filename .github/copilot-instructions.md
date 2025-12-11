<!-- Copilot / AI Agent instructions for JsonDataViewer -->
# JsonDataViewer — Copilot Instructions

Purpose: concise guidance for AI code agents working on this WPF C# app.

- Big picture: This is a small WPF desktop app (MVVM-style) that reads a local JSON dataset and displays Groups → Apps → Permissions → Users in a tabbed UI. Key files:
  - [MainWindow.xaml](MainWindow.xaml#L1-L120) — primary UI layout, DataGrids and bindings.
  - [MainWindow.xaml.cs](MainWindow.xaml.cs#L1-L200) — startup: reads `group_data.json` and sets DataContext.
  - [ViewModels/MainWindowViewModel.cs](ViewModels/MainWindowViewModel.cs#L1-L120) — primary viewmodel: collection sources, selection logic, permission mapping, and small RelayCommand.
  - [Models/DataModels.cs](Models/DataModels.cs#L1-L200) — JSON data models; note `AppPermission` uses `JsonExtensionData` for dynamic `perm*` keys.
  - [Converters/EnumToItemsSourceConverter.cs](Converters/EnumToItemsSourceConverter.cs#L1-L120) — helper expecting the Enum Type passed as XAML `ConverterParameter`.
  - `group_data.json` — canonical sample data; the app reads this file at runtime (ensure it is present in the output folder).

- Data flow and intent:
  - JSON -> `MainWindow` (reads with Newtonsoft.Json) -> `GroupData` model -> `MainWindowViewModel` (constructor receives `GroupData`).
  - The ViewModel exposes Collection/Observable collections and `CollectionViewSource` used by the XAML DataGrids.
  - Permissions are stored as dynamic property keys (e.g. `permView`, `permBatchIndex`) inside `AppPermission.PermissionsData` (via `JsonExtensionData`). The ViewModel maps these codes to friendly names in `LoadPermissionMapping()`.

- Important conventions and pitfalls (use these when modifying code):
  - Bindings vs model property names: XAML commonly binds to properties like `Name`, `DomainName`, `SecureId` while the model uses `GroupName`, `Domain`, `SID`. When changing data models or bindings, update both the model and all XAML bindings to keep UI functional.
  - Dynamic permission keys: treat any property that starts with `perm` as a toggle (value `1` means granted). The ViewModel uses `int.TryParse` or checks `ToString()` to handle inconsistent types in JSON.
  - Enum converter usage: `EnumToItemsSourceConverter` expects the enum Type in `ConverterParameter`. See `ViewMode` usage in `MainWindowViewModel` for examples.
  - Commands: a simple `RelayCommand` is declared inside `MainWindowViewModel.cs`. For added commands prefer implementing consistent `ICommand` patterns used here.

- Developer workflows:
  - Build & run locally: from the project folder run `dotnet build` and `dotnet run` (or open the solution in Visual Studio and F5).
  - Ensure `group_data.json` is copied to the output directory (Debug/Release). If UI shows "file not found", verify file properties in the .csproj or copy it manually.
  - No test project detected — unit tests are not present; validate visually in the running UI.

- External dependencies & integration points:
  - Uses Newtonsoft.Json (Json.NET) for serialization/deserialization.
  - No network services — the data source is the local `group_data.json` and UI assets in `assets/`.

- When making changes an AI agent should:
  1. Prefer small, focused edits. Update the ViewModel first for UI behavior, then adjust XAML bindings and models if necessary.
  2. Run the app (`dotnet run`) to verify UI and data load; check the Output/Debug folder for `group_data.json` presence.
  3. If touching models, ensure `JsonProperty` mappings and XAML binding names remain consistent.
  4. When adding new permission names, add entries to `LoadPermissionMapping()` (ViewModel) so the UI shows friendly labels.

If anything here is unclear or you want more examples (e.g., sample code edits to reconcile binding/model name differences), tell me which area to expand.
