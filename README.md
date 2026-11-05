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
| `Tab` | 選択中の項目に対するアクションメニューを開く |
| `Ctrl+N` / `Ctrl+P` | 選択を下 / 上へ移動 |
| `Ctrl+F` / `Ctrl+PageDown` | 次のタブへ |
| `Ctrl+PageUp` | 前のタブへ |
| `Ctrl+U` | アプリ一覧と設定を再読み込み |
| `Esc` | ウィンドウを隠す (アクションメニュー表示中はメニューを閉じる) |

`Enter`、`Ctrl+Enter`、`Ctrl+I` は組み込みアクションのキー割り当てで、設定で変更・追加できます。
アクションメニューでは名前であいまい検索し、`Ctrl+N` / `Ctrl+P` で選んで `Enter` で実行します。

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

### アクション

`actions` に、選択中の項目に対して行う操作を定義できます。`Tab` のメニューに並ぶほか、`key` を書けば直接実行できます。

```yaml
actions:
  - name: ターミナルで開く
    key: Ctrl+T
    exec: wt
    args: -d "{dir}"
  - name: VS Code で開く
    key: Ctrl+O
    exec: code
    args: "{dir}"
  - name: 管理者として実行   # 組み込みと同名なので上書きになる
    key: Shift+Enter
    verb: runas
```

- `{path}`: 項目の実体。アプリならショートカットのリンク先、ファイルならそのパス、コマンドはエクスプローラーを開くものに限りその引数。
- `{dir}`: `{path}` のあるフォルダ。`{path}` 自体がフォルダならそのフォルダ。
- `exec` は `alias` と PATH で解決されます。省略すると項目そのものを `verb` (`open` / `runas`) で起動します。
- 組み込みの「開く (Enter)」「管理者として実行 (Ctrl+Enter)」「場所を開く (Ctrl+I)」は常にあり、同じ `name` を書くと置き換わります。
- 同じ `key` を複数に割り当てた場合は後に定義したものが有効です。`Ctrl+N` などの移動キーはアクションより優先されます。

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
