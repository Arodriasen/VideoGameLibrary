using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.ViewModels
{
    public partial class GameViewModel : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _barcode = string.Empty;
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private string _platform = string.Empty;
        [ObservableProperty] private string _publisher = string.Empty;
        [ObservableProperty] private string _genre = string.Empty;
        [ObservableProperty] private string _tags = string.Empty;
        [ObservableProperty] private int? _year;
        [ObservableProperty] private string _coverUrl = string.Empty;
        [ObservableProperty] private byte[]? _coverData;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private int _rating;
        [ObservableProperty] private bool _played;
        [ObservableProperty] private bool _isWishlist;
        [ObservableProperty] private bool _isSelected;

        // Notifica al MainViewModel para recalcular el contador de seleccionados
        public Action? OnSelectionChanged { get; set; }
        partial void OnIsSelectedChanged(bool value) => OnSelectionChanged?.Invoke();

        // Para los chips de la tarjeta — se recalcula cuando cambia Tags (p.ej. tras editar el juego)
        public List<string> TagList => MainViewModel.SplitTags(Tags).ToList();
        partial void OnTagsChanged(string value) => OnPropertyChanged(nameof(TagList));

        public static GameViewModel FromModel(Game g) => new()
        {
            Id = g.Id,
            Barcode = g.Barcode ?? string.Empty,
            Title = g.Title,
            Platform = g.Platform,
            Publisher = g.Publisher,
            Genre = g.Genre,
            Tags = g.Tags,
            Year = g.Year,
            CoverUrl = g.CoverUrl,
            CoverData = g.CoverData,
            Notes = g.Notes,
            Rating = g.Rating,
            Played = g.Played,
            IsWishlist = g.IsWishlist
        };

        public Game ToModel() => new()
        {
            Id = Id,
            Barcode = string.IsNullOrEmpty(Barcode) ? null : Barcode,
            Title = Title,
            Platform = Platform,
            Publisher = Publisher,
            Genre = Genre,
            Tags = Tags,
            Year = Year,
            CoverUrl = CoverUrl,
            CoverData = CoverData,
            Notes = Notes,
            Rating = Rating,
            Played = Played,
            IsWishlist = IsWishlist
        };
    }
}
