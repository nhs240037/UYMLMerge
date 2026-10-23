namespace UYMerge.App.Services;

public interface IFilePickerService
{
	Task<string?> PickInputFileAsync();

	Task<string?> PickOutputFileAsync();
}