<!-- Translation status:
Source file: docs/API.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# API リファレンス

QY-GaussPlume は `/api/*` 配下で HTTP JSON エンドポイントを提供します。JSON は camelCase で、エラーは `{ "detail": "..." }` と標準 HTTP ステータスで返します。

## 排出源 `/api/sources`

排出源 API は一覧、取得、作成、一括作成、更新、削除、汚染物質メタデータ、マーカー、Excel テンプレート、Excel インポートを扱います。等価面源は `emissionRate=0` と実測 `concentration` を送信します。

## 受容点 `/api/receptors`

受容点 API は CRUD、一括作成、Excel テンプレート、Excel インポート、選択エクスポート、選択削除を扱います。

## 気象場 `/api/meteorology`

気象場 API は保存済みの風速、風向、安定度、境界層高度、温度、湿度、雲量、降水量を管理します。

## シミュレーション `/api/simulation`

`POST /api/simulation/run` は単一風向シミュレーションです。任意の `windSpeed` と `windDirection` は現在の要求だけで保存済み気象場を一時上書きします。`POST /api/simulation/run_parallel` は重み付き多風向シミュレーションです。

## マーカー設定 `/api/config`

マーカー設定 API は地図 UI で使う排出源と受容点のスタイルを管理します。

## 地図 `/api/map`

地図 API は任意の GeoJSON 境界読み込み、地図範囲、Shapefile メタデータを提供します。大きな Shapefile は既定では読み込みません。

## ブラウザで API を確認

バックエンドを起動し、`http://localhost:5207/openapi/v1.json` を開くと OpenAPI ドキュメントを確認できます。
