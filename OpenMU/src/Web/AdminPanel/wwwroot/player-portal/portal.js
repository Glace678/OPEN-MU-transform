const selector = document.querySelector('#culture');
const status = document.querySelector('#status');
const result = document.querySelector('#result');
const recoveryResult = document.querySelector('#recovery-result');
const views = ['register', 'change-password', 'reset-password'];
const params = new URLSearchParams(location.search);
let text = {};
let ready = false;
let busy = false;
let languageRevision = 0;
const allowedCultures = [...selector.options].map(option => option.value);
const requestedCulture = params.get('culture') || navigator.language;
selector.value = allowedCultures.find(value => value.toLowerCase() === requestedCulture.toLowerCase())
  || allowedCultures.find(value => value === requestedCulture.split('-')[0]) || 'en';

function updateButtons() {
  document.querySelectorAll('button[type="submit"]').forEach(button => { button.disabled = busy || !ready; });
  selector.disabled = busy;
  document.querySelectorAll('[data-view]').forEach(button => { button.disabled = busy; });
}

function showStatus(message, error) {
  status.textContent = message;
  status.dataset.error = String(error);
  status.hidden = false;
}

function selectView(view) {
  const selected = views.includes(view) ? view : 'register';
  document.querySelectorAll('[data-panel]').forEach(panel => { panel.hidden = panel.dataset.panel !== selected; });
  document.querySelectorAll('[data-view]').forEach(button => {
    const active = button.dataset.view === selected;
    button.setAttribute('aria-selected', String(active));
    button.tabIndex = active ? 0 : -1;
  });
  const path = location.pathname.includes('/player-portal/') ? location.pathname : '/' + selected;
  history.replaceState(null, '', path + '?view=' + selected + '&culture=' + encodeURIComponent(selector.value));
}

async function loadLanguage() {
  const revision = ++languageRevision;
  ready = false;
  updateButtons();
  try {
    const response = await fetch('/api/registration/text?culture=' + encodeURIComponent(selector.value), { credentials: 'omit', cache: 'no-store' });
    if (!response.ok) throw new Error('text');
    const translated = await response.json();
    if (revision !== languageRevision) return;
    text = translated;
    document.documentElement.lang = selector.value;
    document.querySelectorAll('[data-text]').forEach(element => {
      if (typeof text[element.dataset.text] === 'string') element.textContent = text[element.dataset.text];
    });
    ready = true;
  } catch {
    if (revision === languageRevision) showStatus(text.CannotConnect || 'Cannot connect to the server.', true);
  } finally {
    if (revision === languageRevision) updateButtons();
  }
}

document.querySelectorAll('[data-view]').forEach(button => button.addEventListener('click', () => selectView(button.dataset.view)));
document.querySelector('nav').addEventListener('keydown', event => {
  if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key) || busy) return;
  event.preventDefault();
  const tabs = [...document.querySelectorAll('[data-view]')];
  const current = tabs.indexOf(document.activeElement);
  const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1
    : (current + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
  tabs[next].click();
  tabs[next].focus();
});
selector.addEventListener('change', () => {
  status.hidden = true;
  selectView(document.querySelector('[aria-selected="true"]').dataset.view);
  void loadLanguage();
});
document.querySelectorAll('form').forEach(form => form.addEventListener('submit', async event => {
  event.preventDefault();
  if (!ready || busy || !form.reportValidity()) return;
  const payload = Object.fromEntries(new FormData(form));
  const password = payload.password || payload.newPassword;
  const confirmation = payload.confirmPassword || payload.confirmNewPassword;
  if (password && password !== confirmation) { showStatus(text.PasswordMismatch, true); return; }
  busy = true;
  updateButtons();
  status.hidden = true;
  try {
    const response = await fetch('/api/registration/' + form.dataset.endpoint + '?culture=' + encodeURIComponent(selector.value), {
      method: 'POST', credentials: 'omit', cache: 'no-store', redirect: 'error',
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload),
    });
    const outcome = await response.json();
    if (!response.ok || typeof outcome.success !== 'boolean' || typeof outcome.message !== 'string') throw new Error('response');
    showStatus(outcome.message, !outcome.success);
    if (outcome.success) {
      form.reset();
      if (form.dataset.endpoint === 'create') form.hidden = true;
      if (typeof outcome.recoveryCode === 'string') {
        recoveryResult.value = outcome.recoveryCode;
        result.hidden = false;
        recoveryResult.focus();
        recoveryResult.select();
      } else if (form.dataset.endpoint === 'create') {
        showStatus(outcome.message + ' ' + text.RecoveryUnavailable, false);
      }
    }
  } catch {
    showStatus(text.CannotConnect || 'Cannot connect to the server.', true);
  } finally {
    busy = false;
    updateButtons();
  }
}));

selectView(params.get('view') || location.pathname.split('/').pop());
void loadLanguage();
