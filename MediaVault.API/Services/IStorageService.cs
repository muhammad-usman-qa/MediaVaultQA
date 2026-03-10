namespace MediaVault.API.Services;

public interface IStorageService
{
    Task<string> GenerateBlobUrlAsync(string fileName, string containerName);
    Task<bool> DeleteBlobAsync(string blobUrl);
    Task<bool> BlobExistsAsync(string blobUrl);
}
