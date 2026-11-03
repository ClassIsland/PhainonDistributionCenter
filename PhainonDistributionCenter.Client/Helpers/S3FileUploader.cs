using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace PhainonDistributionCenter.Client.Helpers;

internal sealed class S3FileUploader(
    IAmazonS3 client,
    Func<TimeSpan, CancellationToken, Task>? delay = null,
    Action<string>? log = null)
{
    private const int MaxRetries = 5;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private readonly Action<string> _log = log ?? Console.Error.WriteLine;

    public async Task UploadAsync(string bucketName, string key, string filePath,
        CancellationToken cancellationToken = default)
    {
        for (var retry = 0; ; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // The SDK opens FilePath for each new request. Never reuse a request whose
                // input stream may have been consumed or disposed by a failed upload.
                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    FilePath = filePath
                };
                await client.PutObjectAsync(request, cancellationToken);
                return;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsRetryable(exception))
            {
                if (retry == MaxRetries)
                {
                    _log($"Upload failed for {filePath} after {MaxRetries} retries: {exception.Message}");
                    throw;
                }

                var wait = TimeSpan.FromSeconds(1 << retry);
                _log($"Upload failed for {filePath}: {exception.Message}. " +
                     $"Retry {retry + 1}/{MaxRetries} in {wait.TotalSeconds} seconds.");
                await _delay(wait, cancellationToken);
            }
        }
    }

    private static bool IsRetryable(Exception exception)
    {
        if (exception is AmazonServiceException serviceException)
        {
            // IncompleteBody is HTTP 400, but can result from an interrupted upload.
            // It must be handled explicitly rather than retrying all client errors.
            return serviceException.Retryable != null ||
                   IsRetryableStatus(serviceException.StatusCode) ||
                   serviceException.ErrorCode is "IncompleteBody" or "RequestTimeout" or "RequestTimeoutException"
                       or "SlowDown" or "Throttling" or "ThrottlingException" or "RequestLimitExceeded"
                       or "InternalError" or "ServiceUnavailable" ||
                   (serviceException.InnerException != null && IsRetryable(serviceException.InnerException));
        }

        if (exception is HttpRequestException httpException)
        {
            return httpException.StatusCode == null || IsRetryableStatus(httpException.StatusCode.Value);
        }

        if (exception is WebException webException)
        {
            return webException.Status is WebExceptionStatus.Timeout or WebExceptionStatus.ConnectFailure
                or WebExceptionStatus.ConnectionClosed or WebExceptionStatus.ReceiveFailure
                or WebExceptionStatus.SendFailure or WebExceptionStatus.NameResolutionFailure
                or WebExceptionStatus.ProxyNameResolutionFailure or WebExceptionStatus.KeepAliveFailure;
        }

        return exception is SocketException or TimeoutException or OperationCanceledException ||
               (exception.InnerException != null && IsRetryable(exception.InnerException));
    }

    private static bool IsRetryableStatus(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode is >= 500 and <= 599;
}
