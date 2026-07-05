using AppUI.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace AppUI.Deck.Dialogs
{
    /// <summary>
    /// The Deck shell's dialog slot: a stack (nested dialogs are possible — each one
    /// pumps its own dispatcher frame) with the topmost bound by the shell window.
    /// </summary>
    public class DeckDialogHostViewModel : ViewModelBase
    {
        private readonly List<DeckDialogViewModel> _stack = new List<DeckDialogViewModel>();

        public DeckDialogViewModel Current
        {
            get { return _stack.LastOrDefault(); }
        }

        public bool HasDialog
        {
            get { return _stack.Any(); }
        }

        internal void Push(DeckDialogViewModel dialog)
        {
            _stack.Add(dialog);
            NotifyChanged();
        }

        internal void Pop(DeckDialogViewModel dialog)
        {
            _stack.Remove(dialog);
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            NotifyPropertyChanged(nameof(Current));
            NotifyPropertyChanged(nameof(HasDialog));
        }
    }
}
