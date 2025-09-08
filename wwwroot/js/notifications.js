/**
 * Sistema de Notificações In-App
 * Gerencia toasts, sons e notificações em tempo real
 */
class NotificationManager {
    constructor() {
        this.connection = null;
        this.soundEnabled = true;
        this.toastContainer = null;
        this.notificationCenter = null;
        this.unreadCount = 0;

        this.init();
    }

    async init() {
        this.createToastContainer();
        this.createNotificationCenter();
        this.setupSignalR();
        this.loadExistingNotifications();
        this.setupEventListeners();

        console.log('🔔 Sistema de Notificações inicializado');
    }

    createToastContainer() {
        this.toastContainer = document.createElement('div');
        this.toastContainer.id = 'toast-container';
        this.toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
        this.toastContainer.style.zIndex = '9999';
        document.body.appendChild(this.toastContainer);
    }

    createNotificationCenter() {
        // Adicionar ícone de notificação na navbar
        const navbar = document.querySelector('.navbar-nav');
        if (navbar) {
            const notificationItem = document.createElement('li');
            notificationItem.className = 'nav-item dropdown me-3';
            notificationItem.innerHTML = `
                <a class="nav-link notification-bell" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                    <i class="fas fa-bell"></i>
                    <span class="badge bg-danger notification-count" style="display: none;">0</span>
                </a>
                <div class="dropdown-menu dropdown-menu-end notification-dropdown" style="width: 350px; max-height: 400px; overflow-y: auto;">
                    <div class="dropdown-header d-flex justify-content-between align-items-center">
                        <span>Notificações</span>
                        <button class="btn btn-sm btn-outline-secondary mark-all-read" type="button">
                            Marcar todas como lidas
                        </button>
                    </div>
                    <div class="notification-list">
                        <div class="dropdown-item text-center text-muted">
                            Carregando notificações...
                        </div>
                    </div>
                </div>
            `;

            // Inserir antes do último item (usuário)
            const userDropdown = navbar.querySelector('.user-dropdown')?.parentElement;
            if (userDropdown) {
                navbar.insertBefore(notificationItem, userDropdown);
            } else {
                navbar.appendChild(notificationItem);
            }

            this.notificationCenter = notificationItem;
        }
    }

    async setupSignalR() {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR não está disponível');
            return;
        }

        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl('/notificationHub')
                .build();

            this.connection.on('ReceiveNotification', (notification) => {
                this.handleNewNotification(notification);
            });

            await this.connection.start();
            console.log('✅ Conectado ao hub de notificações');
        } catch (error) {
            console.error('❌ Erro ao conectar SignalR:', error);
        }
    }

    async loadExistingNotifications() {
        try {
            const response = await fetch('/api/notifications');
            if (response.ok) {
                const notifications = await response.json();
                this.updateNotificationCenter(notifications);
                this.updateUnreadCount(notifications.filter(n => !n.isRead).length);
            }
        } catch (error) {
            console.error('Erro ao carregar notificações:', error);
        }
    }

    setupEventListeners() {
        // Marcar todas como lidas
        document.addEventListener('click', async (e) => {
            if (e.target.classList.contains('mark-all-read')) {
                await this.markAllAsRead();
            }
        });

        // Clique em notificação específica
        document.addEventListener('click', async (e) => {
            const notificationItem = e.target.closest('.notification-item');
            if (notificationItem) {
                const notificationId = notificationItem.dataset.notificationId;
                if (notificationId) {
                    await this.markAsRead(notificationId);

                    // Se tem actionUrl, navegar
                    const actionUrl = notificationItem.dataset.actionUrl;
                    if (actionUrl) {
                        window.location.href = actionUrl;
                    }
                }
            }
        });

        // Toggle de som
        document.addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.shiftKey && e.key === 'M') {
                this.toggleSound();
            }
        });
    }

    handleNewNotification(notification) {
        console.log('🔔 Nova notificação:', notification);

        // Tocar som se habilitado
        if (notification.playSound && this.soundEnabled) {
            // Usar o SoundManager unificado
            if (window.soundManager) {
                window.soundManager.playNotificationSound(notification.priority);
            }
        }

        // Mostrar toast se habilitado
        if (notification.showToast) {
            this.showToast(notification);
        }

        // Atualizar centro de notificações
        this.addToNotificationCenter(notification);
        this.updateUnreadCount(this.unreadCount + 1);
    }

    showToast(notification) {
        const toastId = `toast-${notification.id}-${Date.now()}`;
        const toastElement = document.createElement('div');
        toastElement.id = toastId;
        toastElement.className = `toast notification-toast toast-${notification.color}`;
        toastElement.setAttribute('role', 'alert');
        toastElement.setAttribute('aria-live', 'assertive');
        toastElement.setAttribute('aria-atomic', 'true');

        const autoHideDelay = notification.priority === 'urgent' ? 10000 :
            notification.priority === 'high' ? 7000 : 5000;

        toastElement.innerHTML = `
            <div class="toast-header bg-${notification.color} text-white">
                <i class="${notification.icon} me-2"></i>
                <strong class="me-auto">${this.escapeHtml(notification.title)}</strong>
                <small class="text-white-50">${this.formatTime(notification.createdAt)}</small>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
            </div>
            <div class="toast-body">
                ${this.escapeHtml(notification.message)}
                ${notification.actionUrl ? `<hr><a href="${notification.actionUrl}" class="btn btn-sm btn-${notification.color}">Ver detalhes</a>` : ''}
            </div>
        `;

        this.toastContainer.appendChild(toastElement);

        // Inicializar Bootstrap toast
        const toast = new bootstrap.Toast(toastElement, {
            autohide: true,
            delay: autoHideDelay
        });

        toast.show();

        // Remover elemento após esconder
        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });
    }

    addToNotificationCenter(notification) {
        const notificationList = document.querySelector('.notification-list');
        if (!notificationList) return;

        // Remover mensagem de carregamento
        const loadingMsg = notificationList.querySelector('.text-muted');
        if (loadingMsg) loadingMsg.remove();

        const notificationItem = document.createElement('div');
        notificationItem.className = 'notification-item dropdown-item d-flex align-items-start py-2';
        notificationItem.dataset.notificationId = notification.id;
        notificationItem.dataset.actionUrl = notification.actionUrl || '';
        notificationItem.style.cursor = 'pointer';

        notificationItem.innerHTML = `
            <div class="me-2">
                <i class="${notification.icon} text-${notification.color}"></i>
            </div>
            <div class="flex-grow-1">
                <div class="fw-bold small">${this.escapeHtml(notification.title)}</div>
                <div class="text-muted small">${this.escapeHtml(notification.message)}</div>
                <div class="text-muted small">${this.formatTime(notification.createdAt)}</div>
            </div>
            ${notification.priority === 'urgent' || notification.priority === 'high' ?
                '<div class="ms-2"><span class="badge bg-danger">!</span></div>' : ''}
        `;

        // Inserir no topo
        notificationList.insertBefore(notificationItem, notificationList.firstChild);

        // Limitar a 10 notificações visíveis
        const items = notificationList.querySelectorAll('.notification-item');
        if (items.length > 10) {
            items[items.length - 1].remove();
        }
    }

    updateNotificationCenter(notifications) {
        const notificationList = document.querySelector('.notification-list');
        if (!notificationList) return;

        notificationList.innerHTML = '';

        if (notifications.length === 0) {
            notificationList.innerHTML = '<div class="dropdown-item text-center text-muted">Nenhuma notificação</div>';
            return;
        }

        notifications.slice(0, 10).forEach(notification => {
            this.addToNotificationCenter(notification);
        });
    }

    updateUnreadCount(count) {
        this.unreadCount = count;
        const badge = document.querySelector('.notification-count');
        if (badge) {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count.toString();
                badge.style.display = 'inline-block';
            } else {
                badge.style.display = 'none';
            }
        }
    }

    async markAsRead(notificationId) {
        try {
            const response = await fetch(`/api/notifications/${notificationId}/mark-read`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                }
            });

            if (response.ok) {
                this.updateUnreadCount(Math.max(0, this.unreadCount - 1));
                // Atualizar visualmente a notificação
                const item = document.querySelector(`[data-notification-id="${notificationId}"]`);
                if (item) {
                    item.classList.add('opacity-75');
                }
            }
        } catch (error) {
            console.error('Erro ao marcar notificação como lida:', error);
        }
    }

    async markAllAsRead() {
        try {
            const response = await fetch('/api/notifications/mark-all-read', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                }
            });

            if (response.ok) {
                this.updateUnreadCount(0);
                // Recarregar notificações
                await this.loadExistingNotifications();
            }
        } catch (error) {
            console.error('Erro ao marcar todas as notificações como lidas:', error);
        }
    }

    toggleSound() {
        // Usar o SoundManager unificado
        if (window.soundManager) {
            this.soundEnabled = window.soundManager.toggle();
        } else {
            this.soundEnabled = !this.soundEnabled;
            localStorage.setItem('notificationSoundEnabled', this.soundEnabled.toString());
        }

        const message = this.soundEnabled ? 'Sons de notificação ativados' : 'Sons de notificação desativados';
        this.showToast({
            id: Date.now(),
            title: 'Configuração alterada',
            message: message,
            type: 'info',
            color: 'info',
            icon: this.soundEnabled ? 'fas fa-volume-up' : 'fas fa-volume-mute',
            createdAt: new Date().toISOString(),
            playSound: false,
            showToast: true
        });
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    formatTime(isoString) {
        const date = new Date(isoString);
        const now = new Date();
        const diff = now - date;

        if (diff < 60000) return 'Agora';
        if (diff < 3600000) return `${Math.floor(diff / 60000)}m`;
        if (diff < 86400000) return `${Math.floor(diff / 3600000)}h`;
        return date.toLocaleDateString('pt-BR');
    }
}

// Inicializar quando o DOM estiver carregado
document.addEventListener('DOMContentLoaded', () => {
    // Verificar se o usuário está logado
    if (document.querySelector('.navbar .user-dropdown')) {
        window.notificationManager = new NotificationManager();
    }
});

// CSS para as notificações
const notificationStyles = `
<style>
.notification-toast {
    min-width: 300px;
    max-width: 400px;
}

.toast-primary { border-left: 4px solid #0d6efd; }
.toast-success { border-left: 4px solid #198754; }
.toast-warning { border-left: 4px solid #ffc107; }
.toast-danger { border-left: 4px solid #dc3545; }
.toast-info { border-left: 4px solid #0dcaf0; }

.notification-bell {
    position: relative;
}

.notification-bell .badge {
    position: absolute;
    top: -5px;
    right: -5px;
    font-size: 0.7rem;
    min-width: 18px;
    height: 18px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
}

.notification-dropdown {
    box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.15);
}

.notification-item:hover {
    background-color: rgba(0, 0, 0, 0.05);
}

.notification-item.opacity-75 {
    opacity: 0.75;
}

@keyframes notification-pulse {
    0% { transform: scale(1); }
    50% { transform: scale(1.1); }
    100% { transform: scale(1); }
}

.notification-bell.has-new {
    animation: notification-pulse 2s infinite;
}
</style>
`;

document.head.insertAdjacentHTML('beforeend', notificationStyles);
