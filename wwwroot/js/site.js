const menuButton = document.querySelector('.menu-toggle');
const sidebar = document.querySelector('.sidebar');
const mobile = window.matchMedia('(max-width: 760px)');
function setNavigation(open, restoreFocus = false) {
 document.body.classList.toggle('nav-open', open);
 menuButton?.setAttribute('aria-expanded', String(open));
 if (sidebar) sidebar.inert = mobile.matches && !open;
 if (open && mobile.matches) sidebar?.querySelector('button')?.focus();
 if (!open && restoreFocus) menuButton?.focus();
}
menuButton?.addEventListener('click', () => setNavigation(!document.body.classList.contains('nav-open')));
document.querySelector('.sidebar-close')?.addEventListener('click', () => setNavigation(false, true));
document.querySelector('.nav-backdrop')?.addEventListener('click', () => setNavigation(false, true));
mobile.addEventListener('change', () => setNavigation(false));
document.addEventListener('keydown', event => {
 if (!mobile.matches || !document.body.classList.contains('nav-open')) return;
 if (event.key === 'Escape') { setNavigation(false, true); return; }
 if (event.key === 'Tab') {
  const items = [...sidebar.querySelectorAll('a,button')];
  const first = items[0], last = items.at(-1);
  if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
 }
});
setNavigation(false);
document.querySelector('.form-errors')?.focus();
document.querySelectorAll('form[method="post"]').forEach(form => form.addEventListener('submit', () => {
 if (!form.checkValidity()) return;
 const button = form.querySelector('button[type="submit"]');
 if (button) { button.disabled = true; button.textContent = '處理中…'; }
}));
