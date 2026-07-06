// Dashboard: load the ai-service roles/technologies catalog, fill the Job Role dropdown, and let
// the user pick Focus Areas for the selected role. Selected focus areas are mirrored as hidden
// <input name="FocusAreas"> so they post with the standard form (no change to the submit flow).
(function () {
    const form = document.getElementById("dash-form");
    if (!form) return;

    const catalogUrl = form.dataset.catalogUrl;
    const roleSelect = document.getElementById("role");
    const focusBox = document.getElementById("focus-control");
    const selectedBox = document.getElementById("focus-selected");

    // Editorial "popular" ordering; only those present for the role are shown as default chips.
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
        focusBox.querySelectorAll("button[data-tech]").forEach((b) =>
            b.classList.toggle("selected", selected.has(b.dataset.tech)));
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

        // Default: a handful of popular chips that exist for this role.
        const popular = POPULAR.filter((t) => techs.includes(t)).slice(0, 12);
        popular.forEach((t) => focusBox.appendChild(makeChip(t, false)));

        // Toggle to reveal the full, searchable list.
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
        search.placeholder = "Search technologies…";
        search.className = "dash-input dash-focus-search";

        const list = document.createElement("div");
        list.className = "dash-chips";
        techs.forEach((t) => list.appendChild(makeChip(t, true)));

        search.addEventListener("input", () => {
            const q = search.value.trim().toLowerCase();
            list.querySelectorAll("button[data-tech]").forEach((b) =>
                b.style.display = (!q || b.dataset.tech.toLowerCase().includes(q)) ? "" : "none");
        });

        panel.appendChild(search);
        panel.appendChild(list);
        focusBox.appendChild(panel);
        toggle.addEventListener("click", () =>
            panel.style.display = panel.style.display === "none" ? "" : "none");
    }

    fetch(catalogUrl)
        .then((r) => r.json())
        .then((cat) => {
            techByRole = cat.technologies || {};
            if (Array.isArray(cat.roles) && cat.roles.length) {
                const current = roleSelect.value;
                roleSelect.innerHTML = cat.roles
                    .map((r) => `<option value="${r}">${r}</option>`).join("");
                if (cat.roles.includes(current)) roleSelect.value = current;
            }
            renderFocus();
        })
        .catch(() => {
            focusBox.innerHTML =
                '<span class="dash-hint">Could not load technologies — you can still start; the whole role will be covered.</span>';
        });

    roleSelect.addEventListener("change", renderFocus);
})();
