using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPKBultenAnaliz.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddVadeHisseKodu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Vade",
                table: "BultenAnalizSonuclari",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HisseKodu",
                table: "BultenAnalizSonuclari",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BultenAnalizSonuclari_HisseKodu",
                table: "BultenAnalizSonuclari",
                column: "HisseKodu");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BultenAnalizSonuclari_HisseKodu",
                table: "BultenAnalizSonuclari");

            migrationBuilder.DropColumn(
                name: "Vade",
                table: "BultenAnalizSonuclari");

            migrationBuilder.DropColumn(
                name: "HisseKodu",
                table: "BultenAnalizSonuclari");
        }
    }
}
