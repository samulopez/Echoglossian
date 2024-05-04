using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echoglossian.EFCoreSqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddObjectivesToQuestPlate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObjectivesAsText",
                table: "questplates",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObjectivesAsText",
                table: "questplates");
        }
    }
}
