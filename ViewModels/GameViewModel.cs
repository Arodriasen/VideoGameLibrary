using VideoGameLibrary.Models;
using CommunityToolkit.Mvvm.ComponentModel;

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
        [ObservableProperty] private int? _year;
        [ObservableProperty] private string _coverUrl = string.Empty;
        [ObservableProperty] private byte[]? _coverData;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private int _rating;

        public static GameViewModel FromModel(Game g) => new()
        {
            Id = g.Id,
            Barcode = g.Barcode ?? string.Empty,
            Title = g.Title,
            Platform = g.Platform,
            Publisher = g.Publisher,
            Genre = g.Genre,
            Year = g.Year,
            CoverUrl = g.CoverUrl,
            CoverData = g.CoverData,
            Notes = g.Notes,
            Rating = g.Rating
        };

        public Game ToModel() => new()
        {
            Id = Id,
            Barcode = string.IsNullOrEmpty(Barcode) ? null : Barcode,
            Title = Title,
            Platform = Platform,
            Publisher = Publisher,
            Genre = Genre,
            Year = Year,
            CoverUrl = CoverUrl,
            CoverData = CoverData,
            Notes = Notes,
            Rating = Rating
        };
    }
}
