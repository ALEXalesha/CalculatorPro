/*
 * Calculator iOS 26 — page glue. Lays out the keypad, renders CalcEngine state
 * and forwards clicks / keys. All calculator logic lives in calc-engine.js.
 */
(function () {
  'use strict';

  const $ = id => document.getElementById(id);

  // ====== WINDOW BUTTONS (preload.js exposes window.windowAPI) ======
  const windowAPI = window.windowAPI;
  $('winMin').addEventListener('click', e => {
    e.preventDefault(); e.stopPropagation();
    if (windowAPI) windowAPI.minimize();
  });
  $('winClose').addEventListener('click', e => {
    e.preventDefault(); e.stopPropagation();
    if (windowAPI) windowAPI.close(); else window.close();
  });

  // ====== ADAPTIVE BUTTON SIZE ======
  // Buttons must stay round: cell = min(width-based, height-based) size.
  const calcEl = $('calc');
  const MIN_DISPLAY_BASIC = 140;
  const MIN_DISPLAY_SCI = 110;

  function fit() {
    const isSci = calcEl.classList.contains('scientific');
    const totalW = calcEl.clientWidth;
    const totalH = calcEl.clientHeight;

    const padX = 20, padY = 22;
    const gap = 10, sciGap = 6;
    const sideKeypadPad = 20;
    const headerH = 38;
    const sciToggleH = 32 + 6;
    const MIN_DISPLAY = isSci ? MIN_DISPLAY_SCI : MIN_DISPLAY_BASIC;

    const cellByWidth = Math.floor((totalW - padX - sideKeypadPad - 3 * gap) / 4);
    // The sci pad spans exactly the keypad width: 5*sciCell + 4*sciGap == 4*cell + 3*gap
    const sciCellFromCell = c => Math.floor((4 * c + 3 * gap - 4 * sciGap) / 5);

    // Height left for keypad (+ sci pad) after reserving the display.
    const remainingH = totalH - padY - headerH - sciToggleH - MIN_DISPLAY;
    // sci: remainingH = (4*cell + 36) + (5*cell + 40) ⇒ cell = (remainingH - 76) / 9
    const cellByHeight = isSci
      ? Math.floor((remainingH - 76) / 9)
      : Math.floor((remainingH - 40) / 5);

    const cell = Math.max(34, Math.min(cellByWidth, cellByHeight));
    const sciCell = sciCellFromCell(cell);

    const sciH = isSci ? 5 * sciCell + 4 * sciGap + 6 : 0;
    const keypadH = 5 * cell + 4 * gap;
    const actualDisplayH = totalH - padY - headerH - sciToggleH - keypadH - sciH;

    const exprFz = Math.max(13, Math.min(isSci ? 18 : 22, Math.floor(actualDisplayH * 0.18)));
    const resultByDisplay = Math.max(28, Math.floor((actualDisplayH - exprFz - 10) * 0.88));
    const resultByCell = Math.floor(cell * (isSci ? 0.85 : 0.95));
    const resultFz = Math.min(72, Math.max(28, Math.min(resultByDisplay, resultByCell)));

    const root = document.documentElement.style;
    root.setProperty('--cell', cell + 'px');
    root.setProperty('--sci-cell', sciCell + 'px');
    root.setProperty('--result-fz', resultFz + 'px');
    root.setProperty('--expr-fz', exprFz + 'px');
    root.setProperty('--min-display', MIN_DISPLAY + 'px');
  }

  new ResizeObserver(fit).observe(document.body);
  window.addEventListener('load', fit);

  // ====== ENGINE ======
  function safeStorage() {
    try { return window.localStorage; } catch (e) { return null; }
  }

  const expressionEl = $('expression');
  const resultEl = $('result');
  const menu = $('menu');
  const clearBtn = $('clearBtn');
  const btn2nd = $('btn2nd');
  const btnRad = $('btnRad');
  const trigButtons = [['btnSin', 'sin'], ['btnCos', 'cos'], ['btnTan', 'tan']].map(([id, name]) => [$(id), name]);
  const opButtons = document.querySelectorAll('.btn.op');

  const engine = window.CalcEngine.createEngine({ storage: safeStorage(), onChange: updateDisplay });
  const state = engine.state;

  function updateDisplay() {
    expressionEl.textContent = state.lastExpression;
    resultEl.textContent = state.currentExpr;
    const len = state.currentExpr.length;
    resultEl.classList.toggle('small', len > 11);
    resultEl.classList.toggle('tiny', len > 16);
    clearBtn.textContent = engine.clearLabel();

    const active = engine.activeOperator();
    opButtons.forEach(b => b.classList.toggle('active', b.dataset.op === active));

    btn2nd.classList.toggle('active2nd', state.isSecond);
    for (const [btn, name] of trigButtons) {
      btn.innerHTML = state.isSecond ? name + '<span class="sup">-1</span>' : name;
    }
    btnRad.textContent = state.isRad ? 'Deg' : 'Rad';

    requestAnimationFrame(() => {
      resultEl.scrollLeft = resultEl.scrollWidth;
      expressionEl.scrollLeft = expressionEl.scrollWidth;
    });
  }

  document.querySelectorAll('.btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const { num, op, act } = btn.dataset;
      if (num !== undefined) engine.inputNum(num);
      else if (op !== undefined) engine.inputOp(op);
      else if (act === 'clear') engine.pressClear();
      else if (act === 'back') engine.backspace();
      else if (act === 'percent') engine.applyPercent();
      else if (act === 'negate') engine.negate();
      else if (act) engine.sci(act);
    });
  });

  // ====== MENU (basic / scientific / history) ======
  function setScientific(on) {
    calcEl.classList.toggle('scientific', on);
    document.querySelectorAll('.menu-item').forEach(i => {
      if (i.dataset.mode) i.classList.toggle('active', i.dataset.mode === (on ? 'scientific' : 'basic'));
    });
    fit();
  }

  $('menuBtn').addEventListener('click', e => {
    e.stopPropagation();
    menu.classList.toggle('open');
  });
  document.addEventListener('click', () => menu.classList.remove('open'));

  document.querySelectorAll('.menu-item').forEach(item => {
    item.addEventListener('click', () => {
      menu.classList.remove('open');
      if (item.dataset.action === 'history') { openHistoryPanel(); return; }
      if (item.dataset.mode) setScientific(item.dataset.mode === 'scientific');
    });
  });

  $('sciToggle').addEventListener('click', () => setScientific(!calcEl.classList.contains('scientific')));

  // ====== HISTORY PANEL ======
  const historyPanel = $('historyPanel');
  const historyList = $('historyList');
  const historyClearBtn = $('historyClear');

  function escapeHTML(s) {
    return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  }

  function renderHistory() {
    const history = state.history;
    if (history.length === 0) {
      historyList.innerHTML = '<div class="history-empty">История пуста.<br>Завершайте вычисления знаком =, чтобы они сохранялись здесь.</div>';
      historyClearBtn.disabled = true;
      return;
    }
    historyClearBtn.disabled = false;
    // newest on top
    historyList.innerHTML = history.slice().reverse().map((h, i) => `
      <div class="history-item" data-idx="${history.length - 1 - i}">
        <div class="h-expr">${escapeHTML(h.expr)} =</div>
        <div class="h-result">${escapeHTML(h.result)}</div>
      </div>`).join('');
  }

  historyList.addEventListener('click', e => {
    const item = e.target.closest('.history-item');
    if (!item) return;
    engine.useHistoryEntry(+item.dataset.idx);
    closeHistoryPanel();
  });

  function openHistoryPanel() {
    renderHistory();
    historyPanel.classList.add('open');
  }
  function closeHistoryPanel() {
    historyPanel.classList.remove('open');
  }

  $('historyBack').addEventListener('click', e => { e.stopPropagation(); closeHistoryPanel(); });
  historyClearBtn.addEventListener('click', e => {
    e.stopPropagation();
    engine.clearHistory();
    renderHistory();
  });

  // ====== KEYBOARD ======
  document.addEventListener('keydown', e => {
    // With the history panel open only Esc (close) is handled.
    if (historyPanel.classList.contains('open')) {
      if (e.key === 'Escape') closeHistoryPanel();
      return;
    }
    if (/^\d$/.test(e.key)) engine.inputNum(e.key);
    else if (e.key === '.' || e.key === ',') engine.inputNum('.');
    else if (['+', '-', '*', '/'].includes(e.key)) engine.inputOp(e.key);
    else if (e.key === 'Enter' || e.key === '=') { e.preventDefault(); engine.inputOp('='); }
    else if (e.key === 'Backspace') engine.backspace();
    else if (e.key === 'Escape') engine.clearAll();
    else if (e.key === '%') engine.applyPercent();
  });

  // ====== HORIZONTAL SCROLL (wheel + drag) for long numbers ======
  function makeScrollableX(el) {
    const updateOverflowState = () => el.classList.toggle('overflowing', el.scrollWidth > el.clientWidth + 1);
    new ResizeObserver(updateOverflowState).observe(el);
    new MutationObserver(updateOverflowState).observe(el, { childList: true, characterData: true, subtree: true });
    setTimeout(updateOverflowState, 0);

    el.addEventListener('wheel', e => {
      if (el.scrollWidth <= el.clientWidth) return;
      const delta = Math.abs(e.deltaX) > Math.abs(e.deltaY) ? e.deltaX : e.deltaY;
      if (delta === 0) return;
      e.preventDefault();
      el.scrollLeft += delta;
    }, { passive: false });

    let isDown = false, startX = 0, startScroll = 0;
    el.addEventListener('mousedown', e => {
      if (e.button !== 0 || el.scrollWidth <= el.clientWidth) return;
      isDown = true;
      startX = e.clientX;
      startScroll = el.scrollLeft;
      el.classList.add('dragging');
    });
    document.addEventListener('mousemove', e => {
      if (!isDown) return;
      el.scrollLeft = startScroll - (e.clientX - startX);
      e.preventDefault();
    });
    document.addEventListener('mouseup', () => {
      if (!isDown) return;
      isDown = false;
      el.classList.remove('dragging');
    });
  }

  makeScrollableX(resultEl);
  makeScrollableX(expressionEl);

  updateDisplay();
  fit();
}());
