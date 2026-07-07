// Drives the Interview Room: countdown timer, chat exchange, and camera preview.
// Chat calls go to same-origin WebClient proxy actions (not the Gateway directly), so the browser
// sends the HttpOnly JWT cookie automatically and the server attaches the Bearer header.
(function () {
    const root = document.getElementById("room-app");
    if (!root) return;

    const messageUrl = root.dataset.messageUrl;
    const endUrl = root.dataset.endUrl;
    const pauseUrl = root.dataset.pauseUrl;
    const unpauseUrl = root.dataset.unpauseUrl;
    const tourSeenUrl = root.dataset.tourSeenUrl;
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

    // Idle reminder / auto-advance state. Thresholds come from the server (SessionService.
    // GetIdleThresholds) and are re-sent with every question so the client never invents its own
    // numbers. idleSeconds starts from the server-computed elapsed value (non-zero on a page
    // refresh mid-question) so refreshing never grants free extra time.
    let idleReminderSeconds = parseInt(root.dataset.idleReminderSeconds, 10) || 90;
    let idleHardSeconds = parseInt(root.dataset.idleHardSeconds, 10) || 180;
    let idleSeconds = parseInt(root.dataset.idleElapsedSeconds, 10) || 0;
    let idleTier = 0; // 0 = none shown, 1 = reminder shown, 2 = warning+countdown shown
    const TIER2_LEAD_SECONDS = 20;

    // Pause/Resume state (Room UX Phase 2). Quota numbers are server-computed and re-synced from
    // every pause/unpause response — the client never invents or decrements them itself.
    let paused = root.dataset.initialStatus === "Paused";
    let pauseCount = parseInt(root.dataset.pauseCount, 10) || 0;
    let pausesRemaining = parseInt(root.dataset.pausesRemaining, 10) || 0;
    let pausedSecondsRemaining = parseInt(root.dataset.pausedSecondsRemaining, 10) || 0;

    // Product Tour state (Room UX Phase 3). tourActive only gates the idle detector for the
    // FIRST-EVER auto-run of the tour (before the interview has really started for this user) —
    // it must NOT gate the countdown timer (that stays in sync with the server's StartedAt) and
    // must NOT be touched by a manual "Replay guide" click mid-interview (see replayTourBtn below),
    // otherwise a user could spam Replay to stall the idle clock indefinitely.
    let hasSeenRoomTour = root.dataset.hasSeenTour === "true";
    let tourActive = false;

    const startedAtMs = Date.now();
    let ended = !isActive;

    const chatEl = document.getElementById("chat-messages");
    const inputEl = document.getElementById("answer-input");
    const sendBtn = document.getElementById("send-btn");
    const skipBtn = document.getElementById("skip-btn");
    const micBtn = document.getElementById("mic-btn");
    const hintBtn = document.getElementById("hint-btn");
    const endBtn = document.getElementById("end-btn");
    const pauseBtn = document.getElementById("pause-btn");
    const pauseBtnLabel = document.getElementById("pause-btn-label");
    const pauseQuotaText = document.getElementById("pause-quota-text");
    const pauseOverlayEl = document.getElementById("pause-overlay");
    const pauseOverlayQuotaEl = document.getElementById("pause-overlay-quota");
    const resumeBtn = document.getElementById("resume-btn");
    const timerEl = document.getElementById("timer");
    const progressEl = document.getElementById("progress-text");
    const statusEl = document.getElementById("status-text");
    const idleBannerEl = document.getElementById("idle-banner");
    const replayTourBtn = document.getElementById("replay-tour-btn");

    // Static, interviewer-voice reminder copy — placeholders pending the final UX copy deck.
    // Tier 1 never mentions time; Tier 2's countdown is appended separately in showTier2Banner.
    const TIER1_REMINDERS = [
        "Take your time — I'm here whenever you're ready to continue.",
        "No rush at all. Whenever you're ready, go ahead and share your answer.",
        "Feel free to think it through out loud — I'm listening whenever you're ready.",
        "Whenever you're ready, I'd love to hear your thoughts on this one."
    ];
    const TIER2_WARNINGS = [
        "Still there? I'll move on to the next question if I don't hear back.",
        "Just checking in — I'll continue to the next question if there's no response.",
        "I don't want to rush you, but I will move forward if you're not able to answer.",
        "Are you still with me? I'll proceed to the next question shortly."
    ];
    let lastTier1Index = -1;
    let lastTier2Index = -1;
    let tier2Text = "";

    function pickVariant(arr, lastIndex) {
        if (arr.length === 1) return { text: arr[0], index: 0 };
        let idx;
        do { idx = Math.floor(Math.random() * arr.length); } while (idx === lastIndex);
        return { text: arr[idx], index: idx };
    }

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

    // Reflects the server-computed pause quota in the sidebar button and the overlay. Disables
    // Pause once either limit is exhausted — the server enforces this independently on /pause,
    // this is display-only.
    function updatePauseQuotaUI() {
        const quotaLine = `${pausesRemaining} pause(s) left · ${pausedSecondsRemaining}s of pause time left`;
        if (pauseQuotaText) pauseQuotaText.textContent = quotaLine;
        if (pauseOverlayQuotaEl) pauseOverlayQuotaEl.textContent = quotaLine;

        const quotaExhausted = pausesRemaining <= 0 || pausedSecondsRemaining <= 0;
        if (pauseBtn) pauseBtn.disabled = ended || paused || quotaExhausted;
        if (pauseBtnLabel) pauseBtnLabel.textContent = quotaExhausted ? "Pause limit reached" : "Pause Interview";
    }

    // Paused freezes the countdown timer and the idle clock (both live in the same 1s interval —
    // see tickIdle's caller below) and covers the chat panel so pausing can't be used to read the
    // question carefully or look something up. The server independently blocks answer/auto-advance
    // calls while Status="Paused" (SendMessageAsync requires "InProgress"), so this is UX, not the
    // only line of defense.
    function setPausedUI(isPaused) {
        paused = isPaused;
        if (pauseOverlayEl) pauseOverlayEl.classList.toggle("d-none", !isPaused);

        const disableWhilePaused = isPaused || ended;
        if (inputEl) inputEl.disabled = disableWhilePaused;
        if (sendBtn) sendBtn.disabled = disableWhilePaused;
        if (skipBtn) skipBtn.disabled = disableWhilePaused;
        if (hintBtn) hintBtn.disabled = disableWhilePaused;
        if (micBtn) micBtn.disabled = disableWhilePaused;

        updatePauseQuotaUI();
    }

    async function pauseInterview() {
        if (paused || ended || !pauseUrl) return;
        if (pauseBtn) pauseBtn.disabled = true;
        try {
            const data = await postProxy(pauseUrl);
            pauseCount = data.pauseCount;
            pausesRemaining = data.pausesRemaining;
            pausedSecondsRemaining = data.pausedSecondsRemaining;
            if (statusEl) statusEl.textContent = "Paused";
            setPausedUI(true);
        } catch (err) {
            appendBubble("AI", `Could not pause: ${err.message}.`);
            updatePauseQuotaUI();
        }
    }

    async function resumeInterview() {
        if (!paused || !unpauseUrl) return;
        if (resumeBtn) resumeBtn.disabled = true;
        try {
            const data = await postProxy(unpauseUrl);
            remainingSeconds = data.remainingSeconds;
            pauseCount = data.pauseCount;
            pausesRemaining = data.pausesRemaining;
            pausedSecondsRemaining = data.pausedSecondsRemaining;
            updateTimerDisplay();
            if (statusEl) statusEl.textContent = "InProgress";
            setPausedUI(false);
        } catch (err) {
            appendBubble("AI", `Could not resume: ${err.message}.`);
        } finally {
            if (resumeBtn) resumeBtn.disabled = false;
        }
    }

    pauseBtn?.addEventListener("click", pauseInterview);
    resumeBtn?.addEventListener("click", resumeInterview);

    // Product Tour (Room UX Phase 3) — one driver.js instance, built fresh each run so it always
    // reflects the current DOM state (e.g. Pause button disabled state). Draft copy: the actual
    // spec-room-ux-enhancements.md text deck isn't in this repo, so wording here is a placeholder —
    // easy to edit, it's just a plain array of {element, popover} entries.
    function buildRoomTourSteps() {
        return [
            {
                element: "#chat-messages",
                popover: {
                    title: "Your Interview Chat",
                    description: "The AI interviewer asks questions here, and your answers show up as chat bubbles, just like a real conversation."
                }
            },
            {
                element: "#mic-btn",
                popover: {
                    title: "Answer by Voice",
                    description: "Record your answer instead of typing — it gets transcribed into the box below for you to review before sending."
                }
            },
            {
                element: "#voice-toggle",
                popover: {
                    title: "AI Voice",
                    description: "Turn this on to have the interviewer read each question out loud."
                }
            },
            {
                element: "#hint-btn",
                popover: {
                    title: "Ask for a Hint",
                    description: "Stuck on a question? Get one light, directional hint without giving away the full answer."
                }
            },
            {
                element: "#skip-btn",
                popover: {
                    title: "Skip a Question",
                    description: "Not sure? Skip it — it's graded as \"no answer provided\", never as a wrong attempt."
                }
            },
            {
                element: "#pause-btn",
                popover: {
                    title: "Pause the Interview",
                    description: "Need a short break? Pause a limited number of times — the countdown timer freezes while paused."
                }
            },
            {
                element: ".timer-box",
                popover: {
                    title: "Time Remaining",
                    description: "Your session countdown. It keeps running once the interview starts, including while you're reading this tour."
                }
            },
            {
                element: ".room-progress",
                popover: {
                    title: "Your Progress",
                    description: "Tracks how many questions you've answered out of the total planned for this session."
                }
            },
            {
                element: "#end-btn",
                popover: {
                    title: "End the Interview",
                    description: "Finish early any time you want — you'll be taken straight to your evaluation report."
                }
            }
        ];
    }

    // Fire-and-forget: tells the server this user has seen the tour so it never auto-shows again
    // (a failed request just means the auto-tour may show once more next session — not worth
    // blocking or retrying for).
    function markRoomTourSeen() {
        if (hasSeenRoomTour || !tourSeenUrl) return;
        hasSeenRoomTour = true;
        postProxy(tourSeenUrl).catch(() => {});
    }

    // onFinished covers every way the tour can end — completing the last step, clicking the X,
    // clicking the overlay, or pressing Escape — so "skip" and "finish" are handled identically.
    function runRoomTour(onFinished) {
        if (typeof window.driver === "undefined") {
            if (onFinished) onFinished();
            return;
        }
        const tour = window.driver.js.driver({
            showProgress: true,
            allowClose: true,
            overlayOpacity: 0.65,
            steps: buildRoomTourSteps(),
            onDestroyed: () => {
                if (onFinished) onFinished();
            }
        });
        tour.drive();
    }

    // Manual replay (header button): pure UI, never touches tourActive/idle gating — the interview
    // is already fully underway, so nothing needs to wait for this to finish.
    replayTourBtn?.addEventListener("click", () => {
        if (ended) return;
        runRoomTour();
    });

    function hideIdleBanner() {
        if (!idleBannerEl) return;
        idleBannerEl.className = "idle-banner d-none";
        idleBannerEl.innerHTML = "";
    }

    function showTier1Banner() {
        if (!idleBannerEl) return;
        const picked = pickVariant(TIER1_REMINDERS, lastTier1Index);
        lastTier1Index = picked.index;
        idleBannerEl.className = "idle-banner tier1";
        idleBannerEl.textContent = picked.text;
    }

    function showTier2Banner(secondsLeft) {
        if (!idleBannerEl) return;
        idleBannerEl.className = "idle-banner tier2";
        idleBannerEl.innerHTML = "";
        const msg = document.createElement("span");
        msg.textContent = `${tier2Text} (auto-advancing in ${secondsLeft}s)`;
        const cancelBtn = document.createElement("button");
        cancelBtn.type = "button";
        cancelBtn.className = "idle-banner-cancel";
        cancelBtn.textContent = "I'm still answering";
        cancelBtn.addEventListener("click", resetIdle);
        idleBannerEl.appendChild(msg);
        idleBannerEl.appendChild(cancelBtn);
    }

    // Rolling reset: any real user interaction cancels whatever idle tier is showing and restarts
    // the local idle clock. The server independently re-verifies idle from AskedAt when the client
    // later claims a "TimedOut" auto-advance, so this reset can never be exploited to skip early.
    function resetIdle() {
        if (idleSeconds === 0 && idleTier === 0) return;
        idleSeconds = 0;
        idleTier = 0;
        hideIdleBanner();
    }

    function tickIdle() {
        if (ended || !currentQuestionId) return;
        idleSeconds += 1;

        if (idleSeconds >= idleHardSeconds) {
            autoAdvance();
            return;
        }

        const tier2StartsAt = idleHardSeconds - TIER2_LEAD_SECONDS;
        if (idleSeconds >= tier2StartsAt) {
            if (idleTier !== 2) {
                idleTier = 2;
                const picked = pickVariant(TIER2_WARNINGS, lastTier2Index);
                lastTier2Index = picked.index;
                tier2Text = picked.text;
            }
            showTier2Banner(idleHardSeconds - idleSeconds);
        } else if (idleSeconds >= idleReminderSeconds) {
            if (idleTier !== 1) {
                idleTier = 1;
                showTier1Banner();
            }
        }
    }

    // Applies a freshly-served question (from send/skip/auto-advance responses) to local state,
    // including the server-supplied idle thresholds for that question.
    function applyNextQuestion(nextQuestion) {
        currentQuestionId = nextQuestion.sessionQuestionId;
        currentQuestionText = nextQuestion.content;
        idleReminderSeconds = nextQuestion.idleReminderSeconds || idleReminderSeconds;
        idleHardSeconds = nextQuestion.idleHardSeconds || idleHardSeconds;
        idleSeconds = nextQuestion.idleElapsedSeconds || 0;
        idleTier = 0;
        hideIdleBanner();
        appendBubble("AI", nextQuestion.content);
        speak(nextQuestion.content);
        inputEl.disabled = false;
        sendBtn.disabled = false;
        inputEl.focus();
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
            if (ended || paused) return;
            remainingSeconds -= 1;
            updateTimerDisplay();
            // Idle tracking only (never the timer) waits out the first-ever auto-tour — see the
            // tourActive comment near its declaration.
            if (!tourActive) tickIdle();
            if (remainingSeconds <= 0) {
                clearInterval(countdownHandle);
                finishInterview();
            }
        }, 1000);

        // Rolling reset on any real interaction — keydown/mousemove/click/focus all count.
        ["keydown", "mousemove", "click"].forEach((evt) =>
            document.addEventListener(evt, resetIdle, { passive: true })
        );
        window.addEventListener("focus", resetIdle);
    }

    async function finishInterview() {
        if (ended) return;
        ended = true;
        hideIdleBanner();
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

            applyNextQuestion(data.nextQuestion);
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
                content: "",
                reason: "Skipped"
            });

            answeredQuestions += 1;
            updateProgressDisplay();

            if (data.ended || !data.nextQuestion) {
                await finishInterview();
                return;
            }

            applyNextQuestion(data.nextQuestion);
            if (skipBtn) skipBtn.disabled = false;
        } catch (err) {
            appendBubble("AI", `Error: ${err.message}. Please try again.`);
            inputEl.disabled = false;
            sendBtn.disabled = false;
            if (skipBtn) skipBtn.disabled = false;
        }
    }

    // Auto-advance = the idle countdown reached the server-verified hard deadline with no
    // interaction. Submits the same empty answer as Skip, tagged reason "TimedOut" so the server
    // (and later, transcript/report views) can tell an inactivity timeout apart from a manual Skip.
    async function autoAdvance() {
        if (!currentQuestionId || ended) return;
        idleTier = 0;
        hideIdleBanner();

        inputEl.disabled = true;
        sendBtn.disabled = true;
        if (skipBtn) skipBtn.disabled = true;

        const note = document.createElement("div");
        note.className = "chat-skip-note";
        note.textContent = "Auto-advanced due to inactivity";
        chatEl.appendChild(note);
        chatEl.scrollTop = chatEl.scrollHeight;

        try {
            const data = await postProxy(messageUrl, {
                sessionQuestionId: currentQuestionId,
                content: "",
                reason: "TimedOut"
            });

            answeredQuestions += 1;
            updateProgressDisplay();

            if (data.ended || !data.nextQuestion) {
                await finishInterview();
                return;
            }

            applyNextQuestion(data.nextQuestion);
            if (skipBtn) skipBtn.disabled = false;
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
    // (for Hint) regardless of tour/pause state.
    if (isActive) {
        const aiBubbles = chatEl.querySelectorAll(".chat-bubble-ai:not(.chat-bubble-greeting)");
        const last = aiBubbles[aiBubbles.length - 1];
        if (last) currentQuestionText = last.textContent;
    }

    // With AI Voice on, speaks the static greeting first (fresh session only, so a mid-session
    // refresh doesn't re-greet) and then the current question — sequentially, never overlapping.
    // Skipped while paused (the overlay hides the question, so reading it aloud would defeat the
    // point). Called either right away (tour already seen) or after the first-ever tour finishes.
    function speakGreetingAndFirstQuestion() {
        if (!isActive || paused) return;
        const greetingEl = chatEl.querySelector(".chat-bubble-greeting");
        (async () => {
            if (greetingEl && answeredQuestions === 0) await speak(greetingEl.textContent);
            if (currentQuestionText) speak(currentQuestionText);
        })();
    }

    // Applies the initial Paused state (e.g. a page refresh while paused) and initializes the
    // pause-quota display either way.
    setPausedUI(paused);

    // First-ever visit to a Room for this user: the tour runs BEFORE greeting/TTS/idle-tracking so
    // a first-timer isn't hearing a question narrated while also reading tour popovers. Every later
    // visit speaks right away, exactly as before Phase 3.
    if (isActive && !hasSeenRoomTour) {
        tourActive = true;
        runRoomTour(() => {
            tourActive = false;
            markRoomTourSeen();
            speakGreetingAndFirstQuestion();
        });
    } else {
        speakGreetingAndFirstQuestion();
    }

    updateTimerDisplay();
    updateProgressDisplay();
})();
