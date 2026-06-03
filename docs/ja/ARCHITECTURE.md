<!-- Translation status:
Source file: docs/ARCHITECTURE.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# アーキテクチャと進化

QY-GaussPlume は Vue フロントエンド、ASP.NET Core API、純粋なシミュレーション Core、EF Core 永続化に分かれています。この境界により、ガウスプルームモデルを HTTP やデータベースから独立してテストできます。

## 全体アーキテクチャ

ブラウザは `frontend-vue` に接続し、`/api/*` は `backend-dotnet` にプロキシされます。バックエンドはデータ読み込み、格子構築、シミュレーション、寄与分析、Shapefile、Excel 入出力を編成します。

## 4 層バックエンド

API 層は HTTP と DTO、サービス層はデータとアルゴリズムの編成、Core は大気計算、Data は EF Core による SQLite マッピングを担当します。

## データフロー：単一風向シミュレーション

`POST /api/simulation/run` は気象場、排出源、受容点を読み込み、格子を作成し、発生源ごとの濃度場を計算し、汚染物質場と受容点寄与ランキングを返します。

## データフロー：多風向並列

`POST /api/simulation/run_parallel` は共有コンテキストを作り、各風向を並列評価し、重みに基づいて濃度場と受容点寄与を合成します。

## フロントエンド分層

フロントエンドは API クライアント、型、Pinia store、ルーティング view、地図 component、composable、色階・座標・選択・ダウンロード・エラー処理 utility に分かれています。

## 11 段階の進化履歴

プロジェクトは Python/FastAPI から ASP.NET Core 9 と Vue 3 へ、バックエンド、Core アルゴリズム、フロントエンド、地図、ダッシュボード、管理ページを段階的に移行しました。

## 主要なトレードオフ

Core は API/Data 依存を持たず、EF Core の既定値の落とし穴を避け、大きな Shapefile は必要時だけ読み込み、多風向計算には .NET のスレッド並列を使います。
