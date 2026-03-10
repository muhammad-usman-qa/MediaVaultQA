namespace MediaVault.API.Models;

public class AudioFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
    public AudioFormat Format { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string Tags { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
}

public enum AudioFormat { Mp3, Wav, Flac, Ogg, Aac }
