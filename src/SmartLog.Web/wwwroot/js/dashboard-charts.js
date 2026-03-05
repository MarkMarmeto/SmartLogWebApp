// Dashboard Charts - SmartLog
// Uses Chart.js 4.x with teal-green color scheme

const chartColors = {
    primary: '#2C7873',
    accent: '#3D9B96',
    light: '#6EC5C0',
    success: '#52C41A',
    warning: '#F59E0B',
    danger: '#EF4444',
    gridLines: '#E2E8F0',
    text: '#2C3E50',
    background: 'rgba(44, 120, 115, 0.1)',
    backgroundAccent: 'rgba(61, 155, 150, 0.1)'
};

let trendChart = null;
let gradeChart = null;
let weekdayChart = null;
let currentTrendDays = 30;

// Initialize all dashboard components
document.addEventListener('DOMContentLoaded', function () {
    loadAttendanceTrend(currentTrendDays);
    loadAttendanceByGrade();
    loadWeekdayPattern();
    loadRecentActivity();

    // Auto-refresh every 60 seconds
    setInterval(function () {
        loadRecentActivity();
    }, 60000);
});

// Trend chart time range buttons
function setTrendRange(days) {
    currentTrendDays = days;
    document.querySelectorAll('.trend-range-btn').forEach(btn => btn.classList.remove('active'));
    document.querySelector(`[data-days="${days}"]`)?.classList.add('active');
    loadAttendanceTrend(days);
}

// Attendance Trend Line Chart
async function loadAttendanceTrend(days) {
    try {
        const response = await fetch(`/api/v1/dashboard/attendance-trend?days=${days}`);
        const data = await response.json();

        const ctx = document.getElementById('trendChart');
        if (!ctx) return;

        const labels = data.map(d => {
            const date = new Date(d.date);
            return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        });
        const rates = data.map(d => d.attendanceRate);

        if (trendChart) {
            trendChart.destroy();
        }

        trendChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Attendance Rate (%)',
                    data: rates,
                    borderColor: chartColors.primary,
                    backgroundColor: chartColors.background,
                    fill: true,
                    tension: 0.3,
                    pointRadius: days <= 7 ? 5 : days <= 30 ? 3 : 1,
                    pointHoverRadius: 6,
                    pointBackgroundColor: chartColors.primary,
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const idx = context.dataIndex;
                                const point = data[idx];
                                return [
                                    `Attendance: ${point.attendanceRate}%`,
                                    `Present: ${point.presentCount} / ${point.totalEnrolled}`
                                ];
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        max: 100,
                        ticks: {
                            callback: value => value + '%',
                            color: chartColors.text
                        },
                        grid: { color: chartColors.gridLines }
                    },
                    x: {
                        ticks: { color: chartColors.text, maxTicksLimit: 10 },
                        grid: { display: false }
                    }
                }
            }
        });
    } catch (error) {
        console.error('Failed to load attendance trend:', error);
    }
}

// Attendance by Grade Bar Chart
async function loadAttendanceByGrade() {
    try {
        const response = await fetch('/api/v1/dashboard/attendance-by-grade');
        const data = await response.json();

        const ctx = document.getElementById('gradeChart');
        if (!ctx) return;

        const labels = data.map(d => d.gradeName);
        const rates = data.map(d => d.attendanceRate);
        const colors = rates.map(r => {
            if (r >= 90) return chartColors.success;
            if (r >= 80) return chartColors.warning;
            return chartColors.danger;
        });

        if (gradeChart) {
            gradeChart.destroy();
        }

        gradeChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Attendance Rate (%)',
                    data: rates,
                    backgroundColor: colors.map(c => c + '80'),
                    borderColor: colors,
                    borderWidth: 1,
                    borderRadius: 4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const idx = context.dataIndex;
                                const point = data[idx];
                                return [
                                    `Rate: ${point.attendanceRate}%`,
                                    `Present: ${point.presentCount} / ${point.totalEnrolled}`
                                ];
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        max: 100,
                        ticks: {
                            callback: value => value + '%',
                            color: chartColors.text
                        },
                        grid: { color: chartColors.gridLines }
                    },
                    x: {
                        ticks: { color: chartColors.text },
                        grid: { display: false }
                    }
                }
            }
        });
    } catch (error) {
        console.error('Failed to load attendance by grade:', error);
    }
}

// Weekly Attendance Pattern
async function loadWeekdayPattern() {
    try {
        const response = await fetch('/api/v1/dashboard/attendance-by-weekday?weeks=4');
        const data = await response.json();

        const ctx = document.getElementById('weekdayChart');
        if (!ctx) return;

        const labels = data.map(d => d.dayOfWeek.substring(0, 3));

        if (weekdayChart) {
            weekdayChart.destroy();
        }

        weekdayChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'This Week',
                        data: data.map(d => d.currentWeekRate),
                        backgroundColor: chartColors.primary + 'B3',
                        borderColor: chartColors.primary,
                        borderWidth: 1,
                        borderRadius: 4
                    },
                    {
                        label: '4-Week Average',
                        data: data.map(d => d.averageRate),
                        backgroundColor: chartColors.light + '80',
                        borderColor: chartColors.light,
                        borderWidth: 1,
                        borderRadius: 4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top',
                        labels: { boxWidth: 12, padding: 15 }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return `${context.dataset.label}: ${context.parsed.y}%`;
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        max: 100,
                        ticks: {
                            callback: value => value + '%',
                            color: chartColors.text
                        },
                        grid: { color: chartColors.gridLines }
                    },
                    x: {
                        ticks: { color: chartColors.text },
                        grid: { display: false }
                    }
                }
            }
        });
    } catch (error) {
        console.error('Failed to load weekday pattern:', error);
    }
}

// Recent Activity Feed
async function loadRecentActivity() {
    try {
        const response = await fetch('/api/v1/dashboard/recent-activity?count=10');
        const data = await response.json();

        const container = document.getElementById('activityFeed');
        if (!container) return;

        if (data.length === 0) {
            container.innerHTML = '<p class="text-muted text-center py-3">No recent activity</p>';
            return;
        }

        let html = '<div class="list-group list-group-flush">';
        data.forEach(activity => {
            const time = new Date(activity.timestamp);
            const timeStr = time.toLocaleString('en-US', {
                month: 'short', day: 'numeric',
                hour: '2-digit', minute: '2-digit'
            });

            const icon = getActionIcon(activity.action);
            const linkStart = activity.linkUrl ? `<a href="${activity.linkUrl}" class="list-group-item list-group-item-action py-2">` : '<div class="list-group-item py-2">';
            const linkEnd = activity.linkUrl ? '</a>' : '</div>';

            html += `${linkStart}
                <div class="d-flex justify-content-between align-items-start">
                    <div>
                        <i class="bi ${icon} me-2 text-muted"></i>
                        <small class="fw-semibold">${escapeHtml(activity.action)}</small>
                        <br>
                        <small class="text-muted">${escapeHtml(activity.details)}</small>
                    </div>
                    <div class="text-end">
                        <small class="text-muted">${timeStr}</small>
                        <br>
                        <small class="text-muted">${escapeHtml(activity.userName)}</small>
                    </div>
                </div>
            ${linkEnd}`;
        });
        html += '</div>';
        container.innerHTML = html;
    } catch (error) {
        console.error('Failed to load recent activity:', error);
    }
}

function getActionIcon(action) {
    const icons = {
        'LoginSuccess': 'bi-box-arrow-in-right',
        'LoginFailed': 'bi-x-circle',
        'AccountLocked': 'bi-lock',
        'AccountUnlocked': 'bi-unlock',
        'UserCreated': 'bi-person-plus',
        'UserDeactivated': 'bi-person-x',
        'UserReactivated': 'bi-person-check',
        'PasswordChanged': 'bi-key',
        'PasswordReset': 'bi-key-fill',
        'StudentCreated': 'bi-mortarboard',
        'BulkStudentImport': 'bi-cloud-upload',
        'CreateFaculty': 'bi-person-badge',
        'BulkFacultyImport': 'bi-cloud-upload'
    };
    return icons[action] || 'bi-activity';
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text || '';
    return div.innerHTML;
}
