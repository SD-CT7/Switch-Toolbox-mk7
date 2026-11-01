# Switch Toolbox 修正一式(最新版)

`source/` の中身は、リポジトリのルートに、**同じフォルダ構成のまま上書き**します(全ファイルが、元のファイルの完全な置き換えです)。
`runtime/` は、ビルドしたToolboxのフォルダに、手で置くものです(任意)。

構文は、全ファイルで確認済みです。Toolbox全体のビルドと、画面での確認は、していません。

## 3DS(BCLYT / BCLAN)の描画

| ファイル | 内容 |
|---|---|
| `Toolbox/Shader/Layout/Legacy/Bflyt.frag` | 先頭に `#version 130` を追加(Mesaで `switch` が通らず、パネルが白一色になる問題) |
| `CTR/BclytShader.cs` | 回転アニメーション(RotateX/Y/Z)の反映 / TEV(Combiners)のプレビュー用シェーダー `BclytTevShader` / MaterialColor(黒・白)の反映 / テクスチャごとのSRT |
| `BxlytShader.cs` | テクスチャの回転の中心を、画像の中央に修正 / テクスチャごとのSRT |
| `BxlytToGL.cs` | 頂点色アニメーションの反映 / TEV材質で、専用シェーダーを使う |
| `CTR/Materials/TextureRef.cs` | ラップモード・フィルターを、画面から変更可能に / 新規テクスチャの初期値を、Clamp + 線形に |
| `Texture/BCLIM.cs` | L4(12)・A4(13)形式を、3DSの規格どおりに読む |

## BCLYTの保存

| ファイル | 内容 |
|---|---|
| `CTR/MAT1.cs` | テクスチャ追加(Add)後の保存で、型の変換エラーになる問題 |
| `CTR/Materials/TevStage.cs` | 保存で `ConstColors` が欠ける問題(読み直しで値が壊れる原因) / 編集内容を保存に反映 |

## アニメーションの読み込みと再生

| ファイル | 内容 |
|---|---|
| `Animation/LytAnimGroup.cs` | テクスチャSRT・テクスチャパターンを、`Index`(テクスチャ番号)ごとのグループに分ける |
| `Animation/LytAnimation.cs` | 同上の再生 / 表示・非表示アニメーションの記録 |
| `Common.cs` | Animation Windowの一覧(CLANのパネル名の長さ) / テクスチャごとのSRT値 / 表示・非表示の記録 / グループの重複判定(名前 + 種類) |
| `GUI/BFLYT/LayoutViewer.cs` | アニメーションで隠したペインと子ペインを描かない |

## 画面(エディタ)

| ファイル | 内容 |
|---|---|
| `GUI/BFLYT/LayoutEditor.cs` | 同じBCLANが2つ並ぶ問題 / `Animation` メニューに「Edit Animation Texture List...」を追加 |
| `GUI/BFLYT/Animations/LayoutAnimList.cs` | 同じBCLANが2つ並ぶ問題 |
| `GUI/BFLYT/Animations/Basic/LayoutAnimTreeLoader.cs` | 追加するターゲットの `Index` を、ゲームの形式に合わせる |
| `GUI/BFLYT/Animations/Basic/AddAnimGroupDialog.cs` | 同じ名前のペインと材質を、別々に追加できる |
| `GUI/BFLYT/Animations/Basic/AddGroupTypeDialog.cs` | TextureSRTを、材質側の選択肢に移す(再生は、材質でしか行われないため) |
| `GUI/BFLYT/Editor/Materials/PaneMatBlending.cs` | 壊れた値で、Blendingタブが落ちる問題 |

## 実行時の追加ファイル(任意) `runtime/`

`Lib/SarcHashes/` に、SARCのハッシュ辞書を入れています。ビルドしたToolboxの `Lib\SarcHashes` に置いてください。

- `EFE_HashTable.txt`:EveryFileExplorerの辞書(3441件)から作った名前リスト
- `Custom_Names.txt` / `.saht`:追加の名前(`mode_race.bclim`)

## 既知の未対応

- TEVの「結果をレジスタに保存」の指定(SavePrev)は、反映していません
- TEVとMaterialColorの、8番以降の定数色は、アニメーションに対応していません
- ウィンドウパネルの枠と、テキストは、従来のシェーダーのままです
- Wii用(BRLYT)の描画は、変えていません
- 画像からBCLIMを作る機能は、ありません(EFEなどで作ってください)
