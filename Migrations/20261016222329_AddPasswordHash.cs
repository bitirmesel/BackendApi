using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DktApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password",
                table: "therapists",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "therapists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "players",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "therapists");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "players");

            migrationBuilder.AlterColumn<string>(
                name: "password",
                table: "therapists",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
