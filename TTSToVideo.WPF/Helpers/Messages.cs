using NetXP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TTSToVideo.WPF.Helpers.Implementations
{
    public class Messages : IMessage
    {
        public async Task<bool> Confirm(string message)
        {
            await Task.Yield();
            MessageBoxResult result = MessageBox.Show(message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public void Error(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void Fatal(string message)
        {
            MessageBox.Show(message, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        public void Info(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void Warn(string message)
        {
            MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
