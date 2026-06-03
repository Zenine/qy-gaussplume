<!-- Translation status:
Source file: docs/WORKFLOW.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# Development Workflow

QY-GaussPlume uses repository scripts for local startup, shutdown, testing, and full verification. Prefer project scripts over local Git hooks.

## Start And Stop

Use `./scripts/start.sh` to start backend and frontend together, and `./scripts/stop.sh` to stop them. Backend runs on port 5207 and frontend on port 5173.

## Run Tests

Run `./scripts/verify.sh` before committing. It executes backend tests, frontend tests, and frontend production build.

## Common Change Templates

For new data entities, API endpoints, backend algorithms, frontend pages, dashboard weather controls, and management pages, update tests first and keep DTOs, types, docs, and workflow notes aligned.

## Known Pitfalls

Avoid EF Core `HasDefaultValue` for fields without database defaults, handle historical NULL values, stub Canvas in jsdom tests, avoid concurrent `npm install`, and keep backend port settings synchronized.

## Verify Full Stack

After tests pass, start the app and check backend and Vite proxy endpoints with `curl` to confirm the full stack is wired correctly.

## Other Common Commands

Useful commands include exporting OpenAPI JSON, checking frontend bundle size, inspecting SQLite tables, and tailing runtime logs.

## Runtime Logs

`scripts/start.sh` writes backend logs to `.run/backend.log` and frontend logs to `.run/frontend.log`, rotating old logs on each start.

## Feedback

For bug reports, feature suggestions, or questions, contact the maintainer or open a GitHub issue.
