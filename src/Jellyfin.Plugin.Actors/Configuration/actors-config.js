const pluginId = '7c2e9a41-5f8b-4d3e-9a1c-0b6e8f2a4c10';

export default function (view) {
    let loaded = false;
    let bound = false;

    const q = (sel) => view.querySelector(sel);

    function client() {
        return window.ApiClient;
    }

    function setStatus(text) {
        const el = q('#actorsStatus');
        if (el) el.textContent = text || '';
    }

    function prop(obj, pascal, camel) {
        if (!obj) return undefined;
        if (obj[pascal] !== undefined) return obj[pascal];
        return obj[camel];
    }

    function normalizeId(value) {
        return String(value == null ? '' : value).replace(/-/g, '').toLowerCase();
    }

    function escapeHtml(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function escapeAttr(value) {
        return escapeHtml(value).replace(/\n/g, '');
    }

    function setHashRoute(path) {
        const clean = String(path || '').replace(/^#?\/?/, '');
        window.location.hash = '#/' + clean;
    }

    function navigateTo(name) {
        const path = 'configurationpage?name=' + encodeURIComponent(name);
        try {
            if (window.Dashboard && typeof Dashboard.navigate === 'function') {
                Dashboard.navigate(path);
                setTimeout(function () {
                    if (window.location.hash.indexOf('name=' + name) === -1) {
                        setHashRoute(path);
                    }
                }, 50);
                return;
            }
        } catch (e) { /* fall through */ }
        setHashRoute(path);
    }

    function fetchLibraries(api) {
        const url = api.getUrl('Actors/Libraries');
        if (typeof api.getJSON === 'function') {
            return api.getJSON(url);
        }
        return api.ajax({ type: 'GET', url: url, dataType: 'json' });
    }

    function loadConfig() {
        const api = client();
        const list = q('#actorsLibraryList');
        if (!api) {
            list.innerHTML = '<p>ApiClient nicht verfügbar. Bitte Seite neu laden.</p>';
            return Promise.resolve();
        }

        setStatus('');
        return Promise.all([
            api.getPluginConfiguration(pluginId),
            fetchLibraries(api)
        ]).then(function (results) {
            const config = results[0] || {};
            const libraries = results[1] || [];
            const includedIds = prop(config, 'IncludedLibraryIds', 'includedLibraryIds') || [];
            const included = {};
            includedIds.forEach(function (id) { included[normalizeId(id)] = true; });

            if (!libraries.length) {
                list.innerHTML = '<p>Keine Libraries gefunden.</p>';
                loaded = true;
                return;
            }

            list.innerHTML = libraries.map(function (lib, index) {
                const id = prop(lib, 'Id', 'id') || '';
                const name = prop(lib, 'Name', 'name') || id;
                const type = prop(lib, 'CollectionType', 'collectionType') || '';
                const isIncluded = included[normalizeId(id)] || !!prop(lib, 'Included', 'included');
                const inputId = 'actors_lib_' + index;
                return '<label class="actors-lib-row" for="' + inputId + '">' +
                    '<input type="checkbox" class="actors-lib-check" id="' + inputId +
                    '" data-id="' + escapeAttr(id) + '"' + (isIncluded ? ' checked' : '') + ' />' +
                    '<span class="lib-label">' + escapeHtml(name) +
                    (type ? '<span class="lib-type">(' + escapeHtml(type) + ')</span>' : '') +
                    '</span></label>';
            }).join('');
            loaded = true;
        }).catch(function (err) {
            console.error('[Actors] loadConfig failed', err);
            list.innerHTML =
                '<p>Libraries konnten nicht geladen werden. Ist das Plugin aktiv und der Server neu gestartet?</p>';
        });
    }

    function saveConfig() {
        const api = client();
        const checks = Array.prototype.slice.call(view.querySelectorAll('.actors-lib-check'));
        const ids = checks.filter(function (c) { return c.checked; }).map(function (c) {
            return c.getAttribute('data-id');
        });

        setStatus('Speichere…');
        return api.getPluginConfiguration(pluginId).then(function (config) {
            config.IncludedLibraryIds = ids;
            config.includedLibraryIds = ids;
            return api.updatePluginConfiguration(pluginId, config);
        }).then(function () {
            setStatus('Baue Index neu…');
            const url = api.getUrl('Actors/Libraries/RefreshMapping') + '?reason=settings-save';
            if (typeof api.ajax === 'function') {
                return api.ajax({ type: 'POST', url: url });
            }
            return fetch(url, {
                method: 'POST',
                headers: api.getRequestHeaders ? api.getRequestHeaders() : {}
            });
        }).then(function () {
            setStatus(ids.length
                ? 'Gespeichert. Index aktualisiert (' + ids.length + ' Libraries).'
                : 'Gespeichert. Keine Library gewählt — Übersicht bleibt leer.');
            if (window.Dashboard && Dashboard.processPluginConfigurationUpdateResult) {
                Dashboard.processPluginConfigurationUpdateResult();
            }
        }).catch(function (err) {
            console.error('[Actors] saveConfig failed', err);
            setStatus('Fehler beim Speichern. Details in der Browser-Konsole.');
        });
    }

    function bind() {
        if (bound) return;
        bound = true;

        const saveBtn = q('#actorsSaveBtn');
        if (saveBtn) {
            saveBtn.addEventListener('click', function (e) {
                e.preventDefault();
                saveConfig();
            });
        }

        const openBtn = q('#actorsOpenBtn');
        if (openBtn) {
            openBtn.addEventListener('click', function (e) {
                e.preventDefault();
                navigateTo('actors');
            });
        }
    }

    function onShow() {
        bind();
        loaded = false;
        loadConfig();
    }

    view.addEventListener('viewshow', onShow);
}
