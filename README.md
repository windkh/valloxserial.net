# valloxserial.net
Simple .Net application to monitor vallox serial bus activity.

Connect the RS485 bus of the vallox ventilation device to your computer
using a standard RS485 to USB Converter (2 wires A and B are needed).
Start the application, choose the right COM port and connect.
Now the application receives all messages from the bus and tries to interpret the data.
The received data is printed to the output window of visual studio, when started in debug mode.

This is only a beta! 
Use at your own risk. Note that connecting your computer to the RS485 bus could
damage the vallox device. Power down the device before connecting/disconnecting the bus.

Tested with vallox digit se.

![Settings Window](https://raw.github.com/windkh/valloxserial.net/blob/master/ValloxSerialNet.png)
