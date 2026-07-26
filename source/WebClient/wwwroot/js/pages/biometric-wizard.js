// Biometric verification wizard — 4 distinct full screens driven by a client-side state
// machine (guide → face → voice → done); only one screen is rendered at a time.
// Visuals follow .claude/biometric-wizard-mockup.html. Two modes share the shell:
//   enroll : Profile enrollment proxies (EnrollFace / EnrollVoice)
//   verify : session challenge proxies (Challenge / VerifyFace / VerifyVoice)
// Camera/mic tracks are stopped on every screen transition so hardware is never left on.
(function () {
    const root = document.getElementById("wizard-app");
    if (!root) return;

    const urls = {
        enrollFace: root.dataset.enrollFaceUrl,
        enrollVoice: root.dataset.enrollVoiceUrl,
        challenge: root.dataset.challengeUrl,
        verifyFace: root.dataset.verifyFaceUrl,
        verifyVoice: root.dataset.verifyVoiceUrl,
        profile: root.dataset.profileUrl
    };

    const screens = {
        guide: document.getElementById("screen-guide"),
        face: document.getElementById("screen-face"),
        voice: document.getElementById("screen-voice"),
        done: document.getElementById("screen-done")
    };
    const focusTargets = {
        guide: document.getElementById("guide-title"),
        face: document.getElementById("face-title"),
        voice: document.getElementById("voice-title"),
        done: document.getElementById("done-title")
    };

    // Guide elements
    const permCamera = document.getElementById("perm-camera");
    const permCameraLabel = document.getElementById("perm-camera-label");
    const permMic = document.getElementById("perm-mic");
    const permMicLabel = document.getElementById("perm-mic-label");
    const permHelp = document.getElementById("perm-help");
    const grantBtn = document.getElementById("grant-btn");
    const consentCheckbox = document.getElementById("consent-checkbox");
    const startBtn = document.getElementById("start-btn");
    const verifyStartBtn = document.getElementById("verify-start-btn");
    const verifySessionSelect = document.getElementById("verify-session-select");

    // Face elements
    const video = document.getElementById("bw-video");
    const faceOval = document.getElementById("face-oval");
    const faceScanline = document.getElementById("face-scanline");
    const faceCountdown = document.getElementById("face-countdown");
    const faceProcessing = document.getElementById("face-processing");
    const faceGuidance = document.getElementById("face-guidance");
    const faceError = document.getElementById("face-error");
    const faceActionBtn = document.getElementById("face-action-btn");
    const faceExitBtn = document.getElementById("face-exit-btn");

    // Voice elements
    const voiceCard = document.getElementById("voice-card");
    const voicePhraseLabel = document.getElementById("voice-phrase-label");
    const voicePhrase = document.getElementById("voice-phrase");
    const voiceBars = Array.prototype.slice.call(
        document.getElementById("voice-bars").querySelectorAll("i"));
    const voiceTimerValue = document.getElementById("voice-timer-value");
    const voiceTimerNote = document.getElementById("voice-timer-note");
    const voiceProcessing = document.getElementById("voice-processing");
    const voiceGuidance = document.getElementById("voice-guidance");
    const voiceError = document.getElementById("voice-error");
    const voiceRecordBtn = document.getElementById("voice-record-btn");
    const voiceStopBtn = document.getElementById("voice-stop-btn");
    const voiceExitBtn = document.getElementById("voice-exit-btn");

    // Done elements
    const doneTitle = document.getElementById("done-title");
    const doneMessage = document.getElementById("done-message");
    const doneTranscript = document.getElementById("done-transcript");
    const doneCta = document.getElementById("done-cta");

    const FRAME_COUNT = 8;
    const FRAME_INTERVAL_MS = 300;
    const MIN_RECORD_SECONDS = 4;
    const MAX_RECORD_SECONDS = 20;

    const ENROLL_VOICE_PROMPT =
        "Introduce yourself naturally for a few seconds — your name and your role.";

    // ---- Wizard state --------------------------------------------------------
    let mode = "enroll";          // "enroll" | "verify"
    let sessionId = null;         // verify mode only
    let facePassed = false;
    let voiceResult = null;
    let bonusAwarded = false; // enroll mode only — set true if either enroll call reports it
    let cameraStream = null;
    let micStream = null;
    let mediaRecorder = null;
    let recordedChunks = [];
    let recordSeconds = 0;
    let recordTimerHandle = null;
    let audioContext = null;
    let vizFrameHandle = null;
    let camGranted = false;
    let micGranted = false;

    const canvas = document.createElement("canvas");

    // ---- Shared helpers ------------------------------------------------------
    function stopCamera() {
        if (cameraStream) {
            cameraStream.getTracks().forEach(function (t) { t.stop(); });
            cameraStream = null;
        }
        video.srcObject = null;
    }

    function stopMic() {
        if (mediaRecorder && mediaRecorder.state === "recording") {
            try { mediaRecorder.stop(); } catch (e) { /* already stopping */ }
        }
        mediaRecorder = null;
        if (micStream) {
            micStream.getTracks().forEach(function (t) { t.stop(); });
            micStream = null;
        }
        if (audioContext) {
            audioContext.close().catch(function () {});
            audioContext = null;
        }
        cancelAnimationFrame(vizFrameHandle);
        clearInterval(recordTimerHandle);
        voiceCard.classList.remove("recording");
    }

    function stopAllMedia() {
        stopCamera();
        stopMic();
    }

    function show(el) { if (el) el.hidden = false; }
    function hide(el) { if (el) el.hidden = true; }

    function showScreen(name) {
        stopAllMedia();
        Object.keys(screens).forEach(function (key) {
            screens[key].hidden = key !== name;
        });
        const target = focusTargets[name];
        if (target) target.focus({ preventScroll: false });
        window.scrollTo(0, 0);
    }

    async function fetchJson(url, options) {
        const response = await fetch(url, options);
        const data = await response.json().catch(function () { return null; });
        if (!response.ok) {
            throw new Error((data && data.error) || "Request failed (" + response.status + ").");
        }
        return data;
    }

    function getChallenge(modality) {
        return fetchJson(urls.challenge +
            "?sessionId=" + encodeURIComponent(sessionId) +
            "&modality=" + encodeURIComponent(modality));
    }

    function delay(ms) {
        return new Promise(function (resolve) { setTimeout(resolve, ms); });
    }

    // ---- Guide: permissions + consent gating ---------------------------------
    function applyPermState(kind, state) {
        const ready = state === "granted";
        const pill = kind === "camera" ? permCamera : permMic;
        const label = kind === "camera" ? permCameraLabel : permMicLabel;
        const name = kind === "camera" ? "Camera" : "Microphone";
        pill.dataset.state = ready ? "ready" : (state === "denied" ? "blocked" : "pending");
        label.textContent = ready ? name + " ready" : (state === "denied" ? name + " blocked" : name + " — allow");
        if (kind === "camera") camGranted = ready; else micGranted = ready;

        grantBtn.hidden = camGranted && micGranted;
        if (state === "denied") {
            permHelp.textContent =
                "Access is blocked. Allow camera/microphone for this site in your browser settings, then reload.";
            show(permHelp);
        }
        updateStartButton();
    }

    function updateStartButton() {
        startBtn.disabled = !(consentCheckbox.checked && camGranted && micGranted);
    }

    async function refreshPermissions() {
        if (!navigator.permissions || !navigator.permissions.query) {
            // No Permissions API (e.g. older Safari): user must use the grant button probe.
            applyPermState("camera", "prompt");
            applyPermState("mic", "prompt");
            grantBtn.hidden = false;
            return;
        }
        try {
            const cam = await navigator.permissions.query({ name: "camera" });
            applyPermState("camera", cam.state);
            cam.onchange = function () { applyPermState("camera", cam.state); };
        } catch (e) {
            grantBtn.hidden = false;
        }
        try {
            const mic = await navigator.permissions.query({ name: "microphone" });
            applyPermState("mic", mic.state);
            mic.onchange = function () { applyPermState("mic", mic.state); };
        } catch (e) {
            grantBtn.hidden = false;
        }
    }

    grantBtn.addEventListener("click", async function () {
        hide(permHelp);
        try {
            // One combined probe; tracks stop immediately — this only surfaces the prompt.
            const probe = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
            probe.getTracks().forEach(function (t) { t.stop(); });
            applyPermState("camera", "granted");
            applyPermState("mic", "granted");
        } catch (err) {
            applyPermState("camera", "denied");
            applyPermState("mic", "denied");
        }
    });

    consentCheckbox.addEventListener("change", updateStartButton);

    startBtn.addEventListener("click", function () {
        mode = "enroll";
        sessionId = null;
        facePassed = false;
        voiceResult = null;
        showScreen("face");
        enterFaceStep();
    });

    if (verifyStartBtn && verifySessionSelect) {
        verifyStartBtn.addEventListener("click", function () {
            mode = "verify";
            sessionId = verifySessionSelect.value;
            facePassed = false;
            voiceResult = null;
            showScreen("face");
            enterFaceStep();
        });
    }

    // ---- Face step -----------------------------------------------------------
    function setOvalState(state) {
        faceOval.classList.remove("scanning", "success", "error");
        if (state) faceOval.classList.add(state);
    }

    function setFaceGuidance(text) { faceGuidance.textContent = text || ""; }

    function setFaceError(text) {
        faceError.textContent = text || "";
        faceError.hidden = !text;
    }

    async function enterFaceStep() {
        setOvalState("");
        setFaceError("");
        hide(faceProcessing);
        hide(faceScanline);
        hide(faceCountdown);
        faceActionBtn.disabled = true;
        faceActionBtn.textContent = "I'm ready";
        setFaceGuidance("Preparing camera…");

        let challengeDirection = null;
        if (mode === "verify") {
            try {
                const challenge = await getChallenge("face");
                challengeDirection = (challenge.direction || "").toUpperCase();
            } catch (err) {
                setFaceGuidance("Challenge unavailable");
                setFaceError(err.message);
                faceActionBtn.textContent = "Try again";
                faceActionBtn.disabled = false;
                faceActionBtn.onclick = enterFaceStep;
                return;
            }
        }

        try {
            cameraStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
            video.srcObject = cameraStream;
        } catch (err) {
            setFaceGuidance("Camera unavailable");
            setFaceError("Could not access the camera. Allow camera permission for this site, then try again.");
            faceActionBtn.textContent = "Try again";
            faceActionBtn.disabled = false;
            faceActionBtn.onclick = enterFaceStep;
            return;
        }

        setFaceGuidance(mode === "verify"
            ? "Center your face — you'll turn your head " + challengeDirection
            : "Center your face in the oval");
        faceActionBtn.disabled = false;
        faceActionBtn.onclick = function () { runFaceCapture(challengeDirection); };
    }

    async function runFaceCapture(direction) {
        faceActionBtn.disabled = true;
        setFaceError("");

        // Brief countdown so the user can settle.
        show(faceCountdown);
        for (let n = 3; n >= 1; n--) {
            faceCountdown.textContent = String(n);
            await delay(800);
        }
        hide(faceCountdown);

        canvas.width = video.videoWidth || 640;
        canvas.height = video.videoHeight || 480;
        const ctx = canvas.getContext("2d");

        setOvalState("scanning");
        show(faceScanline);

        const blobs = [];
        if (mode === "verify") {
            setFaceGuidance(direction === "LEFT"
                ? "← Turn your head slowly to the LEFT and back"
                : "Turn your head slowly to the RIGHT and back →");
            for (let i = 0; i < FRAME_COUNT; i++) {
                ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
                const blob = await new Promise(function (resolve) {
                    canvas.toBlob(resolve, "image/jpeg", 0.9);
                });
                if (blob) blobs.push(blob);
                await delay(FRAME_INTERVAL_MS);
            }
        } else {
            setFaceGuidance("Hold steady…");
            await delay(350);
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            const blob = await new Promise(function (resolve) {
                canvas.toBlob(resolve, "image/jpeg", 0.92);
            });
            if (blob) blobs.push(blob);
        }

        hide(faceScanline);
        stopCamera();
        show(faceProcessing);

        try {
            if (mode === "verify") {
                const form = new FormData();
                form.append("sessionId", sessionId);
                blobs.forEach(function (blob, i) {
                    form.append("frames", blob, "frame" + i + ".jpg");
                });
                const result = await fetchJson(urls.verifyFace, { method: "POST", body: form });
                hide(faceProcessing);
                if (!result.passed) {
                    failFaceStep(result.reason || "Face verification failed.");
                    return;
                }
            } else {
                const form = new FormData();
                form.append("face", blobs[0], "face.jpg");
                const enrollResult = await fetchJson(urls.enrollFace, { method: "POST", body: form });
                if (enrollResult && enrollResult.bonusAwarded) bonusAwarded = true;
                hide(faceProcessing);
            }
        } catch (err) {
            hide(faceProcessing);
            failFaceStep(err.message);
            return;
        }

        facePassed = true;
        setOvalState("success");
        setFaceGuidance(mode === "verify" ? "Face verified ✓" : "Face captured ✓");
        await delay(1100);
        showScreen("voice");
        enterVoiceStep();
    }

    function failFaceStep(reason) {
        setOvalState("error");
        setFaceGuidance("Check failed");
        setFaceError(reason + " — please try again.");
        faceActionBtn.textContent = "Try again";
        faceActionBtn.disabled = false;
        faceActionBtn.onclick = enterFaceStep;
    }

    faceExitBtn.addEventListener("click", function () { showScreen("guide"); });

    // ---- Voice step ----------------------------------------------------------
    function setVoiceGuidance(text) { voiceGuidance.textContent = text || ""; }

    function setVoiceError(text) {
        voiceError.textContent = text || "";
        voiceError.hidden = !text;
    }

    function resetBars() {
        voiceBars.forEach(function (bar) { bar.style.height = "10px"; });
    }

    function setTimer(seconds, note) {
        const m = Math.floor(seconds / 60);
        const s = (seconds % 60).toString().padStart(2, "0");
        voiceTimerValue.textContent = m + ":" + s;
        voiceTimerNote.textContent = "/ " + note;
    }

    async function enterVoiceStep() {
        if (!facePassed) {
            // Deep-link guard: voice is only reachable after the face step passes.
            showScreen("guide");
            return;
        }
        setVoiceError("");
        hide(voiceProcessing);
        resetBars();
        setTimer(0, "ready");
        voiceRecordBtn.hidden = false;
        voiceRecordBtn.disabled = true;
        voiceRecordBtn.textContent = "Start recording";
        hide(voiceStopBtn);

        if (mode === "verify") {
            voicePhraseLabel.textContent = "Read this aloud";
            voicePhrase.textContent = "…";
            setVoiceGuidance("Fetching your phrase…");
            try {
                const challenge = await getChallenge("voice");
                voicePhrase.textContent = "“" + (challenge.phrase || "") + "”";
            } catch (err) {
                setVoiceGuidance("");
                setVoiceError(err.message);
                voiceRecordBtn.disabled = false;
                voiceRecordBtn.textContent = "Try again";
                voiceRecordBtn.onclick = enterVoiceStep;
                return;
            }
        } else {
            voicePhraseLabel.textContent = "Your prompt";
            voicePhrase.textContent = ENROLL_VOICE_PROMPT;
        }

        setVoiceGuidance("Press record, then speak for at least " + MIN_RECORD_SECONDS + " seconds.");
        voiceRecordBtn.disabled = false;
        voiceRecordBtn.onclick = startRecording;
    }

    async function startRecording() {
        setVoiceError("");
        if (typeof MediaRecorder === "undefined" || !MediaRecorder.isTypeSupported("audio/webm")) {
            setVoiceError("Your browser does not support audio recording.");
            return;
        }
        try {
            micStream = await navigator.mediaDevices.getUserMedia({ audio: true });
        } catch (err) {
            setVoiceError("Could not access the microphone. Allow microphone permission for this site, then try again.");
            return;
        }

        // Live level bars driven by an analyser node.
        audioContext = new (window.AudioContext || window.webkitAudioContext)();
        const source = audioContext.createMediaStreamSource(micStream);
        const analyser = audioContext.createAnalyser();
        analyser.fftSize = 128;
        source.connect(analyser);
        const levels = new Uint8Array(analyser.frequencyBinCount);

        function renderBars() {
            analyser.getByteFrequencyData(levels);
            const step = Math.floor(levels.length / voiceBars.length) || 1;
            for (let i = 0; i < voiceBars.length; i++) {
                const value = levels[i * step] / 255;
                voiceBars[i].style.height = Math.max(10, Math.round(value * 44)) + "px";
            }
            vizFrameHandle = requestAnimationFrame(renderBars);
        }
        renderBars();

        recordedChunks = [];
        mediaRecorder = new MediaRecorder(micStream, { mimeType: "audio/webm" });
        mediaRecorder.addEventListener("dataavailable", function (e) {
            if (e.data && e.data.size > 0) recordedChunks.push(e.data);
        });
        mediaRecorder.addEventListener("stop", onRecordingStopped);
        mediaRecorder.start();

        voiceCard.classList.add("recording");
        recordSeconds = 0;
        setTimer(0, "recording…");
        voiceRecordBtn.hidden = true;
        show(voiceStopBtn);
        voiceStopBtn.disabled = true;
        setVoiceGuidance("Keep speaking… (" + MIN_RECORD_SECONDS + "s minimum)");

        recordTimerHandle = setInterval(function () {
            recordSeconds += 1;
            setTimer(recordSeconds, "recording…");
            if (recordSeconds >= MIN_RECORD_SECONDS) {
                voiceStopBtn.disabled = false;
                setVoiceGuidance("Press Stop & submit when you finish.");
            }
            if (recordSeconds >= MAX_RECORD_SECONDS && mediaRecorder && mediaRecorder.state === "recording") {
                mediaRecorder.stop();
            }
        }, 1000);
    }

    voiceStopBtn.addEventListener("click", function () {
        if (mediaRecorder && mediaRecorder.state === "recording") {
            mediaRecorder.stop();
        }
    });

    async function onRecordingStopped() {
        const chunks = recordedChunks;
        const seconds = recordSeconds;
        stopMic();
        resetBars();
        hide(voiceStopBtn);
        voiceRecordBtn.hidden = false;
        setTimer(seconds, "done");

        setVoiceGuidance("");
        show(voiceProcessing);

        try {
            const blob = new Blob(chunks, { type: "audio/webm" });
            let result = null;
            if (mode === "verify") {
                const form = new FormData();
                form.append("sessionId", sessionId);
                form.append("audio", blob, "voice.webm");
                result = await fetchJson(urls.verifyVoice, { method: "POST", body: form });
                hide(voiceProcessing);
                if (!result.passed) {
                    failVoiceStep(result.reason || "Voice verification failed.");
                    return;
                }
            } else {
                const form = new FormData();
                form.append("voice", blob, "voice.webm");
                const enrollResult = await fetchJson(urls.enrollVoice, { method: "POST", body: form });
                if (enrollResult && enrollResult.bonusAwarded) bonusAwarded = true;
                hide(voiceProcessing);
            }
            voiceResult = result;
        } catch (err) {
            hide(voiceProcessing);
            failVoiceStep(err.message + (seconds < MIN_RECORD_SECONDS
                ? " (recording was " + seconds + "s — aim for at least " + MIN_RECORD_SECONDS + "s)"
                : ""));
            return;
        }

        setVoiceGuidance(mode === "verify" ? "Voice verified ✓" : "Voice captured ✓");
        await delay(900);
        showScreen("done");
        enterDoneStep();
    }

    function failVoiceStep(reason) {
        setVoiceError(reason + " — please try again.");
        setVoiceGuidance("");
        voiceRecordBtn.textContent = "Record again";
        voiceRecordBtn.disabled = false;
        // In verify mode the challenge was consumed — fetch a fresh one before re-recording.
        voiceRecordBtn.onclick = mode === "verify" ? enterVoiceStep : startRecording;
    }

    voiceExitBtn.addEventListener("click", function () { showScreen("guide"); });

    // ---- Completion ----------------------------------------------------------
    function enterDoneStep() {
        if (mode === "verify") {
            doneTitle.textContent = "You're verified";
            doneMessage.textContent = "Your identity has been confirmed successfully.";
            doneCta.textContent = "Continue";
            doneCta.onclick = function () { window.location.reload(); };
            if (voiceResult && voiceResult.transcript) {
                doneTranscript.textContent = "We heard: “" + voiceResult.transcript + "”";
                show(doneTranscript);
            } else {
                hide(doneTranscript);
            }
        } else {
            doneTitle.textContent = "You're all set";
            doneMessage.textContent = "Identity verification is set up. You can re-enroll any time.";
            if (bonusAwarded) {
                doneMessage.textContent += " 🎉 You've been awarded 100 free credits for completing both Face and Voice enrollment!";
            }
            doneCta.textContent = "Done";
            doneCta.onclick = function () { window.location.href = urls.profile; };
            hide(doneTranscript);
        }
    }

    // ---- Global lifecycle ------------------------------------------------------
    document.addEventListener("visibilitychange", function () {
        // Pause looping animations while the tab is hidden.
        root.classList.toggle("bw-anim-paused", document.hidden);
    });

    window.addEventListener("beforeunload", stopAllMedia);

    refreshPermissions();
    updateStartButton();
})();
