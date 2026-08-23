# References

IEEE-style, numbered, cross-referenced from chapters as `[n]`. Only sources the author is
confident exist and are correctly attributed are listed with full details; anything uncertain is
marked `[CITATION TO VERIFY]` at its point of use in the chapter and is not given a numbered entry
here until verified.

[1] `[CITATION TO VERIFY]` — J. Chen et al., "BGE M3-Embedding: Multi-Lingual, Multi-Functionality,
Multi-Granularity Text Embeddings Through Self-Knowledge Distillation," cited in
`01_G1_session_configuration.md` F-01 §8k for the `BAAI/bge-m3` embedding model used in
near-duplicate question suppression. Author list, venue, and year not independently verified in
this pass — do not cite downstream without verification.

[2] `[CITATION TO VERIFY]` — L. Zheng et al., "Judging LLM-as-a-Judge with MT-Bench and Chatbot
Arena," NeurIPS 2023 Datasets and Benchmarks Track. Cited in
`03_G3_interview_room_interaction.md` F-09 §8k for the LLM-as-judge grading pattern used by
`evaluator.GeminiJudge`. Venue/year not independently re-confirmed in this pass.

[3] `[CITATION TO VERIFY]` — X. Wang et al., "Self-Consistency Improves Chain of Thought Reasoning
in Language Models," ICLR 2023. Cited in `03_G3_interview_room_interaction.md` F-09 §8k for the
`SELF_CONSISTENCY_RUNS` median-aggregation pattern. Not independently re-confirmed.

[4] `[CITATION TO VERIFY]` — P. Lewis et al., "Retrieval-Augmented Generation for
Knowledge-Intensive NLP Tasks," NeurIPS 2020. Cited in `03_G3_interview_room_interaction.md` F-09
§8h/§8k for the genuine RAG pipeline (ChromaDB `query_similar`, `BAAI/bge-m3`) used in answer
grading. Not independently re-confirmed.

[5] `[CITATION TO VERIFY]` — J. Deng et al., "ArcFace: Additive Angular Margin Loss for Deep Face
Recognition," CVPR 2019. Cited in `04_G4_biometric_enrolment.md` F-12 §8k for the angular-margin
training objective behind InsightFace's `buffalo_l` ArcFace ResNet-50 model. Not independently
re-confirmed.

[6] `[CITATION TO VERIFY]` — J. Guo et al. (InsightFace project), open-source toolkit
documentation. Cited in `04_G4_biometric_enrolment.md` F-12 §8k. Not independently re-confirmed.

[7] `[CITATION TO VERIFY]` — B. Desplanques, J. Thienpondt, K. Demuynck, "ECAPA-TDNN: Emphasized
Channel Attention, Propagation and Aggregation in TDNN Based Speaker Verification," Interspeech
2020. Cited in `04_G4_biometric_enrolment.md` F-13 §8k for the SpeechBrain
`spkrec-ecapa-voxceleb` model. Not independently re-confirmed.

[8] `[CITATION TO VERIFY]` — A. Nagrani, J.S. Chung, A. Zisserman, "VoxCeleb: A Large-Scale
Speaker Identification Dataset," Interspeech 2017. Cited in `04_G4_biometric_enrolment.md` F-13
§8k for the training corpus underlying the ECAPA-TDNN checkpoint. Not independently re-confirmed.

[9] `[CITATION TO VERIFY]` — A. George and S. Marcel, "Deep Pixel-wise Binary Supervision for Face
Presentation Attack Detection," ICB 2019. Cited in
`05_G5_biometric_verification_and_integrity.md` F-15 §8k as a starting reference for future
liveness/anti-spoofing work, not as grounding for the current (disabled) mechanism. Not
independently re-confirmed.

*(Further entries added as later chapters introduce additional cited models/techniques.)*
