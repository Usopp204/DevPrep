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
    let jdSkills = []; // required_skills + nice_to_have from the parsed JD, lowercased — drives Focus Areas pre-select.

    // Same technology, different names/abbreviations — used to pre-select catalog chips even
    // when the JD spells it differently. Deliberately does NOT include "is a kind of" pairs
    // (e.g. "SQL" vs "SQL Server" are related but different products, not synonyms) — that
    // would cause false positives, so those still require an exact match.
    const TECH_ALIAS_GROUPS = [
        ["javascript", "js"],
        ["typescript", "ts"],
        ["node.js", "node", "nodejs"],
        ["postgresql", "postgres"],
        ["mongodb", "mongo"],
        ["c#", "csharp", "c-sharp"],
        ["go", "golang"],
        ["python", "py"],
        ["kubernetes", "k8s"],
        ["vue.js", "vue", "vuejs"],
        ["react.js", "react", "reactjs"],
        ["next.js", "next", "nextjs"],
        [".net", "dotnet", "net"],
        ["sql server", "mssql", "ms sql", "microsoft sql server"],
    ];
    const TECH_ALIAS_LOOKUP = {};
    TECH_ALIAS_GROUPS.forEach((group) => group.forEach((name) => { TECH_ALIAS_LOOKUP[name] = group; }));

    // Whole-token containment check: does `name` appear as its own token inside `haystack`?
    // A JD skill entry is often a whole descriptive sentence, e.g. "JavaScript frameworks like
    // VueJS or ReactJS" — that never equals the catalog entry "JavaScript" exactly, but it does
    // mention it. Splits on anything that isn't a letter/digit/. /#/+ (so "c#", "c++", ".net",
    // "node.js" survive as single tokens) instead of using regex \b, which mishandles those
    // trailing symbol characters.
    function containsWholeToken(haystackLower, name) {
        const padded = " " + haystackLower.replace(/[^a-z0-9.#+]+/gi, " ") + " ";
        return padded.includes(" " + name.toLowerCase() + " ");
    }

    function techMatchesJd(catalogTech, jdSkillSet, jdSkillsJoined) {
        const t = catalogTech.toLowerCase();
        if (jdSkillSet.has(t)) return true;
        const group = TECH_ALIAS_LOOKUP[t] || [t];
        if (group.some((alias) => jdSkillSet.has(alias))) return true;
        return group.some((alias) => containsWholeToken(jdSkillsJoined, alias));
    }

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
        focusBox.innerHTML = "";

        const techs = techByRole[roleSelect.value] || [];
        if (!techs.length) {
            syncHiddenInputs();
            focusBox.innerHTML = '<span class="dash-hint">No technologies found for this role.</span>';
            return;
        }

        // If a JD has been parsed, suggest+pre-select only the catalog techs it actually mentions
        // (still editable — the user can un-tick any of them or add more from "All technologies").
        // Otherwise fall back to the generic popular-technologies shortlist (unselected, as before).
        let compact;
        if (jdSkills.length) {
            const jdSkillSet = new Set(jdSkills);
            const jdSkillsJoined = jdSkills.join(" | "); // already lowercased when jdSkills was built
            compact = techs.filter((t) => techMatchesJd(t, jdSkillSet, jdSkillsJoined));
            compact.forEach((t) => selected.add(t));
        } else {
            compact = POPULAR.filter((t) => techs.includes(t)).slice(0, 12);
        }
        syncHiddenInputs();
        compact.forEach((t) => focusBox.appendChild(makeChip(t, false)));

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

    // The Job Role picked on Profile/CvJd for the shared JD — applied as a one-time default here
    // (not a hard lock) once both the catalog and the JD have loaded, whichever finishes last.
    let jdPreferredPosition = null;
    let userChangedRole = false;
    let catalogRoles = [];

    function applyJdPreferredRole() {
        if (userChangedRole || !jdPreferredPosition || !catalogRoles.length) return;
        const p = jdPreferredPosition.toLowerCase();
        const match = catalogRoles.find((r) => r.toLowerCase() === p) ||
            catalogRoles.find((r) => p.includes(r.toLowerCase()));
        if (match && roleSelect.value !== match) {
            roleSelect.value = match;
            renderFocus();
        }
    }

    fetch(catalogUrl)
        .then((response) => response.json())
        .then((catalog) => {
            techByRole = catalog.technologies || {};
            if (Array.isArray(catalog.roles) && catalog.roles.length) {
                catalogRoles = catalog.roles;
                const current = roleSelect.value;
                roleSelect.innerHTML = catalog.roles
                    .map((role) => `<option value="${role}">${role}</option>`).join("");
                if (catalog.roles.includes(current)) roleSelect.value = current;
            }
            renderFocus();
            applyJdPreferredRole();
        })
        .catch(() => {
            focusBox.innerHTML =
                '<span class="dash-hint">Could not load technologies. You can still start; the whole role will be covered.</span>';
        });

    roleSelect.addEventListener("change", () => {
        userChangedRole = true;
        renderFocus();
    });

    // ── Job Description widget — shared with Profile/CvJd (same CvJdAPI JobDescription record) ──
    const ijdStatus = document.getElementById("ijd-status");
    const ijdParsedBox = document.getElementById("ijd-parsed");
    const ijdInputBox = document.getElementById("ijd-input");
    const ijdHiddenText = document.getElementById("ijd-hidden-text");
    const ijdHiddenId = document.getElementById("ijd-hidden-id");
    const ijdPasteTab = document.getElementById("ijd-tab-paste");
    const ijdFileTab = document.getElementById("ijd-tab-file");
    const ijdPasteMode = document.getElementById("ijd-paste-mode");
    const ijdFileMode = document.getElementById("ijd-file-mode");
    const ijdFileInput = document.getElementById("ijd-file");
    const ijdBtnAi = document.getElementById("ijd-btn-ai");
    const ijdBtnNormal = document.getElementById("ijd-btn-normal");
    const ijdBtnCancel = document.getElementById("ijd-btn-cancel");
    const ijdLoadingBox = document.getElementById("ijd-loading");
    const ijdElapsedEl = document.getElementById("ijd-elapsed");
    const ijdLoadingMsgEl = document.getElementById("ijd-loading-msg");
    let ijdTimer = null;
    let hasExistingJd = false; // true once a parsed JD has been shown — lets "Đổi JD" offer Hủy

    function setIjdLoading(on) {
        ijdLoadingBox.classList.toggle("active", on);
        if (on) {
            ijdInputBox.style.display = "none";
            let sec = 0;
            ijdElapsedEl.textContent = "0s";
            ijdLoadingMsgEl.textContent = "AI is reading the JD content...";
            ijdTimer = setInterval(() => {
                sec++;
                ijdElapsedEl.textContent = sec + "s";
                if (sec === 8) ijdLoadingMsgEl.textContent = "AI is extracting required skills...";
                if (sec === 30) ijdLoadingMsgEl.textContent = "Model is doing a deeper analysis, please wait a bit longer...";
                if (sec === 90) ijdLoadingMsgEl.textContent = "Almost done, just a bit more...";
            }, 1000);
        } else {
            clearInterval(ijdTimer);
            ijdTimer = null;
            if (ijdParsedBox.style.display !== "block") ijdInputBox.style.display = "block";
        }
    }

    function escHtml(s) {
        return String(s || "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }

    function skillLabel(s) {
        if (typeof s === "string") return s;
        return s.name || s.skill || s.title || JSON.stringify(s);
    }

    function setIjdStatus(message, ok) {
        ijdStatus.textContent = message || "";
        ijdStatus.style.color = ok === true ? "var(--color-primary)" : ok === false ? "var(--color-error)" : "";
    }

    function showParsedJd(clarified, fallbackPosition) {
        hasExistingJd = true;
        document.getElementById("ijd-parsed-position").textContent = clarified.position || fallbackPosition || "—";
        const company = clarified.company || "";
        const companyEl = document.getElementById("ijd-parsed-company");
        companyEl.textContent = company;
        companyEl.style.display = company ? "block" : "none";

        const reqs = (clarified.required_skills || []).map(skillLabel);
        const nices = (clarified.nice_to_have || []).map(skillLabel);
        jdSkills = reqs.concat(nices).map((s) => s.toLowerCase());

        const reqTags = document.getElementById("ijd-req-tags");
        reqTags.innerHTML = reqs.map((s) => `<span class="dash-chip">${escHtml(s)}</span>`).join("");
        document.getElementById("ijd-req-wrap").style.display = reqs.length ? "block" : "none";

        const niceTags = document.getElementById("ijd-nice-tags");
        niceTags.innerHTML = nices.map((s) => `<span class="dash-chip">${escHtml(s)}</span>`).join("");
        document.getElementById("ijd-nice-wrap").style.display = nices.length ? "block" : "none";

        ijdParsedBox.style.display = "block";
        ijdInputBox.style.display = "none";
        renderFocus(); // Focus Areas now knows jdSkills — re-render so it pre-selects them.
    }

    function showJdInputForm() {
        ijdParsedBox.style.display = "none";
        ijdInputBox.style.display = "block";
        // Pre-fill with the JD currently on file so switching to "Đổi JD" doesn't look like the
        // existing content vanished — it's still the same record until AI Parse/Normal is pressed.
        document.getElementById("ijd-text").value = ijdHiddenText.value || "";
        ijdBtnCancel.style.display = hasExistingJd ? "" : "none";
    }

    function cancelJdEdit() {
        ijdInputBox.style.display = "none";
        ijdParsedBox.style.display = "block";
        setIjdStatus("", null);
    }

    async function loadCurrentJd() {
        try {
            const resp = await fetch("/CvJd/GetJdInfo");
            if (!resp.ok) return;
            const jd = await resp.json().catch(() => null);
            if (!jd || jd.error) return;
            ijdHiddenText.value = jd.rawText || "";
            ijdHiddenId.value = jd.jdId || "";
            if (jd.position) {
                jdPreferredPosition = jd.position;
                applyJdPreferredRole();
            }
            if (jd.clarifiedJson) {
                try {
                    const clarified = JSON.parse(jd.clarifiedJson);
                    showParsedJd(clarified, jd.position || "");
                    setIjdStatus("Loaded your current JD from your profile.", true);
                } catch { /* malformed clarifiedJson — leave the input form for the user to re-parse */ }
            }
        } catch { /* network error — leave the input form so the user can still type/paste a JD */ }
    }

    ijdPasteTab.addEventListener("click", () => {
        ijdPasteTab.classList.add("selected");
        ijdFileTab.classList.remove("selected");
        ijdPasteMode.style.display = "block";
        ijdFileMode.style.display = "none";
    });
    ijdFileTab.addEventListener("click", () => {
        ijdFileTab.classList.add("selected");
        ijdPasteTab.classList.remove("selected");
        ijdFileMode.style.display = "block";
        ijdPasteMode.style.display = "none";
    });
    ijdFileInput.addEventListener("change", () => {
        const file = ijdFileInput.files && ijdFileInput.files[0];
        document.getElementById("ijd-file-name").textContent = file ? file.name : "";
    });
    document.getElementById("ijd-change-btn").addEventListener("click", showJdInputForm);

    async function parseJd(mode) {
        const isFile = ijdFileTab.classList.contains("selected");
        const text = document.getElementById("ijd-text").value.trim();
        if (isFile && !(ijdFileInput.files && ijdFileInput.files.length)) {
            setIjdStatus("Please choose a JD file first.", false);
            return;
        }
        if (!isFile && !text) {
            setIjdStatus("Please enter the JD content first.", false);
            return;
        }

        ijdBtnAi.disabled = true;
        ijdBtnNormal.disabled = true;
        setIjdStatus("", null);
        setIjdLoading(true);

        try {
            let resp;
            if (isFile) {
                const form = new FormData();
                form.append("file", ijdFileInput.files[0]);
                form.append("position", roleSelect.value);
                resp = await fetch("/CvJd/UploadJdFile", { method: "POST", body: form });
            } else {
                const form = new FormData();
                form.append("jdText", text);
                form.append("position", roleSelect.value);
                form.append("parseMode", mode);
                resp = await fetch("/CvJd/SaveJd", { method: "POST", body: form });
            }
            const data = await resp.json().catch(() => ({}));
            if (data.error) {
                setIjdStatus("Error: " + data.error, false);
                return;
            }
            await loadCurrentJd(); // re-fetch the canonical record (with JdId) and render it
            setIjdStatus(mode === "ai" ? "JD parsed successfully by AI!" : "JD saved (Normal mode).", true);
        } catch (e) {
            setIjdStatus("Error: " + e.message, false);
        } finally {
            ijdBtnAi.disabled = false;
            ijdBtnNormal.disabled = false;
            setIjdLoading(false);
        }
    }

    ijdBtnAi.addEventListener("click", () => parseJd("ai"));
    ijdBtnNormal.addEventListener("click", () => parseJd("normal"));
    ijdBtnCancel.addEventListener("click", cancelJdEdit);

    loadCurrentJd();
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