using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ditado.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AtribuicaoDitadosTurmas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RespostaDitados_Ditados_DitadoId",
                table: "RespostaDitados");

            migrationBuilder.DropForeignKey(
                name: "FK_RespostaSegmentos_RespostaDitados_RespostaDitadoId",
                table: "RespostaSegmentos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RespostaDitados",
                table: "RespostaDitados");

            migrationBuilder.RenameTable(
                name: "RespostaDitados",
                newName: "RespostasDitados");

            migrationBuilder.RenameIndex(
                name: "IX_RespostaDitados_DitadoId",
                table: "RespostasDitados",
                newName: "IX_RespostasDitados_DitadoId");

            migrationBuilder.AddColumn<int>(
                name: "AlunoId",
                table: "RespostasDitados",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RespostasDitados",
                table: "RespostasDitados",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "TurmaDitados",
                columns: table => new
                {
                    TurmaId = table.Column<int>(type: "int", nullable: false),
                    DitadoId = table.Column<int>(type: "int", nullable: false),
                    DataAtribuicao = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DataLimite = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurmaDitados", x => new { x.TurmaId, x.DitadoId });
                    table.ForeignKey(
                        name: "FK_TurmaDitados_Ditados_DitadoId",
                        column: x => x.DitadoId,
                        principalTable: "Ditados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurmaDitados_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasDitados_AlunoId_DitadoId_DataRealizacao",
                table: "RespostasDitados",
                columns: new[] { "AlunoId", "DitadoId", "DataRealizacao" });

            migrationBuilder.CreateIndex(
                name: "IX_TurmaDitados_DitadoId",
                table: "TurmaDitados",
                column: "DitadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_RespostasDitados_Ditados_DitadoId",
                table: "RespostasDitados",
                column: "DitadoId",
                principalTable: "Ditados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RespostasDitados_Usuarios_AlunoId",
                table: "RespostasDitados",
                column: "AlunoId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RespostaSegmentos_RespostasDitados_RespostaDitadoId",
                table: "RespostaSegmentos",
                column: "RespostaDitadoId",
                principalTable: "RespostasDitados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RespostasDitados_Ditados_DitadoId",
                table: "RespostasDitados");

            migrationBuilder.DropForeignKey(
                name: "FK_RespostasDitados_Usuarios_AlunoId",
                table: "RespostasDitados");

            migrationBuilder.DropForeignKey(
                name: "FK_RespostaSegmentos_RespostasDitados_RespostaDitadoId",
                table: "RespostaSegmentos");

            migrationBuilder.DropTable(
                name: "TurmaDitados");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RespostasDitados",
                table: "RespostasDitados");

            migrationBuilder.DropIndex(
                name: "IX_RespostasDitados_AlunoId_DitadoId_DataRealizacao",
                table: "RespostasDitados");

            migrationBuilder.DropColumn(
                name: "AlunoId",
                table: "RespostasDitados");

            migrationBuilder.RenameTable(
                name: "RespostasDitados",
                newName: "RespostaDitados");

            migrationBuilder.RenameIndex(
                name: "IX_RespostasDitados_DitadoId",
                table: "RespostaDitados",
                newName: "IX_RespostaDitados_DitadoId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RespostaDitados",
                table: "RespostaDitados",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RespostaDitados_Ditados_DitadoId",
                table: "RespostaDitados",
                column: "DitadoId",
                principalTable: "Ditados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RespostaSegmentos_RespostaDitados_RespostaDitadoId",
                table: "RespostaSegmentos",
                column: "RespostaDitadoId",
                principalTable: "RespostaDitados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
