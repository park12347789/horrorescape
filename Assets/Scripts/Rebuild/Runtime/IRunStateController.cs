public interface IRunStateController
{
    bool IsEscaped { get; }
    bool IsFailureModalShowing { get; }
    bool NotifyRunFailure(string caughtBy);
}
