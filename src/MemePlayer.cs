namespace Loupedeck.SpanishMemesPlugin;

using System.Diagnostics;

internal sealed class MemePlayer
{
    private static readonly Uri Trending = new("https://www.myinstants.com/en/index/es/");
    private readonly HttpClient http;
    private readonly Action<string> log;
    private readonly CancellationToken stop;
    private readonly Func<string, CancellationToken, Task> playFile;
    private readonly object sync = new();
    private CancellationTokenSource? playing;
    private Uri[] sounds = [];
    private DateTimeOffset updated;
    private int lastIndex = -1;

    internal MemePlayer(Action<string> log, CancellationToken stop, HttpClient? http = null, Func<string, CancellationToken, Task>? playFile = null)
    {
        this.log = log;
        this.stop = stop;
        this.playFile = playFile ?? PlayWithAfplayAsync;
        this.http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
        this.http.DefaultRequestHeaders.UserAgent.ParseAdd("SpanishMemesKeypad/0.1 (personal-use prototype)");
    }

    internal async Task WarmAsync()
    {
        try { await this.RefreshAsync(this.stop); }
        catch (OperationCanceledException) { }
        catch (Exception e) { this.log($"Unable to fetch trending sounds: {e.Message}"); }
    }

    internal void Stop()
    {
        lock (this.sync) this.playing?.Cancel();
    }

    internal async Task PlayAsync()
    {
        CancellationTokenSource current;
        lock (this.sync)
        {
            if (this.playing is not null) return;
            current = CancellationTokenSource.CreateLinkedTokenSource(this.stop);
            this.playing = current;
        }
        try
        {
            var token = current.Token;
            if (this.sounds.Length == 0 || DateTimeOffset.UtcNow - this.updated > TimeSpan.FromHours(1))
            {
                try { await this.RefreshAsync(token); }
                catch (Exception e) when (e is not OperationCanceledException && this.sounds.Length > 0) { this.log($"Using cached catalogue: {e.Message}"); }
            }
            token.ThrowIfCancellationRequested();
            if (this.sounds.Length == 0) throw new InvalidOperationException("No sounds found on the trending page");
            var index = Random.Shared.Next(this.sounds.Length);
            if (this.sounds.Length > 1 && index == this.lastIndex) index = (index + 1 + Random.Shared.Next(this.sounds.Length - 1)) % this.sounds.Length;
            this.lastIndex = index;
            var uri = this.sounds[index];
            this.log($"Playing {uri.AbsolutePath}");
            await this.DownloadAndPlayAsync(uri, token);
        }
        catch (OperationCanceledException) { this.log("Playback cancelled"); }
        catch (Exception e) { this.log($"Playback failed: {e.Message}"); }
        finally
        {
            lock (this.sync)
            {
                this.playing = null;
                current.Dispose();
            }
        }
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        using var response = await this.http.GetAsync(Trending, token);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(token);
        var found = TrendingCatalog.Parse(html);
        if (found.Length == 0) throw new InvalidOperationException("No playable sound links found in trending page");
        this.sounds = found;
        this.updated = DateTimeOffset.UtcNow;
        this.log($"Loaded {found.Length} trending sounds");
    }

    private async Task DownloadAndPlayAsync(Uri uri, CancellationToken token)
    {
        var file = Path.Combine(Path.GetTempPath(), $"spanish-memes-{Guid.NewGuid():N}.mp3");
        try
        {
            using var response = await this.http.GetAsync(uri, token);
            response.EnsureSuccessStatusCode();
            await using (var output = File.Create(file)) await response.Content.CopyToAsync(output, token);
            token.ThrowIfCancellationRequested();
            await this.playFile(file, token);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    internal static async Task PlayWithAfplayAsync(string file, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var process = Process.Start(new ProcessStartInfo("/usr/bin/afplay") { UseShellExecute = false, ArgumentList = { file } }) ?? throw new InvalidOperationException("Could not start afplay");
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync();
            throw;
        }
        if (process.ExitCode != 0) throw new InvalidOperationException($"afplay exited with {process.ExitCode}");
    }
}
