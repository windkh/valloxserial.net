using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValloxSerialNet
{
    public class Statistics : NotificationObject
    {
        private int _rxCount = 0;
        private int _txCount = 0;


        public Statistics(string name, byte id)
        {
            Name = name;
            Id = id;
        }

        public string Name
        {
            get; 
            private set;
        }

        public byte Id
        {
            get;
            private set;
        }

        public int RxCount
        {
            get
            {
                return _rxCount;
            }
            set
            {
                if (value != _rxCount)
                {
                    _rxCount = value;
                    RaisePropertyChanged(("RxCount"));
                }
            }
        }


        public int TxCount
        {
            get
            {
                return _txCount;
            }
            set
            {
                if (value != _txCount)
                {
                    _txCount = value;
                    RaisePropertyChanged(("TxCount"));
                }
            }
        }
    }
}
