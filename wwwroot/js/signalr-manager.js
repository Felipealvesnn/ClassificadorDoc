/**
 * Gerenciador Central de SignalR - Hub Unificado
 * Responsável por uma única conexão SignalR compartilhada entre todos os componentes
 */
class SignalRManager {
    constructor() {
        // Implementar Singleton Pattern
        if (SignalRManager.instance) {
            return SignalRManager.instance;
        }
        SignalRManager.instance = this;

        this.connection = null;
        this.isConnected = false;
        this.isInitialized = false;
        this.subscribers = new Map(); // Componentes que escutam eventos
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;

        // Circuit breaker para proteção
        this.circuitBreaker = {
            failures: 0,
            maxFailures: 5,
            isOpen: false,
            nextAttempt: 0,
            timeout: 30000
        };

        console.log("🔗 SignalR Manager criado");
    }

    /**
     * Inicializa a conexão SignalR única
     */
    async initialize() {
        if (this.isInitialized) {
            console.log("ℹ️ SignalR Manager já inicializado");
            return this.connection;
        }

        if (this.isCircuitBreakerOpen()) {
            console.warn("⚠️ Circuit breaker aberto, aguardando...");
            return null;
        }

        try {
            console.log("🔌 Inicializando conexão SignalR central...");

            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/notificationHub")
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: retryContext => {
                        const baseDelay = Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                        const jitter = Math.random() * 1000;
                        return baseDelay + jitter;
                    }
                })
                .configureLogging(signalR.LogLevel.Warning)
                .build();

            this.setupConnectionEvents();
            this.setupHubEvents();

            await this.connection.start();
            this.isConnected = true;
            this.isInitialized = true;
            this.reconnectAttempts = 0;
            this.circuitBreaker.failures = 0;

            console.log("✅ SignalR Manager conectado com sucesso");

            // Notificar todos os subscribers sobre a conexão
            this.notifySubscribers('connected', { isConnected: true });

            return this.connection;

        } catch (error) {
            console.error("❌ Erro ao inicializar SignalR Manager:", error);
            this.recordCircuitBreakerFailure();
            this.scheduleReconnect();
            return null;
        }
    }

    /**
     * Configura eventos de conexão
     */
    setupConnectionEvents() {
        this.connection.onreconnected(() => {
            console.log("🔄 SignalR Manager reconectado");
            this.isConnected = true;
            this.reconnectAttempts = 0;
            this.circuitBreaker.failures = 0;

            this.notifySubscribers('reconnected', { isConnected: true });

            if (window.showToast) {
                window.showToast.success("Conexão", "Reconectado ao servidor");
            }
        });

        this.connection.onreconnecting(() => {
            console.log("🔄 SignalR Manager tentando reconectar...");
            this.isConnected = false;
            this.notifySubscribers('reconnecting', { isConnected: false });
        });

        this.connection.onclose((error) => {
            console.log("❌ SignalR Manager desconectado:", error);
            this.isConnected = false;
            this.notifySubscribers('disconnected', { isConnected: false, error });
            this.scheduleReconnect();
        });
    }

    /**
     * Configura eventos do Hub
     */
    setupHubEvents() {
        // Eventos de usuários conectados
        this.connection.on("ConnectedUsersUpdate", (stats) => {
            this.notifySubscribers('connectedUsersUpdate', stats);
        });

        // Eventos de notificações
        this.connection.on("ReceiveNotification", (notification) => {
            this.notifySubscribers('receiveNotification', notification);
        });

        // Outros eventos do hub podem ser adicionados aqui
        this.connection.on("Pong", (timestamp) => {
            this.notifySubscribers('pong', { timestamp });
        });
    }

    /**
     * Permite que componentes se inscrevam em eventos
     */
    subscribe(componentName, eventHandlers) {
        this.subscribers.set(componentName, eventHandlers);
        console.log(`📡 ${componentName} inscrito no SignalR Manager`);
    }

    /**
     * Remove inscrição de um componente
     */
    unsubscribe(componentName) {
        this.subscribers.delete(componentName);
        console.log(`📡 ${componentName} removido do SignalR Manager`);
    }

    /**
     * Notifica todos os subscribers sobre um evento
     */
    notifySubscribers(eventType, data) {
        this.subscribers.forEach((handlers, componentName) => {
            if (handlers[eventType] && typeof handlers[eventType] === 'function') {
                try {
                    handlers[eventType](data);
                } catch (error) {
                    console.error(`❌ Erro no handler ${eventType} do ${componentName}:`, error);
                }
            }
        });
    }

    /**
     * Invoca método no hub (centralizado)
     */
    async invoke(methodName, ...args) {
        if (!this.isConnected || !this.connection || this.isCircuitBreakerOpen()) {
            console.warn(`⚠️ Não é possível invocar ${methodName}: conexão indisponível`);
            return false;
        }

        try {
            await this.connection.invoke(methodName, ...args);
            return true;
        } catch (error) {
            console.error(`❌ Erro ao invocar ${methodName}:`, error);
            this.recordCircuitBreakerFailure();
            return false;
        }
    }

    /**
     * Circuit breaker methods
     */
    isCircuitBreakerOpen() {
        if (this.circuitBreaker.isOpen) {
            if (Date.now() > this.circuitBreaker.nextAttempt) {
                this.circuitBreaker.isOpen = false;
                this.circuitBreaker.failures = 0;
                console.log("🔄 Circuit breaker resetado");
            } else {
                return true;
            }
        }
        return false;
    }

    recordCircuitBreakerFailure() {
        this.circuitBreaker.failures++;
        if (this.circuitBreaker.failures >= this.circuitBreaker.maxFailures) {
            this.circuitBreaker.isOpen = true;
            this.circuitBreaker.nextAttempt = Date.now() + this.circuitBreaker.timeout;
            console.warn("⚠️ Circuit breaker aberto devido a muitas falhas");
        }
    }

    /**
     * Agenda tentativa de reconexão
     */
    scheduleReconnect() {
        if (this.reconnectAttempts >= this.maxReconnectAttempts || this.isCircuitBreakerOpen()) {
            console.error("❌ Máximo de tentativas de reconexão atingido");
            return;
        }

        this.reconnectAttempts++;
        const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts), 30000);

        console.log(`🔄 Tentativa de reconexão ${this.reconnectAttempts}/${this.maxReconnectAttempts} em ${delay}ms`);

        setTimeout(() => {
            this.initialize();
        }, delay);
    }

    /**
     * Desconecta e limpa recursos
     */
    async disconnect() {
        console.log("🔌 Desconectando SignalR Manager...");

        // Notificar subscribers antes de desconectar
        this.notifySubscribers('beforeDisconnect', {});

        if (this.connection) {
            try {
                await this.connection.stop();
            } catch (error) {
                console.warn("⚠️ Erro ao desconectar:", error);
            }
            this.connection = null;
        }

        this.isConnected = false;
        this.isInitialized = false;
        this.subscribers.clear();
    }

    /**
     * Obtém status da conexão
     */
    getStatus() {
        return {
            isConnected: this.isConnected,
            isInitialized: this.isInitialized,
            connectionState: this.connection?.connectionState || 'Disconnected',
            reconnectAttempts: this.reconnectAttempts,
            circuitBreakerOpen: this.circuitBreaker.isOpen,
            failures: this.circuitBreaker.failures,
            subscribersCount: this.subscribers.size
        };
    }
}

// Instância global do SignalR Manager
window.signalRManager = new SignalRManager();

// Auto-inicializar quando SignalR estiver disponível
document.addEventListener('DOMContentLoaded', () => {
    if (typeof signalR !== 'undefined') {
        setTimeout(() => {
            window.signalRManager.initialize();
        }, 500);
    } else {
        console.warn("⚠️ SignalR não disponível");
    }
});

// Cleanup ao sair da página
window.addEventListener('beforeunload', () => {
    if (window.signalRManager) {
        window.signalRManager.disconnect();
    }
});

console.log("📄 SignalR Manager carregado");
