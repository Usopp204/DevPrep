(function () {
    "use strict";

    window.TechStackLiveList?.initialize({
        listPath: "/Admin/TechStacks",
        resultsPath: "/Admin/TechStacks/Results",
        forbiddenMessage: "You do not have permission to manage Tech Stacks.",
        errorMessage: "Tech Stack results could not be loaded. Please try again."
    });

    const modalElement = document.getElementById("techStackModal");
    const form = document.getElementById("techStackForm");
    const resultsRegion = document.getElementById("techStackResults");
    if (!modalElement || !form || !resultsRegion) {
        return;
    }

    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    const title = document.getElementById("techStackModalTitle");
    const subtitle = document.getElementById("techStackModalSubtitle");
    const idInput = document.getElementById("techStackId");
    const nameInput = document.getElementById("techStackName");
    const descriptionInput = document.getElementById("techStackDescription");
    const statusField = document.getElementById("techStackStatusField");
    const activeInput = document.getElementById("techStackStatusActive");
    const inactiveInput = document.getElementById("techStackStatusInactive");
    const createStatusNote = document.getElementById("techStackCreateStatusNote");
    const nameError = document.getElementById("techStackNameError");
    const formError = document.getElementById("techStackFormError");
    const nameCount = document.getElementById("nameCount");
    const descriptionCount = document.getElementById("descriptionCount");
    const saveButton = document.getElementById("saveTechStackButton");
    const saveLabel = saveButton.querySelector(".button-label");
    const saveIcon = document.getElementById("saveTechStackIcon");

    function antiForgeryToken() {
        return document.querySelector(
            "#techStackAntiForgeryForm input[name='__RequestVerificationToken']"
        )?.value ?? "";
    }

    function setButtonPending(button, isPending) {
        button.disabled = isPending;
        button.setAttribute("aria-busy", isPending ? "true" : "false");
        button.classList.toggle("is-pending", isPending);
    }

    function clearActionPending(button) {
        setButtonPending(button, false);
        button.dataset.pending = "false";
    }

    function clearFormPending() {
        setButtonPending(saveButton, false);
        form.dataset.submitting = "false";
    }

    function resetErrors() {
        nameError.textContent = "";
        formError.textContent = "";
    }

    function updateCounters() {
        nameCount.textContent = String(nameInput.value.length);
        descriptionCount.textContent = String(descriptionInput.value.length);
    }

    function showRequestError(message) {
        formError.textContent = message ||
            "The request could not be completed. Please try again.";
    }

    async function parseResponse(response) {
        let payload = null;
        try {
            payload = await response.json();
        } catch {
            payload = null;
        }

        if (response.status === 401) {
            window.location.assign("/Auth/Login");
            const error = new Error("Your session has expired. Please sign in again.");
            error.status = response.status;
            throw error;
        }

        if (response.status === 403) {
            const error = new Error("You do not have permission to manage Tech Stacks.");
            error.status = response.status;
            throw error;
        }

        if (!response.ok || !payload?.success) {
            const error = new Error(
                payload?.message || "The request could not be completed. Please try again."
            );
            error.status = response.status;
            throw error;
        }

        return payload;
    }

    async function sendPost(url, body) {
        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" },
            body
        });

        return parseResponse(response);
    }

    function reloadCurrentPage() {
        window.location.assign(window.location.href);
    }

    async function showCreateSuccess(message) {
        await Swal.fire({
            icon: "success",
            title: "Success",
            text: message,
            timer: 1400,
            showConfirmButton: false
        });
        window.location.assign("/Admin/TechStacks?pageIndex=1");
    }

    async function showActionSuccess(message) {
        await Swal.fire({
            icon: "success",
            title: "Success",
            text: message,
            confirmButtonText: "Done"
        });
        reloadCurrentPage();
    }

    async function showStaleRecordError(action) {
        await Swal.fire({
            icon: "error",
            title: `Unable to ${action}`,
            text: "This Tech Stack no longer exists. The list will now be refreshed."
        });
        reloadCurrentPage();
    }

    function configureCreateModal() {
        form.reset();
        resetErrors();
        clearFormPending();
        idInput.value = "";
        activeInput.disabled = true;
        inactiveInput.disabled = true;
        activeInput.checked = false;
        inactiveInput.checked = false;
        statusField.hidden = true;
        createStatusNote.hidden = false;
        title.textContent = "Create Tech Stack";
        subtitle.textContent = "Add a new technology or interview focus area.";
        saveLabel.textContent = "Create Tech Stack";
        saveIcon.textContent = "add_circle";
        updateCounters();
    }

    function configureEditModal(item) {
        resetErrors();
        clearFormPending();
        idInput.value = item.techStackId;
        nameInput.value = item.name ?? "";
        descriptionInput.value = item.description ?? "";
        activeInput.disabled = false;
        inactiveInput.disabled = false;
        activeInput.checked = Boolean(item.isActive);
        inactiveInput.checked = !item.isActive;
        statusField.hidden = false;
        createStatusNote.hidden = true;
        title.textContent = "Edit Tech Stack";
        subtitle.textContent =
            "Update the display information and availability of this Tech Stack.";
        saveLabel.textContent = "Save Changes";
        saveIcon.textContent = "save";
        updateCounters();
    }

    function showModalAndFocusName() {
        modal.show();
        modalElement.addEventListener("shown.bs.modal", function () {
            nameInput.focus();
        }, { once: true });
    }

    async function editTechStack(button) {
        if (button.dataset.pending === "true") {
            return;
        }

        button.dataset.pending = "true";
        setButtonPending(button, true);
        try {
            const response = await fetch(
                `/Admin/TechStacks/Detail/${encodeURIComponent(button.dataset.techStackId)}`,
                {
                    credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                }
            );
            const payload = await parseResponse(response);
            configureEditModal(payload.data);
            showModalAndFocusName();
        } catch (error) {
            if (error.status === 404) {
                await showStaleRecordError("edit");
            } else {
                await Swal.fire({
                    icon: "error",
                    title: "Unable to edit",
                    text: error.message
                });
            }
        } finally {
            clearActionPending(button);
        }
    }

    async function deactivateTechStack(button) {
        if (button.dataset.pending === "true") {
            return;
        }

        button.dataset.pending = "true";
        try {
            const name = button.dataset.techStackName || "this Tech Stack";
            const confirmation = await Swal.fire({
                icon: "warning",
                title: "Deactivate Tech Stack?",
                text: `Existing Questions remain associated. This Tech Stack will become inactive and can be reactivated later. (${name})`,
                showCancelButton: true,
                confirmButtonText: "Deactivate",
                confirmButtonColor: "#dc2626"
            });
            if (!confirmation.isConfirmed) {
                return;
            }

            setButtonPending(button, true);
            const body = new FormData();
            body.append("__RequestVerificationToken", antiForgeryToken());
            const payload = await sendPost(
                `/Admin/TechStacks/Deactivate/${encodeURIComponent(button.dataset.techStackId)}`,
                body
            );
            clearActionPending(button);
            await showActionSuccess(payload.message);
        } catch (error) {
            clearActionPending(button);
            if (error.status === 404) {
                await showStaleRecordError("deactivate");
            } else {
                await Swal.fire({
                    icon: "error",
                    title: "Unable to deactivate",
                    text: error.message
                });
            }
        } finally {
            if (button.dataset.pending === "true") {
                clearActionPending(button);
            }
        }
    }

    async function reactivateTechStack(button) {
        if (button.dataset.pending === "true") {
            return;
        }

        button.dataset.pending = "true";
        try {
            const confirmation = await Swal.fire({
                icon: "question",
                title: "Reactivate Tech Stack?",
                text: `${button.dataset.techStackName || "This Tech Stack"} will become available for use again.`,
                showCancelButton: true,
                confirmButtonText: "Reactivate",
                confirmButtonColor: "#059669"
            });
            if (!confirmation.isConfirmed) {
                return;
            }

            setButtonPending(button, true);
            const body = new FormData();
            body.append("__RequestVerificationToken", antiForgeryToken());
            const payload = await sendPost(
                `/Admin/TechStacks/Reactivate/${encodeURIComponent(button.dataset.techStackId)}`,
                body
            );
            clearActionPending(button);
            await showActionSuccess(payload.message);
        } catch (error) {
            clearActionPending(button);
            if (error.status === 404) {
                await showStaleRecordError("reactivate");
            } else {
                await Swal.fire({
                    icon: "error",
                    title: "Unable to reactivate",
                    text: error.message
                });
            }
        } finally {
            if (button.dataset.pending === "true") {
                clearActionPending(button);
            }
        }
    }

    document.getElementById("createTechStackButton")?.addEventListener("click", function () {
        configureCreateModal();
        showModalAndFocusName();
    });

    nameInput.addEventListener("input", updateCounters);
    descriptionInput.addEventListener("input", updateCounters);

    resultsRegion.addEventListener("click", function (event) {
        if (!(event.target instanceof Element)) {
            return;
        }

        const editButton = event.target.closest(".js-edit-tech-stack");
        if (editButton) {
            editTechStack(editButton);
            return;
        }

        const deactivateButton = event.target.closest(".js-deactivate-tech-stack");
        if (deactivateButton) {
            deactivateTechStack(deactivateButton);
            return;
        }

        const reactivateButton = event.target.closest(".js-reactivate-tech-stack");
        if (reactivateButton) {
            reactivateTechStack(reactivateButton);
        }
    });

    form.addEventListener("submit", async function (event) {
        event.preventDefault();
        if (form.dataset.submitting === "true") {
            return;
        }

        resetErrors();
        nameInput.value = nameInput.value.trim();
        updateCounters();
        if (!nameInput.value) {
            nameError.textContent = "Name is required.";
            nameInput.focus();
            return;
        }

        const techStackId = idInput.value;
        const isCreate = !techStackId;
        if (!isCreate && !activeInput.checked && !inactiveInput.checked) {
            showRequestError("Status is required.");
            return;
        }

        const endpoint = isCreate
            ? "/Admin/TechStacks/Create"
            : `/Admin/TechStacks/Update/${encodeURIComponent(techStackId)}`;

        form.dataset.submitting = "true";
        setButtonPending(saveButton, true);
        try {
            const payload = await sendPost(endpoint, new FormData(form));
            clearFormPending();
            modal.hide();

            if (isCreate) {
                await showCreateSuccess(payload.message);
            } else {
                await showActionSuccess(payload.message);
            }
        } catch (error) {
            clearFormPending();
            if (error.status === 404 && !isCreate) {
                modal.hide();
                await showStaleRecordError("update");
            } else {
                showRequestError(error.message);
            }
        } finally {
            if (form.dataset.submitting === "true") {
                clearFormPending();
            }
        }
    });
})();