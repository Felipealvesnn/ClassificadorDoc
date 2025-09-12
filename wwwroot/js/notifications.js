/**
 * Sistema de Notificações In-App - VERSÃO OTIMIZADA
 * Gerencia notificações em tempo real usando SignalR Manager centralizado
 */
class NotificationManager {
    constructor() {
        // Implementar Singleton Pattern
        if (NotificationManager.instance) {
            return NotificationManager.instance;
        }
        NotificationManager.instance = this;

        this.soundEnabled = true;
        this.notificationCenter = null;
        this.unreadCount = 0;
        this.isInitialized = false;

        // Rate limiting para notificações
        this.rateLimiter = {
            maxNotifications: 10,
            timeWindow: 60000, // 1 minuto
            notifications: []
        };

        // Cache de elementos DOM
        this.cachedElements = {
            notificationBadge: null,
            notificationList: null,
            markAllReadBtn: null
        };

        // Event listeners cleanup
        this.eventListeners = [];

        this.init();
    }

    async init() {
        if (this.isInitialized) {
            console.log("ℹ️ Sistema de notificações já inicializado");
            return;
        }

        this.createNotificationCenter();
        this.setupSignalR();
        this.loadExistingNotifications();
        this.setupEventListeners();
        this.isInitialized = true;

        console.log('🔔 Sistema de Notificações inicializado (versão otimizada)');
    }

    createNotificationCenter() {
        const navbar = document.querySelector('.navbar-nav');
        if (!navbar || this.notificationCenter) return;

        const notificationItem = document.createElement('li');
        notificationItem.className = 'nav-item dropdown me-3';

        // Usar template mais seguro
        const template = this.createNotificationTemplate();
        notificationItem.innerHTML = template;

        // Inserir antes do último item (usuário)
        const userDropdown = navbar.querySelector('.user-dropdown')?.parentElement;
        if (userDropdown) {
            navbar.insertBefore(notificationItem, userDropdown);
        } else {
            navbar.appendChild(notificationItem);
        }

        this.notificationCenter = notificationItem;

        // Cache elementos importantes
        this.cachedElements.notificationBadge = notificationItem.querySelector('.notification-count');
        this.cachedElements.notificationList = notificationItem.querySelector('.notification-list');
        this.cachedElements.markAllReadBtn = notificationItem.querySelector('.mark-all-read');
    }

    createNotificationTemplate() {
        return `
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
    }

    async setupSignalR() {
        // Usar SignalR Manager centralizado em vez de criar conexão própria
        if (!window.signalRManager) {
            console.warn('SignalR Manager não está disponível');
            // Tentar novamente em 1 segundo
            setTimeout(() => this.setupSignalR(), 1000);
            return;
        }

        try {
            // Se inscrever nos eventos do SignalR Manager
            window.signalRManager.subscribe('notifications', {
                connected: () => {
                    console.log('✅ Conectado ao hub de notificações via SignalR Manager');
                },
                reconnected: () => {
                    console.log('🔄 Reconectado ao hub de notificações');
                },
                disconnected: () => {
                    console.log('❌ Desconectado do hub de notificações');
                },
                receiveNotification: (notification) => {
                    this.handleNewNotification(notification);
                }
            });

            console.log('✅ Sistema de notificações configurado com SignalR Manager');
        } catch (error) {
            console.error('❌ Erro ao configurar notificações com SignalR Manager:', error);
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
        // Event delegation otimizada
        const handleNotificationClick = async (e) => {
            // Marcar todas como lidas
            if (e.target.matches('.mark-all-read')) {
                e.preventDefault();
                await this.markAllAsRead();
                return;
            }

            // Clique em notificação específica
            const notificationItem = e.target.closest('.notification-item');
            if (notificationItem) {
                e.preventDefault();
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
        };

        // Toggle de som
        const handleKeydown = (e) => {
            if (e.ctrlKey && e.shiftKey && e.key === 'M') {
                e.preventDefault();
                this.toggleSound();
            }
        };

        // Adicionar listeners com cleanup tracking
        this.addEventListener(document, 'click', handleNotificationClick);
        this.addEventListener(document, 'keydown', handleKeydown);
    }

    // Utilitário para rastrear event listeners
    addEventListener(element, event, handler) {
        element.addEventListener(event, handler);
        this.eventListeners.push({
            element,
            event,
            handler
        });
    }

    // Rate limiting para notificações
    isRateLimited() {
        const now = Date.now();

        // Limpar notificações antigas
        this.rateLimiter.notifications = this.rateLimiter.notifications.filter(
            timestamp => now - timestamp < this.rateLimiter.timeWindow
        );

        // Verificar se excedeu o limite
        if (this.rateLimiter.notifications.length >= this.rateLimiter.maxNotifications) {
            console.warn('⚠️ Rate limit de notificações atingido');
            return true;
        }

        this.rateLimiter.notifications.push(now);
        return false;
    }

    handleNewNotification(notification) {
        if (this.isRateLimited()) {
            return;
        }

        console.log('🔔 Nova notificação:', notification);

        // Tocar som se habilitado
        if (notification.playSound && this.soundEnabled) {
            if (window.soundManager) {
                window.soundManager.playNotificationSound(notification.priority);
            }
        }

        // Mostrar toast se habilitado - USANDO SISTEMA EXISTENTE
        if (notification.showToast && window.showToast) {
            const toastFunction = window.showToast[notification.type] || window.showToast.info;
            toastFunction(notification.title, notification.message);
        }

        // Atualizar centro de notificações
        this.addToNotificationCenter(notification);
        this.updateUnreadCount(this.unreadCount + 1);
    }

    addToNotificationCenter(notification) {
        const notificationList = this.cachedElements.notificationList;
        if (!notificationList) return;

        // Remover mensagem de carregamento
        const loadingMsg = notificationList.querySelector('.text-muted');
        if (loadingMsg && loadingMsg.textContent.includes('Carregando')) {
            loadingMsg.remove();
        }

        const notificationItem = this.createNotificationItem(notification);

        // Inserir no topo
        notificationList.insertBefore(notificationItem, notificationList.firstChild);

        // Limitar a 10 notificações visíveis
        const items = notificationList.querySelectorAll('.notification-item');
        if (items.length > 10) {
            items[items.length - 1].remove();
        }
    }

    createNotificationItem(notification) {
        const item = document.createElement('div');
        item.className = 'notification-item dropdown-item d-flex align-items-start py-2';
        item.dataset.notificationId = notification.id;
        item.dataset.actionUrl = notification.actionUrl || '';
        item.style.cursor = 'pointer';

        // Criar estrutura usando createElement
        const iconDiv = document.createElement('div');
        iconDiv.className = 'me-2';
        const icon = document.createElement('i');
        icon.className = `${notification.icon} text-${notification.color}`;
        iconDiv.appendChild(icon);

        const contentDiv = document.createElement('div');
        contentDiv.className = 'flex-grow-1';

        const titleDiv = document.createElement('div');
        titleDiv.className = 'fw-bold small';
        titleDiv.textContent = notification.title;

        const messageDiv = document.createElement('div');
        messageDiv.className = 'text-muted small';
        messageDiv.textContent = notification.message;

        const timeDiv = document.createElement('div');
        timeDiv.className = 'text-muted small';
        timeDiv.textContent = this.formatTime(notification.createdAt);

        contentDiv.appendChild(titleDiv);
        contentDiv.appendChild(messageDiv);
        contentDiv.appendChild(timeDiv);

        item.appendChild(iconDiv);
        item.appendChild(contentDiv);

        if (notification.priority === 'urgent' || notification.priority === 'high') {
            const priorityDiv = document.createElement('div');
            priorityDiv.className = 'ms-2';
            const badge = document.createElement('span');
            badge.className = 'badge bg-danger';
            badge.textContent = '!';
            priorityDiv.appendChild(badge);
            item.appendChild(priorityDiv);
        }

        return item;
    }

    updateNotificationCenter(notifications) {
        const notificationList = this.cachedElements.notificationList;
        if (!notificationList) return;

        // Usar DocumentFragment para melhor performance
        const fragment = document.createDocumentFragment();

        if (notifications.length === 0) {
            const emptyMsg = document.createElement('div');
            emptyMsg.className = 'dropdown-item text-center text-muted';
            emptyMsg.textContent = 'Nenhuma notificação';
            fragment.appendChild(emptyMsg);
        } else {
            notifications.slice(0, 10).forEach(notification => {
                fragment.appendChild(this.createNotificationItem(notification));
            });
        }

        notificationList.innerHTML = '';
        notificationList.appendChild(fragment);
    }

    updateUnreadCount(count) {
        this.unreadCount = count;
        const badge = this.cachedElements.notificationBadge;
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
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const response = await fetch(`/api/notifications/${notificationId}/mark-read`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token || ''
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
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const response = await fetch('/api/notifications/mark-all-read', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token || ''
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
        if (window.soundManager) {
            this.soundEnabled = window.soundManager.toggle();
        } else {
            this.soundEnabled = !this.soundEnabled;
            localStorage.setItem('notificationSoundEnabled', this.soundEnabled.toString());
        }

        const message = this.soundEnabled ? 'Sons de notificação ativados' : 'Sons de notificação desativados';

        // Usar sistema de toasts existente
        if (window.showToast) {
            window.showToast.info('Configuração alterada', message);
        }
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

    // Cleanup method
    cleanup() {
        // Remover event listeners
        this.eventListeners.forEach(({ element, event, handler }) => {
            element.removeEventListener(event, handler);
        });
        this.eventListeners = [];

        // Remover inscrição do SignalR Manager
        if (window.signalRManager) {
            window.signalRManager.unsubscribe('notifications');
        }

        this.isInitialized = false;
        console.log('🧹 Sistema de notificações limpo');
    }
}

// Inicializar quando o DOM estiver carregado
document.addEventListener('DOMContentLoaded', () => {
    // Verificar se o usuário está logado
    if (document.querySelector('.navbar .user-dropdown')) {
        // Aguardar SignalR Manager estar disponível
        setTimeout(() => {
            window.notificationManager = new NotificationManager();
        }, 1500); // Maior delay para aguardar SignalR Manager
    }
});

// Cleanup ao sair da página
window.addEventListener('beforeunload', () => {
    if (window.notificationManager) {
        window.notificationManager.cleanup();
    }
});

console.log("📄 Script de notificações carregado (versão otimizada - usando SignalR Manager)");
