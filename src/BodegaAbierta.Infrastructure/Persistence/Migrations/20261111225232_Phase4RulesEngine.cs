using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BodegaAbierta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4RulesEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Beneficiaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beneficiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Beneficiaries_FoodBanks_FoodBankId",
                        column: x => x.FoodBankId,
                        principalTable: "FoodBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PickupSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    ReservedCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupSlots", x => x.Id);
                    table.CheckConstraint("CK_PickupSlots_Capacity", "[Capacity] >= 1 AND [ReservedCount] >= 0 AND [ReservedCount] <= [Capacity]");
                    table.CheckConstraint("CK_PickupSlots_Window", "[EndsAt] > [StartsAt]");
                    table.ForeignKey(
                        name: "FK_PickupSlots_FoodBanks_FoodBankId",
                        column: x => x.FoodBankId,
                        principalTable: "FoodBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Distributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PickupSlotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Distributions_Beneficiaries_BeneficiaryId",
                        column: x => x.BeneficiaryId,
                        principalTable: "Beneficiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Distributions_FoodBanks_FoodBankId",
                        column: x => x.FoodBankId,
                        principalTable: "FoodBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Distributions_PickupSlots_PickupSlotId",
                        column: x => x.PickupSlotId,
                        principalTable: "PickupSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Distributions_PolicySets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalTable: "PolicySets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DistributionAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DistributionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionAllocations", x => x.Id);
                    table.CheckConstraint("CK_DistributionAllocations_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_DistributionAllocations_Distributions_DistributionId",
                        column: x => x.DistributionId,
                        principalTable: "Distributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DistributionAllocations_InventoryLots_InventoryLotId",
                        column: x => x.InventoryLotId,
                        principalTable: "InventoryLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DistributionAllocations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_FoodBankId_Name",
                table: "Beneficiaries",
                columns: new[] { "FoodBankId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionAllocations_DistributionId",
                table: "DistributionAllocations",
                column: "DistributionId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionAllocations_InventoryLotId",
                table: "DistributionAllocations",
                column: "InventoryLotId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionAllocations_ProductId",
                table: "DistributionAllocations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Distributions_BeneficiaryId",
                table: "Distributions",
                column: "BeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_Distributions_FoodBankId_BeneficiaryId_ScheduledFor",
                table: "Distributions",
                columns: new[] { "FoodBankId", "BeneficiaryId", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_Distributions_PickupSlotId",
                table: "Distributions",
                column: "PickupSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Distributions_PolicySetId",
                table: "Distributions",
                column: "PolicySetId");

            migrationBuilder.CreateIndex(
                name: "IX_PickupSlots_FoodBankId_StartsAt",
                table: "PickupSlots",
                columns: new[] { "FoodBankId", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributionAllocations");

            migrationBuilder.DropTable(
                name: "Distributions");

            migrationBuilder.DropTable(
                name: "Beneficiaries");

            migrationBuilder.DropTable(
                name: "PickupSlots");
        }
    }
}
