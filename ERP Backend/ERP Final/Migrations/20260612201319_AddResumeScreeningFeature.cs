using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeScreeningFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResumeScreeningBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalCandidatesMatched = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ScreenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeScreeningBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResumeScreeningBatches_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResumeMatchResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SimilarityScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResumeScreeningBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeMatchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResumeMatchResults_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResumeMatchResults_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ResumeMatchResults_ResumeScreeningBatches_ResumeScreeningBatchId",
                        column: x => x.ResumeScreeningBatchId,
                        principalTable: "ResumeScreeningBatches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResumeMatchResults_CandidateId",
                table: "ResumeMatchResults",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeMatchResults_JobPostingId",
                table: "ResumeMatchResults",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeMatchResults_ResumeScreeningBatchId",
                table: "ResumeMatchResults",
                column: "ResumeScreeningBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeScreeningBatches_JobPostingId",
                table: "ResumeScreeningBatches",
                column: "JobPostingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResumeMatchResults");

            migrationBuilder.DropTable(
                name: "ResumeScreeningBatches");
        }
    }
}
