using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace DtshPocWinUI.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    public ObservableCollection<DtshStatusRow> StatusRows { get; } = [];

    public ObservableCollection<DtshLogEntry> LogEntries { get; } = [];

    [ObservableProperty]
    public partial string WindowHandleText { get; set; } = "HWND: 0x0";

    [ObservableProperty]
    public partial string InitializeHandleText { get; set; } = "Initialize HWND: 0x0";

    [ObservableProperty]
    public partial string CurrentProfileText { get; set; } = "Current profile: unknown";

    [ObservableProperty]
    public partial bool UseWindowHandleForInitialize { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsInfoBarOpen { get; set; } = true;

    [ObservableProperty]
    public partial string LastTitle { get; set; } = "Ready";

    [ObservableProperty]
    public partial string LastMessage { get; set; } = "Refresh status to query IDetectionAndSharing.";

    [ObservableProperty]
    public partial InfoBarSeverity LastSeverity { get; set; } = InfoBarSeverity.Informational;

    public void UpdateWindowHandle()
    {
        nint hwnd = App.WindowHandle;
        WindowHandleText = $"App HWND: {DtshNative.FormatHwnd(hwnd)}";
        UpdateInitializeHandleText();
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private void RefreshStatus()
    {
        RunOperation("Refresh status", () => RefreshStatusCore(updateInfoBar: true));
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private void InitializeFactory()
    {
        RunOperation("Initialize factory", () =>
        {
            nint initializeHwnd = GetInitializeHwnd();
            int hr = DtshNative.InitializeFactory(initializeHwnd);
            ReportHr("IMultiObjectElevationFactory.Initialize", hr, $"initialize hwnd {DtshNative.FormatHwnd(initializeHwnd)}");
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private void CreateElevatedObject()
    {
        RunOperation("Create elevated object", () =>
        {
            nint initializeHwnd = GetInitializeHwnd();
            int hr = DtshNative.CreateElevatedDetectionAndSharing(initializeHwnd);
            ReportHr("CreateElevatedObject(IDetectionAndSharing)", hr, $"initialize hwnd {DtshNative.FormatHwnd(initializeHwnd)}");
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private void TurnOn()
    {
        RunOperation("Turn on", () =>
        {
            nint initializeHwnd = GetInitializeHwnd();
            nint turnOnHwnd = App.WindowHandle;
            int hr = DtshNative.TurnOnDtSharing(initializeHwnd, turnOnHwnd);
            ReportHr(
                "IDetectionAndSharing.TurnOn",
                hr,
                $"initialize hwnd {DtshNative.FormatHwnd(initializeHwnd)}, turn-on hwnd {DtshNative.FormatHwnd(turnOnHwnd)}");

            RefreshStatusCore(updateInfoBar: false);
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private void OpenSettings()
    {
        RunOperation("Open settings", () =>
        {
            int hr = DtshNative.OpenNetCenter();
            ReportHr("IOpenControlPanel.Open", hr, "Microsoft.NetworkAndSharingCenter / Advanced");
        });
    }

    private bool CanRunOperation()
    {
        return !IsBusy;
    }

    private nint GetInitializeHwnd()
    {
        UpdateWindowHandle();
        return UseWindowHandleForInitialize ? App.WindowHandle : 0;
    }

    private void AddStatusRow(IDetectionAndSharing dtsh, DtshType type)
    {
        int hr = dtsh.GetStatus(type, out DtshState state, out DtshAction action);
        string stateText = hr >= 0 ? DtshNative.FormatState(state) : "-";
        string actionText = hr >= 0 ? DtshNative.FormatAction(action) : "-";

        StatusRows.Add(new DtshStatusRow(
            type.ToString(),
            DtshNative.FormatHr(hr),
            stateText,
            actionText));
    }

    private void RefreshStatusCore(bool updateInfoBar)
    {
        UpdateWindowHandle();
        StatusRows.Clear();

        IDetectionAndSharing dtsh = DtshNative.CreateDetectionAndSharing();
        int profileHr = dtsh.GetCurrentFwProfile(out NetFwProfileType2 currentProfile);
        CurrentProfileText = profileHr >= 0
            ? $"Current profile: {DtshNative.FormatProfile(currentProfile)} ({(int)currentProfile})"
            : $"Current profile: {DtshNative.FormatHr(profileHr)}";

        AddStatusRow(dtsh, DtshType.NetworkDiscovery);
        AddStatusRow(dtsh, DtshType.FileSharing);
        AddStatusRow(dtsh, DtshType.All);

        string result = profileHr >= 0
            ? $"Profile {DtshNative.FormatProfile(currentProfile)}; {StatusRows.Count} status rows."
            : $"Profile query failed: {DtshNative.FormatHr(profileHr)}";
        if (updateInfoBar)
        {
            SetInfoBar("Status refreshed", result, profileHr >= 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        }

        AddLog(result);
    }

    private void ReportHr(string operation, int hr, string detail)
    {
        string message = $"{detail}; HRESULT {DtshNative.FormatHr(hr)}";
        AddLog($"{operation}: {message}");
        SetInfoBar(operation, message, hr >= 0 ? InfoBarSeverity.Success : InfoBarSeverity.Error);
    }

    private void RunOperation(string operation, Action action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            AddLog($"{operation} started.");
            action();
        }
        catch (Exception ex)
        {
            AddLog($"{operation} failed: {ex}");
            SetInfoBar(operation, ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetInfoBar(string title, string message, InfoBarSeverity severity)
    {
        LastTitle = title;
        LastMessage = message;
        LastSeverity = severity;
        IsInfoBarOpen = true;
    }

    private void AddLog(string message)
    {
        LogEntries.Add(new DtshLogEntry(
            DateTimeOffset.Now.ToString("HH:mm:ss"),
            message));

        while (LogEntries.Count > 200)
        {
            LogEntries.RemoveAt(0);
        }
    }

    private void UpdateInitializeHandleText()
    {
        nint initializeHwnd = UseWindowHandleForInitialize ? App.WindowHandle : 0;
        InitializeHandleText = $"Initialize HWND: {DtshNative.FormatHwnd(initializeHwnd)}";
    }

    partial void OnUseWindowHandleForInitializeChanged(bool value)
    {
        UpdateInitializeHandleText();
        AddLog($"Initialize hwnd mode: {(value ? "App HWND" : "0")}.");
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshStatusCommand.NotifyCanExecuteChanged();
        InitializeFactoryCommand.NotifyCanExecuteChanged();
        CreateElevatedObjectCommand.NotifyCanExecuteChanged();
        TurnOnCommand.NotifyCanExecuteChanged();
        OpenSettingsCommand.NotifyCanExecuteChanged();
    }
}

public sealed class DtshStatusRow(string type, string hresult, string state, string action)
{
    public string Type { get; } = type;

    public string HResult { get; } = hresult;

    public string State { get; } = state;

    public string Action { get; } = action;
}

public sealed class DtshLogEntry(string timestamp, string message)
{
    public string Timestamp { get; } = timestamp;

    public string Message { get; } = message;
}
