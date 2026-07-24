(function () {
    "use strict";

    if (window.DevPrepFeedbackReport) {
        return;
    }

    const modal = document.querySelector("[data-report-modal]");
    const form = modal?.querySelector("[data-report-form]");
    if (!modal || !form) {
        window.DevPrepFeedbackReport = { initialized: false };
        return;
    }

    const details = form.querySelector("[data-report-details]");
    const counter = form.querySelector("[data-report-counter]");
    const errorBox = form.querySelector("[data-report-error]");
    const reasonError = form.querySelector("[data-report-reason-error]");
    const detailsError = form.querySelector("[data-report-details-error]");
    const submitButton = form.querySelector("[data-report-submit]");
    const submitLabel = form.querySelector("[data-report-submit-label]");
    const preview = modal.querySelector("[data-report-preview]");
    const author = modal.querySelector("[data-report-author]");
    const typeLabel = modal.querySelector("[data-report-type]");
    const toast = document.querySelector("[data-report-toast]");
    let activeTrigger = null;
    let currentFeedbackKey = null;
    let submitting = false;
    let toastTimer;

    function selectedReason() {
        return form.querySelector("[data-report-reason]:checked")?.value || "";
    }

    function updateDetailsState() {
        const otherSelected = selectedReason() === "Other";
        details.required = otherSelected;
        const mode = form.querySelector("[data-report-details-mode]");
        if (mode) {
            mode.textContent = otherSelected ? "(required)" : "(optional)";
        }

        updateCounter();
    }

    function updateCounter() {
        const maximum = Number(details.maxLength) || 1000;
        const invalid = details.value.length > maximum
            || (details.required && details.value.trim().length === 0);
        counter.textContent = `${details.value.length} / ${maximum}`;
        counter.classList.toggle("is-invalid", invalid);
        details.setAttribute("aria-invalid", invalid ? "true" : "false");
    }

    function setError(message) {
        errorBox.textContent = message || "";
        errorBox.hidden = !message;
    }

    function setFieldError(element, message) {
        if (!element) {
            return;
        }
        element.textContent = message || "";
        element.hidden = !message;
    }

    function clearErrors() {
        setError("");
        setFieldError(reasonError, "");
        setFieldError(detailsError, "");
    }

    function applyApiErrors(payload) {
        const entries = Object.entries(payload?.errors || {});
        entries.forEach(function (entry) {
            const key = entry[0].toLowerCase();
            const message = Array.isArray(entry[1]) ? entry[1][0] : entry[1];
            if (key.endsWith("reasoncode")) {
                setFieldError(reasonError, message);
            } else if (key.endsWith("details")) {
                setFieldError(detailsError, message);
            }
        });
        setError(payload?.message || "");
    }

    function setPending(pending) {
        submitting = pending;
        submitButton.disabled = pending;
        form.querySelectorAll("button, input, textarea").forEach(function (control) {
            if (!control.matches("[data-report-close]")) {
                control.disabled = pending;
            }
        });
        submitLabel.textContent = pending ? "Submitting..." : "Submit Report";
        form.setAttribute("aria-busy", pending ? "true" : "false");
    }

    function focusableElements() {
        return Array.from(modal.querySelectorAll(
            "button:not([disabled]), input:not([disabled]), textarea:not([disabled]), [href], [tabindex]:not([tabindex='-1'])"))
            .filter(function (element) { return !element.hidden && element.offsetParent !== null; });
    }

    function openModal(trigger) {
        activeTrigger = trigger;
        const feedbackId = trigger.dataset.feedbackId;
        const feedbackType = trigger.dataset.feedbackType;
        const feedbackKey = `${feedbackType}:${feedbackId}`;
        if (currentFeedbackKey !== feedbackKey) {
            form.reset();
            details.value = "";
            currentFeedbackKey = feedbackKey;
        }

        form.querySelector("[data-report-feedback-id]").value = feedbackId;
        form.querySelector("[data-report-feedback-type]").value = trigger.dataset.feedbackType;
        const previewSource = document.getElementById(trigger.dataset.previewId || "");
        preview.textContent = previewSource?.textContent?.trim() || "No written comment.";
        author.textContent = trigger.dataset.feedbackAuthor || "DevPrep User";
        typeLabel.textContent = trigger.dataset.feedbackTypeLabel || "Feedback preview";
        clearErrors();
        updateDetailsState();
        modal.hidden = false;
        document.body.style.overflow = "hidden";
        window.requestAnimationFrame(function () {
            form.querySelector("[data-report-reason]")?.focus();
        });
    }

    function closeModal() {
        if (submitting) {
            return;
        }

        modal.hidden = true;
        document.body.style.overflow = "";
        activeTrigger?.focus();
    }

    function markReported(trigger) {
        if (!trigger) {
            return;
        }

        trigger.disabled = true;
        trigger.classList.add("is-reported");
        trigger.setAttribute("aria-label", "Feedback already reported");
        const label = trigger.querySelector("span:last-child");
        if (label) {
            label.textContent = "Reported";
        }
    }

    function showToast(message, isError) {
        if (!toast) {
            return;
        }

        window.clearTimeout(toastTimer);
        toast.textContent = message;
        toast.classList.toggle("is-error", Boolean(isError));
        toast.hidden = false;
        toastTimer = window.setTimeout(function () {
            toast.hidden = true;
        }, 6000);
    }

    function failureMessage(status) {
        if (status === 403) {
            return "You do not have permission to report this feedback.";
        }
        if (status === 429) {
            return "Too many requests. Please wait and try again.";
        }
        if (status >= 500) {
            return "Reporting is temporarily unavailable. Your selections have been kept; please try again.";
        }
        return "Please correct the report fields and try again.";
    }

    async function submitReport(event) {
        event.preventDefault();
        if (submitting) {
            return;
        }

        updateDetailsState();
        clearErrors();
        const reasonCode = selectedReason();
        if (!reasonCode) {
            setFieldError(reasonError, "Choose a reason before submitting your report.");
            form.reportValidity();
            return;
        }
        if (details.required && details.value.trim().length === 0) {
            setFieldError(detailsError, "Details are required when the reason is Other.");
            details.focus();
            return;
        }

        const feedbackId = form.querySelector("[data-report-feedback-id]").value;
        const feedbackType = form.querySelector("[data-report-feedback-type]").value;
        const returnUrl = form.querySelector("[data-report-return-url]").value;
        const antiForgeryToken = form.querySelector("input[name='__RequestVerificationToken']")?.value;
        const requestUrl = new URL(form.action, window.location.href);
        requestUrl.searchParams.set("feedbackId", feedbackId);
        requestUrl.searchParams.set("feedbackType", feedbackType);
        requestUrl.searchParams.set("returnUrl", returnUrl);
        const requestBody = {
            reasonCode: reasonCode,
            details: details.value.trim().length === 0 ? null : details.value
        };

        setPending(true);
        try {
            const response = await fetch(requestUrl, {
                method: "POST",
                body: JSON.stringify(requestBody),
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": antiForgeryToken || "",
                    "X-Requested-With": "XMLHttpRequest"
                }
            });
            let payload = null;
            try {
                payload = await response.json();
            } catch {
                payload = null;
            }

            if (response.status === 401 && payload?.loginUrl) {
                window.location.assign(payload.loginUrl);
                return;
            }

            if (response.ok || response.status === 409) {
                markReported(activeTrigger);
                setPending(false);
                closeModal();
                showToast(response.status === 409
                    ? (payload?.message || "You have already reported this feedback.")
                    : (payload?.message || "Thank you. Your report has been submitted for review."), false);
                return;
            }

            if (response.status === 404) {
                const card = activeTrigger?.closest(".review-card");
                setPending(false);
                closeModal();
                if (card) {
                    card.remove();
                }
                showToast("This feedback is no longer publicly available.", true);
                return;
            }

            if (payload?.message || payload?.errors) {
                applyApiErrors(payload);
            } else {
                setError(failureMessage(response.status));
            }
        } catch {
            setError("Reporting is temporarily unavailable. Your selections have been kept; please try again.");
        } finally {
            if (!modal.hidden) {
                setPending(false);
            }
        }
    }

    document.addEventListener("click", function (event) {
        const trigger = event.target.closest("[data-report-trigger]");
        if (trigger) {
            openModal(trigger);
            return;
        }

        if (!modal.hidden && event.target.closest("[data-report-close]")) {
            closeModal();
        }
    });

    document.addEventListener("change", function (event) {
        if (event.target.matches("[data-report-reason]")) {
            clearErrors();
            updateDetailsState();
        }
    });

    details.addEventListener("input", function () {
        setError("");
        setFieldError(detailsError, "");
        updateCounter();
    });

    document.addEventListener("keydown", function (event) {
        if (modal.hidden) {
            return;
        }

        if (event.key === "Escape") {
            event.preventDefault();
            closeModal();
            return;
        }

        if (event.key !== "Tab") {
            return;
        }

        const focusable = focusableElements();
        if (focusable.length === 0) {
            return;
        }

        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    });

    form.addEventListener("submit", submitReport);
    updateDetailsState();
    window.DevPrepFeedbackReport = { initialized: true, open: openModal };
}());
