// Forgot Password Page Scripts
document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('forgotPasswordForm');
    const emailInput = document.querySelector('input[name="Email"]');
    const submitBtn = document.getElementById('submitBtn');
    const submitText = document.getElementById('submitText');
    const submitLoading = document.getElementById('submitLoading');

    // Email validation regex
    const emailRegex = new RegExp('^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$');

    // Real-time email validation
    emailInput.addEventListener('input', function () {
        validateEmail(this);
    });

    emailInput.addEventListener('blur', function () {
        validateEmail(this);
    });

    // Form submission
    form.addEventListener('submit', function (e) {
        e.preventDefault();

        if (validateForm()) {
            showLoading();

            // Simulate API call
            setTimeout(() => {
                form.submit();
            }, 1000);
        }
    });

    function validateEmail(input) {
        const isValid = emailRegex.test(input.value.trim());

        if (input.value.trim() === '') {
            clearValidation(input);
        } else if (isValid) {
            showValid(input);
        } else {
            showInvalid(input, 'E-mail inválido');
        }

        return isValid || input.value.trim() === '';
    }

    function validateForm() {
        const email = emailInput.value.trim();

        if (email === '') {
            showInvalid(emailInput, 'E-mail é obrigatório');
            emailInput.focus();
            return false;
        }

        if (!validateEmail(emailInput)) {
            emailInput.focus();
            return false;
        }

        return true;
    }

    function showValid(input) {
        input.classList.remove('is-invalid');
        input.classList.add('is-valid');
        const feedback = input.parentNode.parentNode.querySelector('.invalid-feedback');
        if (feedback) feedback.textContent = '';
    }

    function showInvalid(input, message) {
        input.classList.remove('is-valid');
        input.classList.add('is-invalid');
        const feedback = input.parentNode.parentNode.querySelector('.invalid-feedback');
        if (feedback) feedback.textContent = message;
    }

    function clearValidation(input) {
        input.classList.remove('is-valid', 'is-invalid');
        const feedback = input.parentNode.parentNode.querySelector('.invalid-feedback');
        if (feedback) feedback.textContent = '';
    }

    function showLoading() {
        submitBtn.disabled = true;
        submitText.classList.add('d-none');
        submitLoading.classList.remove('d-none');
    }

    function hideLoading() {
        submitBtn.disabled = false;
        submitText.classList.remove('d-none');
        submitLoading.classList.add('d-none');
    }

    // Auto-dismiss alerts after 5 seconds
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        setTimeout(() => {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 5000);
    });
});
