# Jellyfin.Plugin.Actors

Schauspieler-Übersicht für **Jellyfin 12**, gefiltert auf fest konfigurierte Libraries.

## Installieren über Katalog (empfohlen)

1. In Jellyfin: **Dashboard → Plugins → Repositories → Repository hinzufügen**
2. Name: `Actors`
3. URL:

```text
https://agentcooper75.github.io/jellyfin-plugin-actors/manifest.json
```

Fallback (falls Pages noch baut):

```text
https://raw.githubusercontent.com/agentcooper75/jellyfin-plugin-actors/gh-pages/manifest.json
```

4. **Katalog** öffnen → **Actors** installieren
5. Jellyfin **einmal neu starten**
6. **Dashboard → Plugins → Actors → Einstellungen** → Libraries anhaken → Speichern

Updates danach: Katalog → Aktualisieren → neu starten. Kein SMB-Kopieren nötig.

## Features

- Raster mit Foto, Name, Titelanzahl
- Suche + Sortierung (Name / Anzahl Titel)
- Filmografie nur aus den gewählten Libraries
- Admin-Einstellung: Libraries fest an-/abschalten
- Menüeintrag **Schauspieler** in Jellyfin Web

## Wichtiges Verhalten

Ohne gewählte Library bleibt die Übersicht **leer**.

## Entwickeln / Release

```bash
# lokal paketieren (aktualisiert auch manifest.json)
./build.sh 1.0.0.2

# Release veröffentlichen
# 1) build.yaml version + changelog anpassen
# 2) committen, taggen, pushen:
git add -A && git commit -m "release: 1.0.0.2"
git tag v1.0.0.2
git push origin main v1.0.0.2
```

GitHub Actions baut das Zip, erstellt das Release und publiziert `manifest.json` auf GitHub Pages.

## API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/Actors` | Paginierte Liste (`searchTerm`, `sortBy=Name\|ItemCount`) |
| GET | `/Actors/{personId}/Items` | Filmografie |
| GET | `/Actors/Libraries` | Libraries + Included-Flag |
| POST | `/Actors/Libraries/RefreshMapping` | Index neu aufbauen |
