
document.addEventListener("DOMContentLoaded", function () {
    // Search rows inside a table using the search input's data-table attribute.
    document.querySelectorAll("[data-table-search]").forEach(function (input) {
        input.addEventListener("input", function () {
            const tableId = input.dataset.tableSearch;
            const table = document.getElementById(tableId);

            if (!table) return;

            const query = input.value.trim().toLowerCase();

            table.querySelectorAll("tbody tr").forEach(function (row) {
                if (row.dataset.emptyState === "true") return;

                row.hidden = !row.textContent.toLowerCase().includes(query);
            });
        });
    });

    // Close modals when the user clicks the backdrop.
    document.querySelectorAll("[data-modal-backdrop]").forEach(function (modal) {
        modal.addEventListener("click", function (event) {
            if (event.target === modal) {
                modal.hidden = true;
            }
        });
    });

    // Close an open modal when Escape is pressed.
    document.addEventListener("keydown", function (event) {
        if (event.key !== "Escape") return;

        document.querySelectorAll("[data-modal-backdrop]").forEach(function (modal) {
            modal.hidden = true;
        });
    });
});
