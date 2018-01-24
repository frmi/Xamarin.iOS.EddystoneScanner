using System;
using System.Collections.Generic;
using System.Diagnostics;
using CoreBluetooth;
using CoreFoundation;
using Foundation;
using static Xamarin.iOS.EddystoneScanner.Eddystone;

namespace Xamarin.iOS.EddystoneScanner
{
    public class BeaconScanner : NSObject
    {
        public interface BeaconScannerDelegate
        {
            void DidFindBeacon(BeaconScanner beaconScanner, BeaconInfo beaconInfo);
            void DidLoseBeacon(BeaconScanner beaconScanner, BeaconInfo beaconInfo);
            void DidUpdateBeacon(BeaconScanner beaconScanner, BeaconInfo beaconInfo);
            void DidObserveURLBeacon(BeaconScanner beaconScanner, NSUrl url, int RSSI);
        }

        public BeaconScannerDelegate ScannerDelegate { get; set; }

        CentralManagerDelegate managerDelegate;
        CBCentralManager centralManager;
        DispatchQueue beaconOperationsQueue = new DispatchQueue("beacon_dispatch_queue");
        bool shouldBeScanning = false;

        public BeaconScanner()
        {
            Init();

            managerDelegate = new CentralManagerDelegate(this);
            centralManager = new CBCentralManager(managerDelegate, beaconOperationsQueue);
            centralManager.Delegate = managerDelegate;
        }

        public void StartScanning()
        {
            beaconOperationsQueue.DispatchAsync(() => StartScanningSyncronized());
        }

        public void StopScanning()
        {
            centralManager.StopScan();
        }

        void StartScanningSyncronized()
        {
            if (centralManager.State != CBCentralManagerState.PoweredOn)
            {
                Debug.WriteLine($"CentralManager state is {centralManager.State}, cannot start scan");
                shouldBeScanning = true;
            } 
            else 
            {
                Debug.WriteLine("Starting to scan for Eddystones");
                var services = CBUUID.FromString("FEAA");
                var options = new NSDictionary(CBCentralManager.ScanOptionAllowDuplicatesKey, true);
                centralManager.ScanForPeripherals(services, options);
            }
        }

        private class CentralManagerDelegate : CBCentralManagerDelegate
        {
            BeaconScanner beaconScanner;
            Dictionary<string, Dictionary<string, object>> seenEddystoneCache = new Dictionary<string, Dictionary<string, object>>();
            Dictionary<NSUuid, NSData> deviceIDCache = new Dictionary<NSUuid, NSData>();
            private long onLostTimout = 15;

            public CentralManagerDelegate(BeaconScanner beaconScanner)
            {
                this.beaconScanner = beaconScanner;
            }

            public override void UpdatedState(CBCentralManager central)
            {
                if (central.State == CBCentralManagerState.PoweredOn && beaconScanner.shouldBeScanning)
                {
                    beaconScanner.StartScanningSyncronized();
                }
            }

            ///
            /// Core Bluetooth CBCentralManager callback when we discover a beacon. We're not super 
            /// interested in any error situations at this point in time.
            ///
            public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber RSSI)
            {
                var serviceData = advertisementData[CBAdvertisement.DataServiceDataKey] as NSDictionary;
                if (serviceData != null)
                {
                    var eft = Eddystone.FrameTypeForFrame(serviceData);

                    // If it's a telemetry frame, stash it away and we'll send it along with the next regular
                    // frame we see. Otherwise, process the UID frame.
                    if (eft == BeaconInfo.EddystoneFrameType.TelemetryFrameType)
                    {
                        var data = Eddystone.TelemetryDataForFrame(advertisementData);
                        if (deviceIDCache.ContainsKey(peripheral.Identifier))
                        {
                            deviceIDCache[peripheral.Identifier] = data;
                        } else {
                            deviceIDCache.Add(peripheral.Identifier, data);
                        }
                    }
                    else if (eft == BeaconInfo.EddystoneFrameType.UIDFrameType
                             || eft == BeaconInfo.EddystoneFrameType.EIDFrameType)
                    {
                        if (!deviceIDCache.ContainsKey(peripheral.Identifier)) 
                        {
                            Debug.WriteLine($"deviceIDCache does not contain key: {peripheral.Identifier}.");
                            return;
                        }

                        var telemetry = deviceIDCache[peripheral.Identifier];
                        var serviceUUID = CBUUID.FromString("FEAA");
                        var _RSSI = RSSI.Int32Value;

                        var beaconServiceData = serviceData[serviceUUID] as NSData;
                        if (beaconServiceData != null)
                        {
                            var beaconInfo = (eft == BeaconInfo.EddystoneFrameType.UIDFrameType) ?
                                Eddystone.BeaconInfoForUIDFrameData(beaconServiceData, telemetry, _RSSI) :
                                         Eddystone.BeaconInfoForEIDFrameData(beaconServiceData, telemetry, _RSSI);
                            if (beaconInfo != null)
                            {
                                // NOTE: At this point you can choose whether to keep or get rid of the telemetry
                                //       data. You can either opt to include it with every single beacon sighting
                                //       for this beacon, or delete it until we get a new / "fresh" TLM frame.
                                //       We'll treat it as "report it only when you see it", so we'll delete it
                                //       each time.
                                deviceIDCache.Remove(peripheral.Identifier);

                                if (seenEddystoneCache.ContainsKey(beaconInfo.BeaconId.ToString()))
                                {
                                    var value = seenEddystoneCache[beaconInfo.BeaconId.ToString()]?["onLostTimer"];
                                    if (value != null)
                                    {
                                        var timer = (DispatchTimer)value;
                                        timer.Reschedule();
                                    }

                                    this.beaconScanner.ScannerDelegate?.DidUpdateBeacon(beaconScanner, beaconInfo);
                                }
                                else
                                {
                                    // We've never seen this beacon before
                                    this.beaconScanner.ScannerDelegate?.DidFindBeacon(beaconScanner, beaconInfo);

                                    var onLostTimer = DispatchTimer.ScheduledDispatchTimer(this.onLostTimout,
                                                                                           DispatchQueue.MainQueue,
                                                                                           (DispatchTimer obj) =>
                                                                                           {
                                                                                               var cacheKey = beaconInfo.BeaconId.ToString();
                                                                                               if (seenEddystoneCache.ContainsKey(cacheKey))
                                                                                               {
                                                                                                   var beaconCache = seenEddystoneCache[cacheKey];
                                                                                                   var lostBeaconInfo = beaconCache["beaconInfo"] as BeaconInfo;
                                                                                                   if (beaconCache != null && lostBeaconInfo != null)
                                                                                                   {
                                                                                                       this.beaconScanner.ScannerDelegate?.DidLoseBeacon(beaconScanner, beaconInfo);
                                                                                                       seenEddystoneCache.Remove(beaconInfo.BeaconId.ToString());
                                                                                                   }
                                                                                               }
                                                                                           });
                                    var newMap = new Dictionary<string, object>()
                                        {
                                            {"beaconInfo", beaconInfo},
                                            {"onLostTimer", onLostTimer}
                                        };
                                    if (seenEddystoneCache.ContainsKey(beaconInfo.BeaconId.ToString()))
                                    {
                                        seenEddystoneCache[beaconInfo.BeaconId.ToString()] = newMap;
                                    } 
                                    else 
                                    {
                                        seenEddystoneCache.Add(beaconInfo.BeaconId.ToString(), newMap);
                                    }
                                }
                            }
                        }
                    }
                    else if (eft == BeaconInfo.EddystoneFrameType.URLFrameType)
                    {
                        var serviceUUID = CBUUID.FromString("FEAA");
                        var _RSSI = RSSI.Int32Value;
                        var beaconServiceData = serviceData[serviceUUID] as NSData;
                        if (beaconServiceData != null)
                        {
                            var url = Eddystone.ParseURLFromFrame(beaconServiceData);
                            if (url != null)
                            {
                                this.beaconScanner.ScannerDelegate?.DidObserveURLBeacon(beaconScanner, url, _RSSI);
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Unable to find service data; can't process Eddystone");
                }
            }
        }
    }
}
