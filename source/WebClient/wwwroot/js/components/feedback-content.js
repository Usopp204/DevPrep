(function () {
    "use strict";

    if (window.DevPrepFeedbackContent) {
        return;
    }

    const formSelector = "[data-feedback-form]";
    const expandableSelector = "[data-expandable-text]";
    let resizeTimer;
    let activeDialog = null;
    let activeDialogTrigger = null;
    let bodyOverflowBeforeDialog = "";

    const dialogFocusableSelector = [
        "a[href]",
        "button:not([disabled])",
        "input:not([disabled]):not([type='hidden'])",
        "select:not([disabled])",
        "textarea:not([disabled])",
        "[tabindex]:not([tabindex='-1'])"
    ].join(",");

    function openDialog(modal, trigger) {
        activeDialog = modal;
        activeDialogTrigger = trigger || document.activeElement;
        modal.hidden = false;
        bodyOverflowBeforeDialog = document.body.style.overflow;
        document.body.style.overflow = "hidden";
        setBackgroundInert(modal, true);
        const heading = modal.querySelector("[role='dialog'] h2");
        const firstControl = modal.querySelector(dialogFocusableSelector);
        if (heading) {
            if (!heading.hasAttribute("tabindex")) {
                heading.dataset.feedbackDialogFocusTarget = "true";
                heading.setAttribute("tabindex", "-1");
            }
            heading.focus();
        } else {
            firstControl?.focus();
        }
    }

    function setBackgroundInert(modal, inert) {
        Array.from(document.body.children).forEach(function (element) {
            if (element === modal || element.contains(modal)) return;
            if (inert && !element.inert) {
                element.dataset.feedbackDialogInert = "true";
                element.inert = true;
            } else if (!inert && element.dataset.feedbackDialogInert === "true") {
                delete element.dataset.feedbackDialogInert;
                element.inert = false;
            }
        });
    }

    function setDialogStarRating(modal, name, value) {
        if (!value) {
            return;
        }

        const radio = modal.querySelector(`input[name='${name}'][value='${value}']`);
        if (radio) {
            radio.checked = true;
            radio.dispatchEvent(new Event("change", { bubbles: true }));
        }
    }

    function closeDialog(modal) {
        modal.hidden = true;
        document.body.style.overflow = bodyOverflowBeforeDialog;
        bodyOverflowBeforeDialog = "";
        setBackgroundInert(modal, false);
        const heading = modal.querySelector("[data-feedback-dialog-focus-target='true']");
        if (heading) {
            delete heading.dataset.feedbackDialogFocusTarget;
            heading.removeAttribute("tabindex");
        }
        if (activeDialog === modal) {
            activeDialog = null;
            const trigger = activeDialogTrigger;
            activeDialogTrigger = null;
            trigger?.focus();
        }
    }

    function updateFeedbackForm(form) {
        const textarea = form.querySelector("[data-feedback-comment]");
        if (!textarea) {
            return true;
        }

        const maximum = Number(textarea.getAttribute("maxlength")) || 0;
        const length = textarea.value.length;
        const counter = form.querySelector("[data-feedback-counter]");
        const error = form.querySelector("[data-feedback-error]");
        const exceedsMaximum = maximum > 0 && length > maximum;
        const invalid = exceedsMaximum || textarea.dataset.serverInvalid === "true";

        if (counter) {
            counter.textContent = `${length} / ${maximum}`;
            counter.classList.toggle("is-invalid", invalid);
        }

        textarea.classList.toggle("is-invalid", invalid);
        textarea.setAttribute("aria-invalid", invalid ? "true" : "false");
        if (exceedsMaximum && error) {
            error.textContent = `Comment cannot exceed ${maximum} characters.`;
        }

        return !invalid;
    }

    function refreshExpandableText(element) {
        const controlId = element.id;
        if (!controlId) {
            return;
        }

        const button = document.querySelector(`[data-expand-toggle][aria-controls="${CSS.escape(controlId)}"]`);
        if (!button) {
            return;
        }

        const wasExpanded = element.classList.contains("is-expanded");
        element.classList.remove("is-collapsible", "is-expanded");
        const styles = window.getComputedStyle(element);
        const lineHeight = Number.parseFloat(styles.lineHeight) || (Number.parseFloat(styles.fontSize) * 1.5);
        const lines = Number(element.dataset.lines) || 6;
        const needsControl = element.scrollHeight > (lineHeight * lines) + 1;

        element.classList.toggle("is-collapsible", needsControl);
        element.classList.toggle("is-expanded", needsControl && wasExpanded);
        button.hidden = !needsControl;
        button.setAttribute("aria-expanded", needsControl && wasExpanded ? "true" : "false");
        button.textContent = needsControl && wasExpanded ? "Show less" : "Show more";
    }

    function refresh(root) {
        if (root.matches?.(formSelector)) {
            updateFeedbackForm(root);
        }
        if (root.matches?.(expandableSelector)) {
            refreshExpandableText(root);
        }
        root.querySelectorAll(formSelector).forEach(updateFeedbackForm);
        root.querySelectorAll(expandableSelector).forEach(refreshExpandableText);
    }

    document.addEventListener("input", function (event) {
        const form = event.target.closest(formSelector);
        if (form) {
            const textarea = event.target.closest("[data-feedback-comment]");
            if (textarea) {
                delete textarea.dataset.serverInvalid;
                const error = form.querySelector("[data-feedback-error]");
                if (error) {
                    error.textContent = "";
                }
            }
            updateFeedbackForm(form);
        }
    });

    document.addEventListener("submit", function (event) {
        const form = event.target.closest(formSelector);
        if (!form) {
            return;
        }

        if (!updateFeedbackForm(form) || !form.checkValidity()) {
            event.preventDefault();
            form.reportValidity();
            return;
        }

        if (form.dataset.submitting === "true") {
            event.preventDefault();
            return;
        }

        form.dataset.submitting = "true";
        form.querySelectorAll("[data-feedback-submit]").forEach(function (button) {
            button.disabled = true;
            button.textContent = button.dataset.pendingText || "Submitting...";
        });
    });

    document.addEventListener("click", function (event) {
        const button = event.target.closest("[data-expand-toggle]");
        if (!button) {
            return;
        }

        const content = document.getElementById(button.getAttribute("aria-controls"));
        if (!content) {
            return;
        }

        const expanded = !content.classList.contains("is-expanded");
        content.classList.toggle("is-expanded", expanded);
        button.setAttribute("aria-expanded", expanded ? "true" : "false");
        button.textContent = expanded ? "Show less" : "Show more";
    });

    document.addEventListener("click", function (event) {
        const editSystemTrigger = event.target.closest("[data-edit-system-trigger]");
        if (editSystemTrigger) {
            const modal = document.querySelector("[data-edit-system-modal]");
            if (modal) {
                const feedbackIdInput = modal.querySelector("input[name='feedbackId']");
                if (feedbackIdInput) feedbackIdInput.value = event.target.closest("[data-feedback-id]")?.dataset.feedbackId || "";
                const commentInput = modal.querySelector("textarea[name='Comment']");
                if (commentInput) commentInput.value = editSystemTrigger.dataset.comment || "";
                setDialogStarRating(modal, "OverallRating", editSystemTrigger.dataset.overall);
                setDialogStarRating(modal, "UiUxRating", editSystemTrigger.dataset.uiux);
                setDialogStarRating(modal, "UsabilityRating", editSystemTrigger.dataset.usability);
                setDialogStarRating(modal, "FeatureQualityRating", editSystemTrigger.dataset.feature);
                openDialog(modal, event.target.closest("[data-edit-system-trigger], [data-edit-interview-trigger]"));
            }
            return;
        }
        if (event.target.closest("[data-edit-system-close]")) {
            const modal = document.querySelector("[data-edit-system-modal]");
            if (modal) closeDialog(modal);
            return;
        }

        const deleteSystemTrigger = event.target.closest("[data-delete-system-trigger]");
        if (deleteSystemTrigger) {
            const modal = document.querySelector("[data-delete-system-modal]");
            if (modal) {
                const feedbackIdInput = modal.querySelector("input[name='feedbackId']");
                if (feedbackIdInput) feedbackIdInput.value = deleteSystemTrigger.dataset.feedbackId || "";
                openDialog(modal, deleteSystemTrigger);
            }
            return;
        }
        if (event.target.closest("[data-delete-system-close]")) {
            const modal = document.querySelector("[data-delete-system-modal]");
            if (modal) closeDialog(modal);
            return;
        }

        const editInterviewTrigger = event.target.closest("[data-edit-interview-trigger]");
        if (editInterviewTrigger) {
            const modal = document.querySelector("[data-edit-interview-modal]");
            if (modal) {
                const feedbackIdInput = modal.querySelector("input[name='feedbackId']");
                if (feedbackIdInput) feedbackIdInput.value = event.target.closest("[data-feedback-id]")?.dataset.feedbackId || "";
                const commentInput = modal.querySelector("textarea[name='Comment']");
                if (commentInput) commentInput.value = editInterviewTrigger.dataset.comment || "";
                setDialogStarRating(modal, "OverallRating", editInterviewTrigger.dataset.overall);
                setDialogStarRating(modal, "QuestionQualityRating", editInterviewTrigger.dataset.question);
                setDialogStarRating(modal, "AiAccuracyRating", editInterviewTrigger.dataset.ai);
                setDialogStarRating(modal, "ScoringFairnessRating", editInterviewTrigger.dataset.scoring);
                setDialogStarRating(modal, "FeedbackUsefulnessRating", editInterviewTrigger.dataset.usefulness);
                openDialog(modal, event.target.closest("[data-edit-system-trigger], [data-edit-interview-trigger]"));
            }
            return;
        }
        if (event.target.closest("[data-edit-interview-close]")) {
            const modal = document.querySelector("[data-edit-interview-modal]");
            if (modal) closeDialog(modal);
            return;
        }

        const deleteInterviewTrigger = event.target.closest("[data-delete-interview-trigger]");
        if (deleteInterviewTrigger) {
            const modal = document.querySelector("[data-delete-interview-modal]");
            if (modal) {
                const feedbackIdInput = modal.querySelector("input[name='feedbackId']");
                if (feedbackIdInput) feedbackIdInput.value = deleteInterviewTrigger.dataset.feedbackId || "";
                openDialog(modal, deleteInterviewTrigger);
            }
            return;
        }
        if (event.target.closest("[data-delete-interview-close]")) {
            const modal = document.querySelector("[data-delete-interview-modal]");
            if (modal) closeDialog(modal);
            return;
        }
    });

    document.addEventListener("keydown", function (event) {
        if (!activeDialog) {
            return;
        }
        if (event.key === "Escape") {
            event.preventDefault();
            closeDialog(activeDialog);
            return;
        }
        if (event.key !== "Tab") {
            return;
        }

        const focusable = Array.from(activeDialog.querySelectorAll(dialogFocusableSelector));
        if (focusable.length === 0) {
            event.preventDefault();
            return;
        }
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        const heading = activeDialog.querySelector("[data-feedback-dialog-focus-target='true']");
        if (!activeDialog.contains(document.activeElement)) {
            event.preventDefault();
            (event.shiftKey ? last : first).focus();
        } else if (event.shiftKey && (document.activeElement === first || document.activeElement === heading)) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    });

    window.addEventListener("resize", function () {
        window.clearTimeout(resizeTimer);
        resizeTimer = window.setTimeout(function () {
            document.querySelectorAll(expandableSelector).forEach(refreshExpandableText);
        }, 120);
    });

    const observer = new MutationObserver(function (mutations) {
        mutations.forEach(function (mutation) {
            mutation.addedNodes.forEach(function (node) {
                if (node.nodeType === Node.ELEMENT_NODE) {
                    refresh(node);
                }
            });
        });
    });

    window.DevPrepFeedbackContent = { refresh };
    document.addEventListener("DOMContentLoaded", function () {
        refresh(document);
        observer.observe(document.body, { childList: true, subtree: true });
        const modalToReopen = document.querySelector("[data-open-on-load='true']");
        if (modalToReopen) {
            openDialog(modalToReopen, null);
        }
    });
}());
