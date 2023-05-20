using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;

namespace ImageViewer;

public interface IVersionGetter
{
    Task<Version?> GetLatestVersion();
    string GetReleaseUrl(Version newVersion);
}

public class GitHubVersionGetter : IVersionGetter
{
    public async Task<Version?> GetLatestVersion()
    {
        var clientHandler = new HttpClientHandler();
        clientHandler.AllowAutoRedirect = false;

        using var client = new HttpClient(clientHandler);
        var response = await client.GetAsync("https://github.com/LBognanni/ImageViewer/releases/latest", HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var latestVersion = response.Headers.Location.AbsolutePath.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Last();
        if(Version.TryParse(latestVersion, out var v))
        {
            return v;
        }

        return null;
    }

    public string GetReleaseUrl(Version newVersion) => 
        $"https://github.com/LBognanni/ImageViewer/releases/tag/{newVersion.Major}.{newVersion.Minor}.{newVersion.Build}";
}
