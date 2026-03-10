using System.Net.Http.Json;

namespace MediaVault.API.Services;

public class HttpStorageService : IStorageService
{
    private readonly HttpClient _httpClient;

    public HttpStorageService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string> GenerateBlobUrlAsync(string fileName, string containerName)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/storage/register",
            new { fileName, containerName });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StorageRegistrationResult>();
        return result!.BlobUrl;
    }

    public async Task<bool> DeleteBlobAsync(string blobUrl)
    {
        var response = await _httpClient.DeleteAsync(
            $"/storage/blobs?url={Uri.EscapeDataString(blobUrl)}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> BlobExistsAsync(string blobUrl)
    {
        var response = await _httpClient.GetAsync(
            $"/storage/blobs/exists?url={Uri.EscapeDataString(blobUrl)}");
        return response.IsSuccessStatusCode;
    }
}

public record StorageRegistrationResult(string BlobUrl);
