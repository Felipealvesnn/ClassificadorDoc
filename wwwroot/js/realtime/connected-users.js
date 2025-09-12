/**
 * Gerenciador de usuários conectados em tempo real - VERSÃO OTIMIZADA
 * Usa SignalR Manager centralizado para evitar múltiplas conexões
 */
class ConnectedUsersManager {
    constructor() {
        // Implementar Singleton Pattern
        if (ConnectedUsersManager.instance) {
            return ConnectedUsersManager.instance;
        }
        ConnectedUsersManager.instance = this;

        this.isInitialized = false;
        this.lastActivityUpdate = 0;
        this.activityInterval = null;
        this.cachedElements = null;

        // Debounce para atualizações de atividade
        this.updateActivityDebounced = this.debounce(this.updateActivity.bind(this), 5000);
    }

    /**
     * Utilitário de debounce
     */
    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    /**
     * Inicializa o gerenciador usando SignalR Manager centralizado
     */
    async initialize() {
        if (this.isInitialized) {
            console.log("ℹ️ Gerenciador de usuários conectados já inicializado");
            return;
        }

        // Aguardar SignalR Manager estar disponível
        if (!window.signalRManager) {
            console.warn("⚠️ SignalR Manager não disponível");
            setTimeout(() => this.initialize(), 1000);
            return;
        }

        console.log("🔌 Inicializando gerenciador de usuários conectados...");

        // Se inscrever nos eventos do SignalR Manager
        window.signalRManager.subscribe('connectedUsers', {
            connected: () => this.onSignalRConnected(),
            reconnected: () => this.onSignalRReconnected(),
            disconnected: () => this.onSignalRDisconnected(),
            connectedUsersUpdate: (stats) => this.handleConnectedUsersUpdate(stats),
            receiveNotification: (notification) => this.handleNotification(notification)
        });

        this.isInitialized = true;
        this.startPeriodicActivities();

        // Se SignalR já estiver conectado, solicitar usuários
        if (window.signalRManager.isConnected) {
            await this.requestConnectedUsers();
        }

        console.log("✅ Gerenciador de usuários conectados iniciado com sucesso");
    }

    /**
     * Eventos de conexão SignalR
     */
    onSignalRConnected() {
        console.log("🔄 SignalR conectado - solicitando usuários");
        this.requestConnectedUsers();
    }

    onSignalRReconnected() {
        console.log("🔄 SignalR reconectado - solicitando usuários");
        this.requestConnectedUsers();
    }

    onSignalRDisconnected() {
        console.log("❌ SignalR desconectado");
    }

    /**
     * Processa atualizações de usuários conectados
     */
    handleConnectedUsersUpdate(stats) {
        try {
            // Atualizar contador no navbar se existir
            this.updateNavbarCounter(stats.totalConnected);

            // Atualizar página de usuários conectados se estiver ativa
            if (typeof updateConnectedUsersDisplay === 'function') {
                updateConnectedUsersDisplay(stats);
            }

            // Emitir evento customizado para outros componentes
            document.dispatchEvent(new CustomEvent('connectedUsersUpdated', {
                detail: stats
            }));

        } catch (error) {
            console.error("❌ Erro ao processar atualização de usuários conectados:", error);
        }
    }

    /**
     * Atualiza contador de usuários no navbar - Otimizado
     */
    updateNavbarCounter(count) {
        // Cache dos elementos para evitar múltiplas consultas DOM
        if (!this.cachedElements) {
            this.cachedElements = {
                counter: document.getElementById('connectedUsersCounter'),
                badge: document.querySelector('.connected-users-badge')
            };
        }

        const { counter, badge } = this.cachedElements;

        if (counter && counter.textContent !== count.toString()) {
            counter.textContent = count;
            counter.title = `${count} usuários conectados`;
        }

        if (badge && badge.textContent !== count.toString()) {
            badge.textContent = count;
        }
    }

    /**
     * Processa notificações recebidas
     */
    handleNotification(notification) {
        try {
            if (window.showToast && notification.showToast) {
                const toastFunction = window.showToast[notification.type] || window.showToast.info;
                toastFunction(notification.title, notification.message);
            }

            // Tocar som se configurado
            if (notification.playSound && window.soundManager) {
                window.soundManager.playNotificationSound(notification.type);
            }

        } catch (error) {
            console.error("❌ Erro ao processar notificação:", error);
        }
    }

    /**
     * Solicita lista atualizada de usuários conectados
     */
    async requestConnectedUsers() {
        if (!window.signalRManager || !window.signalRManager.isConnected) return;

        try {
            await window.signalRManager.invoke("RequestConnectedUsers");
        } catch (error) {
            console.error("❌ Erro ao solicitar usuários conectados:", error);
        }
    }

    /**
     * Atualiza atividade do usuário no servidor - Com debounce
     */
    async updateActivity() {
        if (!window.signalRManager || !window.signalRManager.isConnected) return;

        const now = Date.now();
        if (now - this.lastActivityUpdate < 10000) { // Mínimo 10 segundos entre atualizações
            return;
        }

        try {
            this.lastActivityUpdate = now;
            await window.signalRManager.invoke("UpdateActivity");
        } catch (error) {
            console.error("❌ Erro ao atualizar atividade:", error);
        }
    }

    /**
     * Inicia atividades periódicas - Frequência otimizada
     */
    startPeriodicActivities() {
        // Atualizar atividade a cada 60 segundos
        this.activityInterval = setInterval(() => {
            this.updateActivity();
        }, 60000);
    }

    /**
     * Para atividades periódicas
     */
    stopPeriodicActivities() {
        if (this.activityInterval) {
            clearInterval(this.activityInterval);
            this.activityInterval = null;
        }
    }

    /**
     * Desconecta e limpa recursos
     */
    async disconnect() {
        console.log("🔌 Desconectando gerenciador de usuários conectados...");

        this.stopPeriodicActivities();

        // Limpar cache de elementos
        this.cachedElements = null;

        // Remover inscrição do SignalR Manager
        if (window.signalRManager) {
            window.signalRManager.unsubscribe('connectedUsers');
        }

        this.isInitialized = false;
    }

    /**
     * Obtém status da conexão
     */
    getConnectionStatus() {
        return {
            isInitialized: this.isInitialized,
            signalRStatus: window.signalRManager?.getStatus() || null
        };
    }
}

// Instância singleton global do gerenciador
window.connectedUsersManager = new ConnectedUsersManager();

// Auto-inicializar quando o DOM estiver pronto
document.addEventListener('DOMContentLoaded', () => {
    // Aguardar SignalR Manager estar disponível
    setTimeout(() => {
        window.connectedUsersManager.initialize();
    }, 1500); // Maior delay para aguardar SignalR Manager
});

// Limpeza ao sair da página
window.addEventListener('beforeunload', () => {
    if (window.connectedUsersManager) {
        window.connectedUsersManager.disconnect();
    }
});

// Detectar atividade do usuário com debounce
document.addEventListener('click', () => {
    if (window.connectedUsersManager && window.connectedUsersManager.isInitialized) {
        window.connectedUsersManager.updateActivityDebounced();
    }
});

document.addEventListener('keypress', () => {
    if (window.connectedUsersManager && window.connectedUsersManager.isInitialized) {
        window.connectedUsersManager.updateActivityDebounced();
    }
});

console.log("📄 Script de usuários conectados carregado (versão otimizada - usando SignalR Manager)");
