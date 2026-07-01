# Token Checker for Windows

タスクバーに Claude Code と Codex の使用率を常時表示する Windows アプリ。

macOS 版 [Token Checker](https://github.com/satonico/Token-Checker) の Windows 移植版。

## 動作要件

- Windows 10 / 11（64bit）
- Claude Code CLI（`claude auth login` 済み）
- Codex CLI（`npm i -g @openai/codex` 後、`codex login` 済み）

どちらか一方のみでも動作する。

## インストール

### 方法A: exe をダウンロード（推奨・無設定）

[Releases](https://github.com/satonico/Token-Checker-win/releases) から `TokenChecker.exe` をダウンロードしてダブルクリックするだけ。.NET ランタイムを同梱しているため、**.NET のインストールは不要**。

> **「Windows によって PC が保護されました」と表示された場合**
> 署名のないアプリをネットからダウンロードすると Windows SmartScreen が警告を出します（マルウェアという意味ではありません）。次のいずれかで起動できます。
> - 警告画面の「**詳細情報**」→「**実行**」をクリック
> - または exe を右クリック →「プロパティ」→「**ブロックの解除**」にチェック → OK
>
> 警告を一切出したくない場合は、方法B でソースから自分でビルドした exe を使う（自分でビルドした exe には警告が出ない）。

### 方法B: ソースからビルド

開発者向け。先に [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) が必要（`winget install Microsoft.DotNet.SDK.8` でも可。インストール後は PowerShell を開き直す）。

```powershell
git clone https://github.com/satonico/Token-Checker-win.git
cd Token-Checker-win
dotnet build TokenChecker.sln -c Release
```

出力先: `TokenChecker\bin\Release\net8.0-windows\TokenChecker.exe`

`.exe` 単体で他PCに配布したい場合は、ランタイム同梱版を発行する。

```powershell
dotnet publish TokenChecker\TokenChecker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\
```

`publish\TokenChecker.exe` が .NET 不要で動く単一ファイルになる。

## 使い方

1. 事前にターミナルでログインしておく

```powershell
claude auth login
codex login
```

2. `TokenChecker.exe` を起動するとタスクバー上にウィジェットが表示される
3. ウィジェットをクリックするとポップアップで詳細（使用率・リセット時間・更新間隔設定）が開く

## Ubuntu（WSL）に Claude をインストールしている場合

Windows の PowerShell / コマンドプロンプトでは `claude` コマンドが使えず、WSL（Ubuntu 等）の中にのみ Claude Code をインストールしている場合の手順。

### ログイン方法

アプリ起動後、ポップアップ右上の「**ログイン**」を押すとログインウィンドウが開く。

- **「ブラウザでログイン」ボタン** → Windows の PATH にある `claude` を使う（通常の Windows インストール向け）
- **「WSL」ボタン** → WSL 内の `claude` を使う。押すと `wsl -- claude auth login` を実行するターミナルが開き、ブラウザ経由でログインできる

「WSL」ボタンでログインすると、認証情報は WSL 内（`~/.claude/.credentials.json`）に保存される。アプリは WSL のファイルシステムを自動で参照するため、**ログイン完了後はそのまま使用率が表示される**。

### 動作確認済み環境

| 環境 | 動作 |
|---|---|
| Windows に直接 `claude` をインストール | 「ブラウザでログイン」ボタンで通常ログイン |
| WSL（Ubuntu 等）にのみ `claude` をインストール | 「WSL」ボタンでログイン、以降は自動で認証情報を読み取り |

> WSL2 + Windows 11 環境での動作を確認している。Windows 10 + WSL2 でもブラウザが自動で開く場合は同様に動作する。

## アンインストール

PowerShell で以下を順に実行する（対象が無くてもエラーにならない）。

```powershell
# 1. アプリを終了（起動中だとファイルがロックされ削除に失敗するため）
Stop-Process -Name TokenChecker -Force -ErrorAction SilentlyContinue

# 2. 自動起動の登録を削除（登録が無い場合はスキップ）
reg delete "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v TokenChecker /f 2>$null

# 3. 設定・キャッシュを削除（フォルダが無い場合はスキップ）
Remove-Item "$env:APPDATA\TokenChecker" -Recurse -Force -ErrorAction SilentlyContinue
```

あとはビルド／ダウンロードした `TokenChecker.exe`（やクローンしたフォルダ）を削除すれば完了。Claude / Codex の認証情報は CLI 側（`claude` / `codex`）の管理なので、本アプリのアンインストールでは消さない。

## 更新履歴

### v0.4.0
- タスクバーウィジェットのアイコンをドーナツチャートに変更（使用率に応じて緑→黄→赤で変化）
- 直近5時間の使用がない場合、「5時間ウィンドウのデータがありません」を表示するよう変更
- 認証切れ時に表示されるログインウィンドウが閉じられないバグを修正（キャンセル・完了ボタンの不応答、WSL ログイン後の自動検出不具合を含む）
- GPU メモリリークを修正（描画ブラシの静的キャッシュ化）

### v0.3.0
- 使用メモリを大幅に削減（Codex プロセスをポーリング毎に終了するよう変更）
- 認証が切れた際にバルーン通知 + ログインウィンドウを自動表示
- トレイアイコン・ブラシのアロケーションを最適化

### v0.2.0
- 詳細ポップアップのデザインをシンプルに変更（カード形式 → セパレータ区切りのフラットレイアウト）

### v0.1.0
- 初回リリース

## ライセンス

[MIT License](LICENSE) © 2026 satonico224

## 免責事項

本ソフトウェアは現状有姿 (as-is) で提供されるものであり、動作・安全性・正確性について一切の保証を行わない。本ソフトウェアの利用に起因して発生したいかなる損害（データ損失、アカウント停止、トークン漏洩、セキュリティインシデント等を含むがこれに限らない）についても、作者は一切の責任を負わない。利用者自身の責任において使用すること。
