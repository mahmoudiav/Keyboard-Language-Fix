/*
 * Keyboard Language Fix — layout tables
 *
 * Every table maps a *physical key on a US QWERTY keyboard* to the character
 * that the same physical key produces under the target keyboard layout.
 *
 *   base  : the un-shifted layer  ("q" -> "ض")
 *   shift : the shifted layer     ("Q" -> "َ")
 *
 * A value may be more than one character (e.g. the Arabic "b" key produces
 * the ligature "لا"); the converter handles those greedily in both directions.
 *
 * `shiftFallback: true` means the layout has no real case distinction
 * (Hebrew, and for keys we do not model) so an upper-case Latin letter is
 * folded down to the base layer instead of being left untouched.
 */
(function (root) {
  'use strict';

  /** Arabic — Windows "Arabic (101)", the default on Windows/Linux/Android. */
  const ARABIC = {
    id: 'ar',
    name: 'Arabic (101)',
    nameLocal: 'العربية (101)',
    rtl: true,
    shiftFallback: false,
    // Arabic + Arabic Supplement + Presentation Forms
    script: /[؀-ۿݐ-ݿࢠ-ࣿﭐ-﷿ﹰ-﻿]/,
    base: {
      '`': 'ذ',
      'q': 'ض', 'w': 'ص', 'e': 'ث', 'r': 'ق', 't': 'ف', 'y': 'غ',
      'u': 'ع', 'i': 'ه', 'o': 'خ', 'p': 'ح', '[': 'ج', ']': 'د',
      'a': 'ش', 's': 'س', 'd': 'ي', 'f': 'ب', 'g': 'ل', 'h': 'ا',
      'j': 'ت', 'k': 'ن', 'l': 'م', ';': 'ك', "'": 'ط',
      'z': 'ئ', 'x': 'ء', 'c': 'ؤ', 'v': 'ر', 'b': 'لا', 'n': 'ى',
      'm': 'ة', ',': 'و', '.': 'ز', '/': 'ظ'
    },
    shift: {
      '~': 'ّ',
      'Q': 'َ', 'W': 'ً', 'E': 'ُ', 'R': 'ٌ', 'T': 'لإ', 'Y': 'إ',
      'U': '‘', 'I': '÷', 'O': '×', 'P': '؛', '{': '<', '}': '>',
      'A': 'ِ', 'S': 'ٍ', 'D': ']', 'F': '[', 'G': 'لأ', 'H': 'أ',
      'J': 'ـ', 'K': '،', 'L': '/',
      'Z': '~', 'X': 'ْ', 'C': '}', 'V': '{', 'B': 'لآ', 'N': 'آ',
      'M': '’', '<': ',', '>': '.', '?': '؟'
    }
  };

  /** Persian / Farsi — the standard Iranian Windows layout. */
  const PERSIAN = {
    id: 'fa',
    name: 'Persian (Farsi)',
    nameLocal: 'فارسی',
    rtl: true,
    shiftFallback: false,
    script: /[؀-ۿﭐ-﷿ﹰ-﻿]/,
    base: {
      '`': '‍',
      '1': '۱', '2': '۲', '3': '۳', '4': '۴', '5': '۵',
      '6': '۶', '7': '۷', '8': '۸', '9': '۹', '0': '۰',
      'q': 'ض', 'w': 'ص', 'e': 'ث', 'r': 'ق', 't': 'ف', 'y': 'غ',
      'u': 'ع', 'i': 'ه', 'o': 'خ', 'p': 'ح', '[': 'ج', ']': 'چ',
      'a': 'ش', 's': 'س', 'd': 'ی', 'f': 'ب', 'g': 'ل', 'h': 'ا',
      'j': 'ت', 'k': 'ن', 'l': 'م', ';': 'ک', "'": 'گ',
      'z': 'ظ', 'x': 'ط', 'c': 'ز', 'v': 'ر', 'b': 'ذ', 'n': 'د',
      'm': 'پ', ',': 'و', '.': '.', '/': '/'
    },
    shift: {
      '@': '٬', '#': '٫', '$': '﷼', '%': '٪', '^': '×', '&': '،',
      'Q': 'ْ', 'W': 'ٌ', 'E': 'ٍ', 'R': 'ً', 'T': 'ُ', 'Y': 'ِ',
      'U': 'َ', 'I': 'ّ', 'O': ']', 'P': '[', '{': '}', '}': '{',
      'A': 'ؤ', 'S': 'ئ', 'D': 'ي', 'F': 'إ', 'G': 'أ', 'H': 'آ',
      'J': 'ة', 'K': '»', 'L': '«', ':': ':', '"': '؛',
      'Z': 'ك', 'X': 'ٓ', 'C': 'ژ', 'V': 'ٰ', 'B': '‌', 'N': 'ٔ',
      'M': 'ء', '<': '>', '>': '<', '?': '؟'
    }
  };

  /** Russian — ЙЦУКЕН. */
  const RUSSIAN = {
    id: 'ru',
    name: 'Russian (ЙЦУКЕН)',
    nameLocal: 'Русская',
    rtl: false,
    shiftFallback: false,
    script: /[Ѐ-ӿ]/,
    base: {
      '`': 'ё',
      'q': 'й', 'w': 'ц', 'e': 'у', 'r': 'к', 't': 'е', 'y': 'н',
      'u': 'г', 'i': 'ш', 'o': 'щ', 'p': 'з', '[': 'х', ']': 'ъ',
      'a': 'ф', 's': 'ы', 'd': 'в', 'f': 'а', 'g': 'п', 'h': 'р',
      'j': 'о', 'k': 'л', 'l': 'д', ';': 'ж', "'": 'э',
      'z': 'я', 'x': 'ч', 'c': 'с', 'v': 'м', 'b': 'и', 'n': 'т',
      'm': 'ь', ',': 'б', '.': 'ю', '/': '.'
    },
    shift: {
      '~': 'Ё',
      '@': '"', '#': '№', '$': ';', '^': ':', '&': '?',
      'Q': 'Й', 'W': 'Ц', 'E': 'У', 'R': 'К', 'T': 'Е', 'Y': 'Н',
      'U': 'Г', 'I': 'Ш', 'O': 'Щ', 'P': 'З', '{': 'Х', '}': 'Ъ',
      'A': 'Ф', 'S': 'Ы', 'D': 'В', 'F': 'А', 'G': 'П', 'H': 'Р',
      'J': 'О', 'K': 'Л', 'L': 'Д', ':': 'Ж', '"': 'Э',
      'Z': 'Я', 'X': 'Ч', 'C': 'С', 'V': 'М', 'B': 'И', 'N': 'Т',
      'M': 'Ь', '<': 'Б', '>': 'Ю', '?': ','
    }
  };

  /** Hebrew — the standard Israeli layout. */
  const HEBREW = {
    id: 'he',
    name: 'Hebrew',
    nameLocal: 'עברית',
    rtl: true,
    shiftFallback: true,
    script: /[֐-׿יִ-ﭏ]/,
    base: {
      'q': '/', 'w': "'", 'e': 'ק', 'r': 'ר', 't': 'א', 'y': 'ט',
      'u': 'ו', 'i': 'ן', 'o': 'ם', 'p': 'פ', '[': ']', ']': '[',
      'a': 'ש', 's': 'ד', 'd': 'ג', 'f': 'כ', 'g': 'ע', 'h': 'י',
      'j': 'ח', 'k': 'ל', 'l': 'ך', ';': 'ף', "'": ',',
      'z': 'ז', 'x': 'ס', 'c': 'ב', 'v': 'ה', 'b': 'נ', 'n': 'מ',
      'm': 'צ', ',': 'ת', '.': 'ץ', '/': '.'
    },
    shift: {
      '{': '}', '}': '{', '<': '>', '>': '<', '?': '?'
    }
  };

  /** Greek. */
  const GREEK = {
    id: 'el',
    name: 'Greek',
    nameLocal: 'Ελληνικά',
    rtl: false,
    shiftFallback: false,
    // Sigma has one upper case form for two keys (σ and final ς), so "Σ"
    // reverses to the first of them. This is a property of Greek, not a typo.
    ambiguous: ['Σ'],
    script: /[Ͱ-Ͽἀ-῿]/,
    base: {
      'q': ';', 'w': 'ς', 'e': 'ε', 'r': 'ρ', 't': 'τ', 'y': 'υ',
      'u': 'θ', 'i': 'ι', 'o': 'ο', 'p': 'π',
      'a': 'α', 's': 'σ', 'd': 'δ', 'f': 'φ', 'g': 'γ', 'h': 'η',
      'j': 'ξ', 'k': 'κ', 'l': 'λ', ';': '΄',
      'z': 'ζ', 'x': 'χ', 'c': 'ψ', 'v': 'ω', 'b': 'β', 'n': 'ν',
      'm': 'μ'
    },
    shift: {
      'Q': ':', 'W': 'Σ', 'E': 'Ε', 'R': 'Ρ', 'T': 'Τ', 'Y': 'Υ',
      'U': 'Θ', 'I': 'Ι', 'O': 'Ο', 'P': 'Π',
      'A': 'Α', 'S': 'Σ', 'D': 'Δ', 'F': 'Φ', 'G': 'Γ', 'H': 'Η',
      'J': 'Ξ', 'K': 'Κ', 'L': 'Λ', ':': '¨',
      'Z': 'Ζ', 'X': 'Χ', 'C': 'Ψ', 'V': 'Ω', 'B': 'Β', 'N': 'Ν',
      'M': 'Μ'
    }
  };

  const LAYOUTS = [ARABIC, PERSIAN, RUSSIAN, HEBREW, GREEK];

  const byId = Object.create(null);
  for (const l of LAYOUTS) byId[l.id] = l;

  const api = {
    LAYOUTS,
    getLayout: (id) => byId[id] || null,
    listLayouts: () => LAYOUTS.map((l) => ({
      id: l.id, name: l.name, nameLocal: l.nameLocal, rtl: l.rtl
    }))
  };

  root.KLF = Object.assign(root.KLF || {}, { layouts: api });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof globalThis !== 'undefined' ? globalThis : this);
