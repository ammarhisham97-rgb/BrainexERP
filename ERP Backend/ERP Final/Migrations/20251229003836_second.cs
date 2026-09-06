using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP_Final.Migrations
{
    /// <inheritdoc />
    public partial class second : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_ProjectId",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_CustomerId",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Leaves_EmployeeId",
                table: "Leaves");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Payments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Invoices",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_ProjectId_EmployeeId_Date",
                table: "TimeEntries",
                columns: new[] { "ProjectId", "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_MovementNumber",
                table: "StockMovements",
                column: "MovementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId_WarehouseId_MovementDate",
                table: "StockMovements",
                columns: new[] { "ProductId", "WarehouseId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerId_Status_OrderDate",
                table: "SalesOrders",
                columns: new[] { "CustomerId", "Status", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_OrderNumber",
                table: "SalesOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OrderNumber",
                table: "PurchaseOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId_Status_OrderDate",
                table: "PurchaseOrders",
                columns: new[] { "SupplierId", "Status", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId_Status_PaymentDate",
                table: "Payments",
                columns: new[] { "InvoiceId", "Status", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentNumber",
                table: "Payments",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leaves_EmployeeId_Status_StartDate",
                table: "Leaves",
                columns: new[] { "EmployeeId", "Status", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId_Status_InvoiceDate",
                table: "Invoices",
                columns: new[] { "CustomerId", "Status", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_ReceiptNumber",
                table: "GoodsReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_DeliveryNumber",
                table: "Deliveries",
                column: "DeliveryNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId_Date",
                table: "Attendances",
                columns: new[] { "EmployeeId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_ProjectId_EmployeeId_Date",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_MovementNumber",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductId_WarehouseId_MovementDate",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_CustomerId_Status_OrderDate",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_OrderNumber",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_OrderNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_SupplierId_Status_OrderDate",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_Payments_InvoiceId_Status_PaymentDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentNumber",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Leaves_EmployeeId_Status_StartDate",
                table: "Leaves");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CustomerId_Status_InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_ReceiptNumber",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_DeliveryNumber",
                table: "Deliveries");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_EmployeeId_Date",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_ProjectId",
                table: "TimeEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId",
                table: "StockMovements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerId",
                table: "SalesOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Leaves_EmployeeId",
                table: "Leaves",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances",
                column: "EmployeeId");
        }
    }
}
