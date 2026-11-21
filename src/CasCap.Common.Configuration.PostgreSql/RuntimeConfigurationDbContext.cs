using CasCap.Common.Configuration.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CasCap.Common.Configuration.PostgreSql;

/// <summary>Owns only the add-on's fixed cascap_config schema and migration history.</summary>
public sealed class RuntimeConfigurationDbContext(
    DbContextOptions<RuntimeConfigurationDbContext> options
) : DbContext(options)
{
    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(RuntimeConfigurationStorage.Schema);
        var revisions = modelBuilder.Entity<RuntimeRevisionEntity>();
        revisions.ToTable("revisions", table => table.HasCheckConstraint("ck_revisions_positive", "revision > 0 AND contract_version > 0"));
        revisions.HasKey(row => new { row.Application, row.Environment, row.ContractId, row.Revision }).HasName("pk_revisions");
        revisions.Property(row => row.Application).HasColumnName("application").HasMaxLength(100);
        revisions.Property(row => row.Environment).HasColumnName("environment").HasMaxLength(100);
        revisions.Property(row => row.ContractId).HasColumnName("contract_id").HasMaxLength(100);
        revisions.Property(row => row.Revision).HasColumnName("revision");
        revisions.Property(row => row.ContractVersion).HasColumnName("contract_version");
        var payload = revisions.Property(row => row.Payload).HasColumnName("payload").HasColumnType("jsonb");
        var recorded = revisions.Property(row => row.RecordedAtUtc).HasColumnName("recorded_at_utc");
        revisions.Property(row => row.Actor).HasColumnName("actor").HasMaxLength(200);
        revisions.Property(row => row.Reason).HasColumnName("reason").HasMaxLength(500);
        var expires = revisions.Property(row => row.ExpiresAtUtc).HasColumnName("expires_at_utc");
#if NET11_0_OR_GREATER
        // The generated jsonb default is JsonElement; explicitly retain string provider values.
        var payloadComparer = ValueComparer.CreateDefault<string>(favorStructuralComparisons: false);
        payload.HasConversion(new ValueConverter<string, string>(value => value, value => value),
            payloadComparer, payloadComparer);
        // Npgsql 11's generated timestamptz mapping defaults to DateTime; retain UTC instant semantics.
        var timestampConverter = new ValueConverter<DateTimeOffset, DateTime>(
            value => value.UtcDateTime, value => new DateTimeOffset(value));
        recorded.HasConversion(timestampConverter);
        expires.HasConversion(timestampConverter);
#endif
        revisions.Property(row => row.RestoredFromRevision).HasColumnName("restored_from_revision");
        var heads = modelBuilder.Entity<RuntimeHeadEntity>();
        heads.ToTable("heads");
        heads.HasKey(row => new { row.Application, row.Environment, row.ContractId }).HasName("pk_heads");
        heads.Property(row => row.Application).HasColumnName("application").HasMaxLength(100);
        heads.Property(row => row.Environment).HasColumnName("environment").HasMaxLength(100);
        heads.Property(row => row.ContractId).HasColumnName("contract_id").HasMaxLength(100);
        heads.Property(row => row.Revision).HasColumnName("revision").IsConcurrencyToken();
        heads.HasIndex(row => new { row.Application, row.Environment, row.ContractId, row.Revision })
            .HasDatabaseName("ix_heads_revision");
        heads.HasOne<RuntimeRevisionEntity>().WithMany()
            .HasForeignKey(row => new { row.Application, row.Environment, row.ContractId, row.Revision })
            .HasConstraintName("fk_heads_revisions")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
