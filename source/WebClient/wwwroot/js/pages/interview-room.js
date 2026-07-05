// Drives the Interview Room: countdown timer, chat exchange, and camera preview.
// Chat calls go to same-origin WebClient proxy actions (not the Gateway directly), so the browser
// sends the HttpOnly JWT cookie automatically and the server attaches the Bearer header.
(function () {
    const root = document.getElementById("room-app");
    if (!root) return;

    const messageUrl = root.dataset.messageUrl;
    const endUrl = root.dataset.endUrl;
    const ttsUrl = root.dataset.ttsUrl;
    const sttUrl = root.dataset.sttUrl;
    const hintUrl = root.dataset.hintUrl;
    const reportUrl = root.dataset.reportUrl;
    const isActive = root.dataset.isActive === "true";

    let currentQuestionId = root.dataset.currentQuestionId || null;
    let currentQuestionText = "";
    let totalQuestions = parseInt(root.dataset.totalQuestions, 10) || 0;
    let answeredQuestions = parseInt(root.dataset.answeredQuestions, 10) || 0;
    let remainingSeconds = parseInt(root.dataset.remainingSeconds, 10) || 0;

    const startedAtMs = Date.now();
    let ended = !isActive;

    const chatEl = document.getElementById("chat-messages");
    const inputEl = document.getElementById("answer-input");
    const sendBtn = document.getElementById("send-btn");
    const skipBtn = document.getElementById("skip-btn");
    const micBtn = document.getElementById("mic-btn");
    const hintBtn = document.getElementById("hint-btn");
    const endBtn = document.getElementById("end-btn");
    const timerEl = document.getElementById("timer");
    const progressEl = document.getElementById("progress-text");
    const statusEl = document.getElementById("status-text");

    async function postProxy(url, body) {
        const response = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: body ? JSON.stringify(body) : undefined
        });
        const data = await response.json().catch(() => null);
        if (!response.ok) {
            throw new Error((data && data.error) || `Request failed (${response.status}).`);
        }
        return data;
    }

    function formatTime(totalSeconds) {
        const clamped = Math.max(0, totalSeconds);
        const m = Math.floor(clamped / 60).toString().padStart(2, "0");
        const s = (clamped % 60).toString().padStart(2, "0");
        return `${m}:${s}`;
    }

    function updateTimerDisplay() {
        if (timerEl) timerEl.textContent = formatTime(remainingSeconds);
    }

    function updateProgressDisplay() {
        if (progressEl) progressEl.textContent = `${answeredQuestions}/${totalQuestions}`;
    }

    function appendBubble(senderType, content) {
        const row = document.createElement("div");
        row.className = senderType === "AI" ? "chat-row chat-row-ai" : "chat-row chat-row-user";
        const bubble = document.createElement("div");
        bubble.className = senderType === "AI" ? "chat-bubble chat-bubble-ai" : "chat-bubble chat-bubble-user";
        bubble.textContent = content;
        row.appendChild(bubble);
        chatEl.appendChild(row);
        chatEl.scrollTop = chatEl.scrollHeight;
    }

    let countdownHandle = null;
    if (isActive) {
        countdownHandle = setInterval(() => {
            if (ended) return;
            remainingSeconds -= 1;
            updateTimerDisplay();
            if (remainingSeconds <= 0) {
                clearInterval(countdownHandle);
                finishInterview();
            }
        }, 1000);
    }

    async function finishInterview() {
        if (ended) return;
        ended = true;
        if (inputEl) inputEl.disabled = true;
        if (sendBtn) sendBtn.disabled = true;
        if (endBtn) endBtn.disabled = true;
        if (skipBtn) skipBtn.disabled = true;
        if (hintBtn) hintBtn.disabled = true;
        if (statusEl) statusEl.textContent = "Ending…";

        // Static closing (mirrors ai-service's _closing_text), shown before leaving for the report.
        const closing = "That wraps up our interview — thank you for your time and thoughtful " +
            "answers! I'm now putting together your detailed evaluation.";
        appendBubble("AI", closing);

        // End the session server-side in parallel with the farewell — neither blocks the other.
        const endCall = postProxy(endUrl).catch(() => {
            // Session may already be Completed/Expired server-side; proceed to the report regardless.
        });

        // With AI Voice on, let the farewell finish speaking (10s safety cap so a hung clip never
        // strands the user); with it off, pause just long enough to read the bubble.
        if (aiVoiceOn) {
            await Promise.race([speak(closing), new Promise((resolve) => setTimeout(resolve, 10000))]);
        } else {
            await new Promise((resolve) => setTimeout(resolve, 2500));
        }
        await endCall;

        const elapsedSeconds = Math.round((Date.now() - startedAtMs) / 1000);
        window.location.href = `${reportUrl}?sessionId=${root.dataset.sessionId}&durationActualSec=${elapsedSeconds}`;
    }

    async function sendAnswer() {
        const content = inputEl.value.trim();
        if (!content || !currentQuestionId || ended) return;

        inputEl.value = "";
        inputEl.disabled = true;
        sendBtn.disabled = true;
        appendBubble("User", content);

        try {
            const data = await postProxy(messageUrl, {
                sessionQuestionId: currentQuestionId,
                content
            });

            answeredQuestions += 1;
            updateProgressDisplay();

            if (data.ended || !data.nextQuestion) {
                await finishInterview();
                return;
            }

            currentQuestionId = data.nextQuestion.sessionQuestionId;
            currentQuestionText = data.nextQuestion.content;
            appendBubble("AI", data.nextQuestion.content);
            speak(data.nextQuestion.content);
            inputEl.disabled = false;
            sendBtn.disabled = false;
            inputEl.focus();
        } catch (err) {
            appendBubble("AI", `Error: ${err.message}. Please try again.`);
            inputEl.disabled = false;
            sendBtn.disabled = false;
        }
    }

    // Skip = submit an EMPTY answer for the current question via the same message proxy. The empty
    // content still creates a User "Answer" message (so generate advances to the next question), and
    // ai-service grades an empty answer as a clean "No answer provided." (score 0) — never as a real
    // low-quality attempt. The chat flow is otherwise identical to a normal answer.
    async function skipQuestion() {
        if (!currentQuestionId || ended) return;

        inputEl.disabled = true;
        sendBtn.disabled = true;
        if (skipBtn) skipBtn.disabled = true;

        const note = document.createElement("div");
        note.className = "chat-skip-note";
        note.textContent = "Question skipped";
        chatEl.appendChild(note);
        chatEl.scrollTop = chatEl.scrollHeight;

        try {
            const data = await postProxy(messageUrl, {
                sessionQuestionId: currentQuestionId,
                content: ""
            });

            answeredQuestions += 1;
            updateProgressDisplay();

            if (data.ended || !data.nextQuestion) {
                await finishInterview();
                return;
            }

            currentQuestionId = data.nextQuestion.sessionQuestionId;
            currentQuestionText = data.nextQuestion.content;
            appendBubble("AI", data.nextQuestion.content);
            speak(data.nextQuestion.content);
            inputEl.disabled = false;
            sendBtn.disabled = false;
            if (skipBtn) skipBtn.disabled = false;
            inputEl.focus();
        } catch (err) {
            appendBubble("AI", `Error: ${err.message}. Please try again.`);
            inputEl.disabled = false;
            sendBtn.disabled = false;
            if (skipBtn) skipBtn.disabled = false;
        }
    }

    sendBtn?.addEventListener("click", sendAnswer);
    inputEl?.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            sendAnswer();
        }
    });
    skipBtn?.addEventListener("click", skipQuestion);
    endBtn?.addEventListener("click", finishInterview);

    // Hint: ask ai-service (via same-origin proxy) for one light directional hint on the current
    // question, then show it as an AI bubble prefixed with 💡. Does not change the message flow.
    async function askHint() {
        if (!currentQuestionText || ended || !hintUrl) return;

        if (hintBtn) hintBtn.disabled = true;
        try {
            const data = await postProxy(hintUrl, { question: currentQuestionText });
            appendBubble("AI", "💡 " + (data.hint || ""));
        } catch (err) {
            appendBubble("AI", `Could not get a hint: ${err.message}.`);
        } finally {
            if (hintBtn && !ended) hintBtn.disabled = false;
        }
    }

    hintBtn?.addEventListener("click", askHint);

    // Voice answer (STT): record with MediaRecorder, transcribe via the same-origin proxy, and drop
    // the text into the answer box for the user to review and send (never auto-sends).
    let mediaRecorder = null;
    let recordedChunks = [];

    async function startRecording() {
        // Required-format check up front: if the browser can't record webm (e.g. Safari), tell the
        // user clearly instead of failing silently. No alternate format fallback by design.
        if (typeof MediaRecorder === "undefined" || !MediaRecorder.isTypeSupported("audio/webm")) {
            appendBubble("AI", "Voice input isn't supported in this browser. Please type your answer instead.");
            return;
        }
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            recordedChunks = [];
            mediaRecorder = new MediaRecorder(stream, { mimeType: "audio/webm" });
            mediaRecorder.ondataavailable = (e) => { if (e.data.size > 0) recordedChunks.push(e.data); };
            mediaRecorder.onstop = async () => {
                stream.getTracks().forEach((t) => t.stop());
                micBtn.classList.remove("is-recording");
                await transcribe(new Blob(recordedChunks, { type: "audio/webm" }));
            };
            mediaRecorder.start();
            micBtn.classList.add("is-recording");
        } catch {
            appendBubble("AI", "Microphone unavailable or permission denied.");
        }
    }

    function stopRecording() {
        if (mediaRecorder && mediaRecorder.state === "recording") mediaRecorder.stop();
    }

    async function transcribe(blob) {
        if (!sttUrl) return;
        const previousPlaceholder = inputEl.placeholder;
        inputEl.placeholder = "Transcribing…";
        try {
            const form = new FormData();
            form.append("audio", blob, "answer.webm");
            const response = await fetch(sttUrl, { method: "POST", body: form });
            const data = await response.json().catch(() => null);
            if (!response.ok || !data) {
                appendBubble("AI", `Transcription failed${data && data.error ? ": " + data.error : ""}.`);
                return;
            }
            // Fill the box; the user reviews/edits and presses Send (existing message flow).
            inputEl.value = data.text || "";
            inputEl.focus();
        } catch (err) {
            appendBubble("AI", `Transcription error: ${err.message}.`);
        } finally {
            inputEl.placeholder = previousPlaceholder;
        }
    }

    micBtn?.addEventListener("click", () => {
        if (mediaRecorder && mediaRecorder.state === "recording") stopRecording();
        else startRecording();
    });

    // Camera preview — decorative only, the stream is never uploaded anywhere.
    const cameraVideo = document.getElementById("camera-video");
    const cameraPlaceholder = document.getElementById("camera-placeholder");
    const cameraToggleBtn = document.getElementById("camera-toggle");
    let cameraStream = null;

    async function startCamera() {
        try {
            cameraStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
            if (cameraVideo) {
                cameraVideo.srcObject = cameraStream;
                cameraVideo.classList.remove("d-none");
            }
            cameraPlaceholder?.classList.add("d-none");
            if (cameraToggleBtn) cameraToggleBtn.textContent = "Turn Off Camera";
        } catch {
            // Camera unavailable or permission denied — keep the placeholder.
        }
    }

    function stopCamera() {
        cameraStream?.getTracks().forEach((track) => track.stop());
        cameraStream = null;
        if (cameraVideo) {
            cameraVideo.srcObject = null;
            cameraVideo.classList.add("d-none");
        }
        cameraPlaceholder?.classList.remove("d-none");
        if (cameraToggleBtn) cameraToggleBtn.textContent = "Turn On Camera";
    }

    cameraToggleBtn?.addEventListener("click", () => (cameraStream ? stopCamera() : startCamera()));
    if (isActive) startCamera();

    // AI Voice (TTS) — speaks AI questions when the toggle is on. Text/message flow is unchanged;
    // this only adds audio playback of the AI bubbles.
    const voiceToggleBtn = document.getElementById("voice-toggle");
    let aiVoiceOn = voiceToggleBtn ? voiceToggleBtn.classList.contains("is-on") : false;
    let currentAudio = null;

    // Resolves when playback FINISHES (or immediately when voice is off / TTS fails), so callers
    // can chain clips — e.g. speak the greeting fully before speaking the first question.
    async function speak(text) {
        if (!aiVoiceOn || !text || !ttsUrl) return;
        try {
            const response = await fetch(ttsUrl, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ text })
            });
            if (!response.ok) return; // TTS unavailable — stay silent, never block the interview
            const blob = await response.blob();
            currentAudio?.pause();
            const audio = new Audio(URL.createObjectURL(blob));
            currentAudio = audio;
            await new Promise((resolve) => {
                audio.onended = resolve;
                audio.onerror = resolve;
                audio.onpause = resolve; // paused by toggle-off or a newer clip — stop waiting
                audio.play().catch(resolve); // autoplay blocked — ignore
            });
        } catch {
            // Network/TTS error — ignore, the interview continues silently.
        }
    }

    voiceToggleBtn?.addEventListener("click", () => {
        aiVoiceOn = !aiVoiceOn;
        voiceToggleBtn.classList.toggle("is-on", aiVoiceOn);
        voiceToggleBtn.setAttribute("aria-pressed", aiVoiceOn ? "true" : "false");
        if (!aiVoiceOn) currentAudio?.pause();
    });

    // On entering the room, the latest non-greeting AI bubble is the current question. Remember it
    // (for Hint) and, with AI Voice on, speak the static greeting first (fresh session only, so a
    // mid-session refresh doesn't re-greet) and then the question — sequentially, never overlapping.
    if (isActive) {
        const aiBubbles = chatEl.querySelectorAll(".chat-bubble-ai:not(.chat-bubble-greeting)");
        const last = aiBubbles[aiBubbles.length - 1];
        if (last) currentQuestionText = last.textContent;

        const greetingEl = chatEl.querySelector(".chat-bubble-greeting");
        (async () => {
            if (greetingEl && answeredQuestions === 0) await speak(greetingEl.textContent);
            if (currentQuestionText) speak(currentQuestionText);
        })();
    }

    updateTimerDisplay();
    updateProgressDisplay();
})();
