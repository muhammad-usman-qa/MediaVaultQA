using MediaVault.API.Models;
using MediaVault.API.Repositories;

namespace MediaVault.API.Services;

public class AudioFileService : IAudioFileService
{
    private readonly IRepository<AudioFile> _repository;
    private readonly IStorageService _storageService;

    public AudioFileService(IRepository<AudioFile> repository, IStorageService storageService)
    {
        _repository = repository;
        _storageService = storageService;
    }

    public async Task<IReadOnlyList<AudioFile>> GetAllAsync() =>
        await _repository.GetAllAsync();

    public async Task<AudioFile?> GetByIdAsync(Guid id) =>
        await _repository.GetByIdAsync(id);

    public async Task<AudioFile> CreateAsync(CreateAudioFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Artist))
            throw new ArgumentException("Artist is required.", nameof(request));
        if (request.DurationSeconds <= 0)
            throw new ArgumentException("Duration must be positive.", nameof(request));
        if (request.FileSizeBytes <= 0)
            throw new ArgumentException("File size must be positive.", nameof(request));

        var blobUrl = await _storageService.GenerateBlobUrlAsync(request.FileName, "audio");

        var audioFile = new AudioFile
        {
            Title = request.Title,
            Artist = request.Artist,
            DurationSeconds = request.DurationSeconds,
            FileSizeBytes = request.FileSizeBytes,
            Format = request.Format,
            Tags = request.Tags,
            BlobUrl = blobUrl,
            IsProcessed = false
        };

        return await _repository.AddAsync(audioFile);
    }

    public async Task<AudioFile?> UpdateAsync(Guid id, UpdateAudioFileRequest request)
    {
        var audioFile = await _repository.GetByIdAsync(id);
        if (audioFile is null) return null;

        if (request.Title is not null) audioFile.Title = request.Title;
        if (request.Artist is not null) audioFile.Artist = request.Artist;
        if (request.Tags is not null) audioFile.Tags = request.Tags;
        if (request.IsProcessed.HasValue) audioFile.IsProcessed = request.IsProcessed.Value;

        await _repository.UpdateAsync(audioFile);
        return audioFile;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var audioFile = await _repository.GetByIdAsync(id);
        if (audioFile is null) return false;

        await _storageService.DeleteBlobAsync(audioFile.BlobUrl);
        await _repository.DeleteAsync(audioFile);
        return true;
    }

    public async Task<IReadOnlyList<AudioFile>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await _repository.GetAllAsync();

        var all = await _repository.GetAllAsync();
        return all.Where(a =>
            a.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            a.Artist.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            a.Tags.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
