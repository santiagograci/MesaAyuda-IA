using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesaAyuda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToCasos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "casos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "casos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_casos_Eliminado",
                table: "casos",
                column: "Eliminado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_casos_Eliminado",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "casos");
        }
    }
}
