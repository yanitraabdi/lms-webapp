using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M3Billing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "planned_plan_id",
                table: "subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_ref",
                table: "payment_transactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_planned_plan_id",
                table: "subscriptions",
                column: "planned_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_provider_ref",
                table: "payment_transactions",
                column: "provider_ref");

            migrationBuilder.AddForeignKey(
                name: "fk_subscriptions_plans_planned_plan_id",
                table: "subscriptions",
                column: "planned_plan_id",
                principalTable: "plans",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_subscriptions_plans_planned_plan_id",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_subscriptions_planned_plan_id",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_payment_transactions_provider_ref",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "planned_plan_id",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "provider_ref",
                table: "payment_transactions");
        }
    }
}
