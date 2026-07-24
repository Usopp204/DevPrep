(function (global) {
    "use strict";

    const windowSize = 5;

    function getWindow(currentPage, totalPages) {
        const safeTotal = Math.max(1, Number(totalPages) || 1);
        const safeCurrent = Math.min(safeTotal, Math.max(1, Number(currentPage) || 1));
        let start = Math.max(1, safeCurrent - Math.floor(windowSize / 2));
        const end = Math.min(safeTotal, start + windowSize - 1);
        start = Math.max(1, end - windowSize + 1);

        return {
            currentPage: safeCurrent,
            totalPages: safeTotal,
            pages: Array.from({ length: end - start + 1 }, (_, index) => start + index),
            firstPage: start > 1 ? 1 : null,
            hasLeadingEllipsis: start > 2,
            hasTrailingEllipsis: end < safeTotal - 1,
            lastPage: end < safeTotal ? safeTotal : null
        };
    }

    function render(options) {
        const container = typeof options.container === "string"
            ? document.getElementById(options.container)
            : options.container;
        if (!container) return;

        const pagination = getWindow(options.currentPage, options.totalPages);
        container.replaceChildren();

        appendDirection(container, "Previous", pagination.currentPage - 1, pagination.currentPage <= 1, options.onPageChange);
        if (pagination.firstPage !== null) appendPage(container, pagination.firstPage, pagination, options.onPageChange);
        if (pagination.hasLeadingEllipsis) appendEllipsis(container);
        pagination.pages.forEach(page => appendPage(container, page, pagination, options.onPageChange));
        if (pagination.hasTrailingEllipsis) appendEllipsis(container);
        if (pagination.lastPage !== null) appendPage(container, pagination.lastPage, pagination, options.onPageChange);
        appendDirection(container, "Next", pagination.currentPage + 1, pagination.currentPage >= pagination.totalPages, options.onPageChange);
    }

    function appendDirection(container, label, targetPage, disabled, onPageChange) {
        const control = document.createElement("button");
        control.type = "button";
        control.className = `devprep-page-link devprep-page-direction${disabled ? " disabled" : ""}`;
        control.textContent = label;
        control.disabled = disabled;
        control.setAttribute("aria-label", `Go to ${label.toLowerCase()} page`);
        if (disabled) control.setAttribute("aria-disabled", "true");
        else control.addEventListener("click", () => onPageChange(targetPage));
        container.append(control);
    }

    function appendPage(container, page, pagination, onPageChange) {
        const control = document.createElement("button");
        const isCurrent = page === pagination.currentPage;
        control.type = "button";
        control.className = `devprep-page-link${isCurrent ? " active" : ""}`;
        control.textContent = String(page);
        control.setAttribute("aria-label", `Go to page ${page}`);
        if (isCurrent) control.setAttribute("aria-current", "page");
        else control.addEventListener("click", () => onPageChange(page));
        container.append(control);
    }

    function appendEllipsis(container) {
        const ellipsis = document.createElement("span");
        ellipsis.className = "devprep-page-ellipsis";
        ellipsis.textContent = "…";
        ellipsis.setAttribute("aria-hidden", "true");
        container.append(ellipsis);
    }

    global.DevPrepPagination = { getWindow, render };
})(window);
