import json
import os
import re
from pathlib import Path

import torch


def read_cv(path: str) -> str:
    ext = Path(path).suffix.lower()
    text = ""
    if ext == ".pdf":
        import fitz
        with fitz.open(path) as doc:
            for page in doc:
                text += page.get_text()
        if len(text.strip()) < 100:
            from pdf2image import convert_from_path
            import pytesseract
            images = convert_from_path(path, dpi=200)
            text = "".join(pytesseract.image_to_string(img, lang="eng") for img in images)
    elif ext in (".docx", ".doc"):
        from docx import Document
        text = "\n".join(p.text for p in Document(path).paragraphs)
    return text.strip()


def _extract_json(raw: str) -> dict:
    raw = re.sub(r"```json|```", "", raw).strip()
    raw = re.sub(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]", "", raw)
    match = re.search(r"\{[\s\S]*\}", raw)
    if not match:
        return {"error": "No JSON found", "raw": raw[:300]}
    json_str = match.group()
    for fn in [json.loads, lambda s: json.loads(s, strict=False)]:
        try:
            return fn(json_str)
        except Exception:
            pass
    for i in range(len(json_str), len(json_str) // 2, -50):
        try:
            return json.loads(json_str[:i])
        except Exception:
            continue
    return {"error": "Cannot parse JSON", "raw": raw[:300]}


def parse_cv(text: str, model, tokenizer) -> dict:
    prompt = (
        "<start_of_turn>user\n"
        "You are an expert IT recruiter. Analyze this CV deeply.\n\n"
        "IMPORTANT for strengths: Do NOT copy skills. REASON and INFER what this person is truly good at.\n\n"
        f"CV:\n{text[:4000]}\n\n"
        'Return ONLY this JSON (no markdown):\n'
        '{\n'
        '  "personal_info": {"name": "exact name", "email": "exact email", "phone": "exact phone", "location": "exact location"},\n'
        '  "top_skills": ["EVERY technical skill, tool, framework, library, language, database, platform, and AI/ML concept mentioned ANYWHERE in the CV — career objective, education, projects, certifications. One item per skill. E.g.: Python, TensorFlow, PyTorch, Machine Learning, SQL Server, Git"],\n'
        '  "strengths": [{"strength": "concise 2-4 word strength name", "reasoning": "specific evidence from CV text — quote or reference concrete details"}],\n'
        '  "careers": [{"title": "", "company": "", "duration": "", "description": ""}],\n'
        '  "projects": [{"name": "", "description": "", "tech_stack": [], "highlights": ""}],\n'
        '  "education": {"degree": "", "major": "", "school": "", "duration": ""},\n'
        '  "certifications": ["exact cert names"],\n'
        '  "interview_topics": ["specific topics based on their background"]\n'
        "}\n"
        "<end_of_turn>\n<start_of_turn>model\n{"
    )

    inputs = tokenizer(prompt, return_tensors="pt", truncation=True, max_length=2048).to(model.device)
    with torch.no_grad():
        out = model.generate(
            **inputs,
            max_new_tokens=600,
            do_sample=False,
            pad_token_id=tokenizer.eos_token_id,
            repetition_penalty=1.1,
        )
    raw = "{" + tokenizer.decode(out[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True)
    return _extract_json(raw)
