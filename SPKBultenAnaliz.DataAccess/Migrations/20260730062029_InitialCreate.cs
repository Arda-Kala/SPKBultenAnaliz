using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPKBultenAnaliz.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bultenler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BultenAdi = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    YayinTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PdfUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    GenelYoneticiOzeti = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KayitTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Beklemede")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bultenler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BultenAnalizSonuclari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BultenId = table.Column<int>(type: "int", nullable: false),
                    EtkilenenUnsur = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DuyguDurumu = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EtkiSkoru = table.Column<int>(type: "int", nullable: false),
                    AiGerekceYorumu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BultenAnalizSonuclari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BultenAnalizSonuclari_Bultenler_BultenId",
                        column: x => x.BultenId,
                        principalTable: "Bultenler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BultenMetinBloklari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BultenId = table.Column<int>(type: "int", nullable: false),
                    BlokSiraNo = table.Column<int>(type: "int", nullable: false),
                    HamMetinIcerigi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KayitTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BultenMetinBloklari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BultenMetinBloklari_Bultenler_BultenId",
                        column: x => x.BultenId,
                        principalTable: "Bultenler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BultenAnalizSonuclari_BultenId",
                table: "BultenAnalizSonuclari",
                column: "BultenId");

            migrationBuilder.CreateIndex(
                name: "IX_Bultenler_PdfUrl",
                table: "Bultenler",
                column: "PdfUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bultenler_YayinTarihi",
                table: "Bultenler",
                column: "YayinTarihi");

            migrationBuilder.CreateIndex(
                name: "IX_BultenMetinBloklari_BultenId_BlokSiraNo",
                table: "BultenMetinBloklari",
                columns: new[] { "BultenId", "BlokSiraNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BultenAnalizSonuclari");

            migrationBuilder.DropTable(
                name: "BultenMetinBloklari");

            migrationBuilder.DropTable(
                name: "Bultenler");
        }
    }
}
