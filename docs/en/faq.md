<!-- Translation status:
Source file: docs/faq.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# FAQ

QY-GaussPlume questions usually involve local startup, demo data, simulation parameters, map rendering, and verification. Each answer is self-contained for search engines and AI citation.

### Who should use QY-GaussPlume?

It is for research teams, environmental assessment engineers, scenario evaluators, and technical teams that need to explain air-pollution dispersion results.

### Can the SQLite database in the repository be used in production?

No. `backend/air_pollution.db` is anonymized demo data for local runs and feature demonstrations. Real project data should stay outside the repository and be configured through a connection string.

### Does the dashboard wind control overwrite saved meteorology?

No. Dashboard wind speed and direction are temporary parameters for the current single-wind simulation request. They do not update saved meteorology records.

### Why does an equivalent-area source show only one pollutant value?

Equivalent-area sources use measured `concentration` to calculate an equivalent emission rate. The UI exposes only the concentration field and submits `emissionRate=0` internally to avoid two confusing values.

### What should run before committing?

Run `./scripts/verify.sh`. It executes 146 backend xUnit tests, 79 frontend Vitest tests, and the frontend production build.
