using Microsoft.AspNetCore.Mvc;
using SLICE_Website.Data;
using SLICE_Website.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;           // Required for Z-Reading
using Dapper;                             // Required for Z-Reading
using Microsoft.Extensions.Configuration;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesController : ControllerBase
    {
        private readonly SalesRepository _salesRepo;
        private readonly DiscountRepository _discountRepo;
        private readonly string _connectionString;

        public SalesController(SalesRepository salesRepo, DiscountRepository discountRepo, IConfiguration config)
        {
            _salesRepo = salesRepo;
            _discountRepo = discountRepo;
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "YourDatabaseConnectionString";
        }

        [HttpGet("menu/{branchId}")]
        public IActionResult GetMenu(int branchId)
        {
            try
            {
                var rawList = _salesRepo.GetMenu(branchId);
                var displayList = rawList.Select(x => new ProductDisplayDto
                {
                    ProductID = x.ProductID,
                    RawName = x.ProductName,
                    BasePrice = x.BasePrice,
                    MaxCookable = x.MaxCookable,
                    ImagePath = x.ImagePath
                }).ToList();

                return Ok(displayList);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("discounts/{role}")]
        public IActionResult GetAvailableDiscounts(string role)
        {
            try
            {
                var discounts = _discountRepo.GetAvailableDiscounts(role);
                return Ok(discounts);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("checkout")]
        public IActionResult Checkout([FromBody] CheckoutRequest request)
        {
            try
            {
                string errorMessage;
                var cartItems = request.Cart.Select(c => new CartItem
                {
                    ProductID = c.ProductID,
                    ProductName = c.RawName,
                    Qty = c.Qty,
                    Price = c.BasePrice
                }).ToList();

                bool success = _salesRepo.CompleteSale(
                    request.BranchID,
                    request.UserID,
                    cartItems,
                    request.PaymentMethod,
                    request.ReferenceNumber,
                    request.GrandTotal,
                    out errorMessage);

                if (success)
                {
                    if (request.DiscountID.HasValue && request.DiscountAmount > 0)
                    {
                        _discountRepo.LogAppliedDiscount(
                            request.BranchID,
                            request.DiscountID.Value,
                            request.UserID,
                            request.DiscountAmount,
                            request.ReferenceNumber,
                            "POS Sale");
                    }
                    return Ok(new { Message = "Sale Completed" });
                }
                else
                {
                    return BadRequest(errorMessage);
                }
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ========================================================
        // REVERTED: Using your safe Repository method!
        // ========================================================
        [HttpGet("today/{branchId}")]
        public IActionResult GetTodaySales(int branchId)
        {
            try
            {
                var todaySales = _salesRepo.GetTodayTransactions(branchId);
                return Ok(todaySales);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ========================================================
        // POST to match the new Modal Auth Payload
        // ========================================================
        [HttpPost("void")]
        public IActionResult VoidTransaction([FromBody] VoidRequestPayload payload)
        {
            try
            {
                _salesRepo.VoidTransaction(payload.ReferenceNumber);
                return Ok(new { Message = "Transaction successfully voided." });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ========================================================
        // ADDED FOR Z-READING RECONCILIATION MODAL
        // ========================================================
        [HttpGet("expectedcash/{userId}")]
        public IActionResult GetExpectedCash(int userId)
        {
            try
            {
                using (var db = new SqlConnection(_connectionString))
                {
                    string sql = @"
                        SELECT ISNULL(SUM(GrandTotal), 0) 
                        FROM Sales 
                        WHERE UserID = @UserID 
                          AND PaymentMethod = 'Cash' 
                          AND Status = 'Completed'
                          AND CAST(TransactionDate AS DATE) = CAST(GETDATE() AS DATE)";

                    decimal expected = db.QuerySingle<decimal>(sql, new { UserID = userId });
                    return Ok(expected);
                }
            }
            catch
            {
                // Silently return 0 so the logout process is never broken
                return Ok(0m);
            }
        }
    }

    // ========================================================
    // DTOs
    // ========================================================
    public class ProductDisplayDto
    {
        public int ProductID { get; set; }
        public string RawName { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public int MaxCookable { get; set; }
        public string ImagePath { get; set; } = string.Empty;
    }

    public class CheckoutRequest
    {
        public int BranchID { get; set; }
        public int UserID { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public int? DiscountID { get; set; }
        public decimal DiscountAmount { get; set; }
        public List<SalesCartItemDto> Cart { get; set; } = new();
    }

    public class SalesCartItemDto
    {
        public int ProductID { get; set; }
        public string RawName { get; set; } = string.Empty;
        public int Qty { get; set; }
        public decimal BasePrice { get; set; }
    }

    public class TransactionDto
    {
        public string ReferenceNumber { get; set; } = string.Empty;

        // Catch all possible date column names
        public System.DateTime? LocalTransactionDate { get; set; }
        public System.DateTime? TransactionDate { get; set; }

        // Catch all possible product names
        public string ProductName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;

        // Catch all possible quantity names
        public int? QuantitySold { get; set; }
        public int? Qty { get; set; }

        // Catch all possible amount names
        public decimal? TotalAmount { get; set; }
        public decimal? GrandTotal { get; set; }

        // Catch all possible method names
        public string PaymentMethod { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        // --- COMPUTED PROPERTIES FOR THE UI ---
        // These automatically grab whichever value successfully loaded from the database!
        public System.DateTime DisplayDate => LocalTransactionDate ?? TransactionDate ?? System.DateTime.Now;
        public decimal DisplayAmount => TotalAmount ?? GrandTotal ?? 0m;
        public int DisplayQty => QuantitySold ?? Qty ?? 0;
        public string DisplayProduct => !string.IsNullOrEmpty(ProductName) ? ProductName : (!string.IsNullOrEmpty(ItemName) ? ItemName : "Order Item(s)");
        public string DisplayMethod => !string.IsNullOrEmpty(PaymentMethod) ? PaymentMethod : (!string.IsNullOrEmpty(Method) ? Method : "Cash");
    }

    public class VoidRequestPayload
    {
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public int ManagerID { get; set; }
    }
}