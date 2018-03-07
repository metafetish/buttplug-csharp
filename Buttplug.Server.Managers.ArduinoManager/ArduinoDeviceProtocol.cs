namespace Buttplug.Server.Managers.ArduinoManager
{
    public static class ArduinoDeviceProtocol
    {
        public enum SerialCommand : byte
        {
            Ack = 0x01,
            Enable = 0x02,
            Disable = 0x03,
            Speed = 0x04
        }
    }
}