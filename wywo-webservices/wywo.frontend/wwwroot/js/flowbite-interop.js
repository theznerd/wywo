window.flowbiteInterop = {
    initializeFlowbite: function () {
        return initFlowbite();
    }
};

window.scrollPageToBottom = function () {
    requestAnimationFrame(() => {
        window.scrollTo(0, document.documentElement.scrollHeight);
    });
};

// Format an input element's value to (nnn) nnn-nnnn as the user types.
// Usage in markup: <input oninput="formatPhoneInput(this)" inputmode="numeric" maxlength="14" ... />
window.formatPhoneInput = function (el) {
    if (!el) return;
    // Keep only digits and limit to 10
    const digits = (el.value || '').replace(/\D/g, '').slice(0, 10);

    let formatted = '';
    if (digits.length === 0) {
        formatted = '';
    } else if (digits.length <= 3) {
        formatted = `(${digits}`;
    } else if (digits.length <= 6) {
        formatted = `(${digits.slice(0, 3)}) ${digits.slice(3)}`;
    } else {
        formatted = `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`;
    }

    el.value = formatted;

    // Place caret at end (simple and reliable). If you need caret-preservation at arbitrary positions,
    // we can implement a more advanced algorithm that tracks digit index before caret.
    el.setSelectionRange(el.value.length, el.value.length);
};

// Optional: block non-numeric keys on keypress for an extra layer of UX.
// Returns true to allow the key, false to block.
window.phoneKeyFilter = function (evt) {
    const key = evt.key;
    // Allow control/navigation keys
    if (key === 'Backspace' || key === 'Delete' || key === 'ArrowLeft' || key === 'ArrowRight' || key === 'Tab' || evt.ctrlKey || evt.metaKey) {
        return true;
    }
    // Allow only digits
    if (/^\d$/.test(key)) return true;

    evt.preventDefault();
    return false;
};