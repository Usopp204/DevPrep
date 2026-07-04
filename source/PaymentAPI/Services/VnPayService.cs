using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Services;

public sealed class VnPayService(
    ICreditPackageRepository packageRepo,
    IOrderRepository orderRepo,
    IWalletRepository walletRepo,
    ITransactionRepository txRepo,
    ILedgerRepository ledgerRepo,
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

        if (order.Status == "Paid")
            return $"{webClientUrl}/Payment/Success?orderCode={WebUtility.UrlEncode(orderCode)}&credits={order.Credits}&amount={WebUtility.UrlEncode(order.Amount.ToString("N0") + " VND")}";

        if (!isSuccess)
        {
            order.Status    = "Failed";
            order.UpdatedAt = DateTime.UtcNow.AddHours(7);
            await orderRepo.SaveChangesAsync();
            return $"{webClientUrl}/Payment/Failed?reason={WebUtility.UrlEncode(VnpayError(responseCode))}&orderCode={WebUtility.UrlEncode(orderCode)}";
        }

        var now      = DateTime.UtcNow.AddHours(7);
        order.Status    = "Paid";
        order.PaidAt    = now;
        order.UpdatedAt = now;

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

        var wallet = await walletRepo.GetByUserIdAsync(order.UserId);
        int newBalance;
        if (wallet == null)
        {
            wallet = new CreditWallet
            {
                WalletId  = Guid.NewGuid(),
                UserId    = order.UserId,
                Balance   = order.Credits,
                CreatedAt = now,
                UpdatedAt = now
            };
            await walletRepo.AddAsync(wallet);
            newBalance = order.Credits;
        }
        else
        {
            newBalance       = wallet.Balance + order.Credits;
            wallet.Balance   = newBalance;
            wallet.UpdatedAt = now;
        }

        await ledgerRepo.AddAsync(new CreditLedgerEntry
        {
            LedgerId     = Guid.NewGuid(),
            UserId       = order.UserId,
            EntryType    = "Purchase",
            CreditChange = order.Credits,
            BalanceAfter = newBalance,
            ReferenceId  = order.OrderId,
            Description  = $"Credit package purchase - Order {orderCode}",
            CreatedAt    = now
        });

        await orderRepo.SaveChangesAsync();

        return $"{webClientUrl}/Payment/Success?orderCode={WebUtility.UrlEncode(orderCode)}&credits={order.Credits}&amount={WebUtility.UrlEncode(order.Amount.ToString("N0") + " VND")}";
    }

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
        if (order.Status == "Paid") return new VnPayIpnResult("02", "Order already confirmed");

        order.Status    = responseCode == "00" ? "Paid" : "Failed";
        order.UpdatedAt = DateTime.UtcNow.AddHours(7);
        if (responseCode == "00") order.PaidAt = order.UpdatedAt;
        await orderRepo.SaveChangesAsync();

        return new VnPayIpnResult("00", "Confirm Success");
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
