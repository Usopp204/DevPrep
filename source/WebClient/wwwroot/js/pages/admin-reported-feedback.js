(function () {
    "use strict";

    if (window.DevPrepAdminReportedFeedback) {
        return;
    }

    let activeModal = null;
    let activeTrigger = null;

    function updateCounter(modal) {
        const note = modal.querySelector("[data-resolution-note]");
        const counter = modal.querySelector("[data-resolution-counter]");
        if (note && counter) {
            counter.textContent = `${note.value.length} / ${note.maxLength}`;
            counter.classList.toggle("is-invalid", note.value.length > note.maxLength);
        }
    }

    function openModal(kind, trigger) {
        const modal = document.querySelector(`[data-resolution-modal="${kind}"]`);
        if (!modal) {
            return;
        }

        activeModal = modal;
        activeTrigger = trigger;
        modal.querySelector("[data-resolution-report-id]").value = trigger.dataset.reportId;
        modal.querySelector("[data-resolution-target]").textContent = trigger.dataset.reportReason || "Selected feedback report";
        modal.hidden = false;
        document.body.style.overflow = "hidden";
        updateCounter(modal);
        window.requestAnimationFrame(function () {
            modal.querySelector("[data-resolution-note]")?.focus();
        });
    }

    function closeModal() {
        if (!activeModal || activeModal.querySelector("[data-resolution-form]")?.dataset.submitting === "true") {
            return;
        }

        activeModal.hidden = true;
        document.body.style.overflow = "";
        activeTrigger?.focus();
        activeModal = null;
        activeTrigger = null;
    }

    document.addEventListener("click", function (event) {
        const dismiss = event.target.closest("[data-report-dismiss]");
        if (dismiss) {
            openModal("dismiss", dismiss);
            return;
        }

        const remove = event.target.closest("[data-report-delete]");
        if (remove) {
            openModal("delete", remove);
            return;
        }

        if (event.target.closest("[data-resolution-close]")) {
            closeModal();
        }
    });

    document.addEventListener("input", function (event) {
        const modal = event.target.closest("[data-resolution-modal]");
        if (modal && event.target.matches("[data-resolution-note]")) {
            updateCounter(modal);
        }
    });

    document.addEventListener("submit", function (event) {
        const form = event.target.closest("[data-resolution-form]");
        if (!form) {
            return;
        }

        if (form.dataset.submitting === "true") {
            event.preventDefault();
            return;
        }

        if (!form.checkValidity()) {
            event.preventDefault();
            form.reportValidity();
            return;
        }

        form.dataset.submitting = "true";
        form.setAttribute("aria-busy", "true");
        const submit = form.querySelector("[data-resolution-submit]");
        submit.disabled = true;
        submit.textContent = submit.dataset.pendingText || "Working...";
    });

    document.addEventListener("keydown", function (event) {
        if (!activeModal || event.key !== "Escape") {
            return;
        }

        event.preventDefault();
        closeModal();
    });

    window.DevPrepAdminReportedFeedback = { open: openModal };
}());
