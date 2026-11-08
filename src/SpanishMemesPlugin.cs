namespace Loupedeck.SpanishMemesPlugin;

public sealed class SpanishMemesPlugin : Plugin
{
    private readonly CancellationTokenSource stop = new();
    private MemePlayer? player;

    public override bool UsesApplicationApiOnly => true;
    public override bool HasNoApplication => true;

    public override void Load()
    {
        this.player = new MemePlayer(message => this.Log.Info(message), this.stop.Token);
        this.AddDynamicAction(new RandomMemeCommand(this.player));
        _ = this.player.WarmAsync();
    }

    public override void Unload() => this.stop.Cancel();
}

public sealed class SpanishMemesApplication : ClientApplication
{
    protected override string GetProcessName() => "";
    protected override string GetBundleName() => "";
    public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
}

public sealed class RandomMemeCommand : PluginDynamicCommand
{
    private readonly MemePlayer player;

    internal RandomMemeCommand(MemePlayer player)
        : base("Meme aleatorio", "Reproduce un sonido de tendencia en España", "Spanish Memes", DeviceType.All)
    {
        this.player = player;
        this.Name = "spanish_memes_random";
    }

    protected override void RunCommand(string actionParameter) => _ = this.player.PlayAsync();

    protected override bool ProcessButtonEvent2(string actionParameter, DeviceButtonEvent2 buttonEvent)
    {
        switch (buttonEvent.EventType)
        {
            case DeviceButtonEventType.Press:
                this.RunCommand(actionParameter);
                return true;
            case DeviceButtonEventType.LongPress:
                this.player.Stop();
                return true;
            case DeviceButtonEventType.RepeatPress:
            case DeviceButtonEventType.Release:
                return true;
            default:
                return base.ProcessButtonEvent2(actionParameter, buttonEvent);
        }
    }
}
