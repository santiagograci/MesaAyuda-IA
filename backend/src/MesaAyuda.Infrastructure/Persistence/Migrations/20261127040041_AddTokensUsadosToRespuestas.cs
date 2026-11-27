using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesaAyuda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTokensUsadosToRespuestas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TokensUsados",
                table: "respuestas",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokensUsados",
                table: "respuestas");
        }
    }
}
