using CasCap.Common.Configuration.Sqlite.Entities;

namespace CasCap.Common.Configuration.Sqlite;

/// <summary>Owns only prefixed configuration tables, never the application's other tables.</summary>
public sealed class RuntimeConfigurationDbContext(
    DbContextOptions<RuntimeConfigurationDbContext> options
) : DbContext(options)
{
    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var revisions = modelBuilder.Entity<RuntimeRevisionEntity>();
        revisions.ToTable(RuntimeConfigurationStorage.Prefix + "revisions",
            table => table.HasCheckConstraint("ck_revisions_positive", "revision > 0 AND contract_version > 0"));
        revisions.HasKey(row => new { row.Application, row.Environment, row.ContractId, row.Revision }).HasName("pk_revisions");
        revisions.Property(row => row.Application).HasColumnName("application").HasMaxLength(100);
        revisions.Property(row => row.Environment).HasColumnName("environment").HasMaxLength(100);
        revisions.Property(row => row.ContractId).HasColumnName("contract_id").HasMaxLength(100);
        revisions.Property(row => row.Revision).HasColumnName("revision");
        revisions.Property(row => row.ContractVersion).HasColumnName("contract_version");
        revisions.Property(row => row.Payload).HasColumnName("payload");
        revisions.Property(row => row.RecordedAtUtc).HasColumnName("recorded_at_utc");
        revisions.Property(row => row.Actor).HasColumnName("actor").HasMaxLength(200);
        revisions.Property(row => row.Reason).HasColumnName("reason").HasMaxLength(500);
        revisions.Property(row => row.ExpiresAtUtc).HasColumnName("expires_at_utc");
        revisions.Property(row => row.RestoredFromRevision).HasColumnName("restored_from_revision");
        var heads = modelBuilder.Entity<RuntimeHeadEntity>();
        heads.ToTable(RuntimeConfigurationStorage.Prefix + "heads");
        heads.HasKey(row => new { row.Application, row.Environment, row.ContractId }).HasName("pk_heads");
        heads.Property(row => row.Application).HasColumnName("application").HasMaxLength(100);
        heads.Property(row => row.Environment).HasColumnName("environment").HasMaxLength(100);
        heads.Property(row => row.ContractId).HasColumnName("contract_id").HasMaxLength(100);
        heads.Property(row => row.Revision).HasColumnName("revision").IsConcurrencyToken();
        heads.HasIndex(row => new { row.Application, row.Environment, row.ContractId, row.Revision })
            .HasDatabaseName("ix_cascap_config_heads_revision");
        heads.HasOne<RuntimeRevisionEntity>().WithMany()
            .HasForeignKey(row => new { row.Application, row.Environment, row.ContractId, row.Revision })
            .HasConstraintName("fk_heads_revisions").OnDelete(DeleteBehavior.Restrict);
    }
}
