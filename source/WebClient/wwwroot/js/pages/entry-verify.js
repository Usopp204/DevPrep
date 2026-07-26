// entry-verify.js — biometric ENTRY GATE for the Interview Room (face-only).
// One face-match check for the session; pass → Room, out of attempts → back to the
// dashboard via the VerifyFailed action (sets TempData). No head-turn/liveness requirement —
// see ai-service/biometrics/router.py for why (yaw measurement unreliable on real hardware).
// The capture flow mirrors biometric-wizard.js verify mode (same proxies, same
// 8-frame burst) but lives in its own small file so the enroll wizard stays untouched.
(function () {
    "use strict";

    const root = document.getElementById("entry-verify");
    if (!root) return;

    const urls = {
        challenge: root.dataset.challengeUrl,
        verifyFace: root.dataset.verifyFaceUrl,
        entryGate: root.dataset.entryGateUrl,
        room: root.dataset.roomUrl,
        failed: root.dataset.failedUrl,
    };
    const sessionId = root.dataset.sessionId;
    let attemptsRemaining = parseInt(root.dataset.attemptsRemaining, 10) || 0;

    // Same burst parameters as the wizard: multiple frames still improve match robustness
    // even though no head motion is required anymore.
    const FRAME_COUNT = 8;
    const FRAME_INTERVAL_MS = 300;

    const video = document.getElementById("bw-video");
    const oval = document.getElementById("face-oval");
    const scanline = document.getElementById("face-scanline");
    const countdown = document.getElementById("face-countdown");
    const processing = document.getElementById("face-processing");
    const guidance = document.getElementById("face-guidance");
    const errorEl = document.getElementById("face-error");
    const actionBtn = document.getElementById("face-action-btn");
    const attemptsPill = document.getElementById("attempts-pill");

    const canvas = document.createElement("canvas");
    let cameraStream = null;

    function show(el) { if (el) el.hidden = false; }
    function hide(el) { if (el) el.hidden = true; }
    function setGuidance(text) { guidance.textContent = text || ""; }

    function setError(text) {
        errorEl.textContent = text || "";
        errorEl.hidden = !text;
    }

    function setOvalState(state) {
        oval.classList.remove("success", "error", "scanning");
        if (state) oval.classList.add(state);
    }

    function setAttempts(n) {
        attemptsRemaining = n;
        if (attemptsPill) attemptsPill.textContent = "Attempts left: " + n;
    }

    function stopCamera() {
        if (cameraStream) {
            cameraStream.getTracks().forEach(function (t) { t.stop(); });
            cameraStream = null;
        }
    }

    async function fetchJson(url, options) {
        const response = await fetch(url, options);
        const data = await response.json().catch(function () { return null; });
        if (!response.ok) {
            throw new Error((data && data.error) || "Request failed (" + response.status + ").");
        }
        return data;
    }

    function delay(ms) {
        return new Promise(function (resolve) { setTimeout(resolve, ms); });
    }

    async function prepare() {
        setOvalState("");
        setError("");
        hide(processing);
        hide(scanline);
        hide(countdown);
        actionBtn.disabled = true;
        actionBtn.textContent = "I'm ready";
        setGuidance("Preparing camera…");

        try {
            // Still issues/consumes a server-side challenge (required before verify-face
            // will accept a submission) — the requested direction it returns is no longer
            // shown or scored (see the guidance/scoring notes below).
            await fetchJson(urls.challenge +
                "?sessionId=" + encodeURIComponent(sessionId) + "&modality=face");
        } catch (err) {
            setGuidance("Challenge unavailable");
            setError(err.message);
            actionBtn.textContent = "Try again";
            actionBtn.disabled = false;
            actionBtn.onclick = prepare;
            return;
        }

        try {
            cameraStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
            video.srcObject = cameraStream;
        } catch (err) {
            setGuidance("Camera unavailable");
            setError("Could not access the camera. Allow camera permission for this site, then try again.");
            actionBtn.textContent = "Try again";
            actionBtn.disabled = false;
            actionBtn.onclick = prepare;
            return;
        }

        setGuidance("Center your face and look straight at the camera");
        actionBtn.disabled = false;
        actionBtn.onclick = capture;
    }

    async function capture() {
        actionBtn.disabled = true;
        setError("");

        show(countdown);
        for (let n = 3; n >= 1; n--) {
            countdown.textContent = String(n);
            await delay(800);
        }
        hide(countdown);

        canvas.width = video.videoWidth || 640;
        canvas.height = video.videoHeight || 480;
        const ctx = canvas.getContext("2d");

        setOvalState("scanning");
        show(scanline);
        // No motion required — the server only checks face match now (see router.py).
        setGuidance("Hold still and look at the camera");

        const blobs = [];
        for (let i = 0; i < FRAME_COUNT; i++) {
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            const blob = await new Promise(function (resolve) {
                canvas.toBlob(resolve, "image/jpeg", 0.9);
            });
            if (blob) blobs.push(blob);
            await delay(FRAME_INTERVAL_MS);
        }

        hide(scanline);
        stopCamera();
        show(processing);

        try {
            const form = new FormData();
            form.append("sessionId", sessionId);
            blobs.forEach(function (blob, i) {
                form.append("frames", blob, "frame" + i + ".jpg");
            });
            const result = await fetchJson(urls.verifyFace, { method: "POST", body: form });
            hide(processing);

            if (result.passed) {
                setOvalState("success");
                setGuidance("Verified ✓ — entering the room…");
                await delay(900);
                window.location.href = urls.room;
                return;
            }

            await handleFail(result.reason || "Face verification failed.");
        } catch (err) {
            hide(processing);
            // A quality rejection (e.g. no usable frames) does not consume an attempt
            // server-side, but a hard failure message may also mean the retry cap hit.
            await handleFail(err.message);
        }
    }

    async function handleFail(reason) {
        // Ask the server how many attempts are actually left (source of truth).
        try {
            const gate = await fetchJson(urls.entryGate + "?sessionId=" + encodeURIComponent(sessionId));
            setAttempts(gate.attemptsRemaining);
            if (gate.allowed) { window.location.href = urls.room; return; }
            if (gate.attemptsRemaining <= 0) {
                setOvalState("error");
                setGuidance("Verification failed");
                setError("Maximum attempts reached — returning to the dashboard…");
                await delay(1600);
                window.location.href = urls.failed;
                return;
            }
        } catch (_) { /* keep the local counter if the status call fails */ }

        setOvalState("error");
        setGuidance("Check failed");
        setError(reason + " — please try again.");
        actionBtn.textContent = "Try again";
        actionBtn.disabled = false;
        actionBtn.onclick = prepare;
    }

    prepare();
})();
