(() => {
    "use strict";

    const root = document.querySelector("[data-management-dashboard]");
    if (!root || root.dataset.initialized === "true") return;
    root.dataset.initialized = "true";

    const form = root.querySelector("[data-dashboard-form]");
    const fromInput = root.querySelector("#dashboard-from");
    const toInput = root.querySelector("#dashboard-to");
    const applyButton = root.querySelector("[data-apply-button]");
    const chart = root.querySelector("[data-revenue-chart]");
    const bucketList = root.querySelector("[data-bucket-list]");
    const summary = root.querySelector("[data-revenue-summary]");
    const unavailableNotice = root.querySelector("[data-dashboard-unavailable]");
    const errorNotice = root.querySelector("[data-dashboard-error]");
    const initialNode = root.querySelector("[data-dashboard-initial]");
    if (!form || !fromInput || !toInput || !applyButton || !chart || !bucketList || !summary || !initialNode) return;

    const endpoint = root.dataset.dashboardUrl;
    const number = new Intl.NumberFormat("en-US");
    let activeController = null;
    let requestSequence = 0;
    let renderedBucketCount = 0;
    let renderedGranularity = "Daily";
    let resizeFrame = 0;

    function inclusiveDays(from, to) {
        const start = Date.parse(from + "T00:00:00Z");
        const end = Date.parse(to + "T00:00:00Z");
        return Math.floor((end - start) / 86400000) + 1;
    }

    function formatNumber(value) {
        return value === null || value === undefined ? "—" : number.format(Number(value));
    }

    function formatVnd(value) {
        return value === null || value === undefined ? "—" : number.format(Number(value)) + " VND";
    }

    function dateParts(value) {
        const parts = String(value || "").slice(0, 10).split("-");
        return parts.length === 3 ? { year: parts[0], month: parts[1], day: parts[2] } : null;
    }

    function shortDate(value) {
        const parts = dateParts(value);
        return parts ? parts.day + "/" + parts.month : "";
    }

    function fullDate(value) {
        const parts = dateParts(value);
        return parts ? parts.day + "/" + parts.month + "/" + parts.year : String(value || "");
    }

    function tickText(bucket, granularity) {
        if (granularity === "Monthly") {
            const parts = dateParts(bucket.startDate);
            return parts ? parts.month + "/" + parts.year : "";
        }
        if (granularity === "Weekly")
            return shortDate(bucket.startDate) + "–" + shortDate(bucket.endDate);
        return shortDate(bucket.startDate);
    }

    function accessibleBucketLabel(bucket, revenue, orders) {
        const start = fullDate(bucket.startDate);
        const end = fullDate(bucket.endDate);
        const dateText = start === end ? start : start + " to " + end;
        const orderText = formatNumber(orders) + " paid VNPay " + (orders === 1 ? "order" : "orders");
        return dateText + " — " + formatVnd(revenue) + " — " + orderText;
    }

    function setText(field, value, formatter = formatNumber) {
        const target = root.querySelector('[data-field="' + field + '"]');
        if (target) target.textContent = formatter(value);
    }

    function showError(message) {
        errorNotice.textContent = message;
        errorNotice.classList.remove("d-none");
    }

    function clearError() {
        errorNotice.textContent = "";
        errorNotice.classList.add("d-none");
    }

    function showChartNote(message, isError = false) {
        renderedBucketCount = 0;
        chart.replaceChildren();
        const note = document.createElement("div");
        note.className = "ad-chart-note" + (isError ? " is-error" : "");
        note.textContent = message;
        chart.appendChild(note);
        bucketList.replaceChildren();
    }

    function visibleTickIndexes(count, width, granularity) {
        const indexes = new Set();
        if (count <= 0) return indexes;
        indexes.add(0);
        if (count === 1) return indexes;
        indexes.add(count - 1);

        let minimumWidth = 48;
        if (granularity === "Weekly") {
            minimumWidth = 84;
        } else if (granularity === "Monthly") {
            minimumWidth = 64;
        }
        const targetCount = Math.min(count, Math.max(2, Math.floor(Math.max(width, minimumWidth * 2) / minimumWidth)));
        for (let position = 1; position < targetCount - 1; position += 1) {
            indexes.add(Math.round(position * (count - 1) / (targetCount - 1)));
        }
        return indexes;
    }

    function updateTickVisibility() {
        if (renderedBucketCount <= 0) return;
        const visible = visibleTickIndexes(renderedBucketCount, chart.clientWidth, renderedGranularity);
        chart.querySelectorAll("[data-tick-index]").forEach((tick) => {
            tick.hidden = !visible.has(Number(tick.dataset.tickIndex));
        });
    }

    function scheduleTickVisibilityUpdate() {
        if (resizeFrame) return;
        resizeFrame = window.requestAnimationFrame(() => {
            resizeFrame = 0;
            updateTickVisibility();
        });
    }

    function createTick(bucket, index, count, granularity) {
        const label = document.createElement("span");
        label.className = "ad-chart-tick";
        label.dataset.tickIndex = String(index);
        label.setAttribute("aria-hidden", "true");
        if (index === 0) label.classList.add("is-first");
        if (index === count - 1) label.classList.add("is-last");

        if (granularity === "Weekly") {
            const start = document.createElement("span");
            start.textContent = shortDate(bucket.startDate);
            const separator = document.createElement("span");
            separator.className = "ad-chart-tick-separator";
            separator.textContent = "–";
            const end = document.createElement("span");
            end.textContent = shortDate(bucket.endDate);
            label.append(start, separator, end);
        } else {
            label.textContent = tickText(bucket, granularity);
        }

        return label;
    }

    function renderBuckets(data) {
        const buckets = Array.isArray(data.revenueBuckets) ? data.revenueBuckets : [];
        bucketList.replaceChildren();
        renderedBucketCount = buckets.length;
        renderedGranularity = String(data.granularity || "Daily");

        if (!buckets.length) {
            showChartNote("No revenue buckets are available for this date range.");
            return;
        }

        const maximum = Math.max(...buckets.map((bucket) => Number(bucket.revenue || 0)), 0);
        const plot = document.createElement("div");
        plot.className = "ad-chart-plot";
        plot.style.setProperty("--bucket-count", String(buckets.length));

        buckets.forEach((bucket, index) => {
            const revenue = Number(bucket.revenue || 0);
            const orders = Number(bucket.orders || 0);
            const accessibleLabel = accessibleBucketLabel(bucket, revenue, orders);

            const column = document.createElement("div");
            column.className = "ad-chart-bar";

            const barSlot = document.createElement("div");
            barSlot.className = "ad-chart-bar-slot";

            const bar = document.createElement("button");
            bar.type = "button";
            bar.className = "bar";
            if (revenue <= 0) bar.classList.add("is-zero");
            bar.style.height = String(maximum > 0 && revenue > 0 ? Math.max(2, revenue / maximum * 100) : 0) + "%";
            bar.title = accessibleLabel;
            bar.setAttribute("aria-label", accessibleLabel);

            const label = createTick(bucket, index, buckets.length, renderedGranularity);
            const textItem = document.createElement("li");
            textItem.textContent = accessibleLabel;
            bucketList.appendChild(textItem);

            barSlot.appendChild(bar);
            column.append(barSlot, label);
            plot.appendChild(column);
        });

        chart.replaceChildren(plot);
        if (maximum === 0) {
            const empty = document.createElement("div");
            empty.className = "ad-chart-empty";
            empty.textContent = "No VNPay revenue in this date range.";
            chart.appendChild(empty);
        }
        updateTickVisibility();
    }

    function render(data) {
        const available = data?.available === true;
        unavailableNotice.classList.toggle("d-none", available);

        setText("totalCandidates", available ? data.totalCandidates : null);
        setText("newCandidatesThisMonth", available ? data.newCandidatesThisMonth : null);
        setText("totalInterviewSessions", available ? data.totalInterviewSessions : null);
        setText("interviewSessionsThisMonth", available ? data.interviewSessionsThisMonth : null);
        setText("completedInterviewSessions", available ? data.completedInterviewSessions : null);
        setText("grossVnpayRevenueThisMonth", available ? data.grossVnpayRevenueThisMonth : null, formatVnd);
        setText("totalFeedbacks", available ? data.totalFeedbacks : null);
        setText("feedbacksThisMonth", available ? data.feedbacksThisMonth : null);

        const lastUpdated = root.querySelector("[data-last-updated]");
        if (lastUpdated) {
            lastUpdated.textContent = data?.lastUpdated
                ? String(data.lastUpdated).replace("T", " ").slice(0, 16) + " ICT"
                : "—";
        }

        if (!available) {
            summary.textContent = "";
            showChartNote("Dashboard statistics are unavailable.");
            return;
        }

        summary.textContent = "Selected range: " + formatVnd(data.selectedRangeGrossVnpayRevenue) +
            " · " + formatNumber(data.selectedRangePaidVnpayOrders) + " paid VNPay orders" +
            " · " + data.granularity + " granularity" +
            " · " + data.from + " to " + data.to;
        renderBuckets(data);
    }

    function setBusy(busy) {
        applyButton.disabled = busy;
        applyButton.setAttribute("aria-busy", String(busy));
        form.setAttribute("aria-busy", String(busy));
    }

    async function loadDashboard() {
        clearError();
        if (!fromInput.value || !toInput.value) {
            showError("Both From and To dates are required.");
            return;
        }
        if (fromInput.value > toInput.value) {
            showError("The From date must be on or before the To date.");
            return;
        }
        if (inclusiveDays(fromInput.value, toInput.value) > 366) {
            showError("The selected date range must contain no more than 366 days.");
            return;
        }

        activeController?.abort();
        const controller = new AbortController();
        activeController = controller;
        const sequence = ++requestSequence;
        setBusy(true);

        try {
            const url = endpoint + "?from=" + encodeURIComponent(fromInput.value) + "&to=" + encodeURIComponent(toInput.value);
            const response = await fetch(url, {
                headers: { Accept: "application/json" },
                signal: controller.signal
            });
            const body = await response.json().catch(() => null);

            if (sequence !== requestSequence) return;
            if (!response.ok || body?.success !== true || !body.data) {
                showError(body?.message || "Could not load dashboard statistics.");
                return;
            }

            render(body.data);
        } catch (error) {
            if (error?.name === "AbortError") return;
            if (sequence === requestSequence)
                showError("Could not load dashboard statistics.");
        } finally {
            if (sequence === requestSequence) {
                setBusy(false);
                activeController = null;
            }
        }
    }

    form.addEventListener("submit", (event) => {
        event.preventDefault();
        loadDashboard();
    });

    if ("ResizeObserver" in window) {
        new ResizeObserver(scheduleTickVisibilityUpdate).observe(chart);
    } else {
        window.addEventListener("resize", scheduleTickVisibilityUpdate, { passive: true });
    }

    try {
        render(JSON.parse(initialNode.textContent));
    } catch {
        showError("Could not load dashboard statistics.");
        render({ available: false });
    }
})();