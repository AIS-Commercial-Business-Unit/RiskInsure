Particular Platform — ServiceControl / ServicePulse (dev compose)

Files:
- `docker-compose.yml` — brings up `ravendb`, `servicecontrol`, and `servicepulse` for local testing.
- `.env.example` — environment placeholders. Copy to `.env` in this folder and fill values before `docker-compose up`.

Quick start (from repo root):

```powershell
cd platform/particular
cp .env.example .env   # or copy manually on Windows
# Edit .env and set SERVICEBUS_CONNECTION_STRING and PARTICULARSOFTWARE_LICENSE
docker compose up -d
docker compose logs -f servicecontrol
```

Access:
- RavenDB Studio: http://localhost:8080
- ServiceControl API: http://localhost:33333
- ServicePulse UI: http://localhost:8088

Notes:
- Image tags are pinned to known releases: `particular/servicecontrol:6.11.0`, `particular/servicepulse:2.5.0`, `ravendb/ravendb:latest-lts`.
- The compose uses the repo `riskinsure` external network so it integrates with existing services; ensure that network exists or edit the `networks` section.
- The `servicecontrol` container runs with `--setup-and-run` to simplify initial queue creation. For production or upgrade scenarios, follow Particular docs and run setup separately when appropriate.
