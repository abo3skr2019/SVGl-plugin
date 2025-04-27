using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.svgl
{
    public class Svgl : IPlugin
    {
        private PluginInitContext _context;
        private static readonly HttpClient _httpClient = new HttpClient();
        // Debounce fields
        private static DateTime _lastQueryTime = DateTime.MinValue;
        private static string _lastSearchText = string.Empty;
        private static readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
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
                var response = _httpClient.GetAsync(requestUrl).GetAwaiter().GetResult();
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
                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
                results.Add(new Result
                {
                    Title = $"{item.Title} (light)",
                    SubTitle = item.Url,
                    IcoPath = item.Route.Light,
                    Action = _ =>
                    {
                        var svgText = _httpClient.GetStringAsync(item.Route.Light).GetAwaiter().GetResult();
                        _context.API.CopyToClipboard(svgText);
                        return true;
                    }
                });
                results.Add(new Result
                {
                    Title = $"{item.Title} (dark)",
                    SubTitle = item.Url,
                    IcoPath = item.Route.Dark,
                    Action = _ =>
                    {
                        var svgText = _httpClient.GetStringAsync(item.Route.Dark).GetAwaiter().GetResult();
                        _context.API.CopyToClipboard(svgText);
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
}