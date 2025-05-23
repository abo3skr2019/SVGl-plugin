using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Flow.Launcher.Plugin.svgl
{
    /// <summary>
    /// View model for the SVGL plugin settings
    /// </summary>
    public class SettingsViewModel
    {
        /// <summary>
        /// Gets the settings instance
        /// </summary>
        public Settings Settings { get; }
        private readonly PluginInitContext _context;

        /// <summary>
        /// Initializes a new instance of the SettingsViewModel class
        /// </summary>
        /// <param name="settings">The settings instance</param>
        /// <param name="context">The plugin context</param>
        public SettingsViewModel(Settings settings, PluginInitContext context)
        {
            Settings = settings;
            _context = context;
            
            // Set the cache path for display
            Settings.CachePath = Path.Combine(Path.GetTempPath(), "FlowLauncher", "svgl_cache");
        }

        /// <summary>
        /// Clears the SVG cache files
        /// </summary>
        public void ClearCacheCommand()
        {
            try
            {
                var cacheDir = Settings.CachePath;
                if (Directory.Exists(cacheDir))
                {
                    var files = Directory.GetFiles(cacheDir, "*.svg");
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _context.API.ShowMsg($"Error deleting {Path.GetFileName(file)}", ex.Message);
                        }
                    }
                    
                    // Also clear the in-memory search cache
                    ClearSearchCache();
                    
                    _context.API.ShowMsg("Cache Cleared", $"Successfully deleted {files.Length} cached SVG files");
                }
                else
                {
                    _context.API.ShowMsg("Cache Directory Not Found", "The cache directory does not exist.");
                }
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg("Error Clearing Cache", ex.Message);
            }
        }
        
        /// <summary>
        /// Clears the in-memory search cache - to be called from Main.cs
        /// </summary>
        public void ClearSearchCache()
        {
            // This will be implemented in Main.cs with a static method
            Svgl.ClearSearchCache();
        }
    }
}
