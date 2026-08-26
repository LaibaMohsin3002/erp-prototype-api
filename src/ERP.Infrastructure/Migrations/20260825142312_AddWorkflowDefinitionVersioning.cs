using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowDefinitionVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLatestVersion",
                table: "WorkflowDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreviousVersionId",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_PreviousVersionId",
                table: "WorkflowDefinitions",
                column: "PreviousVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDefinitions_WorkflowDefinitions_PreviousVersionId",
                table: "WorkflowDefinitions",
                column: "PreviousVersionId",
                principalTable: "WorkflowDefinitions",
                principalColumn: "WorkflowDefinitionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDefinitions_WorkflowDefinitions_PreviousVersionId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_PreviousVersionId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "IsLatestVersion",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "PreviousVersionId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "WorkflowDefinitions");
        }
    }
}
