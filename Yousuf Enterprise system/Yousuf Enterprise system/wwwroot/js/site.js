// App-wide UI behaviour: popup forms, confirmation prompts and toast messages.
(function () {
    'use strict';

    const MODAL_HEADERS = { 'X-Modal-Request': 'true' };
    let modalGeneration = 0;

    function getAppModal() {
        const element = document.getElementById('appModal');
        return element ? { element: element, instance: bootstrap.Modal.getOrCreateInstance(element) } : null;
    }

    // ---- Toasts ----------------------------------------------------------------

    function showToasts() {
        document.querySelectorAll('.app-toast').forEach(function (toast) {
            bootstrap.Toast.getOrCreateInstance(toast).show();
        });
    }

    // ---- Confirmation ----------------------------------------------------------

    function confirmOptions(form) {
        const data = form.dataset;
        return {
            title: data.confirmTitle || 'Save changes?',
            message: data.confirm || 'Please make sure the details are correct.',
            okText: data.confirmOk || 'Yes, save',
            danger: data.confirmVariant === 'danger'
        };
    }

    // Standalone dialog for actions on normal pages (delete buttons, settings, etc.).
    function confirmDialog(options) {
        return new Promise(function (resolve) {
            const element = document.getElementById('confirmModal');
            if (!element) {
                resolve(window.confirm(options.title));
                return;
            }

            element.querySelector('#confirmModalTitle').textContent = options.title;
            element.querySelector('#confirmModalMessage').textContent = options.message;
            const icon = element.querySelector('.confirm-icon');
            icon.className = 'confirm-icon ' + (options.danger ? 'confirm-icon-danger' : 'confirm-icon-primary');
            icon.innerHTML = '<i class="bi ' + (options.danger ? 'bi-exclamation-triangle' : 'bi-question-circle') + '"></i>';
            const ok = element.querySelector('#confirmModalOk');
            ok.className = 'btn px-4 ' + (options.danger ? 'btn-danger' : 'btn-success');
            ok.textContent = options.okText;

            const modal = bootstrap.Modal.getOrCreateInstance(element);
            let confirmed = false;
            function onOk() {
                confirmed = true;
                modal.hide();
            }
            ok.addEventListener('click', onOk);
            element.addEventListener('hidden.bs.modal', function () {
                ok.removeEventListener('click', onOk);
                resolve(confirmed);
            }, { once: true });
            modal.show();
        });
    }

    // Inside a popup, stacking a second dialog on top is awkward, so the popup's own footer
    // turns into the prompt instead.
    function inlineConfirm(form, options) {
        return new Promise(function (resolve) {
            const actions = form.querySelector('.form-actions, .modal-footer');
            if (!actions) {
                resolve(true);
                return;
            }

            const shown = Array.from(actions.children).filter(function (child) { return !child.hidden; });
            shown.forEach(function (child) { child.hidden = true; });

            const bar = document.createElement('div');
            bar.className = 'inline-confirm' + (options.danger ? ' is-danger' : '');
            bar.innerHTML =
                '<div class="inline-confirm-text"><i class="bi ' + (options.danger ? 'bi-exclamation-triangle' : 'bi-question-circle') + '"></i>' +
                '<div><strong></strong><span></span></div></div>' +
                '<div class="inline-confirm-buttons">' +
                '<button type="button" class="btn btn-light" data-choice="back">Go back</button>' +
                '<button type="button" class="btn ' + (options.danger ? 'btn-danger' : 'btn-success') + '" data-choice="ok"></button>' +
                '</div>';
            bar.querySelector('strong').textContent = options.title;
            bar.querySelector('span').textContent = options.message;
            bar.querySelector('[data-choice="ok"]').textContent = options.okText;
            actions.appendChild(bar);
            bar.querySelector('[data-choice="ok"]').focus();

            bar.addEventListener('click', function (event) {
                const choice = event.target.closest('[data-choice]');
                if (!choice) {
                    return;
                }
                bar.remove();
                shown.forEach(function (child) { child.hidden = false; });
                resolve(choice.dataset.choice === 'ok');
            });
        });
    }

    function setBusy(form, busy) {
        form.querySelectorAll('button[type="submit"], button:not([type])').forEach(function (button) {
            if (busy) {
                button.dataset.idleHtml = button.innerHTML;
                button.disabled = true;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span> Please wait...';
            } else if (button.dataset.idleHtml !== undefined) {
                button.disabled = false;
                button.innerHTML = button.dataset.idleHtml;
                delete button.dataset.idleHtml;
            }
        });
    }

    // ---- Popup forms -----------------------------------------------------------

    function runScript(source) {
        return new Promise(function (resolve) {
            const script = document.createElement('script');
            const src = source.getAttribute('src');
            if (src) {
                const url = new URL(src, window.location.href).href;
                if (Array.from(document.scripts).some(function (s) { return s.src === url; })) {
                    resolve();
                    return;
                }
                script.src = url;
                script.onload = resolve;
                script.onerror = resolve;
                document.body.appendChild(script);
                return;
            }

            // Wrapped in a block so opening the same form again doesn't redeclare its const/let variables.
            script.textContent = '{\n' + source.textContent + '\n}';
            document.body.appendChild(script);
            script.remove();
            resolve();
        });
    }

    async function renderModal(html, fallbackUrl) {
        const modal = getAppModal();
        const template = document.createElement('template');
        template.innerHTML = html;
        const content = template.content.querySelector('.modal-form-content');
        if (!modal || !content) {
            // Not a popup response (e.g. the session expired and this is the login page).
            window.location.assign(fallbackUrl);
            return;
        }

        const scripts = Array.from(template.content.querySelectorAll('script'));
        scripts.forEach(function (script) { script.remove(); });

        modal.element.querySelector('.modal-dialog').className =
            'modal-dialog modal-dialog-centered modal-dialog-scrollable' + (content.dataset.size ? ' modal-' + content.dataset.size : '');
        modal.element.querySelector('#appModalTitle').textContent = content.dataset.title || '';
        const body = modal.element.querySelector('#appModalBody');
        body.replaceChildren.apply(body, Array.from(template.content.childNodes));

        for (const script of scripts) {
            await runScript(script);
        }

        const form = body.querySelector('form');
        if (form && window.jQuery && jQuery.validator && jQuery.validator.unobtrusive) {
            jQuery(form).removeData('validator').removeData('unobtrusiveValidation');
            jQuery.validator.unobtrusive.parse(form);
        }

        const firstError = body.querySelector('.validation-summary-errors, .input-validation-error');
        if (firstError) {
            firstError.scrollIntoView({ block: 'center' });
        } else {
            const firstField = body.querySelector('input:not([type="hidden"]):not([readonly]):not([disabled]), select:not([disabled]), textarea');
            if (firstField) {
                firstField.focus();
            }
        }
    }

    async function openModal(url) {
        const modal = getAppModal();
        if (!modal) {
            window.location.assign(url);
            return;
        }

        const generation = ++modalGeneration;
        modal.element.querySelector('.modal-dialog').className = 'modal-dialog modal-dialog-centered modal-dialog-scrollable modal-lg';
        modal.element.querySelector('#appModalTitle').textContent = 'Loading...';
        modal.element.querySelector('#appModalBody').innerHTML =
            '<div class="modal-loading"><span class="spinner-border text-success" role="status" aria-label="Loading"></span></div>';
        modal.instance.show();

        try {
            const response = await fetch(url, { headers: MODAL_HEADERS, credentials: 'same-origin' });
            if (!response.ok) {
                throw new Error(response.statusText);
            }
            const html = await response.text();
            if (generation !== modalGeneration) {
                return; // closed while loading
            }
            await renderModal(html, url);
        } catch (error) {
            window.location.assign(url);
        }
    }

    function showFormError(form, message) {
        let alert = form.querySelector('.modal-submit-error');
        if (!alert) {
            alert = document.createElement('div');
            alert.className = 'alert alert-danger modal-submit-error';
            form.prepend(alert);
        }
        alert.textContent = message;
        alert.scrollIntoView({ block: 'center' });
    }

    async function submitModalForm(form) {
        let response;
        try {
            response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: MODAL_HEADERS,
                credentials: 'same-origin'
            });
        } catch (error) {
            showFormError(form, 'Could not reach the server. Check your connection and try again.');
            return false;
        }

        if ((response.headers.get('content-type') || '').indexOf('application/json') !== -1) {
            const data = await response.json();
            if (data.redirect) {
                window.location.assign(data.redirect);
                return true;
            }
        }

        if (response.status >= 500) {
            showFormError(form, 'Something went wrong while saving. Please try again.');
            return false;
        }

        // Validation failed: the server sent the form back with its errors.
        await renderModal(await response.text(), form.action);
        return true;
    }

    // ---- Event wiring ----------------------------------------------------------

    document.addEventListener('submit', async function (event) {
        const form = event.target;
        // Client-side validation runs first and cancels the submit when something is invalid.
        if (!(form instanceof HTMLFormElement) || event.defaultPrevented) {
            return;
        }

        const inPopup = !!form.closest('#appModal');
        const wantsConfirm = form.matches('.app-form, [data-confirm], [data-confirm-title]');
        if (!inPopup && !wantsConfirm) {
            return;
        }

        event.preventDefault();
        if (form.dataset.submitting === '1') {
            return;
        }

        if (wantsConfirm) {
            const options = confirmOptions(form);
            const confirmed = form.closest('.modal') ? await inlineConfirm(form, options) : await confirmDialog(options);
            if (!confirmed) {
                return;
            }
        }

        form.dataset.submitting = '1';
        setBusy(form, true);

        if (!inPopup) {
            HTMLFormElement.prototype.submit.call(form);
            return;
        }

        const handled = await submitModalForm(form);
        if (!handled) {
            setBusy(form, false);
            delete form.dataset.submitting;
        }
    });

    document.addEventListener('click', function (event) {
        const trigger = event.target.closest('a[data-modal]');
        if (trigger) {
            // Let ctrl/cmd/shift/middle-click still open the full page in a new tab.
            if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
                return;
            }
            event.preventDefault();
            openModal(trigger.href);
            return;
        }

        const cancel = event.target.closest('#appModal [data-modal-cancel]');
        if (cancel) {
            event.preventDefault();
            const modal = getAppModal();
            if (modal) {
                modal.instance.hide();
            }
        }
    });

    document.addEventListener('DOMContentLoaded', function () {
        showToasts();
        const element = document.getElementById('appModal');
        if (element) {
            element.addEventListener('hidden.bs.modal', function () {
                modalGeneration++;
                element.querySelector('#appModalBody').replaceChildren();
            });
        }
    });
})();
