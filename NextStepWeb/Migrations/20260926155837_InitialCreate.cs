using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextStepWeb.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Situations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OriginalSituationText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Situations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SituationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Situations_SituationId",
                        column: x => x.SituationId,
                        principalTable: "Situations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SituationVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SituationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    SituationText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AnalysisMode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RawAiResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SituationVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SituationVersions_Situations_SituationId",
                        column: x => x.SituationId,
                        principalTable: "Situations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SituationVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    IsRecommendedNext = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedTime = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Urgency = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionItems_SituationVersions_SituationVersionId",
                        column: x => x.SituationVersionId,
                        principalTable: "SituationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SituationVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnderstandingSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupportGuidance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MisuseExplanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdversarialWarning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorseOutcomeAnalysis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhatChanged = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhatHappenedAfterAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DifferentInformation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhatToReassess = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCalmMode = table.Column<bool>(type: "bit", nullable: false),
                    IsAtRisk = table.Column<bool>(type: "bit", nullable: false),
                    IsMisuse = table.Column<bool>(type: "bit", nullable: false),
                    IsAdversarial = table.Column<bool>(type: "bit", nullable: false),
                    IsWorseAfterAction = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assessments_SituationVersions_SituationVersionId",
                        column: x => x.SituationVersionId,
                        principalTable: "SituationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClarificationQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SituationVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AnswerText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAnswered = table.Column<bool>(type: "bit", nullable: false),
                    IsSkipped = table.Column<bool>(type: "bit", nullable: false),
                    AnsweredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClarificationQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClarificationQuestions_SituationVersions_SituationVersionId",
                        column: x => x.SituationVersionId,
                        principalTable: "SituationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SituationVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    PriorityLevel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsTied = table.Column<bool>(type: "bit", nullable: false),
                    TiedNote = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issues_SituationVersions_SituationVersionId",
                        column: x => x.SituationVersionId,
                        principalTable: "SituationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_SituationVersionId_StepOrder",
                table: "ActionItems",
                columns: new[] { "SituationVersionId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_SituationVersionId",
                table: "Assessments",
                column: "SituationVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SituationId",
                table: "AuditLogs",
                column: "SituationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TimestampUtc",
                table: "AuditLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationQuestions_SituationVersionId",
                table: "ClarificationQuestions",
                column: "SituationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_SituationVersionId_Rank",
                table: "Issues",
                columns: new[] { "SituationVersionId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_Situations_ClientRequestId",
                table: "Situations",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Situations_CreatedAtUtc",
                table: "Situations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SituationVersions_SituationId_VersionNumber",
                table: "SituationVersions",
                columns: new[] { "SituationId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItems");

            migrationBuilder.DropTable(
                name: "Assessments");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ClarificationQuestions");

            migrationBuilder.DropTable(
                name: "Issues");

            migrationBuilder.DropTable(
                name: "SituationVersions");

            migrationBuilder.DropTable(
                name: "Situations");
        }
    }
}
