// Program-first broadcast targeting component (US0084).
// Manages checkbox state, serialises filter to #targeting-json, and triggers recipient count updates.
(function () {
    'use strict';

    function init() {
        var selectAll = document.getElementById('select-all-programs');
        if (!selectAll) return;

        // Seed initial targeting JSON from current checked state
        serializeAndUpdate();

        selectAll.addEventListener('change', function () {
            var checked = this.checked;
            document.querySelectorAll('.program-cb:not([disabled])').forEach(function (cb) {
                cb.checked = checked;
                cb.indeterminate = false;
            });
            document.querySelectorAll('.grade-cb').forEach(function (cb) {
                cb.checked = checked;
            });
            serializeAndUpdate();
        });

        document.querySelectorAll('.program-cb').forEach(function (cb) {
            cb.addEventListener('change', function () {
                var code = this.dataset.programCode;
                var checked = this.checked;
                // Set all grades under this program
                document.querySelectorAll('.grade-cb[data-program-code="' + code + '"]').forEach(function (g) {
                    g.checked = checked;
                });
                this.indeterminate = false;
                updateSelectAllState();
                serializeAndUpdate();
            });
        });

        document.querySelectorAll('.grade-cb').forEach(function (cb) {
            cb.addEventListener('change', function () {
                var progCode = this.dataset.programCode;
                updateProgramIndeterminate(progCode);
                updateSelectAllState();
                serializeAndUpdate();
            });
        });
    }

    function updateProgramIndeterminate(programCode) {
        var progCb = document.querySelector('.program-cb[data-program-code="' + programCode + '"]');
        if (!progCb) return;
        var grades = document.querySelectorAll('.grade-cb[data-program-code="' + programCode + '"]');
        var checkedCount = 0;
        grades.forEach(function (g) { if (g.checked) checkedCount++; });
        if (checkedCount === 0) {
            progCb.checked = false;
            progCb.indeterminate = false;
        } else if (checkedCount === grades.length) {
            progCb.checked = true;
            progCb.indeterminate = false;
        } else {
            progCb.checked = false;
            progCb.indeterminate = true;
        }
    }

    function updateSelectAllState() {
        var selectAll = document.getElementById('select-all-programs');
        if (!selectAll) return;
        var allCbs = document.querySelectorAll('.program-cb:not([disabled])');
        var checkedCount = 0;
        var indetermCount = 0;
        allCbs.forEach(function (cb) {
            if (cb.indeterminate) indetermCount++;
            else if (cb.checked) checkedCount++;
        });
        var total = allCbs.length;
        if (checkedCount === total && indetermCount === 0) {
            selectAll.checked = true;
            selectAll.indeterminate = false;
        } else if (checkedCount === 0 && indetermCount === 0) {
            selectAll.checked = false;
            selectAll.indeterminate = false;
        } else {
            selectAll.checked = false;
            selectAll.indeterminate = true;
        }
    }

    function buildFilters() {
        var filters = [];
        document.querySelectorAll('.program-cb:not([disabled])').forEach(function (progCb) {
            var code = progCb.dataset.programCode;
            // Collect checked grades (if indeterminate or checked, iterate grades)
            var gradeCodes = [];
            document.querySelectorAll('.grade-cb[data-program-code="' + code + '"]:checked').forEach(function (g) {
                gradeCodes.push(g.dataset.gradeCode);
            });
            if (gradeCodes.length > 0) {
                filters.push({ programCode: code, gradeLevelCodes: gradeCodes });
            }
        });
        return filters;
    }

    function serializeAndUpdate() {
        var filters = buildFilters();
        var json = filters.length > 0 ? JSON.stringify(filters) : '[]';
        var hidden = document.getElementById('targeting-json');
        if (hidden) hidden.value = json;
        if (typeof window.onTargetingChanged === 'function') {
            window.onTargetingChanged(json);
        }
    }

    // Expose for pages to call after DOM is ready
    window.initBroadcastTargeting = init;
    window.getBroadcastTargetingJson = function () {
        return document.getElementById('targeting-json')
            ? document.getElementById('targeting-json').value
            : '[]';
    };
})();
