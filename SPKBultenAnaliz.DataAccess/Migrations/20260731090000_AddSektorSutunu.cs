using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPKBultenAnaliz.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSektorSutunu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sektor",
                table: "BultenAnalizSonuclari",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BultenAnalizSonuclari_Sektor",
                table: "BultenAnalizSonuclari",
                column: "Sektor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BultenAnalizSonuclari_Sektor",
                table: "BultenAnalizSonuclari");

            migrationBuilder.DropColumn(
                name: "Sektor",
                table: "BultenAnalizSonuclari");
        }
    }
}
