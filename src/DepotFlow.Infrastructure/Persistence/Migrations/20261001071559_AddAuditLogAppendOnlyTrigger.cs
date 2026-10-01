using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepotFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogAppendOnlyTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The audit trail must be tamper-evident: whoever connects, rows can be added but never changed or removed.
            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_AuditLogs_AppendOnly] ON [AuditLogs]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    THROW 51000, 'AuditLogs is append-only: rows cannot be updated or deleted.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER [TR_AuditLogs_AppendOnly]");
        }
    }
}
