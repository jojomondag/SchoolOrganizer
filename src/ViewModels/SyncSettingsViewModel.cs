using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Src.Services;
using Serilog;

namespace SchoolOrganizer.Src.ViewModels;

public partial class SyncSettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly BackgroundSyncService? _backgroundSyncService;

    [ObservableProperty]
    private bool _autoSyncEnabled;

    [ObservableProperty]
    private int _selectedIntervalIndex;

    [ObservableProperty]
    private string _lastSyncTime = "Never";

    [ObservableProperty]
    private string _syncStatusMessage = "Disabled";

    [ObservableProperty]
    private bool _isSyncing = false;

    // Sync interval options (in minutes)
    public List<SyncIntervalOption> IntervalOptions { get; } = new()
    {
        new SyncIntervalOption { DisplayText = "Every 5 minutes", Minutes = 5 },
        new SyncIntervalOption { DisplayText = "Every 15 minutes", Minutes = 15 },
        new SyncIntervalOption { DisplayText = "Every 30 minutes", Minutes = 30 },
        new SyncIntervalOption { DisplayText = "Every 1 hour", Minutes = 60 },
        new SyncIntervalOption { DisplayText = "Every 2 hours", Minutes = 120 },
        new SyncIntervalOption { DisplayText = "Every 4 hours", Minutes = 240 },
    };

    public SyncSettingsViewModel(BackgroundSyncService? backgroundSyncService = null)
    {
        _settingsService = SettingsService.Instance;
        _backgroundSyncService = backgroundSyncService;

        // Load current settings
        LoadSettings();

        // Subscribe to sync service events if available
        if (_backgroundSyncService != null)
        {
            _backgroundSyncService.SyncStatusChanged += OnSyncStatusChanged;
            UpdateSyncInfo();
        }
    }

    private void LoadSettings()
    {
        // Load auto-sync enabled
        AutoSyncEnabled = _settingsService.GetAutoSyncEnabled() ?? false;

        // Load sync interval and find matching index
        var savedInterval = _settingsService.GetSyncIntervalMinutes() ?? 30;
        var matchingOption = IntervalOptions.FirstOrDefault(o => o.Minutes == savedInterval);

        if (matchingOption != null)
        {
            SelectedIntervalIndex = IntervalOptions.IndexOf(matchingOption);
        }
        else
        {
            // Default to 30 minutes if no match found
            SelectedIntervalIndex = IntervalOptions.FindIndex(o => o.Minutes == 30);
        }

        Log.Information($"Loaded sync settings: AutoSync={AutoSyncEnabled}, Interval={savedInterval} minutes");
    }

    partial void OnAutoSyncEnabledChanged(bool value)
    {
        _backgroundSyncService?.SetEnabled(value);
        _settingsService.SaveAutoSyncEnabled(value);
        SyncStatusMessage = value ? "Enabled" : "Disabled";
        Log.Information($"Auto-sync {(value ? "enabled" : "disabled")}");
    }

    partial void OnSelectedIntervalIndexChanged(int value)
    {
        if (value >= 0 && value < IntervalOptions.Count)
        {
            var selectedMinutes = IntervalOptions[value].Minutes;
            _backgroundSyncService?.SetSyncInterval(selectedMinutes);
            _settingsService.SaveSyncIntervalMinutes(selectedMinutes);
            Log.Information($"Sync interval changed to {selectedMinutes} minutes");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SyncNowAsync()
    {
        if (_backgroundSyncService == null)
        {
            Log.Warning("Background sync service not available");
            SyncStatusMessage = "Service not available";
            return;
        }

        if (IsSyncing)
        {
            Log.Information("Sync already in progress");
            return;
        }

        try
        {
            IsSyncing = true;
            SyncStatusMessage = "Syncing...";
            await _backgroundSyncService.SyncNowAsync();
            UpdateSyncInfo();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during manual sync");
            SyncStatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private void OnSyncStatusChanged(object? sender, SyncStatusEventArgs e)
    {
        SyncStatusMessage = e.Message;
        IsSyncing = e.Status == Services.SyncStatus.InProgress;

        if (e.Status == Services.SyncStatus.Completed || e.Status == Services.SyncStatus.Error)
        {
            UpdateSyncInfo();
        }
    }

    private void UpdateSyncInfo()
    {
        if (_backgroundSyncService == null) return;

        var lastSync = _backgroundSyncService.LastSyncTime;
        if (lastSync == DateTime.MinValue)
        {
            LastSyncTime = "Never";
        }
        else
        {
            var timeSpan = DateTime.Now - lastSync;
            if (timeSpan.TotalMinutes < 1)
            {
                LastSyncTime = "Just now";
            }
            else if (timeSpan.TotalHours < 1)
            {
                LastSyncTime = $"{(int)timeSpan.TotalMinutes} minute(s) ago";
            }
            else if (timeSpan.TotalDays < 1)
            {
                LastSyncTime = $"{(int)timeSpan.TotalHours} hour(s) ago";
            }
            else
            {
                LastSyncTime = lastSync.ToString("MMM dd, HH:mm");
            }
        }
    }

    public void Cleanup()
    {
        if (_backgroundSyncService != null)
        {
            _backgroundSyncService.SyncStatusChanged -= OnSyncStatusChanged;
        }
    }
}

public class SyncIntervalOption
{
    public string DisplayText { get; set; } = string.Empty;
    public int Minutes { get; set; }
}
