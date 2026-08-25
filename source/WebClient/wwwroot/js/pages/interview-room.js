// Drives the Interview Room: countdown timer, chat exchange, and camera preview.
// Chat calls go to same-origin WebClient proxy actions (not the Gateway directly), so the browser
// sends the HttpOnly JWT cookie automatically and the server attaches the Bearer header.
(function () {
    const root = document.getElementById("room-app");
    if (!root) return;

    const messageUrl = root.dataset.messageUrl;
    const completeUrl = root.dataset.completeUrl;
    const cancelUrl = root.dataset.cancelUrl;
    const pauseUrl = root.dataset.pauseUrl;
    const unpauseUrl = root.dataset.unpauseUrl;
    const cameraViolationUrl = root.dataset.cameraViolationUrl;
    const verifyInSessionUrl = root.dataset.verifyInSessionUrl;
    const inSessionCheckUrl = root.dataset.inSessionCheckUrl;
    const tourSeenUrl = root.dataset.tourSeenUrl;
    const ttsUrl = root.dataset.ttsUrl;
    const sttUrl = root.dataset.sttUrl;
    const hintUrl = root.dataset.hintUrl;
    const reportUrl = root.dataset.reportUrl;
    const detailsUrl = root.dataset.detailsUrl;
    const isActive = root.dataset.isActive === "true";
    const sessionId = root.dataset.sessionId;

    let currentQuestionId = root.dataset.currentQuestionId || null;
    let verificationCheckId = root.dataset.verificationCheckId || null;
    let verificationCheckExpiresAt = root.dataset.verificationCheckExpiresAt || null;
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

    // Safety bound (NOT a UX feature) against a stuck MediaRecorder: while recording, tickIdle()
    // freezes idleSeconds instead of advancing it (see tickIdle below), but if "recording" never
    // ends (mic error, stream dies without firing onstop, user never clicks stop), that freeze
    // would be unbounded and auto-advance could never fire again. Once this many consecutive
    // seconds of recording-freeze have elapsed, tickIdle stops freezing and resumes counting even
    // though mediaRecorder.state is still "recording".
    const RECORDING_FREEZE_CAP_SECONDS = 180;
    let recordingFreezeSeconds = 0;

    // Reentrancy guard for autoAdvance(): unlike sendAnswer/skipQuestion (locked by disabling the
    // button that triggers them), autoAdvance is fired by tickIdle() on a recurring 1s timer, which
    // keeps calling it every tick until idleSeconds resets — and that reset only happens once THIS
    // call's response is applied. Without this flag, several ticks queued up (e.g. after the tab was
    // backgrounded/throttled and catches up) each fire their own autoAdvance() while the first is
    // still in flight, producing duplicate "Auto-advanced" notes/requests for the same question.
    let autoAdvanceInFlight = false;

    // "AI is typing" delay before the next question is revealed, scaled to question length so a
    // long question doesn't feel instant and a short one doesn't feel sluggish.
    const TYPING_DELAY_BASE_MS = 800;
    const TYPING_DELAY_PER_CHAR_MS = 15;
    const TYPING_DELAY_MAX_MS = 2000;
    let typingTimeoutHandle = null;

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
    let activeTourCleanup = null;

    const startedAtMs = Date.now();
    let ended = !isActive;

    // Set exactly once, at the top of handleForcedCheatingEnd, when a strike forces the session
    // to end. Distinct from `ended` (which several legitimate paths — complete/cancel — also set)
    // so every outbound session request can unambiguously answer "was THIS specific session killed
    // by a cheating strike" and swallow a late response instead of rendering it. A request already
    // in flight when a strike lands has no other way to notice: its own promise resolves/rejects
    // after the fact with no idea the session is gone.
    let sessionTerminated = false;

    function buildReportUrl() {
        const query = new URLSearchParams({
            sessionId: root.dataset.sessionId,
            durationActualSec: Math.max(0, Math.round((Date.now() - startedAtMs) / 1000)).toString()
        });
        return `${reportUrl}?${query.toString()}`;
    }

    function isTerminalStatus(status) {
        return status === "Completed" || status === "Cancelled" || status === "Expired";
    }

    const chatEl = document.getElementById("chat-messages");
    const inputEl = document.getElementById("answer-input");
    const sendBtn = document.getElementById("send-btn");
    const skipBtn = document.getElementById("skip-btn");
    const micBtn = document.getElementById("mic-btn");
    const hintBtn = document.getElementById("hint-btn");
    const cancelInterviewBtn = document.getElementById("cancel-interview-btn");
    const cancelInterviewDialog = document.getElementById("cancel-interview-dialog");
    const continueInterviewBtn = document.getElementById("continue-interview-btn");
    const confirmCancelInterviewBtn = document.getElementById("confirm-cancel-interview-btn");
    const pauseBtn = document.getElementById("pause-btn");
    const pauseBtnLabel = document.getElementById("pause-btn-label");
    const pauseQuotaText = document.getElementById("pause-quota-text");
    const pauseOverlayEl = document.getElementById("pause-overlay");
    const pauseOverlayQuotaEl = document.getElementById("pause-overlay-quota");
    const resumeBtn = document.getElementById("resume-btn");
    const timerEl = document.getElementById("timer");
    const progressEl = document.getElementById("progress-text");
    const progressHeadEl = document.querySelector(".room-progress-head .v");
    const progressFillEl = document.querySelector(".progress-fill");
    const statusEl = document.getElementById("status-text");

    function setStatusDisplay(status) {
        if (!statusEl) return;
        statusEl.textContent = status === "InProgress" ? "Active" : status;
    }
    const idleBannerEl = document.getElementById("idle-banner");
    const idleModalBackdropEl = document.getElementById("idle-modal-backdrop");
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
            // message wins when present (e.g. Hint's {error: code, message: text} shape); other
            // endpoints only ever set "error" to a human-readable string, so this stays compatible.
            const err = new Error((data && (data.message || data.error)) || `Request failed (${response.status}).`);
            err.status = response.status;
            // Machine-readable code (e.g. "session_time_expired", "skip_cooldown") — checked
            // instead of err.status alone so a new 409/429 reason later can't be misread as this one.
            err.code = data && data.error ? data.error : null;
            // Server-computed countdown for 429s (e.g. Skip cooldown) — never derive this from
            // the client's own clock, which can be wrong or deliberately skewed.
            err.retryAfterSeconds = data && typeof data.retryAfterSeconds === "number" ? data.retryAfterSeconds : null;
            throw err;
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
        // Clamp the shown number so a race that slipped past the server guard (or any other
        // drift) can never display more than the session's real total.
        const shown = Math.min(answeredQuestions, totalQuestions);
        if (progressEl) progressEl.textContent = `${shown}/${totalQuestions}`;
        if (progressHeadEl) progressHeadEl.textContent = `${shown} / ${totalQuestions}`;
        if (progressFillEl) {
            const progressPercent = totalQuestions > 0 ? (shown / totalQuestions) * 100 : 0;
            progressFillEl.style.width = `${progressPercent}%`;
        }
    }

    // Server is the source of truth for progress (SessionService.SendMessageAsync now returns
    // it on every turn) — sync from the response instead of blindly incrementing locally, so a
    // duplicate/racing round-trip can't push the displayed count past reality. Falls back to a
    // local +1 only if an older/unexpected response shape omits the field.
    function syncAnsweredFrom(data) {
        answeredQuestions = typeof data.answeredQuestions === "number"
            ? data.answeredQuestions
            : answeredQuestions + 1;
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

    // Ticks the pause-time-remaining display down once per second WHILE paused, separate from
    // countdownHandle (which intentionally freezes — see setPausedUI below — so it can't drive
    // this). Purely a local display countdown: the server is still the source of truth and
    // overwrites pausedSecondsRemaining with the real value the moment Resume responds, so this
    // can't drift the actual quota, only how live the number looks while sitting on this screen.
    let pauseQuotaTickHandle = null;

    function startPauseQuotaTick() {
        if (pauseQuotaTickHandle) return;
        pauseQuotaTickHandle = setInterval(() => {
            if (pausedSecondsRemaining > 0) {
                pausedSecondsRemaining -= 1;
                updatePauseQuotaUI();
            }
            // Reached 0: the server is the actual source of truth for ending an expired pause
            // (SessionService.AutoResumeIfPauseExpiredAsync — it self-heals on the next request
            // that touches the session, i.e. Resume on reload or the active-session lookup, even
            // if this tab was closed the whole time). This call just makes an open tab react
            // immediately instead of waiting for that next request. Stop the tick first so a slow
            // response can't be raced by a second call next second.
            if (pausedSecondsRemaining <= 0) {
                stopPauseQuotaTick();
                resumeInterview();
            }
        }, 1000);
    }

    function stopPauseQuotaTick() {
        if (pauseQuotaTickHandle) {
            clearInterval(pauseQuotaTickHandle);
            pauseQuotaTickHandle = null;
        }
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

        if (isPaused) startPauseQuotaTick(); else stopPauseQuotaTick();

        updatePauseQuotaUI();
    }

    async function pauseInterview() {
        if (paused || ended || sessionTerminated || !pauseUrl) return;
        if (pauseBtn) pauseBtn.disabled = true;
        try {
            const data = await postProxy(pauseUrl);
            if (sessionTerminated) return; // ended mid-flight — the response is stale, drop it
            pauseCount = data.pauseCount;
            pausesRemaining = data.pausesRemaining;
            pausedSecondsRemaining = data.pausedSecondsRemaining;
            setStatusDisplay("Paused");
            setPausedUI(true);
        } catch (err) {
            if (sessionTerminated) return;
            appendBubble("AI", `Could not pause: ${err.message}.`);
            updatePauseQuotaUI();
        }
    }

    async function resumeInterview() {
        if (!paused || sessionTerminated || !unpauseUrl) return;
        if (resumeBtn) resumeBtn.disabled = true;
        try {
            const data = await postProxy(unpauseUrl);
            if (sessionTerminated) return; // ended mid-flight — the response is stale, drop it
            remainingSeconds = data.remainingSeconds;
            pauseCount = data.pauseCount;
            pausesRemaining = data.pausesRemaining;
            pausedSecondsRemaining = data.pausedSecondsRemaining;
            updateTimerDisplay();
            setStatusDisplay("InProgress");
            setPausedUI(false);
        } catch (err) {
            if (sessionTerminated) return;
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
                element: "#chat-messages",
                popover: {
                    title: "Your Interview Chat",
                    description: "The AI interviewer asks questions here, and your answers show up as chat bubbles, just like a real conversation."
                }
            },
            {
                element: ".camera-section",
                popover: {
                    title: "Camera Preview",
                    description: "Your camera stays on for the whole interview (anti-cheating) — it's a local preview only, never uploaded or recorded."
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
                element: "#cancel-interview-btn",                popover: {
                    title: "Cancel the Interview",
                    description: "Cancelling ends the session immediately. Submitted answers may still be evaluated, and Interview Feedback remains available."
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
    function clearDriverArtifacts() {
        document.body.classList.remove("driver-active", "driver-fade", "driver-simple");
        document.querySelectorAll(".driver-overlay, .driver-popover").forEach((element) => element.remove());
    }

    function destroyRoomTour() {
        const cleanup = activeTourCleanup;
        activeTourCleanup = null;
        if (cleanup) cleanup();
        clearDriverArtifacts();
        tourActive = false;
    }

    function runRoomTour(onFinished) {
        if (typeof window.driver === "undefined") {
            if (onFinished) onFinished();
            return;
        }

        if (activeTourCleanup) destroyRoomTour();
        clearDriverArtifacts();

        let notifyFinished = true;
        const tour = window.driver.js.driver({
            showProgress: true,
            allowClose: true,
            overlayOpacity: 0.65,
            steps: buildRoomTourSteps(),
            onDestroyed: () => {
                activeTourCleanup = null;
                clearDriverArtifacts();
                if (notifyFinished && onFinished) onFinished();
            }
        });
        activeTourCleanup = () => {
            notifyFinished = false;
            if (tour.isActive()) tour.destroy();
        };
        tour.drive();
    }

    // Manual replay (header button): pure UI, never touches tourActive/idle gating — the interview
    // is already fully underway, so nothing needs to wait for this to finish.
    replayTourBtn?.addEventListener("click", () => {
        if (ended) return;
        runRoomTour();
    });

    function hideIdleBanner() {
        idleModalBackdropEl?.classList.add("d-none");
        if (!idleBannerEl) return;
        idleBannerEl.className = "idle-banner d-none";
        idleBannerEl.innerHTML = "";
    }

    function showTier1Banner() {
        if (!idleBannerEl) return;
        const picked = pickVariant(TIER1_REMINDERS, lastTier1Index);
        lastTier1Index = picked.index;
        idleBannerEl.className = "idle-banner tier1";
        idleBannerEl.innerHTML = "";
        const msg = document.createElement("p");
        msg.className = "idle-banner-message";
        msg.textContent = picked.text;
        const okBtn = document.createElement("button");
        okBtn.type = "button";
        okBtn.className = "idle-banner-ok";
        okBtn.textContent = "OK";
        // Same reset the tier2 "I'm still answering" button uses: closes the popup and
        // restarts the local idle clock — counts as the user having interacted.
        okBtn.addEventListener("click", resetIdle);
        idleBannerEl.appendChild(msg);
        idleBannerEl.appendChild(okBtn);
        idleModalBackdropEl?.classList.remove("d-none");
        okBtn.focus();
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

        if (mediaRecorder && mediaRecorder.state === "recording") {
            if (recordingFreezeSeconds < RECORDING_FREEZE_CAP_SECONDS) {
                recordingFreezeSeconds += 1;
                return; // frozen: recording counts as answering, not idling
            }
            // Cap reached — a recorder stuck at "recording" must not freeze idle tracking forever.
            // Fall through and resume counting until it actually stops.
        } else {
            recordingFreezeSeconds = 0;
        }

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

    function typingDelayFor(text) {
        return Math.min(TYPING_DELAY_MAX_MS, TYPING_DELAY_BASE_MS + text.length * TYPING_DELAY_PER_CHAR_MS);
    }

    function showTypingIndicator() {
        const row = document.createElement("div");
        row.className = "chat-row chat-row-ai";
        const bubble = document.createElement("div");
        bubble.className = "chat-bubble chat-bubble-ai chat-bubble-typing";
        bubble.innerHTML = "<span class=\"typing-dot\"></span><span class=\"typing-dot\"></span><span class=\"typing-dot\"></span>";
        row.appendChild(bubble);
        chatEl.appendChild(row);
        chatEl.scrollTop = chatEl.scrollHeight;
        return row;
    }

    // Applies a freshly-served question (from send/skip/auto-advance responses) to local state,
    // including the server-supplied idle thresholds for that question. The question itself is
    // revealed after a short "AI is typing" delay (see showTypingIndicator).
    function applyNextQuestion(nextQuestion) {
        currentQuestionId = nextQuestion.sessionQuestionId;
        verificationCheckId = nextQuestion.verificationCheckId || null;
        verificationCheckExpiresAt = nextQuestion.verificationCheckExpiresAt || null;
        currentQuestionText = nextQuestion.content;
        idleReminderSeconds = nextQuestion.idleReminderSeconds || idleReminderSeconds;
        idleHardSeconds = nextQuestion.idleHardSeconds || idleHardSeconds;
        idleSeconds = nextQuestion.idleElapsedSeconds || 0;
        idleTier = 0;
        hideIdleBanner();

        if (typingTimeoutHandle) clearTimeout(typingTimeoutHandle);
        const typingRow = showTypingIndicator();
        typingTimeoutHandle = setTimeout(() => {
            typingTimeoutHandle = null;
            typingRow.remove();
            // The countdown timer can independently call finishInterview() while this delay is
            // in flight (e.g. it hits 0 mid-"typing"). Don't resurrect the input or show a now-stale
            // question bubble after the session has already ended.
            if (ended) return;
            appendBubble("AI", nextQuestion.content);
            speak(nextQuestion.content);
            inputEl.disabled = false;
            sendBtn.disabled = false;
            if (hintBtn) hintBtn.disabled = false;
            inputEl.focus();

            // One cheating check per new question, fired once the question is actually on
            // screen. Not awaited — the check runs in the background and must never delay the
            // interview. This is only ever reached for question 2 onward: the first question of
            // a session is server-rendered into the page, never routed through here, which is
            // exactly the "skip the first question" rule (the entry gate just verified this face).
            captureAndVerifyInSession();
        }, typingDelayFor(nextQuestion.content));
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
                completeInterview();
            }
        }, 1000);

        // Rolling reset scoped to real answering activity, not just "touching the computer".
        // keydown covers typing; input covers paste/IME composition/mobile input (neither reliably
        // fires keydown); click is narrowed to the controls involved in answering, not the whole
        // document. mousemove and window focus no longer count — see tickIdle for the recording
        // freeze that covers voice answers instead.
        document.addEventListener("keydown", resetIdle, { passive: true });
        inputEl?.addEventListener("input", resetIdle, { passive: true });
        [inputEl, micBtn, hintBtn, sendBtn].forEach((el) =>
            el?.addEventListener("click", resetIdle, { passive: true })
        );
    }

    function disableLifecycleControls() {
        if (typingTimeoutHandle) {
            clearTimeout(typingTimeoutHandle);
            typingTimeoutHandle = null;
        }
        document.querySelectorAll(".chat-bubble-typing").forEach((element) => element.closest(".chat-row")?.remove());        if (inputEl) inputEl.disabled = true;
        if (sendBtn) sendBtn.disabled = true;
        if (cancelInterviewBtn) cancelInterviewBtn.disabled = true;
        if (skipBtn) skipBtn.disabled = true;
        if (hintBtn) hintBtn.disabled = true;
    }

    function closeCancelConfirmation() {
        if (cancelInterviewDialog?.open) cancelInterviewDialog.close();
    }

    function cleanupTransientUi() {
        closeCancelConfirmation();
        destroyRoomTour();
        hideCheatingBanner();
    }

    function openCancelConfirmation() {
        if (ended) return;

        if (!cancelInterviewDialog || typeof cancelInterviewDialog.showModal !== "function") {
            // Kept in sync with #cancel-interview-description in Room.cshtml — this is the
            // fallback for browsers without <dialog> support, same warning either way.
            const warning = "Cancelling ends this session immediately. " +
                "Any answers already submitted may still be evaluated, and you can still submit Interview Feedback. " +
                "Credit already spent on this interview will not be refunded.";
            if (window.confirm(`Cancel this interview?\n\n${warning}`)) cancelInterview();
            return;
        }

        cancelInterviewDialog.showModal();
        continueInterviewBtn?.focus();
    }

    continueInterviewBtn?.addEventListener("click", closeCancelConfirmation);
    confirmCancelInterviewBtn?.addEventListener("click", function confirmCancellation() {
        closeCancelConfirmation();
        cancelInterview();
    });
    cancelInterviewDialog?.addEventListener("cancel", closeCancelConfirmation);
    cancelInterviewDialog?.addEventListener("click", function dismissFromBackdrop(event) {
        if (event.target === cancelInterviewDialog) closeCancelConfirmation();
    });
    // ---- Tab-switch / paste monitoring ----------------------------------------------------
    // Phase 1 is RECORD ONLY on the server (Modality="Tab" is not in the strike whitelist), so
    // nothing here can warn harshly or end the interview — the sidebar notice is the whole UI.
    //
    // A blur -> focus round trip is ONE event: measurement starts when focus leaves and the report
    // is sent on return, carrying the measured duration. The type is decided from
    // document.visibilityState at departure — "TabHidden" when the tab itself went to the
    // background, "WindowBlur" when the tab stayed visible but another window took focus. If the
    // user never comes back, pagehide flushes the pending measurement with sendBeacon.
    //
    // Deliberately timer-free: duration is derived from two Date.now() readings, so there is no
    // interval to leak across a bfcache freeze.
    const tabEventUrl = root.dataset.tabEventUrl;
    const TAB_GUARD_SUPPRESS_MS = 3000;

    // Real testing produced 4 paste events within 8 seconds — re-pasting/adjusting a code snippet
    // is a realistic pattern, and now that Paste can end the session (Phase 4), sending every one
    // of those individually would burn through the strike cap in seconds for one paste action.
    // Suppress a Paste report if the previous one was sent less than this long ago. Deliberately a
    // plain timestamp check, not a timer/queue: a burst is simply dropped down to one report, not
    // batched or delayed. Tab/WindowBlur do not need this — the begin/end pairing already
    // coalesces those into one event per departure.
    const PASTE_COALESCE_MS = 5000;

    let tabGuardSuppressedUntil = 0;
    let awayStartedAt = null;
    let awayWasHidden = false;
    let lastPasteSentAt = 0;

    // Browser permission prompts (microphone, camera) steal focus, and on some platforms hide the
    // tab outright — without this they would every single time be recorded as the candidate
    // leaving the interview. Call this IMMEDIATELY BEFORE anything that can raise a prompt; every
    // getUserMedia call site in this file does. This is the main noise control for the feature.
    function suppressTabGuard(ms) {
        const until = Date.now() + (ms || TAB_GUARD_SUPPRESS_MS);
        tabGuardSuppressedUntil = Math.max(tabGuardSuppressedUntil, until);
        // Drop any measurement already running: a blur that lands next to a prompt belongs to the
        // prompt, not to the candidate.
        awayStartedAt = null;
        awayWasHidden = false;
    }

    // Only monitor a live, running interview. A Paused or already-ended session is not
    // "InProgress" server-side and would be refused with 409 anyway.
    function tabGuardActive() {
        return !!tabEventUrl && isActive && !ended && !paused;
    }

    function newIncidentId() {
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
            return window.crypto.randomUUID();
        }
        // crypto.randomUUID needs a secure context; this keeps reporting alive on a plain-http
        // host (e.g. testing over a LAN IP) instead of throwing inside the event handler.
        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
            const r = (Math.random() * 16) | 0;
            return (c === "x" ? r : ((r & 0x3) | 0x8)).toString(16);
        });
    }

    function buildTabEventBody(eventType, durationMs) {
        return JSON.stringify({
            incidentId: newIncidentId(),
            eventType: eventType,
            durationMs: Math.max(0, Math.round(durationMs))
        });
    }

    // Calm and factual on purpose: Phase 1 carries no penalty, so the copy must not read like an
    // accusation. Reuses the existing sidebar notice — no new modal. Tone matches the other
    // integrity strings in this file ("Attention needed", "We couldn't see your face...").
    function tabEventNotice(eventType) {
        if (eventType === "Paste") {
            // Must state the OUTCOME, not just the observation. The paste handler now cancels the
            // insertion, so a user whose text never appears needs to be told that was deliberate —
            // "we noticed content was pasted" alongside an empty textarea reads as a broken app.
            return "Pasting is not allowed — the content was not inserted into your answer.";
        }
        if (eventType === "WindowBlur") {
            return "The interview window lost focus for a moment — please stay on this screen.";
        }
        return "We noticed you switched away from the interview tab — please stay on this screen.";
    }

    async function sendTabEvent(eventType, durationMs) {
        if (!tabGuardActive() || sessionTerminated) return;
        try {
            const response = await fetch(tabEventUrl, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: buildTabEventBody(eventType, durationMs)
            });
            const data = await response.json().catch(() => null);
            // A strike from a different source (Face, Camera, Voice) could have ended the session
            // while this request was in flight — this response is stale either way, drop it.
            if (sessionTerminated) return;
            // Only speak up when the server actually wrote a row. A below-threshold flicker, a
            // duplicate, or monitoring being switched off server-side all come back with
            // recorded=false and must stay silent.
            if (!response.ok || !data || !data.recorded) return;

            // data.tabCount is non-null ONLY for a freshly-recorded Tab/WindowBlur event (see
            // TabEventResultModel) — Paste never reaches the NoFace-style accumulation path and
            // is always its own full strike (handled below). This mirrors the Face flow's split
            // on data.warningType === "NoFace" vs. a real strike.
            if (data.tabCount != null) {
                if (!data.tabJustBecameStrike) {
                    // Gentle heads-up only — not a strike yet, same tier as the calm NoFace
                    // progress notice. The server does not bake progress text into data.message
                    // for Tab the way it does for NoFace, so the "(X/Y)" is built here.
                    setCheatingWarning(
                        `${tabEventNotice(eventType)} (${data.tabCount}/${data.tabStrikeThreshold})`,
                        "notice",
                        7000);
                    return;
                }

                // This event just completed a fresh group of TabStrikeThreshold and converted
                // into a real strike. data.message is already the server's "Repeated tab-switch
                // detections..." text (or the termination message) — same shape as the Face
                // flow's NoFace-just-became-strike case, which also uses data.message as the base
                // instead of a client-built string.
                const tabStrikeText = `${data.message} (${data.strikeCount}/${data.maxStrikes} warnings)`;
                setCheatingWarning(tabStrikeText, "warning");

                if (!data.shouldEndSession) {
                    showCheatingBanner(tabStrikeText, data.strikeCount === data.maxStrikes - 1);
                }

                if (data.shouldEndSession) {
                    await handleForcedCheatingEnd(data.message);
                }
                return;
            }

            // Otherwise this is a Paste event: every recorded Paste is its own full strike
            // (Phase 4), so data.strikeCount is always set here. Reuses tabEventNotice for the
            // base text (the server's own message for this path is just "Tab event recorded.",
            // not user-facing copy) and otherwise mirrors captureAndVerifyInSession's real-strike
            // branch exactly: same "(X/Y warnings)" suffix, same OK-only popup with the same
            // final-warning threshold, same forced-end handling — no parallel UI.
            const strikeText = `${tabEventNotice(eventType)} (${data.strikeCount}/${data.maxStrikes} warnings)`;
            setCheatingWarning(strikeText, "warning");

            if (!data.shouldEndSession) {
                showCheatingBanner(strikeText, data.strikeCount === data.maxStrikes - 1);
            }

            if (data.shouldEndSession) {
                await handleForcedCheatingEnd(data.message);
            }
        } catch {
            // Monitoring must never interrupt the interview: a failed report is simply dropped.
        }
    }

    function beginAway(hidden) {
        if (!tabGuardActive()) return;
        if (Date.now() < tabGuardSuppressedUntil) return;
        if (awayStartedAt !== null) {
            // One departure often fires blur AND visibilitychange. Keep the original start time,
            // but upgrade the classification if the tab really did go to the background.
            if (hidden) awayWasHidden = true;
            return;
        }
        awayStartedAt = Date.now();
        awayWasHidden = hidden;
    }

    function endAway() {
        if (awayStartedAt === null) return;
        const durationMs = Date.now() - awayStartedAt;
        const eventType = awayWasHidden ? "TabHidden" : "WindowBlur";
        awayStartedAt = null;
        awayWasHidden = false;
        // Returning from a permission prompt is not a return from a real departure.
        if (Date.now() < tabGuardSuppressedUntil) return;
        sendTabEvent(eventType, durationMs);
    }

    // Last chance to report a departure the user never came back from. sendBeacon survives the
    // page going away, which fetch() does not. Clears the pending measurement either way so a
    // bfcache restore cannot later flush the same one twice.
    function flushPendingTabEvent() {
        if (awayStartedAt === null) return;
        const durationMs = Date.now() - awayStartedAt;
        const eventType = awayWasHidden ? "TabHidden" : "WindowBlur";
        awayStartedAt = null;
        awayWasHidden = false;
        if (!tabGuardActive() || !navigator.sendBeacon) return;
        navigator.sendBeacon(
            tabEventUrl,
            new Blob([buildTabEventBody(eventType, durationMs)], { type: "application/json" }));
    }

    document.addEventListener("visibilitychange", function () {
        if (document.visibilityState === "hidden") beginAway(true);
        else endAway();
    });
    window.addEventListener("blur", function () {
        beginAway(document.visibilityState === "hidden");
    });
    window.addEventListener("focus", endAway);
    inputEl?.addEventListener("paste", function (event) {
        // Gate the block on the SAME condition that decides whether a warning can be shown, so
        // blocking and explaining always travel together. When monitoring is off or the session
        // is paused/ended, tabGuardActive() is false and sendTabEvent would no-op — blocking
        // there would swallow the paste with no message at all, which is precisely the "the app
        // looks broken" failure this change exists to avoid.
        if (!tabGuardActive()) return;

        // Cancel the insertion FIRST, before the coalescing check below. That check returns early
        // for repeat pastes inside PASTE_COALESCE_MS, so a preventDefault placed after it would
        // let the second and every later paste of a burst land in the textarea — the exact bug
        // being fixed, still present for anyone pasting twice in five seconds.
        //
        // Blocking is unconditional within an active session; REPORTING keeps its existing
        // coalescing untouched. Those are deliberately two different rules: every paste must be
        // stopped, but a burst must still cost only one strike.
        event.preventDefault();

        const now = Date.now();
        if (now - lastPasteSentAt < PASTE_COALESCE_MS) return;
        lastPasteSentAt = now;
        // Instantaneous by nature: durationMs=0 is the correct value, and the server exempts
        // "Paste" from the minimum-away threshold precisely so it still gets recorded.
        sendTabEvent("Paste", 0);
    });

    window.addEventListener("pagehide", function handlePageHide() {
        cleanupTransientUi();
        flushPendingTabEvent();
    });
    window.addEventListener("pageshow", function cleanupRestoredPage(event) {
        if (!event.persisted) return;
        cleanupTransientUi();
        // Restored from bfcache. This path fires WITHOUT a visibilitychange or focus pair, so
        // there is no return to report; pagehide already flushed and cleared the measurement.
        // Reset explicitly anyway so a start time from before the freeze can never resurface as a
        // bogus multi-minute event, and suppress briefly because the restore itself hands focus
        // back to the page.
        awayStartedAt = null;
        awayWasHidden = false;
        suppressTabGuard(TAB_GUARD_SUPPRESS_MS);
    });

    async function completeInterview() {
        if (ended) return;
        cleanupTransientUi();
        ended = true;
        if (cameraRecoveryTimer) {
            clearTimeout(cameraRecoveryTimer);
            cameraRecoveryTimer = null;
        }
        if (skipCooldownTimer) {
            clearInterval(skipCooldownTimer);
            skipCooldownTimer = null;
        }
        clearDraft();
        hideIdleBanner();
        disableLifecycleControls();
        setStatusDisplay("Finalizing...");

        let completion;
        try {
            completion = await postProxy(completeUrl);
        } catch (err) {
            appendBubble("AI", `Could not finalize the interview: ${err.message}`);
            window.location.href = detailsUrl;
            return;
        }

        const completed = completion.status === "Completed";
        const closing = completed
            ? "That wraps up our interview — thank you for your time and thoughtful answers! I'm now putting together your detailed evaluation."
            : "That wraps up this interview. The session has expired and remains available in your history.";
        appendBubble("AI", closing);
        setStatusDisplay(completion.status);

        // With AI Voice on, let the farewell finish speaking (10s safety cap so a hung clip never
        // strands the user); with it off, pause just long enough to read the bubble.
        if (aiVoiceOn) {
            await Promise.race([speak(closing), new Promise((resolve) => setTimeout(resolve, 10000))]);
        } else {
            await new Promise((resolve) => setTimeout(resolve, 2500));
        }
        window.location.href = isTerminalStatus(completion.status) ? buildReportUrl() : detailsUrl;
    }

    async function cancelInterview() {
        if (ended) return;
        cleanupTransientUi();
        ended = true;
        if (cameraRecoveryTimer) {
            clearTimeout(cameraRecoveryTimer);
            cameraRecoveryTimer = null;
        }
        if (skipCooldownTimer) {
            clearInterval(skipCooldownTimer);
            skipCooldownTimer = null;
        }
        clearDraft();
        hideIdleBanner();
        if (countdownHandle) clearInterval(countdownHandle);
        disableLifecycleControls();
        setStatusDisplay("Cancelling...");

        try {
            const completion = await postProxy(cancelUrl);
            setStatusDisplay(completion.status);
            window.location.href = detailsUrl;
        } catch (err) {
            ended = false;
            appendBubble("AI", `Could not cancel the interview: ${err.message}`);
            if (cancelInterviewBtn) cancelInterviewBtn.disabled = false;
            setPausedUI(paused);
        }
    }

    // Polite inline warning under the input (content filter) — auto-hides after a few seconds
    // or as soon as the user edits their answer. Not an alert(): stays in the Room's own UI.
    const contentWarningEl = document.getElementById("content-warning");
    let contentWarningTimer = null;

    function showContentWarning(text) {
        if (!contentWarningEl) return;
        contentWarningEl.textContent = text;
        contentWarningEl.classList.remove("d-none");
        if (contentWarningTimer) clearTimeout(contentWarningTimer);
        contentWarningTimer = setTimeout(hideContentWarning, 6000);
    }

    function hideContentWarning() {
        if (contentWarningTimer) { clearTimeout(contentWarningTimer); contentWarningTimer = null; }
        contentWarningEl?.classList.add("d-none");
    }

    inputEl?.addEventListener("input", hideContentWarning);

    // Auto-resize the answer textarea as the user types, up to MAX_INPUT_HEIGHT_PX (kept equal
    // to .chat-input's max-height in interview-room.css), then it scrolls internally instead of
    // pushing the rest of the Room around — same "grow, then scroll" pattern as Claude/ChatGPT.
    // Resetting height to "auto" before reading scrollHeight is required: scrollHeight only ever
    // grows otherwise, it never shrinks back down once text is deleted.
    const MAX_INPUT_HEIGHT_PX = 200;

    function autoResizeInput() {
        if (!inputEl) return;
        inputEl.style.height = "auto";
        inputEl.style.height = Math.min(inputEl.scrollHeight, MAX_INPUT_HEIGHT_PX) + "px";
    }

    inputEl?.addEventListener("input", autoResizeInput);

    // Answer draft persistence (Room UX) — sessionStorage so an F5 mid-answer doesn't lose what
    // the user was typing. Keyed by sessionId so switching sessions in the same browser can't mix
    // drafts up. Debounced on input (not saved on every keystroke); cleared once the answer is
    // actually sent or the interview ends, so nothing lingers past its useful lifetime.
    const answerDraftKey = `answer-draft:${root.dataset.sessionId}`;
    let draftSaveTimeoutHandle = null;

    function saveDraftNow() {
        if (!inputEl) return;
        if (inputEl.value) sessionStorage.setItem(answerDraftKey, inputEl.value);
        else sessionStorage.removeItem(answerDraftKey);
    }

    function scheduleDraftSave() {
        clearTimeout(draftSaveTimeoutHandle);
        draftSaveTimeoutHandle = setTimeout(saveDraftNow, 300);
    }

    function clearDraft() {
        clearTimeout(draftSaveTimeoutHandle);
        sessionStorage.removeItem(answerDraftKey);
    }

    function restoreDraft() {
        if (!inputEl || ended) return;
        const saved = sessionStorage.getItem(answerDraftKey);
        if (saved) {
            inputEl.value = saved;
            // Setting .value programmatically does not fire an "input" event, so the box would
            // otherwise stay at its 1-row height even when the restored draft is several lines.
            autoResizeInput();
        }
    }

    inputEl?.addEventListener("input", scheduleDraftSave);

    // Server rejected an answer/skip/auto-advance because the session's time already ran out
    // (SessionService.SendMessageAsync's deadline check — see report). The session was already
    // finalized server-side when this was thrown, so completeInterview() just fetches that
    // already-decided status and moves to the end screen — not a fresh completion decision.
    async function handleSessionExpired() {
        appendBubble("AI", "Your interview time has run out.");
        await completeInterview();
    }

    async function sendAnswer() {
        const content = inputEl.value.trim();
        if (!content || !currentQuestionId || ended || sessionTerminated) return;

        // Content filter (client mirror — server re-checks in SendMessageAsync). Runs BEFORE
        // the input is cleared / buttons locked / bubble appended, so a rejected answer stays
        // in the box for the user to fix and nothing about the Đợt-2 locking flow is touched.
        const filter = window.DevPrepContentFilter;
        if (filter && !filter.isAcceptable(content)) {
            showContentWarning("Your answer appears to contain inappropriate or unreadable content. Please review it and try again.");
            return;
        }

        inputEl.value = "";
        autoResizeInput();
        clearDraft();
        inputEl.disabled = true;
        sendBtn.disabled = true;
        // Was previously left enabled here, so a Skip click while Send was still in flight could
        // fire a second /message request for the SAME question (client-side half of the race that
        // caused duplicate bubbles) — close that gap.
        if (skipBtn) skipBtn.disabled = true;
        appendBubble("User", content);

        try {
            const data = await postProxy(messageUrl, {
                sessionQuestionId: currentQuestionId,
                content
            });
            // A strike from a different source could have ended the session while this request
            // was in flight (e.g. an in-session check fired right after Send) — this response is
            // stale either way, drop it silently rather than resurrecting the input/next question.
            if (sessionTerminated) return;

            syncAnsweredFrom(data);
            updateProgressDisplay();

            if (data.ended || !data.nextQuestion) {
                await completeInterview();
                return;
            }

            applyNextQuestion(data.nextQuestion);
            if (skipBtn) skipBtn.disabled = false;
        } catch (err) {
            // Expected, not a failure: the session was already force-ended by a strike (possibly
            // by a different in-flight request) before this one's 409 came back.
            if (sessionTerminated) return;
            if (err.code === "session_time_expired") {
                await handleSessionExpired();
                return;
            }
            appendBubble("AI", `Error: ${err.message} Please try again.`);
            inputEl.disabled = false;
            sendBtn.disabled = false;
            if (skipBtn) skipBtn.disabled = false;
        }
    }

    // Skip = submit an EMPTY answer for the current question via the same message proxy. The empty
    // content still creates a User "Answer" message (so generate advances to the next question), and
    // ai-service grades an empty answer as a clean "No answer provided." (score 0) — never as a real
    // low-quality attempt. The chat flow is otherwise identical to a normal answer.
    let skipCooldownTimer = null;

    // Countdown display for a 429 "skip_cooldown" response — UX only. The server (SessionService.
    // SendMessageAsync, Reason=="Skipped") is what actually enforces the cooldown; this just
    // mirrors it. `seconds` MUST come from the server's retryAfterSeconds — never computed from
    // the client's own clock, which can be wrong or deliberately skewed.
    function startSkipCooldown(seconds) {
        if (!skipBtn) return;
        if (skipCooldownTimer) clearInterval(skipCooldownTimer);
        skipBtn.disabled = true;
        let remaining = Math.max(1, Math.round(seconds));
        const countdownEl = document.createElement("span");
        countdownEl.className = "skip-cooldown-countdown";
        countdownEl.textContent = ` (${remaining}s)`;
        skipBtn.appendChild(countdownEl);

        skipCooldownTimer = setInterval(() => {
            remaining -= 1;
            if (ended || remaining <= 0) {
                clearInterval(skipCooldownTimer);
                skipCooldownTimer = null;
                countdownEl.remove();
                if (!ended) skipBtn.disabled = false;
                return;
            }
            countdownEl.textContent = ` (${remaining}s)`;
        }, 1000);
    }

    async function skipQuestion() {
        if (!currentQuestionId || ended || sessionTerminated) return;

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
            // A strike from a different source could have ended the session while this request
            // was in flight — this response is stale either way, drop it silently.
            if (sessionTerminated) return;

            syncAnsweredFrom(data);
            updateProgressDisplay();

            if (data.ended || !data.nextQuestion) {
                await completeInterview();
                return;
            }

            applyNextQuestion(data.nextQuestion);
            if (skipBtn) skipBtn.disabled = false;
        } catch (err) {
            // Expected, not a failure: the session was already force-ended by a strike before
            // this request's 409 came back.
            if (sessionTerminated) return;
            inputEl.disabled = false;
            sendBtn.disabled = false;
            if (err.code === "session_time_expired") {
                note.textContent = "Time's up — this skip did not go through.";
                await handleSessionExpired();
                return;
            }
            if (err.status === 429) {
                // The "Question skipped" note above already rendered before the server rejected
                // it — correct it rather than leave a note on screen for a skip that didn't happen.
                note.textContent = "Skip did not go through — please wait before trying again.";
                startSkipCooldown(err.retryAfterSeconds || 10);
            } else {
                appendBubble("AI", `Error: ${err.message} Please try again.`);
                if (skipBtn) skipBtn.disabled = false;
            }
        }
    }

    // Auto-advance = the idle countdown reached the server-verified hard deadline with no
    // interaction. Submits the same empty answer as Skip, tagged reason "TimedOut" so the server
    // (and later, transcript/report views) can tell an inactivity timeout apart from a manual Skip.
    async function autoAdvance() {
        if (!currentQuestionId || ended || sessionTerminated || autoAdvanceInFlight) return;
        autoAdvanceInFlight = true;
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
            // A strike from a different source could have ended the session while this request
            // was in flight — this response is stale either way, drop it silently.
            if (sessionTerminated) return;

            syncAnsweredFrom(data);
            updateProgressDisplay();

            if (data.ended || !data.nextQuestion) {
                await completeInterview();
                return;
            }

            applyNextQuestion(data.nextQuestion);
            if (skipBtn) skipBtn.disabled = false;
        } catch (err) {
            // Expected, not a failure: the session was already force-ended by a strike before
            // this request's 409 came back.
            if (sessionTerminated) return;
            if (err.code === "session_time_expired") {
                await handleSessionExpired();
                return;
            }
            appendBubble("AI", `Error: ${err.message} Please try again.`);
            inputEl.disabled = false;
            sendBtn.disabled = false;
            if (skipBtn) skipBtn.disabled = false;
        } finally {
            autoAdvanceInFlight = false;
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
    cancelInterviewBtn?.addEventListener("click", openCancelConfirmation);

    // Hint: ask ai-service (via same-origin proxy) for one light directional hint on the current
    // question, then show it as an AI bubble prefixed with 💡. Does not change the message flow.
    // Server enforces one hint per question (SessionService.HintAsync) — the button stays
    // disabled after use, or on a 409 "already used", and only re-enables on the NEXT question
    // (applyNextQuestion). This client-side disable is UX only, not the security boundary.
    async function askHint() {
        if (!currentQuestionText || !currentQuestionId || ended || sessionTerminated || !hintUrl) return;

        if (hintBtn) hintBtn.disabled = true;
        try {
            const data = await postProxy(hintUrl, { sessionId, sessionQuestionId: currentQuestionId });
            // A strike from a different source could have ended the session while this request
            // was in flight — this response is stale either way, drop it silently.
            if (sessionTerminated) return;
            const hintText = data.hint || "";
            appendBubble("AI", "💡 " + hintText);
            // Text first, audio second: the user reads immediately without waiting on TTS. Not
            // awaited — speak() already no-ops when AI Voice is off and swallows its own TTS
            // errors, so a failure here can never affect the hint bubble already on screen.
            // It also interrupts whatever audio is currently playing (e.g. the question being
            // read aloud) — intended, the hint should be heard right away.
            speak(hintText);
            // Leave disabled: this question's one hint is now spent.
        } catch (err) {
            // Expected, not a failure: the session was already force-ended by a strike before
            // this request's error came back.
            if (sessionTerminated) return;
            if (err.status === 409) {
                appendBubble("AI", "You've already used your hint for this question.");
                // Leave disabled: matches the server's one-hint-per-question state.
            } else {
                appendBubble("AI", `Could not get a hint: ${err.message}.`);
                if (hintBtn && !ended) hintBtn.disabled = false;
            }
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
            // Before the mic permission prompt: it takes focus (and can hide the tab), which the
            // tab guard would otherwise record as leaving the interview.
            suppressTabGuard(TAB_GUARD_SUPPRESS_MS);
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
        if (!sttUrl || sessionTerminated) return;
        const previousPlaceholder = inputEl.placeholder;
        inputEl.placeholder = "Transcribing…";
        try {
            const form = new FormData();
            const voiceCheckId = window.crypto.randomUUID();
            form.append("sessionId", sessionId);
            form.append("voiceCheckId", voiceCheckId);
            form.append("audio", blob, "answer.webm");
            const response = await fetch(sttUrl, { method: "POST", body: form });
            const data = await response.json().catch(() => null);
            // A strike from a different source could have ended the session while this recording
            // was being transcribed — this response is stale either way, drop it silently.
            if (sessionTerminated) return;
            if (!response.ok || !data) {
                appendBubble("AI", `Transcription failed${data && data.error ? ": " + data.error : ""}.`);
                return;
            }

            if (data.voiceMatched === false) {
                const strikeText = `${data.message} (${data.strikeCount}/${data.maxStrikes} warnings)`;
                setCheatingWarning(strikeText, "warning", 10000);

                if (data.shouldEndSession) {
                    await handleForcedCheatingEnd(data.message);
                    return;
                }

                showCheatingBanner(strikeText, data.strikeCount === data.maxStrikes - 1);
            }

            // Fill the box; the user reviews/edits and presses Send (existing message flow).
            inputEl.value = data.text || "";
            resetIdle(); // programmatic .value assignment fires no "input" event — reset explicitly
            autoResizeInput();
            inputEl.focus();
        } catch (err) {
            if (sessionTerminated) return;
            appendBubble("AI", `Transcription error: ${err.message}.`);
        } finally {
            inputEl.placeholder = previousPlaceholder;
        }
    }

    micBtn?.addEventListener("click", () => {
        if (mediaRecorder && mediaRecorder.state === "recording") stopRecording();
        else startRecording();
    });

    // Camera is mandatory. The live stream stays local, while individual verification frames
    // are uploaded for in-session identity checks. A lost camera gets a reconnect attempt every
    // 15-second grace period, repeating indefinitely until it comes back (or the session ends);
    // only the FIRST failed attempt of each continuous loss reports a cheating strike.
    const cameraVideo = document.getElementById("camera-video");
    const cameraPlaceholder = document.getElementById("camera-placeholder");
    const cameraStatusEl = document.getElementById("camera-status");
    const CAMERA_RECONNECT_GRACE_MS = 15000;
    let cameraStream = null;
    let cameraRecoveryTimer = null;
    // Guards against reporting more than one strike for the SAME ongoing camera-loss episode —
    // scheduleCameraRecovery keeps retrying connectCamera() every 15s for as long as the camera
    // stays down, but only the FIRST failed retry in that streak should count as a violation.
    let cameraIncidentReported = false;

    function setCameraStatus(text, isWarning) {
        if (!cameraStatusEl) return;
        cameraStatusEl.textContent = text;
        cameraStatusEl.classList.toggle("camera-status-warning", !!isWarning);
    }

    function createIncidentId() {
        if (typeof crypto.randomUUID === "function") return crypto.randomUUID();
        const bytes = crypto.getRandomValues(new Uint8Array(16));
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;
        const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join("");
        return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
    }

    async function reportCameraViolation(reason) {
        if (!cameraViolationUrl || ended || sessionTerminated) return;
        try {
            const data = await postProxy(cameraViolationUrl, {
                incidentId: createIncidentId(),
                reason
            });
            // A strike from a different source could have ended the session while this request
            // was in flight — this response is stale either way, drop it silently.
            if (sessionTerminated) return;
            if (data.duplicate) return;

            const strikeText = `${data.message} (${data.strikeCount}/${data.maxStrikes} warnings)`;
            setCheatingWarning(strikeText, "critical");
            if (!data.shouldEndSession) {
                showCheatingBanner(strikeText, data.strikeCount === data.maxStrikes - 1);
                return;
            }

            await handleForcedCheatingEnd(data.message);
        } catch (error) {
            if (sessionTerminated) return;
            setCheatingWarning(`Camera incident could not be reported: ${error.message}`, "warning");
        }
    }

    function showCameraUnavailable(reason) {
        if (cameraVideo) {
            cameraVideo.srcObject = null;
            cameraVideo.classList.add("d-none");
        }
        cameraPlaceholder?.classList.remove("d-none");
        const label = reason === "PermissionDenied" ? "Camera: Access required" : "Camera: Disconnected";
        setCameraStatus(`${label} — retrying in 15s`, true);
    }

    async function connectCamera() {
        // Same reasoning as startRecording: suppress before the camera prompt. Every camera path
        // (initial startCamera, and each scheduleCameraRecovery retry) funnels through here, so
        // this one call covers them all.
        suppressTabGuard(TAB_GUARD_SUPPRESS_MS);
        const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
        cameraStream = stream;
        // Camera is back — the next disconnect is a NEW episode and may report again.
        cameraIncidentReported = false;
        if (cameraVideo) {
            cameraVideo.srcObject = stream;
            cameraVideo.classList.remove("d-none");
        }
        cameraPlaceholder?.classList.add("d-none");
        setCameraStatus("Camera: On", false);

        const [track] = stream.getVideoTracks();
        track?.addEventListener("ended", () => {
            if (cameraStream !== stream || ended) return;
            cameraStream = null;
            scheduleCameraRecovery("Disconnected");
        });
        if (verificationCheckId) captureAndVerifyInSession();
    }

    function scheduleCameraRecovery(reason) {
        if (ended || cameraRecoveryTimer) return;
        showCameraUnavailable(reason);
        cameraRecoveryTimer = setTimeout(async () => {
            cameraRecoveryTimer = null;
            try {
                await connectCamera();
            } catch {
                showCameraUnavailable(reason);
                setCameraStatus("Camera: Recovery failed", true);
                // At most one violation per continuous camera-loss episode — the retry loop below
                // keeps running regardless, so a camera left off doesn't rack up a strike every 15s.
                if (!cameraIncidentReported) {
                    cameraIncidentReported = true;
                    await reportCameraViolation(reason);
                }
                // Keep retrying until connectCamera() succeeds or the session ends — previously
                // this stopped after one failed attempt, permanently killing detection for the
                // rest of the session (see bug report).
                scheduleCameraRecovery(reason);
            }
        }, CAMERA_RECONNECT_GRACE_MS);
    }

    async function startCamera() {
        try {
            await connectCamera();
        } catch {
            scheduleCameraRecovery("PermissionDenied");
        }
    }

    if (isActive) startCamera();

    // In-session cheating check — runs once per NEW QUESTION (see applyNextQuestion), grabbing
    // ONE frame from the already-running cameraStream (same canvas.drawImage → toBlob technique
    // as biometric-wizard.js/entry-verify.js) and posting it for a single-frame match +
    // multi-face check. Flag-only by default (server config) — never blocks answering on its
    // own; only ends the session when the server says shouldEndSession (Strictness="Strict" +
    // strike cap hit).
    //
    // The first question is deliberately NOT checked: the entry gate verified this same face
    // seconds earlier, so re-checking immediately costs an ai-service call for no new signal.
    // Rapid skipping is absorbed server-side rather than here — a client-side interval could be
    // bypassed, and this protects a shared resource. The server issues at most one pending
    // checkId per question and consumes it on first use, so skipping fast cannot buy extra
    // ai-service calls; an unused checkId simply expires (Biometrics:InSessionCheckTtlSeconds,
    // 20 seconds by default).
    const cheatingWarningEl = document.getElementById("cheating-warning");
    const integrityStatusIconEl = document.getElementById("integrity-status-icon");
    const integrityStatusTitleEl = document.getElementById("integrity-status-title");
    const integrityStatusMessageEl = document.getElementById("integrity-status-message");
    const inSessionCanvas = document.createElement("canvas");
    let inSessionCheckInFlight = false;
    let integrityAlertVisibleUntil = 0;
    let integrityAlertHideTimer = null;

    // tone controls which .cheating-warning--* class is applied (see interview-room.css):
    // "notice" (gentle no-face reminder, not yet a strike), "warning" (identity mismatch — could
    // be a lighting/angle issue), "critical" (multiple faces / a strike just got recorded — the
    // most serious category, worth a visually distinct look from a plain mismatch).
    const cheatingWarningTones = ["notice", "warning", "critical"];

    function setCheatingWarning(text, tone, minimumVisibleMs) {
        if (!cheatingWarningEl) return;
        if (integrityAlertHideTimer) {
            clearTimeout(integrityAlertHideTimer);
            integrityAlertHideTimer = null;
        }
        if (text && minimumVisibleMs === 0 && Date.now() < integrityAlertVisibleUntil)
            return;
        cheatingWarningTones.forEach((t) => cheatingWarningEl.classList.remove(`integrity-alert--${t}`));
        if (!text) {
            const remainingVisibleMs = integrityAlertVisibleUntil - Date.now();
            if (remainingVisibleMs > 0) {
                integrityAlertHideTimer = setTimeout(
                    () => setCheatingWarning(null, null, 0),
                    remainingVisibleMs);
                return;
            }
            cheatingWarningEl.classList.add("d-none");
            if (integrityStatusMessageEl) integrityStatusMessageEl.textContent = "";
            return;
        }
        const effectiveTone = tone || "notice";
        const visibleMs = minimumVisibleMs ?? (effectiveTone === "notice" ? 7000 : 10000);
        integrityAlertVisibleUntil = Date.now() + visibleMs;
        cheatingWarningEl.classList.add(`integrity-alert--${effectiveTone}`);
        cheatingWarningEl.classList.remove("d-none");
        if (integrityStatusIconEl) {
            integrityStatusIconEl.textContent = effectiveTone === "notice" ? "visibility" : "warning";
        }
        if (integrityStatusTitleEl) {
            integrityStatusTitleEl.textContent = effectiveTone === "notice"
                ? "Attention needed"
                : effectiveTone === "critical" ? "Integrity violation" : "Verification warning";
        }
        if (integrityStatusMessageEl) integrityStatusMessageEl.textContent = text;
    }

    // Maps the server's WarningType to a client-rendered tone + prefix. NoFace only escalates to
    // "critical" the exact check it converts into a strike (data.noFaceJustBecameStrike) — every
    // other NoFace check is just a gentle heads-up, not a cheating accusation.
    function toneForWarning(data) {
        if (data.warningType === "MultiFace") return "critical";
        if (data.warningType === "NoFace") return data.noFaceJustBecameStrike ? "critical" : "notice";
        if (data.warningType === "IdentityMismatch") return "warning";
        return "notice";
    }

    const qualityWarningTypes = new Set([
        "ImageTooBlurry",
        "FaceTooSmall",
        "LowDetectionConfidence",
        "CannotDecode"
    ]);

    function isVerificationTokenError(message) {
        if (!message) return false;
        const normalized = message.toLowerCase();
        return normalized.includes("verification check token")
            || normalized.includes("token is invalid")
            || normalized.includes("token is expired")
            || normalized.includes("token has expired")
            || normalized.includes("token is already used");
    }

    // Cheating strike popup — reuses the exact .idle-banner/.idle-modal-backdrop classes and
    // "tier1" popup shell the idle reminder above already uses, as a separate DOM instance (own
    // ids) so the two can never collide if they happen to fire close together. Only shown for a
    // REAL strike (tone "warning"/"critical") — a "notice" (no-face, not yet a strike) stays
    // sidebar-only via setCheatingWarning so it doesn't interrupt for something that isn't a
    // strike yet. Dismissed by an OK button only, no auto-hide timer: Strictness="Strict" means
    // missing a warning can cost the whole interview (and its credit, non-refundable), so this
    // follows the same reasoning the idle Tier-1 popup already uses for its own OK-only button —
    // an auto-hide timer risks the user never seeing it if they glance away right when it appears.
    const cheatingBannerEl = document.getElementById("cheating-banner");
    const cheatingBannerBackdropEl = document.getElementById("cheating-banner-backdrop");

    function hideCheatingBanner() {
        cheatingBannerBackdropEl?.classList.add("d-none");
        if (!cheatingBannerEl) return;
        cheatingBannerEl.className = "idle-banner d-none";
        cheatingBannerEl.innerHTML = "";
    }

    // isFinalWarning = this strike is one away from the cap (e.g. 2/3) — the NEXT strike ends the
    // interview, so this popup gets extra emphasis (its own title + an explicit "one more strike"
    // line) instead of reading like a routine reminder.
    function showCheatingBanner(strikeText, isFinalWarning) {
        if (!cheatingBannerEl) return;
        cheatingBannerEl.className = isFinalWarning ? "idle-banner tier1 cheating-banner--final" : "idle-banner tier1";
        cheatingBannerEl.innerHTML = "";

        const title = document.createElement("p");
        title.className = "idle-banner-message cheating-banner-title";
        title.textContent = isFinalWarning ? "Final Warning" : "Cheating Warning";

        const body = document.createElement("p");
        body.className = "idle-banner-message";
        body.textContent = isFinalWarning
            ? `${strikeText} One more strike will end this interview, with no credit refund.`
            : strikeText;

        const okBtn = document.createElement("button");
        okBtn.type = "button";
        okBtn.className = "idle-banner-ok";
        okBtn.textContent = "OK";
        okBtn.addEventListener("click", hideCheatingBanner);

        cheatingBannerEl.appendChild(title);
        cheatingBannerEl.appendChild(body);
        cheatingBannerEl.appendChild(okBtn);
        cheatingBannerBackdropEl?.classList.remove("d-none");
        okBtn.focus();
    }

    async function handleForcedCheatingEnd(message) {
        // Set FIRST, before anything else: every outbound session request (send/skip/hint/pause/
        // resume/in-session verify/tab event/STT) checks this and either refuses to start or
        // swallows a response that was already in flight when the strike landed — otherwise a
        // late 409 renders as a stray "Session is not in progress." chat bubble right under this
        // function's own termination message (see report).
        sessionTerminated = true;
        ended = true;
        // Stop the question timer — reusing the exact same guard cancelInterview()/completeInterview()
        // rely on (the interval checks `ended`), plus an explicit clear so it can't tick even once
        // more while the termination modal is up.
        if (countdownHandle) {
            clearInterval(countdownHandle);
            countdownHandle = null;
        }
        stopPauseQuotaTick();
        if (cameraRecoveryTimer) {
            clearTimeout(cameraRecoveryTimer);
            cameraRecoveryTimer = null;
        }
        if (skipCooldownTimer) {
            clearInterval(skipCooldownTimer);
            skipCooldownTimer = null;
        }
        clearDraft();
        disableLifecycleControls();
        // Same status-badge update the normal end-of-session path uses (completeInterview/
        // cancelInterview both call setStatusDisplay with the server's real status) — a
        // cheating-forced end always lands on "Cancelled" server-side (EndSessionForCheatingAsync).
        setStatusDisplay("Cancelled");
        appendBubble("AI", message);
        await showCheatingTerminationModal(message);
        window.location.href = "/Interview";
    }

    function showCheatingTerminationModal(message) {
        return new Promise((resolve) => {
            if (!cheatingBannerEl) {
                resolve();
                return;
            }

            cheatingBannerEl.className = "idle-banner tier1 cheating-banner--final";
            cheatingBannerEl.innerHTML = "";

            const title = document.createElement("p");
            title.className = "idle-banner-message cheating-banner-title";
            title.textContent = "Interview ended";

            const body = document.createElement("p");
            body.className = "idle-banner-message";
            body.textContent = message
                || "The maximum number of integrity warnings has been reached (3/3).";

            const explanation = document.createElement("p");
            explanation.className = "idle-banner-message";
            explanation.textContent =
                "This interview has been closed because the allowed integrity-warning limit was exceeded.";

            const exitButton = document.createElement("button");
            exitButton.type = "button";
            exitButton.className = "idle-banner-ok";
            exitButton.textContent = "Exit interview";
            exitButton.addEventListener("click", resolve, { once: true });

            cheatingBannerEl.appendChild(title);
            cheatingBannerEl.appendChild(body);
            cheatingBannerEl.appendChild(explanation);
            cheatingBannerEl.appendChild(exitButton);
            cheatingBannerBackdropEl?.classList.remove("d-none");
            exitButton.focus();
        });
    }

    async function captureAndVerifyInSession() {
        if (ended || paused || sessionTerminated || !verifyInSessionUrl) return;
        if (!verificationCheckId || !cameraStream || !cameraVideo || inSessionCheckInFlight) return;
        if (verificationCheckExpiresAt && Date.parse(verificationCheckExpiresAt) <= Date.now()) return;
        inSessionCheckInFlight = true;
        try {
            setCheatingWarning(
                "Checking the face currently visible in the camera.",
                "notice",
                0);

            inSessionCanvas.width = cameraVideo.videoWidth || 640;
            inSessionCanvas.height = cameraVideo.videoHeight || 480;
            const ctx = inSessionCanvas.getContext("2d");
            ctx.drawImage(cameraVideo, 0, 0, inSessionCanvas.width, inSessionCanvas.height);
            const frame = await new Promise(
                (resolve) => inSessionCanvas.toBlob(resolve, "image/jpeg", 0.9));
            if (!frame) return;

            const form = new FormData();
            form.append("frame", frame, "frame.jpg");
            form.append("checkId", verificationCheckId);
            verificationCheckId = null;
            verificationCheckExpiresAt = null;
            // Not postProxy: this is multipart (an image blob), not JSON — same reasoning as the
            // capture uploads in biometric-wizard.js/entry-verify.js. Never set Content-Type
            // manually for FormData; the browser adds the multipart boundary itself.
            const response = await fetch(verifyInSessionUrl, { method: "POST", body: form });
            const data = await response.json().catch(() => null);
            // A strike from a different source could have ended the session while this request
            // was in flight — this response is stale either way, drop it silently.
            if (sessionTerminated) return;
            if (!response.ok || !data) {
                const errorMessage = data?.error || data?.message
                    || `Verification request failed (${response.status}).`;
                if (isVerificationTokenError(errorMessage)) {
                    setCheatingWarning(null);
                    return;
                }
                setCheatingWarning(errorMessage, "warning");
                return;
            }

            if (data.passed) {
                setCheatingWarning(null);
                return;
            }

            if (data.warningType === "NoFace" && !data.noFaceJustBecameStrike) {
                // Gentle heads-up only — not a strike yet, so no strike count attached.
                setCheatingWarning(data.message, "notice");
                return;
            }

            if (qualityWarningTypes.has(data.warningType)) {
                // A technically unusable frame is guidance, not evidence of cheating.
                setCheatingWarning(data.message, "notice");
                return;
            }

            // A real strike happened this check: IdentityMismatch, MultiFace, or NoFace just
            // converted (data.noFaceJustBecameStrike). Attach the strike count exactly once here
            // — the server's Message for these three cases is deliberately count-free.
            const strikeText = `${data.message} (${data.strikeCount}/${data.maxStrikes} warnings)`;
            setCheatingWarning(strikeText, toneForWarning(data));

            // The popup is for a real strike only, and is skipped when the session is about to
            // end this same check — appendBubble + the redirect below already make that outcome
            // impossible to miss, so a dismiss-by-OK popup racing a 3s auto-redirect would just
            // be confusing on top of it.
            if (!data.shouldEndSession) {
                showCheatingBanner(strikeText, data.strikeCount === data.maxStrikes - 1);
            }

            if (data.shouldEndSession) {
                await handleForcedCheatingEnd(data.message);
            }
        } catch (error) {
            if (sessionTerminated) return;
            if (isVerificationTokenError(error?.message)) {
                setCheatingWarning(null);
                return;
            }
            setCheatingWarning(
                error?.message || "Verification is temporarily unavailable.",
                "warning");
        } finally {
            inSessionCheckInFlight = false;
        }
    }


    // AI Voice (TTS) — speaks AI questions when the toggle is on. Text/message flow is unchanged;
    // this only adds audio playback of the AI bubbles.
    let verificationSchedulePollInFlight = false;

    async function pollVerificationSchedule() {
        if (ended || paused || sessionTerminated || !inSessionCheckUrl || !currentQuestionId
            || verificationSchedulePollInFlight || inSessionCheckInFlight) return;

        if (verificationCheckId
            && (!verificationCheckExpiresAt
                || Date.parse(verificationCheckExpiresAt) > Date.now())) {
            captureAndVerifyInSession();
            return;
        }

        verificationCheckId = null;
        verificationCheckExpiresAt = null;
        verificationSchedulePollInFlight = true;
        try {
            const separator = inSessionCheckUrl.includes("?") ? "&" : "?";
            const response = await fetch(
                `${inSessionCheckUrl}${separator}sessionQuestionId=${encodeURIComponent(currentQuestionId)}`);
            const data = await response.json().catch(() => null);
            // A strike from a different source could have ended the session while this request
            // was in flight — this response is stale either way, drop it silently.
            if (sessionTerminated) return;
            if (!response.ok || !data) {
                const errorMessage = data?.error || data?.message
                    || `Monitoring request failed (${response.status}).`;
                setCheatingWarning(errorMessage, "warning");
                return;
            }
            if (data.shouldEndSession) {
                await handleForcedCheatingEnd(
                    data.message || "This interview has been ended.");
                return;
            }
            if (!data.due || !data.checkId) return;

            verificationCheckId = data.checkId;
            verificationCheckExpiresAt = data.expiresAt || null;
            captureAndVerifyInSession();
        } catch (error) {
            if (sessionTerminated) return;
            setCheatingWarning(
                error?.message || "Integrity monitoring is temporarily unavailable.",
                "warning");
        } finally {
            verificationSchedulePollInFlight = false;
        }
    }

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
            if (!blob || blob.size === 0) {
                console.warn("[TTS] empty audio response");
                return;
            }
            currentAudio?.pause();
            const audio = new Audio(URL.createObjectURL(blob));
            currentAudio = audio;
            // Waveform follows the real <audio> element instead of just the AI-Voice toggle.
            // Additive listeners only — onended/onerror/onpause below still own resolve().
            const visualizer = document.querySelector(".visualizer");
            const pauseVisualizer = () => visualizer?.classList.add("paused");
            audio.addEventListener("play", () => visualizer?.classList.remove("paused"));
            audio.addEventListener("ended", pauseVisualizer);
            audio.addEventListener("error", pauseVisualizer);
            audio.addEventListener("pause", pauseVisualizer);
            await new Promise((resolve) => {
                audio.onended = resolve;
                audio.onerror = resolve;
                audio.onpause = resolve; // paused by toggle-off or a newer clip — stop waiting
                audio.play().catch((err) => {
                    console.warn("[TTS] play blocked:", err?.name || err);
                    resolve();
                });
            });
        } catch {
            // Network/TTS error — ignore, the interview continues silently.
        }
    }

    voiceToggleBtn?.addEventListener("click", () => {
        aiVoiceOn = !aiVoiceOn;
        voiceToggleBtn.classList.toggle("is-on", aiVoiceOn);
        voiceToggleBtn.setAttribute("aria-pressed", aiVoiceOn ? "true" : "false");
        const visualizer = document.querySelector(".visualizer");
        if (visualizer) visualizer.classList.toggle("paused", !aiVoiceOn);
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
    restoreDraft();

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
