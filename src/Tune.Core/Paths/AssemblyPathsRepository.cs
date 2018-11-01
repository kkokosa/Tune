using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tune.Core.Extensions;

namespace Tune.Core.Paths
{
    public interface IAssemblyPathsRepository
    {
        string GetAssemblyPathBy(string name, DiagnosticAssembyPlatform platform);
    }

    internal class AssemblyPathsRepository : IAssemblyPathsRepository
    {
        private bool _initialized = false;
        private List<string> _cachedAssemblies = new List<string>();
        private Dictionary<string, string> _cachedQueries = new Dictionary<string, string>();

        private void InitializeCache()
        {
            string gacPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "assembly");
            DirectoryInfo assemblyDirectory = new DirectoryInfo(gacPath);
            List<DirectoryInfo> directoriesToTraverse = new List<DirectoryInfo>();
            foreach (var dir in assemblyDirectory.GetDirectories())
            {
                foreach (var file in dir.GetFiles("*.dll", SearchOption.AllDirectories))
                {
                    _cachedAssemblies.Add(file.FullName);
                }
            }
            _initialized = true;
        }
        public string GetAssemblyPathBy(string name, DiagnosticAssembyPlatform platform)
        {
            if (!_initialized)
            {
                InitializeCache();
            }
            var cacheKey = name + platform.ToPlatformString();
            if (_cachedQueries.TryGetValue(cacheKey, out string result))
            {
                return result;
            }
            var assemblyPath = _cachedAssemblies.LastOrDefault(assembly => assembly.Contains(name) && assembly.Contains("_" + platform.ToPlatformString()));
            _cachedQueries.Add(cacheKey, assemblyPath);
            return assemblyPath;
        }
    }
}
