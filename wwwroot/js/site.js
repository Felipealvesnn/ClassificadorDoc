// JavaScript para funcionalidades do layout moderno

document.addEventListener('DOMContentLoaded', function () {
    // Configurar tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Auto-hide de alertas após 5 segundos
    const alerts = document.querySelectorAll('.alert:not(.alert-permanent)');
    alerts.forEach(function (alert) {
        setTimeout(function () {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 5000);
    });

    // Ativar link da página atual
    setActiveNavLink();

    // Adicionar efeitos hover nos cards
    addCardHoverEffects();
});

function setActiveNavLink() {
    const currentPath = window.location.pathname;
    const navLinks = document.querySelectorAll('.navbar-nav .nav-link');

    navLinks.forEach(link => {
        const href = link.getAttribute('href');
        if (href && currentPath.includes(href) && href !== '/') {
            link.classList.add('active');
        }
    });
}

function addCardHoverEffects() {
    const cards = document.querySelectorAll('.card');
    cards.forEach(card => {
        card.addEventListener('mouseenter', function () {
            this.style.transform = 'translateY(-2px)';
            this.style.boxShadow = '0 8px 25px rgba(0,0,0,0.15)';
            this.style.transition = 'all 0.3s ease';
        });

        card.addEventListener('mouseleave', function () {
            this.style.transform = 'translateY(0)';
            this.style.boxShadow = '';
        });
    });
}

function showSettings() {
    // Placeholder para configurações do usuário
    alert('Configurações do usuário - Em desenvolvimento');
}

// Função para mostrar notificação moderna
function showModernNotification(message, type = 'info', duration = 4000) {
    const notificationContainer = getOrCreateNotificationContainer();

    const notification = document.createElement('div');
    notification.className = `alert alert-${type} alert-dismissible fade show modern-notification`;
    notification.innerHTML = `
        <i class="fas fa-${getIconForType(type)} me-2"></i>
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;

    notificationContainer.appendChild(notification);

    // Auto-remove após duração especificada
    setTimeout(() => {
        const bsAlert = new bootstrap.Alert(notification);
        bsAlert.close();
    }, duration);
}

function getOrCreateNotificationContainer() {
    let container = document.getElementById('notification-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'notification-container';
        container.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            z-index: 9999;
            max-width: 400px;
        `;
        document.body.appendChild(container);
    }
    return container;
}

function getIconForType(type) {
    const icons = {
        'success': 'check-circle',
        'danger': 'exclamation-triangle',
        'warning': 'exclamation-circle',
        'info': 'info-circle',
        'primary': 'info-circle'
    };
    return icons[type] || 'info-circle';
}

// Adicionar classe de loading aos botões de submit
document.addEventListener('submit', function (e) {
    const submitBtn = e.target.querySelector('button[type="submit"]');
    if (submitBtn && !submitBtn.disabled) {
        const originalText = submitBtn.innerHTML;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Processando...';
        submitBtn.disabled = true;

        // Restaurar se houver erro (timeout de segurança)
        setTimeout(() => {
            if (submitBtn.disabled) {
                submitBtn.innerHTML = originalText;
                submitBtn.disabled = false;
            }
        }, 30000);
    }
});

// Melhorar experiência de navegação
window.addEventListener('beforeunload', function (e) {
    const forms = document.querySelectorAll('form[data-confirm-leave="true"]');
    let hasUnsavedChanges = false;

    forms.forEach(form => {
        const inputs = form.querySelectorAll('input, textarea, select');
        inputs.forEach(input => {
            if (input.defaultValue !== input.value) {
                hasUnsavedChanges = true;
            }
        });
    });

    if (hasUnsavedChanges) {
        e.preventDefault();
        e.returnValue = 'Você tem alterações não salvas. Deseja sair mesmo assim?';
        return e.returnValue;
    }
});
