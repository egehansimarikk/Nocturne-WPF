using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nocturne
{
    public sealed class ReleaseUpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestTag { get; set; } = "";
        public string ReleaseUrl { get; set; } = "https://github.com/egehansimarikk/Nocturne/releases/latest";
    }

    public static class UpdateChecker
    {
        private const string ApiUrl = "https://api.github.com/repos/egehansimarikk/Nocturne/releases/latest";

        public static async Task<ReleaseUpdateInfo> CheckForUpdatesAsync()
        {
            var result = new ReleaseUpdateInfo();
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Nocturne-Desktop-Client");

                string json = await http.GetStringAsync(ApiUrl);
                using var doc = JsonDocument.Parse(json);

                string tag = doc.RootElement.TryGetProperty("tag_name", out var tagProp)
                    ? (tagProp.GetString() ?? "")
                    : "";
                string htmlUrl = doc.RootElement.TryGetProperty("html_url", out var urlProp)
                    ? (urlProp.GetString() ?? result.ReleaseUrl)
                    : result.ReleaseUrl;

                if (string.IsNullOrWhiteSpace(tag))
                    return result;

                string cleanRemote = tag.Trim().TrimStart('v', 'V');
                Version currentVer = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
                Version normalizedCurrent = new(currentVer.Major, currentVer.Minor, Math.Max(0, currentVer.Build));

                if (Version.TryParse(cleanRemote, out var remoteVer))
                {
                    Version normalizedRemote = new(remoteVer.Major, remoteVer.Minor, Math.Max(0, remoteVer.Build));
                    if (normalizedRemote > normalizedCurrent)
                    {
                        result.IsUpdateAvailable = true;
                        result.LatestTag = tag;
                        result.ReleaseUrl = htmlUrl;
                    }
                }
            }
            catch { }

            return result;
        }
    }
}