// Темы оформления: список, выбор и память о выборе. Те же пять тем и те же имена, что
// в Paint Pro («Вид → Тема»). Сами цвета живут в themes.css; здесь только логика, без
// обращения к DOM напрямую, поэтому модуль проверяется в Node (test/themes.test.js).
// Файл одинаковый в calcpro-glass и ios-calculator.
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.CalcTheme = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  const THEMES = [
    { id: 'glass',  name: 'Стеклянная', note: 'Цветной градиент и стекло' },
    { id: 'formal', name: 'Строгая',    note: 'Ровный тёмный фон, приглушённый синий' },
    { id: 'light',  name: 'Светлая',    note: 'Тёмный текст на светлом' },
    { id: 'night',  name: 'Ночная',     note: 'Почти чёрный фон для тёмной комнаты' },
    { id: 'warm',   name: 'Тёплая',     note: 'Охра и кофе вместо синевы' },
  ];
  const DEFAULT = 'glass';
  const STORAGE_KEY = 'calc-theme';

  /** Известная тема или исходная: значение могли поправить руками или принести из старой версии. */
  function normalize(id) {
    return THEMES.some((t) => t.id === id) ? id : DEFAULT;
  }

  /** Тема, которая сейчас стоит на элементе <html>. */
  function current(el) {
    return normalize(el.getAttribute('data-theme') || DEFAULT);
  }

  /**
   * Поставить тему на элемент и запомнить. У исходной атрибута нет, как в Paint.
   * Хранилище может быть недоступно или бросать (приватный режим, запрет) - тема
   * всё равно применяется, просто не переживёт перезапуск.
   */
  function apply(el, id, storage) {
    const theme = normalize(id);
    if (theme === DEFAULT) el.removeAttribute('data-theme');
    else el.setAttribute('data-theme', theme);
    try { if (storage) storage.setItem(STORAGE_KEY, theme); } catch (_) { /* не беда */ }
    return theme;
  }

  /** Вернуть запомненную тему при запуске, ничего не записывая. */
  function restore(el, storage) {
    let saved = null;
    try { saved = storage ? storage.getItem(STORAGE_KEY) : null; } catch (_) { saved = null; }
    const theme = normalize(saved);
    if (theme === DEFAULT) el.removeAttribute('data-theme');
    else el.setAttribute('data-theme', theme);
    return theme;
  }

  function storage() {
    try { return typeof localStorage !== 'undefined' ? localStorage : null; } catch (_) { return null; }
  }

  // В окне приложения тема ставится сразу, пока грузится <head>: иначе окно на
  // мгновение показывалось бы «Стеклянным» и только потом перекрашивалось.
  if (typeof document !== 'undefined' && document.documentElement) {
    restore(document.documentElement, storage());
  }

  return { THEMES, DEFAULT, STORAGE_KEY, normalize, current, apply, restore, storage };
});
