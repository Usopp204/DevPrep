namespace PaymentAPI.Services;

public record VnPayIpnResult(string RspCode, string Message);

public interface IVnPayService
{
    Task<string> CreateOrderUrlAsync(Guid userId, Guid packageId, string ipAddress);
    Task<string> ProcessReturnAsync(IReadOnlyDictionary<string, string> query);
    Task<VnPayIpnResult> ProcessIpnAsync(IReadOnlyDictionary<string, string> query);
}
