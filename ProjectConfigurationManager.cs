using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;

namespace conan_vs_extension
{
    public class ProjectConfigurationManager
    {
        
        public ProjectConfigurationManager()
        {
        }

        public async Task InjectConanDepsAsync(VCProject vcProject, VCConfiguration vcConfig, string propsFilePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            vcConfig.AddPropertySheet(propsFilePath);
            vcProject.Save();
        }

        public async Task SaveConanPrebuildEventAsync(VCProject vcProject, VCConfiguration vcConfig, string conanCommand)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVCCollection tools = (IVCCollection)vcConfig.Tools;
            VCPreBuildEventTool preBuildTool = (VCPreBuildEventTool)tools.Item("VCPreBuildEventTool");

            if (preBuildTool != null)
            {
                string currentPreBuildEvent = preBuildTool.CommandLine;
                if (!currentPreBuildEvent.Contains(conanCommand))
                {
                    preBuildTool.CommandLine = conanCommand + Environment.NewLine + currentPreBuildEvent;
                    vcProject.Save();
                }
            }
        }
    }
}
