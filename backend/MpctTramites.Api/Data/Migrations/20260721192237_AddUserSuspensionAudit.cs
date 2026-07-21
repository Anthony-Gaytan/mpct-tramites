using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpctTramites.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSuspensionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoSuspension",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuspendidoEn",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuspendidoPorId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoSuspension",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SuspendidoEn",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SuspendidoPorId",
                table: "AspNetUsers");
        }
    }
}
