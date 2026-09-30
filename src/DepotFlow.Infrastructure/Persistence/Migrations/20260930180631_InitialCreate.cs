using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Containers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "char(11)", nullable: false),
                    SizeFeet = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Containers", x => x.Id);
                    table.CheckConstraint("CK_Containers_SizeFeet", "[SizeFeet] IN (20, 40)");
                });

            migrationBuilder.CreateTable(
                name: "ShippingLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContainerId = table.Column<int>(type: "int", nullable: false),
                    ShippingLineId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    GateInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GateOutAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TruckInNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TruckOutNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SealNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DamageNotesIn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DamageNotesOut = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    YardSlotId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ClosedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.CheckConstraint("CK_Visits_Release", "([Status] <> 2 OR [GateOutAtUtc] IS NOT NULL) AND ([GateOutAtUtc] IS NULL OR [GateOutAtUtc] >= [GateInAtUtc])");
                    table.ForeignKey(
                        name: "FK_Visits_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_ShippingLines_ShippingLineId",
                        column: x => x.ShippingLineId,
                        principalTable: "ShippingLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Containers_Number",
                table: "Containers",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShippingLines_Code",
                table: "ShippingLines",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visits_Container_GateIn",
                table: "Visits",
                columns: new[] { "ContainerId", "GateInAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ShippingLineId",
                table: "Visits",
                column: "ShippingLineId");

            migrationBuilder.CreateIndex(
                name: "UX_Visits_Container_Active",
                table: "Visits",
                column: "ContainerId",
                unique: true,
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Visits");

            migrationBuilder.DropTable(
                name: "Containers");

            migrationBuilder.DropTable(
                name: "ShippingLines");
        }
    }
}
