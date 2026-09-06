using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERPSystem.Services.Chatbot;
using ERPSystem.Services.HRChatbot;
using ERPSystem.Services.ResumeMatching;

/// <summary>
/// Defines the i procurement service service contract.
/// </summary>
public interface IProcurementService
{
    Task<Result<SupplierDto>> CreateSupplierAsync(CreateSupplierRequest request);
    Task<Result<List<SupplierDto>>> GetAllSuppliersAsync();
    Task<Result<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request);
    Task<Result<List<PurchaseOrderDto>>> GetAllPurchaseOrdersAsync(PurchaseOrderStatus? status = null);
    Task<Result> ApprovePurchaseOrderAsync(Guid orderId, Guid approvedBy);
    Task<Result<GoodsReceiptDto>> CreateGoodsReceiptAsync(Guid purchaseOrderId, Guid warehouseId, List<GoodsReceiptItemRequest> items, string? receivedBy);
    Task<Result<List<GoodsReceiptDto>>> GetGoodsReceiptsAsync(Guid? purchaseOrderId = null);
    Task<Result<PagedResult<PurchaseOrderDto>>> SearchPurchaseOrdersAsync(PurchaseOrderSearchRequest search, PaginationRequest pagination);
    Task<Result<SupplierDto>> GetSupplierByIdAsync(Guid id);
    Task<Result<SupplierDto>> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request);
    Task<Result> DeactivateSupplierAsync(Guid id);
    Task<Result<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(Guid id);
    Task<Result<PurchaseOrderDto>> UpdatePurchaseOrderAsync(Guid id, UpdatePurchaseOrderRequest request);
    Task<Result> DeletePurchaseOrderAsync(Guid id);
    Task<Result> CancelPurchaseOrderAsync(Guid id);
    Task<Result<GoodsReceiptDto>> GetGoodsReceiptByIdAsync(Guid id);
    Task<Result> DeleteGoodsReceiptAsync(Guid id);
}




