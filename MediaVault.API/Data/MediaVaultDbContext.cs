using MediaVault.API.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.API.Data;

public class MediaVaultDbContext : DbContext
{
    public MediaVaultDbContext(DbContextOptions<MediaVaultDbContext> options) : base(options) { }

    public DbSet<AudioFile> AudioFiles => Set<AudioFile>();
    public DbSet<EmailRecord> EmailRecords => Set<EmailRecord>();
    public DbSet<ChatTranscript> ChatTranscripts => Set<ChatTranscript>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatTranscript>()
            .HasMany(t => t.Messages)
            .WithOne(m => m.Transcript)
            .HasForeignKey(m => m.TranscriptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
