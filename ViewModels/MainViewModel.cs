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

        [ObservableProperty] private string _collectionName = string.Empty;
        [ObservableProperty] private bool _hasCollectionName;
        [ObservableProperty] private string _windowTitle = "Mi Colección de Juegos";

        [ObservableProperty] private ObservableCollection<FilterOption> _platformFilters = new();
        [ObservableProperty] private ObservableCollection<FilterOption> _genreFilters = new();
        [ObservableProperty] private ObservableCollection<FilterOption> _yearFilters = new();
        [ObservableProperty] private ObservableCollection<FilterOption> _ratingFilters = new();
        [ObservableProperty] private bool _onlyMissingCover;

        [ObservableProperty] private string _sortOption = "Título (A-Z)";

        [RelayCommand]
        private void SetSortOption(string option) => SortOption = option;

        [ObservableProperty] private bool _isListView;
        [ObservableProperty] private int _selectedCount;
        [ObservableProperty] private bool _hasSelection;

        [RelayCommand]
        private void ToggleListView() => IsListView = !IsListView;

        [ObservableProperty] private bool _isFabOpen;

        [ObservableProperty] private bool _isWishlistView;
        [ObservableProperty] private string _totalSuffixText = "juegos";
        [ObservableProperty] private string _emptyStateTitle = "Tu colección está vacía";
        [ObservableProperty] private string _emptyStateSubtitle = "Pulsa \"Añadir juego\" o escanea un código de barras para empezar";

        [RelayCommand]
        private void ToggleWishlistView()
        {
            IsWishlistView = !IsWishlistView;

            TotalSuffixText = IsWishlistView ? "en la lista de deseos" : "juegos";
            EmptyStateTitle = IsWishlistView ? "Tu lista de deseos está vacía" : "Tu colección está vacía";
            EmptyStateSubtitle = IsWishlistView
                ? "Añade juegos que quieras comprar más adelante"
                : "Pulsa \"Añadir juego\" o escanea un código de barras para empezar";

            RebuildFilterOptions();
            ApplyFilters();
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var g in Games) g.IsSelected = false;
        }

        private void UpdateSelectionCount()
        {
            SelectedCount = Games.Count(g => g.IsSelected);
            HasSelection = SelectedCount > 0;
        }

        private static readonly string[] RatingLabels = { "★★★★★", "★★★★", "★★★", "★★", "★", "Sin puntuar" };
        private static readonly Dictionary<string, int> RatingLabelToValue = new()
        {
            ["★★★★★"] = 5, ["★★★★"] = 4, ["★★★"] = 3, ["★★"] = 2, ["★"] = 1, ["Sin puntuar"] = 0
        };

        public ISnackbarMessageQueue SnackbarMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

        // La View resuelve la ambigüedad mostrando un selector; null si el usuario cancela
        public Func<List<Game>, Task<Game?>>? PickCandidate { get; set; }

        public MainViewModel(GameRepository repo, GameApiService api)
        {
            _repo = repo;
            _api = api;
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnSortOptionChanged(string value) => ApplyFilters();
        partial void OnOnlyMissingCoverChanged(bool value) => ApplyFilters();

        [RelayCommand]
        public async Task LoadGamesAsync()
        {
            IsLoading = true;
            _allGames = await _repo.GetAllAsync();
            RebuildFilterOptions();
            ApplyFilters();

            CollectionName = await _repo.GetCollectionNameAsync();
            HasCollectionName = !string.IsNullOrWhiteSpace(CollectionName);
            WindowTitle = HasCollectionName ? $"Mi Colección de Juegos — {CollectionName}" : "Mi Colección de Juegos";

            IsLoading = false;
        }

        private void RebuildFilterOptions()
        {
            var scoped = _allGames.Where(g => g.IsWishlist == IsWishlistView);

            PlatformFilters = RebuildFacet(PlatformFilters, scoped.Select(g => g.Platform));
            GenreFilters = RebuildFacet(GenreFilters, scoped.Select(g => g.Genre));
            YearFilters = RebuildFacet(YearFilters, scoped.Where(g => g.Year.HasValue).Select(g => g.Year!.Value.ToString()));
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
            OnlyMissingCover = false;
        }

        private void ApplyFilters()
        {
            IEnumerable<Game> filtered = _allGames.Where(g => g.IsWishlist == IsWishlistView);

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
            if (OnlyMissingCover)
                filtered = filtered.Where(g => g.CoverData == null || g.CoverData.Length == 0);

            IEnumerable<Game> ordered = SortOption switch
            {
                "Plataforma" => filtered.OrderBy(g => g.Platform).ThenBy(g => g.Title),
                "Año (más reciente)" => filtered.OrderByDescending(g => g.Year ?? 0).ThenBy(g => g.Title),
                "Añadido recientemente" => filtered.OrderByDescending(g => g.AddedDate),
                _ => filtered.OrderBy(g => g.Title),
            };

            Games = new ObservableCollection<GameViewModel>(ordered.Select(g =>
            {
                var vm = GameViewModel.FromModel(g);
                vm.OnSelectionChanged = UpdateSelectionCount;
                return vm;
            }));
            TotalGames = Games.Count;
            UpdateSelectionCount();
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
                var where = existing.IsWishlist ? "tu lista de deseos" : "la colección";
                SnackbarMessageQueue.Enqueue($"\"{existing.Title}\" ya está en {where}.");
                return;
            }

            var candidates = await _api.SearchCandidatesByBarcodeAsync(barcode);
            if (candidates.Count == 0)
            {
                SnackbarMessageQueue.Enqueue($"Código {barcode} no encontrado. Añádelo manualmente.");
                return;
            }

            Game? game = candidates[0];
            if (candidates.Count > 1)
            {
                game = PickCandidate != null ? await PickCandidate(candidates) : candidates[0];
                if (game == null)
                {
                    SnackbarMessageQueue.Enqueue("Selección cancelada.");
                    return;
                }
            }

            if (!string.IsNullOrEmpty(game.CoverUrl) && game.CoverData == null)
                game.CoverData = await _api.DownloadCoverAsync(game.CoverUrl);

            try
            {
                await _repo.AddAsync(game);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                SnackbarMessageQueue.Enqueue($"Ese código ya existe en la papelera. Restaura o vacía la papelera primero.");
                return;
            }

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
            var id = gvm.Id;
            var title = gvm.Title;
            await _repo.DeleteAsync(id);
            await LoadGamesAsync();

            // Borrado suave: el juego queda en la papelera, así que deshacer es solo restaurarlo
            SnackbarMessageQueue.Enqueue($"\"{title}\" eliminado.", "DESHACER",
                async () => await UndoDeleteAsync(id));
        }

        private async Task UndoDeleteAsync(int id)
        {
            await _repo.RestoreAsync(id);
            await LoadGamesAsync();
        }

        // Borrado suave igual que DeleteGameAsync: los seleccionados quedan en la papelera,
        // así que se puede deshacer devolviendo sus ids a UndoDeleteSelectedAsync.
        public async Task<List<int>> DeleteSelectedAsync()
        {
            var ids = Games.Where(g => g.IsSelected).Select(g => g.Id).ToList();
            foreach (var id in ids)
                await _repo.DeleteAsync(id);

            await LoadGamesAsync();
            return ids;
        }

        public async Task UndoDeleteSelectedAsync(List<int> ids)
        {
            foreach (var id in ids)
                await _repo.RestoreAsync(id);

            await LoadGamesAsync();
        }

        // Ya lo has comprado: pasa el juego de la lista de deseos a la colección
        public async Task MoveToCollectionAsync(GameViewModel gvm)
        {
            var game = gvm.ToModel();
            game.IsWishlist = false;
            await _repo.UpdateAsync(game);
            await LoadGamesAsync();

            SnackbarMessageQueue.Enqueue($"\"{game.Title}\" movido a la colección.");
        }
    }
}
