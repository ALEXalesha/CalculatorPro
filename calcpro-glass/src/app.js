/*
 * Calc Pro Glass — page glue. Renders CalcCore state into the DOM and forwards
 * clicks / keys to the engine. All calculator logic lives in calc-core.js.
 */
(function () {
  'use strict';

  const {
    STD_LAYOUT, SCI_LAYOUT, FRAC_LAYOUT, ALT_KEYS,
    formatNumber, formatFraction, displayExpr, escHtml,
    renderFractionStack, renderFractionExpr, fractionExprText,
  } = window.CalcCore;

  const $ = id => document.getElementById(id);
  const DOM = {
    display: $('calcDisplay'),
    historyLine: $('historyLine'),
    mainResult: $('mainResult'),
    fractionLine: $('fractionLine'),
    calcKeys: $('calcKeys'),
    memRow: $('memRow'),
    memPlus: $('memPlus'),
    memMinus: $('memMinus'),
    memRecall: $('memRecall'),
    memClear: $('memClear'),
    memStore: $('memStore'),
    tabStd: $('tabStandard'),
    tabSci: $('tabScientific'),
    tabFrac: $('tabFraction'),
    anglePill: $('anglePill'),
    btnMinimize: $('btnMinimize'),
    btnClose: $('btnClose'),
    btnHistory: $('btnHistory'),
    historyPanel: $('historyPanel'),
    historyList: $('historyList'),
    historyClear: $('historyClear'),
  };

  function safeStorage() {
    try { return window.localStorage; } catch (e) { return null; }
  }

  const calc = window.CalcCore.createCalculator({
    storage: safeStorage(),
    on: {
      change: updateDisplay,
      evaluated: animateResult,
      history: renderHistoryList,
      altset: applyAltSet,
      angle: syncAngleMode,
      mode: onModeChanged,
      memflash: flashMemory,
    },
  });
  const state = calc.state;

  // =================================================================
  // KEYPAD
  // =================================================================
  function classFor(type) {
    switch (type) {
      case 'digit': return 'btn btn-digit';
      case 'op': return 'btn btn-op';
      case 'util': return 'btn btn-util';
      case 'del': return 'btn btn-del';
      case 'eq': return 'btn btn-eq';
      case 'sci': return 'btn btn-sci';
      case 'frac': return 'btn btn-sci btn-frac';
      default: return 'btn';
    }
  }

  function renderKeys() {
    let layout = STD_LAYOUT;
    if (state.mode === 'scientific') layout = SCI_LAYOUT;
    else if (state.mode === 'fractions') layout = FRAC_LAYOUT;
    DOM.calcKeys.classList.toggle('scientific', state.mode === 'scientific');
    DOM.calcKeys.classList.toggle('fractions', state.mode === 'fractions');
    DOM.calcKeys.innerHTML = '';
    for (const item of layout) {
      const b = document.createElement('button');
      b.className = classFor(item.t);
      b.dataset.key = item.k;
      b.dataset.original = item.k;
      b.textContent = item.k;
      if (item.k === 'a⁄b') {
        b.setAttribute('data-tip', 'Fraction divider — same as ÷');
        b.title = 'Fraction divider — same as ÷';
      }
      DOM.calcKeys.appendChild(b);
    }
    applyAltSet();
    syncAngleMode();
  }

  // 2nd flips trig/log/power keys to their inverses.
  function applyAltSet() {
    if (state.mode !== 'scientific') return;
    DOM.calcKeys.querySelectorAll('[data-key]').forEach(b => {
      const original = b.dataset.original || b.dataset.key;
      b.dataset.original = original;
      if (original === 'Rad') return; // label driven by syncAngleMode
      const flipped = ALT_KEYS[original];
      const key = state.altSet && flipped ? flipped : original;
      b.dataset.key = key;
      b.textContent = key;
    });
    const sndBtn = DOM.calcKeys.querySelector('[data-key="2nd"]');
    if (sndBtn) sndBtn.classList.toggle('toggle-on', state.altSet);
  }

  function syncAngleMode() {
    const label = state.angleMode.toUpperCase();
    DOM.anglePill.textContent = label;
    DOM.anglePill.classList.toggle('rad', state.angleMode !== 'deg');
    const radBtn = DOM.calcKeys.querySelector('[data-key="Rad"]');
    if (radBtn) radBtn.textContent = label;
  }

  function onModeChanged() {
    DOM.tabStd.classList.toggle('active', state.mode === 'standard');
    DOM.tabSci.classList.toggle('active', state.mode === 'scientific');
    DOM.tabFrac.classList.toggle('active', state.mode === 'fractions');
    DOM.anglePill.classList.toggle('visible', state.mode === 'scientific');
    DOM.memRow.classList.toggle('visible', state.mode === 'scientific');
    renderKeys();
  }

  // =================================================================
  // DISPLAY
  // =================================================================
  function adjustFontSize(text) {
    const el = DOM.mainResult;
    el.classList.remove('shrink-1', 'shrink-2', 'shrink-3', 'shrink-4');
    if (text.length > 22) el.classList.add('shrink-4');
    else if (text.length > 16) el.classList.add('shrink-3');
    else if (text.length > 12) el.classList.add('shrink-2');
    else if (text.length > 9) el.classList.add('shrink-1');
  }

  function setFractionLine(text) {
    DOM.fractionLine.textContent = text;
    DOM.fractionLine.classList.toggle('visible', text !== '');
  }

  function updateFractionsDisplay() {
    DOM.historyLine.textContent = state.hasError ? '' : (state.fracResult !== null
      ? fractionExprText(state.fracTerms, state.fracCursor)
      : ' ');
    if (state.hasError) {
      DOM.mainResult.innerHTML = escHtml(state.display);
    } else if (state.fracResult !== null) {
      const f = state.fracResultFrac;
      const decimalHtml = '<span class="main-decimal">' + escHtml(formatNumber(state.fracResult)) + '</span>';
      let fractionHtml = '';
      if (f && f.d !== 1) {
        const whole = Math.trunc(f.n / f.d);
        const num = Math.abs(f.n - whole * f.d);
        const sign = f.n < 0 ? '−' : '';
        const wholeStr = whole !== 0 ? (sign + Math.abs(whole)) : (f.n < 0 ? '−' : '');
        fractionHtml = '<span class="main-fraction">' +
          (wholeStr ? '<span class="frac-whole">' + escHtml(wholeStr) + '</span><span class="frac-gap">&nbsp;</span>' : '') +
          renderFractionStack(String(num), String(f.d), false, null) +
          '</span>';
      }
      // D⇄F: fraction view falls back to decimal for integers.
      DOM.mainResult.innerHTML = state.fractionMode ? (fractionHtml || decimalHtml) : decimalHtml;
    } else {
      DOM.mainResult.innerHTML = renderFractionExpr(state.fracTerms, state.fracCursor) ||
        '<span class="frac-placeholder">·</span>';
    }
    setFractionLine('');
    DOM.display.classList.toggle('error-glow', state.hasError);
    adjustFontSize(DOM.mainResult.textContent || '0');
  }

  function updateDisplay() {
    if (state.mode === 'fractions') { updateFractionsDisplay(); return; }

    const showExpr = state.expression && !state.justEvaluated && !state.hasError;
    const displayText = state.hasError
      ? state.display
      : (showExpr ? displayExpr(state.expression) : state.display);

    DOM.historyLine.textContent = state.historyLine || ' ';
    DOM.mainResult.textContent = displayText;
    adjustFontSize(displayText);
    DOM.display.classList.toggle('error-glow', state.hasError);

    const f = state.fractionMode && !state.hasError && state.result !== null ? formatFraction(state.result) : null;
    setFractionLine(f ? '= ' + f : '');
  }

  function animateResult() {
    DOM.mainResult.classList.remove('animate-in');
    void DOM.mainResult.offsetWidth; // restart the CSS animation
    DOM.mainResult.classList.add('animate-in');
  }

  function flashMemory(which) {
    const el = { plus: DOM.memPlus, minus: DOM.memMinus, store: DOM.memStore, clear: DOM.memClear, recall: DOM.memRecall }[which];
    if (!el) return;
    el.classList.add('flash');
    setTimeout(() => el.classList.remove('flash'), 450);
  }

  function spawnRipple(target, clientX, clientY) {
    const rect = target.getBoundingClientRect();
    const size = Math.max(rect.width, rect.height) * 1.4;
    const r = document.createElement('span');
    r.className = 'ripple';
    r.style.cssText = `width:${size}px;height:${size}px;left:${clientX - rect.left}px;top:${clientY - rect.top}px;`;
    if (getComputedStyle(target).position === 'static') target.style.position = 'relative';
    target.appendChild(r);
    setTimeout(() => r.remove(), 700);
  }

  // =================================================================
  // HISTORY PANEL
  // =================================================================
  function renderHistoryList() {
    if (!state.calcHistory.length) {
      DOM.historyList.innerHTML = '<div class="history-empty">No calculations yet</div>';
      return;
    }
    DOM.historyList.innerHTML = '';
    state.calcHistory.forEach((item, i) => {
      const el = document.createElement('div');
      el.className = 'history-item';
      el.dataset.idx = i;
      el.innerHTML = '<div class="history-item-expr"></div><div class="history-item-result"></div>';
      el.querySelector('.history-item-expr').textContent = item.expr;
      const numResult = typeof item.result === 'number' ? item.result : parseFloat(item.result);
      el.querySelector('.history-item-result').textContent =
        '= ' + (isFinite(numResult) ? formatNumber(numResult) : String(item.result));
      DOM.historyList.appendChild(el);
    });
  }

  DOM.historyList.addEventListener('click', e => {
    const item = e.target.closest('.history-item');
    if (item) calc.useHistoryEntry(parseInt(item.dataset.idx, 10));
  });
  DOM.historyClear.addEventListener('click', () => calc.clearHistory());
  DOM.btnHistory.addEventListener('click', () => {
    const open = DOM.historyPanel.classList.toggle('open');
    DOM.btnHistory.classList.toggle('active', open);
  });

  // =================================================================
  // EVENT WIRING
  // =================================================================
  DOM.calcKeys.addEventListener('mousedown', e => {
    const btn = e.target.closest('[data-key]');
    if (btn) spawnRipple(btn, e.clientX, e.clientY);
  });
  DOM.calcKeys.addEventListener('click', e => {
    const btn = e.target.closest('[data-key]');
    if (btn) calc.pressKey(btn.dataset.key);
  });

  DOM.tabStd.addEventListener('click', () => calc.setMode('standard'));
  DOM.tabSci.addEventListener('click', () => calc.setMode('scientific'));
  DOM.tabFrac.addEventListener('click', () => calc.setMode('fraction'));
  DOM.anglePill.addEventListener('click', () => calc.cycleAngleMode());

  DOM.memPlus.addEventListener('click', calc.memAdd);
  DOM.memMinus.addEventListener('click', calc.memSub);
  DOM.memRecall.addEventListener('click', calc.memRecall);
  DOM.memClear.addEventListener('click', calc.memClear);
  DOM.memStore.addEventListener('click', calc.memStore);

  const windowControl = action => {
    if (window.electronAPI && window.electronAPI.windowControl) window.electronAPI.windowControl(action);
    else if (action === 'close') window.close();
  };
  DOM.btnClose.addEventListener('click', () => windowControl('close'));
  DOM.btnMinimize.addEventListener('click', () => windowControl('minimize'));

  // =================================================================
  // KEYBOARD
  // =================================================================
  const KEY_MAP = {
    '0': '0', '1': '1', '2': '2', '3': '3', '4': '4', '5': '5', '6': '6', '7': '7', '8': '8', '9': '9',
    '.': '.', ',': '.',
    '+': '+', '-': '−', '*': '×', '/': '÷',
    'Enter': '=', '=': '=',
    'Backspace': '⌫', 'Delete': 'AC', 'Escape': 'AC',
    '(': '(', ')': ')', '%': '%', '^': '^',
  };
  const FRACTION_KEYS = { ArrowUp: '↑', ArrowDown: '↓', ArrowLeft: '←', ArrowRight: '→', '/': 'a/b' };

  document.addEventListener('keydown', e => {
    if (e.ctrlKey && e.key.toLowerCase() === 'h') {
      e.preventDefault();
      DOM.btnHistory.click();
      return;
    }
    if (e.key === 'F1') { e.preventDefault(); calc.toggleMode(); return; }

    if (state.mode === 'fractions' && FRACTION_KEYS[e.key]) {
      e.preventDefault();
      calc.pressKey(FRACTION_KEYS[e.key]);
      return;
    }

    const k = KEY_MAP[e.key];
    if (k === undefined) return;
    e.preventDefault();
    calc.pressKey(k);
    const btn = DOM.calcKeys.querySelector(`[data-key="${CSS.escape(k)}"]`);
    if (btn) {
      const r = btn.getBoundingClientRect();
      spawnRipple(btn, r.left + r.width / 2, r.top + r.height / 2);
    }
  });

  // =================================================================
  // INIT
  // =================================================================
  renderKeys();
  renderHistoryList();
  updateDisplay();
}());
