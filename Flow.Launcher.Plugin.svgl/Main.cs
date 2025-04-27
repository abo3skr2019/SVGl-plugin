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

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var search = query.Search?.Trim();
            if (string.IsNullOrEmpty(search)) return results;
            var requestUrl = $"https://api.svgl.app?search={Uri.EscapeDataString(search)}";
            var json = _httpClient.GetStringAsync(requestUrl).GetAwaiter().GetResult();
            List<SvglApiResult> items;
            try { items = JsonConvert.DeserializeObject<List<SvglApiResult>>(json); }
            catch { return results; }
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