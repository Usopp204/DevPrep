using System.Text.Json;
using PaymentAPI.Services;

namespace PaymentAPI.Endpoints;

/// <summary>
/// The three routes VNPay needs: create the payment URL, receive the buyer's browser back,
/// and receive VNPay's own server-to-server confirmation.
/// </summary>
/// <remarks>
/// Moved out of Program.cs — see the note on <see cref="CreditPackageEndpoints"/>.
///
/// Two callbacks rather than one, and they are not interchangeable. /vnpay/return is the
/// browser landing back on the site and only decides where to redirect the person; it can be
/// missed entirely if they close the tab. /vnpay/ipn is VNPay's server calling ours and is the
/// one that must be treated as authoritative, which is why it answers with VNPay's own
/// RspCode/Message envelope rather than an HTTP status the gateway might rewrite.
/// </remarks>
public static class VnPayEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static void MapVnPayEndpoints(this WebApplication app)
    {
        app.MapPost("/vnpay/order", CreateOrderAsync);
        app.MapGet("/vnpay/return", ProcessReturnAsync);
        app.MapGet("/vnpay/ipn", ProcessIpnAsync);
    }

    public static async Task<IResult> CreateOrderAsync(HttpContext ctx, IVnPayService vnPay)
    {
            // ApiGateway puts the caller's id here after validating the JWT; the service never
            // reads an id out of the request body, which a caller could set to anyone.
            var userIdStr = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.BadRequest("Missing or invalid X-User-Id header.");

            VnpayOrderRequest? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<VnpayOrderRequest>(ctx.Request.Body, JsonOpts);
            }
            catch { return Results.BadRequest("Invalid request body."); }
            if (body == null) return Results.BadRequest("Invalid request body.");

            var log = ctx.RequestServices.GetRequiredService<ILogger<VnPayEndpointsLog>>();
            log.LogInformation("CreateOrder: userId={UserId} packageId={PackageId}", userId, body.PackageId);
            try
            {
                var ipAddr = ctx.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
                var paymentUrl = await vnPay.CreateOrderUrlAsync(userId, body.PackageId, ipAddr);
                return Results.Ok(new { PaymentUrl = paymentUrl });
            }
            catch (InvalidOperationException ex)
            {
                // Thrown when the package id does not resolve — a client mistake, not a fault.
                log.LogWarning("CreateOrder 404: {Message}", ex.Message);
                return Results.NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "CreateOrder failed for user {UserId} package {PackageId}", userId, body.PackageId);
                return Results.Problem(ex.Message);
            }
    }

    public static async Task<IResult> ProcessReturnAsync(HttpRequest req, IVnPayService vnPay)
    {
        var query = req.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        var redirectUrl = await vnPay.ProcessReturnAsync(query);
        return Results.Redirect(redirectUrl);
    }

    public static async Task<IResult> ProcessIpnAsync(HttpRequest req, IVnPayService vnPay)
    {
        var query = req.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        var result = await vnPay.ProcessIpnAsync(query);
        return Results.Json(new { RspCode = result.RspCode, Message = result.Message });
    }

    /// <summary>Category for the logger above; a static class cannot be a generic type argument.</summary>
    private sealed class VnPayEndpointsLog { }
}

public record VnpayOrderRequest(Guid PackageId);
