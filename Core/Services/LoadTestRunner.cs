using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net;

static class LoadTestRunner
{
    private static readonly HttpClientHandler _handler = new()
    {
        MaxConnectionsPerServer = 100,
        UseProxy = false,
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    };

    // TCP connection pool
    private static readonly ConcurrentDictionary<string, TcpClient> _tcpPool = new();
    private static readonly object _tcpPoolLock = new();

    // UDP client pool
    private static readonly ConcurrentDictionary<string, UdpClient> _udpPool = new();
    private static readonly object _udpPoolLock = new();

    // Buffer pool for TCP/UDP operations
    private static readonly ConcurrentBag<byte[]> _bufferPool = new();
    private const int BufferSize = 8192; // Optimal buffer size for most network operations

    private static byte[] GetBuffer()
    {
        if (_bufferPool.TryTake(out byte[]? buffer) && buffer != null)
        {
            return buffer;
        }
        return new byte[BufferSize];
    }

    private static void ReturnBuffer(byte[]? buffer)
    {
        if (buffer != null && buffer.Length == BufferSize)
        {
            _bufferPool.Add(buffer);
        }
    }

    private static TcpClient GetTcpClient(string host, int port)
    {
        var key = $"{host}:{port}";
        if (_tcpPool.TryGetValue(key, out TcpClient? client) && client != null)
        {
            if (client.Connected)
            {
                return client;
            }
            client.Dispose();
            _tcpPool.TryRemove(key, out _);
        }

        lock (_tcpPoolLock)
        {
            if (_tcpPool.TryGetValue(key, out client) && client != null && client.Connected)
            {
                return client;
            }

            client = new TcpClient
            {
                NoDelay = true, // Disable Nagle's algorithm for better performance
                SendTimeout = 5000,
                ReceiveTimeout = 5000,
                SendBufferSize = BufferSize,
                ReceiveBufferSize = BufferSize
            };

            _tcpPool.TryAdd(key, client);
            return client;
        }
    }

    private static UdpClient GetUdpClient(string host, int port)
    {
        var key = $"{host}:{port}";
        if (_udpPool.TryGetValue(key, out UdpClient? client) && client != null)
        {
            return client;
        }

        lock (_udpPoolLock)
        {
            if (_udpPool.TryGetValue(key, out client) && client != null)
            {
                return client;
            }

            client = new UdpClient
            {
                DontFragment = true, // Prevent IP fragmentation
                EnableBroadcast = false
            };

            _udpPool.TryAdd(key, client);
            return client;
        }
    }

    public static async Task RunLoadTest(object client, TestConfig config)
    {
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        Console.WriteLine("⚡ Starting load test...");

        var results = new ConcurrentBag<RequestResult>();
        var stopwatch = new Stopwatch();
        int completedRequests = 0;
        int inProgressRequests = 0;
        var progressLock = new object();

        var progressTimer = new Timer(_ =>
        {
            lock (progressLock)
            {
                double percentComplete = (double)completedRequests / config.NumberOfRequests * 100;
                int progressBarLength = 30;
                int filledLength = (int)Math.Floor(progressBarLength * completedRequests / (double)config.NumberOfRequests);

                string progressBar = "[" + new string('█', filledLength) + new string('░', progressBarLength - filledLength) + "]";

                Console.Write($"\r{progressBar} {percentComplete:F1}% ({completedRequests}/{config.NumberOfRequests}) - In progress: {inProgressRequests}    ");
            }
        }, null, 0, 200);

        stopwatch.Start();

        using (var semaphore = new SemaphoreSlim(config.NumberOfConcurrentRequests))
        {
            var tasks = new List<Task>();

            for (int i = 0; i < config.NumberOfRequests; i++)
            {
                await semaphore.WaitAsync();

                var task = Task.Run(async () =>
                {
                    Interlocked.Increment(ref inProgressRequests);

                    try
                    {
                        var result = await SendRequest(client, config);
                        results.Add(result);
                    }
                    finally
                    {
                        semaphore.Release();
                        Interlocked.Decrement(ref inProgressRequests);
                        Interlocked.Increment(ref completedRequests);

                        if (config.DelayBetweenRequestsMs > 0)
                        {
                            await Task.Delay(config.DelayBetweenRequestsMs);
                        }
                    }
                });

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        stopwatch.Stop();
        progressTimer.Dispose();

        // Cleanup connection pools
        foreach (var tcpClient in _tcpPool.Values)
        {
            tcpClient?.Dispose();
        }
        _tcpPool.Clear();

        foreach (var udpClient in _udpPool.Values)
        {
            udpClient?.Dispose();
        }
        _udpPool.Clear();

        Console.WriteLine();
        Console.WriteLine();

        ResultsReporter.DisplayResults(results, stopwatch.ElapsedMilliseconds, config.NumberOfRequests);
    }

    static async Task<RequestResult> SendRequest(object client, TestConfig config)
    {
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        return client switch
        {
            HttpClient httpClient => await SendHttpRequest(httpClient, config),
            TcpClient tcpClient => await SendTcpRequest(tcpClient, config),
            UdpClient udpClient => await SendUdpRequest(udpClient, config),
            _ => throw new ArgumentException($"Unsupported client type: {client.GetType().Name}")
        };
    }

    static async Task<RequestResult> SendHttpRequest(HttpClient client, TestConfig config)
    {
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var result = new RequestResult();
        var requestStopwatch = new Stopwatch();

        try
        {
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(config.Method), config.Url);

            if (!string.IsNullOrEmpty(config.Body) &&
                (config.Method == "POST" || config.Method == "PUT" || config.Method == "PATCH"))
            {
                request.Content = new StringContent(config.Body, Encoding.UTF8, "application/json");
            }

            requestStopwatch.Start();
            HttpResponseMessage response = await client.SendAsync(request);
            requestStopwatch.Stop();

            result.StatusCode = (int)response.StatusCode;
            result.IsSuccessful = response.IsSuccessStatusCode;
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;
            result.ContentLength = response.Content.Headers.ContentLength ?? 0;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"Request completed: Status={result.StatusCode}, Time={result.ResponseTime}ms");
            }
        }
        catch (HttpRequestException ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = $"Request failed: {ex.Message}";
            requestStopwatch.Stop();
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"Request failed: {ex.Message}");
            }
        }
        catch (TaskCanceledException)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = "Request timed out";
            requestStopwatch.Stop();
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;
            result.IsTimeout = true;

            if (config.VerboseOutput)
            {
                Console.WriteLine("Request timed out");
            }
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            requestStopwatch.Stop();
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }

        return result;
    }

    static async Task<RequestResult> SendTcpRequest(TcpClient client, TestConfig config)
    {
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var result = new RequestResult();
        var requestStopwatch = new Stopwatch();
        var buffer = GetBuffer();

        try
        {
            requestStopwatch.Start();
            
            // Get or create TCP client from pool
            var tcpClient = GetTcpClient(config.Url, config.Port);
            
            if (!tcpClient.Connected)
            {
                await tcpClient.ConnectAsync(config.Url, config.Port);
            }
            
            var networkStream = tcpClient.GetStream();
            
            // Send data if body is provided
            if (!string.IsNullOrEmpty(config.Body))
            {
                var data = Encoding.UTF8.GetBytes(config.Body);
                await networkStream.WriteAsync(data, 0, data.Length);
            }

            // Read response with timeout
            using var cts = new CancellationTokenSource(config.TimeoutMs);
            var readTask = networkStream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
            
            try
            {
                var bytesRead = await readTask;
                requestStopwatch.Stop();

                result.IsSuccessful = true;
                result.ResponseTime = requestStopwatch.ElapsedMilliseconds;
                result.ContentLength = bytesRead;

                if (config.VerboseOutput)
                {
                    Console.WriteLine($"TCP request completed: Time={result.ResponseTime}ms, Bytes received={bytesRead}");
                }
            }
            catch (OperationCanceledException)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = "TCP request timed out";
                result.IsTimeout = true;
                requestStopwatch.Stop();
                result.ResponseTime = requestStopwatch.ElapsedMilliseconds;

                if (config.VerboseOutput)
                {
                    Console.WriteLine("TCP request timed out");
                }
            }
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = $"TCP request failed: {ex.Message}";
            requestStopwatch.Stop();
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"TCP request failed: {ex.Message}");
            }
        }
        finally
        {
            ReturnBuffer(buffer);
        }

        return result;
    }

    static async Task<RequestResult> SendUdpRequest(UdpClient client, TestConfig config)
    {
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var result = new RequestResult();
        var requestStopwatch = new Stopwatch();
        var buffer = GetBuffer();

        try
        {
            requestStopwatch.Start();

            // Get or create UDP client from pool
            var udpClient = GetUdpClient(config.Url, config.Port);

            // Send data if body is provided
            if (!string.IsNullOrEmpty(config.Body))
            {
                var data = Encoding.UTF8.GetBytes(config.Body);
                await udpClient.SendAsync(data, data.Length, config.Url, config.Port);
            }

            // Wait for response with timeout
            using var cts = new CancellationTokenSource(config.TimeoutMs);
            try
            {
                var response = await udpClient.ReceiveAsync(cts.Token);
                requestStopwatch.Stop();

                result.IsSuccessful = true;
                result.ResponseTime = requestStopwatch.ElapsedMilliseconds;
                result.ContentLength = response.Buffer.Length;

                if (config.VerboseOutput)
                {
                    Console.WriteLine($"UDP request completed: Time={result.ResponseTime}ms, Bytes received={response.Buffer.Length}");
                }
            }
            catch (OperationCanceledException)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = "UDP request timed out";
                result.IsTimeout = true;
                requestStopwatch.Stop();
                result.ResponseTime = requestStopwatch.ElapsedMilliseconds;

                if (config.VerboseOutput)
                {
                    Console.WriteLine("UDP request timed out");
                }
            }
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = $"UDP request failed: {ex.Message}";
            requestStopwatch.Stop();
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"UDP request failed: {ex.Message}");
            }
        }
        finally
        {
            ReturnBuffer(buffer);
        }

        return result;
    }
}