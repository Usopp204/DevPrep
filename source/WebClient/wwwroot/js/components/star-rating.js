(function () {
    "use strict";

    function initialize(group) {
        if (group.dataset.starInitialized === "true") {
            return;
        }

        const options = Array.from(group.querySelectorAll("[data-star-value]"));
        const inputs = options.map(function (option) {
            return option.querySelector("input[type='radio']");
        }).filter(Boolean);

        function selectedValue() {
            return Number(inputs.find(function (input) { return input.checked; })?.value || 0);
        }

        function render(value) {
            options.forEach(function (option) {
                const filled = Number(option.dataset.starValue) <= value;
                const icon = option.querySelector("[data-star-icon]");
                if (icon) {
                    icon.textContent = filled ? "star" : "star_border";
                    icon.classList.toggle("is-filled", filled);
                    icon.classList.toggle("is-empty", !filled);
                }
            });
        }

        options.forEach(function (option) {
            option.addEventListener("mouseenter", function () {
                render(Number(option.dataset.starValue));
            });
        });
        group.addEventListener("mouseleave", function () {
            render(selectedValue());
        });
        inputs.forEach(function (input) {
            input.addEventListener("change", function () {
                group.dataset.starSelected = input.value;
                render(Number(input.value));
            });
            input.addEventListener("focus", function () {
                render(Number(input.value));
            });
            input.addEventListener("blur", function () {
                render(selectedValue());
            });
        });

        render(selectedValue());
        group.dataset.starInitialized = "true";
    }

    document.querySelectorAll("[data-star-rating][data-star-editable='true']").forEach(initialize);
    window.DevPrepStarRating = { initialize: initialize };
}());
