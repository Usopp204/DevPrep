# FLOW — `VoiceInteraction` · BL#42, 43 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/VoiceInteraction.md`
> Trạng thái: `DRAFT` — Khảo sát: lô E, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | "AI Voice" trong phòng phỏng vấn: đọc to lời chào / câu hỏi / gợi ý / lời kết bằng TTS (BL#42-43), có cache đĩa và 2 nhà cung cấp (Piper mặc định, ElevenLabs legacy). Kèm nhánh trả lời bằng giọng nói (STT) vẽ thu gọn |
| Điểm vào (user thao tác gì) | Bật/tắt nút **AI Voice** (mặc định BẬT). Khi bật, mỗi bong bóng AI mới tự động được đọc — user không bấm gì thêm |
| Kết thúc (user thấy gì) | Nghe giọng người phỏng vấn + waveform chạy. Mọi lỗi TTS đều **im lặng**: chỉ mất tiếng, phần chữ và luồng phỏng vấn không bị chặn |
| Quy mô | `10` participant · `62` message · `16` class · `2` alt / `7` opt · trang Sequence 2120×3367 · trang Class 3640×936 |
| Đối chiếu ước lượng RECON | RECON đoán `45-55` · thật `62` · hệ số `1.2×` |
| Service ngoài quyền sở hữu | ApiGateway (định tuyến) |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | **Piper TTS** (mặc định, chạy cục bộ + ffmpeg WAV→MP3) hoặc **ElevenLabs TTS** (legacy, còn nối dây qua `TTS_PROVIDER`); nhánh STT dùng **Deepgram** (thuộc `#16`) |
| Ghi DB | **không** — endpoint TTS hoàn toàn stateless, chỉ ghi **cache đĩa** `TTS_CACHE_DIR/<sha256>.mp3` |
| Trừ credit | không |
| KI liên quan | **KI-02** (nhánh lỗi TTS trả 400 thay vì 502) — không phát hiện KI mới |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `POST api/interview/sessions/stt` (`SpeechToText`) | [InterviewSessionController.cs:529-550](../../../source/InterviewAPI/Controllers/InterviewSessionController.cs#L529-L550) | Endpoint **hoạt động về kỹ thuật nhưng chết từ góc nhìn Browser**: đã grep toàn bộ `WebClient/Services/Interview/InterviewService.cs`, `TranscribeAudioAsync` gọi `.../verify/voice-answer` chứ KHÔNG gọi `/sessions/stt`. Không JS nào gọi tới nó. |
| Toàn bộ logic speaker-verify / strike / đóng phiên của `verify/voice-answer` | `BiometricVerificationService.cs:419-608` | Thuộc `#16 VerifyVoiceAnswer` — nhánh STT ở đây chỉ vẽ thu gọn (`opt J`, xem §2). |
| `warm_up_tts()` nạp model Piper lúc container khởi động | [voice.py:165-172](../../../ai-service/app/services/voice.py#L165-L172) | Chạy 1 lần lúc start, không thuộc request nào. |
| Header chẩn đoán `X-TTS-Cache` / `X-TTS-Provider` / `X-TTS-Latency-Ms` | [interview.py:172-207](../../../ai-service/app/api/interview.py#L172-L207) | ai-service có trả, nhưng `AiServiceClient.SynthesizeSpeechAsync` chỉ đọc `ReadAsByteArrayAsync` → header bị bỏ, không tới được .NET hay Browser. Ghi trong nhãn message D-a.1 / D-b.4 để không mất thông tin. |
| Các listener waveform (`.visualizer` play/ended/error/pause) | [interview-room.js:1612-1619](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L1612-L1619) | Hiệu ứng hiển thị — gộp 1 câu vào nhãn message 17. |
| 4 nơi gọi `speak(...)` (greeting/câu hỏi mới, hint, lời kết) | interview-room.js:571, 713, 1046, 1659-1660 | Thuộc `EnterInterviewRoom` / `#5 ConductInterviewQA` / `#7 GenerateInterviewHint` / `#9 EndInterviewSession`. Gộp vào nhãn message 1 làm nguồn kích hoạt. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Trả lời bằng giọng nói (STT) | `#16 VerifyVoiceAnswer` | `opt J` — 1 cặp call+ret ở mức Gateway (`POST .../verify/voice-answer`), không mở rộng bên trong | InterviewService.cs:349-360 |
| Các bong bóng AI kích hoạt `speak()` | `#5` / `#6` / `#7` / `#9` | message 1 (nhãn liệt kê đủ 4 nguồn) | interview-room.js:571, 713, 1046, 1659-1660 |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Interview Room UI | box | `wwwroot/js/pages/interview-room.js:1589-1641` | — |
| 2 | :InterviewController (WebClient) | box | `WebClient/Controllers/Interview/InterviewController.cs:452-500` | — |
| 3 | :InterviewService (WebClient) | box | `WebClient/Services/Interview/InterviewService.cs:333-373` | — |
| 4 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 5 | :InterviewSessionController (InterviewAPI) | box | `InterviewAPI/Controllers/InterviewSessionController.cs:502-523` | có |
| 6 | :AiServiceClient (InterviewAPI) | box | `InterviewAPI/Clients/AiServiceClient.cs:71-96` | có |
| 7 | :ai-service /interview/tts | box | `ai-service/app/api/interview.py:161-207` | có |
| 8 | :tts_cache (disk) | box | `ai-service/app/services/tts_cache.py:30-77` | — |
| 9 | :PiperTTS / ElevenLabsTTS | box | `ai-service/app/services/voice.py:50-163` | — |

> **ACTOR GIÁN TIẾP:** message 1 là sự kiện tự động (một bong bóng AI mới xuất hiện), nhưng user
> đang ngồi trong phòng phỏng vấn và **nghe** kết quả ngay → theo quy ước vẫn dùng actor `User`,
> nhãn ghi rõ `[auto]`. Message 19 là thao tác trực tiếp (bấm nút toggle).

## 4. BẢNG MESSAGE

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Interview Room UI | call | `1. [auto] A new AI bubble appears while AI Voice is on` | — | interview-room.js:1659-1660, 571, 1046, 713 | boundary | 1. [auto] A new AI bubble appears while AI Voice is on - the room greeting, a new question, a hint, or the closing line |
| 2 | :Interview Room UI | (self) | call | `2. speak(text): guard !aiVoiceOn / !text / !ttsUrl` | — | interview-room.js:1595-1596 | self | — |
| A.1 | :Interview Room UI | (self) | call | `A.1. return early - no request is sent` | A | interview-room.js:1596 | self | — |
| A.2 | :Interview Room UI | User | ret | `A.2. Nothing is spoken; the text bubble is still shown as usual` | A | interview-room.js:1596 |  | — |
| 3 | :Interview Room UI | :InterviewController (WebClient) | call | `3. POST /Interview/Tts { text } (application/json)` | — | interview-room.js:1598-1602 |  | — |
| B.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `B.1. Return 400 { error: "Text is required." }` | B | InterviewController.cs:455-458 |  | — |
| B.2 | :Interview Room UI | User | ret | `B.2. speak() returns silently` | B | interview-room.js:1603 |  | B.2. response.ok is false -> speak() returns silently; nothing is spoken and no error is surfaced |
| 4 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `4. GetTtsAudioAsync(request.Text, ct)` | — | InterviewController.cs:460 |  | — |
| 5 | :InterviewService (WebClient) | :API Gateway | call | `5. POST api/interview/sessions/tts { text }` | — | InterviewService.cs:336-339 |  | — |
| 6 | :API Gateway | :InterviewSessionController | call | `6. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| C.1 | :InterviewSessionController | :API Gateway | ret | `C.1. Return 400 {error}` | C | InterviewSessionController.cs:509-510 |  | C.1. Return 400 ApiResponse.Fail("Text is required.") - server-side guard, only reachable by calling the API directly |
| C.2 | :API Gateway | :InterviewService (WebClient) | ret | `C.2. Return 400` | C | ApiGateway/appsettings.json:49-54 |  | — |
| C.3 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `C.3. Return (false, null, "TTS request failed (400).")` | C | InterviewService.cs:340-343 |  | — |
| C.4 | :InterviewController (WebClient) | :Interview Room UI | ret | `C.4. Return 400 { error }` | C | InterviewController.cs:462-465 |  | — |
| C.5 | :Interview Room UI | User | ret | `C.5. response.ok is false -> silent; nothing is spoken` | C | interview-room.js:1603 |  | — |
| 7 | :InterviewSessionController | :AiServiceClient | call | `7. SynthesizeSpeechAsync(req.Text, ct)` | — | InterviewSessionController.cs:514 |  | — |
| 8 | :AiServiceClient | :ai-service | call | `8. POST /interview/tts { text } (header X-Internal-Api-Key)` | — | AiServiceClient.cs:71-76 |  | — |
| 9 | :ai-service | (self) | call | `9. _tts_identity() -> (voice, speaker_id) of the active provider; key = sha256(normalized text \| TTS_PROVIDER \| voice \| speaker_id)` | — | interview.py:150-171, tts_cache.py:30-40 | self |
| 10 | :ai-service | :tts_cache (disk) | call | `10. get(key)` | — | interview.py:171 |  | — |
| 11 | :tts_cache (disk) | :ai-service | ret | `11. Return the cached MP3 bytes / None` | — | tts_cache.py:47-59 |  | 11. Return the cached MP3 bytes / None - disabled cache or any disk error degrades to a miss |
| D-a.1 | :ai-service | :AiServiceClient | ret | `D-a.1. Return 200 audio/mpeg (X-TTS-Cache: HIT)` | D-a | interview.py:173-182 |  | D-a.1. 200 audio/mpeg (X-TTS-Cache: HIT, X-TTS-Provider, X-TTS-Latency-Ms - headers dropped by the .NET proxy) |
| D-b.1 | :ai-service | :PiperTTS / ElevenLabsTTS | call | `D-b.1. _tts.synthesize(text)` | D-b | interview.py:185, voice.py:158-162 |  | D-b.1. _tts.synthesize(text) - the provider instance is chosen once at import time from config.TTS_PROVIDER |
| F.1 | :PiperTTS / ElevenLabsTTS | :ai-service | ret | `F.1. raise (logged: TTS synthesis failed, provider=...)` | F | voice.py:114-116, interview.py:186-187 | throw | — |
| F.2 | :ai-service | :AiServiceClient | ret | `F.2. Return 502 { error: "tts_unavailable" } (X-TTS-Cache: MISS)` | F | interview.py:188-196 |  | — |
| F.3 | :AiServiceClient | :InterviewSessionController | ret | `F.3. EnsureSuccessStatusCode() throws HttpRequestException` | F | AiServiceClient.cs:77, 87-95 | throw | F.3. EnsureSuccessStatusCode throws HttpRequestException (logged with StatusCode + TextLength) |
| F.4 | :InterviewSessionController | :API Gateway | ret | `F.4. Return 400 {error}` | F | InterviewSessionController.cs:519-522 |  | F.4. Return 400 ApiResponse.Fail(ex.Message) - NOT 502; the two TTS error branches disagree (KI-02) |
| F.5 | :API Gateway | :InterviewService (WebClient) | ret | `F.5. Return 400` | F | ApiGateway/appsettings.json:49-54 |  | — |
| F.6 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F.6. Return (false, null, "TTS request failed (400).")` | F | InterviewService.cs:340-343 |  | — |
| F.7 | :InterviewController (WebClient) | :Interview Room UI | ret | `F.7. Return 400 { error }` | F | InterviewController.cs:462-465 |  | — |
| F.8 | :Interview Room UI | User | ret | `F.8. speak() returns silently` | F | interview-room.js:1603, 1629-1631 |  | F.8. response.ok is false -> speak() returns silently; the interview continues without audio |
| E-a.1 | :PiperTTS / ElevenLabsTTS | (self) | call | `E-a.1. PiperVoice.synthesize_wav(text, SynthesisConfig)` | E-a | voice.py:103-134 | self | E-a.1. PiperVoice.synthesize_wav(text, SynthesisConfig(speaker_id)) -> WAV, then ffmpeg transcodes it to mono 64 kbps MP3 |
| E-a.2 | :PiperTTS / ElevenLabsTTS | :ai-service | ret | `E-a.2. Return MP3 bytes` | E-a | voice.py:112 |  | — |
| E-b.1 | :PiperTTS / ElevenLabsTTS | (self) | call | `E-b.1. ElevenLabs text_to_speech.stream(voice_id, model_id)` | E-b | voice.py:61-75 | self | E-b.1. ElevenLabs text_to_speech.stream(voice_id, model_id, mp3_44100_128); the chunks are joined into one MP3 |
| E-b.2 | :PiperTTS / ElevenLabsTTS | :ai-service | ret | `E-b.2. Return MP3 bytes` | E-b | voice.py:73-75 |  | — |
| D-b.2 | :ai-service | :tts_cache (disk) | call | `D-b.2. put(key, audio) [best-effort]` | D-b | interview.py:198, tts_cache.py:62-77 | best-effort | D-b.2. put(key, audio) - written to a pid-suffixed tmp file then os.replace() for an atomic swap |
| D-b.3 | :tts_cache (disk) | :ai-service | ret | `D-b.3. Return` | D-b | tts_cache.py:75-77 | best-effort | D-b.3. Return - any disk error is logged and swallowed, the response is unaffected |
| D-b.4 | :ai-service | :AiServiceClient | ret | `D-b.4. Return 200 audio/mpeg (X-TTS-Cache: MISS)` | D-b | interview.py:199-207 |  | D-b.4. 200 audio/mpeg (X-TTS-Cache: MISS, X-TTS-Provider, X-TTS-Latency-Ms) |
| 12 | :AiServiceClient | :InterviewSessionController | ret | `12. Return byte[] MP3` | — | AiServiceClient.cs:78-86 |  | 12. Return byte[] MP3 (null when the body was empty - a last-resort guard, logged) |
| G.1 | :InterviewSessionController | :API Gateway | ret | `G.1. Return 502 { error: "tts_unavailable" }` | G | InterviewSessionController.cs:515-516 |  | — |
| G.2 | :API Gateway | :InterviewService (WebClient) | ret | `G.2. Return 502` | G | ApiGateway/appsettings.json:49-54 |  | — |
| G.3 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `G.3. Return (false, null, "TTS request failed (502).")` | G | InterviewService.cs:340-343 |  | — |
| G.4 | :InterviewController (WebClient) | :Interview Room UI | ret | `G.4. Return 400 { error }` | G | InterviewController.cs:462-465 |  | — |
| G.5 | :Interview Room UI | User | ret | `G.5. response.ok is false -> silent; nothing is spoken` | G | interview-room.js:1603 |  | — |
| 13 | :InterviewSessionController | :API Gateway | ret | `13. Return 200 File(audio, "audio/mpeg")` | — | InterviewSessionController.cs:517 |  | — |
| 14 | :API Gateway | :InterviewService (WebClient) | ret | `14. Return 200 audio/mpeg` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 15 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `15. Return (true, byte[] audio, null)` | — | InterviewService.cs:345-346 |  | — |
| 16 | :InterviewController (WebClient) | :Interview Room UI | ret | `16. Return 200 File(audio, "audio/mpeg")` | — | InterviewController.cs:467 |  | — |
| 17 | :Interview Room UI | (self) | call | `17. blob = await response.blob(); new Audio(...)` | — | interview-room.js:1604-1619 | self | 17. blob = await response.blob(); currentAudio?.pause(); new Audio(URL.createObjectURL(blob)); wire the .visualizer play / ended / error / pause listeners |
| H.1 | :Interview Room UI | (self) | call | `H.1. console.warn("[TTS] empty audio response") and return` | H | interview-room.js:1605-1608 | self | H.1. Empty blob -> console.warn("[TTS] empty audio response") and return |
| H.2 | :Interview Room UI | User | ret | `H.2. Nothing is spoken` | H | interview-room.js:1607 |  | — |
| 18 | :Interview Room UI | User | ret | `18. The interviewer voice plays and the waveform animates` | — | interview-room.js:1620-1628 |  | 18. The interviewer's voice plays and the waveform animates; speak() resolves on ended / error / pause (or immediately if play() is blocked by autoplay policy) so the caller can chain the next clip |
| 19 | User | :Interview Room UI | call | `19. Click the AI Voice toggle` | — | interview-room.js:1634, Room.cshtml:239 |  | — |
| 20 | :Interview Room UI | (self) | call | `20. aiVoiceOn = !aiVoiceOn; currentAudio?.pause()` | — | interview-room.js:1635-1641 | self | 20. aiVoiceOn = !aiVoiceOn; toggle .is-on + aria-pressed; pause the waveform; currentAudio?.pause() when switching off |
| 21 | :Interview Room UI | User | ret | `21. The switch flips; a playing clip stops at once` | — | interview-room.js:1640, 1623 |  | 21. The switch flips; a clip currently playing stops at once and its speak() resolves through onpause |
| J.1 | User | :Interview Room UI | call | `J.1. Click the microphone, record the answer, click again to stop` | J | interview-room.js:1135-1138, 1090-1092 |  | — |
| J.2 | :Interview Room UI | :InterviewController (WebClient) | call | `J.2. POST /Interview/Stt (multipart)` | J | interview-room.js:1099-1104 |  | J.2. POST /Interview/Stt (multipart: sessionId, voiceCheckId, audio=answer.webm) |
| J.3 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `J.3. TranscribeAudioAsync(sessionId, voiceCheckId, bytes, ...)` | J | InterviewController.cs:491-492 |  | J.3. TranscribeAudioAsync(sessionId, voiceCheckId, bytes, fileName, contentType, ct) |
| J.4 | :InterviewService (WebClient) | :API Gateway | call | `J.4. POST .../verify/voice-answer [see #16]` | J | InterviewService.cs:353-360 | boundary | J.4. POST api/interview/sessions/{sessionId}/verify/voice-answer - NOT /sessions/stt; Deepgram STT + the speaker check are #16 VerifyVoiceAnswer |
| J.5 | :API Gateway | :InterviewService (WebClient) | ret | `J.5. Return 200 SttResponse / non-2xx` | J | InterviewService.cs:361-372 | boundary | J.5. Return 200 SttResponse{Text, VoiceChecked, VoiceMatched, StrikeCount, ...} / non-2xx |
| J.6 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `J.6. Return (Success, SttResponse, Error)` | J | InterviewService.cs:366-372 |  | — |
| J.7 | :InterviewController (WebClient) | :Interview Room UI | ret | `J.7. Return 200 JSON / 400 { error }` | J | InterviewController.cs:494-499 |  | — |
| J.8 | :Interview Room UI | User | ret | `J.8. Transcript dropped into the answer box [see #16]` | J | interview-room.js:1106-1127 | boundary | J.8. Transcript dropped into the answer box to review before Send (#16 also renders any cheating warning) |

**Cờ:** `self` · `throw` · `best-effort` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| A | opt | `[AI Voice is off OR text is empty OR the Room has no TTS url]` | A.1 → A.2 | — | 3 (cùng `return` ngay, cùng "không gửi request") |
| B | opt | `[request.Text is null or whitespace (WebClient guard)]` | B.1 → B.2 | — | — |
| C | opt | `[req.Text is null or whitespace (InterviewAPI guard)]` | C.1 → C.5 | — | — |
| D | alt | D-a: `[cache HIT]` · D-b: `[cache MISS - or the cache is disabled / unreadable]` | D-a.1 → D-b.4 | — | — |
| F | opt | `[_tts.synthesize throws]` | F.1 → F.8 | D-b | — |
| E | alt | E-a: `[TTS_PROVIDER = "piper" (default, local)]` · E-b: `[TTS_PROVIDER = "elevenlabs" (legacy, still wired)]` | E-a.1 → E-b.2 | D-b | — |
| G | opt | `[audio is null OR audio.Length == 0]` | G.1 → G.5 | — | — |
| H | opt | `[the response blob is empty]` | H.1 → H.2 | — | — |
| J | opt | `[the candidate answers by voice instead of typing]` | J.1 → J.8 | — | — |

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 2 / 18 | `Every TTS failure path is deliberately silent: speak() checks response.ok, wraps everything in try/catch and never re-throws, so a broken TTS only means no audio - it can never block or delay the interview. That is why no error branch on this page ever shows the user a message.` |
| N2 | 9-11 | `The cache key covers the normalized text AND the provider, voice and speaker_id. Switching TTS_PROVIDER or the Piper voice therefore invalidates every entry automatically instead of serving audio recorded with the old voice.` |
| N3 | F.4 | `KI-02: ai-service reports a synthesis failure as 502 {"error":"tts_unavailable"}, but InterviewAPI's catch turns it into 400 ApiResponse.Fail(...) - while the "empty audio" guard right above returns a real 502. Two different statuses for the same user-visible outcome.` |
| N4 | J.4 | `POST /Interview/Stt does NOT reach POST api/interview/sessions/stt. That InterviewAPI endpoint works but has no caller anywhere in WebClient; the Room's STT proxy posts to .../verify/voice-answer instead, which transcribes AND runs the speaker check (#16).` |

---

## 7. CLASS — danh sách

> Chỉ liệt kê class .NET. Phía ai-service là **module Python** (`interview.py`, `tts_cache.py`,
> `voice.py`) — theo tiền lệ lô A/C không đưa vào trang Class; luồng của chúng đã thể hiện đủ ở
> trang Sequence (participant 7-9).

| # | Tên hiển thị | Loại | Liệt kê | Nguồn |
|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | SUBSET (`Tts`, `Stt`) | local |
| 2 | Controller | framework-base | FULL (rỗng) | SHARED |
| 3 | IInterviewService (WebClient) | interface | SUBSET | local |
| 4 | InterviewService (WebClient) | class | SUBSET | local |
| 5 | IApiClient / 6 ApiClient | interface/class | FULL | SHARED |
| 7 | TtsProxyRequest | DTO | FULL | local |
| 8 | SttResponse (WebClient) | DTO | FULL | local |
| 9 | ApiResponse<T> (WebClient) | DTO | FULL | local |
| 10 | ControllerBase | framework-base | FULL (rỗng) | SHARED |
| 11 | InterviewSessionController (InterviewAPI) | class | SUBSET (`TextToSpeech`, `SpeechToText`) | local |
| 12 | ApiResponse<T> (InterviewAPI) | DTO | FULL | SHARED |
| 13 | TtsRequest | DTO | FULL | local |
| 14 | SttResponse (InterviewAPI) | DTO | FULL | local |
| 15 | IAiServiceClient | interface | FULL | local |
| 16 | AiServiceClient (InterviewAPI) | class | FULL | SHARED |

## 8. CLASS — chi tiết

### `InterviewController (WebClient)` — `SUBSET`
- Field `- interviewService: IInterviewService`
- Method `+ Tts(request: TtsProxyRequest, cancellationToken)`, `+ Stt(sessionId, voiceCheckId, audio, cancellationToken)`

### `InterviewService (WebClient)` — `SUBSET`
- `+ GetTtsAudioAsync(text, cancellationToken)` (bespoke — không dùng `SendAsync<T>` vì trả
  `audio/mpeg` chứ không phải envelope), `+ TranscribeAudioAsync(...)`

### `InterviewSessionController (InterviewAPI)` — `SUBSET`
- Method `+ TextToSpeech(req: TtsRequest, cancellationToken)`,
  `+ SpeechToText(audio: IFormFile, cancellationToken)` — vẽ để thấy endpoint `/sessions/stt`
  tồn tại nhưng không có caller (§1).

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

Xem `specs/voiceinteraction_classes.yaml` khoá `relations:`.

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| KI-02 | `InterviewSessionController.cs:515-522` | Guard "audio rỗng" trả **502** `tts_unavailable`, còn `catch (Exception)` ngay dưới (ai-service trả 502 / unreachable / timeout) lại trả **400** | nhánh lỗi không nhất quán | **Vẽ thật cả 2** (`opt F` = 400, `opt G` = 502) + note N3 | Có (đã tồn tại) |
| — | `InterviewSessionController.cs:529-550` | `POST /sessions/stt` không có caller nào trong WebClient | code chết từ góc nhìn Browser | **Không vẽ** (§1) + note N4; vẫn giữ method trong trang Class để thấy nó tồn tại | không (đã ghi ở RECON) |

> Đã kiểm và **bác** 2 nghi vấn:
> (a) `SynthesizeSpeechAsync` trả `null!` khi body rỗng — có guard đối ứng đúng ở
> `TextToSpeech` (`audio is null || audio.Length == 0` → 502), không NRE.
> (b) Mọi lỗi TTS đều im lặng với người dùng — **cố ý**, comment code ghi rõ
> "TTS unavailable — stay silent, never block the interview"
> ([interview-room.js:1603](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L1603)).
> **Không phát hiện KI mới.**


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `B.2`)** KI-02: ai-service reports a synthesis failure as 502 {"error":"tts_unavailable"}, but InterviewAPI's catch turns it into 400 ApiResponse.Fail(...) - while the "empty audio" guard right above returns a real 502. Two different statuses for the same user-visible outcome. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | Vẽ `tts_cache` và provider TTS thành participant riêng hay gộp vào `:ai-service`? | **Tách riêng 2 participant** (`:tts_cache (disk)`, `:PiperTTS / ElevenLabsTTS`) | Cache hit/miss và lựa chọn provider chính là 2 khung rẽ nhánh trọng tâm của BL#42-43; gộp vào 1 lifeline sẽ biến chúng thành self-message và mất hẳn thông tin "ai làm gì". |
| 2 | 2 provider Piper / ElevenLabs | **1 lifeline chung `:PiperTTS / ElevenLabsTTS`, phân biệt bằng `alt E`** | Instance được chọn **một lần lúc import** (`voice.py:158-162`), tại runtime chỉ có đúng 1 object; vẽ 2 lifeline sẽ gợi ý sai là cả hai cùng tồn tại. |
| 3 | Nhánh STT | **`opt J` thu gọn 8 message, boundary ở mức Gateway** | Đúng khuyến nghị RECON ("giữ nhánh STT ở dạng opt gọn"); toàn bộ speaker-verify đã vẽ ở `#16`. |
| 4 | Guard `Text is required.` có ở CẢ WebClient (`opt B`) lẫn InterviewAPI (`opt C`) | **Vẽ cả hai, tách riêng** | Khác participant phát ra lỗi và khác đường về; `opt C` chỉ tới được khi gọi thẳng API — vẫn là guard sống (cùng cách xử lý như `#10` với G3/G4). |
| 5 | Nút toggle AI Voice (message 19-21) | **Vẽ như một tương tác riêng ở cuối trang** | Nó không nằm trong chuỗi `speak()` nhưng là thao tác user-facing duy nhất của BL#43; bỏ đi thì trang không còn chỗ nào thể hiện việc bật/tắt. |
| 6 | Module Python (`interview.py`, `voice.py`, `tts_cache.py`) trên trang Class | **Không vẽ** | Tiền lệ lô A/C: trang Class chỉ chứa class .NET. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `A.1`)** Every TTS failure path is deliberately silent: speak() checks response.ok, wraps everything in try/catch and never re-throws, so a broken TTS only means no audio - it can never block or delay the interview. That is why no error branch on this page ever shows the user a message. *(gỡ từ note trên diagram)*
- **(cạnh message `3`)** The cache key covers the normalized text AND the provider, voice and speaker_id. Switching TTS_PROVIDER or the Piper voice therefore invalidates every entry automatically instead of serving audio recorded with the old voice. *(gỡ từ note trên diagram)*
- **(cạnh message `6`)** POST /Interview/Stt does NOT reach POST api/interview/sessions/stt. That InterviewAPI endpoint works but has no caller anywhere in WebClient; the Room's STT proxy posts to .../verify/voice-answer instead, which transcribes AND runs the speaker check (#16). *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | RECON không ghi UC-id cho #6 | Nhóm xác nhận ê-líp use-case rồi điền vào `specs/voiceinteraction.yaml` | luồng 1 |
