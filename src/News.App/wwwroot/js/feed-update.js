class FeedUpdate extends HTMLButtonElement {
    constructor() {
        super();
    }

    connectedCallback() {
        this.addEventListener('click', this.updateFeed);
    }

    disconnectedCallback() {
        this.removeEventListener('click', this.updateFeed);
    }

    updateFeed(e) {
        e.preventDefault();
        const action = this.dataset.action;
        if (action) {
            fetch(action, { method: 'PUT' });
        }
    }
}

window.customElements.define('feed-update', FeedUpdate, { extends: 'button' });