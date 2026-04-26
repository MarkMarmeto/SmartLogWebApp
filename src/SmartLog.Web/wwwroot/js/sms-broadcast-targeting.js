// Program-first broadcast targeting component (US0084, extended US0107).
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
            // US0107: also flip NG group
            var ngParent = document.querySelector('.nongraded-cb');
            if (ngParent) {
                ngParent.checked = checked;
                ngParent.indeterminate = false;
                document.querySelectorAll('.nongraded-section-cb').forEach(function (cb) {
                    cb.checked = checked;
                });
            }
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

        // US0107: NG parent toggle
        var ngParent = document.querySelector('.nongraded-cb');
        if (ngParent) {
            ngParent.addEventListener('change', function () {
                var checked = this.checked;
                document.querySelectorAll('.nongraded-section-cb').forEach(function (cb) {
                    cb.checked = checked;
                });
                this.indeterminate = false;
                updateSelectAllState();
                serializeAndUpdate();
            });
        }

        // US0107: NG section checkboxes
        document.querySelectorAll('.nongraded-section-cb').forEach(function (cb) {
            cb.addEventListener('change', function () {
                updateNonGradedIndeterminate();
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

    function updateNonGradedIndeterminate() {
        var ngParent = document.querySelector('.nongraded-cb');
        if (!ngParent) return;
        var sections = document.querySelectorAll('.nongraded-section-cb');
        var checkedCount = 0;
        sections.forEach(function (cb) { if (cb.checked) checkedCount++; });
        if (checkedCount === 0) {
            ngParent.checked = false;
            ngParent.indeterminate = false;
        } else if (checkedCount === sections.length) {
            ngParent.checked = true;
            ngParent.indeterminate = false;
        } else {
            ngParent.checked = false;
            ngParent.indeterminate = true;
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

        // US0107: include NG group in "all" assessment
        var ngParent = document.querySelector('.nongraded-cb');
        var ngContributes = false;
        if (ngParent) {
            total++;
            if (ngParent.indeterminate) indetermCount++;
            else if (ngParent.checked) { checkedCount++; ngContributes = true; }
        }

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
            var gradeCodes = [];
            document.querySelectorAll('.grade-cb[data-program-code="' + code + '"]:checked').forEach(function (g) {
                gradeCodes.push(g.dataset.gradeCode);
            });
            if (gradeCodes.length > 0) {
                filters.push({ programCode: code, gradeLevelCodes: gradeCodes });
            }
        });

        // US0107: NG branch — emit entry with empty programCode + sectionNames
        var ngParent = document.querySelector('.nongraded-cb');
        if (ngParent) {
            var sectionNames = [];
            document.querySelectorAll('.nongraded-section-cb:checked').forEach(function (s) {
                sectionNames.push(s.dataset.sectionName);
            });
            if (sectionNames.length > 0) {
                filters.push({ programCode: '', gradeLevelCodes: [], sectionNames: sectionNames });
            }
        }

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
