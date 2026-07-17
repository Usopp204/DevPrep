(function () {
    "use strict";

    window.TechStackLiveList?.initialize({
        listPath: "/Manager/TechStacks",
        resultsPath: "/Manager/TechStacks/Results",
        forbiddenMessage: "You do not have permission to view Manager Tech Stacks.",
        errorMessage: "Tech Stack results could not be loaded. Please try again."
    });
})();
