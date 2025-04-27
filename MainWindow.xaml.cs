using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls; // Added for Button
using VirtualEnvManager.Models;
using VirtualEnvManager.Services;

namespace VirtualEnvManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private VirtualEnvService envService;
    private readonly SettingsService settingsService = new();
    private VirtualEnv _selectedEnvForManagement; // Store the env being managed
    private bool _isWorkonHomeValid = false; // Track if WORKON_HOME is set and usable

    public MainWindow()
    {
        InitializeComponent();
        InitializeApp();
    }

    private void InitializeApp()
    {
        string workonHomePath = settingsService.GetWorkonHome();

        if (string.IsNullOrEmpty(workonHomePath))
        {
            // WORKON_HOME not set - guide user to Settings
            _isWorkonHomeValid = false;
            StatusTextBlock.Text = "Error: WORKON_HOME environment variable not set. Please set it in Settings.";
            ShowSettingsView(); // Directly show settings view
            // Disable explorer controls if needed, though showing settings might be enough
            SetExplorerControlsEnabled(false); 
        }
        else
        {
            // WORKON_HOME is set, try to initialize service
            try
            {
                envService = new VirtualEnvService(workonHomePath);
                _isWorkonHomeValid = true;
                StatusTextBlock.Text = "Ready";
                SetExplorerControlsEnabled(true);
                RefreshEnvList(); // Load environments
                ShowExplorerView(); // Start in explorer view
            }
            catch (Exception ex) 
            {
                // Error initializing with the found path (e.g., path invalid, permissions)
                _isWorkonHomeValid = false;
                StatusTextBlock.Text = $"Error initializing with WORKON_HOME '{workonHomePath}': {ex.Message}. Please check the path in Settings.";
                ShowSettingsView(); // Show settings so user can fix it
                WorkonHomeTextBox.Text = workonHomePath; // Show the problematic path
                SetExplorerControlsEnabled(false); 
            }
        }
    }
    
    // Helper to enable/disable explorer controls based on WORKON_HOME validity
    private void SetExplorerControlsEnabled(bool isEnabled)
    {
        // Assuming Buttons have x:Name properties matching their Click handlers (e.g., RefreshEnvListButton)
        // If not, you'll need to add x:Name to the buttons in XAML
        RefreshEnvListButton.IsEnabled = isEnabled;
        CreateEnvButton.IsEnabled = isEnabled;
        DeleteEnvButton.IsEnabled = isEnabled;
        CloneEnvButton.IsEnabled = isEnabled;
        EnvDataGrid.IsEnabled = isEnabled;
        // Settings button should always be enabled
        SettingsButton.IsEnabled = true;
    }

    // --- UI Navigation --- 

    private void ShowExplorerView()
    {
        ExplorerViewGrid.Visibility = Visibility.Visible;
        ManagementViewGrid.Visibility = Visibility.Collapsed;
        SettingsViewGrid.Visibility = Visibility.Collapsed;
        // StatusTextBlock.Text = "Ready"; // Status updated by InitializeApp or actions
        _selectedEnvForManagement = null; 
        // Only clear selection if the grid is enabled (i.e., WORKON_HOME is valid)
        if (_isWorkonHomeValid && EnvDataGrid != null)
        { 
           EnvDataGrid.SelectedItem = null;
        }
    }

    private void ShowManagementView(VirtualEnv envToManage)
    {
        if (!_isWorkonHomeValid) return; // Don't show if WORKON_HOME is bad
        _selectedEnvForManagement = envToManage;
        if (_selectedEnvForManagement == null) return;

        ManagementEnvNameTextBlock.Text = $"Managing Environment: {_selectedEnvForManagement.Name}";
        ExplorerViewGrid.Visibility = Visibility.Collapsed;
        ManagementViewGrid.Visibility = Visibility.Visible;
        SettingsViewGrid.Visibility = Visibility.Collapsed;
        
        // Clear previous data
        PackageDataGrid.ItemsSource = null;
        VarsDataGrid.ItemsSource = null;
        PackageNameTextBox.Clear();
        VarKeyTextBox.Clear();
        VarValueTextBox.Clear();

        // Load initial data for the management view
        LoadPackagesForSelectedEnv();
        LoadMetadataForSelectedEnv();
        StatusTextBlock.Text = $"Managing '{_selectedEnvForManagement.Name}'";
    }
    
    private void ShowSettingsView()
    {
        // Ensure the TextBox shows the *current* value from the environment variable, 
        // not necessarily the one envService might be using if initialization failed.
        WorkonHomeTextBox.Text = settingsService.GetWorkonHome() ?? ""; 
        ExplorerViewGrid.Visibility = Visibility.Collapsed;
        ManagementViewGrid.Visibility = Visibility.Collapsed;
        SettingsViewGrid.Visibility = Visibility.Visible;
        StatusTextBlock.Text = _isWorkonHomeValid ? "Application Settings" : "Error: WORKON_HOME path needs configuration.";
    }

    private void BackToExplorer_Click(object sender, RoutedEventArgs e)
    {
        ShowExplorerView();
    }
    
    private void ShowSettings_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsView();
    }

    // --- Data Loading/Refreshing --- 

    private void RefreshEnvList()
    {
        if (!_isWorkonHomeValid || envService == null)
        {
            StatusTextBlock.Text = "Error: WORKON_HOME path not configured correctly.";
            EnvDataGrid.ItemsSource = null;
            return;
        }
        try
        {
            EnvDataGrid.ItemsSource = envService.ListEnvs();
            StatusTextBlock.Text = "Environments loaded from: " + envService.WorkonHome;
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Error listing environments: " + ex.Message;
            EnvDataGrid.ItemsSource = null;
        }
    }
    
    private void LoadPackagesForSelectedEnv()
    {
        if (_selectedEnvForManagement == null) { /* Optional: Log error or ignore */ return; }
        if (envService == null) { StatusTextBlock.Text = "Error: Service not initialized."; return; }

        try
        {
            PackageDataGrid.ItemsSource = envService.ListPackages(_selectedEnvForManagement.Name);
            // StatusTextBlock.Text = $"Packages loaded for '{_selectedEnvForManagement.Name}'."; // Avoid overwriting main status too often
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Error loading packages for '{_selectedEnvForManagement.Name}': {ex.Message}"; }
    }
    
    private void LoadMetadataForSelectedEnv()
    {
        if (_selectedEnvForManagement == null) { /* Optional: Log error or ignore */ return; }
        if (envService == null) { StatusTextBlock.Text = "Error: Service not initialized."; return; }

        try
        {
            var dict = envService.GetEnvVars(_selectedEnvForManagement.Name)
                                 .Select(kv => new { Key = kv.Key, Value = kv.Value })
                                 .ToList();
            VarsDataGrid.ItemsSource = dict;
            // StatusTextBlock.Text = $"Metadata loaded for '{_selectedEnvForManagement.Name}'."; // Avoid overwriting main status too often
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Error loading metadata for '{_selectedEnvForManagement.Name}': {ex.Message}"; }
    }

    // Helper to get selected environment from the MAIN grid (for Delete/Clone)
    private VirtualEnv GetSelectedEnvFromExplorerGrid()
    {
        return EnvDataGrid.SelectedItem as VirtualEnv;
    }

    // --- Event Handlers --- 

    private void ManageEnv_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VirtualEnv env)
        {
            ShowManagementView(env);
        }
    }
    
    private void EnvDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Used primarily to enable/disable Delete/Clone buttons based on selection in Explorer view
        // bool isEnvSelected = EnvDataGrid.SelectedItem != null;
        // DeleteButton.IsEnabled = isEnvSelected;
        // CloneButton.IsEnabled = isEnvSelected;
        // LsSitePackagesButton.IsEnabled = isEnvSelected; // Assuming LsSitePackages needs a selected env
    }

    // Explorer View Handlers (Delete/Clone now act on EnvDataGrid.SelectedItem) 
    private void RefreshEnvList_Click(object s, RoutedEventArgs e) 
    { 
        if (!_isWorkonHomeValid) return;
        RefreshEnvList(); 
    }
    private void CreateEnv_Click(object s, RoutedEventArgs e)
    {
        if (!_isWorkonHomeValid) return;
        var dlg = new InputDialog("Environment Name:", "Create Environment");
        if (dlg.ShowDialog() != true) return;

        var interpreterDlg = new InputDialog("Python Interpreter Path (optional):", "Interpreter");
        if (interpreterDlg.ShowDialog() != true) 
        {
            try 
            {
               envService.CreateEnv(dlg.InputText, null);
               RefreshEnvList();
               StatusTextBlock.Text = $"Created '{dlg.InputText}'.";
            }
            catch (Exception ex) { StatusTextBlock.Text = "Error: " + ex.Message; }
            return; 
        }

        try
        {
            var interp = string.IsNullOrWhiteSpace(interpreterDlg.InputText) 
                         ? null 
                         : interpreterDlg.InputText;
            envService.CreateEnv(dlg.InputText, interp);
            RefreshEnvList();
            StatusTextBlock.Text = $"Created '{dlg.InputText}'.";
        }
        catch (Exception ex) { StatusTextBlock.Text = "Error: " + ex.Message; }
    }
    private void DeleteSelectedEnv_Click(object s, RoutedEventArgs e)
    {
        if (!_isWorkonHomeValid) return;
        var envToDelete = GetSelectedEnvFromExplorerGrid();
        if (envToDelete == null) { StatusTextBlock.Text = "Select an environment from the list to delete."; return; }
        // ... (Rest of Delete logic uses envToDelete.Name, then RefreshEnvList) ...
        if (MessageBox.Show($"Delete '{envToDelete.Name}'?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        try
        {
            envService.DeleteEnv(envToDelete.Name);
            RefreshEnvList();
            StatusTextBlock.Text = $"Deleted '{envToDelete.Name}'.";
        }
        catch (Exception ex) { StatusTextBlock.Text = "Error: " + ex.Message; }
    }
    private void CloneSelectedEnv_Click(object s, RoutedEventArgs e)
    {
        if (!_isWorkonHomeValid) return;
        var sourceEnv = GetSelectedEnvFromExplorerGrid();
        if (sourceEnv == null) { StatusTextBlock.Text = "Select an environment from the list to clone."; return; }
        // ... (Rest of Clone logic uses sourceEnv.Name, then RefreshEnvList) ...
        var dlg = new InputDialog("New Environment Name:", "Clone Environment");
        if (dlg.ShowDialog() != true) return;
        try
        {
            envService.CloneEnv(sourceEnv.Name, dlg.InputText);
            RefreshEnvList();
            StatusTextBlock.Text = $"Cloned '{sourceEnv.Name}' to '{dlg.InputText}'.";
        }
        catch (Exception ex) { StatusTextBlock.Text = "Error: " + ex.Message; }
    }

    // Management View Handlers (now use _selectedEnvForManagement)
    private void RefreshPackages_Click(object s, RoutedEventArgs e) 
    { 
        if (!_isWorkonHomeValid || _selectedEnvForManagement == null) return;
        LoadPackagesForSelectedEnv(); 
    }
    private void RefreshMetadata_Click(object s, RoutedEventArgs e) 
    { 
        if (!_isWorkonHomeValid || _selectedEnvForManagement == null) return;
        LoadMetadataForSelectedEnv(); 
    }

    private void InstallPackage_Click(object s, RoutedEventArgs e)
    {
        if (_selectedEnvForManagement == null) return;
        var pkg = PackageNameTextBox.Text;
        if (string.IsNullOrWhiteSpace(pkg)) { StatusTextBlock.Text = "Enter package name to install."; return; }
        // ... (Use _selectedEnvForManagement.Name, call LoadPackagesForSelectedEnv on success) ...
        try
        {
            envService.InstallPackage(_selectedEnvForManagement.Name, pkg);
            LoadPackagesForSelectedEnv(); 
            StatusTextBlock.Text = $"Installed '{pkg}' in '{_selectedEnvForManagement.Name}'.";
            PackageNameTextBox.Clear(); // Clear input after action
        }
        catch (Exception ex) { StatusTextBlock.Text = "Error installing package: " + ex.Message; }
    }
    private void RemovePackage_Click(object s, RoutedEventArgs e)
    {
        if (_selectedEnvForManagement == null) return;
        var pkg = PackageNameTextBox.Text;
        if (string.IsNullOrWhiteSpace(pkg)) { StatusTextBlock.Text = "Enter package name to remove."; return; }
        // Check selected item in PackageDataGrid if textbox is empty?
        // ... (Use _selectedEnvForManagement.Name, call LoadPackagesForSelectedEnv on success) ...
         try
        {
            envService.RemovePackage(_selectedEnvForManagement.Name, pkg);
            LoadPackagesForSelectedEnv(); 
            StatusTextBlock.Text = $"Removed '{pkg}' from '{_selectedEnvForManagement.Name}'.";
             PackageNameTextBox.Clear(); // Clear input after action
        }
        catch (Exception ex) { StatusTextBlock.Text = "Error removing package: " + ex.Message; }
    }
    private void UpgradePackage_Click(object s, RoutedEventArgs e)
    {
        if (_selectedEnvForManagement == null) return;
        var pkg = PackageNameTextBox.Text;
        if (string.IsNullOrWhiteSpace(pkg)) { StatusTextBlock.Text = "Enter package name to upgrade."; return; }
        // ... (Use _selectedEnvForManagement.Name, call LoadPackagesForSelectedEnv on success) ...
         try
        {
            envService.UpgradePackage(_selectedEnvForManagement.Name, pkg);
            LoadPackagesForSelectedEnv(); 
            StatusTextBlock.Text = $"Upgraded '{pkg}' in '{_selectedEnvForManagement.Name}'.";
             PackageNameTextBox.Clear(); // Clear input after action
        }
        catch (Exception ex) { StatusTextBlock.Text = "Error upgrading package: " + ex.Message; }
    }

    private void SetVar_Click(object s, RoutedEventArgs e)
    {
        if (_selectedEnvForManagement == null) return;
        var key = VarKeyTextBox.Text;
        var val = VarValueTextBox.Text;
        if (string.IsNullOrWhiteSpace(key)) { StatusTextBlock.Text = "Enter variable key to set."; return; }
        // ... (Use _selectedEnvForManagement.Name, call LoadMetadataForSelectedEnv on success) ...
        try
        {
            envService.SetEnvVar(_selectedEnvForManagement.Name, key, val);
            LoadMetadataForSelectedEnv(); 
            StatusTextBlock.Text = $"Set variable '{key}' in '{_selectedEnvForManagement.Name}'.";
             VarKeyTextBox.Clear(); // Clear inputs after action
             VarValueTextBox.Clear();
        }
        catch (Exception ex) { StatusTextBlock.Text = "Error setting variable: " + ex.Message; }
    }
    private void RemoveVar_Click(object s, RoutedEventArgs e)
    {
        if (_selectedEnvForManagement == null) return;
        var key = VarKeyTextBox.Text;
        if (string.IsNullOrWhiteSpace(key)) { StatusTextBlock.Text = "Enter variable key to remove."; return; }
        // ... (Use _selectedEnvForManagement.Name, call LoadMetadataForSelectedEnv on success) ...
         try
        {
            envService.RemoveEnvVar(_selectedEnvForManagement.Name, key);
            LoadMetadataForSelectedEnv(); 
            StatusTextBlock.Text = $"Removed variable '{key}' from '{_selectedEnvForManagement.Name}'.";
             VarKeyTextBox.Clear(); // Clear input after action
             VarValueTextBox.Clear();
        }
        catch (Exception ex) { StatusTextBlock.Text = "Error removing variable: " + ex.Message; }
    }

    // Settings View Handlers
    private void SaveSettings_Click(object s, RoutedEventArgs e)
    {
        string newPath = WorkonHomeTextBox.Text;
        if (string.IsNullOrWhiteSpace(newPath))
        {
             StatusTextBlock.Text = "Error: WORKON_HOME path cannot be empty.";
             return;
        }
        try
        {
            // Test if the new path is usable *before* saving and switching
            var testService = new VirtualEnvService(newPath);
            // Test listing envs to ensure directory is accessible
            testService.ListEnvs(); // This might throw if inaccessible
            
            // If tests passed, save the setting and update the main service instance
            settingsService.SetWorkonHome(newPath);
            envService = testService; // Use the successfully tested service instance
            _isWorkonHomeValid = true;
            
            StatusTextBlock.Text = "WORKON_HOME updated successfully.";
            SetExplorerControlsEnabled(true);
            RefreshEnvList(); // Load envs with the new path
            ShowExplorerView(); // Go back to explorer after saving settings
        }
        catch (Exception ex) 
        {
            _isWorkonHomeValid = false; // Path is not valid
            StatusTextBlock.Text = $"Error applying new WORKON_HOME: {ex.Message}. Please verify the path.";
             SetExplorerControlsEnabled(false); // Disable explorer controls
             // Keep the user on the settings page to fix the path
        }
    }
    private void MkProject_Click(object s, RoutedEventArgs e)
    {
        if (!_isWorkonHomeValid) { StatusTextBlock.Text = "Error: WORKON_HOME path not configured correctly."; return; }
        // ... (Rest of MkProject logic remains the same, refreshes EnvDataGrid in Explorer view) ...
    }
    private void LsSitePackages_Click(object s, RoutedEventArgs e)
    {
        if (!_isWorkonHomeValid) { StatusTextBlock.Text = "Error: WORKON_HOME path not configured correctly."; return; }
        // This command needs a selected environment. Let's get it from the main grid.
        var selectedEnv = GetSelectedEnvFromExplorerGrid();
        if (selectedEnv == null) { StatusTextBlock.Text = "Select an environment in the Explorer view first."; return; }
        if (envService == null) { StatusTextBlock.Text = "Error: Service not initialized."; return; }
        try
        {
            envService.ExecuteWrapperCommand($"workon {selectedEnv.Name} && lssitepackages");
            StatusTextBlock.Text = $"lssitepackages ran for '{selectedEnv.Name}'. Check console output.";
        }
        catch (Exception ex) { StatusTextBlock.Text = "Error: " + ex.Message; }
    }
}