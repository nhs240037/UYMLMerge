using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using UYMerge.App.Services;

namespace UYMerge.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
  private readonly MergeService _mergeService;
  private readonly IFilePickerService _filePicker;

  private MergeServiceResult? _mergeResult;

  [ObservableProperty]
  private string basePath = string.Empty;

  [ObservableProperty]
  private string oursPath = string.Empty;

  [ObservableProperty]
  private string theirsPath = string.Empty;

  [ObservableProperty]
  private string outputPath = string.Empty;

  [ObservableProperty]
  private string statusMessage = "3つのUnity YAMLファイルを選択してください。";

  [ObservableProperty]
  private bool isBusy;

  [ObservableProperty]
  private int conflictCount;

  public bool CanSave =>
      _mergeResult is { CanSave: true } &&
      !string.IsNullOrWhiteSpace(OutputPath) &&
      !IsBusy;

  public MainViewModel(
      MergeService mergeService,
      IFilePickerService filePicker)
  {
    _mergeService = mergeService;
    _filePicker = filePicker;
  }

  partial void OnOutputPathChanged(string value) =>
      OnPropertyChanged(nameof(CanSave));

  partial void OnIsBusyChanged(bool value) =>
      OnPropertyChanged(nameof(CanSave));

  [RelayCommand]
  private async Task SelectBaseAsync()
  {
    var path = await _filePicker.PickInputFileAsync();
    if (path is not null)
      BasePath = path;
  }

  [RelayCommand]
  private async Task SelectOursAsync()
  {
    var path = await _filePicker.PickInputFileAsync();
    if (path is not null)
      OursPath = path;
  }

  [RelayCommand]
  private async Task SelectTheirsAsync()
  {
    var path = await _filePicker.PickInputFileAsync();
    if (path is not null)
      TheirsPath = path;
  }

  [RelayCommand]
  private async Task SelectOutputAsync()
  {
    var path = await _filePicker.PickOutputFileAsync();
    if (path is not null)
      OutputPath = path;
  }

  [RelayCommand]
  private async Task MergeAsync()
  {
    if (string.IsNullOrWhiteSpace(BasePath) ||
        string.IsNullOrWhiteSpace(OursPath) ||
        string.IsNullOrWhiteSpace(TheirsPath))
    {
      StatusMessage = "Base・Ours・Theirsをすべて指定してください。";
      return;
    }

    IsBusy = true;

    try
    {
      _mergeResult = await _mergeService.MergeAsync(
          BasePath,
          OursPath,
          TheirsPath);

      ConflictCount = _mergeResult.MergeResult.Conflicts.Count;

      StatusMessage = _mergeResult.HasConflicts
          ? $"マージ完了：競合が {ConflictCount} 件あります。解決するまで保存できません。"
          : "マージ成功：競合はありません。保存先を指定してください。";

      OnPropertyChanged(nameof(CanSave));
    }
    catch (Exception ex)
    {
      _mergeResult = null;
      ConflictCount = 0;
      StatusMessage = $"マージに失敗しました：{ex.Message}";
      OnPropertyChanged(nameof(CanSave));
    }
    finally
    {
      IsBusy = false;
    }
  }

  [RelayCommand(CanExecute = nameof(CanSave))]
  private async Task SaveAsync()
  {
    if (_mergeResult is not { CanSave: true } result ||
        string.IsNullOrWhiteSpace(OutputPath))
    {
      return;
    }

    IsBusy = true;

    try
    {
      await _mergeService.SaveAsync(OutputPath, result);
      StatusMessage = $"保存しました：{OutputPath}";
    }
    catch (Exception ex)
    {
      StatusMessage = $"保存に失敗しました：{ex.Message}";
    }
    finally
    {
      IsBusy = false;
      SaveCommand.NotifyCanExecuteChanged();
    }
  }
}