using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashFlow.Transactions.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantIdReferenceDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_transaction_merchant_id_reference_date",
                schema: "transactions",
                table: "transaction",
                columns: new[] { "merchant_id", "reference_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transaction_merchant_id_reference_date",
                schema: "transactions",
                table: "transaction");
        }
    }
}
