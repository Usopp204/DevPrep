"""
train_kaggle.py — Pipeline train model parse CV/JD trên dữ liệu THẬT (Djinni), chạy trên Kaggle GPU.

2 stage (tách rời để resume được giữa các session Kaggle 12h):

  Stage 1 — Teacher gán nhãn (gemma-2-9b-it, 4-bit), CÓ CHECKPOINT — chạy lại là tự bỏ qua
  các id đã label xong (ghi từng dòng vào labels.jsonl):
      python train_kaggle.py label --input raw_texts.jsonl --out labels.jsonl

  Stage 2 — QLoRA fine-tune student gemma-2-2b-it rồi merge + push HF:
      python train_kaggle.py train --pairs labels.jsonl --push DuongNTCE/devprep-cv-parser

Cải tiến so với script cũ (12 mẫu tự viết):
  - Dữ liệu thật ≥2500 mẫu, có CẢ hai task CV parse + JD parse (model production dùng cho cả 2
    nhưng trước giờ chỉ được train task CV).
  - Prompt lúc train = ĐÚNG prompt production (app/cv/parser.py + app/jd/parser.py) → không lệch
    phân phối giữa train và inference.
  - Loss chỉ tính trên phần completion (JSON model sinh ra), mask phần prompt
    (DataCollatorForCompletionOnlyLM) — script cũ tính loss trên cả prompt.
  - Train/val split + eval mỗi epoch, nhãn được validate JSON + schema trước khi đưa vào train.

HF token: đọc từ Kaggle secret HUGGING_FACE_TOKEN, fallback biến môi trường HF_TOKEN.
"""
from __future__ import annotations

import argparse
import json
import os
import re

# ─────────────────────────────── Prompts (khớp production) ───────────────────────────────

CV_SYSTEM = (
    "You are an expert IT recruiter. Analyze this CV deeply.\n\n"
    "IMPORTANT for strengths: Do NOT copy skills. REASON and INFER what this person is truly good at.\n\n"
)
CV_SCHEMA = """{
  "personal_info": {"name": "exact name", "email": "exact email", "phone": "exact phone", "location": "exact location"},
  "top_skills": ["EVERY technical skill, tool, framework, library, language, database, platform, and AI/ML concept mentioned ANYWHERE in the CV — career objective, education, projects, certifications. One item per skill. E.g.: Python, TensorFlow, PyTorch, Machine Learning, SQL Server, Git"],
  "strengths": [{"strength": "concise 2-4 word strength name", "reasoning": "specific evidence from CV text — quote or reference concrete details"}],
  "careers": [{"title": "", "company": "", "duration": "", "description": ""}],
  "projects": [{"name": "", "description": "", "tech_stack": [], "highlights": ""}],
  "education": {"degree": "", "major": "", "school": "", "duration": ""},
  "certifications": ["exact cert names"],
  "interview_topics": ["specific topics based on their background"]
}"""

JD_RULES = (
    "You are an expert IT recruiter. Analyze this job description carefully.\n\n"
    "Rules:\n"
    "1. Extract all explicit information from the JD\n"
    "2. For required_skills and nice_to_have: each item MUST be a plain string (not an object). "
    "Split combined skills into separate items: 'TensorFlow/PyTorch' → 'TensorFlow' and 'PyTorch' as two separate strings\n"
    "3. Clarify vague requirements by inferring what they typically mean for this role type\n\n"
)
JD_SCHEMA = """{
  "position": "exact job title",
  "company": "company name or Unknown",
  "description": "comprehensive clarified description of the role and responsibilities",
  "required_skills": ["Python", "TensorFlow", "Machine Learning", "SQL", "Git"],
  "nice_to_have": ["Docker", "REST API"],
  "experience_years": "Entry level or 2+ years",
  "education": "required education or Not specified",
  "responsibilities": ["responsibility1", "responsibility2"],
  "work_type": "Remote/Onsite/Hybrid or Not specified",
  "clarifications": ["clarification1"]
}"""


def build_user_turn(kind: str, text: str) -> str:
    if kind == "cv":
        return (
            f"{CV_SYSTEM}CV:\n{text[:4000]}\n\n"
            f"Return ONLY this JSON (no markdown):\n{CV_SCHEMA}"
        )
    return (
        f"{JD_RULES}Job Description:\n{text[:3500]}\n\n"
        f"Return ONLY valid JSON (no markdown, no extra text):\n{JD_SCHEMA}\n"
        "Replace the example skill values above with actual skills from the job description."
    )


def build_prompt(kind: str, text: str) -> str:
    return f"<start_of_turn>user\n{build_user_turn(kind, text)}\n<end_of_turn>\n<start_of_turn>model\n{{"


# ─────────────────────────────── JSON extraction (như production) ───────────────────────────────

def extract_json(raw: str):
    raw = re.sub(r"```json|```", "", raw).strip()
    raw = re.sub(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]", "", raw)
    m = re.search(r"\{[\s\S]*\}", raw)
    if not m:
        return None
    s = m.group()
    for fn in (json.loads, lambda x: json.loads(x, strict=False)):
        try:
            return fn(s)
        except Exception:
            pass
    for i in range(len(s), len(s) // 2, -50):
        try:
            return json.loads(s[:i])
        except Exception:
            continue
    return None


def valid_label(kind: str, label) -> bool:
    if not isinstance(label, dict):
        return False
    if kind == "cv":
        if not isinstance(label.get("top_skills"), list) or not label["top_skills"]:
            return False
        return isinstance(label.get("personal_info"), dict)
    # jd
    req = label.get("required_skills")
    if not isinstance(req, list) or not req:
        return False
    return all(isinstance(s, str) for s in req)


def hf_login():
    token = os.environ.get("HF_TOKEN")
    if not token:
        try:
            from kaggle_secrets import UserSecretsClient
            token = UserSecretsClient().get_secret("HUGGING_FACE_TOKEN")
        except Exception:
            token = None
    if token:
        from huggingface_hub import login
        login(token=token)
    return token


# ─────────────────────────────── Stage 1: teacher labeling ───────────────────────────────

def cmd_label(args):
    import torch
    from transformers import AutoTokenizer, AutoModelForCausalLM, BitsAndBytesConfig

    hf_login()
    done_ids = set()
    if os.path.exists(args.out):
        with open(args.out, encoding="utf-8") as f:
            for line in f:
                try:
                    done_ids.add(json.loads(line)["id"])
                except Exception:
                    pass
    print(f"Resume: {len(done_ids)} labels already done")

    items = []
    with open(args.input, encoding="utf-8") as f:
        for line in f:
            it = json.loads(line)
            if it["id"] not in done_ids:
                items.append(it)
    print(f"To label: {len(items)}")
    if not items:
        return

    bnb = BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_compute_dtype=torch.bfloat16,
                             bnb_4bit_use_double_quant=True, bnb_4bit_quant_type="nf4")
    print(f"Loading teacher {args.teacher} ...")
    tok = AutoTokenizer.from_pretrained(args.teacher)
    mdl = AutoModelForCausalLM.from_pretrained(args.teacher, quantization_config=bnb, device_map="auto")
    mdl.eval()

    ok = skip = 0
    with open(args.out, "a", encoding="utf-8") as fout:
        for n, it in enumerate(items, 1):
            prompt = build_prompt(it["type"], it["text"])
            inp = tok(prompt, return_tensors="pt", truncation=True, max_length=3200).to(mdl.device)
            with torch.no_grad():
                out = mdl.generate(**inp, max_new_tokens=900, do_sample=False,
                                   pad_token_id=tok.eos_token_id, repetition_penalty=1.1)
            raw = "{" + tok.decode(out[0][inp["input_ids"].shape[1]:], skip_special_tokens=True)
            label = extract_json(raw)
            if label and valid_label(it["type"], label):
                fout.write(json.dumps({"id": it["id"], "type": it["type"],
                                       "text": it["text"], "label": label}, ensure_ascii=False) + "\n")
                fout.flush()
                ok += 1
            else:
                skip += 1
            if n % 20 == 0:
                print(f"  [{n}/{len(items)}] ok={ok} skip={skip}")
    print(f"Done. labeled={ok}, skipped={skip}")


# ─────────────────────────────── Stage 2: QLoRA train ───────────────────────────────

def cmd_train(args):
    import torch
    from datasets import Dataset
    from peft import LoraConfig, get_peft_model, TaskType, PeftModel
    from transformers import AutoTokenizer, AutoModelForCausalLM, BitsAndBytesConfig
    from trl import SFTTrainer, SFTConfig
    from trl import DataCollatorForCompletionOnlyLM

    token = hf_login()

    pairs = []
    with open(args.pairs, encoding="utf-8") as f:
        for line in f:
            it = json.loads(line)
            if valid_label(it["type"], it.get("label")):
                pairs.append(it)
    print(f"Training pairs: {len(pairs)} "
          f"(cv={sum(1 for p in pairs if p['type']=='cv')}, jd={sum(1 for p in pairs if p['type']=='jd')})")

    def to_text(p):
        return (
            f"<start_of_turn>user\n{build_user_turn(p['type'], p['text'])}\n<end_of_turn>\n"
            f"<start_of_turn>model\n{json.dumps(p['label'], ensure_ascii=False)}\n<end_of_turn>"
        )

    ds = Dataset.from_dict({"text": [to_text(p) for p in pairs]}).train_test_split(test_size=0.05, seed=42)
    print(f"train={len(ds['train'])}, val={len(ds['test'])}")

    bnb = BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_compute_dtype=torch.bfloat16,
                             bnb_4bit_use_double_quant=True, bnb_4bit_quant_type="nf4")
    tok = AutoTokenizer.from_pretrained(args.student)
    tok.pad_token = tok.eos_token
    tok.padding_side = "right"

    mdl = AutoModelForCausalLM.from_pretrained(args.student, quantization_config=bnb, device_map={"": 0})
    mdl.enable_input_require_grads()
    mdl = get_peft_model(mdl, LoraConfig(
        r=16, lora_alpha=32, lora_dropout=0.05, bias="none", task_type=TaskType.CAUSAL_LM,
        target_modules=["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"]))
    mdl.print_trainable_parameters()

    collator = DataCollatorForCompletionOnlyLM(response_template="<start_of_turn>model\n", tokenizer=tok)

    trainer = SFTTrainer(
        model=mdl, train_dataset=ds["train"], eval_dataset=ds["test"],
        processing_class=tok, data_collator=collator,
        args=SFTConfig(
            output_dir=args.outdir, num_train_epochs=args.epochs,
            per_device_train_batch_size=1, gradient_accumulation_steps=8,
            learning_rate=1e-4, warmup_ratio=0.03, lr_scheduler_type="cosine",
            bf16=True, logging_steps=20, eval_strategy="epoch", save_strategy="epoch",
            report_to="none", dataset_text_field="text", max_length=2048,
        ),
    )
    trainer.args._n_gpu = 1  # QLoRA 4-bit không hỗ trợ DataParallel
    trainer.train()
    trainer.save_model(args.outdir)
    tok.save_pretrained(args.outdir)

    print("Merging LoRA into base (bf16, CPU)...")
    base = AutoModelForCausalLM.from_pretrained(args.student, torch_dtype=torch.bfloat16,
                                                device_map="cpu", low_cpu_mem_usage=True)
    merged = PeftModel.from_pretrained(base, args.outdir).merge_and_unload()
    merged_dir = args.outdir + "_merged"
    merged.save_pretrained(merged_dir, safe_serialization=True)
    tok.save_pretrained(merged_dir)

    if args.push:
        print(f"Pushing to {args.push} ...")
        merged.push_to_hub(args.push, private=True, token=token)
        tok.push_to_hub(args.push, private=True, token=token)
        print(f"Done → https://huggingface.co/{args.push}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    p1 = sub.add_parser("label", help="Stage 1: teacher gán nhãn (resume được)")
    p1.add_argument("--input", default="raw_texts.jsonl")
    p1.add_argument("--out", default="labels.jsonl")
    p1.add_argument("--teacher", default="google/gemma-2-9b-it")
    p1.set_defaults(fn=cmd_label)

    p2 = sub.add_parser("train", help="Stage 2: QLoRA train + merge + push")
    p2.add_argument("--pairs", default="labels.jsonl")
    p2.add_argument("--student", default="google/gemma-2-2b-it")
    p2.add_argument("--outdir", default="cv-parser-ft")
    p2.add_argument("--epochs", type=int, default=2)
    p2.add_argument("--push", default=None, help="HF repo id, vd: DuongNTCE/devprep-cv-parser")
    p2.set_defaults(fn=cmd_train)

    args = ap.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
