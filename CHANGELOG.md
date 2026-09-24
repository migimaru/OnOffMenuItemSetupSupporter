# Changelog

このプロジェクトの変更履歴を記録します。

## [0.2.0] - 2026-09-24

### Added

- 既存ItemTogglerを編集する機能を追加
- `[編集] ONで非表示` / `[編集] OFFで非表示` を追加
- 子MenuFolderを含めた編集に対応
- 既存の複数Target ItemTogglerの維持・分割に対応
- Active / EditorOnlyの編集に対応

### Fixed

- 自動生成したItemTogglerのmenuNameを固定しないよう修正
- Hierarchy上でItemToggler名を変更した際、lilycalInventoryのメニュー名も追従するよう修正

## [0.1.1] - 2026-09-14

### Changed

- GitHubユーザー名変更に伴いRepository / Release / ChangelogのURLを更新
- VPM Repository URLを新しいGitHubユーザー名に合わせて更新
- `package.json` のバージョンを0.1.1へ更新
- `author.url` を追加

### Notes

- ツール本体の機能変更はありません

## [0.1.0] - 2026-09

### Added

- 初回公開版
- Avatar RootからItemOffMenu / Accessoryを自動生成
- 同名ItemOffMenuの自動ナンバリング
- `ONで非表示`
- `OFFで非表示`
- 複数衣装Root対応
- SkinnedMeshRenderer検出
- MeshRenderer + MeshFilter検出
- Active / Inactive初期状態対応
- EditorOnly Mesh対応
- EditorOnly MenuFolder選択防止
- MenuFolder振り分け
- 子MenuFolder上下配置
- 8項目以内への自動調整
- NextPage自動生成
- 自動NextPage識別Component
- 既存NextPageの空き枠利用
- ItemToggler追加
- ItemToggler上書き
- 安全な既存ItemToggler削除
- Undo対応
- Mesh選択機能
- Mesh重複登録防止
- 未使用MenuFolder案内
- EditMode Test
