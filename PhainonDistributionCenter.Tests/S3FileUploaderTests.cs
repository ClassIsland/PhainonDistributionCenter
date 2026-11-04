using System.Net;
using System.Net.Sockets;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using PhainonDistributionCenter.Client.Helpers;
using Xunit;

namespace PhainonDistributionCenter.Tests;

public class S3FileUploaderTests
{
    [Fact]
    public async Task FirstAttemptSucceedsWithoutWaiting()
    {
        using var client = new StubS3Client((_, _) => Task.FromResult(new PutObjectResponse()));
        var waits = new List<TimeSpan>();
        var uploader = CreateUploader(client, waits);

        await uploader.UploadAsync("bucket", "key", "file.zip");

        Assert.Single(client.Requests);
        Assert.Empty(waits);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task IncompleteBodyRetriesAtExponentialIntervalsAndPreservesLastFailure(int failures)
    {
        var attempts = 0;
        var errors = new List<AmazonS3Exception>();
        using var client = new StubS3Client((_, _) =>
        {
            if (++attempts > failures) return Task.FromResult(new PutObjectResponse());
            var error = ServiceError(HttpStatusCode.BadRequest, "IncompleteBody",
                "You did not provide the number of bytes specified by the Content-Length HTTP header.");
            errors.Add(error);
            return Task.FromException<PutObjectResponse>(error);
        });
        var waits = new List<TimeSpan>();
        var messages = new List<string>();
        var uploader = CreateUploader(client, waits, messages.Add);

        if (failures == 6)
        {
            var thrown = await Assert.ThrowsAsync<AmazonS3Exception>(() =>
                uploader.UploadAsync("bucket", "key", "file.zip"));
            Assert.Same(errors[^1], thrown);
            Assert.Contains("file.zip", messages[^1]);
            Assert.Contains("after 5 retries", messages[^1]);
        }
        else
        {
            await uploader.UploadAsync("bucket", "key", "file.zip");
        }

        Assert.Equal(Math.Min(failures + 1, 6), attempts);
        Assert.Equal(new[] { 1, 2, 4, 8, 16 }.Take(Math.Min(failures, 5)),
            waits.Select(wait => (int)wait.TotalSeconds));
        Assert.Contains("Retry 1/5 in 1 seconds", messages[0]);
        Assert.All(client.Requests, request =>
        {
            Assert.Equal("bucket", request.BucketName);
            Assert.Equal("key", request.Key);
            Assert.Equal("file.zip", request.FilePath);
        });
        Assert.Equal(attempts, client.Requests.Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(40)]
    public async Task ConfiguredRetryLimitCapsAttemptsAndDelay(int maxRetries)
    {
        var error = ServiceError(HttpStatusCode.ServiceUnavailable);
        using var client = new StubS3Client((_, _) => Task.FromException<PutObjectResponse>(error));
        var waits = new List<TimeSpan>();
        var messages = new List<string>();
        var uploader = new S3FileUploader(client,
            (wait, _) => { waits.Add(wait); return Task.CompletedTask; }, messages.Add, maxRetries);

        var thrown = await Assert.ThrowsAsync<AmazonS3Exception>(() =>
            uploader.UploadAsync("bucket", "key", "file.zip"));

        Assert.Same(error, thrown);
        Assert.Equal(maxRetries + 1, client.Requests.Count);
        Assert.Equal(Enumerable.Range(0, maxRetries).Select(retry =>
                retry >= 8 ? 256 : (int)Math.Pow(2, retry)),
            waits.Select(wait => (int)wait.TotalSeconds));
        Assert.Contains($"after {maxRetries} retries", messages[^1]);
    }

    [Fact]
    public void NegativeRetryLimitIsRejected()
    {
        using var client = new StubS3Client((_, _) => Task.FromResult(new PutObjectResponse()));

        Assert.Throws<ArgumentOutOfRangeException>(() => new S3FileUploader(client, maxRetries: -1));
    }

    public static IEnumerable<object[]> TransientErrors()
    {
        yield return [ServiceError(HttpStatusCode.RequestTimeout)];
        yield return [ServiceError(HttpStatusCode.TooManyRequests)];
        yield return [ServiceError(HttpStatusCode.InternalServerError)];
        yield return [ServiceError(HttpStatusCode.BadGateway)];
        yield return [ServiceError(HttpStatusCode.ServiceUnavailable)];
        yield return [ServiceError(HttpStatusCode.GatewayTimeout)];
        yield return [ServiceError(HttpStatusCode.BadRequest, "RequestTimeout")];
        yield return [ServiceError(HttpStatusCode.BadRequest, "SlowDown")];
        yield return [ServiceError(HttpStatusCode.BadRequest, "Throttling")];
        yield return [new HttpRequestException("Connection interrupted")];
        yield return [new HttpRequestException("Throttled", null, HttpStatusCode.TooManyRequests)];
        yield return [new SocketException((int)SocketError.ConnectionReset)];
        yield return [new TimeoutException()];
        yield return [new TaskCanceledException("Request timeout without caller cancellation")];
        yield return [new AmazonClientException("Network failure", new IOException("Stream interrupted",
            new SocketException((int)SocketError.ConnectionReset)))];
        yield return [new WebException("Connection interrupted", WebExceptionStatus.ConnectionClosed)];
    }

    [Theory]
    [MemberData(nameof(TransientErrors))]
    public async Task TransientErrorsCanRecover(Exception error)
    {
        var attempts = 0;
        using var client = new StubS3Client((_, _) => ++attempts == 1
            ? Task.FromException<PutObjectResponse>(error)
            : Task.FromResult(new PutObjectResponse()));
        var waits = new List<TimeSpan>();

        await CreateUploader(client, waits).UploadAsync("bucket", "key", "file.zip");

        Assert.Equal(2, attempts);
        Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(waits));
    }

    public static IEnumerable<object[]> PermanentErrors()
    {
        yield return [ServiceError(HttpStatusCode.BadRequest, "InvalidArgument")];
        yield return [ServiceError(HttpStatusCode.Forbidden, "AccessDenied")];
        yield return [ServiceError(HttpStatusCode.NotFound, "NoSuchBucket")];
        yield return [new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden)];
        yield return [new FileNotFoundException("Missing file")];
        yield return [new IOException("Local file read failed")];
        yield return [new UnauthorizedAccessException()];
        yield return [new ArgumentException("Invalid configuration")];
    }

    [Theory]
    [MemberData(nameof(PermanentErrors))]
    public async Task PermanentErrorsFailWithoutRetry(Exception error)
    {
        using var client = new StubS3Client((_, _) => Task.FromException<PutObjectResponse>(error));
        var waits = new List<TimeSpan>();

        var thrown = await Record.ExceptionAsync(() =>
            CreateUploader(client, waits).UploadAsync("bucket", "key", "file.zip"));

        Assert.Same(error, thrown);
        Assert.Single(client.Requests);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task CancellationBeforeUploadingDoesNotSendARequest()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new StubS3Client((_, _) => Task.FromResult(new PutObjectResponse()));
        var waits = new List<TimeSpan>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateUploader(client, waits).UploadAsync("bucket", "key", "file.zip", cancellation.Token));

        Assert.Empty(client.Requests);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task CancellationDuringUploadDoesNotRetry()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new StubS3Client((_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.FromCanceled<PutObjectResponse>(token);
        });
        var waits = new List<TimeSpan>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateUploader(client, waits).UploadAsync("bucket", "key", "file.zip", cancellation.Token));

        Assert.Single(client.Requests);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task CancellationInterruptsTheRetryDelay()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new StubS3Client((_, _) =>
            Task.FromException<PutObjectResponse>(ServiceError(HttpStatusCode.ServiceUnavailable)));
        var delayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var uploader = new S3FileUploader(client, (_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            delayStarted.SetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, _ => { });

        var upload = uploader.UploadAsync("bucket", "key", "file.zip", cancellation.Token);
        await delayStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task ConcurrentFilesHaveIndependentRetryCounts()
    {
        var attempts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        using var client = new StubS3Client((request, _) => attempts.AddOrUpdate(request.Key, 1, (_, count) => count + 1) <= 5
            ? Task.FromException<PutObjectResponse>(ServiceError(HttpStatusCode.ServiceUnavailable))
            : Task.FromResult(new PutObjectResponse()));
        var uploader = new S3FileUploader(client, async (_, _) => { await Task.Yield(); }, _ => { });

        await Task.WhenAll(uploader.UploadAsync("bucket", "file-1", "file-1.zip"),
            uploader.UploadAsync("bucket", "file-2", "file-2.zip"));

        Assert.Equal(6, attempts["file-1"]);
        Assert.Equal(6, attempts["file-2"]);
    }

    [Fact]
    public async Task SdkIncompleteBodyResponseReopensFileAndSendsTheWholeBody()
    {
        const string body = "This is the complete file body for a restarted S3 upload.";
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, body, new UTF8Encoding(false));
            using var handler = new IncompleteBodyHandler();
            using var httpClient = new HttpClient(handler);
            using var client = new AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config
            {
                ServiceURL = "https://s3.example.test",
                ForcePathStyle = true,
                MaxErrorRetry = 0,
                HttpClientFactory = new TestHttpClientFactory(httpClient)
            });
            var waits = new List<TimeSpan>();

            await CreateUploader(client, waits).UploadAsync("bucket", "key", filePath);

            Assert.Equal(2, handler.Attempts);
            Assert.Contains(body, handler.SuccessfulBody);
            Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(waits));
            // No SDK-owned file stream remains open after either attempt.
            using var exclusive = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static S3FileUploader CreateUploader(IAmazonS3 client, List<TimeSpan> waits, Action<string>? log = null) =>
        new(client, (wait, _) => { waits.Add(wait); return Task.CompletedTask; }, log ?? (_ => { }));

    private static AmazonS3Exception ServiceError(HttpStatusCode status, string? code = null, string message = "Upload failed") =>
        new(message) { StatusCode = status, ErrorCode = code };

    private sealed class StubS3Client(Func<PutObjectRequest, CancellationToken, Task<PutObjectResponse>> upload)
        : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "https://s3.example.test" })
    {
        public System.Collections.Concurrent.ConcurrentBag<PutObjectRequest> Requests { get; } = [];

        public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return upload(request, cancellationToken);
        }
    }

    private sealed class TestHttpClientFactory(HttpClient client) : Amazon.Runtime.HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => client;
        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;
        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
    }

    private sealed class IncompleteBodyHandler : HttpMessageHandler
    {
        public int Attempts { get; private set; }
        public string SuccessfulBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (++Attempts == 1)
            {
                // Simulate a server rejecting an upload after only part of the body arrived.
                var stream = await request.Content!.ReadAsStreamAsync(cancellationToken);
                var prefix = new byte[3];
                await stream.ReadExactlyAsync(prefix, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    RequestMessage = request,
                    Content = new StringContent("<Error><Code>IncompleteBody</Code>" +
                        "<Message>You did not provide the number of bytes specified by the Content-Length HTTP header.</Message>" +
                        "<RequestId>test</RequestId></Error>", Encoding.UTF8, "application/xml")
                };
            }

            SuccessfulBody = Encoding.UTF8.GetString(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent("") };
        }
    }
}
