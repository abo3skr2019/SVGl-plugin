using System;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.svgl
{
    /// <summary>
    /// Settings class for the SVGL plugin
    /// </summary>
    public class Settings : BaseModel
    {
        // Default debounce interval in milliseconds
        private int _debounceInterval = 500;
        /// <summary>
        /// Gets or sets the debounce interval in milliseconds for search queries
        /// </summary>
        public int DebounceInterval
        {
            get => _debounceInterval;
            set
            {
                if (value < 0)
                    value = 0;
                _debounceInterval = value;
                OnPropertyChanged();
            }
        }

        // Enable or disable background for dark theme SVGs
        private bool _addDarkBackground = true;
        /// <summary>
        /// Gets or sets whether to add a black background to dark theme SVGs
        /// </summary>
        public bool AddDarkBackground
        {
            get => _addDarkBackground;
            set
            {
                _addDarkBackground = value;
                OnPropertyChanged();
            }
        }

        // Maximum number of results to show
        private int _maxResults = 10;
        /// <summary>
        /// Gets or sets the maximum number of results to show
        /// </summary>
        public int MaxResults
        {
            get => _maxResults;
            set
            {
                if (value < 1)
                    value = 1;
                _maxResults = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Flag to clear the cache - not saved, just used for the UI
        /// </summary>
        [JsonIgnore]
        public bool ClearCache { get; set; }        /// <summary>
        /// The path where cache is stored (read-only for display)
        /// </summary>
        [JsonIgnore]
        public string CachePath { get; set; }
    }
}
