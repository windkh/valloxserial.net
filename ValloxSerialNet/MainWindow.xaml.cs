using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace ValloxSerialNet
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private  readonly ValloxModel _viewModel = new ValloxModel();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _viewModel;

            this.Loaded += (sender, args) =>
            {
                SortListView(VariablesListView, "Id", ListSortDirection.Ascending);
                SortListView(DevicesListView, "Address", ListSortDirection.Ascending);

                StartPolling();
            }; 
        }

        private void StartPolling()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0,0,0,2);

            Byte variable = 0;
            timer.Tick += (sender, args) =>
            {
                _viewModel.ReadVariable(variable);
                variable++;
                if (variable > 255)
                {
                    //timer.Stop();
                }
            };

            timer.Start();
        }

        private void SortListView(ListView listView, string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView =
            CollectionViewSource.GetDefaultView(listView.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }      
    }
}