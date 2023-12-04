class ConfirmDialog extends HTMLElement {
    constructor() {
        super();
    }

    connectedCallback() {
        this.render();

        const confirmBtn = this.querySelector("#confirm-btn");
        confirmBtn.addEventListener("click", () => {
            const action = this.getAttribute("action");
            if (action) {
                fetch(action, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/x-www-form-urlencoded"
                    },
                    body: this.getActionParams()
                });
            }
        });
    }

    disconnectedCallback() {
        const confirmBtn = this.querySelector("#confirm-btn");
        confirmBtn.removeEventListener("click");
    }

    getActionParams() {
        const params = new URLSearchParams();

        const attributes = this.attributes;
        for (let i = 0; i < attributes.length; i++) {
            const attributeName = attributes[i].name;
            const attributeValue = attributes[i].value;

            if (attributeName.startsWith('param-')) {
                const paramName = attributeName.slice(6);
                params.append(paramName, attributeValue);
            }
        }

        return params;
    }

    render() {
        this.innerHTML = `
            <button id="dialog-btn" type="button" class="btn btn-outline-danger" data-bs-toggle="modal" data-bs-target="#dialog">
                ${this.getAttribute("action-text") || this.getAttribute("title") || "<i class=\"bi bi-trash\"></i>"}
            </button>

            <div id="dialog" class="modal fade" tabindex="-1" aria-hidden="true" style="display: none;">
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
                            <p>${this.textContent || this.getAttribute("text") || "Are you sure?"}</p>
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

window.customElements.define('confirm-dialog', ConfirmDialog);