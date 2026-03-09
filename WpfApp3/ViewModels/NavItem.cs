using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace WpfApp3.ViewModels;

public partial class NavItem : ObservableObject
{
    public string Title { get; }
    public ICommand Command { get; }

    public NavItem(string title, ICommand command)
    {
        Title = title;
        Command = command;
    }
}
