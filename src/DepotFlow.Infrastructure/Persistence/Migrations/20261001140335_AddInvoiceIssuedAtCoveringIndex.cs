using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceIssuedAtCoveringIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IssuedAt_Covering",
                table: "Invoices",
                column: "IssuedAtUtc")
                .Annotation("SqlServer:Include", new[] { "ShippingLineId", "Total", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_IssuedAt_Covering",
                table: "Invoices");
        }
    }
}
