/**
 * Social media buttons (Facebook, Instagram, TikTok) in Super App brand colours.
 * Links come from SA.config.social. An empty link renders the button without navigation.
 */
(function (SA) {
  const NETWORKS = [
    { key: 'facebook', label: 'Facebook', svg: '<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false"><path fill="currentColor" d="M9.101 23.691v-7.98H6.627v-3.667h2.474v-1.58c0-4.085 1.848-5.978 5.858-5.978.401 0 .955.042 1.468.103a8.68 8.68 0 0 1 1.141.195v3.325a8.623 8.623 0 0 0-.653-.036 26.805 26.805 0 0 0-.733-.009c-.707 0-1.259.096-1.675.309a1.686 1.686 0 0 0-.679.622c-.258.42-.374.995-.374 1.752v1.297h3.919l-.386 2.103-.287 1.564h-3.246v8.245C19.396 23.238 24 18.179 24 12.044c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.628 3.874 10.35 9.101 11.647Z"/></svg>' },
    { key: 'instagram', label: 'Instagram', svg: '<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true" focusable="false"><rect x="3" y="3" width="18" height="18" rx="5.5"/><circle cx="12" cy="12" r="4.2"/><circle cx="17.4" cy="6.6" r="1.1" fill="currentColor" stroke="none"/></svg>' },
    { key: 'tiktok', label: 'TikTok', svg: '<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false"><path fill="currentColor" d="M12.525.02c1.31-.02 2.61-.01 3.91-.02.08 1.53.63 3.09 1.75 4.17 1.12 1.11 2.7 1.62 4.24 1.79v4.03c-1.44-.05-2.89-.35-4.2-.97-.57-.26-1.1-.59-1.62-.93-.01 2.92.01 5.84-.02 8.75-.08 1.4-.54 2.79-1.35 3.94-1.31 1.92-3.58 3.17-5.91 3.21-1.43.08-2.86-.31-4.08-1.03-2.02-1.19-3.44-3.37-3.65-5.71-.02-.5-.03-1-.01-1.49.18-1.9 1.12-3.72 2.58-4.96 1.66-1.44 3.98-2.13 6.15-1.72.02 1.48-.04 2.96-.04 4.44-.99-.32-2.15-.23-3.02.37-.63.41-1.11 1.04-1.36 1.75-.21.51-.15 1.07-.14 1.61.24 1.64 1.82 3.02 3.5 2.87 1.12-.01 2.19-.66 2.77-1.61.19-.33.4-.67.41-1.06.1-1.79.06-3.57.07-5.36.01-4.03-.01-8.05.02-12.07z"/></svg>' },
  ];

  /** Returns the social button row. variant: 'dark' (on ink backgrounds) | 'light' */
  const render = ({ variant = 'dark', label = 'Follow Super App' } = {}) => {
    const links = SA.config.social || {};
    return `<div class="social social-${variant}">
      <span class="social-label">${label}</span>
      <ul class="social-list">${NETWORKS.map((n) => {
        const url = links[n.key];
        const attrs = url ? `href="${url}" target="_blank" rel="noopener"` : 'role="link" aria-disabled="true" tabindex="0" data-social-pending';
        return `<li><a class="social-btn social-${n.key}" ${attrs} aria-label="Super App on ${n.label}" title="${n.label}">${n.svg}</a></li>`;
      }).join('')}</ul>
    </div>`;
  };

  // Placeholder buttons don't navigate until a link is configured
  document.addEventListener('click', (e) => { if (e.target.closest('[data-social-pending]')) e.preventDefault(); });

  SA.ui.social = { render, NETWORKS };
})(window.SA);
