using System.Collections.Concurrent;
using System.Security.Cryptography;
using SmartX.Core.Contracts;

namespace SmartX.Core;

// Keeps attachments encrypted on disk. Metadata stays in memory like the rest of the gateway.
public sealed class AttachmentStore
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    // Covers the three things the brief names: configs, logs and deployment photos
    private static readonly string[] AllowedExtensions =
        [".json", ".yaml", ".yml", ".conf", ".cfg", ".txt", ".log", ".csv", ".png", ".jpg", ".jpeg"];

    private readonly ConcurrentDictionary<Guid, AttachmentDto> _metadata = new();
    private readonly string _directory;
    private readonly byte[] _key;

    public AttachmentStore(string directory, byte[] key)
    {
        _directory = directory;
        _key = key;

        Directory.CreateDirectory(_directory);
    }

    public static bool IsAllowedExtension(string fileName) =>
        AllowedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    public static string AllowedExtensionList => string.Join(", ", AllowedExtensions);

    public async Task<AttachmentDto> SaveAsync(
        string nodeId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        Guid id = Guid.NewGuid();

        using Aes aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        await using FileStream file = File.Create(PathFor(id));

        // The IV is not secret, it just has to be unique per file, so it rides along at the front
        await file.WriteAsync(aes.IV, cancellationToken);

        await using (CryptoStream crypto = new(file, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            // Copied through rather than read into memory, so a large log costs a buffer not a heap allocation
            await content.CopyToAsync(crypto, cancellationToken);
        }

        AttachmentDto metadata = new()
        {
            Id = id,
            NodeId = nodeId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = content.Length,
            UploadedAtUtc = DateTimeOffset.UtcNow
        };

        _metadata[id] = metadata;
        return metadata;
    }

    public bool TryGet(Guid id, out AttachmentDto? metadata) => _metadata.TryGetValue(id, out metadata);

    public IReadOnlyList<AttachmentDto> ForNode(string nodeId) =>
        [.. _metadata.Values.Where(a => string.Equals(a.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))];

    public async Task<Stream> OpenDecryptedAsync(Guid id, CancellationToken cancellationToken)
    {
        FileStream file = File.OpenRead(PathFor(id));

        byte[] iv = new byte[16];
        await file.ReadExactlyAsync(iv, cancellationToken);

        Aes aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;

        return new CryptoStream(file, aes.CreateDecryptor(), CryptoStreamMode.Read);
    }

    public bool Delete(Guid id)
    {
        if (!_metadata.TryRemove(id, out _))
        {
            return false;
        }

        File.Delete(PathFor(id));
        return true;
    }

    private string PathFor(Guid id) => Path.Combine(_directory, $"{id}.bin");
}
