/**
 * Sistema de Alertas - Extensão para Notificações
 * Integra com o sistema existente de toasts do Bootstrap
 */

// Estender o sistema existente com funcionalidades de alerta
window.AlertNotificationSystem = {
    // Configurações de som
    soundEnabled: true,
    soundVolume: 0.7,

    /**
     * Reproduzir som para alertas
     */
    playAlertSound: function (priority = 'NORMAL') {
        if (!this.soundEnabled) return;

        try {
            // Criar contexto de áudio se não existir
            if (!this.audioContext) {
                this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
            }

            // Sons diferentes baseados na prioridade
            const frequencies = {
                'HIGH': [800, 600, 400], // Som de emergência
                'NORMAL': [500, 400],    // Som padrão
                'LOW': [300]             // Som suave
            };

            const freq = frequencies[priority] || frequencies['NORMAL'];
            this.playToneSequence(freq);

        } catch (error) {
            console.log('Áudio não suportado ou bloqueado:', error);
        }
    },

    /**
     * Reproduzir sequência de tons
     */
    playToneSequence: function (frequencies) {
        frequencies.forEach((freq, index) => {
            setTimeout(() => {
                this.playTone(freq, 0.2);
            }, index * 300);
        });
    },

    /**
     * Reproduzir tom específico
     */
    playTone: function (frequency, duration) {
        const oscillator = this.audioContext.createOscillator();
        const gainNode = this.audioContext.createGain();

        oscillator.connect(gainNode);
        gainNode.connect(this.audioContext.destination);

        oscillator.frequency.value = frequency;
        oscillator.type = 'sine';

        gainNode.gain.setValueAtTime(0, this.audioContext.currentTime);
        gainNode.gain.linearRampToValueAtTime(this.soundVolume, this.audioContext.currentTime + 0.01);
        gainNode.gain.exponentialRampToValueAtTime(0.01, this.audioContext.currentTime + duration);

        oscillator.start(this.audioContext.currentTime);
        oscillator.stop(this.audioContext.currentTime + duration);
    },

    /**
     * Mostrar notificação de alerta usando o sistema existente
     */
    showAlertNotification: function (notification) {
        // Usar a função existente do site.js
        const type = this.mapAlertTypeToBootstrap(notification.type);

        // Criar mensagem rica
        let message = `<strong>${notification.title}</strong>`;
        if (notification.message) {
            message += `<br><small>${notification.message}</small>`;
        }

        // Adicionar link de ação se disponível
        if (notification.actionUrl) {
            message += `<br><a href="${notification.actionUrl}" class="btn btn-sm btn-outline-${type} mt-2">Ver Detalhes</a>`;
        }

        // Usar sistema existente com duração maior para alertas
        const duration = notification.priority === 'HIGH' ? 10000 : 6000;

        if (typeof showModernNotification === 'function') {
            showModernNotification(message, type, duration);
        } else {
            // Fallback para alert do navegador
            alert(`${notification.title}\n${notification.message}`);
        }

        // Reproduzir som se habilitado
        if (notification.playSound) {
            this.playAlertSound(notification.priority);
        }

        // Vibrar dispositivo móvel se suportado
        if (notification.priority === 'HIGH' && 'vibrate' in navigator) {
            navigator.vibrate([200, 100, 200]);
        }
    },

    /**
     * Mapear tipos de alerta para classes do Bootstrap
     */
    mapAlertTypeToBootstrap: function (type) {
        const mapping = {
            'ALERT': 'danger',
            'ERROR': 'danger',
            'WARNING': 'warning',
            'SUCCESS': 'success',
            'INFO': 'info'
        };
        return mapping[type] || 'primary';
    },

    /**
     * Configurar preferências de som
     */
    configureSounds: function (enabled = true, volume = 0.7) {
        this.soundEnabled = enabled;
        this.soundVolume = Math.max(0, Math.min(1, volume));

        // Salvar preferência no localStorage
        localStorage.setItem('alertSoundEnabled', enabled);
        localStorage.setItem('alertSoundVolume', volume);
    },

    /**
     * Inicializar sistema
     */
    init: function () {
        // Carregar preferências salvas
        const savedSoundEnabled = localStorage.getItem('alertSoundEnabled');
        const savedSoundVolume = localStorage.getItem('alertSoundVolume');

        if (savedSoundEnabled !== null) {
            this.soundEnabled = savedSoundEnabled === 'true';
        }

        if (savedSoundVolume !== null) {
            this.soundVolume = parseFloat(savedSoundVolume);
        }

        console.log('Sistema de Alertas inicializado');
    }
};

// Inicializar quando o DOM estiver pronto
document.addEventListener('DOMContentLoaded', function () {
    window.AlertNotificationSystem.init();
});

// Função global para disparar alertas (compatibilidade)
window.showAlertNotification = function (title, message, type = 'INFO', priority = 'NORMAL', playSound = false, actionUrl = null) {
    const notification = {
        title: title,
        message: message,
        type: type,
        priority: priority,
        playSound: playSound,
        actionUrl: actionUrl
    };

    window.AlertNotificationSystem.showAlertNotification(notification);
};

// Exemplo de teste (remover em produção)
window.testAlertSystem = function () {
    showAlertNotification(
        'Teste de Alerta',
        'Este é um teste do sistema de alertas com som.',
        'ALERT',
        'HIGH',
        true,
        '/Alertas'
    );
};
