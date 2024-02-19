using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;

namespace conan_vs_extension
{
    public class BuildEventsHandler
    {
        private readonly DTE _dte;
        private readonly BuildEvents _buildEvents;

        public BuildEventsHandler(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _dte = dte;
            _buildEvents = _dte.Events.BuildEvents;
            
            // Subscribe to compilation events
            _buildEvents.OnBuildBegin -= OnBuildBegin;
            _buildEvents.OnBuildDone += OnBuildDone;
            _buildEvents.OnBuildProjConfigBegin += OnBuildProjConfigBegin;
            _buildEvents.OnBuildProjConfigDone += OnBuildProjConfigDone;
        }

        private void OnBuildBegin(vsBuildScope scope, vsBuildAction action)
        {
            var message = "OnBuildBegin";
            System.Diagnostics.Debug.WriteLine(message);
        }

        private void OnBuildProjConfigBegin(string Project, string ProjectConfig, string Platform, string SolutionConfig)
        {
            var message = "OnBuildProjConfigBegin";
            System.Diagnostics.Debug.WriteLine(message);
        }

        private void OnBuildProjConfigDone(string Project, string ProjectConfig, string Platform, string SolutionConfig, bool Success)
        {
            var message = "OnBuildProjConfigDone";
            System.Diagnostics.Debug.WriteLine(message);
        }

        private void OnBuildDone(vsBuildScope Scope, vsBuildAction Action)
        {
            var message = "OnBuildDone";
            System.Diagnostics.Debug.WriteLine(message);
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _buildEvents.OnBuildBegin -= OnBuildBegin;
            _buildEvents.OnBuildDone -= OnBuildDone;
            _buildEvents.OnBuildProjConfigBegin -= OnBuildProjConfigBegin;
            _buildEvents.OnBuildProjConfigDone -= OnBuildProjConfigDone;        }
    }
}
