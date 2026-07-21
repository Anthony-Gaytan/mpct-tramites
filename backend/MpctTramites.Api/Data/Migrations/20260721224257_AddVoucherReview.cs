using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpctTramites.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoRevision",
                table: "Pagos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisadoPorId",
                table: "Pagos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherNombre",
                table: "Pagos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherRuta",
                table: "Pagos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoRevision",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "RevisadoPorId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "VoucherNombre",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "VoucherRuta",
                table: "Pagos");
        }
    }
}
