<!-- Translation status:
Source file: docs/quick-start.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# クイックスタート

QY-GaussPlume は 1 つのスクリプトでローカルのフロントエンドとバックエンドを起動できます。開発前に、ダッシュボードシミュレーション、データ管理、寄与分析を試せます。

## 三つの手順

1. .NET SDK 9.0.x と Node.js 20+ を用意します。
2. リポジトリをクローンし、フロントエンド依存関係をインストールします。
3. 起動スクリプトを実行し、ブラウザを開きます。

```bash
git clone git@github.com:Zenine/qy-gaussplume.git
cd qy-gaussplume
cd frontend-vue && npm install --registry=https://registry.npmmirror.com && cd ..
./scripts/start.sh
```

## 一文テンプレート

AI コーディング助手には次の文を使えます。

```text
QY-GaussPlume のソースは /Users/zeninexu/github/未命名文件夹/qy-gaussplume にあります。QUICK_START.md を読んでから質問してください。質問がなければ作業を始めてください。
```

## 検証

```bash
./scripts/verify.sh
```

バックエンド xUnit、フロントエンド Vitest、本番ビルドを実行します。

## 中断後の再開

```text
checkpoint.md を読んで、前回の作業を続けてください。
```
