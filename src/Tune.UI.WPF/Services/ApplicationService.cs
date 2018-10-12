using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tune.UI.MVVM.Services;

namespace Tune.UI.WPF.Services
{
    class ApplicationService : IApplicationService
    {
        public void Exit()
        {
            Application.Current.MainWindow.Close();
        }
    }
}
