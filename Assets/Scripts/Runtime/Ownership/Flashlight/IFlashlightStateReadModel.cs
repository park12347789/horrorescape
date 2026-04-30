public interface IFlashlightStateReadModel
{
    bool HasFlashlight { get; }
    bool IsFlashlightEnabled { get; }
    float ChargeNormalized { get; }
    int StoredBatteryCount { get; }
    bool IsFullCharge { get; }
}
