using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROYECTO_SUBASTA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TriggersInmutabilidadAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Pomelo.EntityFrameworkCore.MySql")
            {
                migrationBuilder.Sql(@"
                    CREATE TRIGGER trg_logs_auditoria_no_update
                    BEFORE UPDATE ON LogsAuditoria
                    FOR EACH ROW
                    BEGIN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Los registros de auditoria son inmutables (append-only) y no pueden modificarse.';
                    END;
                ");

                migrationBuilder.Sql(@"
                    CREATE TRIGGER trg_logs_auditoria_no_delete
                    BEFORE DELETE ON LogsAuditoria
                    FOR EACH ROW
                    BEGIN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Los registros de auditoria son inmutables (append-only) y no pueden eliminarse.';
                    END;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Pomelo.EntityFrameworkCore.MySql")
            {
                migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_logs_auditoria_no_update;");
                migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_logs_auditoria_no_delete;");
            }
        }
    }
}
