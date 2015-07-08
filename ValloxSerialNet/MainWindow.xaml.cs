using System.Windows;

namespace ValloxSerialNet
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ValloxModel viewModel = new ValloxModel();
            DataContext = viewModel;
        }
    }
}