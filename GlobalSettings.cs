using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace conan_vs_extension{
    public static class GlobalSettings
    {
        private static string _conanExecutablePath;

        public string ConanExecutablePath
        {
            get => _conanExecutablePath;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _conanExecutablePath = value;
                    GlobalSettings.ConanExecutablePath = value;
                }
            }
        }
    }
}

