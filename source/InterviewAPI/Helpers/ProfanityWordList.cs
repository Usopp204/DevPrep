namespace InterviewAPI.Helpers;

/// <summary>
/// STARTER blocked-word list for the interview-answer content filter — deliberately kept in
/// its own file so it can be extended without touching the matching logic in
/// ContentModerationHelper. Entries are matched as WHOLE words (or whole consecutive-word
/// phrases), case-insensitive, against the letter-tokens of the answer — never as substrings —
/// so e.g. "cock" here can never flag "peacock".
///
/// Keep entries unambiguous: skip abbreviations that collide with normal usage
/// ("dm" = direct message, "cc" = carbon copy, "vl" = initials...).
/// The client has a mirrored copy in wwwroot/js/pages/content-filter.js — keep both in sync
/// when adding entries.
/// </summary>
public static class ProfanityWordList
{
    public static readonly string[] Entries =
    {
        // ---- English ----
        "fuck", "fucking", "fucker", "motherfucker",
        "shit", "bullshit",
        "bitch", "bitches",
        "asshole",
        "cunt",
        "dick", "dickhead",
        "pussy",
        "cock",
        "bastard",
        "whore", "slut",
        "faggot",
        "nigger", "nigga",

        // ---- Vietnamese (accented) ----
        "đụ", "đù",
        "địt",
        "đéo",
        "cặc",
        "lồn",
        "buồi",
        "đĩ",
        "đụ má", "đù má", "địt mẹ", "địt con mẹ",
        "vãi lồn",
        "con đĩ",

        // ---- Vietnamese (common unaccented/teencode spellings) ----
        // Only phrases that are unambiguous without diacritics; single unaccented words like
        // "du"/"ma"/"lon" are far too collision-prone ("du lịch", "ma trận", "lon bia").
        "dit me", "dit con me",
        "du ma", "dm may",
        "vcl", "clgt",
    };
}
