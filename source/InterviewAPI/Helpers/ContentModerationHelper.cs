using System.Globalization;
using System.Text;

namespace InterviewAPI.Helpers;

/// <summary>
/// Server-side content filter for interview answers (the real enforcement layer — the mirror
/// checks in wwwroot/js/pages/content-filter.js are UX only and can be bypassed).
///
/// Two checks, both deliberately LOOSE (missing a bad answer is acceptable; flagging a real
/// technical answer is not):
///  - Profanity: whole-word/phrase match against ProfanityWordList (EN + VI).
///  - Gibberish: no dictionary — three independent signals, ORed together, each validated
///    against 37+ real technical/compound terms with zero false positives (2026-07-24):
///     1. Vowel ratio: a letters-only token of 12+ chars whose vowel ratio is under 20% has
///        no plausible syllable structure ("cnwiqnfqifff"), while real terms (useState,
///        kubernetes, middleware, XMLHttpRequest ≈ 21% vowels) all stay above it.
///     2. Token length: no real word/identifier runs past 40 unbroken chars (longest checked,
///        AbstractSingletonProxyFactoryBean, is 33) — a keyboard-mash token easily does.
///     3. Character diversity: a 12+ char token whose unique-char ratio is ≤ 40% is
///        keyboard-mash-repetitive ("nfuawbfubugbguebuegege" = 8 unique/22 = 36%), while real
///        words — even famously repetitive ones (mississippian, nonrepresentational) — stay
///        well above 40%. (A consonant-run-length signal and a lower length floor were also
///        tried and rejected: both false-positived on real terms — XMLHttpRequest, RESTful,
///        "strengths" — see the investigation notes for this feature.)
/// </summary>
public static class ContentModerationHelper
{
    // Checked against tokens AFTER diacritic stripping, so Vietnamese vowels (ơ, ậ, ế...)
    // count as vowels. 'y' counts: it plays vowel in both English (rhythm) and Vietnamese (ký).
    private static readonly HashSet<char> Vowels = new("aeiouy");

    private const int GibberishMinTokenLength = 12;
    private const double GibberishMaxVowelRatio = 0.2;
    private const int GibberishMaxTokenLength = 40;
    private const double GibberishMaxUniqueCharRatio = 0.4;

    private static readonly HashSet<string> BlockedWords = new(
        ProfanityWordList.Entries.Where(e => !e.Contains(' ')),
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[][] BlockedPhrases = ProfanityWordList.Entries
        .Where(e => e.Contains(' '))
        .Select(e => e.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .ToArray();

    /// <summary>True when the answer is acceptable; false with a reason category otherwise.</summary>
    public static bool IsAcceptable(string content)
        => !ContainsProfanity(content) && !LooksLikeGibberish(content);

    public static bool ContainsProfanity(string content)
    {
        var tokens = Tokenize(content);

        for (var i = 0; i < tokens.Count; i++)
        {
            if (BlockedWords.Contains(tokens[i]))
                return true;

            foreach (var phrase in BlockedPhrases)
            {
                if (i + phrase.Length > tokens.Count) continue;
                var match = true;
                for (var j = 0; j < phrase.Length; j++)
                {
                    if (!string.Equals(tokens[i + j], phrase[j], StringComparison.OrdinalIgnoreCase))
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
        }

        return false;
    }

    public static bool LooksLikeGibberish(string content)
    {
        foreach (var token in Tokenize(content))
        {
            if (token.Length > GibberishMaxTokenLength)
                return true;

            if (token.Length < GibberishMinTokenLength) continue;

            var stripped = StripDiacritics(token);
            var vowels = stripped.Count(c => Vowels.Contains(c));
            if ((double)vowels / stripped.Length < GibberishMaxVowelRatio)
                return true;

            var uniqueChars = stripped.Distinct().Count();
            if ((double)uniqueChars / stripped.Length <= GibberishMaxUniqueCharRatio)
                return true;
        }

        return false;
    }

    // Splits into lowercase letters-only tokens. Tokens containing digits are dropped rather
    // than split (code identifiers like "sha256Hash" or "utf8" must never feed the gibberish
    // ratio check in fragments).
    private static List<string> Tokenize(string content)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var hasDigit = false;

        void Flush()
        {
            if (current.Length > 0 && !hasDigit)
                tokens.Add(current.ToString());
            current.Clear();
            hasDigit = false;
        }

        foreach (var c in content.ToLowerInvariant())
        {
            if (char.IsLetter(c))
            {
                current.Append(c);
            }
            else if (char.IsDigit(c))
            {
                current.Append(c);
                hasDigit = true;
            }
            else
            {
                Flush();
            }
        }
        Flush();

        return tokens;
    }

    private static string StripDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        // đ/Đ don't decompose under FormD — map explicitly so they count as consonants, not lost.
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }
}
