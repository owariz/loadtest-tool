using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

static class LoadTestRunner
{
    public static async Task RunLoadTest(HttpClient client, TestConfig config)
    {
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

        Console.WriteLine();
        Console.WriteLine();

        ResultsReporter.DisplayResults(results, stopwatch.ElapsedMilliseconds, config.NumberOfRequests);
    }

    static async Task<RequestResult> SendRequest(HttpClient client, TestConfig config)
    {
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
}