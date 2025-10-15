# Codex Project Status: MyShowtime

## Highlights Completed
- Replaced the initial placeholder schema with a rich media domain: `Media` and `Episodes` tables, view-state enums, and JSON-backed genre/cast fields managed via EF Core migrations.
- Implemented TMDB ingestion for movies and multi-season TV shows (details + seasons API), including streaming provider detection and cast aggregation.
- Delivered Minimal API endpoints covering library listing, detail retrieval, TMDB import/sync, metadata edits (priority/watch state/notes/hide), and per-episode state updates.
- Rebuilt the Blazor WASM client to mirror the WPF layout: upper-left media grid with watch filters, lower-left TMDB search/import panel, central detail view (poster, notes, priority/watch controls), and right-side episode browser/detail.
- Simplified the layout header (no settings drawer) while keeping deployment-friendly defaults.
- Automated deployment remains in place: API published to `/var/www/projects/MyShowtime/api`, client assets to `/var/www/projects/MyShowtime/wwwroot` (this is the target when running `dotnet publish`), hosted by `systemd` + Nginx proxy at `http://myshowtime.local`.
- Added scoped Tailscale access by serving the client and API at `/MyShowtime/` through an Nginx snippet that rewrites static assets and proxies back to `http://127.0.0.1:5000`.

## Current State
- Service is live locally; TMDB imports create full records in PostgreSQL and hydrate episode lists. Library mutations persist through the API and surface immediately in the Blazor UI.
- Database schema migrations (`InitialCreate`, `CreateMediaSchema`) are applied; `Media` rows are keyed by TMDB id with episode uniqueness enforced per season/episode.
- Environment configuration (`/etc/myshowtime/myshowtime.env`) now drives TMDB key, PostgreSQL credentials, and ASP.NET Core environment.
- UI reflects WPF look-and-feel with watch filters, hide toggle, notes editor, priority steppers, and episode watch-state radios backed by live API calls.
- Blazor dashboard panels are height-capped (library/search at 50vh, details/episodes at viewport height) with internal scrolling, and the client remembers each browser's last-selected media item via `localStorage`.
- Blazor dashboard now surfaces a zoom-tip banner (reappears after ~18 hours), shows movie posters in the episode column when no episodes exist, and compresses grid rows for denser library browsing.
- Nginx now responds at `http://100.102.6.85/MyShowtime/` for Tailscale clients while keeping `http://myshowtime.local/` available on the LAN.

## Outstanding Considerations
- Advanced WPF features still pending: person/crew search mode, streaming availability editing, and richer relationship views (networks, memberships).
- Secrets remain in the environment file; consider rotation or secret-management tooling before external deployment.
- HTTPS termination is not configured on Nginx; add TLS before exposing beyond localhost.
- No automated tests yet; regressions could occur without unit/integration coverage or end-to-end UI smoke tests.
- TMDB requests run sequentially when importing all seasons; heavy usage may require caching, rate limiting, or background jobs.
- UI font sizing still needs refinement—consider reintroducing per-user scaling or typographic tokens when the design stabilises.

## Suggested Next Steps
1. Port remaining WPF workflows (person search, provider maintenance, advanced filtering) into corresponding API endpoints and Blazor panels.
2. Introduce persistence for user-defined fields such as custom source labels, application settings, and multi-user support if needed.
3. Add automated tests plus CI scripting, and consider containerizing API + client for reproducible deployment.
4. Harden ops: enable HTTPS, rotate credentials, and monitor TMDB call volume (retry/backoff/caching layer).

## Lessons Learned (Today)
- Publishing straight to `/var/www/projects/MyShowtime/wwwroot` right after `dotnet publish -c Release src/MyShowtime.Client` ensures UI tweaks (like the header rename) land without stale cached bundles.
- JS interop guards are essential—wrapping calls to the `myShowtimeState` helpers prevents the Blazor app from logging generic “Unhandled error” messages when scripts haven’t loaded yet.
- Blazor WASM caching can mask layout changes; after deployment, always do a hard refresh or clear the service-worker cache when validating UI fixes.
- When hosting under `/MyShowtime/`, keep `<base href="./" />` in both the repo and deployed `wwwroot/index.html`; republish with `dotnet publish -c Release src/MyShowtime.Client` so the deployed files stay in sync.
- Preserve `/etc/nginx/snippets/projects/MyShowtime.conf` (rewrites + proxy) whenever nginx configs are refreshed so the scoped path keeps working.
- Use lightweight `localStorage` helpers (via `myShowtimeState`) for user-specific state like the last-selected media, keeping it per-browser without introducing server-side user tracking yet.
- Dense tabular layouts are more readable after shrinking row padding by ~40%; tweak font + padding together to avoid cramped hit targets.
- Default zoom guidance now relies on a dismissable in-app banner; we store a timestamp so it stays hidden for ~18 hours before reappearing.
