// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Hosting.RustFs;

namespace CommunityToolkit.Aspire.Hosting.RustFs.Tests;

public class RustFsS3SignerTests
{
    private const string EmptyBodySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [Fact]
    public void SignPutBucket_ReturnsRequiredHeaders()
    {
        var headers = RustFsS3Signer.SignPutBucket(
            hostHeader: "localhost:9000",
            bucketName: "mybucket",
            accessKey: "ACCESSKEY",
            secretKey: "SECRETKEY",
            region: "us-east-1",
            timestamp: new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("x-amz-date", headers.Keys);
        Assert.Contains("x-amz-content-sha256", headers.Keys);
        Assert.Contains("Authorization", headers.Keys);
    }

    [Fact]
    public void SignPutBucket_UsesAmzDateFormat()
    {
        var headers = RustFsS3Signer.SignPutBucket(
            hostHeader: "localhost:9000",
            bucketName: "mybucket",
            accessKey: "ACCESSKEY",
            secretKey: "SECRETKEY",
            region: "us-east-1",
            timestamp: new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("20240115T120000Z", headers["x-amz-date"]);
    }

    [Fact]
    public void SignPutBucket_UsesEmptyBodyContentHash()
    {
        var headers = RustFsS3Signer.SignPutBucket(
            hostHeader: "localhost:9000",
            bucketName: "mybucket",
            accessKey: "ACCESSKEY",
            secretKey: "SECRETKEY",
            region: "us-east-1",
            timestamp: new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(EmptyBodySha256, headers["x-amz-content-sha256"]);
    }

    [Fact]
    public void SignPutBucket_AuthorizationHeaderHasExpectedStructure()
    {
        var headers = RustFsS3Signer.SignPutBucket(
            hostHeader: "localhost:9000",
            bucketName: "mybucket",
            accessKey: "ACCESSKEY",
            secretKey: "SECRETKEY",
            region: "us-east-1",
            timestamp: new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero));

        var authorization = headers["Authorization"];

        Assert.StartsWith("AWS4-HMAC-SHA256 ", authorization);
        Assert.Contains("Credential=ACCESSKEY/20240115/us-east-1/s3/aws4_request", authorization);
        Assert.Contains("SignedHeaders=host;x-amz-content-sha256;x-amz-date", authorization);
        Assert.Matches(@"Signature=[0-9a-f]{64}$", authorization);
    }

    [Fact]
    public void SignPutBucket_IsDeterministicForSameInput()
    {
        var timestamp = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

        var first = RustFsS3Signer.SignPutBucket("localhost:9000", "mybucket", "AK", "SK", "us-east-1", timestamp);
        var second = RustFsS3Signer.SignPutBucket("localhost:9000", "mybucket", "AK", "SK", "us-east-1", timestamp);

        Assert.Equal(first["Authorization"], second["Authorization"]);
    }

    [Fact]
    public void SignPutBucket_DifferentBucketsProduceDifferentSignatures()
    {
        var timestamp = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

        var a = RustFsS3Signer.SignPutBucket("localhost:9000", "alpha", "AK", "SK", "us-east-1", timestamp);
        var b = RustFsS3Signer.SignPutBucket("localhost:9000", "bravo", "AK", "SK", "us-east-1", timestamp);

        Assert.NotEqual(a["Authorization"], b["Authorization"]);
    }

    [Fact]
    public void SignPutBucket_DifferentSecretKeysProduceDifferentSignatures()
    {
        var timestamp = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

        var a = RustFsS3Signer.SignPutBucket("localhost:9000", "mybucket", "AK", "SECRET1", "us-east-1", timestamp);
        var b = RustFsS3Signer.SignPutBucket("localhost:9000", "mybucket", "AK", "SECRET2", "us-east-1", timestamp);

        Assert.NotEqual(a["Authorization"], b["Authorization"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SignPutBucket_ThrowsWhenBucketIsNullOrWhitespace(string? bucket)
    {
        Assert.ThrowsAny<ArgumentException>(() => RustFsS3Signer.SignPutBucket(
            "localhost:9000", bucket!, "AK", "SK", "us-east-1", DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SignPutBucket_ThrowsWhenSecretKeyIsNullOrWhitespace(string? secret)
    {
        Assert.ThrowsAny<ArgumentException>(() => RustFsS3Signer.SignPutBucket(
            "localhost:9000", "mybucket", "AK", secret!, "us-east-1", DateTimeOffset.UtcNow));
    }
}
