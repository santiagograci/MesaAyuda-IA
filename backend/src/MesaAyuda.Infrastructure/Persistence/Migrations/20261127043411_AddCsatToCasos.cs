using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesaAyuda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCsatToCasos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CalificacionCsat",
                table: "casos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComentarioCsat",
                table: "casos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalificacionCsat",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "ComentarioCsat",
                table: "casos");
        }
    }
}
