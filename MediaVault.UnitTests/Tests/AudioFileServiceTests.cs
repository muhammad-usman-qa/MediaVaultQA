using FluentAssertions;
using MediaVault.API.Models;
using MediaVault.API.Repositories;
using MediaVault.API.Services;
using MediaVault.UnitTests.Fakes;
using Moq;

namespace MediaVault.UnitTests.Tests;

public class AudioFileServiceTests
{
    private readonly Mock<IRepository<AudioFile>> _mockRepository;
    private readonly Mock<IStorageService> _mockStorage;
    private readonly AudioFileService _sut;
    private readonly AudioFileFaker _faker;

    public AudioFileServiceTests()
    {
        _mockRepository = new Mock<IRepository<AudioFile>>();
        _mockStorage = new Mock<IStorageService>();
        _sut = new AudioFileService(_mockRepository.Object, _mockStorage.Object);
        _faker = new AudioFileFaker();
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllAudioFiles()
    {
        var files = _faker.Generate(5);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(files);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(5);
        result.Should().BeEquivalentTo(files);
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetAllAsync();

        result.Should().BeEmpty();
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsAudioFile()
    {
        var file = _faker.Generate();
        _mockRepository.Setup(r => r.GetByIdAsync(file.Id)).ReturnsAsync(file);

        var result = await _sut.GetByIdAsync(file.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(file.Id);
        result.Title.Should().Be(file.Title);
        result.Artist.Should().Be(file.Artist);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AudioFile?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesAudioFileAndCallsStorage()
    {
        const string expectedUrl = "https://storage.mediavault.com/audio/bohemian.mp3";
        var request = new CreateAudioFileRequest(
            "Bohemian Rhapsody", "Queen", 354, 5_000_000, AudioFormat.Mp3, "bohemian.mp3", "rock,classic");

        _mockStorage.Setup(s => s.GenerateBlobUrlAsync("bohemian.mp3", "audio"))
                    .ReturnsAsync(expectedUrl);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<AudioFile>()))
                       .ReturnsAsync((AudioFile a) => a);

        var result = await _sut.CreateAsync(request);

        result.Should().NotBeNull();
        result.Title.Should().Be("Bohemian Rhapsody");
        result.Artist.Should().Be("Queen");
        result.DurationSeconds.Should().Be(354);
        result.FileSizeBytes.Should().Be(5_000_000);
        result.Format.Should().Be(AudioFormat.Mp3);
        result.Tags.Should().Be("rock,classic");
        result.BlobUrl.Should().Be(expectedUrl);
        result.IsProcessed.Should().BeFalse();

        _mockStorage.Verify(s => s.GenerateBlobUrlAsync("bohemian.mp3", "audio"), Times.Once);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<AudioFile>()), Times.Once);
    }

    [Theory]
    [InlineData("", "Queen", 354, 1_000_000, "Title is required")]
    [InlineData("  ", "Queen", 354, 1_000_000, "Title is required")]
    [InlineData("Bohemian Rhapsody", "", 354, 1_000_000, "Artist is required")]
    [InlineData("Bohemian Rhapsody", "Queen", 0, 1_000_000, "Duration must be positive")]
    [InlineData("Bohemian Rhapsody", "Queen", -5, 1_000_000, "Duration must be positive")]
    [InlineData("Bohemian Rhapsody", "Queen", 354, 0, "File size must be positive")]
    public async Task CreateAsync_WithInvalidRequest_ThrowsArgumentException(
        string title, string artist, int duration, long fileSize, string expectedMessage)
    {
        var request = new CreateAudioFileRequest(title, artist, duration, fileSize, AudioFormat.Mp3, "file.mp3");

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*{expectedMessage}*");

        _mockStorage.Verify(s => s.GenerateBlobUrlAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<AudioFile>()), Times.Never);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WhenExists_UpdatesAndReturnsFile()
    {
        var existing = _faker.Generate();
        var updateRequest = new UpdateAudioFileRequest("New Title", "New Artist", "jazz,live", true);
        _mockRepository.Setup(r => r.GetByIdAsync(existing.Id)).ReturnsAsync(existing);

        var result = await _sut.UpdateAsync(existing.Id, updateRequest);

        result.Should().NotBeNull();
        result!.Title.Should().Be("New Title");
        result.Artist.Should().Be("New Artist");
        result.Tags.Should().Be("jazz,live");
        result.IsProcessed.Should().BeTrue();
        _mockRepository.Verify(r => r.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNullFields_OnlyUpdatesProvidedFields()
    {
        var existing = _faker.Generate();
        var originalTitle = existing.Title;
        var updateRequest = new UpdateAudioFileRequest(null, null, "new-tags", null);
        _mockRepository.Setup(r => r.GetByIdAsync(existing.Id)).ReturnsAsync(existing);

        var result = await _sut.UpdateAsync(existing.Id, updateRequest);

        result!.Title.Should().Be(originalTitle);
        result.Tags.Should().Be("new-tags");
    }

    [Fact]
    public async Task UpdateAsync_WhenNotExists_ReturnsNull()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AudioFile?)null);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateAudioFileRequest("X", null, null, null));

        result.Should().BeNull();
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AudioFile>()), Times.Never);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenExists_DeletesFromStorageAndRepository_ReturnsTrue()
    {
        var file = _faker.Generate();
        _mockRepository.Setup(r => r.GetByIdAsync(file.Id)).ReturnsAsync(file);
        _mockStorage.Setup(s => s.DeleteBlobAsync(file.BlobUrl)).ReturnsAsync(true);

        var result = await _sut.DeleteAsync(file.Id);

        result.Should().BeTrue();
        _mockStorage.Verify(s => s.DeleteBlobAsync(file.BlobUrl), Times.Once);
        _mockRepository.Verify(r => r.DeleteAsync(file), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ReturnsFalseWithNoSideEffects()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AudioFile?)null);

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        result.Should().BeFalse();
        _mockStorage.Verify(s => s.DeleteBlobAsync(It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<AudioFile>()), Times.Never);
    }

    // ── SearchAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ByTitle_ReturnsMatchingFiles()
    {
        var files = new List<AudioFile>
        {
            new() { Title = "Bohemian Rhapsody", Artist = "Queen", Tags = "rock" },
            new() { Title = "Stairway to Heaven", Artist = "Led Zeppelin", Tags = "rock" },
            new() { Title = "Jazz Nocturne", Artist = "Bill Evans", Tags = "jazz" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(files);

        var result = await _sut.SearchAsync("Bohemian");

        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Bohemian Rhapsody");
    }

    [Fact]
    public async Task SearchAsync_ByArtist_ReturnsMatchingFiles()
    {
        var files = new List<AudioFile>
        {
            new() { Title = "Song 1", Artist = "Led Zeppelin", Tags = "rock" },
            new() { Title = "Song 2", Artist = "Led Zeppelin", Tags = "classic" },
            new() { Title = "Song 3", Artist = "Bill Evans", Tags = "jazz" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(files);

        var result = await _sut.SearchAsync("Led Zeppelin");

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(f => f.Artist.Should().Be("Led Zeppelin"));
    }

    [Fact]
    public async Task SearchAsync_ByTag_ReturnsMatchingFiles()
    {
        var files = new List<AudioFile>
        {
            new() { Title = "Track 1", Artist = "Artist A", Tags = "rock,live" },
            new() { Title = "Track 2", Artist = "Artist B", Tags = "jazz" },
            new() { Title = "Track 3", Artist = "Artist C", Tags = "rock,studio" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(files);

        var result = await _sut.SearchAsync("rock");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_IsCaseInsensitive()
    {
        var files = new List<AudioFile>
        {
            new() { Title = "BOHEMIAN RHAPSODY", Artist = "QUEEN", Tags = "ROCK" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(files);

        var result = await _sut.SearchAsync("bohemian");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ReturnsAllFiles()
    {
        var files = _faker.Generate(10);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(files);

        var result = await _sut.SearchAsync("");

        result.Should().HaveCount(10);
    }

    [Fact]
    public async Task SearchAsync_WithNoMatches_ReturnsEmptyList()
    {
        var files = new List<AudioFile>
        {
            new() { Title = "Jazz Piece", Artist = "Miles Davis", Tags = "jazz" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(files);

        var result = await _sut.SearchAsync("metal");

        result.Should().BeEmpty();
    }
}
