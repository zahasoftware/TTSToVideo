using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.Helpers.Implementations
{
    public class ProgressBar : IProgressBar
    {
        public event EventHandler<int>? Incrementing;
        public event EventHandler<string>? MessageChanged;

        private double _iTotal;
        public double Total
        {
            get { return _iTotal; }
            set
            {
                Clear();
                _iTotal = value;
            }
        }

        public bool IsIncrementing
        {
            get
            {
                return _iCont != 0;
            }
        }


        private int _iCont;

        private readonly object oLock = new();
        public void Increment()
        {
            lock (oLock)
            {
                this._iCont++;

                var percent = (int)((_iCont * 100) / Total);
                if (this.Incrementing != null)
                {
                    Incrementing(this, percent);
                }
            }
        }

        public void ShowMessage(string sMessage)
        {
            MessageChanged?.Invoke(this, sMessage);
        }


        public void Clear()
        {
            _iCont = 0;
            _iTotal = 0;
            Incrementing?.Invoke(this, 0);
            MessageChanged?.Invoke(this, "");
        }
    }
}
