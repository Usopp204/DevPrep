// content-filter.js — CLIENT mirror of InterviewAPI's ContentModerationHelper, for instant
// feedback before an answer is sent. UX only: the server re-runs the same checks in
// SendMessageAsync and is the real enforcement (this file can trivially be bypassed).
//
// STARTER blocked-word list — keep in sync with Helpers/ProfanityWordList.cs when extending.
// Entries match as WHOLE words / whole consecutive-word phrases (never substrings), so
// "cock" can never flag "peacock". Skip ambiguous abbreviations (dm, cc, vl...).
(function (global) {
    "use strict";

    var BLOCKED_ENTRIES = [
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
        // ---- Vietnamese (unambiguous unaccented/teencode) ----
        "dit me", "dit con me",
        "du ma", "dm may",
        "vcl", "clgt",
    ];

    // Loose gibberish heuristic (no dictionary), three signals ORed together — mirrors
    // InterviewAPI's ContentModerationHelper.cs exactly, see that file for the validation notes:
    //  1. Vowel ratio: 12+ char token, under 20% vowels ("cnwiqnfqifff") — real terms
    //     (useState, kubernetes, XMLHttpRequest ≈ 21%) stay above it.
    //  2. Token length: no real word/identifier runs past 40 unbroken chars.
    //  3. Character diversity: 12+ char token, unique-char ratio ≤ 40% is keyboard-mash-
    //     repetitive ("nfuawbfubugbguebuegege" = 36%); real words (even "mississippian") don't.
    var GIBBERISH_MIN_TOKEN_LENGTH = 12;
    var GIBBERISH_MAX_VOWEL_RATIO = 0.2;
    var GIBBERISH_MAX_TOKEN_LENGTH = 40;
    var GIBBERISH_MAX_UNIQUE_CHAR_RATIO = 0.4;

    var blockedWords = {};
    var blockedPhrases = [];
    BLOCKED_ENTRIES.forEach(function (entry) {
        var lower = entry.toLowerCase();
        if (lower.indexOf(" ") === -1) blockedWords[lower] = true;
        else blockedPhrases.push(lower.split(/\s+/));
    });

    // Lowercase letters-only tokens; tokens containing digits are dropped whole (code
    // identifiers like "sha256Hash" must never feed the vowel-ratio check in fragments).
    function tokenize(content) {
        var tokens = [];
        var parts = content.toLowerCase().split(/[^\p{L}\p{N}]+/u);
        parts.forEach(function (part) {
            if (!part) return;
            if (/\p{N}/u.test(part)) return; // contains a digit — skip whole token
            tokens.push(part);
        });
        return tokens;
    }

    function stripDiacritics(text) {
        return text.normalize("NFD").replace(/[̀-ͯ]/g, "")
            .replace(/đ/g, "d").replace(/Đ/g, "D");
    }

    function containsProfanity(content) {
        var tokens = tokenize(content);
        for (var i = 0; i < tokens.length; i++) {
            if (blockedWords[tokens[i]]) return true;
            for (var p = 0; p < blockedPhrases.length; p++) {
                var phrase = blockedPhrases[p];
                if (i + phrase.length > tokens.length) continue;
                var match = true;
                for (var j = 0; j < phrase.length; j++) {
                    if (tokens[i + j] !== phrase[j]) { match = false; break; }
                }
                if (match) return true;
            }
        }
        return false;
    }

    function looksLikeGibberish(content) {
        var tokens = tokenize(content);
        for (var i = 0; i < tokens.length; i++) {
            if (tokens[i].length > GIBBERISH_MAX_TOKEN_LENGTH) return true;
            if (tokens[i].length < GIBBERISH_MIN_TOKEN_LENGTH) continue;

            var stripped = stripDiacritics(tokens[i]);
            var vowels = (stripped.match(/[aeiouy]/g) || []).length;
            if (vowels / stripped.length < GIBBERISH_MAX_VOWEL_RATIO) return true;

            var uniqueChars = {};
            for (var c = 0; c < stripped.length; c++) uniqueChars[stripped[c]] = true;
            if (Object.keys(uniqueChars).length / stripped.length <= GIBBERISH_MAX_UNIQUE_CHAR_RATIO) return true;
        }
        return false;
    }

    var api = {
        containsProfanity: containsProfanity,
        looksLikeGibberish: looksLikeGibberish,
        isAcceptable: function (content) {
            return !containsProfanity(content) && !looksLikeGibberish(content);
        }
    };

    if (typeof module !== "undefined" && module.exports) module.exports = api; // tests (node)
    global.DevPrepContentFilter = api;
})(typeof window !== "undefined" ? window : globalThis);
