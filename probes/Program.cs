using System.Net;
using Loupedeck.SpanishMemesPlugin;

if (args.Length > 0 && args[0] == "--play")
{
    var player = new MemePlayer(Console.WriteLine, CancellationToken.None);
    await player.PlayAsync();
    return;
}
if (args.Length > 0 && args[0] == "--stop-audio")
{
    var file = Path.Combine(Path.GetTempPath(), $"spanish-memes-probe-{Guid.NewGuid():N}.wav");
    try
    {
        const int bytes = 44100 * 2 * 10;
        using (var writer = new BinaryWriter(File.Create(file)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
            writer.Write((short)1); writer.Write(44100); writer.Write(88200); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes); writer.Write(new byte[bytes]);
        }
        using var cancel = new CancellationTokenSource();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var task = MemePlayer.PlayWithAfplayAsync(file, cancel.Token);
        await Task.Delay(300);
        cancel.Cancel();
        try { await task.WaitAsync(TimeSpan.FromSeconds(3)); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException) { }
        if (timer.Elapsed > TimeSpan.FromSeconds(3)) throw new Exception("Audio process did not stop promptly");
        Console.WriteLine("afplay stopped on cancellation");
    }
    finally { File.Delete(file); }
    return;
}
if (args.Length > 0 && args[0] == "--behavior")
{
    using var http = new HttpClient(new FakeHandler());
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var startedAgain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var calls = 0;
    var player = new MemePlayer(Console.WriteLine, CancellationToken.None, http, async (_, token) =>
    {
        if (Interlocked.Increment(ref calls) == 1) started.TrySetResult();
        else startedAgain.TrySetResult();
        await Task.Delay(Timeout.Infinite, token);
    });
    var first = player.PlayAsync();
    await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
    await player.PlayAsync().WaitAsync(TimeSpan.FromSeconds(3));
    if (calls != 1) throw new Exception("A second press queued or started another sound");
    player.Stop();
    await first.WaitAsync(TimeSpan.FromSeconds(3));
    var next = player.PlayAsync();
    await startedAgain.Task.WaitAsync(TimeSpan.FromSeconds(3));
    if (calls != 2) throw new Exception("A new sound did not start after stopping");
    player.Stop();
    await next.WaitAsync(TimeSpan.FromSeconds(3));
    Console.WriteLine("Repeated press ignored; long press cancelled; next press started");
    return;
}
var html = await File.ReadAllTextAsync(args[0]);
var sounds = TrendingCatalog.Parse(html);
if (sounds.Length == 0 || sounds.Any(u => u.Host != "www.myinstants.com" || !u.AbsolutePath.StartsWith("/media/sounds/")))
    throw new Exception("No valid sound paths found");
Console.WriteLine($"Parsed {sounds.Length} unique sound URLs");

internal sealed class FakeHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = request.RequestUri!.AbsolutePath.StartsWith("/en/index/")
            ? new StringContent("<button onclick=\"play('/media/sounds/test.mp3')\"></button>")
            : new ByteArrayContent([0, 1, 2]);
        return Task.FromResult(response);
    }
}
