const pluginId = '7c2e9a41-5f8b-4d3e-9a1c-0b6e8f2a4c10';

export default function (view) {
    const state = {
        startIndex: 0,
        limit: 50,
        total: 0,
        searchTerm: '',
        sortBy: 'Name',
        searchTimer: null,
        libPanelOpen: false,
        bound: false
    };

    const q = (sel) => view.querySelector(sel);

    function client() {
        return window.ApiClient;
    }

    function prop(obj, pascal, camel) {
        if (!obj) return undefined;
        if (obj[pascal] !== undefined) return obj[pascal];
        return obj[camel];
    }

    function escapeHtml(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function escapeAttr(value) {
        return escapeHtml(value).replace(/\n/g, '');
    }

    function normalizeId(value) {
        return String(value == null ? '' : value).replace(/-/g, '').toLowerCase();
    }

    function setHashRoute(path) {
        const clean = String(path || '').replace(/^#?\/?/, '');
        window.location.hash = '#/' + clean;
    }

    function imageUrl(itemId, tag) {
        if (!itemId) return '';
        const c = client();
        let url = c.getUrl('Items/' + itemId + '/Images/Primary');
        if (tag) url += '?tag=' + encodeURIComponent(tag);
        return url;
    }

    function showList() {
        q('#actorsListView').classList.remove('actors-hidden');
        q('#actorsDetailView').classList.add('actors-hidden');
    }

    function showDetail() {
        q('#actorsListView').classList.add('actors-hidden');
        q('#actorsDetailView').classList.remove('actors-hidden');
    }

    function setLibStatus(text) {
        const el = q('#actorsLibStatus');
        if (el) el.textContent = text || '';
    }

    function setLibPanelOpen(open) {
        state.libPanelOpen = !!open;
        const panel = q('#actorsLibPanel');
        if (!panel) return;
        panel.classList.toggle('actors-hidden', !state.libPanelOpen);
        if (state.libPanelOpen) {
            loadLibraryPanel();
        }
    }

    function fetchLibraries(api) {
        const url = api.getUrl('Actors/Libraries');
        if (typeof api.getJSON === 'function') {
            return api.getJSON(url);
        }
        return api.ajax({ type: 'GET', url: url, dataType: 'json' });
    }

    function loadLibraryPanel() {
        const api = client();
        const list = q('#actorsLibraryList');
        if (!api || !list) return Promise.resolve();

        setLibStatus('');
        list.innerHTML = '<p>Lade Libraries…</p>';

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
                return;
            }

            list.innerHTML = libraries.map(function (lib, index) {
                const id = prop(lib, 'Id', 'id') || '';
                const name = prop(lib, 'Name', 'name') || id;
                const type = prop(lib, 'CollectionType', 'collectionType') || '';
                const isIncluded = included[normalizeId(id)] || !!prop(lib, 'Included', 'included');
                const inputId = 'actors_browse_lib_' + index;
                return '<label class="actors-lib-row" for="' + inputId + '">' +
                    '<input type="checkbox" class="actors-lib-check" id="' + inputId +
                    '" data-id="' + escapeAttr(id) + '"' + (isIncluded ? ' checked' : '') + ' />' +
                    '<span class="lib-label">' + escapeHtml(name) +
                    (type ? '<span class="lib-type">(' + escapeHtml(type) + ')</span>' : '') +
                    '</span></label>';
            }).join('');
        }).catch(function (err) {
            console.error('[Actors] loadLibraryPanel failed', err);
            list.innerHTML = '<p>Libraries konnten nicht geladen werden.</p>';
        });
    }

    function saveLibraries() {
        const api = client();
        if (!api) return Promise.resolve();

        const checks = Array.prototype.slice.call(view.querySelectorAll('#actorsLibraryList .actors-lib-check'));
        const ids = checks.filter(function (c) { return c.checked; }).map(function (c) {
            return c.getAttribute('data-id');
        });

        setLibStatus('Speichere…');
        return api.getPluginConfiguration(pluginId).then(function (config) {
            config.IncludedLibraryIds = ids;
            config.includedLibraryIds = ids;
            return api.updatePluginConfiguration(pluginId, config);
        }).then(function () {
            setLibStatus('Baue Index neu…');
            const url = api.getUrl('Actors/Libraries/RefreshMapping') + '?reason=browse-save';
            if (typeof api.ajax === 'function') {
                return api.ajax({ type: 'POST', url: url });
            }
            return fetch(url, {
                method: 'POST',
                headers: api.getRequestHeaders ? api.getRequestHeaders() : {}
            });
        }).then(function () {
            setLibStatus(ids.length
                ? 'Gespeichert. Index aktualisiert (' + ids.length + ' Libraries).'
                : 'Gespeichert. Keine Library gewählt — Übersicht bleibt leer.');
            state.startIndex = 0;
            return loadActors();
        }).catch(function (err) {
            console.error('[Actors] saveLibraries failed', err);
            setLibStatus('Fehler beim Speichern. Details in der Browser-Konsole.');
        });
    }

    function loadActors() {
        const c = client();
        const empty = q('#actorsEmpty');
        const grid = q('#actorsGrid');
        const meta = q('#actorsMeta');
        if (!c) {
            empty.classList.remove('actors-hidden');
            empty.textContent = 'ApiClient nicht verfügbar.';
            return Promise.resolve();
        }

        const params = {
            startIndex: state.startIndex,
            limit: state.limit,
            sortBy: state.sortBy
        };
        if (state.searchTerm) params.searchTerm = state.searchTerm;

        return c.getJSON(c.getUrl('Actors', params)).then(function (data) {
            state.total = prop(data, 'TotalRecordCount', 'totalRecordCount') || 0;
            const hasConfigured = prop(data, 'HasConfiguredLibraries', 'hasConfiguredLibraries');
            const synced = prop(data, 'LibraryMappingLastSyncedUtc', 'libraryMappingLastSyncedUtc');
            const items = prop(data, 'Items', 'items') || [];

            if (!hasConfigured) {
                empty.classList.remove('actors-hidden');
                empty.innerHTML = '<strong>Keine Libraries ausgewählt.</strong><br/>' +
                    'Klicke oben auf <em>Libraries festlegen</em> und speichere mindestens eine Library.';
                grid.innerHTML = '';
                meta.textContent = '';
                q('#actorsPager').classList.add('actors-hidden');
                return;
            }

            empty.classList.add('actors-hidden');
            meta.textContent = state.total + ' Schauspieler' +
                (synced ? ' · Index: ' + new Date(synced).toLocaleString() : '');

            if (!items.length) {
                grid.innerHTML = '<p>Keine Treffer.</p>';
            } else {
                grid.innerHTML = items.map(function (actor) {
                    const id = prop(actor, 'Id', 'id');
                    const name = prop(actor, 'Name', 'name') || '';
                    const tag = prop(actor, 'PrimaryImageTag', 'primaryImageTag');
                    const count = prop(actor, 'ItemCount', 'itemCount') || 0;
                    const img = imageUrl(id, tag);
                    const poster = img
                        ? '<img class="actor-poster" loading="lazy" src="' + escapeHtml(img) + '" alt="" />'
                        : '<div class="actor-poster placeholder">👤</div>';
                    return '<button type="button" class="actor-card" data-id="' + escapeHtml(id) + '">' +
                        poster +
                        '<div class="actor-name">' + escapeHtml(name) + '</div>' +
                        '<div class="actor-count">' + count + ' Titel</div>' +
                        '</button>';
                }).join('');
            }

            const page = Math.floor(state.startIndex / state.limit) + 1;
            const pages = Math.max(1, Math.ceil(state.total / state.limit));
            q('#actorsPageLabel').textContent = 'Seite ' + page + ' / ' + pages;
            q('#actorsPrev').disabled = state.startIndex <= 0;
            q('#actorsNext').disabled = state.startIndex + state.limit >= state.total;
            q('#actorsPager').classList.toggle('actors-hidden', state.total <= state.limit);

            Array.prototype.forEach.call(grid.querySelectorAll('.actor-card'), function (btn) {
                btn.addEventListener('click', function () {
                    openActor(btn.getAttribute('data-id'));
                });
            });
        }).catch(function (err) {
            console.error('[Actors] loadActors failed', err);
            empty.classList.remove('actors-hidden');
            empty.textContent = 'Schauspieler konnten nicht geladen werden.';
        });
    }

    function openActor(personId) {
        const c = client();
        showDetail();
        q('#actorsDetailTitle').textContent = 'Lade…';
        q('#filmographyGrid').innerHTML = '';

        return c.getJSON(c.getUrl('Actors/' + personId + '/Items')).then(function (data) {
            q('#actorsDetailTitle').textContent = prop(data, 'Name', 'name') || 'Filmografie';
            q('#actorsDetailMeta').textContent =
                (prop(data, 'TotalRecordCount', 'totalRecordCount') || 0) + ' Titel in den gewählten Libraries';

            const items = prop(data, 'Items', 'items') || [];
            const grid = q('#filmographyGrid');
            if (!items.length) {
                grid.innerHTML = '<p>Keine Titel.</p>';
                return;
            }

            grid.innerHTML = items.map(function (item) {
                const id = prop(item, 'Id', 'id');
                const name = prop(item, 'Name', 'name') || '';
                const type = prop(item, 'Type', 'type') || '';
                const year = prop(item, 'ProductionYear', 'productionYear');
                const tag = prop(item, 'PrimaryImageTag', 'primaryImageTag');
                const img = imageUrl(id, tag);
                const yearLabel = year ? ' (' + year + ')' : '';
                const poster = img
                    ? '<img class="film-poster" loading="lazy" src="' + escapeHtml(img) + '" alt="" />'
                    : '<div class="film-poster"></div>';
                return '<button type="button" class="film-card" data-id="' + escapeHtml(id) + '">' +
                    poster +
                    '<div class="actor-name">' + escapeHtml(name) + yearLabel + '</div>' +
                    '<div class="actor-count">' + escapeHtml(type) + '</div>' +
                    '</button>';
            }).join('');

            Array.prototype.forEach.call(grid.querySelectorAll('.film-card'), function (btn) {
                btn.addEventListener('click', function () {
                    navigateDetails(btn.getAttribute('data-id'));
                });
            });
        }).catch(function (err) {
            console.error('[Actors] openActor failed', err);
            q('#actorsDetailMeta').textContent = 'Filmografie konnte nicht geladen werden.';
        });
    }

    function navigateDetails(id) {
        const path = 'details?id=' + encodeURIComponent(id);
        try {
            if (window.Dashboard && typeof Dashboard.navigate === 'function') {
                Dashboard.navigate(path);
                setTimeout(function () {
                    if (window.location.hash.indexOf('details') === -1) {
                        setHashRoute(path);
                    }
                }, 50);
                return;
            }
        } catch (e) { /* fall through */ }
        setHashRoute(path);
    }

    function bind() {
        if (state.bound) return;
        state.bound = true;

        const search = q('#actorsSearch');
        if (search) {
            search.addEventListener('input', function (e) {
                clearTimeout(state.searchTimer);
                state.searchTimer = setTimeout(function () {
                    state.searchTerm = e.target.value.trim();
                    state.startIndex = 0;
                    loadActors();
                }, 250);
            });
        }

        const sort = q('#actorsSort');
        if (sort) {
            sort.addEventListener('change', function (e) {
                state.sortBy = e.target.value;
                state.startIndex = 0;
                loadActors();
            });
        }

        const prev = q('#actorsPrev');
        if (prev) {
            prev.addEventListener('click', function () {
                state.startIndex = Math.max(0, state.startIndex - state.limit);
                loadActors();
            });
        }

        const next = q('#actorsNext');
        if (next) {
            next.addEventListener('click', function () {
                state.startIndex = state.startIndex + state.limit;
                loadActors();
            });
        }

        const back = q('#actorsBackBtn');
        if (back) {
            back.addEventListener('click', showList);
        }

        const configLink = q('#actorsConfigLink');
        if (configLink) {
            configLink.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                setLibPanelOpen(!state.libPanelOpen);
            });
        }

        const saveBtn = q('#actorsLibSaveBtn');
        if (saveBtn) {
            saveBtn.addEventListener('click', function (e) {
                e.preventDefault();
                saveLibraries();
            });
        }

        const cancelBtn = q('#actorsLibCancelBtn');
        if (cancelBtn) {
            cancelBtn.addEventListener('click', function (e) {
                e.preventDefault();
                setLibPanelOpen(false);
            });
        }
    }

    function onShow() {
        bind();
        showList();
        loadActors();
    }

    view.addEventListener('viewshow', onShow);
}
