# Codex Project Status: MyShowtime

## Highlights Completed
- Established a .NET 8 solution with Blazor WebAssembly client, Minimal API backend, and shared DTO library.
- Implemented PostgreSQL persistence via Entity Framework Core with automated migrations; database `myshowtime` seeded through `InitialCreate`.
- Added TMDB integration with environment‑driven API key, search endpoint, metadata refresh, and validation/error handling.
- Built a Blazor client experience that mirrors the original WPF styling: dual-panel dashboard, TMDB search table, library grid, and contextual feedback.
- Published artifacts to `/var/www/projects/MyShowtime` and configured a `systemd` service (`myshowtime-api`) plus Nginx reverse proxy (`myshowtime.local`) for static hosting and `/api/*` proxying.

## Current State
- Service is live locally: `myshowtime-api` listens on `http://localhost:5000`, proxied via Nginx at `http://myshowtime.local`.
- Environment file `/etc/myshowtime/myshowtime.env` holds production settings (TMDB key, PostgreSQL credentials, ASP.NET Core hosting values).
- PostgreSQL contains the `Shows` table with enforced unique TMDB IDs; timestamps update correctly on refresh/save flows.
- Blazor UI deployed under `/var/www/projects/MyShowtime/wwwroot` and styled assets served by Nginx.

## Outstanding Considerations
- Client currently surfaces summaries and actions; deeper detail panes (posters, notes editing, episodic grids) remain to reach full parity with the desktop app.
- Sensitive secrets live in the environment file—rotate as needed and consider vault integration for production.
- HTTPS is not yet enabled on Nginx; add certificates before exposing beyond the local network.
- Automated tests (unit/integration) have not been introduced; future work could cover API validation and client interaction tests.

## Suggested Next Steps
1. Design and implement detail views (poster, notes, watch state) and richer filtering mirroring the WPF workflows.
2. Introduce caching/throttling for TMDB searches and optional offline indicators.
3. Add CI-friendly build/test scripts and consider containerizing API + client for deployment consistency.
