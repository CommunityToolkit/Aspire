using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.RustFs;

/// <summary>
/// Minimal AWS Signature Version 4 signer scoped to S3 <c>PUT /{bucket}</c> requests
/// against a single host with no query string and an empty body.
/// </summary>
/// <remarks>
/// This is intentionally limited so that the implementation is small, auditable, and easy
/// to test. Do not generalise it without first widening the unit tests.
/// </remarks>
internal static class RustFsS3Signer
{
    private const string Service = "s3";
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string AwsRequest = "aws4_request";

    private const string EmptyBodySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    internal const string ContentSha256HeaderName = "x-amz-content-sha256";
    internal const string DateHeaderName = "x-amz-date";
    internal const string AuthorizationHeaderName = "Authorization";

    /// <summary>
    /// Builds the signed headers required to send a <c>PUT /{bucket}</c> request to an
    /// S3-compatible endpoint with an empty body.
    /// </summary>
    /// <param name="hostHeader">The literal <c>Host</c> header value (e.g. <c>localhost:9000</c>).</param>
    /// <param name="bucketName">The bucket name. Must be a valid S3 bucket name.</param>
    /// <param name="accessKey">The access key id.</param>
    /// <param name="secretKey">The secret access key.</param>
    /// <param name="region">The signing region. RustFs/MinIO defaults to <c>us-east-1</c>.</param>
    /// <param name="timestamp">The request timestamp in UTC.</param>
    /// <returns>A dictionary of header name to header value.</returns>
    public static IReadOnlyDictionary<string, string> SignPutBucket(
        string hostHeader,
        string bucketName,
        string accessKey,
        string secretKey,
        string region,
        DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostHeader);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        var utc = timestamp.ToUniversalTime();
        var amzDate = utc.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var canonicalUri = "/" + UriEncodePathSegment(bucketName);

        var canonicalHeaders = new StringBuilder()
            .Append("host:").Append(hostHeader).Append('\n')
            .Append(ContentSha256HeaderName).Append(':').Append(EmptyBodySha256).Append('\n')
            .Append(DateHeaderName).Append(':').Append(amzDate).Append('\n')
            .ToString();

        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";

        var canonicalRequest = new StringBuilder()
            .Append("PUT\n")
            .Append(canonicalUri).Append('\n')
            .Append('\n')
            .Append(canonicalHeaders).Append('\n')
            .Append(signedHeaders).Append('\n')
            .Append(EmptyBodySha256)
            .ToString();

        var credentialScope = $"{dateStamp}/{region}/{Service}/{AwsRequest}";

        var stringToSign = new StringBuilder()
            .Append(Algorithm).Append('\n')
            .Append(amzDate).Append('\n')
            .Append(credentialScope).Append('\n')
            .Append(HexLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))))
            .ToString();

        var signingKey = DeriveSigningKey(secretKey, dateStamp, region);
        var signature = HexLower(HmacSha256(signingKey, stringToSign));

        var authorization = $"{Algorithm} Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DateHeaderName] = amzDate,
            [ContentSha256HeaderName] = EmptyBodySha256,
            [AuthorizationHeaderName] = authorization,
        };
    }

    private static byte[] DeriveSigningKey(string secretKey, string dateStamp, string region)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = HmacSha256(kSecret, dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, Service);
        return HmacSha256(kService, AwsRequest);
    }

    private static byte[] HmacSha256(byte[] key, string data)
        => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string HexLower(byte[] bytes)
        => Convert.ToHexString(bytes).ToLowerInvariant();

    private static string UriEncodePathSegment(string segment)
    {
        var sb = new StringBuilder(segment.Length);
        foreach (var c in segment)
        {
            if ((c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c is '-' or '_' or '.' or '~')
            {
                sb.Append(c);
            }
            else
            {
                foreach (var b in Encoding.UTF8.GetBytes([c]))
                {
                    sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }
            }
        }
        return sb.ToString();
    }
}
