namespace WebClient.Services.Shared;

// Extracts the account id (JWT "sub" claim) from the access_token cookie. This is the
// server-side source of truth for "who is the current user" — PaymentController used to read a
// "user_id" cookie that nothing in the app ever set, so every CreateOrder call failed. Always
// derive the user id from the token instead of trusting a separate cookie.
public static class JwtUserId
{
    public static Guid? FromToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        var parts = token.Split('.');
        if (parts.Length < 2) return null;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(payload);

            using var doc = System.Text.Json.JsonDocument.Parse(bytes);
            var sub = doc.RootElement.TryGetProperty("sub", out var s) ? s.GetString() : null;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
