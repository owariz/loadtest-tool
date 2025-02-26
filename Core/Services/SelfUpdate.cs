using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

class SelfUpdate
{
    private static readonly HttpClient client = new HttpClient();
    static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString().TrimEnd(new char[] { '.', '0' }) ?? "0.0.0";

    const string GitHubApiUrl = "https://api.github.com/repos/owariz/loadtest-tool/releases/latest";

    public async static Task CheckForUpdates()
    {
        try
        {
            Console.WriteLine("\n🔍 Checking for updates...");
            var latestInfo = await GetLatestReleaseInfo();

            Console.WriteLine("\n" + DrawTable(CurrentVersion, latestInfo.version ?? "Unknown"));

            if (latestInfo.version != null && new Version(latestInfo.version) > new Version(CurrentVersion))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n🚀 New version available!");
                Console.WriteLine("Do you want to update? (Y/N)");
                Console.ResetColor();

                if (Console.ReadLine()?.ToLower() == "y")
                {
                    await DownloadAndInstallUpdate(latestInfo.downloadUrl?.ToString() ?? "");
                }
                else
                {
                    Console.WriteLine("Skipping update...");
                }
            }
            else
            {
                Console.WriteLine("\n✅ You are using the latest version.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for updates: {ex.Message}");
        }
    }

    private static async Task<(string? version, string? downloadUrl)> GetLatestReleaseInfo()
    {
        try
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
            var response = await client.GetStringAsync(GitHubApiUrl);
            using JsonDocument doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagNameProp))
            {
                Console.WriteLine("API response does not contain the tag_name property.");
                return (null, null);
            }

            string latestVersion = tagNameProp.GetString() ?? "0.0.0";
            latestVersion = latestVersion.TrimStart('v');

            var assets = root.GetProperty("assets").EnumerateArray();
            var asset = assets.FirstOrDefault(a => a.GetProperty("name").GetString()?.EndsWith(".zip") == true);

            if (asset.ValueKind == JsonValueKind.Undefined)
            {
                return (latestVersion, null);
            }

            string downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";

            return (latestVersion, downloadUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving release info: {ex.Message}");
            return (null, null);
        }
    }

    static async Task DownloadAndInstallUpdate(string downloadUrl)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "update.zip");
        string extractPath = Path.Combine(Path.GetTempPath(), "update");

        using var client = new HttpClient();
        Console.WriteLine("Downloading update...");
        var data = await client.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(tempFile, data);

        Console.WriteLine("Extracting update...");
        if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
        ZipFile.ExtractToDirectory(tempFile, extractPath);

        string currentPath = AppDomain.CurrentDomain.BaseDirectory;
        foreach (var file in Directory.GetFiles(extractPath))
        {
            File.Copy(file, Path.Combine(currentPath, Path.GetFileName(file)), true);
        }

        Console.WriteLine("Update completed.");
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(currentPath, "LoadTest.exe"),
            UseShellExecute = true
        });

        Environment.Exit(0);
    }

    static string DrawTable(string currentVersion, string latestVersion)
    {
        string separator = "├───────────────┼───────────────┤";
        string header = "│ Current Ver.  │ Latest Ver.   │";
        string row = $"│ {currentVersion.PadRight(13)} │ {latestVersion.PadRight(13)} │";

        return $@"
┌───────────────┬───────────────┐
{header}
{separator}
{row}
└───────────────┴───────────────┘";
    }
}