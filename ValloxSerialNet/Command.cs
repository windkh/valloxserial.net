using System;
using System.Windows.Input;

namespace ValloxSerialNet
{
    /// <summary>
    /// Simple command implementation.
    /// </summary>
    public class Command : ICommand
    {
        private readonly Action _executeAction;
        private readonly Func<bool> _canExecuteFunction;

        public Command(Action executeAction, Func<bool> canExecuteFunction)
        {
            _executeAction = executeAction;
            _canExecuteFunction = canExecuteFunction;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecuteFunction();
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter)
        {
            _executeAction();
        }

        public void RaiseCanExecuteChanged()
        {
            EventHandler handler = CanExecuteChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
