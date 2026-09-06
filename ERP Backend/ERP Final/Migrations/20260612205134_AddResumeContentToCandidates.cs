using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeContentToCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResumeContent",
                table: "Candidates",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResumeContent",
                table: "Candidates");
        }
    }
}
