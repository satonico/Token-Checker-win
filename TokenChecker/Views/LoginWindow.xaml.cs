using System.Diagnostics;
using System.Windows;
using TokenChecker.Models;
using TokenChecker.Services;
using TokenChecker.ViewModels;

namespace TokenChecker.Views;

public partial class LoginWindow : Window
{
    private readonly string _cliCommand;
    private readonly WindowsTokenSource? _tokenSource;
    private readonly UsageViewModel _vm;
    private readonly bool _watchClaude;
    private string? _tokenSnapshot;
    private CancellationTokenSource? _pollCts;
    private bool _closing;

    public LoginWindow(string service, string cliCommand, UsageViewModel vm, WindowsTokenSource? tokenSource = null)
    {
        _cliCommand  = cliCommand;
        _tokenSource = tokenSource;
        _vm          = vm;
        _watchClaude = tokenSource != null;

        InitializeComponent();

        TitleLabel.Text         = $"{service} ログイン";
        OpenTerminalBtn.Content = $"ブラウザでログイン ({cliCommand})";
        DescLabel.Text          =
            "「ブラウザでログイン」を押すとターミナルが開きます。\n" +
            "WSL（Ubuntu）にのみインストールしている場合は「WSL」ボタンをご利用ください。\n" +
            "ブラウザが開かない場合は「コマンドをコピー」でご自身のターミナルに貼り付けて実行してください。\n" +
            "ログイン完了後、このウィンドウは自動的に閉じます。";

        Loaded += async (_, _) =>
        {
            await SnapshotCurrentTokenAsync();
            DoneBtn.IsEnabled = true;
            if (_tokenSource != null)
                _ = PollForNewTokenAsync();
        };

        Closed += (_, _) =>
        {
            _pollCts?.Cancel();
            _vm.SnapshotChanged -= OnSnapshotChanged;
        };

        _vm.SnapshotChanged += OnSnapshotChanged;
    }

    // ── App-level polling による認証回復検出 ──────────────────────────────

    private void OnSnapshotChanged()
    {
        if (_closing) return;
        var snap = _vm.Snapshot;
        var authError = _watchClaude
            ? snap.ClaudeError?.Kind is DomainErrorKind.TokenMissing or DomainErrorKind.AnthropicUnauthorized
            : snap.CodexError?.Kind  is DomainErrorKind.CodexUnauthorized or DomainErrorKind.CodexRpcError;
        if (authError || snap.FetchedAt == DateTime.MinValue) return;

        _closing = true;
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded) return;
            ShowStatus("✓ ログイン完了！ウィンドウを閉じます...");
            var timer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(1400) };
            timer.Tick += (_, _) => { timer.Stop(); if (IsLoaded) Close(); };
            timer.Start();
        });
    }

    // ── Initialization ────────────────────────────────────────────────────

    private async Task SnapshotCurrentTokenAsync()
    {
        if (_tokenSource == null) return;
        try { _tokenSnapshot = await _tokenSource.ReadAccessTokenAsync(); }
        catch { _tokenSnapshot = null; }
    }

    // ── Button handlers ───────────────────────────────────────────────────

    private void OpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/k {_cliCommand}")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            ShowStatus("ターミナルを開けませんでした。「コマンドをコピー」でご自身のターミナルに実行してください。");
            return;
        }

        // OAuth のブラウザ/ターミナル作業を妨げないよう最前面固定を解除する。
        Topmost = false;

        OpenTerminalBtn.IsEnabled = false;
        DoneBtn.IsEnabled         = true;
        ShowStatus("ブラウザでログインしてください。完了すると自動的に閉じます。");

        if (_tokenSource != null)
            _ = PollForNewTokenAsync();
    }

    private void OpenWsl_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // bash -il で .bashrc + .profile の両方を読み込み、claude の PATH を有効にする
            // シングルクォートは cmd.exe に解釈されないため wsl 側へそのまま渡る
            Process.Start(new ProcessStartInfo("cmd.exe", $"/k wsl -- bash -il -c '{_cliCommand}'")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            ShowStatus("WSLを開けませんでした。WSL（Ubuntu等）がインストールされているか確認してください。");
            return;
        }

        Topmost = false;
        OpenTerminalBtn.IsEnabled = false;
        OpenWslBtn.IsEnabled      = false;
        DoneBtn.IsEnabled         = true;
        ShowStatus("WSLターミナルでログインしてください。完了すると自動的に閉じます。");

        if (_tokenSource != null)
            _ = PollForNewTokenAsync();
    }

    private void CopyCommand_Click(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText(_cliCommand); }
        catch { }
        ShowStatus($"コピーしました: {_cliCommand}\nご自身のターミナルに貼り付けて実行してください。");
        DoneBtn.IsEnabled = true;
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        _pollCts?.Cancel();
        _ = _vm.RefreshAsync(force: true);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _pollCts?.Cancel();
        Close();
    }

    // ── Token polling ─────────────────────────────────────────────────────

    private async Task PollForNewTokenAsync()
    {
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = _pollCts.Token;

        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(3000, ct); }
            catch (OperationCanceledException) { break; }

            try
            {
                var token = await _tokenSource!.ReadAccessTokenAsync(ct);
                if (token != _tokenSnapshot)
                {
                    Dispatcher.Invoke(() => ShowStatus("✓ ログイン完了！ウィンドウを閉じます..."));
                    await Task.Delay(1400, ct);
                    Dispatcher.Invoke(() =>
                    {
                        _ = _vm.RefreshAsync(force: true);
                        Close();
                    });
                    return;
                }
            }
            catch (DomainError e) when (e.Kind == DomainErrorKind.TokenMissing) { }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ShowStatus(string message)
    {
        StatusLabel.Text      = message;
        StatusArea.Visibility = Visibility.Visible;
    }
}
