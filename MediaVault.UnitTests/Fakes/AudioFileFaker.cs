using Bogus;
using MediaVault.API.Models;

namespace MediaVault.UnitTests.Fakes;

/// <summary>
/// Generates realistic fake AudioFile entities for testing.
/// Uses Bogus to produce varied, realistic test data.
/// </summary>
public class AudioFileFaker : Faker<AudioFile>
{
    private static readonly string[] Genres = ["rock", "jazz", "classical", "pop", "hip-hop", "electronic", "blues", "folk"];
    private static readonly string[] Suffixes = ["EP", "Album Version", "Live", "Remix", "Remastered", "Extended Mix"];

    public AudioFileFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Title, f => $"{f.Lorem.Word()} {f.PickRandom(Suffixes)}");
        RuleFor(x => x.Artist, f => f.Name.FullName());
        RuleFor(x => x.DurationSeconds, f => f.Random.Int(30, 720));
        RuleFor(x => x.FileSizeBytes, f => f.Random.Long(500_000, 50_000_000));
        RuleFor(x => x.Format, f => f.PickRandom<AudioFormat>());
        RuleFor(x => x.UploadedAt, f => f.Date.Past(2).ToUniversalTime());
        RuleFor(x => x.Tags, f => string.Join(",", f.PickRandom(Genres, f.Random.Int(1, 4))));
        RuleFor(x => x.IsProcessed, f => f.Random.Bool(0.8f));
        RuleFor(x => x.BlobUrl, f => $"https://storage.mediavault.com/audio/{f.Random.Guid()}.{f.PickRandom("mp3", "wav", "flac")}");
    }
}
