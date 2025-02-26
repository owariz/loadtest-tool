class TestConfig
{
    public string Url { get; set; } = string.Empty;
    public int NumberOfRequests { get; set; }
    public int NumberOfConcurrentRequests { get; set; }
    public int TimeoutSeconds { get; set; }
    public string Method { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    public string Body { get; set; } = string.Empty;
    public int DelayBetweenRequestsMs { get; set; }
    public bool VerboseOutput { get; set; }
}