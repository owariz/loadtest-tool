using System.Net;

static class HttpClientFactory
{
    public static HttpClient CreateHttpClient(TestConfig config)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        client.DefaultRequestHeaders.Add("User-Agent", "PowerLoadTest/1.0");
        foreach (var header in config.Headers)
        {
            client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        return client;
    }
}