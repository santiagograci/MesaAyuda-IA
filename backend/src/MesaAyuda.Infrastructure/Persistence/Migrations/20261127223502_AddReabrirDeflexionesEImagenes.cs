using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesaAyuda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReabrirDeflexionesEImagenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagenAdjuntaBase64",
                table: "respuestas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagenAdjuntaBase64",
                table: "casos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "metricas_sistema",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Valor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metricas_sistema", x => x.Clave);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metricas_sistema");

            migrationBuilder.DropColumn(
                name: "ImagenAdjuntaBase64",
                table: "respuestas");

            migrationBuilder.DropColumn(
                name: "ImagenAdjuntaBase64",
                table: "casos");
        }
    }
}
