using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyShowtime.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiUserArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create new tables first before dropping any columns
            migrationBuilder.CreateTable(
                name: "user_episode",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    episode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    watch_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_episode", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_episode_Episodes_episode_id",
                        column: x => x.episode_id,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_episode_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_media",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    watch_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    hidden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    available_on = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_media", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_media_Media_media_id",
                        column: x => x.media_id,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_media_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    last_selected_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    preferences = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_settings_Media_last_selected_media_id",
                        column: x => x.last_selected_media_id,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_settings_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_episode_episode_id",
                table: "user_episode",
                column: "episode_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_episode_user_id_episode_id",
                table: "user_episode",
                columns: new[] { "user_id", "episode_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_media_media_id",
                table: "user_media",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_media_user_id_media_id",
                table: "user_media",
                columns: new[] { "user_id", "media_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_settings_last_selected_media_id",
                table: "user_settings",
                column: "last_selected_media_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_settings_user_id",
                table: "user_settings",
                column: "user_id",
                unique: true);

            // Migrate existing data to user_media for User 101 (Alice Johnson)
            migrationBuilder.Sql(@"
                INSERT INTO user_media (user_id, media_id, watch_state, priority, hidden, notes, source, available_on, created_at, updated_at)
                SELECT
                    101 as user_id,
                    ""Id"" as media_id,
                    COALESCE(""WatchState"", 'Unwatched') as watch_state,
                    COALESCE(""Priority"", 3) as priority,
                    COALESCE(""Hidden"", false) as hidden,
                    ""Notes"" as notes,
                    ""Source"" as source,
                    ""AvailableOn"" as available_on,
                    ""CreatedAtUtc"" as created_at,
                    COALESCE(""UpdatedAtUtc"", ""CreatedAtUtc"") as updated_at
                FROM ""Media""
                WHERE ""Id"" IS NOT NULL;
            ");

            // Migrate existing episode watch states to user_episode for User 101
            migrationBuilder.Sql(@"
                INSERT INTO user_episode (user_id, episode_id, watch_state, created_at, updated_at)
                SELECT
                    101 as user_id,
                    ""Id"" as episode_id,
                    COALESCE(""WatchState"", 'Unwatched') as watch_state,
                    ""CreatedAtUtc"" as created_at,
                    COALESCE(""UpdatedAtUtc"", ""CreatedAtUtc"") as updated_at
                FROM ""Episodes""
                WHERE ""Id"" IS NOT NULL;
            ");

            // Now drop the old columns after data has been migrated
            migrationBuilder.DropColumn(
                name: "AvailableOn",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Hidden",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "WatchState",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "WatchState",
                table: "Episodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_episode");

            migrationBuilder.DropTable(
                name: "user_media");

            migrationBuilder.DropTable(
                name: "user_settings");

            migrationBuilder.AddColumn<string>(
                name: "AvailableOn",
                table: "Media",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Hidden",
                table: "Media",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Media",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Media",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Media",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WatchState",
                table: "Media",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WatchState",
                table: "Episodes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }
    }
}
