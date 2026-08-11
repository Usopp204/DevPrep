(function () {
    "use strict";

    const modal = document.querySelector("[data-delete-session-modal]");
    const confirmButton = modal?.querySelector("[data-delete-session-confirm]");
    const sessionName = modal?.querySelector("[data-delete-session-name]");

    if (!modal || !confirmButton || !sessionName) return;

    const focusableSelector = [
        "a[href]",
        "button:not([disabled])",
        "input:not([disabled]):not([type='hidden'])",
        "select:not([disabled])",
        "textarea:not([disabled])",
        "[tabindex]:not([tabindex='-1'])"
    ].join(",");

    let activeForm = null;
    let activeTrigger = null;
    let bodyOverflowBeforeDialog = "";

    function setBackgroundInert(inert) {
        Array.from(document.body.children).forEach(function (element) {
            if (element === modal || element.contains(modal)) return;

            if (inert && !element.inert) {
                element.dataset.deleteSessionInert = "true";
                element.inert = true;
            } else if (!inert && element.dataset.deleteSessionInert === "true") {
                delete element.dataset.deleteSessionInert;
                element.inert = false;
            }
        });
    }

    function openDialog(trigger) {
        const form = trigger.closest("form");
        if (!form) return;

        activeForm = form;
        activeTrigger = trigger;
        sessionName.textContent = trigger.dataset.sessionTitle || "this interview session";
        confirmButton.disabled = false;
        modal.removeAttribute("aria-busy");
        modal.hidden = false;
        bodyOverflowBeforeDialog = document.body.style.overflow;
        document.body.style.overflow = "hidden";
        setBackgroundInert(true);

        const heading = modal.querySelector("h2");
        heading?.setAttribute("tabindex", "-1");
        heading?.focus();
    }

    function closeDialog() {
        if (modal.hidden) return;

        modal.hidden = true;
        document.body.style.overflow = bodyOverflowBeforeDialog;
        bodyOverflowBeforeDialog = "";
        setBackgroundInert(false);
        modal.querySelector("h2")?.removeAttribute("tabindex");

        const trigger = activeTrigger;
        activeForm = null;
        activeTrigger = null;
        trigger?.focus();
    }

    document.addEventListener("click", function (event) {
        const trigger = event.target.closest("[data-delete-session-trigger]");
        if (trigger) {
            openDialog(trigger);
            return;
        }

        if (!modal.hidden && event.target.closest("[data-delete-session-close]")) {
            closeDialog();
        }
    });

    confirmButton.addEventListener("click", function () {
        if (!activeForm || confirmButton.disabled) return;

        confirmButton.disabled = true;
        modal.setAttribute("aria-busy", "true");
        activeForm.requestSubmit();
    });

    document.addEventListener("keydown", function (event) {
        if (modal.hidden) return;

        if (event.key === "Escape") {
            event.preventDefault();
            closeDialog();
            return;
        }

        if (event.key !== "Tab") return;

        const focusable = Array.from(modal.querySelectorAll(focusableSelector));
        if (focusable.length === 0) {
            event.preventDefault();
            return;
        }

        const heading = modal.querySelector("h2");
        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (!modal.contains(document.activeElement)) {
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
})();
