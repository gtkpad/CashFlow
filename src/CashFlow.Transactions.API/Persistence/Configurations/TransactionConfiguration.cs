using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Transactions.API.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transaction");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, v => new TransactionId(v))
            .HasColumnName("id");

        builder.Property(t => t.MerchantId)
            .HasConversion(id => id.Value, v => new MerchantId(v))
            .HasColumnName("merchant_id")
            .IsRequired();

        builder.Property(t => t.ReferenceDate).HasColumnName("reference_date").IsRequired();
        builder.Property(t => t.Type).HasColumnName("type").IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasMaxLength(128);

        builder.OwnsOne(t => t.Value, v =>
        {
            v.Property(m => m.Amount).HasColumnName("value_amount").HasPrecision(18, 2).IsRequired();
            v.Property(m => m.Currency).HasColumnName("value_currency").HasMaxLength(3).HasDefaultValue("BRL");
        });

        // xmin concurrency token (shadow property — replaces deprecated UseXminAsConcurrencyToken)
        builder.Property<uint>("xmin").IsRowVersion();
        builder.HasIndex(t => t.ReferenceDate).HasDatabaseName("ix_transaction_reference_date");
        builder.HasIndex(t => new { t.MerchantId, t.ReferenceDate })
            .HasDatabaseName("ix_transaction_merchant_id_reference_date");
        builder.Ignore(t => t.DomainEvents);
    }
}
