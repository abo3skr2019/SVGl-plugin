using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Flow.Launcher.Plugin;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.svgl
{
    /// <summary>
    /// SVGL Plugin for Flow Launcher to search and copy SVG icons
    /// </summary>
    public class Svgl : IPlugin, IAsyncPlugin
    {
        private PluginInitContext _context;
        private static readonly HttpClient _httpClient = new HttpClient();
        // Debounce fields
        private static DateTime _lastQueryTime = DateTime.MinValue;
        private static string _lastSearchText = string.Empty;
        private static readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);
        private static readonly string _cacheDir = Path.Combine(Path.GetTempPath(), "FlowLauncher", "svgl_cache");

        /// <summary>
        /// Initialize the plugin
        /// </summary>
        /// <param name="context">Context containing plugin API</param>
        public void Init(PluginInitContext context)
        {
            _context = context;
            Directory.CreateDirectory(_cacheDir);
        }

        /// <summary>
        /// Initialize the plugin asynchronously
        /// </summary>
        /// <param name="context">Context containing plugin API</param>
        public Task InitAsync(PluginInitContext context)
        {
            _context = context;
            Directory.CreateDirectory(_cacheDir);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Query the SVGL API for SVG icons (sync version, calls async method)
        /// </summary>
        /// <param name="query">Search query from Flow Launcher</param>
        /// <returns>List of results with SVG icons</returns>
        public List<Result> Query(Query query)
        {
            return QueryAsync(query, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
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
            if (string.IsNullOrEmpty(search)) return results;

            var now = DateTime.Now;
            // If search text changed and user is still typing (within debounce interval), skip
            if (!search.Equals(_lastSearchText, StringComparison.OrdinalIgnoreCase) &&
                now - _lastQueryTime < _debounceInterval)
                return results;
            _lastSearchText = search;
            _lastQueryTime = now;

            var requestUrl = $"https://api.svgl.app?search={Uri.EscapeDataString(search)}";

            // HTTP request and JSON parsing with error handling
            List<SvglApiResult> items;
            try
            {
                var response = await _httpClient.GetAsync(requestUrl, token);
                if (!response.IsSuccessStatusCode)
                {
                    results.Add(new Result
                    {
                        Title = $"SVGL API Error ({(int)response.StatusCode}): {response.ReasonPhrase}",
                        SubTitle = "Rate limited or API issue, please try again later",
                        Action = _ => false
                    });
                    return results;
                }
                var json = await response.Content.ReadAsStringAsync();
                items = JsonConvert.DeserializeObject<List<SvglApiResult>>(json);
            }
            catch (Exception ex)
            {
                results.Add(new Result
                {
                    Title = "SVGL API Error",
                    SubTitle = ex.Message,
                    Action = _ => false
                });
                return results;
            }

            foreach (var item in items)
            {
                if (token.IsCancellationRequested)
                    return results;

                // prepare local icon for light theme
                var lightPath = Path.Combine(_cacheDir, $"{item.Id}_light.svg");
                if (!File.Exists(lightPath))
                {
                    var svg = await _httpClient.GetStringAsync(item.Route.Light);
                    File.WriteAllText(lightPath, svg);
                }

                results.Add(new Result
                {
                    Title = $"{item.Title} (light)",
                    SubTitle = item.Url,
                    IcoPath = lightPath,
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard(File.ReadAllText(lightPath));
                        return true;
                    }
                });

                // prepare local icon for dark theme
                var darkPath = Path.Combine(_cacheDir, $"{item.Id}_dark.svg");
                var darkRawPath = Path.Combine(_cacheDir, $"{item.Id}_dark_raw.svg");
                // cache raw SVG for dark theme
                if (!File.Exists(darkRawPath))
                {
                    var rawSvg = await _httpClient.GetStringAsync(item.Route.Dark);
                    File.WriteAllText(darkRawPath, rawSvg);
                }
                // generate modified SVG with black background if needed
                if (!File.Exists(darkPath))
                {
                    var rawSvg = File.ReadAllText(darkRawPath);
                    var tagEnd = rawSvg.IndexOf('>');
                    var svgWithBg = rawSvg.Insert(tagEnd + 1, "<rect width=\"100%\" height=\"100%\" fill=\"black\" />");
                    File.WriteAllText(darkPath, svgWithBg);
                }

                results.Add(new Result
                {
                    Title = $"{item.Title} (dark)",
                    SubTitle = item.Url,
                    IcoPath = darkPath,
                    Action = _ =>
                    {
                        // copy original unedited SVG to clipboard
                        var svg = File.ReadAllText(darkRawPath);
                        _context.API.CopyToClipboard(svg);
                        return true;
                    }
                });
            }
            return results;
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