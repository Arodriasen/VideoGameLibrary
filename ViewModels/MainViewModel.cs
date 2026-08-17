using VideoGameLibrary.Models;
using VideoGameLibrary.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace VideoGameLibrary.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly GameRepository _repo;
        private readonly GameApiService _api;
        private List<Game> _allGames = new();

        [ObservableProperty] private ObservableCollection<GameViewModel> _games = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _totalGames;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private string _quickScanBarcode = string.Empty;

        [ObservableProperty] private ObservableCollection<FilterOption> _platformFilters = new();
        [ObservableProperty] private ObservableCollection<FilterOption> _genreFilters = new();
        [ObservableProperty] private ObservableCollection<FilterOption> _yearFilters = new();
        [ObservableProperty] private ObservableCollection<FilterOption> _ratingFilters = new();

        private static readonly string[] RatingLabels = { "★★★★★", "★★★★", "★★★", "★★", "★", "Sin puntuar" };
        private static readonly Dictionary<string, int> RatingLabelToValue = new()
        {
            ["★★★★★"] = 5, ["★★★★"] = 4, ["★★★"] = 3, ["★★"] = 2, ["★"] = 1, ["Sin puntuar"] = 0
        };

        public ISnackbarMessageQueue SnackbarMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

        public MainViewModel(GameRepository repo, GameApiService api)
        {
            _repo = repo;
            _api = api;
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();

        [RelayCommand]
        public async Task LoadGamesAsync()
        {
            IsLoading = true;
            _allGames = await _repo.GetAllAsync();
            RebuildFilterOptions();
            ApplyFilters();
            IsLoading = false;
        }

        private void RebuildFilterOptions()
        {
            PlatformFilters = RebuildFacet(PlatformFilters, _allGames.Select(g => g.Platform));
            GenreFilters = RebuildFacet(GenreFilters, _allGames.Select(g => g.Genre));
            YearFilters = RebuildFacet(YearFilters, _allGames.Where(g => g.Year.HasValue).Select(g => g.Year!.Value.ToString()));
            RatingFilters = RebuildRatingFacet(RatingFilters);
        }

        private ObservableCollection<FilterOption> RebuildRatingFacet(ObservableCollection<FilterOption> previous)
        {
            var previousSelected = previous.Where(f => f.IsSelected).Select(f => f.Value).ToHashSet();

            var result = new ObservableCollection<FilterOption>();
            foreach (var label in RatingLabels)
                result.Add(new FilterOption { Value = label, IsSelected = previousSelected.Contains(label), OnChanged = ApplyFilters });

            return result;
        }

        private ObservableCollection<FilterOption> RebuildFacet(ObservableCollection<FilterOption> previous, IEnumerable<string> rawValues)
        {
            var previousSelected = previous.Where(f => f.IsSelected).Select(f => f.Value).ToHashSet();
            var distinct = rawValues.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v);

            var result = new ObservableCollection<FilterOption>();
            foreach (var value in distinct)
                result.Add(new FilterOption { Value = value, IsSelected = previousSelected.Contains(value), OnChanged = ApplyFilters });

            return result;
        }

        [RelayCommand]
        private void ClearFilters()
        {
            foreach (var f in PlatformFilters) f.IsSelected = false;
            foreach (var f in GenreFilters) f.IsSelected = false;
            foreach (var f in YearFilters) f.IsSelected = false;
            foreach (var f in RatingFilters) f.IsSelected = false;
        }

        private void ApplyFilters()
        {
            IEnumerable<Game> filtered = _allGames;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.ToLower();
                filtered = filtered.Where(g =>
                    g.Title.ToLower().Contains(s) ||
                    g.Platform.ToLower().Contains(s) ||
                    (g.Barcode != null && g.Barcode.Contains(s)) ||
                    g.Publisher.ToLower().Contains(s) ||
                    g.Genre.ToLower().Contains(s));
            }

            var selectedPlatforms = PlatformFilters.Where(f => f.IsSelected).Select(f => f.Value).ToHashSet();
            var selectedGenres = GenreFilters.Where(f => f.IsSelected).Select(f => f.Value).ToHashSet();
            var selectedYears = YearFilters.Where(f => f.IsSelected).Select(f => f.Value).ToHashSet();
            var selectedRatings = RatingFilters.Where(f => f.IsSelected).Select(f => RatingLabelToValue[f.Value]).ToHashSet();

            if (selectedPlatforms.Count > 0)
                filtered = filtered.Where(g => selectedPlatforms.Contains(g.Platform));
            if (selectedGenres.Count > 0)
                filtered = filtered.Where(g => selectedGenres.Contains(g.Genre));
            if (selectedYears.Count > 0)
                filtered = filtered.Where(g => g.Year.HasValue && selectedYears.Contains(g.Year.Value.ToString()));
            if (selectedRatings.Count > 0)
                filtered = filtered.Where(g => selectedRatings.Contains(g.Rating));

            Games = new ObservableCollection<GameViewModel>(filtered.OrderBy(g => g.Title).Select(GameViewModel.FromModel));
            TotalGames = Games.Count;
        }

        // Escaneo rápido: busca, guarda y avisa con un snackbar no bloqueante, sin abrir diálogo
        public async Task QuickAddByBarcodeAsync()
        {
            var barcode = GameApiService.NormalizeBarcode(QuickScanBarcode);
            QuickScanBarcode = string.Empty;

            if (string.IsNullOrEmpty(barcode)) return;

            var existing = await _repo.GetByBarcodeAsync(barcode);
            if (existing != null)
            {
                SnackbarMessageQueue.Enqueue($"\"{existing.Title}\" ya está en la colección.");
                return;
            }

            var game = await _api.SearchByBarcodeAsync(barcode);
            if (game == null)
            {
                SnackbarMessageQueue.Enqueue($"Código {barcode} no encontrado. Añádelo manualmente.");
                return;
            }

            if (!string.IsNullOrEmpty(game.CoverUrl) && game.CoverData == null)
                game.CoverData = await _api.DownloadCoverAsync(game.CoverUrl);

            await _repo.AddAsync(game);
            await LoadGamesAsync();
            SnackbarMessageQueue.Enqueue($"Añadido: {game.Title}");
        }

        public async Task SaveGameAsync(Game game)
        {
            if (game.Id == 0)
                await _repo.AddAsync(game);
            else
                await _repo.UpdateAsync(game);

            await LoadGamesAsync();
        }

        public async Task DeleteGameAsync(GameViewModel gvm)
        {
            await _repo.DeleteAsync(gvm.Id);
            await LoadGamesAsync();
        }
    }
}
