using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DktApi.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationAndConnectionRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invitation_codes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    therapist_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    used_by_player_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitation_codes_players_used_by_player_id",
                        column: x => x.used_by_player_id,
                        principalTable: "players",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_invitation_codes_therapists_therapist_id",
                        column: x => x.therapist_id,
                        principalTable: "therapists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "connection_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    therapist_id = table.Column<long>(type: "bigint", nullable: false),
                    invitation_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_connection_requests_invitation_codes_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "invitation_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_connection_requests_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_connection_requests_therapists_therapist_id",
                        column: x => x.therapist_id,
                        principalTable: "therapists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_connection_requests_invitation_id",
                table: "connection_requests",
                column: "invitation_id");

            migrationBuilder.CreateIndex(
                name: "IX_connection_requests_player_id_therapist_id",
                table: "connection_requests",
                columns: new[] { "player_id", "therapist_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_connection_requests_therapist_id",
                table: "connection_requests",
                column: "therapist_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_codes_code",
                table: "invitation_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitation_codes_therapist_id",
                table: "invitation_codes",
                column: "therapist_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_codes_used_by_player_id",
                table: "invitation_codes",
                column: "used_by_player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connection_requests");

            migrationBuilder.DropTable(
                name: "invitation_codes");
        }
    }
}
