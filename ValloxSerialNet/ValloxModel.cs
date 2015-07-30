using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Windows.Threading;

namespace ValloxSerialNet
{
    /// <summary>
    ///     This software requires a RS485 to COM Port Hardware.
    ///     Suspend and Resume commands are not implemented!!!
    ///     91 and 8F
    /// 
    ///     TODO:
    /// 
    /// Read Variable
    /// 
    /// LogWindow
    /// 
    /// Variables
    ///  IoPortFanSpeedRelays
    ///     IoPortMultiPurpose1
    ///     IoPortMultiPurpose2
    ///     MachineInstalledC02Sensor
    ///     LastErrorNumber
    ///     PostHeatingOnCounter
    ///     PostHeatingOffTime
    ///     PostHeatingTargetValue
    ///     Flags1
    ///     Flags2
    ///     Flags3
    ///     Flags4
    ///     Flags5
    ///     Flags6
    ///     FirePlaceBoosterCounter
    ///     MaintenanceMonthCounter
    /// </summary>
    internal class ValloxModel : NotificationObject
    {
        private byte _senderId = Vallox.Adress.Panel2; 

        private readonly List<Byte> _availableFanSpeeds = new List<Byte> {1, 2, 3, 4, 5, 6, 7, 8};

        private readonly ObservableCollection<Statistics> _detectedDevices = new ObservableCollection<Statistics>();
        private readonly Dispatcher _dispatcher;
        private readonly Queue<Byte> _interpreterQueue = new Queue<Byte>(Vallox.TelegramLength);
        private readonly Queue<Byte> _receiveQueue = new Queue<Byte>();
        private readonly SerialPort _serialPort = new SerialPort();
        private readonly ObservableCollection<ValloxVariable> _variables = new ObservableCollection<ValloxVariable>();

        private int _adjustmentIntervalMinutes;
        private bool _automaticHumidityLevelSeekerState;
        private int _basicHumidityLevel;
        private Vallox.BoostSwitchMode _boostSwitchMode = Vallox.BoostSwitchMode.Boost;
        private bool _cascadeAdjust;
        private int _cellDefrostingThreshold;
        private bool _co2AdjustState;
        private int _co2High;
        private int _co2Low;
        private int _co2SetPointLower;
        private int _co2SetPointUpper;
        private string _comPort;
        private Command _connectCommand;
        private int _dcFanInputAdjustment;
        private int _dcFanOutputAdjustment;
        private Command _disconnectCommand;


        private int _fanSpeed;
        private int _fanSpeedMax;
        private int _fanSpeedMin;
        private bool _faultIndicator;
        private bool _filterGuardIndicator;
        private bool _heatingIndicator;
        private int _heatingSetPoint;
        private bool _heatingState;

        private int _hrcBypassThreshold;

        private int _humidity;
        private bool _humidityAdjustState;
        private int _humiditySensor1;
        private int _humiditySensor2;

        private int _inputFanStopThreshold;

        private Vallox.MaxSpeedLimitMode _maxSpeedLimitMode = Vallox.MaxSpeedLimitMode.Always;
        private bool _powerState;
        private int _preHeatingSetPoint;
        private Vallox.RadiatorType _radiatorType = Vallox.RadiatorType.Electric;
        private Byte _selectedFanSpeed = 1;
        private Byte _selectedVariable;
        private Byte _selectedValue;

        private int _serviceReminder;
        private bool _serviceReminderIndicator;
        private Command _setFanSpeedCommand;
        private Command _readVariableCommand;
        private Command _writeVariableCommand;
        private int _tempExhaust;
        private int _tempIncomming;
        private int _tempInside;
        private int _tempOutside;
        

        public ValloxModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public void ExecuteConnect()
        {
            if (!IsConnected)
            {
                _serialPort.PortName = ComPort;
                _serialPort.BaudRate = 9600;
                _serialPort.Parity = Parity.None;
                _serialPort.StopBits = StopBits.One;
                _serialPort.DataBits = 8;
                _serialPort.Handshake = Handshake.None;

                _serialPort.DataReceived += SerialPortDataReceivedHandler;
                _serialPort.ReceivedBytesThreshold = Vallox.TelegramLength;
                _serialPort.ReadBufferSize = 1024;

                _serialPort.Open();

                ConnectCommand.RaiseCanExecuteChanged();
                DisconnectCommand.RaiseCanExecuteChanged();
                SetFanSpeedCommand.RaiseCanExecuteChanged();
                ReadVariableCommand.RaiseCanExecuteChanged();
                WriteVariableCommand.RaiseCanExecuteChanged();

                RaisePropertyChanged("IsConnected");
            }
        }

        public void ExecuteDisconnect()
        {
            if (IsConnected)
            {
                _serialPort.Close();

                ConnectCommand.RaiseCanExecuteChanged();
                DisconnectCommand.RaiseCanExecuteChanged();
                SetFanSpeedCommand.RaiseCanExecuteChanged();
                ReadVariableCommand.RaiseCanExecuteChanged();
                WriteVariableCommand.RaiseCanExecuteChanged();
            }
        }

        public void ExecuteSetFanSpeed()
        {
            if (IsConnected)
            {
                Byte fanSpeed = Vallox.ConvertBackFanSpeed(SelectedFanSpeed - 1);
                Byte[] telegram = Vallox.CreateTelegram(_senderId, Vallox.Adress.Master, Vallox.Variable.FanSpeed,
                    fanSpeed);
                _serialPort.Write(telegram, 0, telegram.Length);
            }
        }

        public void ReadVariable(Byte variable)
        {
            if (IsConnected)
            {
                Byte[] telegram = Vallox.CreateTelegram(_senderId, Vallox.Adress.Master, Vallox.PollRequest, variable);
                _serialPort.Write(telegram, 0, telegram.Length);
            }
        }

        public void ExecuteReadVariable()
        {
            Byte variable = SelectedVariable;
            ReadVariable(variable);
        }

        public void WriteVariable(Byte variable, Byte value)
        {
            if (IsConnected)
            {
                Byte[] telegram = Vallox.CreateTelegram(_senderId, Vallox.Adress.Master, variable, value);
                _serialPort.Write(telegram, 0, telegram.Length);
            }
        }

        public void ExecuteWriteVariable()
        {
            Byte variable = SelectedVariable;
            Byte value = SelectedValue;
            WriteVariable(variable, value);
        }

        #region Commands

        public Command ConnectCommand
        {
            get
            {
                if (_connectCommand == null)
                {
                    _connectCommand = new Command(ExecuteConnect, () => { return (ComPort != null && !IsConnected); });
                }

                return _connectCommand;
            }
        }

        public Command DisconnectCommand
        {
            get
            {
                if (_disconnectCommand == null)
                {
                    _disconnectCommand = new Command(ExecuteDisconnect, () => IsConnected);
                }

                return _disconnectCommand;
            }
        }

        public Command SetFanSpeedCommand
        {
            get
            {
                if (_setFanSpeedCommand == null)
                {
                    _setFanSpeedCommand = new Command(ExecuteSetFanSpeed, () => IsConnected);
                }

                return _setFanSpeedCommand;
            }
        }

        public Command ReadVariableCommand
        {
            get
            {
                if (_readVariableCommand == null)
                {
                    _readVariableCommand = new Command(ExecuteReadVariable, () => IsConnected);
                }

                return _readVariableCommand;
            }
        }

        public Command WriteVariableCommand
        {
            get
            {
                if (_writeVariableCommand == null)
                {
                    _writeVariableCommand = new Command(ExecuteWriteVariable, () => IsConnected);
                }

                return _writeVariableCommand;
            }
        }

        public bool IsConnected
        {
            get
            {
                return _serialPort.IsOpen;
            }
        }

        #endregion

        #region Properties

        #region Ui related properties

        public byte SenderId
        {
            get
            {
                return _senderId;
            }
            set
            {
                if (value != _senderId)
                {
                    _senderId = value;
                    RaisePropertyChanged(("SenderId"));
                    ConnectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string ComPort
        {
            get
            {
                return _comPort;
            }
            set
            {
                if (value != _comPort)
                {
                    _comPort = value;
                    RaisePropertyChanged(("ComPort"));
                    ConnectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string[] AvailableComPorts
        {
            get
            {
                return SerialPort.GetPortNames();
            }
        }

        public List<Byte> AvailableFanSpeeds
        {
            get
            {
                return _availableFanSpeeds;
            }
        }

        public Byte SelectedFanSpeed
        {
            get
            {
                return _selectedFanSpeed;
            }

            set
            {
                if (value != _selectedFanSpeed)
                {
                    _selectedFanSpeed = value;
                    RaisePropertyChanged("SelectedFanSpeed");
                }
            }
        }

        public Byte SelectedVariable
        {
            get
            {
                return _selectedVariable;
            }

            set
            {
                if (value != _selectedVariable)
                {
                    _selectedVariable = value;
                    RaisePropertyChanged("SelectedVariable");
                }
            }
        }

        public Byte SelectedValue
        {
            get
            {
                return _selectedValue;
            }

            set
            {
                if (value != _selectedValue)
                {
                    _selectedValue = value;
                    RaisePropertyChanged("SelectedValue");
                }
            }
        }

        public ObservableCollection<Statistics> DetectedDevices
        {
            get
            {
                return _detectedDevices;
            }
        }


        /// <summary>
        /// Contains the latest value of the received variables.
        /// </summary>
        public ObservableCollection<ValloxVariable> Variables
        {
            get
            {
                return _variables;
            }
        }

        #endregion

        #region Vallox properties

        public bool PowerState
        {
            get
            {
                return _powerState;
            }
            set
            {
                if (value != _powerState)
                {
                    _powerState = value;
                    RaisePropertyChanged(("PowerState"));
                }
            }
        }

        public bool Co2AdjustState
        {
            get
            {
                return _co2AdjustState;
            }
            set
            {
                if (value != _co2AdjustState)
                {
                    _co2AdjustState = value;
                    RaisePropertyChanged(("Co2AdjustState"));
                }
            }
        }

        public bool HumidityAdjustState
        {
            get
            {
                return _humidityAdjustState;
            }
            set
            {
                if (value != _humidityAdjustState)
                {
                    _humidityAdjustState = value;
                    RaisePropertyChanged(("HumidityAdjustState"));
                }
            }
        }

        public bool HeatingState
        {
            get
            {
                return _heatingState;
            }
            set
            {
                if (value != _heatingState)
                {
                    _heatingState = value;
                    RaisePropertyChanged(("HumidityAdjustState"));
                }
            }
        }

        public bool FilterGuardIndicator
        {
            get
            {
                return _filterGuardIndicator;
            }
            set
            {
                if (value != _filterGuardIndicator)
                {
                    _filterGuardIndicator = value;
                    RaisePropertyChanged(("FilterGuardIndicator"));
                }
            }
        }

        public bool HeatingIndicator
        {
            get
            {
                return _heatingIndicator;
            }
            set
            {
                if (value != _heatingIndicator)
                {
                    _heatingIndicator = value;
                    RaisePropertyChanged(("HeatingIndicator"));
                }
            }
        }

        public bool FaultIndicator
        {
            get
            {
                return _faultIndicator;
            }
            set
            {
                if (value != _faultIndicator)
                {
                    _faultIndicator = value;
                    RaisePropertyChanged(("FaultIndicator"));
                }
            }
        }

        public bool ServiceReminderIndicator
        {
            get
            {
                return _serviceReminderIndicator;
            }
            set
            {
                if (value != _serviceReminderIndicator)
                {
                    _serviceReminderIndicator = value;
                    RaisePropertyChanged(("ServiceReminderIndicator"));
                }
            }
        }

        public Vallox.MaxSpeedLimitMode MaxSpeedLimitMode
        {
            get
            {
                return _maxSpeedLimitMode;
            }
            set
            {
                if (value != _maxSpeedLimitMode)
                {
                    _maxSpeedLimitMode = value;
                    RaisePropertyChanged(("MaxSpeedLimitMode"));
                }
            }
        }

        public int AdjustmentIntervalMinutes
        {
            get
            {
                return _adjustmentIntervalMinutes;
            }
            set
            {
                if (value != _adjustmentIntervalMinutes)
                {
                    _adjustmentIntervalMinutes = value;
                    RaisePropertyChanged(("AdjustmentIntervalMinutes"));
                }
            }
        }

        public bool AutomaticHumidityLevelSeekerState
        {
            get
            {
                return _automaticHumidityLevelSeekerState;
            }
            set
            {
                if (value != _automaticHumidityLevelSeekerState)
                {
                    _automaticHumidityLevelSeekerState = value;
                    RaisePropertyChanged(("AutomaticHumidityLevelSeekerState"));
                }
            }
        }

        public Vallox.BoostSwitchMode BoostSwitchMode
        {
            get
            {
                return _boostSwitchMode;
            }
            set
            {
                if (value != _boostSwitchMode)
                {
                    _boostSwitchMode = value;
                    RaisePropertyChanged(("BoostSwitchMode"));
                }
            }
        }

        public Vallox.RadiatorType RadiatorType
        {
            get
            {
                return _radiatorType;
            }
            set
            {
                if (value != _radiatorType)
                {
                    _radiatorType = value;
                    RaisePropertyChanged(("RadiatorType"));
                }
            }
        }

        public bool CascadeAdjust
        {
            get
            {
                return _cascadeAdjust;
            }
            set
            {
                if (value != _cascadeAdjust)
                {
                    _cascadeAdjust = value;
                    RaisePropertyChanged(("CascadeAdjust"));
                }
            }
        }

        public int FanSpeed
        {
            get
            {
                return _fanSpeed;
            }
            set
            {
                if (value != _fanSpeed)
                {
                    _fanSpeed = value;
                    RaisePropertyChanged(("FanSpeed"));
                }
            }
        }

        public int FanSpeedMax
        {
            get
            {
                return _fanSpeedMax;
            }
            set
            {
                if (value != _fanSpeedMax)
                {
                    _fanSpeedMax = value;
                    RaisePropertyChanged(("FanSpeedMax"));
                }
            }
        }

        public int FanSpeedMin
        {
            get
            {
                return _fanSpeedMin;
            }
            set
            {
                if (value != _fanSpeedMin)
                {
                    _fanSpeedMin = value;
                    RaisePropertyChanged(("FanSpeedMin"));
                }
            }
        }

        public int TempInside
        {
            get
            {
                return _tempInside;
            }
            set
            {
                if (value != _tempInside)
                {
                    _tempInside = value;
                    RaisePropertyChanged(("TempInside"));
                }
            }
        }

        public int TempOutside
        {
            get
            {
                return _tempOutside;
            }
            set
            {
                if (value != _tempOutside)
                {
                    _tempOutside = value;
                    RaisePropertyChanged(("TempOutside"));
                }
            }
        }

        public int TempExhaust
        {
            get
            {
                return _tempExhaust;
            }
            set
            {
                if (value != _tempExhaust)
                {
                    _tempExhaust = value;
                    RaisePropertyChanged(("TempExhaust"));
                }
            }
        }

        public int TempIncomming
        {
            get
            {
                return _tempIncomming;
            }
            set
            {
                if (value != _tempIncomming)
                {
                    _tempIncomming = value;
                    RaisePropertyChanged(("TempIncomming"));
                }
            }
        }

        public int HrcBypassThreshold
        {
            get
            {
                return _hrcBypassThreshold;
            }
            set
            {
                if (value != _hrcBypassThreshold)
                {
                    _hrcBypassThreshold = value;
                    RaisePropertyChanged(("HrcBypassThreshold"));
                }
            }
        }

        public int DcFanInputAdjustment
        {
            get
            {
                return _dcFanInputAdjustment;
            }
            set
            {
                if (value != _dcFanInputAdjustment)
                {
                    _dcFanInputAdjustment = value;
                    RaisePropertyChanged(("DcFanInputAdjustment"));
                }
            }
        }

        public int DcFanOutputAdjustment
        {
            get
            {
                return _dcFanOutputAdjustment;
            }
            set
            {
                if (value != _dcFanOutputAdjustment)
                {
                    _dcFanOutputAdjustment = value;
                    RaisePropertyChanged(("DcFanOutputAdjustment"));
                }
            }
        }

        public int CellDefrostingThreshold
        {
            get
            {
                return _cellDefrostingThreshold;
            }
            set
            {
                if (value != _cellDefrostingThreshold)
                {
                    _cellDefrostingThreshold = value;
                    RaisePropertyChanged(("CellDefrostingThreshold"));
                }
            }
        }

        public int BasicHumidityLevel
        {
            get
            {
                return _basicHumidityLevel;
            }
            set
            {
                if (value != _basicHumidityLevel)
                {
                    _basicHumidityLevel = value;
                    RaisePropertyChanged(("BasicHumidityLevel"));
                }
            }
        }

        public int Humidity
        {
            get
            {
                return _humidity;
            }
            set
            {
                if (value != _humidity)
                {
                    _humidity = value;
                    RaisePropertyChanged(("Humidity"));
                }
            }
        }

        public int Co2High
        {
            get
            {
                return _co2High;
            }
            set
            {
                if (value != _co2High)
                {
                    _co2High = value;
                    RaisePropertyChanged(("Co2High"));
                }
            }
        }

        public int Co2Low
        {
            get
            {
                return _co2Low;
            }
            set
            {
                if (value != _co2Low)
                {
                    _co2Low = value;
                    RaisePropertyChanged(("Co2Low"));
                }
            }
        }

        public int Co2SetPointUpper
        {
            get
            {
                return _co2SetPointUpper;
            }
            set
            {
                if (value != _co2SetPointUpper)
                {
                    _co2SetPointUpper = value;
                    RaisePropertyChanged(("Co2SetPointUpper"));
                }
            }
        }

        public int Co2SetPointLower
        {
            get
            {
                return _co2SetPointLower;
            }
            set
            {
                if (value != _co2SetPointLower)
                {
                    _co2SetPointLower = value;
                    RaisePropertyChanged(("Co2SetPointLower"));
                }
            }
        }

        public int HumiditySensor1
        {
            get
            {
                return _humiditySensor1;
            }
            set
            {
                if (value != _humiditySensor1)
                {
                    _humiditySensor1 = value;
                    RaisePropertyChanged(("HumiditySensor1"));
                }
            }
        }

        public int HumiditySensor2
        {
            get
            {
                return _humiditySensor2;
            }
            set
            {
                if (value != _humiditySensor2)
                {
                    _humiditySensor2 = value;
                    RaisePropertyChanged(("HumiditySensor2"));
                }
            }
        }

        public int HeatingSetPoint
        {
            get
            {
                return _heatingSetPoint;
            }
            set
            {
                if (value != _heatingSetPoint)
                {
                    _heatingSetPoint = value;
                    RaisePropertyChanged(("HeatingSetPoint"));
                }
            }
        }

        public int PreHeatingSetPoint
        {
            get
            {
                return _preHeatingSetPoint;
            }
            set
            {
                if (value != _preHeatingSetPoint)
                {
                    _preHeatingSetPoint = value;
                    RaisePropertyChanged(("PreHeatingSetPoint"));
                }
            }
        }

        public int InputFanStopThreshold
        {
            get
            {
                return _inputFanStopThreshold;
            }
            set
            {
                if (value != _inputFanStopThreshold)
                {
                    _inputFanStopThreshold = value;
                    RaisePropertyChanged(("InputFanStopThreshold"));
                }
            }
        }

        public int ServiceReminder
        {
            get
            {
                return _serviceReminder;
            }
            set
            {
                if (value != _serviceReminder)
                {
                    _serviceReminder = value;
                    RaisePropertyChanged(("ServiceReminder"));
                }
            }
        }

        #endregion

        #endregion

        #region Privates

        private void SerialPortDataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            var sp = (SerialPort) sender;

            int bytesToRead = sp.BytesToRead;
            for (int i = 0; i < bytesToRead; i++)
            {
                var b = (Byte) sp.ReadByte();
                _receiveQueue.Enqueue(b);
            }

            InterpretReceivedData();
        }

        /// <summary>
        ///     Move everything into the buffers and trigger intrperter
        /// </summary>
        private void InterpretReceivedData()
        {
            while (_receiveQueue.Count() > 0)
            {
                Byte receivedByte = _receiveQueue.Dequeue();
                _interpreterQueue.Enqueue(receivedByte);
                if (_interpreterQueue.Count() == Vallox.TelegramLength)
                {
                    if (_dispatcher.Invoke(() => Interpret()))
                    {
                        _interpreterQueue.Clear();
                    }
                    else
                    {
                        // This could be neccessary when we get connected and receive only a part of a complete telegram.
                        Byte droppedByte = _interpreterQueue.Dequeue();
                        WriteLine("Dropped Byte {0:X02}", droppedByte);
                    }
                }
            }
        }

        /// <summary>
        ///     Try to detect a valid telegram and call the interpreter
        /// </summary>
        /// <returns></returns>
        private bool Interpret()
        {
            bool success = false;

            Queue<byte>.Enumerator enumerator = _interpreterQueue.GetEnumerator();
            enumerator.MoveNext();

            Byte domain = enumerator.Current;
            if (domain == Vallox.Domain)
            {
                enumerator.MoveNext();
                Byte sender = enumerator.Current;

                enumerator.MoveNext();
                Byte receiver = enumerator.Current;

                enumerator.MoveNext();
                Byte command = enumerator.Current;

                enumerator.MoveNext();
                Byte arg = enumerator.Current;

                enumerator.MoveNext();
                Byte checksum = enumerator.Current;

                Byte computedCheckSum = Vallox.ComputeCheckSum(domain, sender, receiver, command, arg);
                if (checksum == computedCheckSum)
                {
                    Write("{0:X02} {1:X02} {2:X02} {3:X02} {4:X02} {5:X02}    ", domain, sender, receiver, command, arg,
                        checksum);
                    Interpret(domain, sender, receiver, command, arg);
                    success = true;
                }
                else
                {
                    WriteLine("Wrong checksum: {0:X02} {1:X02} {2:X02} {3:X02} {4:X02} {5:X02} ({6:X02})", domain,
                        sender, receiver, command, arg, checksum, computedCheckSum);
                    success = false;
                }
            }

            return success;
        }

        /// <summary>
        ///     Intrepret approved message
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="sender"></param>
        /// <param name="receiver"></param>
        /// <param name="command"></param>
        /// <param name="value"></param>
        private void Interpret(Byte domain, Byte sender, Byte receiver, Byte command, Byte value)
        {
            string senderString = Vallox.ConvertAddress(sender);
            UpdateStatistics(senderString, sender, true, false);

            string receiverString = Vallox.ConvertAddress(receiver);
            UpdateStatistics(receiverString, receiver, false, true);


            if (receiver == Vallox.Adress.Panel1 || receiver == Vallox.Adress.Panel8 || receiver == _senderId ||
                receiver == Vallox.Adress.Panels) // TODO: right now 0x20 0x21 0x22 are received
            {
                Byte variable = command;

                ValloxVariable variableItem = GetVariableItem(variable);
                variableItem.Value = value;

                switch (variable)
                {
                    case Vallox.Variable.FanSpeed:
                    {
                        int fanSpeed = Vallox.ConvertFanSpeed(value);
                        WriteLine("Fan speed {0}", fanSpeed);
                        FanSpeed = fanSpeed;
                        break;
                    }
                    case Vallox.Variable.Humidity:
                    {
                        WriteLine("Humidity {0}%", value);
                        Humidity = value;
                        break;
                    }
                    case Vallox.Variable.Co2High:
                    {
                        WriteLine("CO2 high {0}", value);
                        Co2High = value;
                        break;
                    }
                    case Vallox.Variable.Co2Low:
                    {
                        WriteLine("CO2 low {0}", value);
                        Co2Low = value;
                        break;
                    }
                    case Vallox.Variable.HumiditySensor1:
                    {
                        WriteLine("%RH1 {0}", value);
                        HumiditySensor1 = value;
                        break;
                    }
                    case Vallox.Variable.HumiditySensor2:
                    {
                        WriteLine("%RH2 {0}", value);
                        HumiditySensor2 = value;
                        break;
                    }
                    case Vallox.Variable.TempOutside:
                    {
                        int temperature = Vallox.ConvertTemp(value);
                        WriteLine("Temperature outside {0}°", temperature);
                        TempOutside = temperature;
                        break;
                    }
                    case Vallox.Variable.TempExhaust:
                    {
                        int temperature = Vallox.ConvertTemp(value);
                        WriteLine("Temperature exhaust {0}°", temperature);
                        TempExhaust = temperature;
                        break;
                    }
                    case Vallox.Variable.TempInside:
                    {
                        int temperature = Vallox.ConvertTemp(value);
                        WriteLine("Temperature inside {0}°", temperature);
                        TempInside = temperature;
                        break;
                    }
                    case Vallox.Variable.TempIncomming:
                    {
                        int temperature = Vallox.ConvertTemp(value);
                        WriteLine("Temperature incomming {0}°", temperature);
                        TempIncomming = temperature;
                        break;
                    }
                    case Vallox.Variable.Select:
                    {
                        WriteLine("Select {0}", value);

                        bool powerState;
                        bool co2AdjustState;
                        bool humidityAdjustState;
                        bool heatingState;
                        bool filterGuardIndicator;
                        bool heatingIndicator;
                        bool faultIndicator;
                        bool serviceReminderIndicator;

                        Vallox.ConvertSelect(value,
                            out powerState, out co2AdjustState, out humidityAdjustState, out heatingState,
                            out filterGuardIndicator, out heatingIndicator, out faultIndicator,
                            out serviceReminderIndicator);

                        PowerState = powerState;
                        Co2AdjustState = co2AdjustState;
                        HumidityAdjustState = humidityAdjustState;
                        HeatingState = heatingState;
                        FilterGuardIndicator = filterGuardIndicator;
                        HeatingIndicator = heatingIndicator;
                        FaultIndicator = faultIndicator;
                        ServiceReminderIndicator = serviceReminderIndicator;
                        break;
                    }
                    case Vallox.Variable.HeatingSetPoint:
                    {
                        int temperature = Vallox.ConvertTemp(value);
                        WriteLine("Heating set point temperature {0}°", temperature);
                        HeatingSetPoint = temperature;
                        break;
                    }
                    case Vallox.Variable.FanSpeedMax:
                    {
                        int fanSpeed = Vallox.ConvertFanSpeed(value);
                        WriteLine("Fan speed max {0}", fanSpeed);
                        FanSpeedMax = fanSpeed;
                        break;
                    }
                    case Vallox.Variable.ServiceReminder:
                    {
                        WriteLine("Service reminder {0}", value);
                        ServiceReminder = value;
                        break;
                    }
                    case Vallox.Variable.PreHeatingSetPoint:
                    {
                        int temperature = Vallox.ConvertTemp(value);
                        WriteLine("Pre heating set point {0}°", temperature);
                        PreHeatingSetPoint = temperature;
                        break;
                    }
                    case Vallox.Variable.InputFanStop:
                    {
                        int temperature = Vallox.ConvertTemp(value);
                        WriteLine("Input fan stop below temp {0}°", temperature);
                        InputFanStopThreshold = temperature;
                        break;
                    }
                    case Vallox.Variable.FanSpeedMin:
                    {
                        int fanSpeed = Vallox.ConvertFanSpeed(value);
                        WriteLine("Fan speed min {0}", fanSpeed);
                        FanSpeedMin = fanSpeed;
                        break;
                    }
                    case Vallox.Variable.Program:
                    {
                        WriteLine("Program {0}", value);

                        int adjustmentIntervalMinutes;
                        bool automaticHumidityLevelSeekerState;
                        Vallox.BoostSwitchMode boostSwitchMode;
                        Vallox.RadiatorType radiatorType;
                        bool cascadeAdjust;
                        Vallox.ConvertProgram(value, out adjustmentIntervalMinutes,
                            out automaticHumidityLevelSeekerState,
                            out boostSwitchMode, out radiatorType, out cascadeAdjust);

                        AdjustmentIntervalMinutes = adjustmentIntervalMinutes;
                        AutomaticHumidityLevelSeekerState = automaticHumidityLevelSeekerState;
                        BoostSwitchMode = boostSwitchMode;
                        RadiatorType = radiatorType;
                        CascadeAdjust = cascadeAdjust;
                        break;
                    }
                    case Vallox.Variable.BasicHumidityLevel:
                    {
                        WriteLine("Basic humidity level {0}%", value);
                        BasicHumidityLevel = value;
                        break;
                    }
                    case Vallox.Variable.HrcBypass:
                    {
                        int temperature = Vallox.ConvertTemp(value);
                        WriteLine("HRC bypass {0}°", temperature);
                        HrcBypassThreshold = temperature;
                        break;
                    }
                    case Vallox.Variable.DcFanInputAdjustment:
                    {
                        WriteLine("DC fan input adjustment {0}%", value);
                        DcFanInputAdjustment = value;
                        break;
                    }
                    case Vallox.Variable.DcFanOutputAdjustment:
                    {
                        WriteLine("DC fan output adjustment {0}%", value);
                        DcFanOutputAdjustment = value;
                        break;
                    }
                    case Vallox.Variable.CellDefrosting:
                    {
                        int temperature = Vallox.ConvertTemp(value);
                        WriteLine("Cell defrosting below temperature {0}°", temperature);
                        CellDefrostingThreshold = temperature;
                        break;
                    }
                    case Vallox.Variable.Co2SetPointUpper:
                    {
                        WriteLine("CO2 set point upper {0}", value);
                        Co2SetPointUpper = value;
                        break;
                    }
                    case Vallox.Variable.Co2SetPointLower:
                    {
                        WriteLine("CO2 set point lower {0}", value);
                        Co2SetPointLower = value;
                        break;
                    }
                    case Vallox.Variable.Program2:
                    {
                        WriteLine("Program2 {0}", value);
                        Vallox.MaxSpeedLimitMode maxSpeedLimitMode;
                        Vallox.ConvertProgram2(value, out maxSpeedLimitMode);
                        MaxSpeedLimitMode = maxSpeedLimitMode;
                        break;
                    }
                    case Vallox.Variable.Unknown:
                    {
                        WriteLine("Unkown at {0} {1}", DateTime.Now.ToShortTimeString(), value);
                        break;
                    }
                    case Vallox.Variable.Flags1:
                    {
                        WriteLine("Flags1 {0}", value);
                        break;
                    }
                    case Vallox.Variable.Flags2:
                    {
                        WriteLine("Flags2 {0}", value);
                        break;
                    }
                    case Vallox.Variable.Flags3:
                    {
                        WriteLine("Flags3 {0}", value);
                        break;
                    }
                    case Vallox.Variable.Flags4:
                    {
                        WriteLine("Flags4 {0}", value);
                        break;
                    }
                    case Vallox.Variable.Flags5:
                    {
                        WriteLine("Flags5 {0}", value);
                        break;
                    }
                    case Vallox.Variable.Flags6:
                    {
                        WriteLine("Flags6 {0}", value);
                        break;
                    }
                    case Vallox.Variable.IoPortFanSpeedRelays:
                    {
                        WriteLine("IoPortFanSpeedRelays {0}", value);
                        break;
                    }
                    case Vallox.Variable.IoPortMultiPurpose1:
                    {
                        WriteLine("IoPortMultiPurpose1 {0}", value);
                        break;
                    }
                    case Vallox.Variable.IoPortMultiPurpose2:
                    {
                        WriteLine("IoPortMultiPurpose2 {0}", value);
                        break;
                    }
                    case Vallox.Variable.MachineInstalledC02Sensor:
                    {
                        WriteLine("MachineInstalledC02Sensor {0}", value);
                        break;
                    }
                    case Vallox.Variable.PostHeatingOnCounter:
                    {
                        WriteLine("PostHeatingOnCounter {0}", value);
                        break;
                    }
                    case Vallox.Variable.PostHeatingOffTime:
                    {
                        WriteLine("PostHeatingOffTime {0}", value);
                        break;
                    }
                    case Vallox.Variable.PostHeatingTargetValue:
                    {
                        WriteLine("PostHeatingTargetValue {0}", value);
                        break;
                    }
                    case Vallox.Variable.FirePlaceBoosterCounter:
                    {
                        WriteLine("FirePlaceBoosterCounter {0}", value);
                        break;
                    }
                    case Vallox.Variable.LastErrorNumber:
                    {
                        WriteLine("LastErrorNumber {0}", value);
                        break;
                    }
                    case Vallox.Variable.MaintenanceMonthCounter:
                    {
                        WriteLine("MaintenanceMonthCounter {0}", value);
                        break;
                    }
                    case Vallox.Variable.SuspendBus:
                    {
                        WriteLine("Suspend bus for C02 sensor cummunication at {0} {1}",
                            DateTime.Now.ToShortTimeString(), value);
                        break;
                    }
                    case Vallox.Variable.ResumeBus:
                    {
                        WriteLine("Resume bus for all devices at {0} {1}", DateTime.Now.ToShortTimeString(), value);
                        break;
                    }

                    default:
                    {
                        WriteLine("Unknown command {0:X02} {1}", command, value);
                        break;
                    }
                }
            }
            else
            {
                if (command == 0)
                {
                    string variable = Vallox.ConvertVariable(value);
                    WriteLine("{0:X02} --> {1:X02}: get {2}", senderString, receiverString, variable);
                }
                else
                {
                    string variable = Vallox.ConvertVariable(command);
                    WriteLine("{0:X02} --> {1:X02}: set {2} = 0x{3:X02}", senderString, receiverString, variable, value);
                }
            }
        }

        private ValloxVariable GetVariableItem(byte variable)
        {
            ValloxVariable foundVariable = null;
            foreach (ValloxVariable valloxVariable in _variables)
            {
                if (valloxVariable.Id == variable)
                {
                    foundVariable = valloxVariable;
                    break;
                }
            }

            if (foundVariable == null)
            {
                string description = GetVariableDescription(variable);
                foundVariable = new ValloxVariable(variable, description);
                _variables.Add(foundVariable);
            }

            return foundVariable;
        }

        private string GetVariableDescription(byte variable)
        {
            string description;

            if (!Vallox.VariableNames.TryGetValue(variable, out description))
            {
                description = "unknown";
            }

            return description;
        }

        private void UpdateStatistics(string name, byte id, bool tx, bool rx)
        {
            Statistics nameStat = null;

            foreach (Statistics stat in DetectedDevices)
            {
                if (stat.Name == name)
                {
                    nameStat = stat;
                    break;
                }
            }

            if (nameStat == null)
            {
                nameStat = new Statistics(name, id);
                DetectedDevices.Add(nameStat);
            }

            if (tx)
            {
                nameStat.TxCount++;
            }

            if (rx)
            {
                nameStat.RxCount++;
            }
        }

        public static void WriteLine(string format, params object[] args)
        {
            Debug.WriteLine(format, args);
        }

        public static void Write(string format, params object[] args)
        {
            string msg = string.Format(format, args);
            Debug.Write(msg);
        }

        #endregion
    }
}