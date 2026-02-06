/**
 * Lightweight table sorting for all tables with class "table-sortable".
 * Click a <th> header to sort ascending; click again to sort descending.
 * Handles text, numbers, currency ($), dates, and percentages.
 */
(function () {
    'use strict';

    function parseSortValue(text) {
        if (!text) return '';
        text = text.trim();
        // Currency: $1,234.56
        if (/^\$/.test(text)) {
            return parseFloat(text.replace(/[$,]/g, '')) || 0;
        }
        // Percentage: 85%
        if (/%$/.test(text)) {
            return parseFloat(text.replace('%', '')) || 0;
        }
        // Date: yyyy-MM-dd or MM/dd/yyyy
        if (/^\d{4}-\d{2}-\d{2}/.test(text) || /^\d{1,2}\/\d{1,2}\/\d{4}/.test(text)) {
            var d = new Date(text);
            return isNaN(d.getTime()) ? text.toLowerCase() : d.getTime();
        }
        // Date: Mon DD, YYYY or similar
        if (/^[A-Za-z]{3}\s+\d/.test(text)) {
            var d2 = new Date(text);
            if (!isNaN(d2.getTime())) return d2.getTime();
        }
        // Numeric
        var num = parseFloat(text.replace(/,/g, ''));
        if (!isNaN(num) && /^-?[\d,]+\.?\d*$/.test(text.replace(/,/g, ''))) {
            return num;
        }
        return text.toLowerCase();
    }

    function initSortable(table) {
        var headers = table.querySelectorAll('thead th');
        if (!headers.length) return;

        headers.forEach(function (th, colIdx) {
            // Skip columns marked as no-sort
            if (th.classList.contains('no-sort')) return;
            // Skip checkbox columns (contains only an input)
            if (th.querySelector('input[type="checkbox"]')) return;
            // Skip empty headers (action columns)
            if (!th.textContent.trim()) return;

            th.style.cursor = 'pointer';
            th.style.userSelect = 'none';
            th.style.whiteSpace = 'nowrap';
            th.setAttribute('title', 'Click to sort');

            // Add sort icon
            var icon = document.createElement('i');
            icon.className = 'fa fa-sort ms-1 text-muted';
            icon.style.fontSize = '0.7rem';
            icon.style.opacity = '0.5';
            th.appendChild(icon);

            th.addEventListener('click', function () {
                var tbody = table.querySelector('tbody');
                if (!tbody) return;

                var rows = Array.from(tbody.querySelectorAll('tr'));
                var isAsc = th.dataset.sortDir === 'asc';
                var newDir = isAsc ? 'desc' : 'asc';

                // Reset all headers in this table
                headers.forEach(function (h) {
                    h.dataset.sortDir = '';
                    var ic = h.querySelector('.fa-sort, .fa-sort-up, .fa-sort-down');
                    if (ic) {
                        ic.className = 'fa fa-sort ms-1 text-muted';
                        ic.style.opacity = '0.5';
                    }
                });

                th.dataset.sortDir = newDir;
                icon.className = newDir === 'asc' ? 'fa fa-sort-up ms-1 text-primary' : 'fa fa-sort-down ms-1 text-primary';
                icon.style.opacity = '1';

                rows.sort(function (a, b) {
                    var aCell = a.cells[colIdx];
                    var bCell = b.cells[colIdx];
                    if (!aCell || !bCell) return 0;

                    var aVal = parseSortValue(aCell.textContent);
                    var bVal = parseSortValue(bCell.textContent);

                    var result;
                    if (typeof aVal === 'number' && typeof bVal === 'number') {
                        result = aVal - bVal;
                    } else {
                        result = String(aVal).localeCompare(String(bVal), undefined, { numeric: true });
                    }

                    return newDir === 'asc' ? result : -result;
                });

                // Re-append sorted rows
                rows.forEach(function (row) {
                    tbody.appendChild(row);
                });
            });
        });
    }

    // Initialize on DOM ready
    function init() {
        var tables = document.querySelectorAll('table.table-sortable');
        tables.forEach(initSortable);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
