
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UYMerge.App.Models;

public enum ChangeKind
{
  None,
  Added,
  Deleted,
  Modified,
  Conflict
}

public sealed class UnityHierarchyNode : INotifyPropertyChanged
{
  private ChangeKind _change;
  private ChangeKind _descendantChange;
  private bool _hasDescendantConflict;

  public event PropertyChangedEventHandler? PropertyChanged;

  public long FileId { get; init; }

  public string Name { get; init; } = string.Empty;

  public string Icon { get; init; } = "game-object";

  public ChangeKind Change
  {
    get => _change;
    set
    {
      if (SetField(ref _change, value))
      {
        OnPropertyChanged(nameof(ChangeMarker));
        OnPropertyChanged(nameof(ChangeBrush));
        OnPropertyChanged(nameof(ChangeBackground));
      }
    }
  }

  public ChangeKind DescendantChange
  {
    get => _descendantChange;
    set
    {
      if (SetField(ref _descendantChange, value))
      {
        OnPropertyChanged(nameof(ChangeBackground));
      }
    }
  }

  public bool HasDescendantConflict
  {
    get => _hasDescendantConflict;
    set
    {
      if (SetField(ref _hasDescendantConflict, value))
      {
        OnPropertyChanged(nameof(DescendantConflictBrush));
      }
    }
  }

  public string ChangeMarker => Change switch
  {
    ChangeKind.Added => "+",
    ChangeKind.Deleted => "-",
    ChangeKind.Modified => "*",
    ChangeKind.Conflict => "!",
    _ => string.Empty
  };

  public string ChangeBrush => Change switch
  {
    ChangeKind.Added => "#6DD6A0",
    ChangeKind.Deleted => "#FF7777",
    ChangeKind.Modified => "#FFBD69",
    ChangeKind.Conflict => "#C4A7FF",
    _ => "#9CA3AF"
  };

  public string ChangeBackground => Change switch
  {
    ChangeKind.Added => "#263B32",
    ChangeKind.Deleted => "#40292D",
    ChangeKind.Modified => "#403526",
    ChangeKind.Conflict => "#382D49",
    _ => DescendantChange switch
    {
      ChangeKind.Added => "#22332C",
      ChangeKind.Deleted => "#38272B",
      ChangeKind.Modified => "#382F23",
      ChangeKind.Conflict => "#32283F",
      _ => "#202126"
    }
  };

  public string DescendantConflictBrush =>
      HasDescendantConflict ? "#C4A7FF" : "Transparent";

  public ObservableCollection<UnityHierarchyNode> Children { get; } = new();

  public ObservableCollection<UnityInspectorComponent> Components { get; } = new();

  private bool SetField<T>(
      ref T field,
      T value,
      [CallerMemberName] string? propertyName = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value))
      return false;

    field = value;
    OnPropertyChanged(propertyName);
    return true;
  }

  private void OnPropertyChanged(string? propertyName) =>
      PropertyChanged?.Invoke(
          this,
          new PropertyChangedEventArgs(propertyName));
}

public sealed class UnityInspectorComponent
{
  public long FileId { get; init; }

  public int ClassId { get; init; }

  public string TypeName { get; init; } = string.Empty;

  public string Icon { get; init; } = "component";

  public ChangeKind Change { get; set; }

  public string RawText { get; init; } = string.Empty;
}
