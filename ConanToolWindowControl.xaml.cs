﻿using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System.Collections;
using System.IO;
using System.Reflection;
using EnvDTE;


namespace conan_vs_extension
{
    public class Component
    {
        public string cmake_target_name { get; set; }
    }

    public class Library
    {
        public string cmake_file_name { get; set; }
        public string cmake_target_name { get; set; }
        public string description { get; set; }
        public List<string> license { get; set; }
        public bool v2 { get; set; }
        public List<string> versions { get; set; }
        public Dictionary<string, Component> components { get; set; } = new Dictionary<string, Component>();
    }

    public class RootObject
    {
        public long date { get; set; }
        public Dictionary<string, Library> libraries { get; set; }
    }

    /// <summary>
    /// Interaction logic for ConanToolWindowControl.
    /// </summary>
    public partial class ConanToolWindowControl : UserControl
    {
        private ProjectConfigurationManager _manager;
        private DTE _dte;
        private RootObject _jsonData;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConanToolWindowControl"/> class.
        /// </summary>
        public ConanToolWindowControl()
        {
            this.InitializeComponent();
            LibraryHeader.Visibility = Visibility.Collapsed;
            myWebBrowser.Visibility = Visibility.Collapsed;
            _manager = new ProjectConfigurationManager();
            _ = InitializeAsync();
        }


        private async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _dte = (DTE)ServiceProvider.GlobalProvider.GetService(typeof(DTE));
            if (_dte == null)
            {
                throw new InvalidOperationException("Cannot access DTE service.");
            }

            await CopyJsonFileFromResourceIfNeededAsync();
            await LoadLibrariesFromJsonAsync();
        }
        private async Task CopyJsonFileFromResourceIfNeededAsync()
        {
            string userConanFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".conan-vs-extension");
            string jsonFilePath = Path.Combine(userConanFolder, "targets-data.json");

            if (!File.Exists(jsonFilePath))
            {
                if (!Directory.Exists(userConanFolder))
                {
                    Directory.CreateDirectory(userConanFolder);
                }

                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "conan_vs_extension.Resources.targets-data.json";
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                {
                    string jsonContent = await reader.ReadToEndAsync();
                    using (var writer = new StreamWriter(jsonFilePath))
                    {
                        await writer.WriteAsync(jsonContent);
                    }
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterListView(SearchTextBox.Text);
        }

        private async Task LoadLibrariesFromJsonAsync()
        {
            string userConanFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".conan-vs-extension");
            string jsonFilePath = Path.Combine(userConanFolder, "targets-data.json");

            string json = await Task.Run(() => File.ReadAllText(jsonFilePath));
            _jsonData = JsonConvert.DeserializeObject<RootObject>(json);

            Dispatcher.Invoke(() =>
            {
                PackagesListView.Items.Clear();
                foreach (var library in _jsonData.libraries.Keys)
                {
                    PackagesListView.Items.Add(library);
                }
            });
        }

        private void FilterListView(string searchText)
        {
            if (_jsonData == null || _jsonData.libraries == null) return;

            PackagesListView.Items.Clear();

            var filteredLibraries = _jsonData.libraries
                .Where(kv => kv.Key.Contains(searchText))
                .ToList();

            foreach (var library in filteredLibraries)
            {
                PackagesListView.Items.Add(library.Key);
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PackagesListView.SelectedItem is string selectedItem)
            {
                var htmlContent = GenerateHtml(selectedItem);
                myWebBrowser.NavigateToString(htmlContent);
            }
        }

        public void UpdatePanel(string name, string description, string licenses, List<string> versions)
        {
            LibraryNameLabel.Content = name;
            VersionsComboBox.ItemsSource = versions;
            VersionsComboBox.SelectedIndex = 0;

            DescriptionTextBlock.Text = description ?? "No description available.";
            LicenseText.Text = licenses ?? "No description available.";

            InstallButton.Visibility = Visibility.Visible;
            RemoveButton.Visibility = Visibility.Collapsed;

            LibraryHeader.Visibility = Visibility.Visible;
            myWebBrowser.Visibility = Visibility.Visible;

        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedLibrary = LibraryNameLabel.Content.ToString();
            var selectedVersion = VersionsComboBox.SelectedItem.ToString();

            MessageBox.Show($"Installing {selectedLibrary} version {selectedVersion}");

            InstallButton.Visibility = Visibility.Collapsed;
            RemoveButton.Visibility = Visibility.Visible;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedLibrary = LibraryNameLabel.Content.ToString();
            var selectedVersion = VersionsComboBox.SelectedItem.ToString();

            MessageBox.Show($"Removing {selectedLibrary} version {selectedVersion}");

            InstallButton.Visibility = Visibility.Visible;
            RemoveButton.Visibility = Visibility.Collapsed;
        }


        private string GenerateHtml(string name)
        {
            if (_jsonData == null || !_jsonData.libraries.ContainsKey(name)) return "";

            var library = _jsonData.libraries[name];
            var versions = library.versions;
            var description = library.description ?? "No description available.";
            var licenses = library.license != null ? string.Join(", ", library.license) : "No license information.";
            var cmakeFileName = library.cmake_file_name ?? name;
            var cmakeTargetName = library.cmake_target_name ?? $"{name}::{name}";
            var warningSection = !library.v2 ? "<div class='warning'>Warning: This library is not compatible with Conan v2.</div>" : string.Empty;

            UpdatePanel(name, description, licenses, versions);

            var additionalInfo = $@"
        <p>Please, be aware that this information is generated automatically and it may contain some mistakes. If you have any problem, you can check the <a href='https://github.com/conan-io/conan-center-index/tree/master/recipes/{name}' target='_blank'>upstream recipe</a> to confirm the information. Also, for more detailed information on how to consume Conan packages, please <a href='https://docs.conan.io/2/tutorial/consuming_packages.html' target='_blank'>check the Conan documentation</a>.</p>";

            var componentsSection = string.Empty;
            if (library.components != null && library.components.Count > 0)
            {
                componentsSection += "<h2>Declared components for " + name + "</h2>";
                componentsSection += "<p>This library declares components, so you can use the components targets in your project instead of the global target. There are the declared CMake target names for the library's components:<br><ul>";
                foreach (var component in library.components)
                {
                    var componentCmakeTargetName = component.Value.cmake_target_name ?? $"{name}::{component.Key}";
                    componentsSection += $"<li>{component.Key}: <code>{componentCmakeTargetName}</code></li>";
                }
                componentsSection += "</ul></p>";
            }

            var htmlTemplate = $@"
<html>
<head>
    <style>
        body {{ font-family: 'Roboto', sans-serif; }}
        .code {{ background-color: lightgray; padding: 10px; border-radius: 5px; overflow: auto; white-space: pre; }}
        .warning {{ background-color: yellow; padding: 10px; }}
    </style>
</head>
<body>
    {warningSection}
    <h2>Using {name} with CMake</h2>
<pre class='code'>
# First, tell CMake to find the package.
find_package({cmakeFileName})

# Then, link your executable or library with the package target.
target_link_libraries(your_target_name PRIVATE {cmakeTargetName})
</pre>
    {additionalInfo}
    {componentsSection}
</body>
</html>";
            return htmlTemplate;
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]

        private void ShowConfigurationDialog()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _dte.ExecuteCommand("Tools.Options", GuidList.strConanOptionsPage);
        }
        
        private void Configuration_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ShowConfigurationDialog();

            string conanPath = GlobalSettings.ConanExecutablePath;

            foreach (Project project in _dte.Solution.Projects)
            {
                if (project.Object is VCProject vcProject)
                {
                    string projectFilePath = project.FullName;
                    string projectDirectory = Path.GetDirectoryName(projectFilePath);
                    string propsFilePath = Path.Combine(projectDirectory, "conandeps.props");
                    string conanInstallCommand = $"\"{conanPath}\" install --requires=fmt/10.2.1 -g=MSBuildDeps -s=build_type=$(Configuration) --build=missing";

                    foreach (VCConfiguration vcConfig in (IEnumerable)vcProject.Configurations)
                    {
                        _ = _manager.SaveConanPrebuildEventAsync(vcProject, vcConfig, conanInstallCommand);
                    }
                }
            }

            foreach (Project project in _dte.Solution.Projects)
            {
                if (project.Object is VCProject vcProject)
                {
                    string projectFilePath = project.FullName;
                    string projectDirectory = Path.GetDirectoryName(projectFilePath);
                    string propsFilePath = Path.Combine(projectDirectory, "conandeps.props");

                    foreach (VCConfiguration vcConfig in (IEnumerable)vcProject.Configurations)
                    {
                        _ = _manager.InjectConanDepsAsync(vcProject, vcConfig, propsFilePath);
                    }
                }
            }

        }

        private string getProfileName(string vcConfigName)
        {
            return vcConfigName.Replace("|", "_");
        }

        private string getConanArch(string platform)
        {
            var archMap = new Dictionary<string, string>();
            archMap["x64"] = "x86_64";
            archMap["Win32"] = "x86";
            return archMap[platform];
        }

        private string getConanCompilerVersion(string platformToolset)
        {
            var msvcVersionMap = new Dictionary<string, string>();
            msvcVersionMap["v143"] = "193";
            msvcVersionMap["v142"] = "192";
            msvcVersionMap["v141"] = "191";
            return msvcVersionMap[platformToolset];
        }

        private string getConanCppstd(string languageStandard)
        {
            List<string> cppStdValues = new List<string>() { "14", "17", "20", "23" };

            foreach (string cppStdValue in cppStdValues)
            {
                if (languageStandard.Contains(cppStdValue))
                {
                    return cppStdValue;
                }
            }
            return "null";
        }

        private void ShowPackages_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (_dte != null && _dte.Solution != null && _dte.Solution.Projects != null)
                {
                    foreach (Project project in _dte.Solution.Projects)
                    {
                        if (project.Object is VCProject vcProject)
                        {
                            string projectDirectory = System.IO.Path.GetDirectoryName(project.FullName);
                            string conanProjectDirectory = System.IO.Path.Combine(projectDirectory, ".conan");

                            if (!Directory.Exists(conanProjectDirectory))
                            {
                                Directory.CreateDirectory(conanProjectDirectory);
                            }

                            foreach (VCConfiguration vcConfig in (IEnumerable)vcProject.Configurations)
                            {
                                string profileName = getProfileName(vcConfig.Name);
                                string profilePath = System.IO.Path.Combine(conanProjectDirectory, profileName);

                                if (!File.Exists(profilePath))
                                {
                                    string toolset = vcConfig.Evaluate("$(PlatformToolset)").ToString();
                                    string compilerVersion = getConanCompilerVersion(toolset);
                                    string arch = getConanArch(vcConfig.Evaluate("$(PlatformName)").ToString());
                                    IVCRulePropertyStorage generalRule = vcConfig.Rules.Item("ConfigurationGeneral") as IVCRulePropertyStorage;
                                    string languageStandard = generalRule == null ? null : generalRule.GetEvaluatedPropertyValue("LanguageStandard");
                                    string cppStd = getConanCppstd(languageStandard);
                                    string buildType = vcConfig.ConfigurationName;
                                    string profileContent = $"[settings]\narch={arch}\nbuild_type={buildType}\ncompiler=msvc\ncompiler.cppstd={cppStd}\ncompiler.runtime=dynamic\n" +
                                        $"compiler.runtime_type={buildType}\ncompiler.version={compilerVersion}\nos=Windows";
                                    File.WriteAllText(profilePath, profileContent);
                                }
                            }
                        }
                    }

                    MessageBox.Show($"Generated profiles for actual project.", "Conan profiles generated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"There was a problem generating the file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateJsonDataAsync()
        {
            string jsonUrl = "https://raw.githubusercontent.com/conan-io/conan-clion-plugin/develop2/src/main/resources/conan/targets-data.json";

            string userConanFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".conan-vs-extension");
            string jsonFilePath = Path.Combine(userConanFolder, "targets-data.json");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    string jsonContent = await httpClient.GetStringAsync(jsonUrl);

                    File.WriteAllText(jsonFilePath, jsonContent);

                    MessageBox.Show("Libraries data file updated.", "Libraries data file updated.", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating: {ex.Message}", "Error updating", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            _ = UpdateJsonDataAsync();
        }
    }
}