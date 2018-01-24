using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using CoreBluetooth;
using Foundation;
using static Xamarin.iOS.EddystoneScanner.Eddystone.BeaconId;
using static Xamarin.iOS.EddystoneScanner.Eddystone.BeaconInfo;

namespace Xamarin.iOS.EddystoneScanner
{
    public static class Eddystone
    {
        ///
        /// BeaconID
        ///
        /// Uniquely identifies an Eddystone compliant beacon.
        ///
        public class BeaconId
        {
            public enum BeaconType
            {
                Eddystone,
                EddystoneEID
            }

            public BeaconType Type { get; private set; }
            public byte[] Id { get; private set; }
            public string Base64Representation { get; private set; }

            public BeaconId(BeaconType beaconType, byte[] beaconId)
            {
                this.Type = beaconType;
                this.Id = beaconId;
                this.Base64Representation = Convert.ToBase64String(beaconId);
            }

            public override string ToString()
            {
                if (Type == BeaconType.Eddystone || Type == BeaconType.EddystoneEID)
                {
                    var hexId = string.Concat(Id.Select(b => b.ToString("X2")).ToArray());
                    return $"BeaconID beacon: {hexId}";
                }

                return $"BeaconID with inbalid type {Type}";
            }

            private string HexBeaconId(byte[] beaconId)
            {
                var retval = "";
                foreach (var b in beaconId)
                {
                    var s = Convert.ToChar(b).ToString();
                    if (s.Length == 1)
                    {
                        s = "0" + s;
                    }
                    retval += s;
                }
                return retval;
            }

            public override bool Equals(object obj)
            {
                if (this == obj)
                {
                    return true;
                }
                var other = obj as BeaconId;
                if (other == null)
                {
                    return false;
                }

                return this.Type == other.Type &&
                           this.Id == other.Id;
            }
        }

        ///
        /// BeaconInfo
        ///
        /// Contains information fully describing a beacon, including its beaconID, transmission power,
        /// RSSI, and possibly telemetry information.
        ///
        public class BeaconInfo
        {

            public static byte EddystoneUIDFrameTypeID = 0x00;
            public static byte EddystoneURLFrameTypeID = 0x10;
            public static byte EddystoneTLMFrameTypeID = 0x20;
            public static byte EddystoneEIDFrameTypeID = 0x30;

            public enum EddystoneFrameType
            {
                UnknownFrameType,
                UIDFrameType,
                URLFrameType,
                TelemetryFrameType,
                EIDFrameType
            }

            public EddystoneFrameType FrameType
            {
                get;
                set;
            }

            public BeaconId BeaconId { get; private set; }
            public int TxPower { get; private set; }
            public int RSSI { get; private set; }
            public NSData Telemetry { get; private set; }

            public BeaconInfo(BeaconId beaconId, int txPower, int RSSI, NSData telemetry)
            {
                this.BeaconId = beaconId;
                this.TxPower = txPower;
                this.RSSI = RSSI;
                this.Telemetry = telemetry;
            }

            public override string ToString()
            {
                switch (BeaconId.Type)
                {
                    case BeaconType.Eddystone:
                        return $"Eddystone {BeaconId}, txPower: {TxPower}, RSSI: {RSSI}";
                    case BeaconType.EddystoneEID:
                        return $"Eddystone EID {BeaconId}, base64: {BeaconId.Base64Representation}, txPower: {TxPower}, RSSI: {RSSI}";
                }

                return $"BeaconType unknown {BeaconId}, txPower: {TxPower}, RSSI: {RSSI}";
            }
        }

        public static EddystoneFrameType FrameTypeForFrame(NSDictionary advertisementFrameList)
        {
            var uuid = CBUUID.FromString("FEAA");
            var frameData = advertisementFrameList[uuid] as NSData;
            if (frameData != null)
            {
                var count = frameData.Length;
                if (count > 1)
                {
                    var frameBytes = Enumerable.Repeat((byte)0, (int)count).ToArray();
                    Marshal.Copy(frameData.Bytes, frameBytes, 0, (int)count);
                    if (frameBytes[0] == BeaconInfo.EddystoneUIDFrameTypeID)
                    {
                        return BeaconInfo.EddystoneFrameType.UIDFrameType;
                    }
                    else if (frameBytes[0] == BeaconInfo.EddystoneTLMFrameTypeID)
                    {
                        return BeaconInfo.EddystoneFrameType.TelemetryFrameType;
                    }
                    else if (frameBytes[0] == BeaconInfo.EddystoneEIDFrameTypeID)
                    {
                        return BeaconInfo.EddystoneFrameType.EIDFrameType;
                    }
                    else if (frameBytes[0] == BeaconInfo.EddystoneURLFrameTypeID)
                    {
                        return BeaconInfo.EddystoneFrameType.URLFrameType;
                    }
                }
            }
            return EddystoneFrameType.UnknownFrameType;
        }

        public static NSData TelemetryDataForFrame(NSDictionary advertisementFrameList)
        {
            return advertisementFrameList[CBUUID.FromString("FEAA")] as NSData;
        }

        public static BeaconInfo BeaconInfoForUIDFrameData(NSData frameData, NSData telemetry, int RSSI)
        {
            var count = frameData.Length;
            if (count > 1)
            {
                var frameBytes = Enumerable.Repeat((byte)0, (int)count).ToArray();
                Marshal.Copy(frameData.Bytes, frameBytes, 0, (int)count);

                if (frameBytes[0] != BeaconInfo.EddystoneUIDFrameTypeID)
                {
                    Debug.WriteLine("Unexpected non UID Frame passed to BeaconInfoForUIDFrameData.");
                    return null;
                }
                else if (frameBytes.Length < 18)
                {
                    Debug.WriteLine("Frame Data for UID Frame unexpectedly truncated in BeaconInfoForUIDFrameData.");
                }

                var txPower = Convert.ToInt32(frameBytes[1]);
                var beaconId = frameBytes.Skip(2).Take(17).ToArray();
                var bid = new BeaconId(BeaconType.Eddystone, beaconId);
                return new BeaconInfo(bid, txPower, RSSI, telemetry);
            }

            return null;
        }

        public static BeaconInfo BeaconInfoForEIDFrameData(NSData frameData, NSData telemetry, int RSSI)
        {
            var count = frameData.Length;
            if (count > 1)
            {
                var frameBytes = Enumerable.Repeat((byte)0, (int)count).ToArray();
                Marshal.Copy(frameData.Bytes, frameBytes, 0, (int)count);

                if (frameBytes[0] != BeaconInfo.EddystoneEIDFrameTypeID)
                {
                    Debug.WriteLine("Unexpected non EID Frame passed to BeaconInfoForEIDFrameData.");
                    return null;
                }
                else if (frameBytes.Length < 10)
                {
                    Debug.WriteLine("Frame Data for EID Frame unexpectedly truncated in BeaconInfoForEIDFrameData.");
                }

                var txPower = Convert.ToInt32(frameBytes[1]);
                var beaconId = frameBytes.Skip(2).Take(10).ToArray();
                var bid = new BeaconId(BeaconType.EddystoneEID, beaconId);
                return new BeaconInfo(bid, txPower, RSSI, telemetry);
            }

            return null;
        }

        private static string URLPrefixFromByte(byte schemeID)
        {
            switch (schemeID)
            {
                case 0x00:
                    return "http://www.";
                case 0x01:
                    return "https://www.";
                case 0x02:
                    return "http://";
                case 0x03:
                    return "https://";
                default:
                    return null;
            }
        }

        public static NSUrl ParseURLFromFrame(NSData frameData)
        {
            var count = frameData.Length;
            if (count > 0)
            {
                var frameBytes = Enumerable.Repeat((byte)0, (int)count).ToArray();
                Marshal.Copy(frameData.Bytes, frameBytes, 0, (int)count);

                var URLPrefix = URLPrefixFromByte(frameBytes[2]);
                if (URLPrefix != null)
                {
                    var output = URLPrefix;
                    for (int i = 3; i < frameBytes.Length; i++)
                    {
                        var encoded = EncodedStringFromByte(frameBytes[i]);
                        if (encoded != null)
                        {
                            output += encoded;
                        }
                    }
                    return NSUrl.FromString(output);
                }
            }

            return null;
        }

        private static string EncodedStringFromByte(byte charVal)
        {
            switch (charVal)
            {
                case 0x00:
                    return ".com/";
                case 0x01:
                    return ".org/";
                case 0x02:
                    return ".edu/";
                case 0x03:
                    return ".net/";
                case 0x04:
                    return ".info/";
                case 0x05:
                    return ".biz/";
                case 0x06:
                    return ".gov/";
                case 0x07:
                    return ".com";
                case 0x08:
                    return ".org";
                case 0x09:
                    return ".edu";
                case 0x0a:
                    return ".net";
                case 0x0b:
                    return ".info";
                case 0x0c:
                    return ".biz";
                case 0x0d:
                    return ".gov";
                default:
                    return NSString.FromData(NSData.FromArray(new byte[] { charVal }), NSStringEncoding.UTF8);
            }
        }
    }
}
