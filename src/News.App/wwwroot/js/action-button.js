class ActionButton extends HTMLButtonElement {
    #timerId;

    constructor() {
        super();
    }

    connectedCallback() {
        this.addEventListener('click', this.#act);
    }

    disconnectedCallback() {
        this.removeEventListener('click', this.#act);

        if (this.#timerId) {
            clearTimeout(this.#timerId);
        }
    }

    #act(e) {
        e.preventDefault();

        const action = this.dataset.action;
        if (action) {
            this.disabled = true;

            fetch(action, { method: this.dataset.method || 'POST' }).then(response => {
                this.disabled = false;
                if (response.ok) {
                    this.#maybeReload();
                }
            });
        }
    }

    #maybeReload() {
        const reload = this.dataset.reload;

        if (reload) {
            const interval = ActionButton.#parseInterval(reload);
            if (interval) {
                if (this.#timerId) {
                    clearTimeout(this.#timerId);
                }

                this.#timerId = setTimeout(() => {
                    window.location.reload();
                }, interval);
            }
            else {
                window.location.reload();
            }
        }
    }

    static #parseInterval(str) {
        if (str == undefined) {
            return undefined;
        }

        let interval = NaN;
        if (str.slice(-2) == "ms") {
            interval = parseFloat(str.slice(0, -2));
        } else if (str.slice(-1) == "s") {
            interval = parseFloat(str.slice(0, -1)) * 1000;
        } else if (str.slice(-1) == "m") {
            interval = parseFloat(str.slice(0, -1)) * 1000 * 60;
        } else {
            interval = parseFloat(str);
        }
        return isNaN(interval) ? undefined : interval;
    }
}

window.customElements.define('action-button', ActionButton, { extends: 'button' });