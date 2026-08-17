using VideoGameLibrary.Models;
using VideoGameLibrary.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace VideoGameLibrary.ViewModels
{
    public partial class GameEditViewModel : ObservableObject
    {
        private readonly GameApiService _api;

        [ObservableProperty] private string _dialogTitle = "Añadir juego";
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
        [ObservableProperty] private bool _isSearching;
        [ObservableProperty] private string _errorMessage = string.Empty;

        public int GameId { get; set; }
        public bool Saved { get; private set; }
        public System.Action? CloseDialog { get; set; }

        public GameEditViewModel(GameApiService api)
        {
            _api = api;
        }

        public void LoadFromGame(Game game)
        {
            DialogTitle = "Editar juego";
            GameId = game.Id;
            Barcode = game.Barcode ?? string.Empty;
            Title = game.Title;
            Platform = game.Platform;
            Publisher = game.Publisher;
            Genre = game.Genre;
            Year = game.Year;
            CoverUrl = game.CoverUrl;
            CoverData = game.CoverData;
            Notes = game.Notes;
            Rating = game.Rating;
        }

        [RelayCommand]
        private void SetRating(string value)
        {
            var parsed = int.Parse(value);
            Rating = Rating == parsed ? 0 : parsed;
        }

        [RelayCommand]
        public async Task SearchAsync()
        {
            ErrorMessage = string.Empty;
            var barcode = GameApiService.NormalizeBarcode(Barcode);
            if (string.IsNullOrEmpty(barcode))
            {
                ErrorMessage = "Introduce un código de barras.";
                return;
            }

            IsSearching = true;
            var game = await _api.SearchByBarcodeAsync(barcode);
            IsSearching = false;

            if (game == null)
            {
                ErrorMessage = "Código de barras no encontrado. Escribe el título y pulsa \"Buscar por nombre\".";
                return;
            }

            Title = game.Title;
            Platform = game.Platform;
            Publisher = game.Publisher;
            Genre = game.Genre;
            Year = game.Year;
            CoverUrl = game.CoverUrl;

            if (!string.IsNullOrEmpty(game.CoverUrl))
            {
                IsSearching = true;
                CoverData = await _api.DownloadCoverAsync(game.CoverUrl);
                IsSearching = false;
            }
        }

        [RelayCommand]
        public async Task SearchByNameAsync()
        {
            ErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(Title))
            {
                ErrorMessage = "Escribe un título para buscar.";
                return;
            }

            IsSearching = true;
            var game = await _api.SearchByNameAsync(Title);
            IsSearching = false;

            if (game == null)
            {
                ErrorMessage = "No se encontró información para ese título.";
                return;
            }

            if (!string.IsNullOrEmpty(game.Platform)) Platform = game.Platform;
            if (!string.IsNullOrEmpty(game.Genre)) Genre = game.Genre;
            if (game.Year.HasValue) Year = game.Year;
            if (!string.IsNullOrEmpty(game.CoverUrl)) CoverUrl = game.CoverUrl;
            if (game.CoverData != null) CoverData = game.CoverData;
        }

        [RelayCommand]
        private void Save()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Title))
            {
                ErrorMessage = "El título es obligatorio.";
                return;
            }

            Saved = true;
            CloseDialog?.Invoke();
        }

        public Game ToGame()
        {
            var cleanBarcode = GameApiService.NormalizeBarcode(Barcode);
            return new()
            {
                Id = GameId,
                Barcode = string.IsNullOrEmpty(cleanBarcode) ? null : cleanBarcode,
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
}
