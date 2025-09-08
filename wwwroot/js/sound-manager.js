/**
 * Sistema Unificado de Sons para Notificações e Alertas
 * Gerencia todos os sons de forma otimizada com cache e fallbacks
 */
class SoundManager {
    constructor() {
        this.enabled = true;
        this.volume = 0.7;
        this.audioContext = null;
        this.audioCache = new Map();
        this.initPromise = null;
        this.isInitialized = false;

        // Configurações dos sons
        this.soundProfiles = {
            // Para notificações normais (Audio API - mais compatível)
            notification: {
                method: 'audio',
                sounds: {
                    low: 'data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1fdJivrJBhNjVgodDbq2EcBj+a2/LDciUFLIHO8tiJNwgZaLvt559NEAxQp+PwtmMcBjiR1/LMeSwFJHfH8N2QQAoUXrTp66hVFApGn+DyvmMeC0OX3/DSeSYEKHzN8+GQXQ4XZKM=',
                    normal: 'data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1fdJivrJBhNjVgodDbq2EcBj+a2/LDciUFLIHO8tiJNwgZaLvt559NEAxQp+PwtmMcBjiR1/LMeSwFJHfH8N2QQAoUXrTp66hVFApGn+DyvmMeC0OX3/DSeSYEKHzN8+GQXQ4XZKM=',
                    high: 'data:audio/wav;base64,UklGRmIGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YT4GAACBhYqFbF1fdJivrJBhNjVgodDbq2EcBj+a2/LDciUFLIHO8tiJNwgZaLvt559NEAxQp+PwtmMcBjiR1/LMeSwFJHfH8N2QQAoUXrTp66hVFApGn+DyvmMeC0OX3/DSeSYEKHzN8+GQXQ4XZKM=',
                    urgent: 'data:audio/wav;base64,UklGRmIGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YT4GAACBhYqFbF1fdJivrJBhNjVgodDbq2EcBj+a2/LDciUFLIHO8tiJNwgZaLvt559NEAxQp+PwtmMcBjiR1/LMeSwFJHfH8N2QQAoUXrTp66hVFApGn+DyvmMeC0OX3/DSeSYEKHzN8+GQXQ4XZKM='
                },
                volume: 0.4
            },
            // Para alertas críticos (Web Audio API - mais poderoso)
            alert: {
                method: 'webaudio',
                frequencies: {
                    LOW: [300],
                    NORMAL: [500, 400],
                    HIGH: [800, 600, 400]
                },
                volume: 0.6,
                duration: 0.2,
                interval: 300
            }
        };

        this.loadSettings();
    }

    /**
     * Inicialização lazy do AudioContext
     */
    async init() {
        if (this.initPromise) return this.initPromise;

        this.initPromise = this._doInit();
        return this.initPromise;
    }

    async _doInit() {
        if (this.isInitialized) return;

        try {
            // Inicializar AudioContext apenas se necessário
            if (this.needsWebAudio()) {
                this.audioContext = new (window.AudioContext || window.webkitAudioContext)();

                // Resolver problemas de autoplay policy
                if (this.audioContext.state === 'suspended') {
                    await this.audioContext.resume();
                }
            }

            // Pré-carregar sons essenciais
            await this.preloadEssentialSounds();

            this.isInitialized = true;
            console.log('🔊 SoundManager inicializado');

        } catch (error) {
            console.warn('⚠️ Erro ao inicializar áudio:', error);
            this.enabled = false;
        }
    }

    /**
     * Verificar se Web Audio API é necessária
     */
    needsWebAudio() {
        return true; // Sempre tentar inicializar para alertas
    }

    /**
     * Pré-carregar sons essenciais
     */
    async preloadEssentialSounds() {
        const essentialSounds = ['normal', 'high'];

        for (const priority of essentialSounds) {
            try {
                const audio = new Audio(this.soundProfiles.notification.sounds[priority]);
                audio.volume = 0.01; // Volume mínimo para pré-load
                this.audioCache.set(`notification_${priority}`, audio);
            } catch (error) {
                console.warn(`Erro ao pré-carregar som ${priority}:`, error);
            }
        }
    }

    /**
     * Reproduzir som de notificação
     */
    async playNotificationSound(priority = 'normal') {
        if (!this.enabled) return;

        await this.init();

        const normalizedPriority = this.normalizePriority(priority);
        const profile = this.soundProfiles.notification;

        try {
            // Usar cache se disponível
            let audio = this.audioCache.get(`notification_${normalizedPriority}`);

            if (!audio) {
                audio = new Audio(profile.sounds[normalizedPriority] || profile.sounds.normal);
                this.audioCache.set(`notification_${normalizedPriority}`, audio);
            }

            // Clonar para permitir múltiplos sons simultâneos
            const audioClone = audio.cloneNode();
            audioClone.volume = profile.volume * this.volume;

            return audioClone.play().catch(e =>
                console.log('Som de notificação bloqueado:', e.message)
            );

        } catch (error) {
            console.warn('Erro ao reproduzir som de notificação:', error);
        }
    }

    /**
     * Reproduzir som de alerta (mais complexo)
     */
    async playAlertSound(priority = 'NORMAL') {
        if (!this.enabled) return;

        await this.init();

        if (!this.audioContext) {
            // Fallback para Audio API
            return this.playNotificationSound(priority.toLowerCase());
        }

        const profile = this.soundProfiles.alert;
        const frequencies = profile.frequencies[priority] || profile.frequencies.NORMAL;

        try {
            // Reproduzir sequência de tons
            for (let i = 0; i < frequencies.length; i++) {
                setTimeout(() => {
                    this.playTone(frequencies[i], profile.duration);
                }, i * profile.interval);
            }

        } catch (error) {
            console.warn('Erro ao reproduzir som de alerta:', error);
            // Fallback para notificação simples
            this.playNotificationSound(priority.toLowerCase());
        }
    }

    /**
     * Reproduzir tom específico (Web Audio API)
     */
    playTone(frequency, duration = 0.2) {
        if (!this.audioContext || !this.enabled) return;

        try {
            const oscillator = this.audioContext.createOscillator();
            const gainNode = this.audioContext.createGain();

            oscillator.connect(gainNode);
            gainNode.connect(this.audioContext.destination);

            oscillator.frequency.value = frequency;
            oscillator.type = 'sine';

            const now = this.audioContext.currentTime;
            const effectiveVolume = this.soundProfiles.alert.volume * this.volume;

            gainNode.gain.setValueAtTime(0, now);
            gainNode.gain.linearRampToValueAtTime(effectiveVolume, now + 0.01);
            gainNode.gain.exponentialRampToValueAtTime(0.01, now + duration);

            oscillator.start(now);
            oscillator.stop(now + duration);

        } catch (error) {
            console.warn('Erro ao reproduzir tom:', error);
        }
    }

    /**
     * Normalizar prioridades entre sistemas
     */
    normalizePriority(priority) {
        const mapping = {
            // Mapeamento notifications.js
            'urgent': 'urgent',
            'high': 'high',
            'normal': 'normal',
            'low': 'low',

            // Mapeamento alerts.js
            'HIGH': 'high',
            'NORMAL': 'normal',
            'LOW': 'low'
        };

        return mapping[priority] || 'normal';
    }

    /**
     * Configurar som
     */
    configure(options = {}) {
        if (typeof options.enabled === 'boolean') {
            this.enabled = options.enabled;
        }

        if (typeof options.volume === 'number') {
            this.volume = Math.max(0, Math.min(1, options.volume));
        }

        this.saveSettings();

        // Teste de som se solicitado
        if (options.test) {
            this.testSound();
        }
    }

    /**
     * Testar som
     */
    async testSound() {
        console.log('🔊 Testando sistema de som...');

        await this.playNotificationSound('normal');

        setTimeout(() => {
            this.playAlertSound('HIGH');
        }, 1000);
    }

    /**
     * Alternar som on/off
     */
    toggle() {
        this.enabled = !this.enabled;
        this.saveSettings();

        const status = this.enabled ? 'ativado' : 'desativado';
        console.log(`🔊 Som ${status}`);

        return this.enabled;
    }

    /**
     * Carregar configurações do localStorage
     */
    loadSettings() {
        try {
            const savedEnabled = localStorage.getItem('soundManager_enabled');
            const savedVolume = localStorage.getItem('soundManager_volume');

            if (savedEnabled !== null) {
                this.enabled = savedEnabled === 'true';
            }

            if (savedVolume !== null) {
                this.volume = parseFloat(savedVolume);
            }

        } catch (error) {
            console.warn('Erro ao carregar configurações de som:', error);
        }
    }

    /**
     * Salvar configurações no localStorage
     */
    saveSettings() {
        try {
            localStorage.setItem('soundManager_enabled', this.enabled.toString());
            localStorage.setItem('soundManager_volume', this.volume.toString());
        } catch (error) {
            console.warn('Erro ao salvar configurações de som:', error);
        }
    }

    /**
     * Limpar cache e recursos
     */
    dispose() {
        // Limpar cache de áudio
        this.audioCache.clear();

        // Fechar AudioContext
        if (this.audioContext && this.audioContext.state !== 'closed') {
            this.audioContext.close();
        }

        this.isInitialized = false;
        console.log('🔊 SoundManager finalizado');
    }
}

// Instância global
window.soundManager = new SoundManager();

// API de compatibilidade para sistemas existentes
window.playNotificationSound = (priority) => window.soundManager.playNotificationSound(priority);
window.playAlertSound = (priority) => window.soundManager.playAlertSound(priority);

// Inicialização automática em interação do usuário
document.addEventListener('click', () => {
    if (!window.soundManager.isInitialized) {
        window.soundManager.init();
    }
}, { once: true });

// Atalho para desenvolvimento
if (typeof console !== 'undefined') {
    console.log('🔊 SoundManager carregado! Use soundManager.testSound() para testar');
}
