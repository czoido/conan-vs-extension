using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System.Collections;
using Newtonsoft.Json.Linq;
using static Microsoft.VisualStudio.Shell.ThreadedWaitDialogHelper;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using VSLangProj;


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

            _dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
            if (_dte == null)
            {
                throw new InvalidOperationException("Cannot access DTE service.");
            }

            await LoadLibrariesFromJsonAsync();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterListView(SearchTextBox.Text);
        }

        private async Task LoadLibrariesFromJsonAsync()
        {
            string url = "https://raw.githubusercontent.com/conan-io/conan-clion-plugin/develop2/src/main/resources/conan/targets-data.json";
            using (var httpClient = new HttpClient())
            {
                var json = await httpClient.GetStringAsync(url);
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

        public async Task InjectConanDeps() {
            // Asegúrate de que estás en el hilo de la interfaz de usuario.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            DTE dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
            if (dte == null)
            {
                System.Diagnostics.Debug.WriteLine("Can't get the DTE.");
                return;
            }


            // Obtén la instancia del motor de proyecto VC.
            // var vcProjectEngine = (VCProjectEngine)dte.GetObject("VCProjectEngineObject");

            foreach (Project project in dte.Solution.Projects)
            {
                // Comprueba si el objeto del proyecto es un proyecto de C++.
                if (project.Object is VCProject vcProject)
                {
                    // Itera a través de las configuraciones del proyecto.
                    foreach (VCConfiguration vcConfig in (IEnumerable)vcProject.Configurations)
                    {
                        // Obtener la ruta del archivo de proyecto
                        string projectFilePath = project.FullName;
                        // Obtener el directorio del proyecto
                        string projectDirectory = Path.GetDirectoryName(projectFilePath);
                        // Construir la ruta de la hoja de propiedades relativa al directorio del proyecto
                        string propsFilePath = Path.Combine(projectDirectory, "conandeps.props");


                        vcConfig.AddPropertySheet(propsFilePath);

                        //IVCCollection ctools = (IVCCollection)vcConfig.Tools;
                        //VCProjectEngineObject vcProjectEngine = ctools.Item("VCProjectEngineObject") as VCProjectEngineObject;

                        // Obtiene la colección de hojas de propiedades.
                        //IVCCollection sheets = vcConfig.PropertySheets as IVCCollection;
                        //if (sheets != null)
                        //{
                        // Crear un nuevo archivo de propiedades.
                        //VCPropertySheet newSheet = (VCPropertySheet)vcProjectEngine.CreatePropertySheet("conan");

                        // Asigna el nombre y la ubicación del archivo de propiedades.
                        //newSheet.Name = "MiHerramienta";
                        //newSheet.PropertySheetFile = "archivo.props"; // Cambia esto por la ruta real de tu archivo.

                        // Agrega la nueva hoja de propiedades a la colección.
                        //vcConfig.AddPropertySheet("archivo.props");

                        // Guarda los cambios en el proyecto.
                        //project.Save();
                        //}
                    }
                    project.Save();

                }
            }
        }

            
        public async Task GetInformation()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
            if (dte == null)
            {
                System.Diagnostics.Debug.WriteLine("Can't get the DTE.");
                return;
            }

            string conanPath = GlobalSettings.ConanExecutablePath;

            // TODO: Do this only for the current/active project?
            foreach (Project project in dte.Solution.Projects)
            {
                if (project.Object is VCProject vcProject)
                {

                    foreach (VCConfiguration vcConfig in (IEnumerable)vcProject.Configurations)
                    {
                        // Acceder a las herramientas de la configuración
                        IVCCollection tools = (IVCCollection)vcConfig.Tools;

                        // Intentar obtener el compilador de C/C++
                        VCCLCompilerTool compilerTool = tools.Item("VCCLCompilerTool") as VCCLCompilerTool;
                        if (compilerTool != null)
                        {
                            // Acceder a la propiedad de la versión del toolset si está disponible
                            try
                            {
                                var toolsetVersion = compilerTool.ToolPath;
                                System.Diagnostics.Debug.WriteLine($"Toolset version: {toolsetVersion}");
                            }
                            catch (COMException ex)
                            {
                                // Manejar la excepción si la propiedad no está disponible
                                System.Diagnostics.Debug.WriteLine($"Error accessing toolset version: {ex.Message}");
                            }

                            PropertyDescriptorCollection props = TypeDescriptor.GetProperties(compilerTool);
                            foreach (PropertyDescriptor prop in props)
                            {
                                object val = null;
                                try
                                {
                                    val = prop.GetValue(compilerTool);
                                }
                                catch (Exception ex)
                                {
                                    // Puede haber propiedades que arrojen excepciones al intentar acceder a su valor.
                                    // Puedes manejar la excepción como creas conveniente aquí.
                                    Debug.WriteLine($"Exception al obtener la propiedad {prop.Name}: {ex.Message}");
                                }

                                // Imprimir el nombre de la propiedad y su valor.
                                Debug.WriteLine($"{prop.Name}: {val}");
                            }
                        }

                        // Aquí puedes buscar la propiedad que te interesa
                        // Por ejemplo: PlatformToolset
                        var toolset = vcConfig.Evaluate("$(PlatformToolset)");
                        Debug.WriteLine($"Toolset: {toolset}");

                        foreach (VCPropertySheet sheet in vcConfig.PropertySheets as IVCCollection)
                        {
                            if (sheet == null || String.IsNullOrEmpty(sheet.Name))
                            {
                                continue;
                            }
                            System.Diagnostics.Debug.WriteLine($"{sheet.Name} - {sheet.GetType().Name}");
                        }

                        foreach (var tool in tools)
                        {
                            System.Diagnostics.Debug.WriteLine($"------>>>>{tool.GetType().Name}");
                        }


                        VCPlatform platform = (VCPlatform)vcConfig.Platform;

                        System.Diagnostics.Debug.WriteLine($"------>>>>{vcConfig.Platform}");
                        System.Diagnostics.Debug.WriteLine($"------>>>>{platform.VCProjectEngine}");

                        VCPreBuildEventTool preBuildTool = (VCPreBuildEventTool)tools.Item("VCPreBuildEventTool");

                        /*
                        VCPlatform platform = vcConfig.Platform as VCPlatform;
                        toolset = platform.Tools.ToolsVersion;

                        IVCRulePropertyStorage generalRule = vcConfig.Rules.Item("ConfigurationGeneral") as IVCRulePropertyStorage;
                        if (generalRule != null)
                        {
                            langStandard = generalRule.GetPropertyValue("LanguageStandard");
                        }

                        // If we've found the information we need, we can break out of the loop
                        if (!string.IsNullOrEmpty(toolset) && !string.IsNullOrEmpty(langStandard))
                        {
                            break;
                        }                         
                         */

                        AddPropsFileToVCProject(project, "");

                        if (preBuildTool != null)
                        {
                            string conanInstallCommand = $"\"{conanPath}\" install --requires=fmt/10.2.1 -g=MSBuildDeps -s=build_type=$(Configuration) --build=missing";
                            string currentPreBuildEvent = preBuildTool.CommandLine;

                            // FIXME: maybe better to call a script that has always the same name so we can change the script
                            // if needed without changing the prebuild event.
                            if (!currentPreBuildEvent.Contains("conan"))
                            {
                                preBuildTool.CommandLine = conanInstallCommand + Environment.NewLine + currentPreBuildEvent;
                                project.Save();
                            }
                        }
                    }
                }
            }
        }

        public async Task SaveConanPrebuildEventAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
            if (dte == null)
            {
                System.Diagnostics.Debug.WriteLine("Can't get the DTE.");
                return;
            }

            InjectConanDeps();

            string conanPath = GlobalSettings.ConanExecutablePath;

            // TODO: Do this only for the current/active project?
            foreach (Project project in dte.Solution.Projects)
            {
                if (project.Object is VCProject vcProject)
                {
                    //AddPropsFileToVCProject(vcProject, "");


                    foreach (VCConfiguration vcConfig in (IEnumerable)vcProject.Configurations)
                    {
                        IVCCollection tools = (IVCCollection)vcConfig.Tools;
                        VCPreBuildEventTool preBuildTool = (VCPreBuildEventTool)tools.Item("VCPreBuildEventTool");

                        if (preBuildTool != null)
                        {
                            string conanInstallCommand = $"\"{conanPath}\" install --requires=fmt/10.2.1 -g=MSBuildDeps -s=build_type=$(Configuration) --build=missing";
                            string currentPreBuildEvent = preBuildTool.CommandLine;

                            // FIXME: maybe better to call a script that has always the same name so we can change the script
                            // if needed without changing the prebuild event.
                            if (!currentPreBuildEvent.Contains("conan"))
                            {
                                preBuildTool.CommandLine = conanInstallCommand + Environment.NewLine + currentPreBuildEvent;
                                project.Save();
                            }
                        }
                    }
                }
            }
        }

        // Este método asume que ya tienes acceso al objeto DTE y que estás en el hilo de la UI.
        public void AddPropsFileToVCProject(Project project, string propsFilePath)
        {
            // Asegúrate de estar en el hilo de la UI.

            ThreadHelper.ThrowIfNotOnUIThread();
 
            if (project.Object is VCProject vcProject)
            {
                // Iterar sobre cada configuración del proyecto
                foreach (VCConfiguration config in (IVCCollection)vcProject.Configurations)
                {
                    foreach (VCPropertySheet sheet in config.PropertySheets as IVCCollection)
                    {
                        if (sheet == null || String.IsNullOrEmpty(sheet.Name))
                        {
                            continue;
                        }
                        System.Diagnostics.Debug.WriteLine($"{sheet.Name} - {sheet.GetType().Name}");
                    }


                    // Buscar la herramienta VCPropertySheet
                    VCPropertySheet propSheet = null;
                    IVCCollection tools = config.Tools as IVCCollection;

                    foreach (var tool in tools)
                    {
                        if (tool is VCPropertySheet sheet)
                        {
                            propSheet = sheet;
                            System.Diagnostics.Debug.WriteLine($"{sheet.Name} - {sheet.GetType().Name}");
                            break;
                        }
                    }

                    // Si no existe una VCPropertySheet, se crea una
                    
                    /*
                    if (propSheet == null)
                    {
                        propSheet = new VCPropertySheet
                        {
                            // Configurar la hoja de propiedades aquí
                        };
                        tools.Add(propSheet);
                    }

                    // Agregar el archivo .props a la hoja de propiedades
                    if (propSheet != null)
                    {
                        // Puedes tener que verificar primero si el archivo .props ya está presente.
                        VCPropertySheetTool propSheetTool = propSheet.Tool as VCPropertySheetTool;
                        propSheetTool.PropertySheets = propsFilePath + ";" + propSheetTool.PropertySheets;
                    }
                    */
                }
            }
        }


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

        private void ShowPackages_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
    string.Format(System.Globalization.CultureInfo.CurrentUICulture, "Invoked '{0}'", this.ToString()),
    "Conan C/C++ Package Manager");
        }
    }
}