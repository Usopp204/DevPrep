import json
import re

import torch


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


def parse_jd(raw_jd: str, model, tokenizer) -> dict:
    prompt = (
        "<start_of_turn>user\n"
        "You are an expert IT recruiter. Analyze this job description carefully.\n\n"
        "Rules:\n"
        "1. Extract all explicit information from the JD\n"
        "2. For required_skills and nice_to_have: each item MUST be a plain string (not an object). Split combined skills into separate items: 'TensorFlow/PyTorch' → 'TensorFlow' and 'PyTorch' as two separate strings\n"
        "3. Clarify vague requirements by inferring what they typically mean for this role type\n\n"
        f"Job Description:\n{raw_jd[:3500]}\n\n"
        'Return ONLY valid JSON (no markdown, no extra text):\n'
        '{\n'
        '  "position": "exact job title",\n'
        '  "company": "company name or Unknown",\n'
        '  "description": "comprehensive clarified description of the role and responsibilities",\n'
        '  "required_skills": ["Python", "TensorFlow", "Machine Learning", "SQL", "Git"],\n'
        '  "nice_to_have": ["Docker", "REST API"],\n'
        '  "experience_years": "Entry level or 2+ years",\n'
        '  "education": "required education or Not specified",\n'
        '  "responsibilities": ["responsibility1", "responsibility2"],\n'
        '  "work_type": "Remote/Onsite/Hybrid or Not specified",\n'
        '  "clarifications": ["clarification1"]\n'
        "}\n"
        "Replace the example skill values above with actual skills from the job description.\n"
        "<end_of_turn>\n<start_of_turn>model\n{"
    )

    inputs = tokenizer(prompt, return_tensors="pt", truncation=True, max_length=4096).to(model.device)
    with torch.no_grad():
        out = model.generate(
            **inputs,
            max_new_tokens=1000,
            do_sample=False,
            pad_token_id=tokenizer.eos_token_id,
            repetition_penalty=1.1,
        )
    raw = "{" + tokenizer.decode(out[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True)
    result = _extract_json(raw)
    # Ensure skill arrays are lists of strings (model sometimes returns objects)
    for field in ("required_skills", "nice_to_have"):
        items = result.get(field)
        if isinstance(items, list):
            result[field] = [
                v if isinstance(v, str)
                else (v.get("name") or v.get("skill") or v.get("title") or str(v))
                for v in items
                if v
            ]
    return result
