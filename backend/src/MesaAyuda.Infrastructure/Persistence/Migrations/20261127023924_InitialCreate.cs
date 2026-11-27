using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MesaAyuda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "casos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Prioridad = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_casos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_casos_usuarios_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "respuestas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CasoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    EsGeneradaPorIA = table.Column<bool>(type: "boolean", nullable: false),
                    AprobadaPorAnalista = table.Column<bool>(type: "boolean", nullable: false),
                    AutorNombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    FuentesCitadasJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_respuestas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_respuestas_casos_CasoId",
                        column: x => x.CasoId,
                        principalTable: "casos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_casos_ClienteId",
                table: "casos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_casos_Estado",
                table: "casos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_respuestas_CasoId",
                table: "respuestas",
                column: "CasoId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "respuestas");

            migrationBuilder.DropTable(
                name: "casos");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
