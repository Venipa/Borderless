using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Borderless.App.Models;
using Borderless.App.ViewModels;

namespace Borderless.App.Views;

public partial class RulesPage : Page
{
    public RulesPage()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnEditRuleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (sender is FrameworkElement { Tag: ProcessRule rule })
        {
            ViewModel?.OpenEditEditor(rule);
        }
    }

    private void OnDeleteRuleClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { Tag: ProcessRule rule })
        {
            ViewModel?.DeleteRuleCommand.Execute(rule);
        }
    }

    private void OnAddRuleClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.OpenAddEditor();
    }
}
