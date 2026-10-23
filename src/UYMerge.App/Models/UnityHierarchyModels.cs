
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
  private bool _hasChangedComponent;

  public event PropertyChangedEventHandler? PropertyChanged;

  public long FileId { get; init; }

  public string Name { get; init; } = string.Empty;

  // 既存のアイコンは変更しない
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
        OnPropertyChanged(nameof(UnderlineBrush));
        OnPropertyChanged(nameof(HasOwnChange));
        OnPropertyChanged(nameof(DescendantIndicatorBrush));
      }
    }
  }

  /// <summary>
  /// 子孫の状態を集計した結果。
  /// 親自身の Change とは独立して保持する。
  /// </summary>
  public ChangeKind DescendantChange
  {
    get => _descendantChange;
    set
    {
      if (SetField(ref _descendantChange, value))
      {
        OnPropertyChanged(nameof(ChangeBackground));
        OnPropertyChanged(nameof(DescendantIndicatorBrush));
        OnPropertyChanged(nameof(HasDescendantChange));
      }
    }
  }

  /// <summary>
  /// 子孫に競合が1つでも存在するか。
  /// DescendantChange の集計結果にかかわらず保持する。
  /// </summary>
  public bool HasDescendantConflict
  {
    get => _hasDescendantConflict;
    set
    {
      if (SetField(ref _hasDescendantConflict, value))
      {
        OnPropertyChanged(nameof(DescendantConflictBrush));
        OnPropertyChanged(nameof(DescendantIndicatorBrush));
        OnPropertyChanged(nameof(ChangeBackground));
        OnPropertyChanged(nameof(HasDescendantChange));
      }
    }
  }

  public bool HasOwnChange => Change != ChangeKind.None;
  public bool HasDescendantChange =>
    HasDescendantConflict ||
    DescendantChange != ChangeKind.None;
  public bool HasChangedComponent
  {
    get => _hasChangedComponent;
    set
    {
      if (SetField(ref _hasChangedComponent, value))
      {
        OnPropertyChanged(nameof(ChangeMarker));
        OnPropertyChanged(nameof(ChangeBrush));
        OnPropertyChanged(nameof(ChangeBackground));
      }
    }
  }

  public string ChangeMarker => Change switch
  {
    ChangeKind.Added => "+",
    ChangeKind.Deleted => "-",
    ChangeKind.Modified => "*",
    ChangeKind.Conflict => "!",
    _ when HasChangedComponent => "*",
    _ => string.Empty
  };

  public string ChangeBrush => Change switch
  {
    ChangeKind.Added => "#6DD6A0",
    ChangeKind.Deleted => "#FF7777",
    ChangeKind.Modified => "#FFBD69",
    ChangeKind.Conflict => "#C4A7FF",
    _ when HasChangedComponent => "#FFBD69",
    _ => "#9CA3AF"
  };

  /// <summary>
  /// 自身に変更がある場合は自身の背景色を優先する。
  /// 自身が未変更なら子孫の集計状態を表示する。
  /// </summary>
  public string ChangeBackground => Change switch
  {
    ChangeKind.Added => "#263B32",
    ChangeKind.Deleted => "#40292D",
    ChangeKind.Modified => "#403526",
    ChangeKind.Conflict => "#382D49",
    _ when HasChangedComponent => "#403526",
    _ => DescendantChange switch
    {
      ChangeKind.Added => "#22332C",
      ChangeKind.Deleted => "#38272B",
      ChangeKind.Modified => "#382F23",
      ChangeKind.Conflict => "#32283F",
      _ => "#202126"
    }
  };

  /// <summary>
  /// 自身に変更があるオブジェクトだけ下線を表示する。
  /// </summary>
  public string UnderlineBrush =>
      Change == ChangeKind.None ? "Transparent" : ChangeBrush;

  /// <summary>
  /// 子孫の状態を表す三角形の色。
  /// 自身の変更状態ではなく、子孫の集計結果に基づく。
  /// </summary>
  public string DescendantIndicatorBrush
  {
    get
    {
      if (HasDescendantConflict ||
          DescendantChange == ChangeKind.Conflict)
      {
        return "#C4A7FF";
      }

      return DescendantChange switch
      {
        ChangeKind.Modified => "#FFBD69",
        ChangeKind.Added => "#6DD6A0",
        ChangeKind.Deleted => "#FF7777",
        _ => "Transparent"
      };
    }
  }

  // 既存のバインディングとの互換性を維持
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
