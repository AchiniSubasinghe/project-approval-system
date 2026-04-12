using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace project_approval_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class FeatureComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupMembers",
                table: "Proposals",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxProjectCapacity",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupMembers",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "MaxProjectCapacity",
                table: "AspNetUsers");
        }
    }
}
