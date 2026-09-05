(() => {
  document.documentElement.classList.add('admin-js');

  const navToggle = document.querySelector('[data-nav-toggle]');
  const navDrawerId = navToggle?.getAttribute('aria-controls');
  const navDrawer = navDrawerId ? document.getElementById(navDrawerId) : null;
  const navMain = document.querySelector('[data-nav-main]');
  const navScrim = document.querySelector('[data-nav-close]');
  const navLinks = navDrawer ? navDrawer.querySelectorAll('[data-admin-nav-link]') : [];
  const narrowViewport = window.matchMedia('(max-width: 720px)');
  let navigationOpen = false;
  let returnFocus = null;

  const updateFragmentNavigation = () => {
    const currentPath = window.location.pathname.toLowerCase();
    const activeFragment = currentPath === '/admin/catalog'
      ? window.location.hash.slice(1).toLowerCase()
      : '';
    const rewardLink = document.querySelector('[data-nav-fragment="rewards"]');
    const catalogLink = document.querySelector('[data-nav-key="catalog"]');
    const rewardsActive = activeFragment === 'rewards';

    if (rewardLink) {
      rewardLink.classList.toggle('active', rewardsActive);
      if (rewardsActive) rewardLink.setAttribute('aria-current', 'page');
      else rewardLink.removeAttribute('aria-current');
    }

    if (catalogLink && currentPath === '/admin/catalog') {
      catalogLink.classList.toggle('active', !rewardsActive);
      if (rewardsActive) catalogLink.removeAttribute('aria-current');
      else catalogLink.setAttribute('aria-current', 'page');
    }
  };

  const setNavigationState = (open, restoreFocus = true) => {
    if (!navToggle || !navDrawer || !navMain || !navScrim || !narrowViewport.matches) return;

    navigationOpen = open;
    navDrawer.classList.toggle('is-open', open);
    navDrawer.setAttribute('aria-hidden', open ? 'false' : 'true');
    navToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    navToggle.setAttribute(
      'aria-label',
      open ? (navToggle.dataset.navLabelClose || '') : (navToggle.dataset.navLabelOpen || ''));
    navMain.inert = open;
    navScrim.hidden = !open;
    document.documentElement.classList.toggle('nav-open', open);

    if (open) {
      returnFocus = document.activeElement;
      const firstLink = navLinks[0];
      if (restoreFocus && firstLink) firstLink.focus();
    } else if (restoreFocus) {
      const focusTarget = returnFocus && typeof returnFocus.focus === 'function'
        ? returnFocus
        : navToggle;
      focusTarget.focus();
      returnFocus = null;
    }
  };

  const syncNavigationMode = () => {
    if (!navToggle || !navDrawer || !navMain || !navScrim) return;

    if (narrowViewport.matches) {
      navDrawer.setAttribute('aria-hidden', navigationOpen ? 'false' : 'true');
      navMain.inert = navigationOpen;
      navScrim.hidden = !navigationOpen;
      return;
    }

    navigationOpen = false;
    navDrawer.classList.remove('is-open');
    navDrawer.removeAttribute('aria-hidden');
    navToggle.setAttribute('aria-expanded', 'false');
    navToggle.setAttribute('aria-label', navToggle.dataset.navLabelOpen || '');
    navMain.inert = false;
    navScrim.hidden = true;
    document.documentElement.classList.remove('nav-open');
  };

  if (navToggle && navDrawer && navMain && navScrim) {
    navToggle.addEventListener('click', () => setNavigationState(!navigationOpen));
    navScrim.addEventListener('click', () => setNavigationState(false));
    navLinks.forEach((link) => {
      link.addEventListener('click', () => {
        if (narrowViewport.matches) setNavigationState(false, false);
      });
    });
    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && navigationOpen && narrowViewport.matches) {
        event.preventDefault();
        setNavigationState(false);
      }
    });
    if (typeof narrowViewport.addEventListener === 'function') {
      narrowViewport.addEventListener('change', syncNavigationMode);
    } else {
      narrowViewport.addListener(syncNavigationMode);
    }
    syncNavigationMode();
  }

  window.addEventListener('hashchange', updateFragmentNavigation);
  updateFragmentNavigation();

  document.querySelectorAll('[data-copy]').forEach((button) => {
    button.addEventListener('click', async () => {
      const value = button.dataset.copy;
      if (!value || !navigator.clipboard) return;
      await navigator.clipboard.writeText(value);
      const old = button.textContent;
      button.textContent = button.dataset.copySuccess || old;
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
        if (status) status.textContent = generateButton.dataset.generatedMessage || '';
      } catch (error) {
        if (status) status.textContent = generateButton.dataset.generationError || '';
      }
    });

    toggleButton.addEventListener('click', () => {
      const shouldShow = password.type === 'password';
      password.type = shouldShow ? 'text' : 'password';
      confirmation.type = shouldShow ? 'text' : 'password';
      toggleButton.textContent = shouldShow
        ? (toggleButton.dataset.hideLabel || '')
        : (toggleButton.dataset.showLabel || '');
      toggleButton.setAttribute('aria-pressed', shouldShow ? 'true' : 'false');
    });

    copyButton.addEventListener('click', async () => {
      if (!password.value || !navigator.clipboard) {
        if (status) status.textContent = copyButton.dataset.copyUnavailable || '';
        return;
      }
      try {
        await navigator.clipboard.writeText(password.value);
        if (status) status.textContent = copyButton.dataset.copySuccess || '';
      } catch (error) {
        if (status) status.textContent = copyButton.dataset.copyDenied || '';
      }
    });

    updateButtons();
  });
})();
