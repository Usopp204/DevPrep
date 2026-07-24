import re
from typing import Tuple

# Canonical form → abbreviations/aliases (bidirectional expansion)
_ALIASES: dict[str, set[str]] = {
    "machine learning": {"ml"},
    "deep learning": {"dl"},
    "natural language processing": {"nlp"},
    "computer vision": {"cv"},
    "artificial intelligence": {"ai"},
    "javascript": {"js"},
    "typescript": {"ts"},
    "tensorflow": {"tf"},
    "kubernetes": {"k8s"},
    "postgresql": {"postgres", "pg"},
    "continuous integration": {"ci"},
    "continuous delivery": {"cd"},
    "application programming interface": {"api"},
    "representational state transfer": {"rest"},
    "sql server": {"mssql"},
    "scikit": {"sklearn", "scikit-learn"},
}

# Build reverse lookup: abbr → set of canonical/other forms
_EXPAND: dict[str, set[str]] = {}
for _canon, _abbrs in _ALIASES.items():
    _EXPAND.setdefault(_canon, set()).update(_abbrs)
    for _a in _abbrs:
        _EXPAND.setdefault(_a, set()).add(_canon)


def _normalize(text: str) -> set[str]:
    words = re.findall(r"[a-z0-9#+.]+", text.lower())
    result = set(words)
    for w in words:
        if w in _EXPAND:
            result.update(_EXPAND[w])
    return result


def _build_cv_tokens(cv_skills: list, strengths: list) -> set[str]:
    """Union of tokens from top_skills + strength names (not reasoning, too broad)."""
    tokens: set[str] = set()
    for s in cv_skills:
        tokens.update(_normalize(str(s)))
    for s in strengths:
        tokens.update(_normalize(str(s.get("strength", ""))))
    return tokens


def _skill_match(cv_tokens: set[str], jd_skills: list) -> Tuple[float, list, list]:
    if not jd_skills:
        return 0.0, [], []  # no skills listed → no score, no bonus

    matched, missing = [], []
    for skill in jd_skills:
        if _normalize(str(skill)) & cv_tokens:
            matched.append(skill)
        else:
            missing.append(skill)

    ratio = len(matched) / len(jd_skills)
    return ratio, matched, missing


# Common English stop words to exclude from strength matching
_STOP = {
    "the","a","an","and","or","of","in","on","at","to","for","with","by","is",
    "are","was","were","be","been","has","have","had","do","does","did","will",
    "would","can","could","may","might","should","this","that","it","its","i",
    "my","me","we","our","they","their","you","your","from","as","about","not",
    "but","also","more","than","so","if","into","up","out","after","before",
    "during","between","through","both","each","other","which","who","when",
    "where","how","what","there","then","than","such","based","strong","solid",
    "good","well","demonstrated","demonstrates","experience","skills","skill",
    "knowledge","ability","work","working","used","using","use","cv","resume",
    "mentions","mention","related","participation","participated","project",
    "projects","shows","shown","indicate","indicates","suggests","evidence",
}


def _strength_tokens(s: dict) -> set[str]:
    """Tokens from strength name only (not reasoning — too verbose and causes false matches)."""
    raw = _normalize(str(s.get("strength", "")))
    return raw - _STOP


def calculate_score(cv_json: dict, jd_json: dict) -> dict:
    cv_skills = cv_json.get("top_skills", [])
    strengths = cv_json.get("strengths", [])
    required_skills = jd_json.get("required_skills", [])
    nice_to_have = jd_json.get("nice_to_have", [])

    cv_tokens = _build_cv_tokens(cv_skills, strengths)

    req_ratio, matched_req, missing_req = _skill_match(cv_tokens, required_skills)

    # Strength score: match strength NAMES against JD required_skills tokens (not prose description)
    if strengths and required_skills:
        req_tokens = set()
        for skill in required_skills:
            req_tokens.update(_normalize(str(skill)) - _STOP)
        matched_strengths = [str(s.get("strength", "")) for s in strengths if _strength_tokens(s) & req_tokens]
        strength_score = len(matched_strengths) / len(strengths)
    else:
        matched_strengths = []
        strength_score = 0.0

    nice_ratio, matched_nice, missing_nice = _skill_match(cv_tokens, nice_to_have)

    total = round(req_ratio * 60 + strength_score * 25 + nice_ratio * 15)
    total = max(0, min(100, total))

    suggestions = []
    for skill in missing_req[:5]:
        suggestions.append(f"Học thêm kỹ năng: {skill}")
    if total < 60:
        suggestions.append("CV chưa phù hợp nhiều với JD này, cân nhắc các vị trí phù hợp hơn")
    elif total < 80:
        suggestions.append("Bổ sung các kỹ năng còn thiếu để tăng cơ hội ứng tuyển")
    else:
        suggestions.append("CV rất phù hợp với vị trí này!")

    return {
        "score": total,
        "matched_skills": matched_req + matched_nice,
        "missing_skills": missing_req,
        "matched_required": matched_req,
        "matched_strengths": matched_strengths,
        "matched_nice_to_have": matched_nice,
        "missing_nice_to_have": missing_nice,
        "suggestions": suggestions,
        "breakdown": {
            "required_skills_score": round(req_ratio * 60, 1),
            "strength_relevance_score": round(strength_score * 25, 1),
            "nice_to_have_score": round(nice_ratio * 15, 1),
        },
    }
