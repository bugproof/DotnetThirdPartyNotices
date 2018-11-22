using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DotnetThirdPartyNotices
{
    internal class GithubService : IDisposable
    {
        private readonly HttpClient _httpClient;

        public GithubService()
        {
            _httpClient = new HttpClient {BaseAddress = new Uri("https://api.github.com")};
            // https://developer.github.com/v3/#user-agent-required
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DotnetLicense");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        public async Task<string> GetLicenseContentFromId(string licenseId)
        {
            var json = await _httpClient.GetStringAsync($"licenses/{licenseId}");
            var jObject = JObject.Parse(json);
            return jObject["body"].Value<string>();
        }

        public async Task<string> GetLicenseContentFromRepositoryPath(string repositoryPath)
        {
            repositoryPath = repositoryPath.TrimEnd('/');
            var json = await _httpClient.GetStringAsync($"repos{repositoryPath}/license");
            var jObject = JObject.Parse(json);

            var encoding = jObject["encoding"].Value<string>();
            var content = jObject["content"].Value<string>();

            if (encoding != "base64") return content;

            var bytes = Convert.FromBase64String(content);
            return Encoding.UTF8.GetString(bytes);
        }

    }
}
