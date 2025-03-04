public enum TestProtocol
{
    Http,
    Tcp,
    Udp
}

public class TestConfig
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string Body { get; set; } = string.Empty;
    public int NumberOfRequests { get; set; }
    public int NumberOfConcurrentRequests { get; set; }
    public int TimeoutSeconds { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    public int DelayBetweenRequestsMs { get; set; }
    public bool VerboseOutput { get; set; }
    public TestProtocol Protocol { get; set; } = TestProtocol.Http;
    public int Port { get; set; }
    public int TimeoutMs { get; set; } = 30000;
}