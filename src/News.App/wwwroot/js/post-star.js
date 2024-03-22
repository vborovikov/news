class PostStar extends HTMLAnchorElement {
    constructor() {
        super();
        this.star = false;
    }

    connectedCallback() {
        this.star = this.dataset.favorite.toLowerCase() === 'true';
        this.icon = this.firstElementChild;
        this.render();

        this.addEventListener('click', e => {
            e.preventDefault();
            this.toggleStar();
        });
    }

    disconnectedCallback() {
        this.removeEventListener('click');
    }

    toggleStar() {
        const star = !this.star;
        fetch(star ? this.dataset.star : this.dataset.unstar, { method: 'PATCH' }).then(response => {
            if (response.ok) {
                this.star = star;
                this.render();
            }
        });
    }

    render() {
        if (this.star) {
            this.icon.classList.remove('bi-star');
            this.icon.classList.add('bi-star-fill');
        } else {
            this.icon.classList.remove('bi-star-fill');
            this.icon.classList.add('bi-star');
        }
    }
}

window.customElements.define('post-star', PostStar, { extends: 'a' });