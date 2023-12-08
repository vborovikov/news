class ConfirmDialog extends HTMLButtonElement {
    constructor() {
        super();
        this.modalDiv = document.createElement('div');
    }

    connectedCallback() {
        this.render();

        const confirmBtn = this.modalDiv.querySelector("#confirm-btn");
        confirmBtn.addEventListener("click", () => {
            const action = this.getAttribute("action");
            if (action) {
                fetch(action, {
                    method: "DELETE",
                    headers: {
                        "Content-Type": "application/x-www-form-urlencoded"
                    },
                    body: this.getActionParams()
                })
                .then(response => {
                    if (response.ok) {
                        window.location.reload();
                    }
                });
            }
        });

        const dialogDiv = this.modalDiv.querySelector("#dialog");
        document.body.append(dialogDiv);

        this.setAttribute("data-bs-toggle", "modal");
        this.setAttribute("data-bs-target", "#dialog");
    }

    disconnectedCallback() {
        this.removeEventListener("click");

        const confirmBtn = this.modalDiv.querySelector("#confirm-btn");
        confirmBtn.removeEventListener("click");

        const dialogDiv = this.modalDiv.querySelector("#dialog");
        dialogDiv.remove();
    }

    getActionParams() {
        const params = new URLSearchParams();

        const attributes = this.attributes;
        for (let i = 0; i < attributes.length; i++) {
            const attributeName = attributes[i].name;
            const attributeValue = attributes[i].value;

            if (attributeName.startsWith('action-param-')) {
                const paramName = attributeName.slice(13);
                params.append(paramName, attributeValue);
            }
        }

        return params;
    }

    render() {
        this.modalDiv.innerHTML = `
            <div id="dialog" class="modal fade" tabindex="-1" aria-hidden="true">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header bg-danger text-white">
                            <h5 class="modal-title">
                                <span class="bi bi-trash">
                                    ${this.getAttribute("title") || this.getAttribute("action-text") || "Delete"}
                                </span>
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <p>${this.getAttribute("text") || "Are you sure?"}</p>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                ${this.getAttribute("cancel-text") || "Cancel"}
                            </button>
                            <button id="confirm-btn" type="button" class="btn btn-danger" data-bs-dismiss="modal">
                                ${this.getAttribute("confirm-text") || this.getAttribute("action-text") || this.getAttribute("title") || "Delete"}
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }
}

window.customElements.define('confirm-dialog', ConfirmDialog, { extends: 'button' });