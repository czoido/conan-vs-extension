using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using VSLangProj;

namespace conan_vs_extension
{
    public class ProjectConfigurationManager
    {
        
        public ProjectConfigurationManager()
        {
        }

        public static async Task InjectConanDepsAsync(Project project, VCConfiguration vcConfig)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            string projectFilePath = project.FullName;
            string projectDirectory = Path.GetDirectoryName(projectFilePath);
            string propsFilePath = Path.Combine(projectDirectory, "conan", "conandeps.props");
            if (File.Exists(propsFilePath))
            {
                bool isAlreadyIncluded = false;
                IVCCollection propertySheets = vcConfig.PropertySheets as IVCCollection;
                foreach (VCPropertySheet sheet in propertySheets)
                {
                    if (sheet.PropertySheetFile.Equals(propsFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        isAlreadyIncluded = true;
                        break;
                    }
                }

                if (!isAlreadyIncluded)
                {
                    vcConfig.AddPropertySheet(propsFilePath);
                    project.Save();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Properties file '{propsFilePath}' does not exist.");
            }
        }

        private async Task SaveConanPrebuildEventAsync(VCProject vcProject, VCConfiguration vcConfig, string conanCommand)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVCCollection tools = (IVCCollection)vcConfig.Tools;
            VCPreBuildEventTool preBuildTool = (VCPreBuildEventTool)tools.Item("VCPreBuildEventTool");

            if (preBuildTool != null)
            {
                string currentPreBuildEvent = preBuildTool.CommandLine;
                if (!currentPreBuildEvent.Contains("conan"))
                {
                    // FIXME: better do this with a script file?
                    preBuildTool.CommandLine = conanCommand + Environment.NewLine + currentPreBuildEvent;
                    vcProject.Save();
                }
            }
        }

        public void SaveConanPrebuildEventsAllConfig(VCProject vcProject)
        {
            string conanPath = GlobalSettings.ConanExecutablePath;
            foreach (VCConfiguration vcConfig in (IEnumerable)vcProject.Configurations)
            {
                string profileName = ConanProfilesManager.getProfileName(vcConfig);
                string prebuildCommand = $"\"{conanPath}\" install . -pr:h=.conan/{profileName} --build=missing";
                _ = SaveConanPrebuildEventAsync(vcProject, vcConfig, prebuildCommand);
            }

        }

        public static Project GetStartupProject(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SolutionBuild solutionBuild = (SolutionBuild)dte.Solution.SolutionBuild;
            string startupProjectName = (string)((Array)solutionBuild.StartupProjects).GetValue(0);
            foreach (Project project in dte.Solution.Projects)
            {
                if (project.UniqueName == startupProjectName)
                {
                    return project;
                }
            }

            return null;
        }

        public static Project GetProjectByName(DTE dte, string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (Project project in dte.Solution.Projects)
            {
                if (project.UniqueName == name)
                {
                    return project;
                }
            }

            return null;
        }

        public static VCConfiguration GetVCConfig(DTE dte, Project project, string ProjectConfig, string Platform)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project.Object is VCProject vcProject)
            {
                foreach (VCConfiguration vcConfig in (IEnumerable)vcProject.Configurations)
                {
                    VCPlatform vcPlatform = vcConfig.Platform as VCPlatform;
                    if (vcConfig.ConfigurationName.Equals(ProjectConfig) 
                        && vcPlatform.Name.Equals(Platform)) { 
                        return vcConfig; 
                    }  
                }
            }
            return null;
        }
    }
}
