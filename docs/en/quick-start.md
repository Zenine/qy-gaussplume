<!-- Translation status:
Source file: docs/quick-start.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# Quick Start

QY-GaussPlume can run the full frontend and backend locally with one script, so you can try dashboard simulation, data management, and contribution analysis before development.

## Three Steps

1. Prepare .NET SDK 9.0.x and Node.js 20+.
2. Clone the repository and install frontend dependencies.
3. Run the startup script and open the browser.

```bash
git clone git@github.com:Zenine/qy-gaussplume.git
cd qy-gaussplume
cd frontend-vue && npm install --registry=https://registry.npmmirror.com && cd ..
./scripts/start.sh
```

## One-Sentence Template

Use this with an AI coding assistant:

```text
The QY-GaussPlume source is at /Users/zeninexu/github/未命名文件夹/qy-gaussplume. Read QUICK_START.md, then ask me questions. If there are no questions, start working.
```

## Verification

```bash
./scripts/verify.sh
```

This runs backend xUnit tests, frontend Vitest tests, and the frontend production build.

## Resume After Interruption

```text
Read checkpoint.md and continue the previous work.
```
