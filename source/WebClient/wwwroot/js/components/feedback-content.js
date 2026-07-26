(function () {
    "use strict";

    if (window.DevPrepFeedbackContent) {
        return;
    }

    const formSelector = "[data-feedback-form]";
    const expandableSelector = "[data-expandable-text]";
    let resizeTimer;

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
    });
}());