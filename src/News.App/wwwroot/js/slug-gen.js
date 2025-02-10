class SlugGenerator extends HTMLInputElement {
    #slug;

    constructor() {
        super();
    }

    connectedCallback() {
        this.#slug = document.querySelector(`input#${this.dataset.target}[type="text"]`);
        if (!this.#slug) {
            throw new Error('Slug input must be present in the HTML');
        }

        this.addEventListener('change', this.#handleTextChanged);
    }

    disconnectedCallback() {
        this.removeEventListener('change', this.#handleTextChanged);
        this.#slug = null;
    }

    #handleTextChanged(event) {
        const urlValue = event.target.value;
        const action = this.dataset.action;
        if (urlValue && urlValue.length > 0 && action) {
            const actionUrl = new URL(action, document.location);
            actionUrl.searchParams.append('url', urlValue);

            fetch(actionUrl, { method: 'GET' })
                .then(response => response.text())
                .then(slug => {
                    if (slug && slug.length > 0) {
                        this.#slug.value = slug;
                    }
                });
        }
    }
}

window.customElements.define('slug-generator', SlugGenerator, { extends: 'input' });