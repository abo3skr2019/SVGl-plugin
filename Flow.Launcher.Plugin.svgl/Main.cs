using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Flow.Launcher.Plugin;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Linq;

namespace Flow.Launcher.Plugin.svgl
{/// <summary>
    /// SVGL Plugin for Flow Launcher to search and copy SVG icons
    /// </summary>
    public class Svgl : IPlugin, IAsyncPlugin, ISettingProvider
    {
        private PluginInitContext _context;
        private Settings _settings;
        private SettingsViewModel _viewModel;
        private static readonly HttpClient _httpClient = new HttpClient();
        
        // Cache system with expiration
        private static Dictionary<string, CacheEntry<List<SvglApiResult>>> _searchCache = 
            new Dictionary<string, CacheEntry<List<SvglApiResult>>>(StringComparer.OrdinalIgnoreCase);
        private static readonly string _cacheDir = Path.Combine(Path.GetTempPath(), "FlowLauncher", "svgl_cache");
          // Debounce and API request tracking
        private static CancellationTokenSource _currentRequestCts;
        private static DateTime _lastQueryTime = DateTime.MinValue;
        private static string _lastSearchText = string.Empty;
        private static DateTime _lastApiCallTime = DateTime.MinValue;
        private static SemaphoreSlim _apiSemaphore = new SemaphoreSlim(1, 1);
        private static Timer _debounceTimer;
        private static Queue<DateTime> _apiCallTimes = new Queue<DateTime>();
        
        // API rate limiting - default to max 10 requests per minute
        private const int ApiMinIntervalMs = 100; // Minimum time between API calls
        private const int ApiMaxPerMinute = 10;   // Maximum API calls per minute
          /// <summary>
        /// Clears the in-memory search cache
        /// </summary>
        public static void ClearSearchCache()
        {
            _searchCache.Clear();
        }

        /// <summary>
        /// Cleanup method to dispose of resources
        /// </summary>
        public static void Cleanup()
        {
            _debounceTimer?.Dispose();
            _currentRequestCts?.Cancel();
            _currentRequestCts?.Dispose();
        }

        /// <summary>
        /// Initialize the plugin
        /// </summary>
        /// <param name="context">Context containing plugin API</param>
        public void Init(PluginInitContext context)
        {
            _context = context;
            _settings = _context.API.LoadSettingJsonStorage<Settings>();
            _viewModel = new SettingsViewModel(_settings, context);
            Directory.CreateDirectory(_cacheDir);
        }

        /// <summary>
        /// Initialize the plugin asynchronously
        /// </summary>
        /// <param name="context">Context containing plugin API</param>
        public Task InitAsync(PluginInitContext context)
        {
            _context = context;
            _settings = _context.API.LoadSettingJsonStorage<Settings>();
            _viewModel = new SettingsViewModel(_settings, context);
            Directory.CreateDirectory(_cacheDir);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Create the settings panel for the plugin
        /// </summary>
        /// <returns>Settings control</returns>
        public Control CreateSettingPanel()
        {
            return new SettingsControl(_context, _viewModel);
        }

        /// <summary>
        /// Query the SVGL API for SVG icons (sync version, calls async method)
        /// </summary>
        /// <param name="query">Search query from Flow Launcher</param>
        /// <returns>List of results with SVG icons</returns>
        public List<Result> Query(Query query)
        {
            return QueryAsync(query, CancellationToken.None).GetAwaiter().GetResult();
        }        /// <summary>
        /// Query the SVGL API for SVG icons (async version)
        /// </summary>
        /// <param name="query">Search query from Flow Launcher</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>List of results with SVG icons</returns>
        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var results = new List<Result>();
            var raw = query.Search; // preserve original including trailing whitespace
            var search = raw?.Trim();
            
            if (string.IsNullOrEmpty(search))
            {
                // When no search is provided, show placeholder result
                results.Add(new Result
                {
                    Title = "Search for SVG Icons",
                    SubTitle = "Start typing to search for icons...",
                    IcoPath = "icon.svg",
                    Action = _ => false
                });
                return results;
            }

            // Cancel any ongoing requests when user changes query
            if (_currentRequestCts != null && search != _lastSearchText)
            {
                _currentRequestCts.Cancel();
                _currentRequestCts.Dispose();
                _currentRequestCts = null;
            }

            // Dispose existing debounce timer
            _debounceTimer?.Dispose();

            // Create new cancellation token source linked to the provided token
            _currentRequestCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var localCts = _currentRequestCts;

            var now = DateTime.Now;
            _lastSearchText = search;
            _lastQueryTime = now;
            
            // Check cache first with expiration - always return immediately if we have cached results
            if (_searchCache.TryGetValue(search, out var cachedEntry) && 
                cachedEntry != null && 
                !cachedEntry.IsExpired(_settings.CacheLifetime) &&
                cachedEntry.Data.Count > 0)
            {
                return CreateResultsFromItems(cachedEntry.Data, localCts.Token);
            }

            // Implement proper debouncing using the settings
            if (_settings.DebounceInterval > 0)
            {
                var timeSinceLastQuery = now - _lastQueryTime;
                if (timeSinceLastQuery < TimeSpan.FromMilliseconds(_settings.DebounceInterval))
                {
                    // Start debounce timer
                    var delayMs = _settings.DebounceInterval - (int)timeSinceLastQuery.TotalMilliseconds;
                    
                    var tcs = new TaskCompletionSource<List<Result>>();
                    _debounceTimer = new Timer(async _ =>
                    {
                        try
                        {
                            if (!localCts.Token.IsCancellationRequested && search == _lastSearchText)
                            {
                                var debouncedResults = await PerformApiRequest(search, localCts.Token);
                                tcs.SetResult(debouncedResults);
                            }
                            else
                            {
                                tcs.SetResult(new List<Result>());
                            }
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                        finally
                        {
                            _debounceTimer?.Dispose();
                        }
                    }, null, delayMs, Timeout.Infinite);
                    
                    // Return loading result while debouncing
                    results.Add(new Result
                    {
                        Title = $"Searching for \"{search}\"...",
                        SubTitle = "Debouncing search query...",
                        IcoPath = "icon.svg",
                        Action = _ => false
                    });
                    return results;
                }
            }

            // No debounce needed or debounce interval is 0, proceed immediately
            return await PerformApiRequest(search, localCts.Token);
        }

        /// <summary>
        /// Performs the actual API request with proper rate limiting
        /// </summary>
        /// <param name="search">Search term</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>List of results</returns>
        private async Task<List<Result>> PerformApiRequest(string search, CancellationToken token)
        {
            var results = new List<Result>();

            try
            {
                await _apiSemaphore.WaitAsync(token);
                try
                {
                    var now = DateTime.Now;
                    
                    // Clean up old API call times (older than 1 minute)
                    while (_apiCallTimes.Count > 0 && _apiCallTimes.Peek() < now.AddMinutes(-1))
                    {
                        _apiCallTimes.Dequeue();
                    }
                    
                    // Check if we've hit the rate limit
                    if (_apiCallTimes.Count >= ApiMaxPerMinute)
                    {
                        results.Add(new Result
                        {
                            Title = "Rate limit exceeded",
                            SubTitle = $"Maximum {ApiMaxPerMinute} API calls per minute reached. Please wait.",
                            IcoPath = "icon.svg",
                            Action = _ => false
                        });
                        return results;
                    }
                    
                    // Enforce minimum interval between API calls
                    var timeSinceLastCall = now - _lastApiCallTime;
                    if (timeSinceLastCall < TimeSpan.FromMilliseconds(ApiMinIntervalMs))
                    {
                        await Task.Delay(ApiMinIntervalMs - (int)timeSinceLastCall.TotalMilliseconds, token);
                    }
                    
                    // Record this API call
                    _apiCallTimes.Enqueue(now);
                    _lastApiCallTime = now;

                    var requestUrl = $"https://api.svgl.app?search={Uri.EscapeDataString(search)}";
                    var response = await _httpClient.GetAsync(requestUrl, token);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        results.Add(new Result
                        {
                            Title = $"SVGL API Error ({(int)response.StatusCode}): {response.ReasonPhrase}",
                            SubTitle = "Rate limited or API issue, please try again later",
                            IcoPath = "icon.svg",
                            Action = _ => false
                        });
                        return results;
                    }
                    
                    var json = await response.Content.ReadAsStringAsync();
                    var items = JsonConvert.DeserializeObject<List<SvglApiResult>>(json);
                    
                    // Update cache with expiration
                    _searchCache[search] = new CacheEntry<List<SvglApiResult>>(items);
                    
                    // Return results from items
                    return CreateResultsFromItems(items, token);
                }
                finally
                {
                    _apiSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Query was canceled, return empty result
                return new List<Result>();
            }
            catch (Exception ex)
            {
                results.Add(new Result
                {
                    Title = "SVGL API Error",
                    SubTitle = ex.Message,
                    IcoPath = "icon.svg",
                    Action = _ => false
                });
                return results;
            }
        }

        /// <summary>
        /// Creates results from API items
        /// </summary>
        private List<Result> CreateResultsFromItems(List<SvglApiResult> items, CancellationToken token)
        {
            var results = new List<Result>();
            
            if (items == null || items.Count == 0)
            {
                results.Add(new Result
                {
                    Title = "No icons found",
                    SubTitle = "Try a different search term",
                    IcoPath = "icon.svg",
                    Action = _ => false
                });
                return results;
            }
            
            foreach (var item in items)
            {
                if (token.IsCancellationRequested)
                    return results;
                    
                // Limit results based on settings (each item produces 2 results - light and dark)
                if (results.Count >= _settings.MaxResults * 2)
                    break;

                try
                {
                    // Get or create the local icon files
                    var (lightPath, darkPath, darkRawPath) = GetOrCreateIconFiles(item);

                    // Light theme result
                    results.Add(new Result
                    {
                        Title = $"{item.Title} (light)",
                        SubTitle = $"Copy icon • {item.Category} • {item.Url}",
                        IcoPath = lightPath,
                        Score = 100, // Match the WebSearch scoring approach
                        Action = _ =>
                        {
                            try
                            {
                                var svg = File.ReadAllText(lightPath);
                                _context.API.CopyToClipboard(svg);
                                _context.API.ShowMsg("SVG Copied", $"The {item.Title} (light) icon has been copied to clipboard");
                                return true;
                            }
                            catch (Exception ex)
                            {
                                _context.API.ShowMsg("Error", $"Failed to copy SVG: {ex.Message}");
                                return false;
                            }
                        }
                    });

                    // Dark theme result
                    results.Add(new Result
                    {
                        Title = $"{item.Title} (dark)",
                        SubTitle = $"Copy icon • {item.Category} • {item.Url}",
                        IcoPath = darkPath,
                        Score = 99, // Slightly lower score than light theme
                        Action = _ =>
                        {
                            try
                            {
                                // copy original unedited SVG to clipboard
                                var svg = File.ReadAllText(darkRawPath);
                                _context.API.CopyToClipboard(svg);
                                _context.API.ShowMsg("SVG Copied", $"The {item.Title} (dark) icon has been copied to clipboard");
                                return true;
                            }
                            catch (Exception ex)
                            {
                                _context.API.ShowMsg("Error", $"Failed to copy SVG: {ex.Message}");
                                return false;
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Skip this item if there's an error processing it
                    _context.API.LogException("SVGL Plugin", "Error processing SVG item", ex);
                }
            }
            return results;
        }
        
        /// <summary>
        /// Gets or creates the icon files for a given API result
        /// </summary>
        /// <param name="item">The API result item</param>
        /// <returns>Tuple with paths to light, dark, and raw dark SVG files</returns>
        private (string LightPath, string DarkPath, string DarkRawPath) GetOrCreateIconFiles(SvglApiResult item)
        {
            // prepare local icon for light theme
            var lightPath = Path.Combine(_cacheDir, $"{item.Id}_light.svg");
            if (!File.Exists(lightPath))
            {
                var svg = _httpClient.GetStringAsync(item.Route.Light).GetAwaiter().GetResult();
                File.WriteAllText(lightPath, svg);
            }

            // prepare local icon for dark theme
            var darkPath = Path.Combine(_cacheDir, $"{item.Id}_dark.svg");
            var darkRawPath = Path.Combine(_cacheDir, $"{item.Id}_dark_raw.svg");
            
            // cache raw SVG for dark theme
            if (!File.Exists(darkRawPath))
            {
                var rawSvg = _httpClient.GetStringAsync(item.Route.Dark).GetAwaiter().GetResult();
                File.WriteAllText(darkRawPath, rawSvg);
            }

            // generate modified SVG with black background if needed
            if (!File.Exists(darkPath) || _settings.AddDarkBackground != File.Exists(darkPath + ".hasbg"))
            {
                var rawSvg = File.ReadAllText(darkRawPath);
                
                if (_settings.AddDarkBackground)
                {
                    var tagEnd = rawSvg.IndexOf('>');
                    var svgWithBg = rawSvg.Insert(tagEnd + 1, "<rect width=\"100%\" height=\"100%\" fill=\"black\" />");
                    File.WriteAllText(darkPath, svgWithBg);
                    // Create marker file to track background setting
                    File.WriteAllText(darkPath + ".hasbg", "1");
                }
                else
                {
                    // If the background is disabled, just use the raw SVG for display
                    File.WriteAllText(darkPath, rawSvg);
                    // Remove marker file if it exists
                    if (File.Exists(darkPath + ".hasbg"))
                        File.Delete(darkPath + ".hasbg");
                }
            }
            
            return (lightPath, darkPath, darkRawPath);
        }
    }

    // Model classes for SVGL API JSON
    class SvglApiResult
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        [JsonConverter(typeof(RouteInfoConverter))]
        public RouteInfo Route { get; set; }
        public WordmarkInfo Wordmark { get; set; }
        public string Url { get; set; }
        public string BrandUrl { get; set; }
    }

    class RouteInfo
    {
        public string Light { get; set; }
        public string Dark { get; set; }
    }

    class WordmarkInfo
    {
        public string Light { get; set; }
        public string Dark { get; set; }
    }
    
    // Custom JSON converter to handle both string and object formats for Route
    class RouteInfoConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RouteInfo);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                // If the route is a string, create a RouteInfo with same URL for both light and dark
                string url = reader.Value.ToString();
                return new RouteInfo { Light = url, Dark = url };
            }
            else
            {
                // If it's an object, deserialize it normally
                return serializer.Deserialize<RouteInfo>(reader);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}