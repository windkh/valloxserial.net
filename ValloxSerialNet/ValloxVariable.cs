namespace ValloxSerialNet
{
    /// <summary>
    /// Contains one single variable.
    /// </summary>
    internal class ValloxVariable : NotificationObject
    {
        private byte _value = 0;
        private int _counter = 1;

        public ValloxVariable(byte id, string description)
        {
            Id = id;
            Description = description;
        }

        public string Description { get; private set; }
        public byte Id { get; private set; }

        public byte Value
        {
            get
            {
                return _value;
            }

            set
            {
                if (value != _value)
                {
                    _value = value;
                    RaisePropertyChanged("Value");
                }

                OnValueChanged();
            }
        }

        private void OnValueChanged()
        {
            Counter++;
        }

        public int Counter
        {
            get
            {
                return _counter;
            }

            set
            {
                if (value != _counter)
                {
                    _counter = value;
                    RaisePropertyChanged("Counter");
                }
            }
        }
    }
}
