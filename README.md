# Xamarin.iOS.EddystoneScanner
Eddystone scanner for Xamarin.iOS. 
Translated to C# from https://github.com/google/eddystone/tree/master/tools/ios-eddystone-scanner-sample/EddystoneScannerSampleSwift

# Example
```csharp
public class SampleViewController : UIViewController, BeaconScanner.BeaconScannerDelegate
{
    BeaconScanner beaconScanner;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        beaconScanner = new BeaconScanner
        {
            ScannerDelegate = this
        };
        beaconScanner.StartScanning();
    }

    public void DidFindBeacon(BeaconScanner beaconScanner, Eddystone.BeaconInfo beaconInfo)
    {
        Debug.WriteLine($"FIND: {beaconInfo.ToString()}");
    }

    public void DidLoseBeacon(BeaconScanner beaconScanner, Eddystone.BeaconInfo beaconInfo)
    {
        Debug.WriteLine($"LOST: {beaconInfo.ToString()}");
    }

    public void DidUpdateBeacon(BeaconScanner beaconScanner, Eddystone.BeaconInfo beaconInfo)
    {
        Debug.WriteLine($"UPDATE: {beaconInfo.ToString()}");
    }

    public void DidObserveURLBeacon(BeaconScanner beaconScanner, NSUrl url, int RSSI)
    {
        Debug.WriteLine($"URL SEEN: {url}, RSSI: {RSSI}");
    }
}
```
