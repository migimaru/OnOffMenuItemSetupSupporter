# Changelog

このプロジェクトの変更履歴を記録します。

## [0.3.0] - 2026-10-23

### Added

- 衣装全体と衣装内のMeshをハンドルで直接並べ替え、適用時のメニュー順へ反映
- 設定・編集画面の列幅を列見出し右端のドラッグで調整可能に
- Mesh名のツールチップで全文を表示
- 通常・編集で共通の衣装追加欄と設定項目
- 適用履歴を連続で戻せる「一つ前に戻す」ボタン
- 削除したメニュー、Prefabのメニュー設定、プリセット参照の復元処理

### Changed

- 設定項目と列見出しを固定し、衣装ごとのMesh一覧だけをスクロールする共通表に変更
- Mesh名の描画幅を基準に初期幅を決定し、手動調整後と並べ替え後は指定幅を維持
- MenuFolderを含む列幅を個別に保持し、列を縮めた際やウィンドウを広げた際の他列への幅の再配分を廃止
- EditorOnly除外の対象がMeshであることを設定名に明記
- 通常起動は空の入力からの上書き、編集起動は既存内容の読み込みに統一
- 右下の操作を「変更リセット」「変更適用」「完了」「キャンセル」に統一
- 完了は適用して閉じ、キャンセルと×は未適用の変更を戻して閉じる
- 子MenuFolder内のメニューは、対象に含めるチェックがONの場合だけ変更・削除（通常は初期ON、編集は初期OFF）
- 設定項目群の下に暗い背景の衣装ドロップ欄と追加ボタンを配置し、重複見出し・動作列・上書き説明文を削除
- 変更リセットは登録先・表示・EditorOnlyだけを戻し、衣装一覧の追加・削除・並べ替えを維持
- 他の機能を持つオブジェクトの登録先変更は、本体を移動せずメニュー設定だけを変更
- Unity内にタブとして配置できる通常ウィンドウに変更し、最小幅を350に縮小

### Fixed

- 子MenuFolderへの登録対象がない場合に、空の手動フォルダーまで削除していた問題を修正
- Mesh行のクリックや並べ替え後に青い選択背景が残る問題を修正し、ハンドルを掴んでいる間だけ強調表示
- 新しく追加したMeshの初期選択を「登録しない」に修正
- 編集画面の横スクロール時に列見出しと各行がずれる問題を修正

## [0.2.1] - 2026-09-24

### Fixed

- 配布バージョンとダウンロードURLを0.2.1へ更新

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
