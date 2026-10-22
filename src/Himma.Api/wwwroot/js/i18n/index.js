import en from './en.js';

// Arabic / English. The Arabic text itself is the key (like gettext), so the code stays
// readable: t('اليوم') shows "اليوم" in Arabic and "Today" in English.
// Placeholders: t('حذف {name}', { name }) -> "Delete Salad".
// Changing the language reloads the page, so every screen (and the server's messages) switch together.

const STORAGE_KEY = 'himma-lang';

function storedLang() {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null; // private mode or blocked storage: fall back to Arabic
  }
}

/** 'ar' (default) or 'en'. */
export const lang = storedLang() === 'en' ? 'en' : 'ar';
export const isRtl = lang === 'ar';

document.documentElement.lang = lang;
document.documentElement.dir = isRtl ? 'rtl' : 'ltr';

export function t(arabic, params) {
  let text = arabic;
  if (lang === 'en') {
    text = en[arabic];
    if (text === undefined) {
      console.warn('Missing English text for:', arabic);
      text = arabic;
    }
  }
  return params ? text.replace(/\{(\w+)\}/g, (match, key) => (key in params ? String(params[key]) : match)) : text;
}

export function setLang(next) {
  try {
    localStorage.setItem(STORAGE_KEY, next);
  } catch {
    // Without storage the choice only lasts for this page load.
  }
  location.reload();
}

/** Name of the other language, written in that language, for the switch button. */
export const otherLanguageName = isRtl ? 'English' : 'العربية';
export const otherLang = isRtl ? 'en' : 'ar';
