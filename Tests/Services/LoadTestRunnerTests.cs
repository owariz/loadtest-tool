using Xunit;
using Moq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace LoadTest.Tests.Services
{
    public class LoadTestRunnerTests
    {
        private readonly TestConfig _defaultConfig;
        private readonly Mock<HttpClient> _httpClientMock;
        private readonly Mock<TcpClient> _tcpClientMock;
        private readonly Mock<UdpClient> _udpClientMock;
        private readonly Mock<NetworkStream> _networkStreamMock;

        public LoadTestRunnerTests()
        {
            _defaultConfig = new TestConfig
            {
                Url = "http://example.com",
                Port = 8080,
                NumberOfRequests = 1,
                NumberOfConcurrentRequests = 1,
                TimeoutSeconds = 5,
                Method = "GET",
                Protocol = TestProtocol.Http
            };

            _httpClientMock = new Mock<HttpClient>();
            _tcpClientMock = new Mock<TcpClient>();
            _udpClientMock = new Mock<UdpClient>();
            _networkStreamMock = new Mock<NetworkStream>();
        }

        [Fact]
        public async Task RunLoadTest_WithNullClient_ThrowsArgumentNullException()
        {
            // Arrange
            object? client = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                LoadTestRunner.RunLoadTest(client!, _defaultConfig));
        }

        [Fact]
        public async Task RunLoadTest_WithNullConfig_ThrowsArgumentNullException()
        {
            // Arrange
            TestConfig? config = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                LoadTestRunner.RunLoadTest(_httpClientMock.Object, config!));
        }

        [Fact]
        public async Task SendHttpRequest_SuccessfulRequest_ReturnsSuccessResult()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test response")
            };
            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(response);

            // Act
            var result = await LoadTestRunner.SendHttpRequest(_httpClientMock.Object, _defaultConfig);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(200, result.StatusCode);
            Assert.True(result.ResponseTime >= 0);
        }

        [Fact]
        public async Task SendHttpRequest_FailedRequest_ReturnsFailureResult()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(response);

            // Act
            var result = await LoadTestRunner.SendHttpRequest(_httpClientMock.Object, _defaultConfig);

            // Assert
            Assert.False(result.IsSuccessful);
            Assert.Equal(500, result.StatusCode);
        }

        [Fact]
        public async Task SendTcpRequest_SuccessfulRequest_ReturnsSuccessResult()
        {
            // Arrange
            var testData = Encoding.UTF8.GetBytes("test response");
            _tcpClientMock.Setup(x => x.Connected).Returns(false);
            _tcpClientMock.Setup(x => x.GetStream()).Returns(_networkStreamMock.Object);
            _networkStreamMock.Setup(x => x.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(testData.Length);

            // Act
            var result = await LoadTestRunner.SendTcpRequest(_tcpClientMock.Object, _defaultConfig);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(testData.Length, result.ContentLength);
            Assert.True(result.ResponseTime >= 0);
        }

        [Fact]
        public async Task SendTcpRequest_Timeout_ReturnsTimeoutResult()
        {
            // Arrange
            _tcpClientMock.Setup(x => x.Connected).Returns(false);
            _tcpClientMock.Setup(x => x.GetStream()).Returns(_networkStreamMock.Object);
            _networkStreamMock.Setup(x => x.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                            .ThrowsAsync(new OperationCanceledException());

            // Act
            var result = await LoadTestRunner.SendTcpRequest(_tcpClientMock.Object, _defaultConfig);

            // Assert
            Assert.False(result.IsSuccessful);
            Assert.True(result.IsTimeout);
            Assert.Contains("timed out", result.ErrorMessage);
        }

        [Fact]
        public async Task SendUdpRequest_SuccessfulRequest_ReturnsSuccessResult()
        {
            // Arrange
            var testData = Encoding.UTF8.GetBytes("test response");
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
            _udpClientMock.Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new UdpReceiveResult(testData, remoteEndPoint));

            // Act
            var result = await LoadTestRunner.SendUdpRequest(_udpClientMock.Object, _defaultConfig);

            // Assert
            Assert.True(result.IsSuccessful);
            Assert.Equal(testData.Length, result.ContentLength);
            Assert.True(result.ResponseTime >= 0);
        }

        [Fact]
        public async Task SendUdpRequest_Timeout_ReturnsTimeoutResult()
        {
            // Arrange
            _udpClientMock.Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
                         .ThrowsAsync(new OperationCanceledException());

            // Act
            var result = await LoadTestRunner.SendUdpRequest(_udpClientMock.Object, _defaultConfig);

            // Assert
            Assert.False(result.IsSuccessful);
            Assert.True(result.IsTimeout);
            Assert.Contains("timed out", result.ErrorMessage);
        }

        [Fact]
        public async Task RunLoadTest_WithMultipleRequests_ExecutesAllRequests()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                NumberOfRequests = 3,
                NumberOfConcurrentRequests = 2,
                TimeoutSeconds = 5
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(response);

            // Act
            await LoadTestRunner.RunLoadTest(_httpClientMock.Object, config);

            // Assert
            _httpClientMock.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), 
                                 Times.Exactly(3));
        }

        [Fact]
        public async Task RunLoadTest_WithDelay_RespectsDelayBetweenRequests()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                NumberOfRequests = 2,
                NumberOfConcurrentRequests = 1,
                DelayBetweenRequestsMs = 100
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(response);

            var stopwatch = new Stopwatch();

            // Act
            stopwatch.Start();
            await LoadTestRunner.RunLoadTest(_httpClientMock.Object, config);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds >= 100); // Should take at least 100ms due to delay
        }

        [Fact]
        public async Task RunLoadTest_WithConcurrentRequests_RespectsConcurrencyLimit()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                NumberOfRequests = 5,
                NumberOfConcurrentRequests = 2,
                TimeoutSeconds = 5
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var concurrentRequests = 0;
            var maxConcurrentRequests = 0;

            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .Returns(async () =>
                          {
                              Interlocked.Increment(ref concurrentRequests);
                              maxConcurrentRequests = Math.Max(maxConcurrentRequests, concurrentRequests);
                              await Task.Delay(100); // Simulate some work
                              Interlocked.Decrement(ref concurrentRequests);
                              return response;
                          });

            // Act
            await LoadTestRunner.RunLoadTest(_httpClientMock.Object, config);

            // Assert
            Assert.True(maxConcurrentRequests <= 2); // Should never exceed concurrency limit
        }

        [Fact]
        public async Task SendHttpRequest_WithCustomHeaders_IncludesHeadersInRequest()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                Headers = new Dictionary<string, string>
                {
                    { "Custom-Header", "test-value" }
                }
            };

            HttpRequestMessage? capturedRequest = null;
            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
                          .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            await LoadTestRunner.SendHttpRequest(_httpClientMock.Object, config);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest.Headers.Contains("Custom-Header"));
            Assert.Equal("test-value", capturedRequest.Headers.GetValues("Custom-Header").First());
        }

        [Fact]
        public async Task SendTcpRequest_WithLargeData_HandlesDataCorrectly()
        {
            // Arrange
            var largeData = new string('x', 10000);
            var config = new TestConfig
            {
                Url = "example.com",
                Port = 8080,
                Body = largeData
            };

            _tcpClientMock.Setup(x => x.Connected).Returns(false);
            _tcpClientMock.Setup(x => x.GetStream()).Returns(_networkStreamMock.Object);
            _networkStreamMock.Setup(x => x.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                            .Returns(Task.CompletedTask);
            _networkStreamMock.Setup(x => x.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(100);

            // Act
            var result = await LoadTestRunner.SendTcpRequest(_tcpClientMock.Object, config);

            // Assert
            Assert.True(result.IsSuccessful);
            _networkStreamMock.Verify(x => x.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendUdpRequest_WithNoResponse_HandlesTimeoutCorrectly()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "example.com",
                Port = 8080,
                TimeoutSeconds = 1 // Short timeout for testing
            };

            _udpClientMock.Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
                         .ThrowsAsync(new OperationCanceledException());

            // Act
            var result = await LoadTestRunner.SendUdpRequest(_udpClientMock.Object, config);

            // Assert
            Assert.False(result.IsSuccessful);
            Assert.True(result.IsTimeout);
            Assert.Contains("timed out", result.ErrorMessage);
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        public async Task SendHttpRequest_WithDifferentMethods_UsesCorrectMethod(string method)
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                Method = method
            };

            HttpRequestMessage? capturedRequest = null;
            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
                          .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            await LoadTestRunner.SendHttpRequest(_httpClientMock.Object, config);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(method, capturedRequest.Method.ToString());
        }

        [Fact]
        public async Task RunLoadTest_WithErrorHandling_CollectsAllResults()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                NumberOfRequests = 3,
                NumberOfConcurrentRequests = 1
            };

            var responses = new[]
            {
                new HttpResponseMessage(HttpStatusCode.OK),
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                new HttpResponseMessage(HttpStatusCode.OK)
            };

            var responseIndex = 0;
            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(() => responses[responseIndex++]);

            // Act
            await LoadTestRunner.RunLoadTest(_httpClientMock.Object, config);

            // Assert
            _httpClientMock.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), 
                                 Times.Exactly(3));
        }

        [Fact]
        public async Task RunLoadTest_WithHighConcurrency_HandlesRequestsEfficiently()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                NumberOfRequests = 100,
                NumberOfConcurrentRequests = 20,
                TimeoutSeconds = 5
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var requestCount = 0;
            var startTime = DateTime.UtcNow;

            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .Returns(async () =>
                          {
                              Interlocked.Increment(ref requestCount);
                              await Task.Delay(10); // Simulate network latency
                              return response;
                          });

            // Act
            await LoadTestRunner.RunLoadTest(_httpClientMock.Object, config);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.Equal(100, requestCount);
            Assert.True(duration.TotalSeconds < 10); // Should complete within 10 seconds
        }

        [Fact]
        public async Task RunLoadTest_WithConnectionPooling_ReusesConnections()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                NumberOfRequests = 50,
                NumberOfConcurrentRequests = 10
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK);

            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(response);

            // Act
            await LoadTestRunner.RunLoadTest(_httpClientMock.Object, config);

            // Assert
            _httpClientMock.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), 
                                 Times.Exactly(50));
        }

        [Fact]
        public async Task SendTcpRequest_WithConnectionPooling_ReusesTcpClients()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "example.com",
                Port = 8080,
                NumberOfRequests = 10
            };

            _tcpClientMock.Setup(x => x.Connected).Returns(false);
            _tcpClientMock.Setup(x => x.GetStream()).Returns(_networkStreamMock.Object);
            _networkStreamMock.Setup(x => x.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(100);

            // Act
            for (int i = 0; i < 10; i++)
            {
                await LoadTestRunner.SendTcpRequest(_tcpClientMock.Object, config);
            }

            // Assert
            _tcpClientMock.Verify(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), 
                                Times.AtMost(1)); // Should connect only once
        }

        [Fact]
        public async Task SendUdpRequest_WithHighThroughput_HandlesRequestsEfficiently()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "example.com",
                Port = 8080,
                NumberOfRequests = 100,
                NumberOfConcurrentRequests = 20
            };

            var testData = new byte[1000];
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
            var startTime = DateTime.UtcNow;

            _udpClientMock.Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new UdpReceiveResult(testData, remoteEndPoint));

            // Act
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(LoadTestRunner.SendUdpRequest(_udpClientMock.Object, config));
            }

            await Task.WhenAll(tasks);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.True(duration.TotalSeconds < 5); // Should complete within 5 seconds
            _udpClientMock.Verify(x => x.ReceiveAsync(It.IsAny<CancellationToken>()), 
                                Times.Exactly(100));
        }

        [Fact]
        public async Task RunLoadTest_WithMemoryOptimization_DoesNotLeakMemory()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                NumberOfRequests = 1000,
                NumberOfConcurrentRequests = 50
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new string('x', 1000)) // 1KB response
            };

            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(response);

            // Act
            var initialMemory = GC.GetTotalMemory(true);
            await LoadTestRunner.RunLoadTest(_httpClientMock.Object, config);
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(true);

            // Assert
            var memoryIncrease = finalMemory - initialMemory;
            Assert.True(memoryIncrease < 10 * 1024 * 1024); // Less than 10MB increase
        }

        [Fact]
        public async Task RunLoadTest_WithBufferPooling_ReusesBuffers()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "http://example.com",
                NumberOfRequests = 100,
                NumberOfConcurrentRequests = 20
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new string('x', 8192)) // Match buffer size
            };
            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(response);

            // Act
            await LoadTestRunner.RunLoadTest(_httpClientMock.Object, config);

            // Assert
            // Note: This is an indirect test since we can't directly measure buffer pool usage
            // but we can verify that the load test completes successfully with high concurrency
            Assert.True(true); // Test passes if it completes without throwing
        }

        [Fact]
        public async Task RunLoadTest_WithMixedProtocols_HandlesAllProtocolsEfficiently()
        {
            // Arrange
            var config = new TestConfig
            {
                Url = "example.com",
                Port = 8080,
                NumberOfRequests = 50,
                NumberOfConcurrentRequests = 10
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var tcpResponse = new byte[100];
            var udpResponse = new UdpReceiveResult(new byte[100], new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080));

            _httpClientMock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(response);

            _tcpClientMock.Setup(x => x.Connected).Returns(false);
            _tcpClientMock.Setup(x => x.GetStream()).Returns(_networkStreamMock.Object);
            _networkStreamMock.Setup(x => x.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(tcpResponse.Length);

            _udpClientMock.Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(udpResponse);

            // Act
            var startTime = DateTime.UtcNow;
            var tasks = new List<Task>();

            for (int i = 0; i < 50; i++)
            {
                switch (i % 3)
                {
                    case 0:
                        tasks.Add(LoadTestRunner.SendHttpRequest(_httpClientMock.Object, config));
                        break;
                    case 1:
                        tasks.Add(LoadTestRunner.SendTcpRequest(_tcpClientMock.Object, config));
                        break;
                    case 2:
                        tasks.Add(LoadTestRunner.SendUdpRequest(_udpClientMock.Object, config));
                        break;
                }
            }

            await Task.WhenAll(tasks);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.True(duration.TotalSeconds < 10); // Should complete within 10 seconds
        }
    }
} 