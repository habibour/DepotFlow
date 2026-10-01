using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitFilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Visits_ShippingLineId",
                table: "Visits");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ShippingLine_GateIn",
                table: "Visits",
                columns: new[] { "ShippingLineId", "GateInAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_Status_GateIn",
                table: "Visits",
                columns: new[] { "Status", "GateInAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Visits_ShippingLine_GateIn",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Visits_Status_GateIn",
                table: "Visits");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ShippingLineId",
                table: "Visits",
                column: "ShippingLineId");
        }
    }
}
