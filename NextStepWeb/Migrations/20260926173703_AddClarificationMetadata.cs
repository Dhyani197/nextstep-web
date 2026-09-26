using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStepWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddClarificationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OptionsJson",
                table: "ClarificationQuestions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Skippable",
                table: "ClarificationQuestions",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptionsJson",
                table: "ClarificationQuestions");

            migrationBuilder.DropColumn(
                name: "Skippable",
                table: "ClarificationQuestions");
        }
    }
}
