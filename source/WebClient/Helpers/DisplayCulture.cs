using System.Globalization;

namespace WebClient.Helpers;

// Fixed cultures for anything rendered to the page.
//
// ToString() without an explicit culture follows CultureInfo.CurrentCulture, which on Windows comes
// from the host's regional format rather than from the app. A developer whose machine is set to
// Vietnamese renders "thg 8 2, 2026" and "1.000.000" where an English-configured machine renders
// "Aug 2, 2026" and "1,000,000" from the very same code, so the site looked different depending on
// who ran it. Pinning the culture at the call site keeps output identical everywhere, including on a
// Linux host, without touching CurrentCulture and so without side effects on unrelated code.
public static class DisplayCulture
{
    // VND amounts. Vietnamese grouping: 1.000.000 - matches the Intl.NumberFormat('vi-VN') already
    // used by the manager payment and credit-package screens.
    public static readonly CultureInfo Money = CultureInfo.GetCultureInfo("vi-VN");

    // Everything else: dates, counts and credit balances. Invariant follows en-US conventions, so
    // "Aug 2, 2026" and "1,000" - matching the toLocaleString('en-US') used for credit balances.
    public static readonly CultureInfo Neutral = CultureInfo.InvariantCulture;
}
