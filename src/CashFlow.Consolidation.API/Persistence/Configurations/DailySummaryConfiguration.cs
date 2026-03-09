using CashFlow.Domain.Consolidation;
using CashFlow.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Consolidation.API.Persistence.Configurations;

public sealed class DailySummaryConfiguration : IEntityTypeConfiguration<DailySummary>
{
    public void Configure(EntityTypeBuilder<DailySummary> builder)
    {
        builder.ToTable("daily_summary");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, v => new DailySummaryId(v))
            .HasColumnName("id");

        builder.Property(d => d.MerchantId)
            .HasConversion(id => id.Value, v => new MerchantId(v))
            .HasColumnName("merchant_id")
            .IsRequired();

        builder.Property(d => d.Date).HasColumnName("date").IsRequired();
        builder.Property(d => d.TransactionCount).HasColumnName("transaction_count").HasDefaultValue(0);
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property<uint>("xmin").IsRowVersion();

        builder.OwnsOne(d => d.TotalCredits, c =>
        {
            c.Property(m => m.Amount).HasColumnName("total_credits_amount").HasPrecision(18, 2).HasDefaultValue(0m);
            c.Property(m => m.Currency).HasColumnName("total_credits_currency").HasMaxLength(3).HasDefaultValue("BRL");
        });

        builder.OwnsOne(d => d.TotalDebits, c =>
        {
            c.Property(m => m.Amount).HasColumnName("total_debits_amount").HasPrecision(18, 2).HasDefaultValue(0m);
            c.Property(m => m.Currency).HasColumnName("total_debits_currency").HasMaxLength(3).HasDefaultValue("BRL");
        });

        builder.Ignore(d => d.Balance);
        builder.Ignore(d => d.DomainEvents);

        builder.HasIndex(d => new { d.MerchantId, d.Date })
            .IsUnique()
            .HasDatabaseName("ix_daily_summary_merchant_date");
    }
}
