using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddYard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YardSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Block = table.Column<string>(type: "char(1)", nullable: false),
                    Row = table.Column<byte>(type: "tinyint", nullable: false),
                    Bay = table.Column<byte>(type: "tinyint", nullable: false),
                    Tier = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "varchar(12)", nullable: false, computedColumnSql: "[Block] + '-' + RIGHT('0' + CAST([Row] AS varchar(3)), 2) + '-' + RIGHT('0' + CAST([Bay] AS varchar(3)), 2) + '-' + CAST([Tier] AS varchar(3))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YardSlots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Visits_Slot_Active",
                table: "Visits",
                column: "YardSlotId",
                unique: true,
                filter: "[Status] = 1 AND [YardSlotId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_YardSlots_Block_Row_Bay_Tier",
                table: "YardSlots",
                columns: new[] { "Block", "Row", "Bay", "Tier" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_YardSlots_YardSlotId",
                table: "Visits",
                column: "YardSlotId",
                principalTable: "YardSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Visits_YardSlots_YardSlotId",
                table: "Visits");

            migrationBuilder.DropTable(
                name: "YardSlots");

            migrationBuilder.DropIndex(
                name: "UX_Visits_Slot_Active",
                table: "Visits");
        }
    }
}
