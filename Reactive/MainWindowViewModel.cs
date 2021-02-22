using GalaSoft.MvvmLight;
using Oracle.ManagedDataAccess.Client;
using Reactive.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive
{
    public sealed class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private bool _disposedValue;
        private readonly OracleConnection _connection;

        private string _searchString;

        private readonly IDisposable _searchSubscription;
        public string SearchString
        {
            get => _searchString;
            set
            {
                if (Set(ref _searchString, value))
                {
                    StringObservable.StringChanged(value);
                }
            }
        }

        private StringObservable StringObservable { get; }

        public List<Drug> Results { get; private set; } = new List<Drug>();

        public ObservableCollection<Drug> Drugs { get; } = new ObservableCollection<Drug>();

        public IDrugSearch DrugSearch { get; }


        public MainWindowViewModel()
        {
            _connection = new OracleConnection("DATA SOURCE=MF.MDB01;PERSIST SECURITY INFO=True;");
            DrugSearch = new DrugSearch(_connection);
            StringObservable = new StringObservable();

            var searchTextObservable = StringObservable
                .Where(text => text.Length > 2 || text.Length == 0)
                .Throttle(TimeSpan.FromMilliseconds(300))
                .DistinctUntilChanged()
                .PairWithPrevious()
                .Where(pair => !AlreadyEmptySearch(pair.previous, pair.current, Results))
                .Select(pair => DoSearchAsync(pair.previous, pair.current))
                .Merge()
                .ObserveOnDispatcher();

            _searchSubscription = searchTextObservable.Subscribe(list =>
            {
                Drugs.Clear();
                foreach (var drug in list)
                {
                    Drugs.Add(drug);
                }
            });
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue && disposing)
            {
                _connection?.Dispose();
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private static bool AlreadyEmptySearch(string previous, string current, IEnumerable<Drug> list) =>
                previous != null && current.StartsWith(previous, StringComparison.OrdinalIgnoreCase) && !list.Any();

        private async Task<IEnumerable<Drug>> DoSearchAsync(string previous, string current)
        {
            if (string.IsNullOrEmpty(current))
            {
                Debug.WriteLine("## Clear");
                return Enumerable.Empty<Drug>();
            }

            if (previous != null && current.StartsWith(previous, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("+++ Reusing...");
                Results = Results.Where(s => s.Name.StartsWith(current, StringComparison.OrdinalIgnoreCase)).ToList();
                return Results;
            }

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                _connection.Open();
            }

            Debug.WriteLine("*** Searching...");
            var results = await DrugSearch.SearchDrug(current, CancellationToken.None).ConfigureAwait(true);
            Results = results.ToList();

            return Results;
        }
    }
}
