// Основные скрипты для сайта
document.addEventListener('DOMContentLoaded', function() {
    // Обработка кнопки "Назад"
    document.querySelectorAll('[onclick="history.back()"]').forEach(button => {
        button.addEventListener('click', function() {
            history.back();
        });
    });

    // Подтверждение удаления
    document.querySelectorAll('form[method="post"][onsubmit]').forEach(form => {
        form.addEventListener('submit', function(e) {
            if (!confirm('Вы уверены, что хотите выполнить это действие?')) {
                e.preventDefault();
            }
        });
    });

    // Инициализация тултипов
    if (window.bootstrap && bootstrap.Tooltip) {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
});