using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NaderProductsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnTotalToCashierInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cashierinvoices",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoicedate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paymentmethod = table.Column<string>(type: "text", nullable: false),
                    customername = table.Column<string>(type: "text", nullable: true),
                    customerphone = table.Column<string>(type: "text", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    discounttotal = table.Column<decimal>(type: "numeric", nullable: false),
                    vattotal = table.Column<decimal>(type: "numeric", nullable: false),
                    grandtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    ReturnTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    issuspended = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cashierinvoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Barcode = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsVatIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    MinQuantity = table.Column<int>(type: "integer", nullable: false),
                    OfferEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OfferStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfferEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfferName = table.Column<string>(type: "text", nullable: true),
                    OfferPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    OfferVatIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    SoldQuantity = table.Column<int>(type: "integer", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cashierinvoiceitems",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoiceid = table.Column<int>(type: "integer", nullable: false),
                    productname = table.Column<string>(type: "text", nullable: true),
                    barcode = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    discount = table.Column<decimal>(type: "numeric", nullable: false),
                    taxincluded = table.Column<bool>(type: "boolean", nullable: false),
                    hasoffer = table.Column<bool>(type: "boolean", nullable: false),
                    offername = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cashierinvoiceitems", x => x.id);
                    table.ForeignKey(
                        name: "FK_cashierinvoiceitems_cashierinvoices_invoiceid",
                        column: x => x.invoiceid,
                        principalTable: "cashierinvoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cashierinvoiceitems_invoiceid",
                table: "cashierinvoiceitems",
                column: "invoiceid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cashierinvoiceitems");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "cashierinvoices");
        }
    }
}
