using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using Clarion.Interop;
using Clarion.Windows;

namespace Clarion;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Clarion";

    private const string MainCommand = "/clarion";
    private const string AliasCommand = "/pfpresets";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;

    internal Configuration Config { get; }
    internal Interop.RecruitmentOps RecruitmentOps { get; }
    internal Services.AutoRefreshService AutoRefresh { get; }
    internal Services.PresetLauncher PresetLauncher { get; }
    internal WindowSystem WindowSystem { get; } = new("Clarion");
    internal MainWindow MainWindow { get; }
    internal ConfigWindow ConfigWindow { get; }
    internal CompanionWindow CompanionWindow { get; }
#if PFP_DEVTOOLS
    internal DiagnosticWindow DiagnosticWindow { get; }
    internal RecruitmentCaptureHook CaptureHook { get; }
    internal Services.AddonWatcher AddonWatcher { get; } = new();
    internal Interop.CallbackCapture CallbackCapture { get; } = new();
#endif

    internal static Plugin Instance { get; private set; } = null!;

    public Plugin()
    {
        Instance = this;
        ECommonsMain.Init(PluginInterface, this);
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(PluginInterface);

        RecruitmentOps = new Interop.RecruitmentOps();
        AutoRefresh = new Services.AutoRefreshService(this);
        PresetLauncher = new Services.PresetLauncher(this);

        MainWindow = new MainWindow(this);
        ConfigWindow = new ConfigWindow(this);
        CompanionWindow = new CompanionWindow(this);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(CompanionWindow);
#if PFP_DEVTOOLS
        CaptureHook = new RecruitmentCaptureHook();
        DiagnosticWindow = new DiagnosticWindow(this);
        WindowSystem.AddWindow(DiagnosticWindow);
#endif

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        PluginInterface.UiBuilder.OpenMainUi += OpenMain;

        // Companion behaviour: follow the in-game recruitment criteria form open/closed.
        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LookingForGroupCondition", OnRecruitFormSetup);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "LookingForGroupCondition", OnRecruitFormFinalize);

        CommandManager.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Clarion. '/clarion cfg' opens settings.",
            ShowInHelp = true,
        });
        CommandManager.AddHandler(AliasCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias for /clarion.",
            ShowInHelp = false,
        });
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMain;
        CommandManager.RemoveHandler(MainCommand);
        CommandManager.RemoveHandler(AliasCommand);
        AddonLifecycle.UnregisterListener(OnRecruitFormSetup, OnRecruitFormFinalize);
        WindowSystem.RemoveAllWindows();

        AutoRefresh.Dispose();

#if PFP_DEVTOOLS
        CaptureHook.Dispose();
        AddonWatcher.Dispose();
        CallbackCapture.Dispose();
#endif
        ECommonsMain.Dispose();
    }

    internal void OpenSettings() => ConfigWindow.IsOpen = true;
    internal void OpenMain() => MainWindow.IsOpen = true;

    // The quick pop-up appears with the recruitment form, but only if the full window isn't already
    // open. It never opens or closes the main window's state - the "Open Clarion" button does that.
    private void OnRecruitFormSetup(AddonEvent type, AddonArgs args)
    {
        if (Config.AutoShowWithRecruitmentForm && !MainWindow.IsOpen)
            CompanionWindow.IsOpen = true;
    }

    private void OnRecruitFormFinalize(AddonEvent type, AddonArgs args)
    {
        CompanionWindow.IsOpen = false;
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = (args ?? string.Empty).Trim().ToLowerInvariant();
        if (trimmed is "cfg" or "config" or "settings")
            OpenSettings();
#if PFP_DEVTOOLS
        else if (trimmed == "diag")
            DiagnosticWindow.IsOpen = true;
        else if (trimmed == "dump")
            Services.RecruitmentReader.DebugDump();
        else if (trimmed == "addons")
            Services.RecruitmentReader.DumpVisibleAddons();
        else if (trimmed == "watchaddons")
            AddonWatcher.Toggle();
        else if (trimmed == "cbcapture")
        {
            CallbackCapture.IsCapturing = !CallbackCapture.IsCapturing;
            ChatGui.Print($"[Clarion] Callback capture {(CallbackCapture.IsCapturing ? "ON - click Recruit Members, then run this again" : "OFF")}.");
        }
        else if (trimmed == "capture")
        {
            CaptureHook.ClearCaptures();
            CaptureHook.IsCapturing = !CaptureHook.IsCapturing;
            ChatGui.Print($"[Clarion] Capture {(CaptureHook.IsCapturing ? "ON - pick a duty now, then run /pfpresets capture again. Do NOT hit Recruit while capturing." : "OFF")}.");
        }
        else if (trimmed == "refreshnow")
        {
            var ok = AutoRefresh.StartRefresh();
            ChatGui.Print($"[Clarion] refreshnow -> {(ok ? "running" : "already running")}");
        }
#endif
        else
            OpenMain();
    }
}
