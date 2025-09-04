// JavaScript para página de login
document.addEventListener('DOMContentLoaded', function () {
    const form = document.querySelector('.login-form');
    const emailInput = document.querySelector('input[name="Email"]');
    const passwordInput = document.querySelector('input[name="Password"]');
    const submitBtn = document.querySelector('.login-btn');
    const btnText = submitBtn?.querySelector('.btn-text');
    const btnSpinner = submitBtn?.querySelector('.btn-spinner');

    // Validação em tempo real
    if (emailInput) emailInput.addEventListener('blur', validateEmail);
    if (passwordInput) passwordInput.addEventListener('input', validatePassword);

    // Prevenir múltiplos submits
    if (form) {
        form.addEventListener('submit', function (e) {
            if (submitBtn.disabled) {
                e.preventDefault();
                return false;
            }

            // Mostrar loading
            submitBtn.disabled = true;
            if (btnText) btnText.classList.add('d-none');
            if (btnSpinner) btnSpinner.classList.remove('d-none');

            // Timeout de segurança (caso a resposta demore)
            setTimeout(() => {
                if (submitBtn.disabled) {
                    submitBtn.disabled = false;
                    if (btnText) btnText.classList.remove('d-none');
                    if (btnSpinner) btnSpinner.classList.add('d-none');
                }
            }, 30000);
        });
    }

    // Limpar loading se houver erro
    if (document.querySelector('.alert-danger')) {
        submitBtn.disabled = false;
        if (btnText) btnText.classList.remove('d-none');
        if (btnSpinner) btnSpinner.classList.add('d-none');
    }
});

function validateEmail() {
    const email = document.querySelector('input[name="Email"]');
    const emailPattern = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

    if (email.value && !emailPattern.test(email.value)) {
        email.classList.add('is-invalid');
        return false;
    } else {
        email.classList.remove('is-invalid');
        email.classList.add('is-valid');
        return true;
    }
}

function validatePassword() {
    const password = document.querySelector('input[name="Password"]');

    if (password.value.length >= 6) {
        password.classList.remove('is-invalid');
        password.classList.add('is-valid');
        return true;
    }
    return false;
}

function togglePassword() {
    const passwordInput = document.querySelector('input[name="Password"]');
    const toggleIcon = document.getElementById('togglePasswordIcon');
    const toggleBtn = document.getElementById('passwordToggle');

    if (passwordInput.type === 'password') {
        passwordInput.type = 'text';
        toggleIcon.className = 'fas fa-eye-slash';
        toggleBtn.setAttribute('aria-label', 'Ocultar senha');
    } else {
        passwordInput.type = 'password';
        toggleIcon.className = 'fas fa-eye';
        toggleBtn.setAttribute('aria-label', 'Mostrar senha');
    }
}

// Melhorar acessibilidade
document.addEventListener('keydown', function (e) {
    // Enter no email vai para senha
    if (e.target.name === 'Email' && e.key === 'Enter') {
        e.preventDefault();
        const passwordField = document.querySelector('input[name="Password"]');
        if (passwordField) passwordField.focus();
    }
});
