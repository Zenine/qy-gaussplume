---
titleTemplate: ':title'
layout: home
hero:
  name: QY-GaussPlume
  text: 拡散評価を地図で操作できるワークフローへ
  tagline: 研究・工程評価チーム向けに、排出源、受容点、気象、寄与分析を 1 つの検証可能な基盤にまとめます。
  image:
    src: /hero.svg
    alt: QY-GaussPlume
  actions:
    - theme: brand
      text: クイックスタート
      link: /ja/quick-start
    - theme: alt
      text: アーキテクチャ
      link: /ja/ARCHITECTURE
features:
  - icon:
      src: /icons/globe.svg
    title: 地図上で影響を評価
    details: 排出源、受容点、範囲、濃度ヒートマップを 1 つの画面で確認できます。
  - icon:
      src: /icons/settings.svg
    title: 風況をすばやく試算
    details: 保存済み気象場を上書きせず、一時的な風速と風向で実行できます。
  - icon:
      src: /icons/bar-chart.svg
    title: 受容点への寄与を説明
    details: 汚染物質ごとに各受容点への発生源寄与をランキングします。
  - icon:
      src: /icons/file-text.svg
    title: データを一括管理
    details: Excel テンプレートで排出源と受容点を管理できます。
  - icon:
      src: /icons/check-circle.svg
    title: 検証付きで出荷
    details: バックエンド、フロントエンド、ビルド検証を含む 209 件の自動テストがあります。
---

<!-- Translation status:
Source file: docs/index.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# QY-GaussPlume

QY-GaussPlume は研究・工程評価向けの大気汚染拡散シミュレーション基盤です。ASP.NET Core 9、Vue 3、Leaflet、ガウスプルームモデルを組み合わせ、排出源管理、気象、シミュレーション、寄与分析を検証可能なワークフローにします。

## ユースケース

- 環境評価で排出源と風況を比較する。
- 濃度場と受容点影響を地図上で見ながら工程案を議論する。
- 研究、教育、納品レビューでモデルシナリオを再現する。

## 次に読む

- [クイックスタート](quick-start.md)
- [アーキテクチャ](ARCHITECTURE.md)
- [API](API.md)
- [ワークフロー](WORKFLOW.md)
- [FAQ](faq.md)
