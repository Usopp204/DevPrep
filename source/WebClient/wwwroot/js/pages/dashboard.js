(function () {
    const form = document.getElementById("dash-form");
    if (!form) return;

    const catalogUrl = form.dataset.catalogUrl;
    const roleSelect = document.getElementById("role");
    const focusBox = document.getElementById("focus-control");
    const selectedBox = document.getElementById("focus-selected");

    const POPULAR = ["React", "Node.js", "TypeScript", "JavaScript", "Python", "FastAPI", "Django",
        "Flask", "Express", "NestJS", "Next.js", "Vue", "Angular", "Spring Boot", "PostgreSQL",
        "MongoDB", "MySQL", "Redis", "Go", "Rust", "Java", "C#", "ASP.NET Core", "Svelte"];

    const selected = new Set();
    let techByRole = {};

    function syncHiddenInputs() {
        selectedBox.innerHTML = "";
        selected.forEach((tech) => {
            const input = document.createElement("input");
            input.type = "hidden";
            input.name = "FocusAreas";
            input.value = tech;
            selectedBox.appendChild(input);
        });
    }

    function refreshChipStates() {
        focusBox.querySelectorAll("button[data-tech]").forEach((button) =>
            button.classList.toggle("selected", selected.has(button.dataset.tech)));
    }

    function makeChip(tech, small) {
        const btn = document.createElement("button");
        btn.type = "button";
        btn.dataset.tech = tech;
        btn.textContent = tech;
        btn.className = "dash-chip selectable" + (small ? " dash-chip-sm" : "");
        btn.classList.toggle("selected", selected.has(tech));
        btn.addEventListener("click", () => {
            if (selected.has(tech)) selected.delete(tech); else selected.add(tech);
            refreshChipStates();
            syncHiddenInputs();
        });
        return btn;
    }

    function renderFocus() {
        selected.clear();
        syncHiddenInputs();
        focusBox.innerHTML = "";

        const techs = techByRole[roleSelect.value] || [];
        if (!techs.length) {
            focusBox.innerHTML = '<span class="dash-hint">No technologies found for this role.</span>';
            return;
        }

        const popular = POPULAR.filter((t) => techs.includes(t)).slice(0, 12);
        popular.forEach((t) => focusBox.appendChild(makeChip(t, false)));

        const toggle = document.createElement("button");
        toggle.type = "button";
        toggle.className = "dash-chip dash-chip-outline";
        toggle.textContent = `All technologies (${techs.length})`;
        focusBox.appendChild(toggle);

        const panel = document.createElement("div");
        panel.className = "dash-focus-panel";
        panel.style.display = "none";

        const search = document.createElement("input");
        search.type = "text";
        search.placeholder = "Search technologies...";
        search.className = "dash-input dash-focus-search";

        const list = document.createElement("div");
        list.className = "dash-chips";
        techs.forEach((t) => list.appendChild(makeChip(t, true)));

        search.addEventListener("input", () => {
            const query = search.value.trim().toLowerCase();
            list.querySelectorAll("button[data-tech]").forEach((button) =>
                button.style.display = (!query || button.dataset.tech.toLowerCase().includes(query)) ? "" : "none");
        });

        panel.appendChild(search);
        panel.appendChild(list);
        focusBox.appendChild(panel);
        toggle.addEventListener("click", () =>
            panel.style.display = panel.style.display === "none" ? "" : "none");
    }

    fetch(catalogUrl)
        .then((response) => response.json())
        .then((catalog) => {
            techByRole = catalog.technologies || {};
            if (Array.isArray(catalog.roles) && catalog.roles.length) {
                const current = roleSelect.value;
                roleSelect.innerHTML = catalog.roles
                    .map((role) => `<option value="${role}">${role}</option>`).join("");
                if (catalog.roles.includes(current)) roleSelect.value = current;
            }
            renderFocus();
        })
        .catch(() => {
            focusBox.innerHTML =
                '<span class="dash-hint">Could not load technologies. You can still start; the whole role will be covered.</span>';
        });

    roleSelect.addEventListener("change", renderFocus);
})();

(function () {
    const list = document.getElementById("recent-session-list");
    const state = document.getElementById("recent-session-state");
    if (!list || !list.dataset.url) return;

    let loading = false;
    let nextPage = Number(list.dataset.nextPage || "2");
    let hasMore = list.dataset.hasMore === "true";
    const loadedSessionIds = new Set(
        Array.from(list.querySelectorAll("[data-session-id]"), (item) => item.dataset.sessionId)
    );

    function setState(message, mode) {
        if (!state) return;
        state.textContent = message || "";
        state.dataset.mode = mode || "";
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function appendSession(item) {
        if (!item.sessionId || loadedSessionIds.has(item.sessionId)) return;

        loadedSessionIds.add(item.sessionId);
        const link = document.createElement("a");
        link.className = "dash-session-item";
        link.href = item.detailsUrl;
        link.dataset.sessionId = item.sessionId;
        link.innerHTML = `<span class="dash-session-title">${escapeHtml(item.title)}</span>` +
            `<span class="dash-session-meta">${escapeHtml(item.level)} &middot; ${escapeHtml(item.displayStatus)} &middot; ${escapeHtml(item.relativeTime)}</span>`;
        list.appendChild(link);
    }

    async function loadMore() {
        if (loading || !hasMore) return;
        loading = true;
        setState("Loading more sessions...", "loading");

        try {
            const response = await fetch(`${list.dataset.url}?page=${nextPage}`, { headers: { "Accept": "application/json" } });
            if (!response.ok) throw new Error("Could not load recent sessions.");
            const data = await response.json();
            (data.items || []).forEach(appendSession);
            hasMore = data.totalPages > data.page;
            nextPage = data.page + 1;
            list.dataset.nextPage = String(nextPage);
            list.dataset.hasMore = hasMore ? "true" : "false";
            setState(hasMore ? "" : "All recent sessions loaded.", hasMore ? "" : "done");
        } catch (error) {
            setState("Could not load more sessions.", "error");
        } finally {
            loading = false;
        }
    }

    list.addEventListener("scroll", () => {
        const nearBottom = list.scrollTop + list.clientHeight >= list.scrollHeight - 24;
        if (nearBottom) loadMore();
    });
})();

(function () {
    document.querySelectorAll("[data-tabs]").forEach((tabs) => {
        tabs.addEventListener("click", (event) => {
            const button = event.target.closest("[data-tab-target]");
            if (!button) return;
            const panelId = button.dataset.tabTarget;
            const scope = tabs.closest(".history-panel") || document;
            tabs.querySelectorAll("[data-tab-target]").forEach((tab) => tab.classList.toggle("is-active", tab === button));
            scope.querySelectorAll(".detail-tab-panel").forEach((panel) => panel.classList.toggle("is-active", panel.id === panelId));
        });
    });
})();