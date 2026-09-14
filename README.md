# On/Off Menu Item Setup Supporter for lilycalInventory

VRChatアバター向けに、lilycalInventoryの `ItemToggler` を使用したON/OFFメニュー作成を補助するUnity Editor拡張です。

衣装やアクセサリーに含まれるMeshを一覧表示し、MenuFolderごとにItemTogglerをまとめて生成できます。

## インストール

VCC（VRChat Creator Companion）からの導入を推奨します。

### VPM Repository

Repository URL:

`https://nuko615.github.io/vpm/index.json`

1. VCCを開く
2. `Settings > Packages` を開く
3. `Add Repository` を選択
4. 上記Repository URLを入力して追加
5. 対象プロジェクトの `Manage Project` を開く
6. `On/Off Menu Item Setup Supporter for lilycalInventory` を追加

VPM Repositoryページ:
https://nuko615.github.io/vpm/

## 主な機能

- `ONで非表示`
- `OFFで非表示`
- 複数の衣装Rootに対応
- SkinnedMeshRendererを自動検出
- MeshRenderer + MeshFilterを自動検出
- Hierarchy上のInactive状態を初期状態として維持
- EditorOnlyのMeshを登録対象から除外可能
- EditorOnlyのMenuFolderを登録先から除外
- 同一Meshの重複登録を防止
- 子MenuFolderへの振り分け
- 子MenuFolderをItemTogglerの上または下へ配置
- 8項目制限に合わせたNextPage自動生成
- 既存の自動NextPageの空き枠を再利用
- 追加 / 上書き
- Undo対応
- 未使用MenuFolderの案内
- Mesh名クリックによるHierarchy選択

## 必要環境

- Unity 2022.3
- VRChat Avatars SDK
- lilycalInventory

本ツールはlilycalInventoryを使用してItemTogglerを生成します。
lilycalInventoryは本パッケージには同梱されません。

`package.json` では以下の範囲を依存関係として指定しています。

- lilycalInventory: `>=1.3.1 <2.0.0`
- VRChat Avatars SDK: `>=3.8.2 <3.11.0`

## 動作確認済み環境

Unity 2022.3.6f1で確認しています。

lilycalInventoryについては、VRChat Avatars SDK 3.8.2との組み合わせで以下を確認しています。

- 1.3.1
- 1.4.0
- 1.4.1
- 1.4.5
- 1.5.0
- 1.5.2

VRChat Avatars SDKについては、lilycalInventory 1.5.2との組み合わせで以下を確認しています。

- 3.8.2
- 3.9.0
- 3.10.0
- 3.10.5

上記以外の依存範囲内バージョンについては、個別の動作確認を行っていない場合があります。

## 使い方

### 1. ItemOffMenuを作成

VRChat Avatar Rootを右クリックし、
`On／Off Menu Item Setup Supporter > Create ItemOffMenu`
を実行します。

`ItemOffMenu > Accessory` が生成されます。

同名のItemOffMenuが存在する場合は、
`ItemOffMenu2 > Accessory2` のように空いている番号が自動的に使用されます。

### 2. ItemTogglerを設定

lilycalInventoryのMenuFolderが設定されているGameObjectを右クリックし、
`ONで非表示` または `OFFで非表示` を選択します。

### 3. 衣装を登録

Hierarchy上の衣装やアクセサリーのRootをツールへドラッグ＆ドロップします。
対象Root以下からMeshが自動的に検出されます。

各Meshについて、表示状態、EditorOnly、登録先MenuFolder、登録しない、を設定できます。

## ONで非表示

ToggleがONになると対象を非表示にします。

- OFF: 表示
- ON: 非表示

対象がHierarchy上でInactiveの場合は、初期Toggle状態も自動的に反転されます。

## OFFで非表示

ToggleがOFFになると対象を非表示にします。

- ON: 表示
- OFF: 非表示

対象がHierarchy上でInactiveの場合は、初期Toggle状態も自動的に反転されます。

## 8項目以内に自動調整

VRChat Expression Menuの項目数を考慮し、
Back、子MenuFolder、ItemToggler、NextPageの合計が8項目以内になるよう、
自動的にNextPageを生成します。

自動生成されたNextPageには識別用Componentが追加されます。
このComponentはEditor上での識別にのみ使用され、アバターの動作には影響しません。

## EditorOnly

### Mesh

`EditorOnlyにはメニューを作成しない` が有効な場合、
EditorOnlyのMeshにはItemTogglerを生成しません。
親GameObjectがEditorOnlyの場合も対象になります。

### MenuFolder

EditorOnlyになっているMenuFolderは一覧には表示されますが、
`-` と表示され、登録先として選択できません。

## 未使用MenuFolder

使用されていないMenuFolderが存在すると、以下を表示します。

未使用のMenuFolder: Accessory  
使用しない場合はHierarchyから削除かEditorOnlyに変更してください。

ツールが自動的に削除やEditorOnly化を行うことはありません。

## 追加

既存のItemTogglerを残したまま新しいItemTogglerを追加します。
自動生成済みNextPageに空きがある場合は、その空きから使用します。
登録対象が0件の場合は追加ボタンを押せません。

## 上書き

既存のItemTogglerと、本ツールが自動生成したNextPageを再生成します。
手動で作成したMenuFolderやNextPageは削除しません。

ItemTogglerと同じGameObjectに他のComponentや子GameObjectが存在する場合は、
GameObject自体を削除せずItemToggler Componentのみ削除します。

## Undo

追加・上書き操作は、原則として1回のUndoで元に戻せるように実装されています。

## テスト

EditMode TestでItemToggler生成、ON/OFF初期値、Active / Inactive、
NextPage生成、8項目制限、子MenuFolder順、追加、上書き、Undo、
EditorOnly、Mesh重複防止を検証しています。

テストコードはGitHubリポジトリには含まれますが、
VPM配布パッケージには含めません。

## ライセンス

MIT License
