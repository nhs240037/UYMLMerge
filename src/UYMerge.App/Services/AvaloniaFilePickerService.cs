using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace UYMerge.App.Services;

public sealed class AvaloniaFilePickerService(
		Func<Window?> getWindow) : IFilePickerService
{
	public async Task<string?> PickInputFileAsync()
	{
		var window = getWindow();
		if (window is null)
		{
			return null;
		}

		var files = await window.StorageProvider.OpenFilePickerAsync(
				new FilePickerOpenOptions
				{
					Title = "Unity YAMLファイルを選択",
					AllowMultiple = false,
					FileTypeFilter =
						[
								new FilePickerFileType("Unity YAML")
										{
												Patterns = ["*.unity", "*.prefab", "*.asset", "*.yaml", "*.yml"]
										},
										FilePickerFileTypes.All
						]
				});

		return files.Count > 0
				? files[0].TryGetLocalPath()
				: null;
	}

	public async Task<string?> PickOutputFileAsync()
	{
		var window = getWindow();
		if (window is null)
		{
			return null;
		}

		var file = await window.StorageProvider.SaveFilePickerAsync(
				new FilePickerSaveOptions
				{
					Title = "マージ結果を保存",
					SuggestedFileName = "merged.unity",
					FileTypeChoices =
						[
								new FilePickerFileType("Unity YAML")
										{
												Patterns = ["*.unity", "*.prefab", "*.asset", "*.yaml", "*.yml"]
										}
						]
				});

		return file?.TryGetLocalPath();
	}
}