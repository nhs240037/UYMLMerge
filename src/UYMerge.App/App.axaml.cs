
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UYMerge.App.Services;
using UYMerge.App.ViewModels;
using UYMerge.App.Views;

namespace UYMerge.App;

public partial class App : Application
{
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      var mainView = new MainView();

      mainView.DataContext = new MainViewModel(
          new MergeService(),
          new AvaloniaFilePickerService(() => mainView));

      desktop.MainWindow = mainView;
    }

    base.OnFrameworkInitializationCompleted();

  }
}
