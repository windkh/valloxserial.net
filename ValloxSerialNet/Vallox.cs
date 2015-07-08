using System;
namespace ValloxSerialNet
{
    /// <summary>
    /// Static functions and definitions of the vallox protocol
    /// 
    /// 01 21 11 00 A3 C9
    /// |  |  |  |  |  |
    /// |  |  |  |  |  CRC
    /// |  |  |  |  Get = Variable, Set = value
    /// |  |  |  Get = 0, >1 = variable
    /// |  |  Receiver
    /// |  Sender 11 = mainboard 21.. control panels 10 boroadcast to mainboards, 20 broadcast to panels
    /// Domain always 1
    /// 
    /// Special telegram (two times) for suspending the bus as the CO2 sensor communication follows another protocol.
    /// 01 11 20 91 0 C3   (91 = SUSPEND)
    /// 01 11 20 9F 0 C3   (8F = RESUME) 
    /// 
    /// Master is always 11
    /// Panels 21-29
    /// LON is always 28
    /// 
    /// RH% sensor value: (x-51)/2.04
    /// 
    /// Näherung im "linearen" Bereich 0x73: Temperatur: 0x73 = dez 115 --> Temp = (115-100)/3 = 5 Grad Celsius 
    /// </summary>
    internal class Vallox
    {
        #region VALLOX_PROTOCOL

        public const int TelegramLength = 6; // always 6
        public const int Domain = 1; // always 1

        public class Adress
        {
            public const int Mainboards = 0x10;
            public const int Panels = 0x20;

            // Addresses for sender and receiver
            public const int Master = Mainboards + 1;


            public const int Panel1 = Panels + 1;
            public const int Panel2 = Panels + 2;
            public const int Panel3 = Panels + 3;
            public const int Panel4 = Panels + 4;
            public const int Panel5 = Panels + 5;
            public const int Panel6 = Panels + 6;
            public const int Panel7 = Panels + 7;
            public const int Lon = Panels + 8;
            public const int Panel8 = Panels + 9;
        }

        public class Variable
        {
            public const int FanSpeed = 0x29;

            // Variables 
            public const int Humidity = 0x2A; // higher measured relative humidity from 2F and 30. Translating Formula (x-51)/2.04
            public const int Co2High = 0x2B;
            public const int Co2Low = 0x2C;

            public const int HumiditySensor1 = 0x2F; // sensor value: (x-51)/2.04
            public const int HumiditySensor2 = 0x30; // sensor value: (x-51)/2.04

            public const int TempOutside = 0x32;
            public const int TempExhaust = 0x33;
            public const int TempInside = 0x34;
            public const int TempIncomming = 0x35;


            // This variable is cyclically polled from the panel but not described in the protocol: ping every minute?
            public const int Unknown1 = 0x71;

            // Suspend Resume Traffic for CO2 sensor interaction: is sent twice as broadcast
            public const int SuspendBus = 0x91;
            public const int ResumeBus = 0x8F;

            // 1 1 1 1 1 1 1 1
            // | | | | | | | |
            // | | | | | | | +- 0 Power state
            // | | | | | | +--- 1 CO2 Adjust state
            // | | | | | +----- 2 %RH adjust state
            // | | | | +------- 3 Heating state
            // | | | +--------- 4 Filterguard indicator
            // | | +----------- 5 Heating indicator
            // | +------------- 6 Fault indicator
            // +--------------- 7 service reminder
            public const int Select = 0xA3;
            public const int HeatingSetPoint = 0xA4;
            public const int FanSpeedMax = 0xA5;
            public const int ServiceReminder = 0xA6;
            public const int PreHeatingSetPoint = 0xA7;

            public const int InputFanStop = 0xA8;  // Temp threshold: fan stops if input temp falls below this temp.

            public const int FanSpeedMin = 0xA9;

            // 1 1 1 1 1 1 1 1
            // | | | | _______
            // | | | |     |  
            // | | | |     +--- 0-3 set adjustment interval of CO2 and %RH in minutes 
            // | | | |   
            // | | | |   
            // | | | | 
            // | | | +--------- 4 automatic RH basic level seeker state
            // | | +----------- 5 boost switch modde (1=boost, 0 = fireplace)
            // | +------------- 6 radiator type 0 = electric, 1 = water
            // +--------------- 7 cascade adjust 0 = off, 1 = on
            public const int Program = 0xAA;

            public const int BasicHumidityLevel = 0xAE;
            public const int HrcBypass = 0xAF; // Heat recovery cell bypass setpoint temp
            public const int DcFanInputAdjustment = 0xB0; // %
            public const int DcFanOutputAdjustment = 0xB1; // %

            public const int CellDefrosting = 0xB2; // Defrosting starts when exhaust air drops below this setpoint temp 

            public const int Co2SetPointUpper = 0xB3;
            public const int Co2SetPointLower = 0xB4;


            // 1 1 1 1 1 1 1 1
            // | | | | | | | |
            // | | | | | | | +- 0 Function of max speed limit 0 = with adjustment, 1 = always 
            // | | | | | | +--- 1  
            // | | | | | +----- 2
            // | | | | +------- 3
            // | | | +--------- 4
            // | | +----------- 5
            // | +------------- 6
            // +--------------- 7
            public const int Program2 = 0xB5;

            // This one is queried at startup and answered with 3 but not described in the protocol: version?
            public const int Unknown2 = 0xC0;
        }

       

        // VALLOX_VARIABLE_PROGRAM2
        public enum MaxSpeedLimitMode
        {
            WithAdjustment,
            Always
        }

        // VALLOX_VARIABLE_PROGRAM
        public enum BoostSwitchMode
        {
            Boost,
            Fireplace
        }

        // VALLOX_VARIABLE_PROGRAM
        public enum RadiatorType
        {
            Electric,
            Water
        }

        public static Byte[] FanSpeedMapping =
        {
            0x01,
            0x03,
            0x07,
            0x0F,
            0x1F,
            0x3F,
            0x7F,
            0xFF
        };

        public static int[] TemperatureMapping =
        {
            -74,-70,-66,-62,-59,-56,-54,-52, 
            -50,-48,-47,-46,-44,-43,-42,-41,
            -40,-39,-38,-37,-36,-35,-34,-33,
            -33,-32,-31,-30,-30,-29,-28,-28,
            -27,-27,-26,-25,-25,-24,-24,-23,
            -23,-22,-22,-21,-21,-20,-20,-19,
            -19,-19,-18,-18,-17,-17,-16,-16,
            -16,-15,-15,-14,-14,-14,-13,-13,
            -12,-12,-12,-11,-11,-11,-10,-10,
            -09,-09,-09,-08,-08,-08,-07,-07,
            -07,-06,-06,-06,-05,-05,-05,-04,
            -04,-04,-03,-03,-03,-02,-02,-02,
            -01,-01,-01,-01, 00, 00, 00, 01, 
             01, 01, 02, 02, 02, 03, 03, 03,
             04, 04, 04, 05, 05, 05, 05, 06,
             06, 06, 07, 07, 07, 08, 08, 08,
             09, 09, 09, 10, 10, 10, 11, 11,
             11, 12, 12, 12 ,13, 13, 13, 14,
             14, 14, 15, 15, 15, 16, 16, 16,
             17, 17, 18, 18, 18, 19, 19, 19, 
             20, 20, 21, 21, 21, 22, 22, 22,
             23, 23, 24, 24, 24, 25, 25, 26,
             26, 27, 27, 27, 28, 28, 29, 29, 
             30, 30, 31, 31, 32, 32, 33, 33, 
             34, 34, 35, 35 ,36, 36, 37, 37, 
             38, 38, 39, 40, 40, 41, 41, 42,
             43, 43, 44, 45, 45, 46, 47, 48,
             49, 49, 50, 51, 52, 53, 53, 54, 
             55, 56, 57, 59, 60, 61, 62, 63,
             65, 66, 68, 69, 71, 73, 75, 77, 
             79, 81, 82, 86, 90, 93, 97, 100,
             100, 100, 100, 100, 100, 100, 100, 100
        };

        #endregion

        #region Statics

        // 1 1 1 1 1 1 1 1
        // | | | | | | | |
        // | | | | | | | +- 0 Power state
        // | | | | | | +--- 1 CO2 Adjust state
        // | | | | | +----- 2 %RH adjust state
        // | | | | +------- 3 Heating state
        // | | | +--------- 4 Filterguard indicator
        // | | +----------- 5 Heating indicator
        // | +------------- 6 Fault indicator
        // +--------------- 7 service reminder
        public static void ConvertSelect(Byte select, out bool powerState, out bool co2AdjustState, out bool humidityAdjustState, out bool heatingState, out bool filterGuardIndicator, out bool heatingIndicator, out bool faultIndicator, out bool serviceReminderIndicator)
        {
            powerState = (select & 0x01) != 0;
            co2AdjustState = (select & 0x02) != 0;
            humidityAdjustState = (select & 0x04) != 0;
            heatingState = (select & 0x08) != 0;
            filterGuardIndicator = (select & 0x10) != 0;
            heatingIndicator = (select & 0x20) != 0;
            faultIndicator = (select & 0x40) != 0;
            serviceReminderIndicator = (select & 0x80) != 0;
        }

        // 1 1 1 1 1 1 1 1
        // | | | | _______
        // | | | |     |  
        // | | | |     +--- 0-3 set adjustment interval of CO2 and %RH in minutes 
        // | | | |   
        // | | | |   
        // | | | | 
        // | | | +--------- 4 automatic RH basic level seeker state
        // | | +----------- 5 boost switch modde (1=boost, 0 = fireplace)
        // | +------------- 6 radiator type 0 = electric, 1 = water
        // +--------------- 7 cascade adjust 0 = off, 1 = on
        public static void ConvertProgram(Byte program, out int adjustmentIntervalMinutes, out bool automaticHumidityLevelSeekerState, out BoostSwitchMode boostSwitchMode, out RadiatorType radiatorType, out bool cascadeAdjust)
        {
            adjustmentIntervalMinutes = program & 0x0F;

            automaticHumidityLevelSeekerState = (program & 0x10) != 0;
            if ((program & 0x20) == 0)
            {
                boostSwitchMode = BoostSwitchMode.Fireplace;
            }
            else
            {
                boostSwitchMode = BoostSwitchMode.Boost;
            }

            if ((program & 0x40) == 0)
            {
                radiatorType = RadiatorType.Electric;
            }
            else
            {
                radiatorType = RadiatorType.Water;
            }

            cascadeAdjust = (program & 0x80) != 0;
        }

        // 1 1 1 1 1 1 1 1
        // | | | | | | | |
        // | | | | | | | +- 0 Function of max speed limit 0 = with adjustment, 1 = always 
        // | | | | | | +--- 1  
        // | | | | | +----- 2
        // | | | | +------- 3
        // | | | +--------- 4
        // | | +----------- 5
        // | +------------- 6
        // +--------------- 7
        public static void ConvertProgram2(Byte program2, out MaxSpeedLimitMode maxSpeeLimitMode)
        {
            if ((program2 & 0x01) == 0)
            {
                maxSpeeLimitMode = MaxSpeedLimitMode.WithAdjustment;
            }
            else
            {
                maxSpeeLimitMode = MaxSpeedLimitMode.WithAdjustment;
            }
        }

        // to readable string
        public static string ConvertAddress(Byte sender)
        {
            string address = string.Format("{0}", sender);
            switch (sender)
            {
                case Adress.Mainboards:
                    address = "Mainboards";
                    break;

                case Adress.Master:
                    address = "Master";
                    break;

                case Adress.Panels:
                    address = "Panels";
                    break;

                case Adress.Panel1:
                    address = "Panel1";
                    break;

                case Adress.Panel2:
                    address = "Panel2";
                    break;

                case Adress.Panel3:
                    address = "Panel3";
                    break;

                case Adress.Panel4:
                    address = "Panel4";
                    break;

                case Adress.Panel5:
                    address = "Panel5";
                    break;

                case Adress.Panel6:
                    address = "Panel6";
                    break;

                case Adress.Panel7:
                    address = "Panel7";
                    break;

                case Adress.Lon:
                    address = "LON";
                    break;

                case Adress.Panel8:
                    address = "Panel8";
                    break;
            }

            return address;
        }

        // to readable string
        public static string ConvertVariable(Byte command)
        {
            string variable = string.Format("{0:X02}", command);
            switch (command)
            {
                case Variable.FanSpeed:
                    {
                        variable = "Fan speed";
                        break;
                    }

                case Variable.Humidity:
                    {
                        variable = "Humidity";
                        break;
                    }

                case Variable.Co2High:
                    {
                        variable = "CO2 high";
                        break;
                    }
                case Variable.Co2Low:
                    {
                        variable = "CO2 low";
                        break;
                    }

                case Variable.HumiditySensor1:
                    {
                        variable = "Humidity sensor 1";
                        break;
                    }
                case Variable.HumiditySensor2:
                    {
                        variable = "Humidity sensor 2";
                        break;
                    }

                case Variable.TempOutside:
                    {
                        variable = "Temp outside";
                        break;
                    }
                case Variable.TempExhaust:
                    {
                        variable = "Temp exhaust";
                        break;
                    }
                case Variable.TempInside:
                    {
                        variable = "Temp inside";
                        break;
                    }
                case Variable.TempIncomming:
                    {
                        variable = "Temp incomming";
                        break;
                    }
                case Variable.Select:
                    {
                        variable = "Select";
                        break;
                    }
                case Variable.HeatingSetPoint:
                    {
                        variable = "Heating set point";
                        break;
                    }
                case Variable.FanSpeedMax:
                    {
                        variable = "Fan speed max";
                        break;
                    }
                case Variable.ServiceReminder:
                    {
                        variable = "Service reminder";
                        break;
                    }
                case Variable.PreHeatingSetPoint:
                    {
                        variable = "Pre heating set point";
                        break;
                    }
                case Variable.InputFanStop:
                    {
                        variable = "Input fan speed stop temp";
                        break;
                    }
                case Variable.FanSpeedMin:
                    {
                        variable = "Fan speed min";
                        break;
                    }
                case Variable.Program:
                    {
                        variable = "Program";
                        break;
                    }
                case Variable.BasicHumidityLevel:
                    {
                        variable = "Basic humidity level";
                        break;
                    }
                case Variable.HrcBypass:
                    {
                        variable = "HRC bypass";
                        break;
                    }
                case Variable.DcFanInputAdjustment:
                    {
                        variable = "DC fan input adjustment";
                        break;
                    }
                case Variable.DcFanOutputAdjustment:
                    {
                        variable = "DC fan output adjustment";
                        break;
                    }
                case Variable.CellDefrosting:
                    {
                        variable = "Cell defrosting";
                        break;
                    }
                case Variable.Co2SetPointUpper:
                    {
                        variable = "CO2 set point upper";
                        break;
                    }
                case Variable.Co2SetPointLower:
                    {
                        variable = "CO2 set point lower";
                        break;
                    }
                case Variable.Program2:
                    {
                        variable = "Program2";
                        break;
                    }
                case Variable.Unknown1:
                    {
                        variable = "Ping";
                        break;
                    }
                case Variable.Unknown2:
                    {
                        variable = "Unknown2";
                        break;
                    }
            }

            return variable;
        }

        public static int ConvertTemp(Byte value)
        {
            return TemperatureMapping[value];
        }

        // 0xFF --> 8
        public static int ConvertFanSpeed(Byte value)
        {
            int fanSpeed = 0;

            for (int i = 0; i < 8; i++)
            {
                if (FanSpeedMapping[i] == value)
                {
                    fanSpeed = i+1;
                    break;
                }
            }

            return fanSpeed;
        }

        // 8 --> 0xFF
        public static Byte ConvertBackFanSpeed(int value)
        {
            Byte fanSpeed = FanSpeedMapping[value];
            return fanSpeed;
        }

        public static byte ComputeCheckSum(params byte[] bytes)
        {
            int checksum = 0;
            if (bytes != null && bytes.Length > 0)
            {
                foreach (byte b in bytes)
                {
                    checksum += b;
                }
            }
            return (byte)(checksum % 256);
        }

        // Domain is always 1 with checksum
        public static Byte[] CreateTelegram(Byte sender, Byte receiver, Byte variable, Byte value)
        {
            Byte[] telegram = new Byte[Vallox.TelegramLength] { Vallox.Domain, sender, receiver, variable, value, 0x00 };
            telegram[Vallox.TelegramLength - 1] = ComputeCheckSum(telegram);
            return telegram;
        }

        #endregion
    }
}
