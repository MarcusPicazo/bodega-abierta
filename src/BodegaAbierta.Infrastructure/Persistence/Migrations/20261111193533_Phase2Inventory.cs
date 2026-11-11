using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BodegaAbierta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Inventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Donors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Donors_FoodBanks_FoodBankId",
                        column: x => x.FoodBankId,
                        principalTable: "FoodBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPerishable = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_FoodBanks_FoodBankId",
                        column: x => x.FoodBankId,
                        principalTable: "FoodBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DonationIntakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationIntakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonationIntakes_Donors_DonorId",
                        column: x => x.DonorId,
                        principalTable: "Donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DonationIntakes_FoodBanks_FoodBankId",
                        column: x => x.FoodBankId,
                        principalTable: "FoodBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonationIntakeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InitialQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLots", x => x.Id);
                    table.CheckConstraint("CK_InventoryLots_Quantities", "[InitialQuantity] > 0 AND [QuantityOnHand] >= 0 AND [QuantityOnHand] <= [InitialQuantity]");
                    table.ForeignKey(
                        name: "FK_InventoryLots_DonationIntakes_DonationIntakeId",
                        column: x => x.DonationIntakeId,
                        principalTable: "DonationIntakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_FoodBanks_FoodBankId",
                        column: x => x.FoodBankId,
                        principalTable: "FoodBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonationIntakes_DonorId",
                table: "DonationIntakes",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_DonationIntakes_FoodBankId_ReceivedAt",
                table: "DonationIntakes",
                columns: new[] { "FoodBankId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Donors_FoodBankId_Name",
                table: "Donors",
                columns: new[] { "FoodBankId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_DonationIntakeId",
                table: "InventoryLots",
                column: "DonationIntakeId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_FoodBankId_ProductId_Status_ExpiresOn",
                table: "InventoryLots",
                columns: new[] { "FoodBankId", "ProductId", "Status", "ExpiresOn" })
                .Annotation("SqlServer:Include", new[] { "QuantityOnHand" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_ProductId",
                table: "InventoryLots",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_StorageLocationId",
                table: "InventoryLots",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_FoodBankId_Name",
                table: "Products",
                columns: new[] { "FoodBankId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryLots");

            migrationBuilder.DropTable(
                name: "DonationIntakes");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Donors");
        }
    }
}
