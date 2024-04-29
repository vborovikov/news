class PostRead extends HTMLElement {
    constructor() {
        super();
    }

    connectedCallback() {
        const action = this.dataset.action;
        if (action) {
            // mark post as read after 10 seconds
            this.timerId = setTimeout(() => {
                fetch(action, { method: 'PATCH' });
            }, 10000);
        }
    }

    disconnectedCallback() {
        if (this.timerId) {
            clearTimeout(this.timerId);
        }
    }
}

window.customElements.define('post-read', PostRead, { extends: 'article' });