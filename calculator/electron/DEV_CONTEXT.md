# Calc Pro — Dev Context (для продолжения в новой сессии)

> Цель документа: дать модели в новой сессии всё необходимое, чтобы продолжить работу над Calc Pro без переоткрытия дискуссий и без перечитывания всего HTML (~2120 строк).

---

## 1. Стек и сборка

- **Платформа:** Electron 33.4.11 + Node 24.14 / npm 11.9 (Windows).
- **electron-builder 25.1.8** — сборка `portable.exe` + NSIS installer.
- **Один HTML-файл (`calc-pro.html`, ~2120 строк)** содержит CSS + HTML + ванильный JS. Без бандлеров/фреймворков. Open-and-edit подход (как Paint Pro, см. `C:\Drive\Alexey\Paint\paint-pro-electron\DEV_CONTEXT.md`).
- **`main.js`** — frameless transparent window 440×720 (min 280×440), `Menu.setApplicationMenu(null)`, single-instance lock, IPC `save-file` и `window-control`.
- **`preload.js`** — `contextBridge` + `electronAPI.saveFile / windowControl`. `contextIsolation:true`, `nodeIntegration:false`.
- **`build/icon.ico`+`icon.png`** — сгенерированы скриптом `build/make-icon.py` (Pillow). Тёмный калькулятор-стиль с дисплеем `=` и сеткой кнопок (одна оранжевая колонка операторов).

Команды:
```
cd C:\Drive\Alexey\Calculator
npm install
npm start                # dev-запуск
npm run build            # dist\Calc Pro 1.0.0.exe (portable, ~71 MB)
npm run build-installer  # dist\Calc Pro Setup 1.0.0.exe (NSIS, ~78 MB)
```

---

## 2. Визуальный стиль — Apple Liquid Glass

**Полностью задокументирован в `C:\Drive\Alexey\Paint\paint-pro-electron\DEV_CONTEXT.md`** (CSS-токены, фон, glass-панели, blik, shimmer, пузыри, кнопки, ripple, scrollbar, тултипы). Тут отмечены только калькулятор-специфичные решения.

### 2.1 Адаптации в Calc Pro

- **НЕТ внешней панели/карточки** вокруг калькулятора. Кнопки, дисплей и mode-tabs лежат прямо на градиенте + пузырях (как у iPhone-калькулятора Apple).
- **Скруглённое окно**: `body { border-radius: 22px; box-shadow: 0 18px 60px rgba(0,0,0,0.55), 0 0 0 1px rgba(255,255,255,0.06); }`. Работает потому что `main.js` создаёт `frame:false, transparent:true, backgroundColor:'#00000000'`.
- **Кнопки — Apple-style**: круглые/капсулы (`border-radius: 999px`), с `backdrop-filter: blur(48px) saturate(200%)` для скрытия пузырей.
  - Цифры: `rgba(60,60,75,0.55)`, белый текст
  - Операторы (÷ × − + =): `linear-gradient(135deg, #ff9f0a 0%, #ff7a00 100%)`, белый
  - AC, +/-, %: светлое стекло `rgba(165,165,175,0.5)`, **чёрный текст** `#1a1a1a`
  - ⌫ (DEL): красное `rgba(255,91,91,0.45)`, белый
  - Sci-функции: тёмный приглушённый `.btn-sci` со шрифтом `clamp(11px, 2vh, 17px)`
  - Hover: `brightness(1.15) + translateY(-1px)`. Active: `scale(0.94)`. Ripple на клик.
- **Полностью адаптивный layout**. Всё через `clamp()` + `flex/grid 1fr` + `min-height: 0`. НЕТ `aspect-ratio`, фиксированных px на кнопках. Окно ресайзится — кнопки сжимаются.
- **Пузыри приглушены**: opacity снижена с 0.95→0.55, чтобы не перекрывать UI. 12 штук разных цветов (violet/pink/cyan/green).

### 2.2 CSS-токены — те же из DEV_CONTEXT (Paint Pro)

```css
:root {
  --glass-bg, --glass-bg-strong, --glass-bg-soft,
  --glass-border, --glass-border-strong, --glass-highlight,
  --glass-shadow, --glass-inset,
  --blur, --blur-strong,
  --text, --text-dim,
  --accent, --accent-2, --accent-grad, --accent-glow;
}
```

---

## 3. Архитектура

### 3.1 Структура layout

```
body (flex column, height 100vh, border-radius:22px)
├── .bubbles                    (z-index 0)
├── .titlebar (36px)            — кнопки close/min + history toggle (часы), drag region
├── .mode-tabs                  — пилюли Standard / Scientific / Fraction. Активная: accent-grad.
├── .calc-display.panel         — historyLine (мелкая) + mainResult (большой)
│                                 + fractionLine (только в режиме Fraction-toggle)
├── .memory-row (sci only)      — MC MR M+ M- MS
├── .calc-keys                  — grid 4 cols × N rows, gap clamp, content зависит от mode
└── .history-panel (overlay)    — выезжает справа, transform translateX
```

### 3.2 State (один глобальный объект)

```js
const state = {
  // Common
  expression: '', display: '0', historyLine: '',
  result: null, memory: 0,
  mode: 'standard', // 'standard' | 'scientific' | 'fractions'
  angleMode: 'deg', // 'deg' | 'rad' | 'grad'
  fractionMode: false,
  altSet: false,
  justEvaluated: false,
  calcHistory: [],
  hasError: false,

  // Fractions mode
  fracTerms: [{ whole:'', num:'', den:'', op:'' }],
  fracCursor: { term: 0, slot: 'whole' },  // slot ∈ {'whole','num','den'}
  fracResult: null,
  fracResultFrac: null,
};
```

`localStorage`: ключи `calcPro.history` и `calcPro.memory` (восстанавливаются при загрузке).

### 3.3 Парсер выражений (no eval)

- **`tokenise(expr)`** → массив токенов `{type, val}`, типы: `num`, `var` (для x), `func`, `op`, `lparen`, `rparen`.
- **`toRPN(tokens)`** → shunting-yard.
- **`evalRPN(rpn, vars={})`** → стек-вычисление. Поддерживает переменную `x` (наследие удалённого Solver-режима, безвредно).
- **`evaluate(s, vars={})`** → `evalRPN(toRPN(tokenise(s)), vars)`.
- **`expressionToInternal(s)`** — заменяет `× → *`, `÷ → /`, `−` (unicode) → `-`.

Поддерживаемые функции (`applyFunc`):
- Trig: `sin cos tan cot asin acos atan` (с учётом angleMode: deg/rad/grad)
- Hyperbolic: `sinh cosh tanh`
- Log/exp: `ln log exp exp10` (десятичный, натуральный, eˣ, 10ˣ)
- Корни/степени: `sqrt cbrt`, операторы `^` для xʸ
- Прочее: `abs neg fact inv` (1/x)

Операторы (`OPERATORS`): `+ - * / ^ %` с приоритетами и ассоциативностью.

### 3.4 Layouts (массивы кнопок)

- **`STD_LAYOUT`** — 4×5: AC ⌫ % ÷ / 7 8 9 × / 4 5 6 − / 1 2 3 + / +/- 0 . =
- **`SCI_LAYOUT`** — 4×11: 2nd π e AC / ( ) |x| ⌫ / sin cos tan cot / sinh cosh tanh % / x² x³ xʸ log / √ ³√ ʸ√x ln / 1/x eˣ 10ˣ n! / далее цифры + EE a⁄b Rad =
- **`FRAC_LAYOUT`** — 4×7: a/b Mix AC ⌫ / ↑ ↓ % ÷ / ← → Simp × / 7 8 9 − / 4 5 6 + / 1 2 3 = / +/- 0 . D⇄F

Тип кнопки → класс через `classFor(type)`: `digit`, `op`, `util`, `del`, `eq`, `sci`, `frac`.

### 3.5 Режим дробей

Полноценный визуальный ввод с курсором:
- Дробь рендерится **стек-стилем** (numerator над чертой, denominator под) через `.frac-stack > .frac-num + .frac-bar + .frac-den`.
- Активный слот (whole/num/den) подсвечивается синим glow с `@keyframes fracBlink`.
- **Workflow**: набираешь `5` (слот whole) → `a/b` → курсор переходит в `num` → набираешь `1` → `↓` или `a/b` → курсор в `den` → `2` → получается `5 ½`.
- Стрелки `↑↓←→` навигируют между слотами и членами выражения. Физические клавиши тоже работают (см. keydown handler).
- Операторы (`+ − × ÷`) финализируют текущий term, добавляют новый пустой.
- `=` собирает выражение через `termToExpression()` (`(whole+num/den)`) и прогоняет через тот же парсер. Результат конвертируется в дробь Stern-Brocot (`decimalToFraction`, denom ≤ 10000).
- `Simp` — упрощение текущего term (GCD + смешанная дробь).
- `D⇄F` — переключение decimal/fraction отображения результата.

Функции: `pressKeyFrac(k)`, `renderFractionExpr()`, `renderFractionStack()`, `renderFracTerm()`, `evaluateFractions()`, `termToExpression()`, `fracResetAll()`.

### 3.6 История

- `state.calcHistory` — последние 50 вычислений `{expr, result, ts}`.
- Панель выезжает справа кликом по иконке часов в titlebar (`transform: translateX 100% → 0`, transition 0.3s). Поверх всего, кроме titlebar.
- Клик по записи — вставляет результат в текущее выражение.
- Иконка корзины — очищает.
- Хоткей `Ctrl+H` — toggle.

### 3.7 Хоткеи

```js
const KEY_MAP = {
  '0'..'9', '.', ',':'.',
  '+':'+', '-':'−', '*':'×', '/':'÷',
  'Enter':'=', '=':'=',
  'Backspace':'⌫', 'Delete':'AC', 'Escape':'AC',
  '(':'(', ')':')', '%':'%', '^':'^',
};
```
- `Ctrl+H` — история
- `F1` — переключение Standard/Scientific
- В fractions mode: `↑↓←→` навигация, `/` = a/b

---

## 4. Что СДЕЛАНО

- ✅ Стандартный режим (5×4)
- ✅ Научный режим с полным набором: trig (+ обратные, гиперболические), `ln/log`, `√/³√/ʸ√x`, `eˣ/10ˣ`, `n!`, `1/x`, `|x|`, `π/e`, `EE`, `Rad/Deg/Grad`
- ✅ Режим дробей с визуальным стек-вводом и курсором, навигация стрелками
- ✅ Apple-style круглые кнопки + liquid-glass
- ✅ Скруглённое окно
- ✅ Адаптивный layout (clamp + flex/grid 1fr)
- ✅ История (50 записей, localStorage, выезжающая панель)
- ✅ Память M+/M−/MR/MC/MS (только в Scientific)
- ✅ Ripple, пузыри, тултипы
- ✅ Парсер shunting-yard (no eval), переменная `x`
- ✅ Кастомная иконка
- ✅ Portable + NSIS сборки

## 5. Что НЕ СДЕЛАНО (отдельные задачи)

Каждая ~1 сессия:

1. **Конвертер величин** (м/см/км, кг/г, °C/°F, объём, время и т.д.) — новый mode-tab, dropdown'ы выбора единиц, таблица коэффициентов.
2. **HEX/BIN/DEC конвертер (Programmer mode)** — отдельный режим с битовым дисплеем, побитовыми операциями (AND/OR/XOR/NOT/SHL/SHR), переключателями word-size (8/16/32/64).
3. **История в виде выезжающей сверху чёрной плашки** — сейчас панель справа, юзер хотел overlay сверху "из-под меню" (drop-down).
4. **Научные константы** — отдельная панель: c, h, k, Avogadro, gravity, и т.д. с инсертом значения в выражение.

## 6. Пытались, но УБРАЛИ (из-за багов)

- **Решатель уравнений** (Solver) — был расширен парсер переменной `x`, метод bisection, textarea для ввода.
- **Статистика** (Stats) — single/paired режим, mean/σ/s/Σx/Σx²/median + линейная регрессия с r².

**Симптом** (со слов пользователя): "ошибку выдает постоянно, а сверху пишет правильны результат". Display сверху не очищался, состояние `hasError` пропагандировалось, textarea терял фокус при перерисовке.
**Если возвращать**: создавать отдельный pane вместо переиспользования `.calc-display`+`.calc-keys`. Использовать DevTools для отладки. **Перед коммитом тестировать на реальных уравнениях** (`x^2-4=0`, `sin(x)=0.5`, и т.д.).

Парсер всё ещё поддерживает `x` — на случай возвращения. Очистить если решено отказаться навсегда.

---

## 7. Антипаттерны (на которые наступили)

1. **Делегировать большие переписывания агенту без тестирования** — агент писал Solver/Stats, заявлял "JS parses cleanly", но live-баги обнаружились только при использовании. Урок: после любой нетривиальной функциональности нужно либо вручную пройти JS-логику, либо тестировать через `npm start` сразу.

2. **`aspect-ratio: 1/1` + фиксированный px на кнопках** — ломает ресайз окна. Фикс: `width:100%; height:100%` внутри grid-cell + `clamp()` для шрифта.

3. **`grid-column: span 2` на кнопке "0"** — Apple-стиль, но юзер счёл некрасивым. Убрано.

4. **Слабый blur на кнопках** — пузыри светят сквозь. Фикс: `backdrop-filter: blur(48px) saturate(200%)`.

5. **Mode-tabs без `flex:1; min-width:0`** — при добавлении 4-5 табов вылезали за окно.

6. **Слишком плотная компоновка scientific** — изначально были крошечные кнопки `(`, `)` и т.д. рядом с крупными цифрами. Фикс: `.btn-sci` с меньшим font-size + приглушённый фон.

7. **`overflow:hidden` на panel с child-тултипами** — обрезает тултипы (см. Paint Pro DEV_CONTEXT).

8. **`eval()`** — НИКОГДА. Всегда свой парсер.

---

## 8. Файлы

```
C:\Drive\Alexey\Calculator\
├── main.js                 (~57 строк)
├── preload.js              (минимальный)
├── package.json            (electron 33.4.11, electron-builder 25.1.8, target portable+nsis)
├── calc-pro.html           (~2120 строк, всё inline)
├── DEV_CONTEXT.md          (этот файл)
├── build/
│   ├── icon.ico
│   ├── icon.png
│   └── make-icon.py        (Python+Pillow генератор)
├── node_modules/           (gitignored)
└── dist/
    ├── Calc Pro 1.0.0.exe        (portable ~71 MB)
    └── Calc Pro Setup 1.0.0.exe  (NSIS ~78 MB)
```

---

## 9. Быстрый старт новой сессии

1. **Прочитать этот DEV_CONTEXT.md полностью** — ~5 минут.
2. По необходимости открыть `C:\Drive\Alexey\Paint\paint-pro-electron\DEV_CONTEXT.md` для CSS-токенов / паттернов liquid-glass.
3. Не читать `calc-pro.html` целиком (~2120 строк) — использовать `Grep` для поиска конкретных функций/блоков. Ключевые имена:
   - `state` (объект состояния)
   - `STD_LAYOUT`, `SCI_LAYOUT`, `FRAC_LAYOUT`
   - `pressKey(k)`, `pressKeyFrac(k)`
   - `renderKeys()`, `updateDisplay()`, `setMode(mode)`
   - `tokenise`, `toRPN`, `evalRPN`, `evaluate`, `applyFunc`, `OPERATORS`
   - `renderFractionStack`, `renderFracTerm`, `renderFractionExpr`
4. **Для нового режима** (например конвертер):
   - Добавить mode-tab в HTML (~line 860)
   - Добавить в `setMode()` ветку с rendering этого режима
   - Создать собственный `render<Mode>Pane()` который ОЧИЩАЕТ и заполняет `.calc-keys` своим контентом, при этом скрывая или преображая `.calc-display`
   - Не забыть про класс на `.calc-keys` для CSS-изоляции (`.fractions`, `.scientific` уже есть)
   - **Тестировать live через `npm start`** перед сборкой
5. Сборка только когда фича работает: `npm run build && npm run build-installer`.

---

## 10. Связанные документы

- Paint Pro DEV_CONTEXT (источник стиля): `C:\Drive\Alexey\Paint\paint-pro-electron\DEV_CONTEXT.md`
- Apple WWDC 2025 — Liquid Glass design language (iOS 26 / macOS Tahoe 26)
- CSS-tricks — «Getting Clarity on Apple's Liquid Glass»
- DEV.to — «Recreating Apple's Liquid Glass Effect with Pure CSS»
