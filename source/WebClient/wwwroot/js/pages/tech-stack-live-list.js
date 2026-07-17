(function (global) {
    "use strict";

    const debounceDelayMilliseconds = 350;
    const validStatuses = ["All", "Active", "Inactive"];

    function initialize(options) {
        const filterForm = document.querySelector(
            options.filterFormSelector || ".tech-stacks-filter-form"
        );
        const resultsRegion = document.getElementById(
            options.resultsRegionId || "techStackResults"
        );
        const searchInput = filterForm?.querySelector("input[name='search']");
        const statusFilter = filterForm?.querySelector("select[name='status']");
        const searchButton = filterForm?.querySelector("[data-tech-stack-search-button]");
        const filterResetLink = filterForm?.querySelector(".js-reset-tech-stack-filters");

        if (!filterForm || !resultsRegion || !searchInput || !statusFilter ||
            !searchButton || resultsRegion.dataset.liveListInitialized === "true") {
            return null;
        }

        resultsRegion.dataset.liveListInitialized = "true";

        let searchTimer = null;
        let resultsController = null;
        let resultsRequestSequence = 0;

        function setResultsLoading(isLoading) {
            resultsRegion.setAttribute("aria-busy", isLoading ? "true" : "false");
            resultsRegion.classList.toggle("is-loading", isLoading);
        }

        function currentFilterState(pageIndex = 1) {
            return {
                search: searchInput.value.trim(),
                status: statusFilter.value || "All",
                pageIndex: Math.max(1, pageIndex)
            };
        }

        function stateFromLocation() {
            const query = new URLSearchParams(window.location.search);
            const pageIndex = Number.parseInt(query.get("pageIndex") || "1", 10);
            const status = query.get("status");

            return {
                search: query.get("search") || "",
                status: validStatuses.includes(status) ? status : "All",
                pageIndex: Number.isFinite(pageIndex) ? Math.max(1, pageIndex) : 1
            };
        }

        function urlFor(path, state) {
            const query = new URLSearchParams();
            if (state.search) {
                query.set("search", state.search);
            }
            query.set("status", state.status);
            query.set("pageIndex", String(state.pageIndex));
            return `${path}?${query.toString()}`;
        }

        function updateFilterControls(state) {
            searchInput.value = state.search;
            statusFilter.value = state.status;

            const isFiltered = Boolean(state.search) || state.status !== "All";
            filterResetLink?.classList.toggle("d-none", !isFiltered);
        }

        function abortResultsRequest() {
            resultsController?.abort();
            resultsController = null;
        }

        function replaceResults(nextRegion) {
            const nodes = Array.from(nextRegion.childNodes).map(function (node) {
                return document.importNode(node, true);
            });

            resultsRegion.replaceChildren(...nodes);
            resultsRegion.dataset.pageIndex = nextRegion.dataset.pageIndex || "1";
            resultsRegion.dataset.search = nextRegion.dataset.search || "";
            resultsRegion.dataset.status = nextRegion.dataset.status || "All";
        }

        function renderResultsFailure(message, state) {
            const statePanel = document.createElement("div");
            statePanel.className = "tech-stacks-state tech-stacks-state-error";
            statePanel.setAttribute("role", "alert");

            const icon = document.createElement("span");
            icon.className = "material-symbols-outlined";
            icon.textContent = "cloud_off";

            const heading = document.createElement("h3");
            heading.textContent = "Unable to load Tech Stacks";

            const detail = document.createElement("p");
            detail.textContent = message;

            const retry = document.createElement("a");
            retry.className = "ts-button ts-button-secondary";
            retry.href = urlFor(options.listPath, state);
            retry.textContent = "Try again";

            statePanel.append(icon, heading, detail, retry);
            resultsRegion.replaceChildren(statePanel);
        }

        async function loadResults(pageIndex = 1, updateHistory = true) {
            window.clearTimeout(searchTimer);
            searchTimer = null;
            abortResultsRequest();

            const state = currentFilterState(pageIndex);
            const controller = new AbortController();
            const sequence = ++resultsRequestSequence;
            resultsController = controller;
            setResultsLoading(true);

            try {
                const response = await fetch(urlFor(options.resultsPath, state), {
                    method: "GET",
                    credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    signal: controller.signal
                });

                if (response.redirected &&
                    new URL(response.url).pathname.startsWith("/Auth/Login")) {
                    window.location.assign("/Auth/Login");
                    return;
                }

                if (response.status === 403) {
                    throw new Error(options.forbiddenMessage);
                }

                if (!response.ok) {
                    throw new Error(options.errorMessage);
                }

                const responseDocument = new DOMParser().parseFromString(
                    await response.text(),
                    "text/html"
                );
                const nextRegion = responseDocument.getElementById(
                    options.resultsRegionId || "techStackResults"
                );

                if (!nextRegion) {
                    throw new Error(options.errorMessage);
                }

                if (sequence !== resultsRequestSequence) {
                    return;
                }

                replaceResults(nextRegion);
                const resolvedState = {
                    search: resultsRegion.dataset.search || "",
                    status: resultsRegion.dataset.status || "All",
                    pageIndex: Number.parseInt(
                        resultsRegion.dataset.pageIndex || "1",
                        10
                    )
                };

                updateFilterControls(resolvedState);
                if (updateHistory) {
                    window.history.replaceState(
                        null,
                        "",
                        urlFor(options.listPath, resolvedState)
                    );
                }
            } catch (error) {
                if (error.name === "AbortError" ||
                    sequence !== resultsRequestSequence) {
                    return;
                }

                renderResultsFailure(error.message || options.errorMessage, state);
                if (updateHistory) {
                    window.history.replaceState(
                        null,
                        "",
                        urlFor(options.listPath, state)
                    );
                }
            } finally {
                if (sequence === resultsRequestSequence) {
                    resultsController = null;
                    setResultsLoading(false);
                }
            }
        }

        function scheduleLiveSearch() {
            window.clearTimeout(searchTimer);
            resultsRequestSequence += 1;
            abortResultsRequest();
            setResultsLoading(false);
            searchTimer = window.setTimeout(function () {
                loadResults(1);
            }, debounceDelayMilliseconds);
        }

        function runImmediateSearch() {
            window.clearTimeout(searchTimer);
            searchTimer = null;
            loadResults(1);
        }

        filterForm.addEventListener("submit", function (event) {
            event.preventDefault();
            runImmediateSearch();
        });

        searchButton.addEventListener("click", function (event) {
            event.preventDefault();
            runImmediateSearch();
        });

        searchInput.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                runImmediateSearch();
            }
        });

        searchInput.addEventListener("input", scheduleLiveSearch);
        statusFilter.addEventListener("change", runImmediateSearch);

        document.addEventListener("click", function (event) {
            if (!(event.target instanceof Element)) {
                return;
            }

            const resetLink = event.target.closest(".js-reset-tech-stack-filters");
            if (!resetLink) {
                return;
            }

            event.preventDefault();
            searchInput.value = "";
            statusFilter.value = "All";
            runImmediateSearch();
        });

        resultsRegion.addEventListener("click", function (event) {
            if (!(event.target instanceof Element)) {
                return;
            }

            const pageLink = event.target.closest(
                ".tech-stacks-page-link:not(.disabled)"
            );
            if (!pageLink) {
                return;
            }

            event.preventDefault();
            const destination = new URL(pageLink.href, window.location.origin);
            const pageIndex = Number.parseInt(
                destination.searchParams.get("pageIndex") || "1",
                10
            );
            loadResults(Number.isFinite(pageIndex) ? pageIndex : 1);
        });

        window.addEventListener("popstate", function () {
            const state = stateFromLocation();
            updateFilterControls(state);
            loadResults(state.pageIndex, false);
        });

        return { loadResults };
    }

    global.TechStackLiveList = {
        debounceDelayMilliseconds,
        initialize
    };
})(window);
