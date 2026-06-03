<!-- Translation status:
Source file: docs/WORKFLOW.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# 開発ワークフロー

QY-GaussPlume はローカル起動、停止、テスト、完全検証にリポジトリ内スクリプトを使います。ローカル Git hook よりプロジェクトスクリプトを優先します。

## 起動と停止

`./scripts/start.sh` でバックエンドとフロントエンドを同時に起動し、`./scripts/stop.sh` で停止します。バックエンドは 5207、フロントエンドは 5173 で動作します。

## テスト実行

コミット前に `./scripts/verify.sh` を実行します。バックエンドテスト、フロントエンドテスト、本番ビルドを実行します。

## よくある変更テンプレート

新しいデータ実体、API、バックエンドアルゴリズム、フロントエンド画面、主控台気象制御、管理ページを変更するときは、先にテストを更新し、DTO、型、文書、ワークフローを揃えます。

## 既知の落とし穴

DB 既定値がない項目に EF Core `HasDefaultValue` を使わないこと、履歴 NULL 値への対応、jsdom の Canvas stub、同時 `npm install` の回避、バックエンド port 設定の同期に注意します。

## フルスタック検証

テスト後にアプリを起動し、`curl` でバックエンドと Vite proxy の endpoint を確認します。

## その他のよく使うコマンド

OpenAPI JSON の出力、フロントエンド bundle サイズ確認、SQLite table 確認、ランタイムログ tail などがあります。

## ランタイムログ

`scripts/start.sh` はバックエンドログを `.run/backend.log`、フロントエンドログを `.run/frontend.log` に書き、起動ごとに古いログを rotate します。

## フィードバック

バグ報告、機能提案、質問はメンテナーへ連絡するか GitHub issue を作成してください。
