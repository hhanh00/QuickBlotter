using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace QuickBlotterUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, ViewModel.IBatchUpdate
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var dc = DataContext;
            if (dc != null)
            {
                var vm = dc as ViewModel.OrderBlotter;
                vm.SetView(this);
            }
        }

        public void BeginUpdate()
        {
            Grid.BeginDataUpdate();
        }

        public void EndUpdate()
        {
            Grid.EndDataUpdate();
        }
    }
}
