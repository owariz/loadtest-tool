class RequestResult
{
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public long ResponseTime { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsTimeout { get; set; }
    public long ContentLength { get; set; }
}