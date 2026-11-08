namespace Loupedeck.SpanishMemesPlugin;

using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal sealed class MemePlayer
{
    private static readonly Uri Trending = new("https://www.myinstants.com/en/index/es/");
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan MinBackoff = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultBackoff = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(24);
    private static readonly string CacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Caches", "SpanishMemesKeypad");
    private static readonly string CatalogPath = Path.Combine(CacheDirectory, "catalog.json");

    private readonly HttpClient http;
    private readonly Action<string> log;
    private readonly CancellationToken stop;
    private readonly Func<string, CancellationToken, Task> playFile;
    private readonly object sync = new();
    private CancellationTokenSource? playing;
    private Uri[] sounds = [];
    private DateTimeOffset updated;
    private DateTimeOffset blockedUntil;
    private int lastIndex = -1;

    internal MemePlayer(Action<string> log, CancellationToken stop, HttpClient? http = null, Func<string, CancellationToken, Task>? playFile = null)
    {
        this.log = log;
        this.stop = stop;
        this.playFile = playFile ?? PlayWithAfplayAsync;
        this.http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
        this.http.DefaultRequestHeaders.UserAgent.ParseAdd("SpanishMemesKeypad/0.1 (personal-use prototype)");
        this.LoadCatalog();
    }

    private bool NeedsRefresh => this.sounds.Length == 0 || DateTimeOffset.UtcNow - this.updated > CatalogTtl;

    internal async Task WarmAsync()
    {
        try { if (this.NeedsRefresh) await this.RefreshAsync(this.stop); }
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
            if (this.NeedsRefresh)
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
            await this.PlaySoundAsync(uri, token);
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
        this.ThrowIfBlocked();
        using var response = await this.http.GetAsync(Trending, token);
        this.RegisterBackoff(response);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(token);
        var found = TrendingCatalog.Parse(html);
        if (found.Length == 0) throw new InvalidOperationException("No playable sound links found in trending page");
        this.sounds = found;
        this.updated = DateTimeOffset.UtcNow;
        this.SaveCatalog();
        this.log($"Loaded {found.Length} trending sounds");
    }

    private async Task PlaySoundAsync(Uri uri, CancellationToken token)
    {
        var file = CachePathFor(uri);
        if (!File.Exists(file)) await this.DownloadAsync(uri, file, token);
        token.ThrowIfCancellationRequested();
        try { await this.playFile(file, token); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Drop a file that cannot be played so the next press downloads it again.
            try { File.Delete(file); }
            catch (IOException) { }
            throw;
        }
    }

    private async Task DownloadAsync(Uri uri, string file, CancellationToken token)
    {
        this.ThrowIfBlocked();
        Directory.CreateDirectory(CacheDirectory);
        var partial = file + ".part";
        try
        {
            using var response = await this.http.GetAsync(uri, token);
            this.RegisterBackoff(response);
            response.EnsureSuccessStatusCode();
            await using (var output = File.Create(partial)) await response.Content.CopyToAsync(output, token);
            File.Move(partial, file, overwrite: true);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    private void ThrowIfBlocked()
    {
        DateTimeOffset until;
        lock (this.sync) until = this.blockedUntil;
        if (DateTimeOffset.UtcNow < until) throw new InvalidOperationException($"requests paused until {until.ToLocalTime():HH:mm} after a block from the site");
    }

    private void RegisterBackoff(HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden or HttpStatusCode.ServiceUnavailable)) return;
        var retry = response.Headers.RetryAfter;
        var wait = retry?.Delta ?? (retry?.Date is { } date ? date - DateTimeOffset.UtcNow : DefaultBackoff);
        if (wait < MinBackoff) wait = MinBackoff;
        if (wait > MaxBackoff) wait = MaxBackoff;
        lock (this.sync) this.blockedUntil = DateTimeOffset.UtcNow + wait;
        this.log($"Myinstants answered {(int)response.StatusCode}; pausing requests for {wait.TotalMinutes:0} min");
    }

    private void LoadCatalog()
    {
        try
        {
            if (!File.Exists(CatalogPath)) return;
            var file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(CatalogPath));
            if (file is null || file.Urls.Length == 0) return;
            this.sounds = file.Urls.Select(url => new Uri(url)).ToArray();
            this.updated = file.Updated;
            this.log($"Loaded {this.sounds.Length} sounds from cache");
        }
        catch (Exception e) when (e is IOException or JsonException or UriFormatException or NotSupportedException)
        {
            this.log($"Ignoring cached catalogue: {e.Message}");
        }
    }

    private void SaveCatalog()
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            File.WriteAllText(CatalogPath, JsonSerializer.Serialize(new CatalogFile(this.updated, this.sounds.Select(uri => uri.AbsoluteUri).ToArray())));
            PruneCache();
        }
        catch (IOException e) { this.log($"Unable to save catalogue: {e.Message}"); }
    }

    // Keeps only the sounds that are still in the current catalogue, so the cache stays bounded.
    private void PruneCache()
    {
        var keep = this.sounds.Select(CachePathFor).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(CacheDirectory, "*.mp3"))
        {
            if (!keep.Contains(file)) File.Delete(file);
        }
    }

    private static string CachePathFor(Uri uri)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        return Path.Combine(CacheDirectory, hash + ".mp3");
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

    internal sealed record CatalogFile(DateTimeOffset Updated, string[] Urls);
}
