using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Services;

public sealed class VnPayService(
    ICreditPackageRepository packageRepo,
    IOrderRepository orderRepo,
    ITransactionRepository txRepo,
    ILedgerRepository ledgerRepo,
    PaymentDbContext db,
    IConfiguration config) : IVnPayService
{
    public async Task<string> CreateOrderUrlAsync(Guid userId, Guid packageId, string ipAddress)
    {
        var package = await packageRepo.GetByIdAsync(packageId);
        if (package == null || !package.IsActive)
            throw new InvalidOperationException("Package not found or inactive.");

        var now          = DateTime.UtcNow.AddHours(7);
        var orderCode    = $"DP{now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        var totalCredits = package.Credits + package.BonusCredits;

        var order = new Order
        {
            OrderId       = Guid.NewGuid(),
            OrderCode     = orderCode,
            UserId        = userId,
            PackageId     = package.PackageId,
            Amount        = package.Price,
            Credits       = totalCredits,
            Currency      = package.Currency,
            PaymentMethod = "VNPay",
            Status        = "Pending",
            ExpiredAt     = now.AddMinutes(30),
            CreatedAt     = now,
            UpdatedAt     = now
        };
        await orderRepo.AddAsync(order);
        await orderRepo.SaveChangesAsync();

        var tmnCode    = config["VNPay:TmnCode"]!;
        var hashSecret = config["VNPay:HashSecret"]!;
        var vnpUrl     = config["VNPay:PaymentUrl"]!;
        var returnUrl  = config["VNPay:ReturnUrl"]!;

        var vnpParams = new SortedDictionary<string, string>
        {
            { "vnp_Version",    "2.1.0" },
            { "vnp_Command",    "pay" },
            { "vnp_TmnCode",    tmnCode },
            { "vnp_Amount",     ((long)(package.Price * 100)).ToString() },
            { "vnp_CurrCode",   "VND" },
            { "vnp_TxnRef",     orderCode },
            { "vnp_OrderInfo",  $"Payment for order {orderCode}" },
            { "vnp_OrderType",  "other" },
            { "vnp_Locale",     "vn" },
            { "vnp_ReturnUrl",  returnUrl },
            { "vnp_IpAddr",     ipAddress },
            { "vnp_CreateDate", now.ToString("yyyyMMddHHmmss") },
            { "vnp_ExpireDate", now.AddMinutes(30).ToString("yyyyMMddHHmmss") }
        };

        var encodedQuery = string.Join("&", vnpParams.Select(kv =>
            $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret));
        var secureHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedQuery))).ToLower();

        return $"{vnpUrl}?{encodedQuery}&vnp_SecureHash={secureHash}";
    }

    public async Task<string> ProcessReturnAsync(IReadOnlyDictionary<string, string> query)
    {
        var webClientUrl = config["WebClientUrl"] ?? "http://localhost:5130";
        var secureHash   = query.GetValueOrDefault("vnp_SecureHash", "").ToLower();
        var hashSecret   = config["VNPay:HashSecret"]!;

        var rawHash = string.Join("&", query
            .Where(kv => kv.Key.StartsWith("vnp_") && kv.Key != "vnp_SecureHash")
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

        using var hmac  = new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret));
        var computed    = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawHash))).ToLower();
        var isValid     = computed == secureHash;

        var orderCode    = query.GetValueOrDefault("vnp_TxnRef", "");
        var responseCode = query.GetValueOrDefault("vnp_ResponseCode", "");
        var isSuccess    = isValid && responseCode == "00";

        var order = await orderRepo.GetByOrderCodeAsync(orderCode);
        if (order == null)
            return $"{webClientUrl}/Payment/Failed?reason={WebUtility.UrlEncode("Order not found")}&orderCode={WebUtility.UrlEncode(orderCode)}";

        // Đơn đã ở trạng thái Paid KHÔNG có nghĩa là credit đã được cộng: IPN chạy trước có thể
        // đã đánh dấu Paid mà chưa cộng. Nên vẫn đi tiếp vào ConfirmPaidAsync - hàm đó tự bỏ qua
        // những phần đã làm rồi.
        if (order.Status == "Paid" || isSuccess)
        {
            await ConfirmPaidAsync(order, query, responseCode, isValid);
            return $"{webClientUrl}/Payment/Success?orderCode={WebUtility.UrlEncode(orderCode)}&credits={order.Credits}&amount={WebUtility.UrlEncode(order.Amount.ToString("N0") + " VND")}";
        }

        order.Status    = "Failed";
        order.UpdatedAt = DateTime.UtcNow.AddHours(7);
        await orderRepo.SaveChangesAsync();
        return $"{webClientUrl}/Payment/Failed?reason={WebUtility.UrlEncode(VnpayError(responseCode))}&orderCode={WebUtility.UrlEncode(orderCode)}";
    }

    /// <summary>
    /// Xác nhận một đơn đã thanh toán: đánh dấu Paid, ghi PaymentTransaction, cộng credit.
    ///
    /// VNPay báo kết quả qua HAI đường độc lập - trình duyệt quay về (ProcessReturnAsync) và
    /// gọi server-to-server (ProcessIpnAsync). Đường nào về trước cũng phải cộng credit, và
    /// đường về sau không được cộng thêm lần nữa. Vì vậy cả ba việc ở đây đều kiểm tra trước
    /// khi làm, và toàn bộ nằm trong một transaction.
    ///
    /// Số dư đọc bằng GetBalanceForUpdateAsync (UPDLOCK, HOLDLOCK): hai đường về đúng cùng lúc
    /// thì đường thứ hai phải đợi, đọc lại và thấy dòng Purchase đã có nên bỏ qua.
    /// </summary>
    private async Task ConfirmPaidAsync(Order order, IReadOnlyDictionary<string, string> query,
                                        string responseCode, bool isValid)
    {
        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow.AddHours(7);

            if (order.Status != "Paid")
            {
                order.Status = "Paid";
                order.PaidAt = now;
                order.UpdatedAt = now;
            }

            if (await txRepo.GetByOrderIdAsync(order.OrderId) is null)
            {
                await txRepo.AddAsync(new PaymentTransaction
                {
                    TransactionId    = Guid.NewGuid(),
                    OrderId          = order.OrderId,
                    Amount           = order.Amount,
                    VnpTransactionNo = query.GetValueOrDefault("vnp_TransactionNo", ""),
                    VnpBankCode      = query.GetValueOrDefault("vnp_BankCode", ""),
                    VnpResponseCode  = responseCode,
                    VnpPayDate       = query.GetValueOrDefault("vnp_PayDate", ""),
                    SecureHashValid  = isValid,
                    Status           = "Success",
                    RawCallbackJson  = JsonSerializer.Serialize(query),
                    CreatedAt        = now
                });
            }

            // Số dư suy ra từ sổ cái (CreditWallets đã bỏ). Ghi dòng Purchase là cộng credit.
            //
            // Lấy khoá TRƯỚC rồi mới kiểm tra đã cộng chưa - không được đảo thứ tự. Kiểm tra
            // trước thì hai đường về cùng lúc đều đọc thấy "chưa cộng" rồi mới lần lượt vào
            // khoá, và đường thứ hai vẫn ghi thêm một dòng. Khoá trước thì đường thứ hai phải
            // đợi, đọc lại và thấy dòng Purchase đường thứ nhất vừa ghi.
            var balanceTruocDo = await ledgerRepo.GetBalanceForUpdateAsync(order.UserId);

            if (!await ledgerRepo.HasPurchaseForOrderAsync(order.OrderId))
            {
                var newBalance = balanceTruocDo + order.Credits;

                await ledgerRepo.AddAsync(new CreditLedgerEntry
                {
                    LedgerId     = Guid.NewGuid(),
                    UserId       = order.UserId,
                    EntryType    = "Purchase",
                    CreditChange = order.Credits,
                    BalanceAfter = newBalance,
                    ReferenceId  = order.OrderId,
                    Description  = $"Credit package purchase - Order {order.OrderCode}",
                    CreatedAt    = now
                });
            }

            await orderRepo.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Đường còn lại đã cộng credit xong trước ta trong gang tấc - UX_Ledger_PurchaseOrder
            // chặn lại. Không phải lỗi: kết quả cuối cùng vẫn đúng, credit đã được cộng một lần.
            await transaction.RollbackAsync();
        }
    }

    // 2601 = trùng trên unique index, 2627 = trùng trên unique constraint.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);

    public async Task<VnPayIpnResult> ProcessIpnAsync(IReadOnlyDictionary<string, string> query)
    {
        var secureHash = query.GetValueOrDefault("vnp_SecureHash", "").ToLower();
        var hashSecret = config["VNPay:HashSecret"]!;

        var rawHash = string.Join("&", query
            .Where(kv => kv.Key.StartsWith("vnp_") && kv.Key != "vnp_SecureHash")
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret));
        var computed   = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawHash))).ToLower();

        if (computed != secureHash)
            return new VnPayIpnResult("97", "Invalid signature");

        var orderCode    = query.GetValueOrDefault("vnp_TxnRef", "");
        var responseCode = query.GetValueOrDefault("vnp_ResponseCode", "");

        var order = await orderRepo.GetByOrderCodeAsync(orderCode);
        if (order == null) return new VnPayIpnResult("01", "Order not found");

        if (responseCode != "00")
        {
            if (order.Status == "Paid") return new VnPayIpnResult("02", "Order already confirmed");
            order.Status    = "Failed";
            order.UpdatedAt = DateTime.UtcNow.AddHours(7);
            await orderRepo.SaveChangesAsync();
            return new VnPayIpnResult("00", "Confirm Success");
        }

        // Trước đây nhánh này chỉ đánh dấu Paid mà KHÔNG cộng credit. Nếu IPN về trước trình
        // duyệt thì ProcessReturnAsync thấy Status='Paid' rồi thoát sớm, và user mất trắng số
        // credit đã mua. Giờ cả hai đường cùng gọi ConfirmPaidAsync, hàm đó tự chống cộng trùng.
        var daCongTruocDo = order.Status == "Paid"
                            && await ledgerRepo.HasPurchaseForOrderAsync(order.OrderId);

        await ConfirmPaidAsync(order, query, responseCode, true);

        return daCongTruocDo
            ? new VnPayIpnResult("02", "Order already confirmed")
            : new VnPayIpnResult("00", "Confirm Success");
    }

    private static string VnpayError(string code) => code switch
    {
        "07" => "Transaction flagged as suspicious. Please contact your bank.",
        "09" => "Card has not been registered for Internet Banking.",
        "10" => "Card authentication failed too many times.",
        "11" => "Payment session has expired.",
        "12" => "Card is locked.",
        "13" => "Incorrect OTP entered.",
        "24" => "Transaction cancelled by customer.",
        "51" => "Insufficient account balance.",
        "65" => "Daily transaction limit exceeded.",
        "75" => "Bank is under maintenance.",
        "79" => "Payment password entered incorrectly too many times.",
        _    => $"Transaction failed. Error code: {code}"
    };
}
