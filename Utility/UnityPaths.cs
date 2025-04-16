using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public static class UnityPaths
    {
        public static string DataPath => AppContext.BaseDirectory;

        public static string PersistentDataPath
        {
            get
            {
                string basePath;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Application Support");
                else // Linux
                    basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");

                string appFolder = "MyApp"; // ←ここは自分のアプリ名に書き換えてね
                return Path.Combine(basePath, appFolder);
            }
        }

        public static string TemporaryCachePath => Path.GetTempPath();
    }
}
