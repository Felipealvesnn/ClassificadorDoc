/**
 * Sistema de Alertas - Extensão para Notificações
 * Integra com o sistema existente de toasts do Bootstrap
 */

// Estender o sistema existente com funcionalidades de alerta
window.AlertNotificationSystem = {

    /**
     * Mostrar notificação de alerta usando Toastr (já instalado)
     */
    showAlertNotification: function (notification) {
        // Usar o Toastr existente configurado no layout
        const type = this.mapAlertTypeToToastr(notification.type);

        // Criar mensagem rica com HTML se necessário
        let message = notification.message || '';

        // Adicionar link de ação se disponível
        if (notification.actionUrl) {
            message += `<br><a href="${notification.actionUrl}" class="btn btn-sm btn-outline-light mt-2" style="text-decoration: none;">Ver Detalhes</a>`;
        }

        // Configurações específicas para alertas
        const alertOptions = {
            timeOut: notification.priority === 'HIGH' ? 15000 : 8000, // Mais tempo para alertas críticos
            extendedTimeOut: 3000,
            closeButton: true,
            progressBar: true,
            allowHtml: true,
            tapToDismiss: notification.priority !== 'HIGH', // Alertas críticos não fecham ao clicar
            preventDuplicates: true
        };

        // Usar o Toastr global já configurado
        if (typeof toastr !== 'undefined') {
            // Aplicar configurações temporariamente
            const originalOptions = { ...toastr.options };
            Object.assign(toastr.options, alertOptions);

            // Mostrar notificação
            toastr[type](message, notification.title);

            // Restaurar configurações originais
            toastr.options = originalOptions;
        } else if (typeof window.showToast !== 'undefined') {
            // Usar a função wrapper existente
            window.showToast[type](notification.title, message);
        } else {
            // Fallback para alert do navegador
            alert(`${notification.title}\n${notification.message}`);
        }

        // Reproduzir som se habilitado
        if (notification.playSound) {
            // Usar o SoundManager unificado
            if (window.soundManager) {
                window.soundManager.playAlertSound(notification.priority);
            }
        }

        // Vibrar dispositivo móvel se suportado
        if (notification.priority === 'HIGH' && 'vibrate' in navigator) {
            navigator.vibrate([200, 100, 200]);
        }
    },

    /**
     * Mapear tipos de alerta para métodos do Toastr
     */
    mapAlertTypeToToastr: function (type) {
        const mapping = {
            'ALERT': 'error',
            'ERROR': 'error',
            'WARNING': 'warning',
            'SUCCESS': 'success',
            'INFO': 'info'
        };
        return mapping[type] || 'info';
    },

    /**
     * Configurar preferências de som (compatibilidade)
     */
    configureSounds: function (enabled = true, volume = 0.7) {
        // Usar o SoundManager unificado
        if (window.soundManager) {
            window.soundManager.configure({
                enabled: enabled,
                volume: volume
            });
        }
    },

    /**
     * Inicializar sistema
     */
    init: function () {
        console.log('Sistema de Alertas inicializado (usando SoundManager unificado)');
    }
};

// Inicializar quando o DOM estiver pronto
document.addEventListener('DOMContentLoaded', function () {
    window.AlertNotificationSystem.init();

    // Adicionar função de teste ao console para desenvolvimento
    if (typeof console !== 'undefined') {
        console.log('🔔 Sistema de Alertas carregado!');
        console.log('Para testar: testAlertSystem() ou showAlertNotification(titulo, mensagem, tipo, prioridade, som, url)');
    }
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
    console.log('🧪 Testando Sistema de Alertas Unificado...');

    // Teste simples com Toastr
    showAlertNotification(
        'Sistema de Alertas Ativo! 🔔',
        'O sistema está monitorando suas métricas em tempo real. Alertas críticos serão exibidos aqui.',
        'SUCCESS',
        'HIGH',
        true,
        '/Alertas'
    );

    // Teste adicional com diferentes tipos
    setTimeout(() => {
        showAlertNotification(
            'Aviso de Monitoramento',
            'Taxa de erro está sendo monitorada. Limite atual: 10%',
            'WARNING',
            'NORMAL',
            true
        );
    }, 2000);

    // Teste de som isolado
    setTimeout(() => {
        if (window.soundManager) {
            console.log('🔊 Testando sons...');
            window.soundManager.testSound();
        }
    }, 4000);
};

// Função para testar performance de som
window.testSoundPerformance = function () {
    console.log('🚀 Teste de Performance de Som...');

    const startTime = performance.now();

    // Teste múltiplos sons rapidamente
    for (let i = 0; i < 5; i++) {
        setTimeout(() => {
            if (window.soundManager) {
                window.soundManager.playNotificationSound(i % 2 === 0 ? 'normal' : 'high');
            }
        }, i * 200);
    }

    setTimeout(() => {
        const endTime = performance.now();
        console.log(`⏱️ Tempo total: ${endTime - startTime}ms`);
    }, 1500);
};
