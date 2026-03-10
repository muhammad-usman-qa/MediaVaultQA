using MediaVault.API.Models;

namespace MediaVault.API.Services;

public interface IAudioFileService
{
    Task<IReadOnlyList<AudioFile>> GetAllAsync();
    Task<AudioFile?> GetByIdAsync(Guid id);
    Task<AudioFile> CreateAsync(CreateAudioFileRequest request);
    Task<AudioFile?> UpdateAsync(Guid id, UpdateAudioFileRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<IReadOnlyList<AudioFile>> SearchAsync(string query);
}

public record CreateAudioFileRequest(
    string Title,
    string Artist,
    int DurationSeconds,
    long FileSizeBytes,
    AudioFormat Format,
    string FileName,
    string Tags = "");

public record UpdateAudioFileRequest(
    string? Title,
    string? Artist,
    string? Tags,
    bool? IsProcessed);
