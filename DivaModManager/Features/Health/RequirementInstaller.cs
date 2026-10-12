using DivaModManager.Common.Helpers;
using DivaModManager.Features.Debug;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DivaModManager.Features.Health
{
    /// <summary>
    /// Installs a required mod. GameBanana / DivaModArchive mods are downloaded the same way as one-click install
    /// (starting DivaModManager with "-download divamodmanager:..."). Anything else opens its page
    /// </summary>
    public static class RequirementInstaller
    {
        public static async Task InstallAsync(string url, string name)
        {
            var gb = Regex.Match(url ?? "", @"gamebanana\.com/mods/(\d+)", RegexOptions.IgnoreCase);
            var dma = Regex.Match(url ?? "", @"divamodarchive\.com/posts?/(\d+)", RegexOptions.IgnoreCase);
            try
            {
                if (gb.Success)
                {
                    var fileId = await NewestGameBananaFileAsync(int.Parse(gb.Groups[1].Value));
                    if (fileId != null)
                    {
                        StartDownload($"divamodmanager:https://gamebanana.com/mmdl/{fileId},Mod,{gb.Groups[1].Value}");
                        Logger.WriteLine($"Installing {name} from GameBanana...", LoggerType.Info);
                        return;
                    }
                }
                else if (dma.Success)
                {
                    StartDownload($"divamodmanager:dma/{dma.Groups[1].Value}");
                    Logger.WriteLine($"Installing {name} from DivaModArchive...", LoggerType.Info);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Couldn't start the download of {name}: {ex.Message}", LoggerType.Warning);
            }
            // Open the page when it can't be downloaded automatically
            if (!string.IsNullOrEmpty(url))
            {
                Logger.WriteLine($"Opening the page of {name}: {url}", LoggerType.Info);
                ProcessHelper.TryStartProcess(url);
            }
        }

        private static void StartDownload(string protocolLine)
        {
            var exe = Environment.ProcessPath;
            Process.Start(new ProcessStartInfo(exe, $"-download \"{protocolLine}\"") { UseShellExecute = false, WorkingDirectory = Global.assemblyLocation });
        }

        /// <summary>ID of the newest file of a GameBanana mod</summary>
        private static async Task<string> NewestGameBananaFileAsync(int modId)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DivaModManager-by-Hanserm");
            using var doc = JsonDocument.Parse(await client.GetStringAsync($"https://gamebanana.com/apiv11/Mod/{modId}?_csvProperties=_aFiles"));
            if (!doc.RootElement.TryGetProperty("_aFiles", out var files) || files.ValueKind != JsonValueKind.Array) return null;
            // Don't use empty files such as "download_from_alternate_file_source" (open the page instead)
            var newest = files.EnumerateArray()
                .Where(f => f.TryGetProperty("_nFilesize", out var size) && size.GetInt64() > 10_000)
                .OrderByDescending(f => f.TryGetProperty("_tsDateAdded", out var t) ? t.GetInt64() : 0)
                .FirstOrDefault();
            return newest.ValueKind == JsonValueKind.Object && newest.TryGetProperty("_idRow", out var id) ? id.GetRawText() : null;
        }
    }
}
