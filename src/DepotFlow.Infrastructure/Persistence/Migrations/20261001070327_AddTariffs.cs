using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tariffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShippingLineId = table.Column<int>(type: "int", nullable: false),
                    SizeFeet = table.Column<byte>(type: "tinyint", nullable: false),
                    StrategyKey = table.Column<string>(type: "varchar(20)", nullable: false),
                    FreeDays = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "BDT"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tariffs", x => x.Id);
                    table.CheckConstraint("CK_Tariffs_FreeDays", "[FreeDays] >= 0");
                    table.CheckConstraint("CK_Tariffs_SizeFeet", "[SizeFeet] IN (20, 40)");
                    table.ForeignKey(
                        name: "FK_Tariffs_ShippingLines_ShippingLineId",
                        column: x => x.ShippingLineId,
                        principalTable: "ShippingLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TariffTiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TariffId = table.Column<int>(type: "int", nullable: false),
                    FromDay = table.Column<int>(type: "int", nullable: false),
                    ToDay = table.Column<int>(type: "int", nullable: true),
                    RatePerDay = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffTiers", x => x.Id);
                    table.CheckConstraint("CK_TariffTiers_Rate", "[RatePerDay] > 0");
                    table.ForeignKey(
                        name: "FK_TariffTiers_Tariffs_TariffId",
                        column: x => x.TariffId,
                        principalTable: "Tariffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Tariffs_Line_Size_Active",
                table: "Tariffs",
                columns: new[] { "ShippingLineId", "SizeFeet" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TariffTiers_TariffId",
                table: "TariffTiers",
                column: "TariffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TariffTiers");

            migrationBuilder.DropTable(
                name: "Tariffs");
        }
    }
}
