# SimpleLauncher

Windows 用のタスクトレイ常駐ランチャーです。グローバルホットキー (既定: `Ctrl+Alt+O`) でウィンドウを呼び出し、
次の項目をあいまい検索してすばやく起動します。

- スタートメニューに登録されたアプリ (exe / UWP)
- デスクトップ直下のファイル
- 設定 YAML に定義したコマンド
- 最近使ったファイル (Recent タブ)

あいまい検索は [fzf](https://github.com/junegunn/fzf) の FuzzyMatchV2 アルゴリズムを C# に移植したもの
(`Services/Fuzzy/FzfMatcher.cs`) で、外部プロセスは使いません。All タブは fzf の `default` スキーム、
Recent タブはパス向けの `path` スキームでスコアリングします。

## キー操作

| キー | 動作 |
| --- | --- |
| `Enter` | 選択中の項目を起動 |
| `Ctrl+Enter` | 管理者として起動 |
| `Ctrl+I` | 項目の場所をエクスプローラーで開く |
| `Ctrl+N` / `Ctrl+P` | 選択を下 / 上へ移動 |
| `Ctrl+F` / `Ctrl+PageDown` | 次のタブへ |
| `Ctrl+PageUp` | 前のタブへ |
| `Ctrl+U` | アプリ一覧と設定を再読み込み |
| `Esc` | ウィンドウを隠す |

ウィンドウを閉じても終了せず隠れるだけです。終了はタスクトレイのメニューから行います。

## 設定

- `appsettings.json` (実行ファイルと同じ場所): `HotKey` でホットキーを変更できます。
- `%APPDATA%\SimpleLauncher\simple-launcher.yaml`: コマンド定義。初回起動時に `launcher.yaml` からコピーされます。
  一覧で `--edit` を選ぶとこのファイルの場所が開きます。

```yaml
version: 1
alias:
  chrome: C:\Program Files\Google\Chrome\Application\chrome.exe
list:
  - |-
    google.com      # 一覧に表示する名前
    chrome          # 実行ファイル (alias 可、PATH から探索)
    https://www.google.com   # 引数 (省略可)
```

## ビルドとテスト

.NET 8 SDK (Windows) が必要です。

```
dotnet build SimpleLauncher.sln
dotnet test SimpleLauncher.sln
```

## 構成

```
SimpleLauncher/
  App.xaml(.cs)        エントリポイント。多重起動抑止、タスクトレイ、依存の組み立て
  Views/               MainWindow。キー入力を ViewModel の操作へ変換する
  ViewModels/          MainWindowVM。一覧・フィルタ・選択・起動の状態
  Services/            アプリ走査、最近使ったファイル、起動情報の組み立て
  Services/Fuzzy/      fzf FuzzyMatchV2 の移植とスコアリングのスキーム
  Config/              appsettings.json と simple-launcher.yaml の読み込み
  Controls/            一致文字を強調表示する TextBlock
  Interop/             グローバルホットキー
  Models/              一覧項目などのデータ型
SimpleLauncher.Tests/  xunit による単体テスト
```
