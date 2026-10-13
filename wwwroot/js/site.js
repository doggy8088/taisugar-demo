document.querySelector('.menu-toggle')?.addEventListener('click', function () {
 const open = document.body.classList.toggle('nav-open'); this.setAttribute('aria-expanded', String(open));
});
document.querySelector('.form-errors')?.focus();
document.querySelectorAll('form[method="post"]').forEach(form => form.addEventListener('submit', () => {
 if (!form.checkValidity()) return;
 const button = form.querySelector('button[type="submit"]');
 if (button) { button.disabled = true; button.textContent = '處理中…'; }
}));
