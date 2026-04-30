public interface IFogVisibilityService : IPlayerVisionQuery2D
{
    void SetBypassEnabled(bool enabled);
    void ResetMemory();
}
