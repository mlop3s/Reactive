using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using Reactive.Services;
using System.Collections.ObjectModel;
using Oracle.ManagedDataAccess.Client;
using GalaSoft.MvvmLight.Command;
using System.Threading;

namespace Reactive
{
    public sealed class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private bool _disposedValue;
        private readonly OracleConnection _connection;
        private bool _isSearching;

        private string _searchString;
        public string SearchString
        {
            get => _searchString;
            set
            {
                if (Set(ref _searchString, value))
                {
                    Search.RaiseCanExecuteChanged();
                }
            }
        }


        public List<Drug> Results { get; private set; } = new List<Drug>();

        public ObservableCollection<Drug> Drugs { get; } = new ObservableCollection<Drug>();

        public RelayCommand Search { get; set; }

        public IDrugSearch DrugSearch { get; }


        public MainWindowViewModel()
        {
            _connection = new OracleConnection("DATA SOURCE=MF.MDB01;PERSIST SECURITY INFO=True;");
            Search = new RelayCommand(DoSearch, CanDoSearch);
            DrugSearch = new DrugSearch(_connection);
        }

        private bool CanDoSearch() => !string.IsNullOrEmpty(SearchString) && !_isSearching;

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

        private void DoSearch()
        {
            DoSearchAsync()
                .ContinueWith(
                    x =>
                    {
                        Console.WriteLine(x.Exception);
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task DoSearchAsync()
        {
            Drugs.Clear();
            _isSearching = true;

            try
            {
                if (_connection.State != System.Data.ConnectionState.Open)
                {
                    _connection.Open();
                }

                var result = await DrugSearch.SearchDrug(SearchString, CancellationToken.None).ConfigureAwait(true);
                Results = result.ToList();
                foreach (var drug in Results)
                {
                    Drugs.Add(drug);
                }
            }
            finally
            {
                _isSearching = false;
                Search.RaiseCanExecuteChanged();
            }
        }
    }
}
