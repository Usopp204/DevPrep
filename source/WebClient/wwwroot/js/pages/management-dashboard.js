(() => {
    "use strict";

    const root = document.querySelector("[data-management-dashboard]");
    if (!root || root.dataset.initialized === "true") return;
    root.dataset.initialized = "true";

    const form = root.querySelector("[data-dashboard-form]");
    const fromInput = root.querySelector("#dashboard-from");
    const toInput = root.querySelector("#dashboard-to");
    const applyButton = root.querySelector("[data-apply-button]");
    const presetButtons = [...root.querySelectorAll("[data-dashboard-preset]")];
    const trendButtons = [...root.querySelectorAll("[data-trend-tab]")];
    const chart = root.querySelector("[data-dashboard-chart]");
    const bucketList = root.querySelector("[data-bucket-list]");
    const summary = root.querySelector("[data-trend-summary]");
    const legend = root.querySelector("[data-trend-legend]");
    const unavailableNotice = root.querySelector("[data-dashboard-unavailable]");
    const errorNotice = root.querySelector("[data-dashboard-error]");
    const initialNode = root.querySelector("[data-dashboard-initial]");
    if (!form || !fromInput || !toInput || !applyButton || !chart || !bucketList ||
        !summary || !legend || !unavailableNotice || !errorNotice || !initialNode) return;

    const endpoint = root.dataset.dashboardUrl;
    const number = new Intl.NumberFormat("en-US");
    let activeController = null;
    let requestSequence = 0;
    let renderedBucketCount = 0;
    let renderedGranularity = "Daily";
    let resizeFrame = 0;
    let activeTrend = "revenue";
    let dashboardData = null;

    const trends = {
        revenue: {
            label: "Revenue",
            empty: "No VNPay revenue in this date range.",
            series: [
                { key: "grossVnpayRevenue", label: "Gross revenue", tone: "revenue", format: formatVnd }
            ],
            summary: (data) => formatVnd(data.periodGrossVnpayRevenue) + " · " +
                formatNumber(data.periodPaidVnpayOrders) + " paid VNPay orders"
        },
        candidates: {
            label: "Candidates",
            empty: "No new candidates in this date range.",
            series: [
                { key: "newCandidates", label: "New candidates", tone: "candidates", format: formatNumber }
            ],
            summary: (data) => formatNumber(data.newCandidates) + " new candidates"
        },
        interviews: {
            label: "Interviews",
            empty: "No interview activity in this date range.",
            series: [
                { key: "interviewSessionsCreated", label: "Created", tone: "interviews-created", format: formatNumber },
                { key: "interviewSessionsCompleted", label: "Completed", tone: "interviews-completed", format: formatNumber }
            ],
            summary: (data) => formatNumber(data.interviewSessionsCreated) + " created · " +
                formatNumber(data.interviewSessionsCompleted) + " completed"
        },
        feedbacks: {
            label: "Feedbacks",
            empty: "No new feedbacks in this date range.",
            series: [
                { key: "systemFeedbacks", label: "System", tone: "feedback-system", format: formatNumber },
                { key: "interviewFeedbacks", label: "Interview", tone: "feedback-interview", format: formatNumber }
            ],
            summary: (data) => formatNumber(data.newFeedbacks) + " total · " +
                formatNumber(data.newSystemFeedbacks) + " system · " +
                formatNumber(data.newInterviewFeedbacks) + " interview"
        }
    };

    function inclusiveDays(from, to) {
        const start = Date.parse(from + "T00:00:00Z");
        const end = Date.parse(to + "T00:00:00Z");
        return Math.floor((end - start) / 86400000) + 1;
    }

    function granularityForRange(from, to) {
        const days = inclusiveDays(from, to);
        if (days <= 31) return "Daily";
        if (days <= 180) return "Weekly";
        return "Monthly";
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

    function parseIsoDate(value) {
        const parts = dateParts(value);
        return parts ? new Date(Date.UTC(Number(parts.year), Number(parts.month) - 1, Number(parts.day))) : null;
    }

    function toIsoDate(value) {
        return value.getUTCFullYear() + "-" +
            String(value.getUTCMonth() + 1).padStart(2, "0") + "-" +
            String(value.getUTCDate()).padStart(2, "0");
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

    function bucketDateLabel(bucket) {
        const start = fullDate(bucket.startDate);
        const end = fullDate(bucket.endDate);
        return start === end ? start : start + " to " + end;
    }

    function setText(field, value, formatter = formatNumber) {
        root.querySelectorAll('[data-field="' + field + '"]').forEach((target) => {
            target.textContent = formatter(value);
        });
    }

    function setDateText(selector, value) {
        root.querySelectorAll(selector).forEach((target) => {
            target.textContent = value || "—";
        });
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
        if (granularity === "Weekly") minimumWidth = 84;
        if (granularity === "Monthly") minimumWidth = 64;
        const targetCount = Math.min(count, Math.max(2, Math.floor(Math.max(width, minimumWidth * 2) / minimumWidth)));
        for (let position = 1; position < targetCount - 1; position += 1)
            indexes.add(Math.round(position * (count - 1) / (targetCount - 1)));
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
        label.textContent = tickText(bucket, granularity);
        return label;
    }

    function renderLegend(configuration) {
        legend.replaceChildren();
        configuration.series.forEach((series) => {
            const item = document.createElement("span");
            item.className = "ad-legend-item";
            const swatch = document.createElement("span");
            swatch.className = "ad-legend-swatch is-" + series.tone;
            swatch.setAttribute("aria-hidden", "true");
            item.append(swatch, document.createTextNode(series.label));
            legend.appendChild(item);
        });
    }

    function accessibleBucketLabel(bucket, configuration) {
        const values = configuration.series.map((series) =>
            series.label + ": " + series.format(bucket[series.key]));
        if (activeTrend === "revenue")
            values.push("Paid VNPay orders: " + formatNumber(bucket.paidVnpayOrders));
        return bucketDateLabel(bucket) + " — " + values.join(" — ");
    }

    function renderBuckets(data) {
        const buckets = Array.isArray(data.buckets) ? data.buckets : [];
        const configuration = trends[activeTrend];
        bucketList.replaceChildren();
        renderedBucketCount = buckets.length;
        renderedGranularity = String(data.granularity || "Daily");
        renderLegend(configuration);
        summary.textContent = configuration.summary(data);
        chart.setAttribute("aria-label", configuration.label + " trend for the selected date range");

        if (!buckets.length) {
            showChartNote("No trend buckets are available for this date range.");
            return;
        }

        const maximum = Math.max(
            ...buckets.flatMap((bucket) => configuration.series.map((series) => Number(bucket[series.key] || 0))),
            0);
        const plot = document.createElement("div");
        plot.className = "ad-chart-plot";
        plot.style.setProperty("--bucket-count", String(buckets.length));

        buckets.forEach((bucket, index) => {
            const accessibleLabel = accessibleBucketLabel(bucket, configuration);
            const column = document.createElement("div");
            column.className = "ad-chart-bucket";

            const barSlot = document.createElement("div");
            barSlot.className = "ad-chart-bar-slot";
            configuration.series.forEach((series) => {
                const value = Number(bucket[series.key] || 0);
                const bar = document.createElement("button");
                bar.type = "button";
                bar.className = "bar is-" + series.tone;
                if (value <= 0) bar.classList.add("is-zero");
                bar.style.height = String(maximum > 0 && value > 0 ? Math.max(2, value / maximum * 100) : 0) + "%";
                bar.title = bucketDateLabel(bucket) + " — " + series.label + ": " + series.format(value);
                bar.setAttribute("aria-label", bar.title);
                barSlot.appendChild(bar);
            });

            const tick = createTick(bucket, index, buckets.length, renderedGranularity);
            const textItem = document.createElement("li");
            textItem.textContent = accessibleLabel;
            bucketList.appendChild(textItem);
            column.append(barSlot, tick);
            plot.appendChild(column);
        });

        chart.replaceChildren(plot);
        if (maximum === 0) {
            const empty = document.createElement("div");
            empty.className = "ad-chart-empty";
            empty.textContent = configuration.empty;
            chart.appendChild(empty);
        }
        updateTickVisibility();
    }

    function render(data) {
        dashboardData = data;
        const available = data?.available === true;
        unavailableNotice.classList.toggle("d-none", available);

        setText("totalCandidatesAsOf", available ? data.totalCandidatesAsOf : null);
        setText("newCandidates", available ? data.newCandidates : null);
        setText("totalInterviewSessionsAsOf", available ? data.totalInterviewSessionsAsOf : null);
        setText("completedInterviewSessionsAsOf", available ? data.completedInterviewSessionsAsOf : null);
        setText("interviewSessionsCreated", available ? data.interviewSessionsCreated : null);
        setText("interviewSessionsCompleted", available ? data.interviewSessionsCompleted : null);
        setText("totalSystemFeedbacksAsOf", available ? data.totalSystemFeedbacksAsOf : null);
        setText("totalInterviewFeedbacksAsOf", available ? data.totalInterviewFeedbacksAsOf : null);
        setText("totalFeedbacksAsOf", available ? data.totalFeedbacksAsOf : null);
        setText("newSystemFeedbacks", available ? data.newSystemFeedbacks : null);
        setText("newInterviewFeedbacks", available ? data.newInterviewFeedbacks : null);
        setText("newFeedbacks", available ? data.newFeedbacks : null);
        setText("grossVnpayRevenueAsOf", available ? data.grossVnpayRevenueAsOf : null, formatVnd);
        setText("paidVnpayOrdersAsOf", available ? data.paidVnpayOrdersAsOf : null);
        setText("periodGrossVnpayRevenue", available ? data.periodGrossVnpayRevenue : null, formatVnd);
        setText("periodPaidVnpayOrders", available ? data.periodPaidVnpayOrders : null);

        setDateText("[data-snapshot-to]", data?.to);
        setDateText("[data-period-from], [data-trend-from]", data?.from);
        setDateText("[data-period-to], [data-trend-to]", data?.to);
        setDateText("[data-granularity]", data?.granularity);

        const lastUpdated = root.querySelector("[data-last-updated]");
        if (lastUpdated) {
            lastUpdated.textContent = data?.lastUpdated
                ? String(data.lastUpdated).replace("T", " ").slice(0, 16) + " ICT"
                : "—";
        }

        if (!available) {
            summary.textContent = "";
            legend.replaceChildren();
            showChartNote("Dashboard statistics are unavailable.");
            return;
        }

        renderBuckets(data);
    }

    function selectTrend(name, focus = false) {
        if (!trends[name]) return;
        activeTrend = name;
        trendButtons.forEach((button) => {
            const selected = button.dataset.trendTab === name;
            button.setAttribute("aria-selected", String(selected));
            button.tabIndex = selected ? 0 : -1;
            if (selected && focus) button.focus();
        });
        if (dashboardData?.available === true) renderBuckets(dashboardData);
    }

    function setBusy(busy) {
        applyButton.disabled = busy;
        applyButton.setAttribute("aria-busy", String(busy));
        form.setAttribute("aria-busy", String(busy));
        presetButtons.forEach((button) => {
            button.disabled = busy;
        });
    }

    function unavailableData() {
        return {
            available: false,
            from: fromInput.value,
            to: toInput.value,
            granularity: granularityForRange(fromInput.value, toInput.value)
        };
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
                render(unavailableData());
                return;
            }

            render(body.data);
        } catch (error) {
            if (error?.name === "AbortError") return;
            if (sequence === requestSequence) {
                showError("Could not load dashboard statistics.");
                render(unavailableData());
            }
        } finally {
            if (sequence === requestSequence) {
                setBusy(false);
                activeController = null;
            }
        }
    }

    function applyPreset(name) {
        const today = parseIsoDate(toInput.max);
        if (!today) return;

        let from;
        let to = new Date(today);
        if (name === "this-month") {
            from = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1));
        } else if (name === "last-month") {
            from = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth() - 1, 1));
            to = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 0));
        } else if (name === "last-30-days") {
            from = new Date(today);
            from.setUTCDate(from.getUTCDate() - 29);
        } else {
            return;
        }

        fromInput.value = toIsoDate(from);
        toInput.value = toIsoDate(to);
        loadDashboard();
    }

    form.addEventListener("submit", (event) => {
        event.preventDefault();
        loadDashboard();
    });

    presetButtons.forEach((button) => {
        button.addEventListener("click", () => applyPreset(button.dataset.dashboardPreset));
    });

    trendButtons.forEach((button, index) => {
        button.addEventListener("click", () => selectTrend(button.dataset.trendTab));
        button.addEventListener("keydown", (event) => {
            let nextIndex = null;
            if (event.key === "ArrowRight") nextIndex = (index + 1) % trendButtons.length;
            if (event.key === "ArrowLeft") nextIndex = (index - 1 + trendButtons.length) % trendButtons.length;
            if (event.key === "Home") nextIndex = 0;
            if (event.key === "End") nextIndex = trendButtons.length - 1;
            if (nextIndex === null) return;
            event.preventDefault();
            selectTrend(trendButtons[nextIndex].dataset.trendTab, true);
        });
    });

    if ("ResizeObserver" in window) {
        new ResizeObserver(scheduleTickVisibilityUpdate).observe(chart);
    } else {
        window.addEventListener("resize", scheduleTickVisibilityUpdate, { passive: true });
    }

    try {
        selectTrend("revenue");
        render(JSON.parse(initialNode.textContent));
    } catch {
        showError("Could not load dashboard statistics.");
        render({ available: false, from: fromInput.value, to: toInput.value, granularity: "Daily" });
    }
})();
