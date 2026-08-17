using VideoGameLibrary.Models;
using VideoGameLibrary.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace VideoGameLibrary.ViewModels
{
    public class StatsRow
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Total { get; set; }
        public string PercentageText => Total > 0 ? $"{Count * 100 / Total}%" : string.Empty;
    }

    public partial class StatsViewModel : ObservableObject
    {
        private readonly GameRepository _repo;

        [ObservableProperty] private bool _isLoading = true;
        [ObservableProperty] private int _totalGames;
        [ObservableProperty] private int _playedCount;
        [ObservableProperty] private int _notPlayedCount;
        [ObservableProperty] private string _playedPercentageText = string.Empty;
        [ObservableProperty] private int _wishlistCount;

        public ObservableCollection<StatsRow> ByPlatform { get; } = new();
        public ObservableCollection<StatsRow> ByGenre { get; } = new();

        public StatsViewModel(GameRepository repo)
        {
            _repo = repo;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;

            var allGames = await _repo.GetAllAsync();
            WishlistCount = allGames.Count(g => g.IsWishlist);

            // Las estadísticas de la colección no mezclan juegos que aún no tienes (lista de deseos)
            var games = allGames.Where(g => !g.IsWishlist).ToList();

            TotalGames = games.Count;
            PlayedCount = games.Count(g => g.Played);
            NotPlayedCount = TotalGames - PlayedCount;
            PlayedPercentageText = TotalGames > 0 ? $"{PlayedCount * 100 / TotalGames}%" : "—";

            ByPlatform.Clear();
            foreach (var row in BuildRows(games, g => g.Platform))
                ByPlatform.Add(row);

            ByGenre.Clear();
            foreach (var row in BuildRows(games, g => g.Genre))
                ByGenre.Add(row);

            IsLoading = false;
        }

        private static List<StatsRow> BuildRows(List<Game> games, Func<Game, string> selector)
        {
            var total = games.Count;
            return games
                .Where(g => !string.IsNullOrWhiteSpace(selector(g)))
                .GroupBy(selector)
                .Select(gr => new StatsRow { Label = gr.Key, Count = gr.Count(), Total = total })
                .OrderByDescending(r => r.Count)
                .ToList();
        }
    }
}
