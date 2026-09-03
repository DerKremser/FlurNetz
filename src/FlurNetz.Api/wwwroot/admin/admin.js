(() => {
  document.querySelectorAll('[data-copy]').forEach((button) => {
    button.addEventListener('click', async () => {
      const value = button.dataset.copy;
      if (!value || !navigator.clipboard) return;
      await navigator.clipboard.writeText(value);
      const old = button.textContent;
      button.textContent = 'Kopiert';
      window.setTimeout(() => { button.textContent = old; }, 1600);
    });
  });

  const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#$%&*+-=?@_';
  const passwordLength = 24;

  const secureRandomIndex = (maxExclusive) => {
    if (!window.crypto || typeof window.crypto.getRandomValues !== 'function') {
      throw new Error('Eine kryptografisch sichere Browser-Zufallsquelle ist nicht verfügbar.');
    }

    const range = 0x100000000;
    const limit = range - (range % maxExclusive);
    const random = new Uint32Array(1);
    do {
      window.crypto.getRandomValues(random);
    } while (random[0] >= limit);
    return random[0] % maxExclusive;
  };

  const generatePassword = () => {
    let password = '';
    for (let index = 0; index < passwordLength; index += 1) {
      password += alphabet[secureRandomIndex(alphabet.length)];
    }
    return password;
  };

  document.querySelectorAll('[data-password-generator]').forEach((root) => {
    const password = root.querySelector('[data-password-generator-password]');
    const confirmation = root.querySelector('[data-password-generator-confirm]');
    const generateButton = root.querySelector('[data-password-generate]');
    const toggleButton = root.querySelector('[data-password-toggle]');
    const copyButton = root.querySelector('[data-password-copy]');
    const status = root.querySelector('[data-password-status]');
    if (!password || !confirmation || !generateButton || !toggleButton || !copyButton) return;

    const updateButtons = () => {
      copyButton.disabled = password.value.length === 0;
    };

    password.addEventListener('input', updateButtons);
    confirmation.addEventListener('input', updateButtons);

    generateButton.addEventListener('click', () => {
      try {
        const generated = generatePassword();
        password.value = generated;
        confirmation.value = generated;
        password.dispatchEvent(new Event('input', { bubbles: true }));
        confirmation.dispatchEvent(new Event('input', { bubbles: true }));
        if (status) status.textContent = 'Sicheres Passwort erzeugt. Bitte jetzt sicher speichern.';
      } catch (error) {
        if (status) status.textContent = 'Passwort konnte nicht sicher erzeugt werden. Verwende bitte eine eigene Passphrase.';
      }
    });

    toggleButton.addEventListener('click', () => {
      const shouldShow = password.type === 'password';
      password.type = shouldShow ? 'text' : 'password';
      confirmation.type = shouldShow ? 'text' : 'password';
      toggleButton.textContent = shouldShow ? 'Verbergen' : 'Anzeigen';
      toggleButton.setAttribute('aria-pressed', shouldShow ? 'true' : 'false');
    });

    copyButton.addEventListener('click', async () => {
      if (!password.value || !navigator.clipboard) {
        if (status) status.textContent = 'Kopieren ist in diesem Browser nicht verfügbar.';
        return;
      }
      try {
        await navigator.clipboard.writeText(password.value);
        if (status) status.textContent = 'Passwort kopiert. Lege es jetzt sicher ab.';
      } catch (error) {
        if (status) status.textContent = 'Kopieren wurde vom Browser abgelehnt.';
      }
    });

    updateButtons();
  });
})();
