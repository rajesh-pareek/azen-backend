using Amazon.S3;
using Amazon.S3.Model;
using Azen.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;

namespace Azen.Infrastructure.Services;

public class S3StorageService : IStorageService
{
    private readonly string _bucketName;
    private readonly IAmazonS3 _s3Client;

    public S3StorageService(IConfiguration config)
    {
        var endpoint = config["Storage:Endpoint"]!;
        var accessKey = config["Storage:AccessKey"]!;
        var secretKey = config["Storage:SecretKey"]!;
        _bucketName = config["Storage:BucketName"]!;

        var s3Config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true, //required for MinIO and R2
            UseHttp = !bool.Parse(config["Storage:UseSSL"] ?? "false")
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
    }

    public async Task<string> UploadAsync(string key, Stream fileStream, string contentType)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType
        };
        await _s3Client.PutObjectAsync(request);
        return key;

    }

    public async Task DeleteAsync(string key)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
        };
        await _s3Client.DeleteObjectAsync(request);
    }

    public string GetPresignedUrl(string key, int expiryMinutes = 60)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Verb = HttpVerb.GET,
            Protocol = Protocol.HTTP
        };
        return _s3Client.GetPreSignedURL(request);
    }

}