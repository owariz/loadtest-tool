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

    public static async Task RunLoadTest(HttpClient client, TestConfig config)
    {
        Console.WriteLine("⚡ Starting load test...");

        var results = new ConcurrentBag<RequestResult>();
        var stopwatch = new Stopwatch();
        var requestCounters = new RequestCounters();
        var progressLock = new object();

        // Optimize progress reporting frequency
        var progressTimer = new Timer(_ =>
        {
            if (!Monitor.TryEnter(progressLock)) return;
            
            try
            {
                double percentComplete = (double)requestCounters.CompletedCount / config.NumberOfRequests * 100;
                int progressBarLength = 30;
                int filledLength = (int)Math.Floor(progressBarLength * requestCounters.CompletedCount / (double)config.NumberOfRequests);

                string progressBar = "[" + new string('█', filledLength) + new string('░', progressBarLength - filledLength) + "]";

                Console.Write($"\r{progressBar} {percentComplete:F1}% ({requestCounters.CompletedCount}/{config.NumberOfRequests}) - In progress: {requestCounters.InProgressCount}    ");
            }
            finally
            {
                Monitor.Exit(progressLock);
            }
        }, null, 0, 500);

        stopwatch.Start();

        // Pre-allocate tasks list with capacity
        var tasks = new List<Task>(config.NumberOfRequests);
        using var semaphore = new SemaphoreSlim(config.NumberOfConcurrentRequests);

        // Create all tasks upfront
        for (int i = 0; i < config.NumberOfRequests; i++)
        {
            tasks.Add(ExecuteRequestAsync(client, config, semaphore, results, requestCounters));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();
        progressTimer.Dispose();

        Console.WriteLine();
        Console.WriteLine();

        ResultsReporter.DisplayResults(results, stopwatch.ElapsedMilliseconds, config.NumberOfRequests);
    }

    private class RequestCounters
    {
        private long _completedCount;
        private long _inProgressCount;

        public long CompletedCount => Interlocked.Read(ref _completedCount);
        public long InProgressCount => Interlocked.Read(ref _inProgressCount);

        public void IncrementCompleted() => Interlocked.Increment(ref _completedCount);
        public void IncrementInProgress() => Interlocked.Increment(ref _inProgressCount);
        public void DecrementInProgress() => Interlocked.Decrement(ref _inProgressCount);
    }

    private static async Task ExecuteRequestAsync(
        HttpClient client, 
        TestConfig config, 
        SemaphoreSlim semaphore,
        ConcurrentBag<RequestResult> results,
        RequestCounters counters)
    {
        await semaphore.WaitAsync();

        try
        {
            counters.IncrementInProgress();

            var result = await SendRequestAsync(client, config);
            results.Add(result);
        }
        finally
        {
            semaphore.Release();
            counters.DecrementInProgress();
            counters.IncrementCompleted();

            if (config.DelayBetweenRequestsMs > 0)
            {
                await Task.Delay(config.DelayBetweenRequestsMs);
            }
        }
    }

    private static async Task<RequestResult> SendRequestAsync(HttpClient client, TestConfig config)
    {
        return config.Protocol switch
        {
            TestProtocol.Http => await SendHttpRequest(client, config),
            TestProtocol.Tcp => await SendTcpRequest(config),
            TestProtocol.Udp => await SendUdpRequest(config),
            _ => throw new ArgumentException($"Unsupported protocol: {config.Protocol}")
        };
    }

    private static async Task<RequestResult> SendHttpRequest(HttpClient client, TestConfig config)
    {
        var result = new RequestResult();
        var requestStopwatch = new Stopwatch();

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(config.Method), config.Url);
            
            // Add common headers for better performance
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Connection.Add("keep-alive");

            if (!string.IsNullOrEmpty(config.Body) &&
                (config.Method == "POST" || config.Method == "PUT" || config.Method == "PATCH"))
            {
                request.Content = new StringContent(config.Body, Encoding.UTF8, "application/json");
            }

            requestStopwatch.Start();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
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

    private static async Task<RequestResult> SendTcpRequest(TestConfig config)
    {
        var result = new RequestResult();
        var requestStopwatch = new Stopwatch();
        var client = new TcpClient();

        try
        {
            requestStopwatch.Start();
            
            // Connect to the server
            await client.ConnectAsync(config.Url, config.Port);
            
            var networkStream = client.GetStream();
            
            // Send data if body is provided
            if (!string.IsNullOrEmpty(config.Body))
            {
                var data = Encoding.UTF8.GetBytes(config.Body);
                await networkStream.WriteAsync(data, 0, data.Length);
            }

            // Wait for response
            var buffer = new byte[4096];
            var bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);

            requestStopwatch.Stop();

            result.IsSuccessful = true;
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;
            result.ContentLength = bytesRead;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"TCP request completed: Time={result.ResponseTime}ms, Bytes received={bytesRead}");
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
            client.Dispose();
        }

        return result;
    }

    private static async Task<RequestResult> SendUdpRequest(TestConfig config)
    {
        var result = new RequestResult();
        var requestStopwatch = new Stopwatch();
        var client = new UdpClient();

        try
        {
            requestStopwatch.Start();

            // Send data if body is provided
            if (!string.IsNullOrEmpty(config.Body))
            {
                var data = Encoding.UTF8.GetBytes(config.Body);
                await client.SendAsync(data, data.Length);
            }

            // Wait for response
            var response = await client.ReceiveAsync(CancellationToken.None);

            requestStopwatch.Stop();

            result.IsSuccessful = true;
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;
            result.ContentLength = response.Buffer.Length;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"UDP request completed: Time={result.ResponseTime}ms, Bytes received={response.Buffer.Length}");
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
            client.Dispose();
        }

        return result;
    }
}